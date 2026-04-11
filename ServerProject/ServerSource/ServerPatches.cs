using System.Reflection;
using HarmonyLib;

namespace YAMJCS;

internal static class SpawnPatchTargets {
    public static MethodBase CreateFromCharacterInfo() =>
        AccessTools.Method(
            typeof(Character),
            "Create",
            new[] {
                typeof(CharacterInfo),
                typeof(Vector2),
                typeof(string),
                typeof(ushort),
                typeof(bool),
                typeof(bool),
                typeof(RagdollParams),
                typeof(bool)
            }) ?? throw new InvalidOperationException("Character.Create(CharacterInfo, ...) not found");

    public static MethodBase CreateFromStringSpecies() =>
        AccessTools.Method(
            typeof(Character),
            "Create",
            new[] {
                typeof(string),
                typeof(Vector2),
                typeof(string),
                typeof(CharacterInfo),
                typeof(ushort),
                typeof(bool),
                typeof(bool),
                typeof(bool),
                typeof(RagdollParams),
                typeof(bool),
                typeof(bool)
            }) ?? throw new InvalidOperationException("Character.Create(string, ...) not found");

    public static MethodBase CreateFromIdentifierSpecies() =>
        AccessTools.Method(
            typeof(Character),
            "Create",
            new[] {
                typeof(Identifier),
                typeof(Vector2),
                typeof(string),
                typeof(CharacterInfo),
                typeof(ushort),
                typeof(bool),
                typeof(bool),
                typeof(bool),
                typeof(RagdollParams),
                typeof(bool),
                typeof(bool)
            }) ?? throw new InvalidOperationException("Character.Create(Identifier, ...) not found");

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
internal static class CharacterCreateFromCharacterInfoPatch {
    static MethodBase TargetMethod() => SpawnPatchTargets.CreateFromCharacterInfo();

    static void Prefix(ref CharacterInfo characterInfo) {
        if (!YAMJ.HasPlayerRaptorJob(characterInfo)) {
            return;
        }

        var mudraptorPrefab = YAMJ.FindMudraptorPrefab();
        if (mudraptorPrefab is null) {
            YAMJ.Log("Mudraptor prefab not found in Character.Create(CharacterInfo, ...) prefix.");
            return;
        }

        // Rebuild the CharacterInfo using the mudraptor species, preserving the original job.
        characterInfo = new CharacterInfo(
            YAMJ.PlayerRaptorSpecies.ToIdentifier(),
            characterInfo.Name,
            characterInfo.OriginalName,
            characterInfo.Job
        ) {
            Salary = characterInfo.Salary,
            PermanentlyDead = characterInfo.PermanentlyDead,
            RenamingEnabled = characterInfo.RenamingEnabled,
            TeamID = characterInfo.TeamID,
            InventoryData = characterInfo.InventoryData,
            HealthData = characterInfo.HealthData,
            OrderData = characterInfo.OrderData,
            StartItemsGiven = characterInfo.StartItemsGiven,
            OmitJobInMenus = characterInfo.OmitJobInMenus,
            HumanPrefabIds = characterInfo.HumanPrefabIds,
            AdditionalTalentPoints = characterInfo.AdditionalTalentPoints,
            TalentRefundPoints = characterInfo.TalentRefundPoints,
            TalentResetCount = characterInfo.TalentResetCount,
            MinReputationToHire = characterInfo.MinReputationToHire
        };

        characterInfo.SetExperience(characterInfo.ExperiencePoints);

        YAMJ.Log("Redirected CharacterCreateFromCharacterInfo");
    }
}

[HarmonyPatch]
internal static class CharacterCreateFromStringSpeciesPatch {
    static MethodBase TargetMethod() => SpawnPatchTargets.CreateFromStringSpecies();

    static void Prefix(ref string speciesName, CharacterInfo? characterInfo) {
        if (!YAMJ.HasPlayerRaptorJob(characterInfo)) {
            return;
        }

        speciesName = YAMJ.PlayerRaptorSpecies;
        YAMJ.Log("Redirected CharacterCreateFromStringSpecies.");
    }
}

[HarmonyPatch]
internal static class CharacterCreateFromIdentifierSpeciesPatch {
    static MethodBase TargetMethod() => SpawnPatchTargets.CreateFromIdentifierSpecies();

    static void Prefix(ref Identifier speciesName, CharacterInfo? characterInfo) {
        if (!YAMJ.HasPlayerRaptorJob(characterInfo)) {
            return;
        }

        speciesName = YAMJ.PlayerRaptorSpecies.ToIdentifier();
        YAMJ.Log("Redirected CharacterCreateFromIdentifierSpecies.");
    }
}

[HarmonyPatch]
internal static class CharacterCreateFromPrefabPatch {
    static MethodBase TargetMethod() => SpawnPatchTargets.CreateFromPrefab();

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
        YAMJ.Log("Redirected CharacterCreateFromPrefab");
    }

    static void Postfix(Character? __result, CharacterInfo? characterInfo) {
        if (__result is null) return;
        if (!YAMJ.HasPlayerRaptorJob(characterInfo)) return;
        GameMain.GameSession?.CrewManager?.AddCharacter(__result);
        GameMain.NetworkMember?.CreateEntityEvent(__result, new Character.AddToCrewEventData(__result.TeamID, __result.Inventory.AllItems));
    }
}