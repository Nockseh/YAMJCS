# Harmony patching reference (YAMJCS)

Notes for writing `[HarmonyPatch]` classes in this mod.

**Important:** this project references **HarmonyX 2.16** (`Build.props`), the BepInEx fork that
Barotrauma ships — not Pardeike's `Lib.Harmony`. The API is the same, but a few behaviours differ.
Differences are flagged with **[HarmonyX]** below.

---

## 1. Anatomy of a patch class

```csharp
[HarmonyPatch]                                  // required: PatchAll() only scans classes with this
internal static class CharacterCanAimGet {
    static MethodBase TargetMethod() => PatchTargets.Character_CanAim_Get;   // what to patch

    static void Postfix(Character __instance, ref bool __result) {           // how to patch it
        ...
    }
}
```

Rules:

- The class needs `[HarmonyPatch]` or `Harmony.PatchAll(assembly)` will never see it.
  (`Plugin.cs` calls `HarmonyInstance.PatchAll(typeof(Plugin).Assembly)`.)
- **Patch methods must be `static`.** The class itself doesn't have to be, but ours all are.
- Patch methods are found by name (`Prefix`, `Postfix`, `Transpiler`, `Finalizer`, `Prepare`,
  `Cleanup`, `TargetMethod`, `TargetMethods`) *or* by attribute (`[HarmonyPrefix]`, `[HarmonyPostfix]`, …).
  Name-based is what we use everywhere.
- Keep one of each kind per class. If you need two prefixes on the same method, write two classes —
  it also lets you give them different priorities.
- Patching happens **once, at load**. If the target method can't be found, `PatchAll` throws and the
  whole mod fails to initialise. That's why `PatchTargets` uses `?? throw new Exception(...)` — it
  turns a null-ref deep inside Harmony into a message that names the missing method.

### Choosing the target

Two styles. This repo uses the second one everywhere.

```csharp
// A) attributes — concise, but the target is spread over the class
[HarmonyPatch(typeof(Character), nameof(Character.DoInteractionUpdate), new[] { typeof(float), typeof(Vector2) })]
internal static class Foo { static void Postfix(Character __instance) { } }

// B) TargetMethod() + a PatchTargets property — one place to fix when the game updates
[HarmonyPatch]
internal static class Foo {
    static MethodBase TargetMethod() => PatchTargets.Character_DoInteractionUpdate;
    static void Postfix(Character __instance) { }
}
```

Attribute forms worth knowing:

| Attribute | Targets |
|---|---|
| `[HarmonyPatch(typeof(T), nameof(T.M))]` | method `M` |
| `[HarmonyPatch(typeof(T), nameof(T.M), new[] { typeof(int) })]` | a specific overload |
| `[HarmonyPatch(typeof(T), nameof(T.P), MethodType.Getter)]` | property getter |
| `[HarmonyPatch(typeof(T), MethodType.Constructor, new Type[0])]` | parameterless ctor |
| `[HarmonyPatch(typeof(T), MethodType.StaticConstructor)]` | `static T()` |
| `[HarmonyPatch(typeof(T), nameof(T.M), MethodType.Enumerator)]` | the compiler-generated `MoveNext` of an iterator |
| `[HarmonyPatch(typeof(T), nameof(T.M), MethodType.Async)]` | the `MoveNext` of an async method |

### `AccessTools` cheat sheet (for `PatchTargets`)

```csharp
AccessTools.Method(typeof(T), "M")                    // includes methods inherited from base types
AccessTools.Method(typeof(T), "M", new[] { ... })     // disambiguate overloads — always do this if >1
AccessTools.DeclaredMethod(typeof(T), "M")            // only if T itself declares it; use when a base
                                                      // class has a same-named method you must not hit
AccessTools.PropertyGetter(typeof(T), "P")            // and PropertySetter
AccessTools.Constructor(typeof(T), new[] { ... })
AccessTools.Field(typeof(T), "f")                     // FieldInfo, for reflection not patching
AccessTools.TypeByName("Barotrauma.SomeInternalType") // types you can't name in source
AccessTools.Method("Barotrauma.Character:IsKeyHit")   // string form: Type:Method
AccessTools.EnumeratorMoveNext(AccessTools.Method(...))// the real body of an iterator method
```

