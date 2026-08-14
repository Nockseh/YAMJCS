using Barotrauma.Items.Components;
using HarmonyLib;

namespace YAMJCS;

internal static partial class PatchTargets {
    public static MethodBase Attack_DoDamage =>
        AccessTools.Method(
            typeof(Attack),
            nameof(Attack.DoDamage),
            new[] {
                typeof(Character),
                typeof(IDamageable),
                typeof(Vector2),
                typeof(float),
                typeof(bool),
                typeof(PhysicsBody),
                typeof(Limb)
            }) ??
        throw new Exception("Attack.DoDamage(Character, IDamageable, Vector2, float, bool, PhysicsBody, Limb) not found");

    public static MethodBase Door_HasRequiredItems =>
        AccessTools.DeclaredMethod(
            typeof(Door),
            nameof(Door.HasRequiredItems),
            new[] {
                typeof(Character),
                typeof(bool),
                typeof(LocalizedString)
            }) ??
        throw new Exception("Items.Components.Door.HasRequiredItems(Character, bool, LocalizedString) not found");
}

internal static class RaptorCanOpener {
    internal static readonly Identifier TalentId = "YAMJCanOpener".ToIdentifier();
}
    
[HarmonyPatch] // can opener: deal more damage to structures
internal static class CanOpenerAttackPatch {
    private const float StructureDamageBonus = 3.00f; //todo: configurable

    static MethodBase TargetMethod() => PatchTargets.Attack_DoDamage;
    
    static void Prefix(Attack __instance, Character attacker, IDamageable target, Limb sourceLimb, out float? __state) {
        __state = null; //using __state again to restore default after modifying

        if (sourceLimb is null) { return; } // creature attacks only
        if (__instance.SourceItem is not null) { return; } // no item attacks (redundant?)

        Structure? structure = target as Structure;
        if (structure is null) { return; } //structures only
        
        if (!YAMJ.IsPlayerRaptor(attacker)) { return; }
        if (!YAMJ.HasTalent(attacker, RaptorCanOpener.TalentId)) { return; }
        
        __state = __instance.DamageMultiplier;
        __instance.DamageMultiplier *= 1f + StructureDamageBonus; //other damage mults exist (genes) so we mult the mult instead of add
    }

    static void Postfix(Attack __instance, float? __state) {
        if (__state.HasValue) {
            __instance.DamageMultiplier = __state.Value; //restore default
        }
    }
}


[HarmonyPatch] // can opener: open doors as if you had a crowbar
internal static class CanOpenerCrowbarPatch {
    private static MethodBase TargetMethod() => PatchTargets.Door_HasRequiredItems;

    static void Postfix(Door __instance, Character character, ref bool __result) {
        if (__result) { return; }
        if (__instance.IsBroken) { return; }

        if (!YAMJ.IsPlayerRaptor(character)) { return; }
        if (!YAMJ.HasTalent(character, RaptorCanOpener.TalentId)) { return; }
        
        if (__instance.IsOpen) {
            __instance.Msg = "yamjHint.forceCloseBeak";
        } else {
            __instance.Msg = "yamjHint.forceOpenBeak";
        }
        __instance.ParseMsg();
        __result = true;
    }
}