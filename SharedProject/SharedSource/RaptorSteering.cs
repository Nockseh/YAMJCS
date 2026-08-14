using Barotrauma.Items.Components;
using HarmonyLib;

namespace YAMJCS;

internal static partial class PatchTargets {
    // Steering inherits from ItemComponent as of Aug 13, 2026
    internal static MethodBase ItemComponent_HasRequiredItems =>
        AccessTools.Method(
            typeof(Steering),
            nameof(Steering.HasRequiredItems),
            new[] {
                typeof(Character),
                typeof(bool),
                typeof(LocalizedString)
            }) ??
        throw new Exception("ItemComponent.HasRequiredItems(Character, bool, LocalizedString");
}

internal class RaptorSteering {
    internal static Identifier TalentId = "YAMJBond_Captain".ToIdentifier();
}

[HarmonyPatch] //gates access to steering components behind a talent
internal static class NavTerminalPatch {
    static MethodBase TargetMethod() => PatchTargets.ItemComponent_HasRequiredItems;

    static void Postfix(ItemComponent __instance, Character character, ref bool __result) {
        if (__result == false) return;
        if (__instance is not Steering) return;
        if (!YAMJ.IsPlayerRaptor(character)) return;
        if (YAMJ.HasTalent(character, RaptorSteering.TalentId)) return;
        #if CLIENT
            if (character == Character.Controlled) {
                YAMJClient.ShowWarning(TextManager.Get("yamjMsg.cantSteer").Value);
            }
        #endif
        __result = false;
    }
}