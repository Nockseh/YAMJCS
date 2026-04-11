using System.Reflection;
using HarmonyLib;

namespace YAMJCS;

internal static class YAMJPatchTargets {
    public static MethodBase CharacterCreate() {
        return AccessTools.FirstMethod(
            typeof(Character),
            m => m.Name == "Create"
                 && m.IsStatic
                 && m.ReturnType == typeof(Character));
    }

    public static MethodBase CharacterIsHumanGetter() {
        return AccessTools.PropertyGetter(typeof(Character), nameof(Character.IsHuman))
               ?? throw new InvalidOperationException("Could not find Character.IsHuman getter");
    }

    public static MethodBase RescueAllIsValidTarget() {
        return AccessTools.Method(typeof(AIObjectiveRescueAll), "IsValidTarget", new[] { typeof(Character) })
               ?? throw new InvalidOperationException("Could not find AIObjectiveRescueAll.IsValidTarget(Character)");
    }
}

[HarmonyPatch]
internal static class CharacterCreatePatch {
    static MethodBase TargetMethod() => YAMJPatchTargets.CharacterCreate();

    static void Postfix(Character __result) {
        if (__result is null) {
            YAMJ.Log("harmony says char is null");
            return;
        }
        YAMJ.HandleCharacterCreated(__result);
    }
}

[HarmonyPatch] //i don't think i need this anymore
internal static class CharacterIsHumanPatch {
    static MethodBase TargetMethod() => YAMJPatchTargets.CharacterIsHumanGetter();

    static void Postfix(Character __instance, ref bool __result) {
        try {
            __result = __result || (__instance?.Params?.UseHumanAI ?? false);
        }
        catch (Exception ex) {
            YAMJ.Log($"CharacterIsHumanPatch failed: {ex}");
        }
    }
}

[HarmonyPatch] //exclude mudraptor as rescue target
internal static class AIObjectiveRescueAllPatch {
    static MethodBase TargetMethod() => YAMJPatchTargets.RescueAllIsValidTarget();

    static bool Prefix(Character target, ref bool __result) {
        try {
            if (YAMJ.IsMudraptor(target)) {
                __result = false;
                return false;
            }
        }
        catch (Exception ex) {
            YAMJ.Log($"AIObjectiveRescueAllPatch failed: {ex}");
        }

        return true;
    }
}