using System.Reflection;
using Barotrauma.Items.Components;
using HarmonyLib;

namespace YAMJCS;

internal static class PatchTargets {
    public static MethodBase Character_Create_FromPrefab =>
        AccessTools.Method(
            typeof(Character),
            nameof(Character.Create),
            new[] {
                typeof(CharacterPrefab),
                typeof(Vector2),
                typeof(string),
                typeof(CharacterInfo),
                typeof(ushort),
                typeof(bool),
                typeof(bool),
                typeof(bool),
                typeof(RagdollParams),
                typeof(bool)
            }) ?? 
        throw new Exception("Character.Create(CharacterPrefab, ...) not found");

    public static MethodBase Repairable_CheckCharacterSuccess =>
        AccessTools.Method(
            typeof(Repairable),
            nameof(Repairable.CheckCharacterSuccess)
        ) ??
        throw new Exception("Repairable.CheckCharacterSuccess(...) not found");
}

[HarmonyPatch]
internal static class CharacterCreateFromPrefabPatch {
    static MethodBase TargetMethod() => PatchTargets.Character_Create_FromPrefab;

    static void Prefix(ref CharacterPrefab prefab, CharacterInfo? characterInfo) {
        if (!YAMJ.HasPlayerRaptorJob(characterInfo)) {
            return;
        }

        CharacterPrefab? mudraptorPrefab = YAMJ.FindMudraptorPrefab();
        if (mudraptorPrefab is null) {
            YAMJ.Log("Mudraptor prefab not found in Character.Create(CharacterPrefab, ...) prefix.");
            return;
        }

        prefab = mudraptorPrefab;
        YAMJ.Log("Redirected Character.Create() to playerRaptor spawn");
    }

    static void Postfix(Character? __result, CharacterInfo? characterInfo) {
        if (__result is null) return;
        if (!YAMJ.HasPlayerRaptorJob(characterInfo)) return;
        //GameMain.GameSession?.CrewManager?.AddCharacter(__result); //don't use this, copies to data then spawns from data

        CrewManager crewManager = GameMain.GameSession?.CrewManager;
        if (crewManager is null) return;
        //always says npc playRaptors are New hires but works good enough for now
        if (!GameMain.GameSession.CrewManager.GetCharacterInfos(true).Contains(__result.Info)) {
            GameMain.NetworkMember?.CreateEntityEvent(__result, new Character.AddToCrewEventData(__result.TeamID, __result.Inventory.AllItems));
            YAMJ.Log("Sent AddToCrew event for player raptor " + __result.Name);
        }
    }
}

[HarmonyPatch]
internal static class CheckRepairSuccess {
    static MethodBase TargetMethod() => PatchTargets.Repairable_CheckCharacterSuccess;

    static void Postfix(Character character, Item bestRepairItem, ref bool __result) {
        if (!__result) return;
        if (YAMJ.IsPlayerRaptor(character)) {
            if (!YAMJ.HasTalent(character, "YAMJCanUseTools")) {
                __result = false;
            }
        }
    }
}