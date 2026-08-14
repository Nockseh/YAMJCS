using System.Xml.Linq;
using Barotrauma.LuaCs.Data;
using Microsoft.Xna.Framework.Input;
using HarmonyLib;

namespace YAMJCS;

public static class YAMJConfig {
    private const string ConfigTokenPrefix = "yamjConfig.";
    private const string ConfigAttribute = "configoverride";
    private static readonly MethodInfo? TalentDescriptionSetter = AccessTools.PropertySetter(typeof(TalentPrefab), nameof(TalentPrefab.Description));
    private static readonly FieldInfo? AfflictionConfigElement = AccessTools.Field(typeof(AfflictionPrefab), nameof(AfflictionPrefab.configElement));
    private static readonly FieldInfo? AfflictionEffects = AccessTools.Field(typeof(AfflictionPrefab), nameof(AfflictionPrefab.effects));
    
    #region Server Settings
    // Shared Settings
    public static float HungerRate => GetFloat("HungerRate") ?? 0.015f;
    public static float EatingHungerReduction => GetFloat("EatingHungerReduction") ?? 4f;
    public static float RaptorVisionRange => GetFloat("RaptorVisionRange") ?? 1500f;
    public static bool RaptorVisionShowDead => GetBool("RaptorVisionShowDead") ?? true;
    public static float CanOpenerDamageBonus => GetFloat("CanOpenerDamageBonus") ?? 3.00f;
    public static float FamiliarWatersDamageBonus => GetFloat("FamiliarWatersDamageBonus") ?? 0.25f;
    public static float SleepWindupTime => GetFloat("SleepWindupTime") ?? 5;
    #endregion
    
    // Client Settings
    #if CLIENT
        // this prevents superfluous allocation, i think
        private static readonly KeyOrMouse DefaultRaptorVisionKey = Keys.V;
        private static readonly KeyOrMouse DefaultQuickChatKey = Keys.B;
    
        public static KeyOrMouse RaptorVisionKey => GetKey("RaptorVisionKey") ?? DefaultRaptorVisionKey;
        public static KeyOrMouse QuickChatKey => GetKey("QuickChatKey") ?? DefaultQuickChatKey;
        public static Color RaptorVisionColor => new Color(
            GetInt("RaptorVisionRed") ?? 255,
            GetInt("RaptorVisionGreen") ?? 127,
            GetInt("RaptorVisionBlue") ?? 0,
            GetInt("RaptorVisionAlpha") ?? 255
        );
    #endif
    
    //settings binding
    private static readonly Dictionary<string, ISettingBase> boundSettings = new();

    public static void InitializeConfig(IConfigService configService, IPluginManagementService pluginService) {
        if (!pluginService.TryGetPackageForPlugin<Plugin>(out ContentPackage thisPackage) || thisPackage is null) {
            YAMJ.Log("Could not resolve content package 'YAMJCS', config not initialized");
            return;
        }

        Bind(configService, thisPackage, "HungerRate");
        Bind(configService, thisPackage, "EatingHungerReduction");
        Bind(configService, thisPackage, "RaptorVisionRange");
        Bind(configService, thisPackage, "RaptorVisionShowDead");
        Bind(configService, thisPackage, "CanOpenerDamageBonus");
        Bind(configService, thisPackage, "FamiliarWatersDamageBonus");
        Bind(configService, thisPackage, "SleepWindupTime");
        #if CLIENT
        Bind(configService, thisPackage, "RaptorVisionKey");
        Bind(configService, thisPackage, "QuickChatKey");
        Bind(configService, thisPackage, "RaptorVisionRed");
        Bind(configService, thisPackage, "RaptorVisionGreen");
        Bind(configService, thisPackage, "RaptorVisionBlue");
        Bind(configService, thisPackage, "RaptorVisionAlpha");
        #endif

        YAMJ.Log($"Bound {boundSettings.Count} config settings: {string.Join(", ", boundSettings.Keys)}");
    }

    private static void Bind(IConfigService configService, ContentPackage package, string name) {
        if (configService.TryGetConfig(package, name, out ISettingBase setting) && setting is not null) {
            setting.OnValueChanged += OnConfigValueChanged;
            boundSettings[name] = setting;
        } else {
            YAMJ.Log($"Config '{name}' not found, using default");
        }
    }

    private static void OnConfigValueChanged(ISettingBase setting) {
        YAMJ.Log($"Config '{setting.InternalName}' changed to {setting.GetStringValue()}");
        ApplyConfigToTalentXml();
        RebuildTalentDescriptions();
        ApplyConfigToAfflictionXml();
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
        if (TalentDescriptionSetter is null) {
            YAMJ.Log("TalentPrefab.Description property setter not found");
            return;
        }

        int counter = 0;
        foreach (TalentPrefab talent in TalentPrefab.TalentPrefabs) {
            if (!UsesConfigTokens(talent)) {
                continue;
            }

            // mirrors TalentPrefab's constructor, but with live values substituted
            LocalizedString description = string.Empty;
            foreach (ContentXElement descriptionElement in talent.ConfigElement.GetChildElements("Description")) {
                TextManager.ConstructDescription(ref description, descriptionElement, ResolveConfigToken);
            }

            description = description
                .Fallback(TextManager.Get($"talentdescription.{talent.Identifier}"))
                .Fallback(string.Empty);

            TalentDescriptionSetter.Invoke(talent, new object[] { description });
            counter++;
        }

        YAMJ.Log($"Refreshed {counter} talent descriptions from config");
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

        if (!boundSettings.TryGetValue(configName, out ISettingBase? setting)) {
            YAMJ.Log($"Description token '{value}' has no bound setting, using text default");
            return string.Empty;
        }

        return FormatSetting(setting, format);
    }
    
