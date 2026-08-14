using Barotrauma.Items.Components;
using HarmonyLib;

namespace YAMJCS;

internal static partial class PatchTargets {
    public static MethodBase Controller_Select =>
        AccessTools.DeclaredMethod(
            typeof(Controller),
            nameof(Controller.Select),
            new[] { typeof(Character) }) ??
        throw new Exception("Controller.Select(Character) not found");
}

[HarmonyPatch] // gates periscope (turret) usage behind talent
internal static class PeriscopePatch {
    static MethodBase TargetMethod() => PatchTargets.Controller_Select;

    static bool Prefix(Controller __instance, Character activator, ref bool __result) {
        if (!YAMJ.IsPlayerRaptor(activator)) { return true; }
        if (!__instance.Item.HasTag(Tags.Periscope)) { return true; }
        if (YAMJ.HasTalent(activator, "YAMJGunner")) { return true; }

        #if CLIENT
            if (activator == Character.Controlled) {
                YAMJClient.ShowWarning(TextManager.Get("yamjMsg.cantUseItem").Value);
            }
        #endif
        
        __result = false;
        return false;
    }
}