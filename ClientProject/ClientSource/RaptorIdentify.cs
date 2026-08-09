using System.Net.Mime;
using System.Runtime.InteropServices.JavaScript;
using HarmonyLib;

namespace YAMJCS;

internal static partial class PatchTargets {
    public static MethodBase Item_Name_Get =>
        AccessTools.PropertyGetter(
            typeof(Item),
            nameof(Item.Name)) ??
        throw new Exception("Item.Name {get} not found");
        
    public static MethodBase Item_Description_Get =>
        AccessTools.PropertyGetter(
            typeof(Item),
            nameof(Item.Description)) ??
        throw new Exception("Item.Description {get} not found");
}

internal static class RaptorIdentify {
    private static readonly (Identifier[] itemTag, string[] requiredTalents, string langTag)[] itemCategories = {
        (["weapon"], 
            ["YAMJSpeechCombat"],  
            "weapon"),
            
        (["tool"],
            ["YAMJSpeechCombat"],   
            "repairTool"),
            
        (["ore"],
            ["YAMJSpeechCombat"],
            "ore"),
            
        (["clothing"],
            ["YAMJSpeechAdvanced"],
            "clothing"),
            
        (["deepdiving"],
            ["YAMJSpeechAdvanced"],
            "deepdiving"),
            
        (["medical", "chem"],
            ["YAMJSpeechAdvanced"],
            "medichem"),
            
        (["ammobox"],
            ["YAMJSpeechAdvanced"],
            "ammobox"),
            
        (["scooter"],
            ["YAMJSpeechAdvanced"],
            "scooter"),
            
        (["logic", "sonar", "sensor", "signal", "sound", "lightcomponent", "mobilebattery", "detonator", "alienartifact", "smallalienartifact"],
            ["YAMJSpeechAdvanced"],
            "tech")
    };
    
    private static readonly Dictionary<string, ItemPrefab?> artPrefabCache = new();
    
    public static string? ResolveDumbKey(Item? item) {
        if (item is null) { return null; }

        Character? controlled = Character.Controlled;
        if (controlled is null || !YAMJ.IsPlayerRaptor(controlled)) { return null; }

        foreach ((Identifier[] tags, string[] requiredTalents, string key) in itemCategories) {
            if (item.HasTag(tags)) {
                foreach (string talent in requiredTalents) {
                    if (YAMJ.HasTalent(controlled, talent)) {
                        return null;
                    }
                } //if doesn't have ANY of the talents
                return key;
            }
        }
        return null;
    }
    
    private static ItemPrefab? GetDumbPrefab(string key) {
        if (artPrefabCache.TryGetValue(key, out ItemPrefab? cached)) { return cached; }
        //initialize if not cached
        ItemPrefab.Prefabs.TryGet(("yamjdumb" + key).ToIdentifier(), out ItemPrefab? prefab);
        if (prefab is null) { YAMJ.Log($"Sprite source prefab 'yamjdumb{key}' not found."); }

        artPrefabCache[key] = prefab; //null is cached too, so a missing prefab doesn't spam lookups
        return prefab;
    }
    
    /// Replacement for the item's in-world sprite, or null to leave it alone.
    public static Sprite? GetWorldSprite(Item item) { //TODO is this necessary? can it be split up elsewhere?
        string? key = ResolveDumbKey(item);
        if (key is null) { return null; }

        Sprite? sprite = GetDumbPrefab(key)?.Sprite;
        sprite?.EnsureLazyLoaded(); //TODO why?
        return sprite;
    }
    
    /// Replacement for the item's inventory icon, or null to leave it alone.
    public static Sprite? GetInventorySprite(Item item) {
        string? key = ResolveDumbKey(item);
        if (key is null) { return null; }

        ItemPrefab? prefab = GetDumbPrefab(key);
        if (prefab is null) { return null; }

        Sprite? sprite = prefab.InventoryIcon ?? prefab.Sprite;
        sprite?.EnsureLazyLoaded(); //TODO why?
        return sprite;
    }

    public static void ClearCache() { //TODO why?
        artPrefabCache.Clear();
    }
}

[HarmonyPatch] //swaps the inventory icon of some items TODO: verify
internal static class ItemOverrideInventorySpriteGet {
    static MethodBase TargetMethod() => PatchTargets.Item_OverrideInventorySprite_Get;

    static void Postfix(Item __instance, ref Sprite? __result) {
        if (__result is not null) { return; } //something already set an override, leave it alone? not sure when this happens normally

        __result = RaptorIdentify.GetInventorySprite(__instance);
    }
}

[HarmonyPatch] //swaps the in-world sprite of some items TODO: verify
internal static class ItemDrawSpriteSwap {
    static MethodBase TargetMethod() => PatchTargets.Item_Draw;

    static void Prefix(Item __instance, ref Sprite? ___activeSprite, out Sprite? __state) {
        __state = null; //allows returning without swapping anything
        if (___activeSprite is null) { return; } //spriteless item, don't give it one

        Sprite? replacement = RaptorIdentify.GetWorldSprite(__instance);
        if (replacement is null || ReferenceEquals(replacement, ___activeSprite)) { return; }

        __state = ___activeSprite;
        ___activeSprite = replacement;
    }

    static void Postfix(ref Sprite? ___activeSprite, Sprite? __state) {
        if (__state is null) { return; }
        ___activeSprite = __state; //return to original
    }
}

[HarmonyPatch] //changes the display name of some items
internal static class ItemNameGet {
    static MethodBase TargetMethod() => PatchTargets.Item_Name_Get;

    static bool Prefix(Item __instance, ref string __result) {
        Character? controlled = Character.Controlled;
        if (YAMJ.IsPlayerRaptor(controlled)) {
            string? key = RaptorIdentify.ResolveDumbKey(__instance);
            if (key is not null) {
                __result = TextManager.Get("entityname.dumb." + key).Value;
                return false;
            }
        }
        return true;
    }
}

[HarmonyPatch] //changes the description of some items
internal static class ItemDescGet {
    static MethodBase TargetMethod() => PatchTargets.Item_Description_Get;

    static bool Prefix(Item __instance, ref string __result) {
        Character? controlled = Character.Controlled;
        if (YAMJ.IsPlayerRaptor(controlled)) {
            string? key = RaptorIdentify.ResolveDumbKey(__instance);
            if (key is not null) {
                __result = TextManager.Get("entitydescription.dumb." + key).Value;
                return false;
            }
        }
        return true;
    }
}