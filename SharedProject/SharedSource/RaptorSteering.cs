using Barotrauma.Items.Components;
using HarmonyLib;

namespace YAMJCS;

internal static partial class PatchTargets {
    internal static MethodBase Item_TryInteract =>
        AccessTools.DeclaredMethod(
            typeof(Item),
            nameof(Item.TryInteract),
            new[] {
                typeof(Character),
                typeof(bool),
                typeof(bool),
                typeof(bool)
            }) ??
        throw new Exception("Item.TryInteract(Character, bool, bool, bool) not found");
}

[HarmonyPatch] // gates nav terminals (any item with a Steering component) behind a talent
internal static class NavTerminalPatch {
    static MethodBase TargetMethod() => PatchTargets.Item_TryInteract;

    static bool Prefix(Item __instance, Character user, bool forceSelectKey, ref bool __result) {
        if (!YAMJ.IsPlayerRaptor(user)) { return true; }
        if (__instance.GetComponent<Steering>() is null) { return true; }
        if (user.HasTalent(BondCaptain.TalentId)) { return true; }

        #if CLIENT
            if (user == Character.Controlled && (forceSelectKey || user.IsKeyHit(InputType.Select))) {
                YAMJClient.ShowWarning(TextManager.Get("yamjMsg.cantSteer").Value);
            }
        #endif

        __result = false;
        return false;
    }
}