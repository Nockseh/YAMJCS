using System.Reflection;
using HarmonyLib;
using Microsoft.Xna.Framework;

namespace YAMJCS;

internal static class SpawnPatchTargets
{
    public static MethodBase CreateFromCharacterInfo() =>
        AccessTools.Method(
            typeof(Character),
            "Create",
            new[]
            {
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
            new[]
            {
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
            new[]
            {
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
            new[]
            {
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

/// Character.Create(CharacterInfo, ...)
/// This overload is the one where species must be inferred from CharacterInfo/job.
/// If the job is PlayerMudraptorJob, swap the CharacterInfo species before creation.
[HarmonyPatch]
internal static class CharacterCreateFromCharacterInfoPatch
{
    static MethodBase TargetMethod() => SpawnPatchTargets.CreateFromCharacterInfo();

    static void Prefix(ref CharacterInfo characterInfo)
    {
        if (!YAMJServer.HasMudraptorJob(characterInfo)) { return; }

        var mudraptorPrefab = YAMJServer.FindMudraptorPrefab();
        if (mudraptorPrefab is null)
        {
            YAMJServer.Log("Mudraptor prefab not found in Character.Create(CharacterInfo, ...) prefix.");
            return;
        }

        // Rebuild the CharacterInfo using the mudraptor species, preserving the original job.
        characterInfo = new CharacterInfo(
            YAMJServer.PlayerRaptorSpecies.ToIdentifier(),
            characterInfo.Name,
            characterInfo.OriginalName,
            characterInfo.Job
        )
        {
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

        YAMJServer.Log("Redirected Character.Create(CharacterInfo, ...) to Mudraptor_player.");
    }
}

/// Character.Create(string speciesName, ...)
[HarmonyPatch]
internal static class CharacterCreateFromStringSpeciesPatch
{
    static MethodBase TargetMethod() => SpawnPatchTargets.CreateFromStringSpecies();

    static void Prefix(ref string speciesName, CharacterInfo? characterInfo)
    {
        if (!YAMJServer.HasMudraptorJob(characterInfo)) { return; }

        speciesName = YAMJServer.PlayerRaptorSpecies;
        YAMJServer.Log("Redirected Character.Create(string, ...) to Mudraptor_player.");
    }
}

/// Character.Create(Identifier speciesName, ...)
[HarmonyPatch]
internal static class CharacterCreateFromIdentifierSpeciesPatch
{
    static MethodBase TargetMethod() => SpawnPatchTargets.CreateFromIdentifierSpecies();

    static void Prefix(ref Identifier speciesName, CharacterInfo? characterInfo)
    {
        if (!YAMJServer.HasMudraptorJob(characterInfo)) { return; }

        speciesName = YAMJServer.PlayerRaptorSpecies.ToIdentifier();
        YAMJServer.Log("Redirected Character.Create(Identifier, ...) to Mudraptor_player.");
    }
}

/// <summary>
/// Character.Create(CharacterPrefab prefab, ...)
/// </summary>
[HarmonyPatch]
internal static class CharacterCreateFromPrefabPatch
{
    static MethodBase TargetMethod() => SpawnPatchTargets.CreateFromPrefab();

    static void Prefix(ref CharacterPrefab prefab, CharacterInfo? characterInfo)
    {
        if (!YAMJServer.HasMudraptorJob(characterInfo)) { return; }

        CharacterPrefab? mudraptorPrefab = YAMJServer.FindMudraptorPrefab();
        if (mudraptorPrefab is null)
        {
            YAMJServer.Log("Mudraptor prefab not found in Character.Create(CharacterPrefab, ...) prefix.");
            return;
        }

        prefab = mudraptorPrefab;
        YAMJServer.Log("Redirected Character.Create(CharacterPrefab, ...) to Mudraptor_player.");
    }
}