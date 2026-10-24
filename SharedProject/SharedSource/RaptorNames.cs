/*
using HarmonyLib;

namespace YAMJCS {

    internal static partial class PatchTargets {
        internal static MethodBase CharacterInfo_GetName =>
            AccessTools.Method(
                typeof(CharacterInfo),
                "GetName",
                new[] {
                    typeof(Rand.RandSync),
                    typeof(string).MakeByRefType()
                }) ??
            throw new Exception("CharacterInfo.GetName(Rand.RandSync, out string) not found");
    }

    [HarmonyPatch] //makes raptor job CharacterInfos roll names from Mudraptor_player/names.xml
    internal static class RaptorNameGeneration {
        static MethodBase TargetMethod() => PatchTargets.CharacterInfo_GetName;

        static void Prefix(CharacterInfo __instance, out ContentXElement? __state) { //state is out
            __state = null; //allows returning without swapping anything
            if (!YAMJ.HasPlayerRaptorJob(__instance)) { return; }

            CharacterPrefab? raptorPrefab = YAMJ.FindPlayerRaptorPrefab();

            __state = __instance.CharacterConfigElement;
            __instance.CharacterConfigElement = raptorPrefab.ConfigElement;
        }

        static void Postfix(CharacterInfo __instance, ContentXElement? __state) {
            if (__state is null) { return; }
            __instance.CharacterConfigElement = __state; //return to original
        }
    }
}
*/