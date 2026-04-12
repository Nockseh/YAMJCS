using System.Reflection;
using HarmonyLib;

namespace YAMJCS;

internal static class PatchTargets {
    public static MethodBase CreateFromPrefab() =>
        AccessTools.Method(
            typeof(Character),
            "Create",
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
            }) ?? throw new InvalidOperationException("Character.Create(CharacterPrefab, ...) not found");
}

[HarmonyPatch]
internal static class CharacterCreateFromPrefabPatch {
    static MethodBase TargetMethod() => PatchTargets.CreateFromPrefab();

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