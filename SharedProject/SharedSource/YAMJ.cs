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
    internal static readonly Identifier RaptorBabySpecies = "RaptorBaby".ToIdentifier();
    internal static readonly Identifier RaptorBabyItemId = "raptorBabyItem".ToIdentifier();
    internal static readonly Identifier RaptorOnlyTag = "yamjraptoronly".ToIdentifier();
    internal static readonly Identifier[] BasicFood = new Identifier[] {"food", "monsterfood", "plant"};
    internal static readonly Identifier[] MechanicFood = new Identifier[] {"ore","scrap"};
    internal static readonly Identifier[] EngineerFood = new Identifier[] {"reactorfuel"};
    internal static readonly Identifier[] DoctorFood = new Identifier[] {"unidentifiedgeneticmaterial", "geneticmaterial"};
    internal static readonly Identifier[] ShrapnelAmmo = new Identifier[] {"munition_core","munition_tip"};
    // vars
    
    internal static ItemPrefab? RaptorBabyItemPrefab;
    internal static CharacterPrefab? PlayerRaptorPrefab;
    
    internal static ILoggerService? LoggerService { get; set; }
    internal static void Log(string message) {
        LoggerService?.Log($"[YAMJCS] {message}");
    }

    // Helper Functions //
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
    
    public static void ApplyAtMaxStrength(Character? character, AfflictionPrefab? prefab) {
        if (character is null || character.Removed || prefab is null) { return; }

        CharacterHealth? health = character.CharacterHealth;
        if (health is null) { return; }

        float vitalityScale = health.MaxVitality / 100f;
        if (vitalityScale <= 0f) { vitalityScale = 1f; }

        health.AddAffliction(prefab.Instantiate(prefab.MaxStrength * vitalityScale));
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

internal static class AfflictionIds {
    internal static readonly Identifier Hunger = "yamjHunger";
    internal static readonly Identifier Satiated = "yamjSatiated";
    internal static readonly Identifier Eating = "yamjEating";
    internal static readonly Identifier OreInGut = "yamjOreInGut";
    internal static readonly Identifier MetalInGut = "yamjMetalInGut";
    internal static readonly Identifier Reactive = "yamjReactive";
    internal static readonly Identifier Infection = "yamjZombieInfection";
    internal static readonly Identifier InfectionCooldown = "yamjZombieInfectionCooldown";
    internal static readonly Identifier SicBuff = "yamjSicBuff";
    internal static readonly Identifier SicCooldown = "yamjSicCooldown";
    internal static readonly Identifier SicAwayBuff = "yamjSicAwayBuff";
    internal static readonly Identifier CaptainProxBuff = "yamjCaptainProxBuff";
    internal static readonly Identifier Marked = "yamjMarked";
    internal static readonly Identifier ClearVision = "yamjClearVision";
    internal static readonly Identifier VouchBuff = "yamjVouchBuff";
}

internal static class AfflictionPrefabs {
    private static AfflictionPrefab? FindAfflictionPrefab(Identifier id) {
        AfflictionPrefab.Prefabs.TryGet(id, out AfflictionPrefab? prefab);
        if (prefab is null) {
            YAMJ.Log($"Affliction prefab {id} not found");
        }
        return prefab;
    }
    
    private static AfflictionPrefab? hunger = null;
    internal static AfflictionPrefab? Hunger => hunger ??= FindAfflictionPrefab(AfflictionIds.Hunger);
    
    private static AfflictionPrefab? satiated = null;
    internal static AfflictionPrefab? Satiated => satiated ??= FindAfflictionPrefab(AfflictionIds.Satiated);
    
    private static AfflictionPrefab? eating = null;
    internal static AfflictionPrefab? Eating => eating ??= FindAfflictionPrefab(AfflictionIds.Eating);
    
    private static AfflictionPrefab? oreInGut = null;
    internal static AfflictionPrefab? OreInGut => oreInGut ??= FindAfflictionPrefab(AfflictionIds.OreInGut);
    
    private static AfflictionPrefab? metalInGut = null;
    internal static AfflictionPrefab? MetalInGut => metalInGut ??= FindAfflictionPrefab(AfflictionIds.MetalInGut);

    private static AfflictionPrefab? reactive = null;
    internal static AfflictionPrefab? Reactive => reactive ??= FindAfflictionPrefab(AfflictionIds.Reactive);
    
    private static AfflictionPrefab? infection = null;
    internal static AfflictionPrefab? Infection => infection ??= FindAfflictionPrefab(AfflictionIds.Infection);

    private static AfflictionPrefab? infectionCooldown = null;
    internal static AfflictionPrefab? InfectionCooldown => infectionCooldown ??= FindAfflictionPrefab(AfflictionIds.InfectionCooldown);

    private static AfflictionPrefab? sicBuff = null;
    internal static AfflictionPrefab? SicBuff => sicBuff ??= FindAfflictionPrefab(AfflictionIds.SicBuff);

    private static AfflictionPrefab? sicCooldown = null;
    internal static AfflictionPrefab? SicCooldown => sicCooldown ??= FindAfflictionPrefab(AfflictionIds.SicCooldown);

    private static AfflictionPrefab? sicAwayBuff = null;
    internal static AfflictionPrefab? SicAwayBuff => sicAwayBuff ??= FindAfflictionPrefab(AfflictionIds.SicAwayBuff);

    private static AfflictionPrefab? captainProxBuff = null;
    internal static AfflictionPrefab? CaptainProxBuff => captainProxBuff ??= FindAfflictionPrefab(AfflictionIds.CaptainProxBuff);

    private static AfflictionPrefab? marked = null;
    internal static AfflictionPrefab? Marked => marked ??= FindAfflictionPrefab(AfflictionIds.Marked);
}
