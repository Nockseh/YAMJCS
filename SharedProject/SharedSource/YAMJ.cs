using Barotrauma.LuaCs.Data;
using HarmonyLib;
using Microsoft.Xna.Framework.Input;

namespace YAMJCS;

internal static partial class YAMJ
{
    // Constants //
    public const string PlayerRaptorSpecies = "Mudraptor_player";
    public const string PlayerRaptorHuskSpecies = "Mudraptor_playerhusk";
    public const string PlayerRaptorJobId = "PlayerMudraptorJob";
    public static readonly Identifier RaptorBabySpecies = "RaptorBaby".ToIdentifier();
    public static readonly Identifier RaptorBabyItemId = "raptorbabyitem".ToIdentifier();
    public static readonly Identifier RaptorOnlyTag = "yamjraptoronly".ToIdentifier();
    // vars
    public static AfflictionPrefab? HungerPrefab;
    public static AfflictionPrefab? EatingBuffPrefab;
    public static ItemPrefab? RaptorBabyItemPrefab;
    public static CharacterPrefab? PlayerRaptorPrefab;
    
    public static ILoggerService? LoggerService { get; set; }
    public static void Log(string message) {
        LoggerService?.Log($"[YAMJCS] {message}");
    }

    // Helper Functions //
    public static CharacterPrefab? FindPlayerRaptorPrefab() {
        if (PlayerRaptorPrefab is null) {
            PlayerRaptorPrefab = CharacterPrefab.FindBySpeciesName(PlayerRaptorSpecies.ToIdentifier());
            if (PlayerRaptorPrefab is null) {throw new Exception("critical error: PlayerRaptor species prefab not found.");}
        }
        return PlayerRaptorPrefab;
    }
    public static ItemPrefab? FindRaptorBabyItemPrefab() {
        if (RaptorBabyItemPrefab is null) {
            ItemPrefab.Prefabs.TryGet(RaptorBabyItemId, out ItemPrefab? prefab);
            RaptorBabyItemPrefab = prefab;
        }
        return RaptorBabyItemPrefab;
    }
    
    public static bool HasPlayerRaptorJob(CharacterInfo? characterInfo) {
        string? jobId = characterInfo?.Job?.Prefab?.Identifier.Value;
        return string.Equals(jobId, PlayerRaptorJobId, StringComparison.OrdinalIgnoreCase);
    }

    public static bool IsPlayerRaptor(Character? character) {
        if (character is null) return false;
        if (character.SpeciesName.Value == PlayerRaptorSpecies || character.SpeciesName.Value == PlayerRaptorHuskSpecies) {
            return true;
        } else {
            return false;
        }
    }
    public static bool IsRaptorBaby(Character? character) {
        return character is not null && character.SpeciesName == RaptorBabySpecies;
    }
    
    public static bool HasTalent(Character character, string talentId) {
        return HasTalent(character, talentId.ToIdentifier());
    }

    public static bool HasTalent(Character character, Identifier talentId) {
        HashSet<Identifier> talents = character.Info.UnlockedTalents;
        if (talents == null || talents.Count == 0) return false;
        if (talents.Contains(talentId.ToIdentifier())) {
            return true;
        } else {
            return false;
        }
    }

    public static bool HasAnyTalent(Character character, Identifier[] talentIds) {
        foreach (Identifier talent in talentIds) {
            if (HasTalent(character, talent)) { return true; }
        }
        return false;
    }
    
    //Shared
    public static void SharedDispose() {
        YAMJConfig.DisposeConfig();
        LoggerService = null;
    }

    public static void SharedOnLoadCompleted(IConfigService configService, IPluginManagementService pluginService) {
        YAMJConfig.InitializeConfig(configService, pluginService);
        YAMJConfig.ApplyConfigToTalentXml();
        YAMJConfig.RebuildTalentDescriptions();
    }
}