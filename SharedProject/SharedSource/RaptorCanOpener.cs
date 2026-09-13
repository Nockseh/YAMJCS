using Barotrauma.Items.Components;
using HarmonyLib;

namespace YAMJCS;

internal static partial class PatchTargets {
    internal static MethodBase Attack_DoDamage =>
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

    internal static MethodBase Door_HasRequiredItems =>
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