`Method` walking the base chain is a real footgun: `AccessTools.Method(typeof(Repairable), "CreateGUI")`
could resolve to an inherited `ItemComponent.CreateGUI` and you'd silently patch every component in the
game. `DeclaredMethod` is the safe default when you mean "this exact class".

---

## 2. Prefix vs Postfix, and what `bool` vs `void` means

Execution order for a patched method:

```
prefixes (priority high → low)
   → original method (unless skipped)
      → postfixes (priority high → low)
         → finalizers
```

### Prefix — return type `void` or `bool`

| Return | Meaning |
|---|---|
| `void` | Observe/modify arguments, then always run the original. |
| `bool` | `true` = run the original. `false` = **skip the original method entirely.** |

Anything other than `void`/`bool` is a patch-time error.

When you return `false`:

- The original body never runs, so **you are responsible for `__result`**. If you don't set it, callers
  get `default(T)` — `null`, `0`, `false`. This is the #1 source of "why is everything null" bugs.
- **Postfixes still run**, both yours and other mods'. They will see whatever `__result` you left.
- Finalizers still run.
- **[HarmonyX]** Other prefixes still run too. Upstream Harmony skips the remaining prefixes once one
  returns `false`; HarmonyX deliberately runs all of them, and they use `bool __runOriginal` to find out
  whether the original is still going to execute. Don't assume returning `false` stops other mods'
  prefixes from firing.

Skip the original only when you really mean "this call does not happen". For a veto on a `bool`-returning
method it's almost always cleaner to use a **postfix** that flips `__result` to `false` — that way the
original's own logic and any other mod's opinion still get to run first. That's the pattern in
`CharacterCanAimGet` and `CharacterInventory.CanBePutInSlot`.

### Postfix — return type `void` or the original's return type

```csharp
// normal form — read/modify the result through __result
static void Postfix(Character __instance, ref bool __result) {
    if (!__result) { return; }          // already false, nothing to do
    __result = SomeCheck(__instance);
}

// pass-through form — first parameter IS the return value, and you return it
static bool Postfix(bool result, Character __instance) {
    return result && SomeCheck(__instance);
}
```

The pass-through form requires the postfix's return type to equal the original's return type, and its
**first parameter** to be that same type (name doesn't matter). It chains cleanly with other mods. Use it
when you're transforming a value; use `ref __result` otherwise. Don't do both.

A `void` postfix on a `void` method is the plain "run some code after this" case —
`CharHudAddToUpdateList` uses it as a per-frame tick.

### Finalizer — return type `void` or `Exception`

Runs even when the original threw. This is Harmony's `try/finally` + `catch`.

```csharp
static Exception Finalizer(Exception __exception, ref bool __result) {
    if (__exception is null) { return null; }
    YAMJ.Log($"swallowed: {__exception}");
    __result = false;
    return null;                 // null = exception suppressed; return it unchanged to rethrow
}
```

- `void` finalizer: exception (if any) continues to propagate.
- Returning `null`: suppresses the exception.
- Returning an `Exception`: replaces the one in flight.

### Transpiler — `IEnumerable<CodeInstruction>`

```csharp
static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions,
                                               ILGenerator generator,
                                               MethodBase original) { ... }
```

Rewrites IL. Only reach for it when a prefix/postfix genuinely can't do the job (changing something in the
*middle* of a method). It breaks on every game update, so if you write one, match on opcodes/operands
(`CodeMatcher`) rather than absolute indices, and log loudly if the pattern isn't found.

**[HarmonyX]** also offers `[HarmonyILManipulator]` (MonoMod `ILCursor`), which is far more pleasant than
raw `CodeInstruction` lists if you ever need IL work.

---

## 3. Linking a prefix to a postfix: `__state`

`__state` is the official channel between the prefix and the postfix **of the same patch class, for the
same call**. It's a local in the generated wrapper, so recursion and re-entrancy are safe.

```csharp
[HarmonyPatch]
internal static class ExampleStatePatch {
    static MethodBase TargetMethod() => PatchTargets.Something;

    // out (or ref) in the prefix — you must assign it on every path
    static void Prefix(Character __instance, out float __state) {
        __state = __instance.Vitality;
    }

    // by value in the postfix (use ref only if you also want to write it, which is pointless here)
    static void Postfix(Character __instance, float __state) {
        float delta = __instance.Vitality - __state;
        if (delta < 0f) { YAMJ.Log($"lost {-delta} vitality"); }
    }
}
```

