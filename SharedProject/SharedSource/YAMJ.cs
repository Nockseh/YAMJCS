using Barotrauma.LuaCs.Data;
using HarmonyLib;
using Microsoft.Xna.Framework.Input;

namespace YAMJCS;

internal static partial class YAMJ
{
    // Constants //
    internal const string PlayerRaptorSpecies = "Mudraptor_player";
    internal static readonly Identifier PlayerRaptorSpeciesId = "Mudraptor_player".ToIdentifier();
    internal const string PlayerRaptorHuskSpecies = "Mudraptor_playerhusk"; //todo convert to id
    internal const string PlayerRaptorJobName = "playerMudraptorJob"; //todo convert to id
    internal static readonly Identifier PlayerRaptorJobId = "playerMudraptorJob".ToIdentifier();
    internal static readonly Identifier HungerAfflictionId = "yamjHunger".ToIdentifier();
    internal static readonly Identifier SatiatedAfflictionId = "yamjSatiated".ToIdentifier();
    internal static readonly Identifier EatingBuffAfflictionId = "yamjEating".ToIdentifier();
    internal static readonly Identifier OreInGutId = "yamjOreInGut".ToIdentifier();
    internal static readonly Identifier MetalInGutId = "yamjMetalInGut".ToIdentifier();
    internal static readonly Identifier RaptorBabySpecies = "RaptorBaby".ToIdentifier();
    internal static readonly Identifier RaptorBabyItemId = "raptorBabyItem".ToIdentifier();
    internal static readonly Identifier RaptorOnlyTag = "yamjraptoronly".ToIdentifier();
    internal static readonly Identifier[] BasicFood = new Identifier[] {"food", "monsterfood", "plant"};
    internal static readonly Identifier[] MechanicFood = new Identifier[] {"ore","scrap"};
    internal static readonly Identifier[] EngineerFood = new Identifier[] {"reactorfuel"};
    internal static readonly Identifier[] DoctorFood = new Identifier[] {"unidentifiedgeneticmaterial", "geneticmaterial"};
    internal static readonly Identifier[] ShrapnelAmmo = new Identifier[] {"munition_core","munition_tip"};
    // vars
    private static AfflictionPrefab? hungerPrefab = null;
    private static AfflictionPrefab? satiatedPrefab = null;
    private static AfflictionPrefab? eatingBuffPrefab = null;
    private static AfflictionPrefab? oreInGutPrefab = null;
    private static AfflictionPrefab? metalInGutPrefab = null;
    internal static AfflictionPrefab? HungerPrefab => hungerPrefab ??= FindAfflictionPrefab(HungerAfflictionId);
    internal static AfflictionPrefab? SatiatedPrefab => satiatedPrefab ??= FindAfflictionPrefab(SatiatedAfflictionId);
    internal static AfflictionPrefab? EatingBuffPrefab => eatingBuffPrefab ??= FindAfflictionPrefab(EatingBuffAfflictionId);
    internal static AfflictionPrefab? OreInGutPrefab => oreInGutPrefab ??= FindAfflictionPrefab(OreInGutId);
    internal static AfflictionPrefab? MetalInGutPrefab => metalInGutPrefab ??= FindAfflictionPrefab(MetalInGutId);
    
    internal static ItemPrefab? RaptorBabyItemPrefab;
    internal static CharacterPrefab? PlayerRaptorPrefab;
    
    internal static ILoggerService? LoggerService { get; set; }
    internal static void Log(string message) {
        LoggerService?.Log($"[YAMJCS] {message}");
    }

    // Helper Functions //
    internal static AfflictionPrefab? FindAfflictionPrefab(Identifier id) {
        AfflictionPrefab.Prefabs.TryGet(id, out AfflictionPrefab? prefab);
        if (prefab is null) {
            Log($"Affliction prefab {id} not found");
        }
        return prefab;
    }
    internal static CharacterPrefab? FindPlayerRaptorPrefab() {
        if (PlayerRaptorPrefab is null) {
            PlayerRaptorPrefab = CharacterPrefab.FindBySpeciesName(PlayerRaptorSpecies.ToIdentifier());
            if (PlayerRaptorPrefab is null) {throw new Exception("critical error: PlayerRaptor species prefab not found.");}
        }
        return PlayerRaptorPrefab;
    }
    internal static ItemPrefab? FindRaptorBabyItemPrefab() {
        if (RaptorBabyItemPrefab is null) {
            ItemPrefab.Prefabs.TryGet(RaptorBabyItemId, out ItemPrefab? prefab);
            RaptorBabyItemPrefab = prefab;
        }
        return RaptorBabyItemPrefab;
    }
    
    internal static bool HasPlayerRaptorJob(CharacterInfo? characterInfo) {
        Identifier? jobId = characterInfo?.Job?.Prefab?.Identifier;
        return jobId == PlayerRaptorJobId;
    }

    internal static bool IsPlayerRaptor(Character? character) {
        if (character is null) return false;
        if (character.SpeciesName == PlayerRaptorSpeciesId) {
            return true;
        } else {
            return false;
        }
    }
    internal static bool IsRaptorBaby(Character? character) {
        return character is not null && character.SpeciesName == RaptorBabySpecies;
    }
    
    internal static bool HasAnyTalent(Character character, Identifier[] talentIds) {
        foreach (Identifier talent in talentIds) {
            if (character.HasTalent(talent)) { return true; }
        }
        return false;
    }
    
    internal static void ApplyAtMaxStrength(Character character, AfflictionPrefab? prefab) {
        if (prefab is null) { return; }
        character.CharacterHealth.AddAffliction(prefab.Instantiate(prefab.MaxStrength * 1000f));
    }
    
    //Shared
    internal static void SharedDispose() {
        YAMJConfig.DisposeConfig();
        LoggerService = null;
    }

    internal static void SharedOnLoadCompleted(IConfigService configService, IPluginManagementService pluginService) {
        YAMJConfig.InitializeConfig(configService, pluginService);
        YAMJConfig.ApplyConfigToTalentXml();
        YAMJConfig.RebuildTalentDescriptions();
        YAMJConfig.ApplyConfigToAfflictionXml();
        YAMJConfig.ApplyConfigToItemXml();
    }
}