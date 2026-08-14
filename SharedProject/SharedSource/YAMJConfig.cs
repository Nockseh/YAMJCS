using Barotrauma.LuaCs.Data;
using Microsoft.Xna.Framework.Input;
using HarmonyLib;

namespace YAMJCS;

public static class YAMJConfig {
    private const string ConfigTokenPrefix = "yamjConfig.";
    
    #region Server Settings
    // Shared Settings
    private static ISettingBase<float>? eatingHungerReduction;
    private static ISettingBase<float>? raptorVisionRange;
    private static ISettingBase<bool>? raptorVisionShowDead;
    private static ISettingBase<float>? canOpenerDamageBonus;

    //config text tokens for real-time replacement
    private static readonly Dictionary<string, Func<float>> descriptionValues =
        new(StringComparer.OrdinalIgnoreCase) {
            ["CanOpenerDamageBonus"] = () => CanOpenerDamageBonus,
            ["EatingHungerReduction"] = () => EatingHungerReduction,
            ["RaptorVisionRange"] = () => RaptorVisionRange,
        };

    //set defaults
    public static float EatingHungerReduction => eatingHungerReduction?.Value ?? 4f;
    public static float RaptorVisionRange => raptorVisionRange?.Value ?? 1500f;
    public static bool RaptorVisionShowDead => raptorVisionShowDead?.Value ?? true;
    public static float CanOpenerDamageBonus => canOpenerDamageBonus?.Value ?? 300f;
    #endregion
    
    // Client Settings
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
            YAMJ.Log("Config services unavailable, using defaults.");
            return;
        }
        if (!pluginService.TryGetPackageForPlugin<Plugin>(out ContentPackage package) || package is null) {
            YAMJ.Log("Could not resolve the content package 'YAMJCS', using defaults.");
            return;
        }

        Bind(configService, package, "EatingHungerReduction", ref eatingHungerReduction);
        Bind(configService, package, "RaptorVisionRange", ref raptorVisionRange);
        Bind(configService, package, "RaptorVisionShowDead", ref raptorVisionShowDead);
        Bind(configService, package, "CanOpenerDamageBonus", ref canOpenerDamageBonus);
        #if CLIENT
            Bind(configService, package, "RaptorVisionKey", ref raptorVisionKey);
            Bind(configService, package, "QuickChatKey", ref quickChatKey);
            Bind(configService, package, "RaptorVisionRed", ref raptorVisionRed);
            Bind(configService, package, "RaptorVisionGreen", ref raptorVisionGreen);
            Bind(configService, package, "RaptorVisionBlue", ref raptorVisionBlue);
            Bind(configService, package, "RaptorVisionAlpha", ref raptorVisionAlpha);
        #endif
        YAMJ.Log($"Bound {boundSettings.Count} config settings.");
    }

    private static void Bind<T>(IConfigService configService, ContentPackage package, string name, ref T? field)
        where T : class, ISettingBase {
        if (configService.TryGetConfig(package, name, out T setting) && setting is not null) {
            field = setting;
            setting.OnValueChanged += OnConfigValueChanged;
            boundSettings.Add(setting);
        } else {
            YAMJ.Log($"Config '{name}' not found, using default.");
        }
    }

    private static void OnConfigValueChanged(ISettingBase setting) {
        YAMJ.Log($"Config '{setting.InternalName}' changed to {setting.GetStringValue()}.");
    }
    
    // For rebuilding talent descriptions based on config
    private static bool UsesConfigTokens(TalentPrefab talent) {
        foreach (ContentXElement descriptionElement in talent.ConfigElement.GetChildElements("Description")) {
            foreach (ContentXElement replaceElement in descriptionElement.GetChildElements("Replace")) {
                string? value = replaceElement.GetAttributeString("value", string.Empty);
                if (value is null) { continue; }
                if (value.StartsWith(ConfigTokenPrefix, StringComparison.OrdinalIgnoreCase)) {
                    return true;
                }
            }
        }
        return false;
    }

    public static void RebuildTalentDescriptions() {
        MethodInfo? talentDescriptionSetter = AccessTools.PropertySetter(typeof(TalentPrefab), nameof(TalentPrefab.Description));
        if (talentDescriptionSetter is null) {
            YAMJ.Log("TalentPrefab.Description property setter not found");
            return;
        }

        int counter = 0;
        foreach (TalentPrefab talent in TalentPrefab.TalentPrefabs) {
            if (!UsesConfigTokens(talent)) { continue; }

            // mirrors TalentPrefab's constructor, but with live values substituted
            LocalizedString description = string.Empty;
            foreach (ContentXElement descriptionElement in talent.ConfigElement.GetChildElements("Description")) {
                TextManager.ConstructDescription(ref description, descriptionElement, ResolveConfigToken);
            }
            description = description
                .Fallback(TextManager.Get($"talentdescription.{talent.Identifier}"))
                .Fallback(string.Empty);

            talentDescriptionSetter.Invoke(talent, new object[] { description });
            counter++;
        }

        YAMJ.Log($"Refreshed {counter} talent descriptions from config.");
    }
    
    // converts <yamjConfig.configName.format> into the number value
    private static string ResolveConfigToken(string value) {
        if (value is null) { return string.Empty; }
        if (!value.StartsWith(ConfigTokenPrefix, StringComparison.OrdinalIgnoreCase)) { return string.Empty; }

        string noPrefix = value.Substring(ConfigTokenPrefix.Length);
        string configName = noPrefix;
        string format = string.Empty;

        int separatorIndex = noPrefix.IndexOf('.');
        if (separatorIndex >= 0) {
            configName = noPrefix.Substring(0, separatorIndex);
            format = noPrefix.Substring(separatorIndex + 1);
        }

        if (!descriptionValues.TryGetValue(configName, out Func<float>? accessor) || accessor is null) {
            YAMJ.Log($"Description token '{value}' has no registered value");
            return "?";
        }

        return FormatValue(accessor(), format);
    }
    
    private static string FormatValue(float value, string format) {
        if (format == "percent") { return Math.Round(value * 100f).ToString(); }
        if (format == "percentInverse") { return Math.Round(100f - value * 100f).ToString(); }
        if (format == "int") { return Math.Round(value).ToString(); }
        return value.ToString("0.##");
    }
    
    public static void DisposeConfig() {
        foreach (ISettingBase setting in boundSettings) {
            setting.OnValueChanged -= OnConfigValueChanged;
        }
        boundSettings.Clear();

        //gameplay
        eatingHungerReduction = null;
        raptorVisionRange = null;
        raptorVisionShowDead = null;
        //talents
        canOpenerDamageBonus = null;
        //client
        #if CLIENT
            raptorVisionKey = null;
            quickChatKey = null;
            raptorVisionRed = null;
            raptorVisionGreen = null;
            raptorVisionBlue = null;
            raptorVisionAlpha = null;
        #endif
    }
}