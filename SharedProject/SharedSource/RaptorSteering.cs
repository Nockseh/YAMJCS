using Barotrauma.Items.Components;
using HarmonyLib;

namespace YAMJCS;

internal static partial class PatchTargets {
    internal static MethodBase Steering_Select =>
        AccessTools.DeclaredMethod(
            typeof(Steering),
            nameof(Steering.Select),
            new[] {
                typeof(Character)
            }) ??
        throw new Exception("Steering.Select(Character) not found");
}

[HarmonyPatch] //gates access to steering components behind a talent
internal static class NavTerminalPatch {
    static MethodBase TargetMethod() => PatchTargets.Steering_Select;

    static bool Prefix(Character character, ref bool __result) {
        if (!YAMJ.IsPlayerRaptor(character)) { return true; }
        if (character.HasTalent(BondCaptain.TalentId)) { return true; }

        #if CLIENT
            if (character == Character.Controlled) {
                YAMJClient.ShowWarning(TextManager.Get("yamjMsg.cantSteer").Value);
            }
        #endif

        __result = false;
        return false;
    }
}