Rules and gotchas:

- The type is whatever you want — a primitive, a struct, a class, a tuple. Both methods must declare the
  **same** type.
- Prefix: declare it `out T __state` (compiler then forces you to assign it) or `ref T __state`.
  Postfix: plain `T __state` is enough to read it.
- If the prefix doesn't run, or your prefix returns early, the postfix sees `default(T)`. Use a nullable
  or a sentinel if "didn't run" is a meaningful state.
- `__state` does **not** cross patch classes. Two separate `[HarmonyPatch]` classes each get their own.
  To share across classes, use a `static` field, or a `ConditionalWeakTable<Character, T>` if it's
  per-entity data (a plain static dictionary keyed on entities leaks).
- It also does not cross the client/server assembly boundary — those are two separate Harmony instances.

---

## 4. Injected `__` parameters

Parameters are matched **by name**, not position, so order them however reads best. Any of these can be
omitted if you don't need them.

| Name | Type | Where | Notes |
|---|---|---|---|
| `__instance` | the declaring type (or `object`) | Prefix, Postfix, Finalizer, Transpiler | The `this`. Not available on static methods. |
| `__result` | original's return type | Prefix, Postfix, Finalizer | Needs `ref` to write. In a prefix it's `default(T)` until you set it — only meaningful if you return `false`. |
| `__resultRef` | `ref RefResult<T>` | Prefix, Postfix | Only for methods declared `ref T Method()`. Rare in Barotrauma. |
| `__state` | anything | Prefix + Postfix | See above. `out`/`ref` in prefix. |
| `___fieldName` | the field's type | Prefix, Postfix | **Three** underscores. Reads/writes a private field on the instance (or a private static field). `ref` to write. |
| `__args` | `object[]` | Prefix, Postfix | All arguments. Writing into the array does propagate back to the real arguments. Boxes value types — prefer named `ref` parameters. |
| `__originalMethod` | `MethodBase` | Prefix, Postfix, Transpiler | Useful when one patch method serves several targets via `TargetMethods()`. |
| `__exception` | `Exception` | **Finalizer only** | `null` when the method completed normally. |
| `__runOriginal` | `bool` | Prefix, Postfix | **Read-only.** In a prefix: will the original run? In a postfix: did it? Return `false` from a `bool` prefix to actually skip; you can't skip by assigning this. |

Plus the original method's own parameters, matched by name:

```csharp
// original: bool CanBePutInSlot(Item item, int i, bool ignoreCondition)
static void Postfix(CharacterInventory __instance, Item item, int i, ref bool __result) { }
```

- Names are case-sensitive and must match the game's parameter names exactly.
- Add `ref` to modify an argument (visible to the original if you do it in a prefix).
- `out` parameters of the original are injected as `out`/`ref` too.
- Alternatives when the name is awkward or missing: `__0`, `__1`, … by zero-based index, or
  `[HarmonyArgument("realName")] Item myItem` / `[HarmonyArgument(0)]`.
- If the type isn't accessible from our assembly, declare the parameter as `object`.

---

## 5. Optional class members

```csharp
static bool Prepare(MethodBase original)          // return false to skip patching
static bool Prepare()                             // called once with null before the per-method calls
static IEnumerable<MethodBase> TargetMethods()    // patch many methods with one class
static Exception Cleanup(MethodBase original, Exception ex)  // return null to swallow a patch failure
```

`Prepare` is the clean way to make a patch conditional (e.g. only apply if some optional type exists);
returning `false` from the parameterless overload skips the entire class.

`Cleanup` returning `null` turns a hard startup crash into a soft "this one patch didn't apply" — worth
adding to patches targeting methods that other mods commonly rename or that may vanish in an update.

---

## 6. Ordering against other mods

```csharp
[HarmonyPatch]
[HarmonyPriority(Priority.Low)]      // First=800, VeryHigh=700, High=600, HigherThanNormal=500,
internal static class Foo { }        // Normal=400, LowerThanNormal=300, Low=200, VeryLow=100, Last=0
```

- **Higher priority runs first — for prefixes *and* postfixes.** So the patch that gets the *final*
  say over `__result` is the **lowest**-priority postfix. If another mod keeps overwriting your value,
  lower your priority rather than raising it.
