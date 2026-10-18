using Barotrauma.LuaCs.Data;
using HarmonyLib;
using Microsoft.Xna.Framework.Input;

namespace YAMJCS;

internal static partial class YAMJ
{
    // Constants //
    public const string PlayerRaptorSpecies = "Mudraptor_player"; //todo convert to id
    public const string PlayerRaptorHuskSpecies = "Mudraptor_playerhusk"; //todo convert to id
    public const string PlayerRaptorJobId = "PlayerMudraptorJob"; //todo convert to id
    public static readonly Identifier HungerAfflictionId = "yamjHunger".ToIdentifier();
    public static readonly Identifier SatiatedAfflictionId = "yamjSatiated".ToIdentifier();
    public static readonly Identifier EatingBuffAfflictionId = "yamjEating".ToIdentifier();
    public static readonly Identifier OreInGutId = "yamjOreInGut".ToIdentifier();
    public static readonly Identifier MetalInGutId = "yamjMetalInGut".ToIdentifier();
    public static readonly Identifier RaptorBabySpecies = "RaptorBaby".ToIdentifier();
    public static readonly Identifier RaptorBabyItemId = "raptorbabyitem".ToIdentifier();
    public static readonly Identifier RaptorOnlyTag = "yamjraptoronly".ToIdentifier();
    public static readonly Identifier[] BasicFood = new Identifier[] {"food", "monsterfood", "plant"};
    public static readonly Identifier[] MechanicFood = new Identifier[] {"ore","scrap"};
    public static readonly Identifier[] EngineerFood = new Identifier[] {"reactorfuel"};
    public static readonly Identifier[] DoctorFood = new Identifier[] {"unidentifiedgeneticmaterial", "geneticmaterial"};
    public static readonly Identifier[] ShrapnelAmmo = new Identifier[] {"munition_core","munition_tip"};
    // vars
    private static AfflictionPrefab? hungerPrefab = null;
    private static AfflictionPrefab? satiatedPrefab = null;
    private static AfflictionPrefab? eatingBuffPrefab = null;
    private static AfflictionPrefab? oreInGutPrefab = null;
    private static AfflictionPrefab? metalInGutPrefab = null;
    public static AfflictionPrefab? HungerPrefab => hungerPrefab ??= FindAfflictionPrefab(HungerAfflictionId);
    public static AfflictionPrefab? SatiatedPrefab => satiatedPrefab ??= FindAfflictionPrefab(SatiatedAfflictionId);
    public static AfflictionPrefab? EatingBuffPrefab => eatingBuffPrefab ??= FindAfflictionPrefab(EatingBuffAfflictionId);
    public static AfflictionPrefab? OreInGutPrefab => oreInGutPrefab ??= FindAfflictionPrefab(OreInGutId);
    public static AfflictionPrefab? MetalInGutPrefab => metalInGutPrefab ??= FindAfflictionPrefab(MetalInGutId);
    
    public static ItemPrefab? RaptorBabyItemPrefab;
    public static CharacterPrefab? PlayerRaptorPrefab;
    
    public static ILoggerService? LoggerService { get; set; }
    public static void Log(string message) {
        LoggerService?.Log($"[YAMJCS] {message}");
    }

    // Helper Functions //
    public static AfflictionPrefab? FindAfflictionPrefab(Identifier id) {
        AfflictionPrefab.Prefabs.TryGet(id, out AfflictionPrefab? prefab);
        if (prefab is null) {
            Log($"Affliction prefab {id} not found");
        }
        return prefab;
    }
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
        if (talents.Contains(talentId)) {
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
        YAMJConfig.ApplyConfigToAfflictionXml();
        YAMJConfig.ApplyConfigToItemXml();
    }
}