    private static string FormatValue(float value, string format) {
        if (format == "percent") { return Math.Round(value * 100f).ToString(); }
        if (format == "percentInverse") { return Math.Round(100f - value * 100f).ToString(); }
        if (format == "int") { return Math.Round(value).ToString(); }
        return value.ToString("0.##");
    }
    
    public static void DisposeConfig() {
        foreach (ISettingBase setting in boundSettings.Values) {
            setting.OnValueChanged -= OnConfigValueChanged;
        }
        boundSettings.Clear();
    }
    
    public static void ApplyConfigToTalentXml() {
        int counter = 0;
        foreach (TalentPrefab talent in TalentPrefab.TalentPrefabs) {
            foreach (XElement element in talent.ConfigElement.Element.Descendants()) {
                XAttribute? configOverride = element.Attribute(ConfigAttribute);
                if (configOverride is null) { continue; }
                counter += ApplyAttributeBindings(element, configOverride.Value);
            }
        }
        YAMJ.Log($"Applied config to {counter} talent XML values");
    }

    private static int ApplyAttributeBindings(XElement element, string bindings) {
        int counter = 0;
        foreach (string binding in bindings.Split(';', StringSplitOptions.RemoveEmptyEntries)) {
            string[] parts = binding.Split('=');
            if (parts.Length != 2) { continue; }

            string attributeName = parts[0].Trim();
            string valueName = parts[1].Trim();

            float? configValue = GetFloat(valueName);
            if (configValue is null) {
                YAMJ.Log($"{ConfigAttribute} binding '{binding}' has no bound setting, leaving XML default");
                continue;
            }

            // invariant required, the game parses XML floats with InvariantCulture
            element.SetAttributeValue(attributeName, configValue.Value.ToString("0.#####", System.Globalization.CultureInfo.InvariantCulture));
            counter++;
        }
        return counter;
    }
    
    public static void ApplyConfigToAfflictionXml() {
        if (AfflictionConfigElement is null || AfflictionEffects is null) {
            YAMJ.Log("AfflictionPrefab internals not found, affliction overrides skipped");
            return;
        }

        int counter = 0;
        foreach (AfflictionPrefab affliction in AfflictionPrefab.Prefabs) {
            ContentXElement? configElement = AfflictionConfigElement.GetValue(affliction) as ContentXElement;
            if (configElement is null) { continue; }

            int applied = 0;
            foreach (XElement element in configElement.Element.Descendants()) {
                XAttribute? configOverride = element.Attribute(ConfigAttribute);
                if (configOverride is null) { continue; }
                applied += ApplyAttributeBindings(element, configOverride.Value);
            }

            if (applied == 0) { continue; }
            RebuildAfflictionEffects(affliction, configElement);
            counter += applied;
        }

        YAMJ.Log($"Applied config to {counter} affliction XML values");
    }

    // the prefab parses Effects once on load, so editing the XML alone does nothing
    private static void RebuildAfflictionEffects(AfflictionPrefab affliction, ContentXElement configElement) {
        List<AfflictionPrefab.Effect>? effects = AfflictionEffects?.GetValue(affliction) as List<AfflictionPrefab.Effect>;
        if (effects is null) { return; }

        effects.Clear();
        foreach (ContentXElement subElement in configElement.Elements()) {
            if (subElement.Name.ToString().ToLowerInvariant() != "effect") { continue; }
            effects.Add(new AfflictionPrefab.Effect(subElement, affliction.Name.Value));
        }
    }
    
    private static string FormatSetting(ISettingBase setting, string format) {
        ISettingBase<float>? floatSetting = setting as ISettingBase<float>;
        if (floatSetting is not null) { return FormatValue(floatSetting.Value, format); }

        ISettingBase<int>? intSetting = setting as ISettingBase<int>;
        if (intSetting is not null) { return FormatValue(intSetting.Value, format); }

        ISettingBase<bool>? boolSetting = setting as ISettingBase<bool>;
        if (boolSetting is not null) {
            if (boolSetting.Value) {
                return TextManager.Get("yes").Value;
            } else {
                return TextManager.Get("no").Value;
            }
        }

        return setting.GetStringValue();
    }
    
    // Setting Getters
    private static float? GetFloat(string name) {
        if (!boundSettings.TryGetValue(name, out ISettingBase? setting)) { return null; }
        ISettingBase<float>? typed = setting as ISettingBase<float>;
        if (typed is null) { return null; }
        return typed.Value;
    }

    private static int? GetInt(string name) {
        if (!boundSettings.TryGetValue(name, out ISettingBase? setting)) { return null; }
        ISettingBase<int>? typed = setting as ISettingBase<int>;
        if (typed is null) { return null; }
        return typed.Value;
    }

    private static bool? GetBool(string name) {
        if (!boundSettings.TryGetValue(name, out ISettingBase? setting)) { return null; }
        ISettingBase<bool>? typed = setting as ISettingBase<bool>;
        if (typed is null) { return null; }
        return typed.Value;
    }

#if CLIENT
    private static KeyOrMouse? GetKey(string name) {
        if (!boundSettings.TryGetValue(name, out ISettingBase? setting)) { return null; }
        ISettingControl? typed = setting as ISettingControl;
        if (typed is null) { return null; }
        return typed.Value;
    }
#endif
}