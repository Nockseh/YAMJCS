using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using Barotrauma.LuaCs.Data;

namespace YAMJCS;

internal static class YAMJ
{
    // Mod Configuration //
    #region Config
    
    //server (shared, for server authority) settings
    private static ISettingBase<float>? eatingHungerReduction;
    private static ISettingBase<float>? raptorVisionRange;
    private static ISettingBase<bool>? raptorVisionShowDead;

    public static float EatingHungerReduction => eatingHungerReduction?.Value ?? 4f;
    public static float RaptorVisionRange => raptorVisionRange?.Value ?? 1500f;
    public static bool RaptorVisionShowDead => raptorVisionShowDead?.Value ?? true;
    
    //client settings
    #if CLIENT
        private static ISettingControl? raptorVisionKey;
        private static ISettingControl? quickChatKey;
        private static ISettingBase<int>? raptorVisionRed;
        private static ISettingBase<int>? raptorVisionGreen;
        private static ISettingBase<int>? raptorVisionBlue;
        private static ISettingBase<int>? raptorVisionAlpha;
        
        // this prevents superfluous allocation when requested
        private static readonly KeyOrMouse DefaultRaptorVisionKey = Keys.V;
        private static readonly KeyOrMouse DefaultQuickChatKey = Keys.B;
    
        public static KeyOrMouse RaptorVisionKey => raptorVisionKey?.Value ?? DefaultRaptorVisionKey;
        public static KeyOrMouse QuickChatKey => quickChatKey?.Value ?? DefaultQuickChatKey;
        public static Color RaptorVisionColor => new Color(
            raptorVisionRed?.Value ?? 255,
            raptorVisionGreen?.Value ?? 127,
            raptorVisionBlue?.Value ?? 0,
            raptorVisionAlpha?.Value ?? 255
        );
    #endif
    
    //settings binding
    private static readonly List<ISettingBase> boundSettings = new();

    public static void InitializeConfig(IConfigService? configService, IPluginManagementService? pluginService) {
        if (configService is null || pluginService is null) {
            Log("Config services unavailable, using defaults.");
            return;
        }
        if (!pluginService.TryGetPackageForPlugin<Plugin>(out ContentPackage package) || package is null) {
            Log("Could not resolve the content package 'YAMJCS', using defaults.");
            return;
        }

        Bind(configService, package, "EatingHungerReduction", ref eatingHungerReduction);
        Bind(configService, package, "RaptorVisionRange", ref raptorVisionRange);
        Bind(configService, package, "RaptorVisionShowDead", ref raptorVisionShowDead);
        #if CLIENT
            Bind(configService, package, "RaptorVisionKey", ref raptorVisionKey);
            Bind(configService, package, "QuickChatKey", ref quickChatKey);
            Bind(configService, package, "RaptorVisionRed", ref raptorVisionRed);
            Bind(configService, package, "RaptorVisionGreen", ref raptorVisionGreen);
            Bind(configService, package, "RaptorVisionBlue", ref raptorVisionBlue);
            Bind(configService, package, "RaptorVisionAlpha", ref raptorVisionAlpha);
        #endif
        Log($"Bound {boundSettings.Count} config settings.");
    }

    private static void Bind<T>(IConfigService configService, ContentPackage package, string name, ref T? field)
        where T : class, ISettingBase {
        if (configService.TryGetConfig(package, name, out T setting) && setting is not null) {
            field = setting;
            setting.OnValueChanged += OnConfigValueChanged;
            boundSettings.Add(setting);
        } else {
            Log($"Config '{name}' not found, using default.");
        }
    }

    private static void OnConfigValueChanged(ISettingBase setting) {
        Log($"Config '{setting.InternalName}' changed to {setting.GetStringValue()}.");
    }

    private static void DisposeConfig() {
        foreach (ISettingBase setting in boundSettings) {
            setting.OnValueChanged -= OnConfigValueChanged;
        }
        boundSettings.Clear();

        eatingHungerReduction = null;
        raptorVisionRange = null;
        raptorVisionShowDead = null;
        #if CLIENT
            raptorVisionKey = null;
            quickChatKey = null;
            raptorVisionRed = null;
            raptorVisionGreen = null;
            raptorVisionBlue = null;
            raptorVisionAlpha = null;
        #endif
    }

    #endregion
    
    // Constants //
    public const string PlayerRaptorSpecies = "Mudraptor_player";
    public const string PlayerRaptorHuskSpecies = "Mudraptor_playerhusk";
    public const string PlayerRaptorJobId = "PlayerMudraptorJob";
    public static readonly Identifier RaptorBabySpecies = "RaptorPet".ToIdentifier();
    public static readonly Identifier RaptorBabyItemId = "raptorpetitem".ToIdentifier();
    public static readonly Identifier RaptorOnlyTag = "yamjraptoronly".ToIdentifier();
    // vars
    public static AfflictionPrefab HungerPrefab;
    public static AfflictionPrefab EatingBuffPrefab;
    
    public static ILoggerService? LoggerService { get; set; }
    public static void Log(string message) {
        LoggerService?.Log($"[YAMJCS] {message}");
    }

    // Helper Functions //
    public static CharacterPrefab? FindMudraptorPrefab()
    {
        return CharacterPrefab.FindBySpeciesName(PlayerRaptorSpecies.ToIdentifier());
    }
    public static ItemPrefab? FindRaptorBabyItemPrefab() {
        ItemPrefab.Prefabs.TryGet(RaptorBabyItemId, out ItemPrefab? prefab);
        return prefab;
    }
    
    public static bool HasPlayerRaptorJob(CharacterInfo? characterInfo)
    {
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
        HashSet<Identifier> talents = character.Info.UnlockedTalents;
        if (talents == null || talents.Count == 0) return false;
        if (talents.Contains(talentId.ToIdentifier())) {
            return true;
        } else {
            return false;
        }
    }

    public static void SharedDispose() {
        DisposeConfig();
        LoggerService = null;
    }
}