- `[HarmonyBefore("some.other.mod.id")]` / `[HarmonyAfter("...")]` order relative to a specific mod id.
- Priority attributes go on the class (applies to all its patches) or on the individual patch method.

---

## 7. Barotrauma-specific notes

- **Client vs server.** `ClientPatches.cs` and `ServerPatches.cs` patch different assemblies and run
  under different `Harmony` instances (`Nockseh.YAMJCS.Client` / `.Server`). Anything GUI —
  `GUITextBox`, `CharacterHUD`, `ChatBox`, `Character.Controlled` — is client-only and must not be
  referenced from `SharedProject`. Shared patches must target methods that exist in both builds with the
  same signature; if a signature differs, split it.
- **Overloads change between updates.** Always pass the explicit `Type[]` to `AccessTools.Method` for any
  method with more than one overload — otherwise Harmony picks one non-deterministically.
- **Unpatching.** `Plugin.Dispose()` calls `HarmonyInstance?.UnpatchSelf()` because Barotrauma reloads
  mods without restarting. Any static state your patches accumulate (caches, dictionaries, event
  subscriptions, `Initialize()` flags) must be reset there too, or a reload leaves stale entries behind.
  **[HarmonyX]** the instance `UnpatchAll(string)` is obsolete — `UnpatchSelf()` is the correct call.
- **Never let a patch throw.** An exception in a prefix propagates into game code that isn't expecting it
  and can take down a whole frame or a network callback. Null-check everything the game hands you, and for
  anything risky add a `Finalizer` that logs and swallows.
- **Hot paths.** Anything on `Character.DoInteractionUpdate`, `CharacterHUD.Draw` or
  `AddToGUIUpdateList` runs per character per frame. Bail out on the cheapest check first
  (`if (!YAMJ.IsPlayerRaptor(__instance)) { return; }`) before touching inventories or tags.
- **Tiny methods may be inlined** by the JIT before Harmony patches them, in which case the patch simply
  never fires for those call sites. If a patch on a one-line property getter appears to do nothing, that's
  the usual cause — patch the caller instead.
- **Iterators/coroutines**: patching the declared method only intercepts the creation of the state
  machine, not the body. Use `MethodType.Enumerator` / `AccessTools.EnumeratorMoveNext`.

---

## 8. Recipes

**Veto a boolean result (preferred over skipping):**
```csharp
static void Postfix(Character __instance, ref bool __result) {
    if (!__result) { return; }
    if (!YAMJ.IsPlayerRaptor(__instance)) { return; }
    __result = false;
}
```

**Fully replace a method:**
```csharp
static bool Prefix(Character __instance, ref float __result) {
    if (!YAMJ.IsPlayerRaptor(__instance)) { return true; }   // vanilla behaviour
    __result = 1.0f;                                          // must set it — original won't
    return false;
}
```

**Rewrite an argument before the original sees it:**
```csharp
static void Prefix(ref IEnumerable<InvSlotType>? allowedSlots) {
    allowedSlots = Filter(allowedSlots);
}
```

**Read a private field:**
```csharp
static void Postfix(Character __instance, ref float ___lastAttackTime) {
    ___lastAttackTime = 0f;
}
```

**Wrap in try/catch:**
```csharp
static Exception Finalizer(Exception __exception) {
    if (__exception is not null) { YAMJ.Log($"patch target threw: {__exception}"); }
    return null;
}
```

---

## 9. Debugging

- `HarmonyFileLog.Enabled = true;` + `HarmonyFileLog.FileWriterPath = "...";` (**[HarmonyX]** — the
  upstream `Harmony.DEBUG` / `FileLog.logPath` pair is not the API here). Also
  `Logger.ChannelFilter = Logger.LogChannel.All;`.
- `[HarmonyDebug]` on a single patch class dumps the generated IL for just that patch.
- `Harmony.GetPatchInfo(methodBase)` at runtime tells you which mods have patched a method and in what
  order — the first thing to check when a patch "doesn't work" and you suspect a conflict.
- If `PatchAll` throws at startup, the message from our `?? throw new Exception("X not found")` in
  `PatchTargets` names the exact signature that stopped resolving.

---

### References
- Harmony docs — https://harmony.pardeike.net/
- HarmonyX differences — https://github.com/BepInEx/HarmonyX/wiki/Difference-between-Harmony-and-HarmonyX
