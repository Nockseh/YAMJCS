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
    private static readonly (Identifier[] itemTag, Identifier[] appraisalTalents, string langTag)[] itemCategories = {
    // tags higher on the list take priority
        (["weapon"], 
            ["YAMJSpeechCombat", "YAMJSpeechAdvanced"],  
            "weapon"),
            
        (["tool"],
            ["YAMJCanUseTools", "YAMJSpeechBasic", "YAMJSpeechAdvanced"],   
            "repairTool"),
            
        (["ore"],
            ["YAMJSpeechAdvanced"],
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
            ["YAMJSpeechCombat", "YAMJSpeechAdvanced"],
            "ammobox"),
            
        (["handheldammo"],
            ["YAMJSpeechCombat", "YAMJSpeechAdvanced"],
            "ammosmall"),
            
        (["scooter"],
            ["YAMJSpeechAdvanced"],
            "scooter"),
            
        (["logic", "sonar", "sensor", "signal", "sound", "lightcomponent", "mobilebattery", "detonator"],
            ["YAMJSpeechAdvanced"],
            "tech"),
            
        (["alienartifact"],
            ["YAMJSpeechAdvanced"],
            "alienlarge"),
            
        (["smallalienartifact"],
            ["YAMJSpeechAdvanced"],
            "aliensmall"),
    };
    
    private static readonly Dictionary<string, ItemPrefab?> artPrefabCache = new();
    
    public static string? ResolveDumbKey(Item? item) {
        if (item is null) { return null; }

        Character? controlled = Character.Controlled;
        if (controlled is null) { return null; }
        
        if (item.HasTag("yamjraptoronly")) { return null; } //any item that is FOR raptors is auto-identified
        
        string? key = null;
        foreach ((Identifier[] itemTags, Identifier[] appraisalTalents, string langTag) in itemCategories) {
            if (!HasAnyTag(item, itemTags)) { continue; }
            if (YAMJ.HasAnyTalent(controlled, appraisalTalents)) { return null; } //any identifiable category identifies the item
            key ??= langTag; //remember the first unidentifiable match and keep looking
        }

        return key;
    }

    private static bool HasAnyTag(Item item, Identifier[] tags) { //yes i know Item.HasTag(IEnumerable<>) exists, this is cheaper on allocation
        foreach (Identifier tag in tags) {
            if (item.HasTag(tag)) { return true; }
        }
        return false;
    }


    private static ItemPrefab? GetDumbPrefab(string key) {
        if (artPrefabCache.TryGetValue(key, out ItemPrefab? cached)) { return cached; }
        //initialize if not cached
        ItemPrefab.Prefabs.TryGet(("yamjdumb" + key).ToIdentifier(), out ItemPrefab? prefab);
        if (prefab is null) { YAMJ.Log($"Sprite source prefab 'yamjdumb{key}' not found."); }

        artPrefabCache[key] = prefab; //null is cached too, so a missing prefab doesn't spam lookups
        return prefab;
    }
    
    //replacement for the item's in-world sprite
    public static Sprite? GetWorldSprite(Item item) {
        string? key = ResolveDumbKey(item);
        if (key is null) { return null; }

        Sprite? sprite = GetDumbPrefab(key)?.Sprite;
        sprite?.EnsureLazyLoaded();
        return sprite;
    }
    
    //replacement for the item's inventory icon
    public static Sprite? GetInventorySprite(Item item) {
        string? key = ResolveDumbKey(item);
        if (key is null) { return null; }

        ItemPrefab? prefab = GetDumbPrefab(key);
        if (prefab is null) { return null; }

        Sprite? sprite = prefab.InventoryIcon ?? prefab.Sprite;
        sprite?.EnsureLazyLoaded();
        return sprite;
    }

    public static void ClearCache() {
        artPrefabCache.Clear();
    }
}

[HarmonyPatch] //swaps the inventory icon of some items
internal static class ItemOverrideInventorySpriteGet {
    static MethodBase TargetMethod() => PatchTargets.Item_OverrideInventorySprite_Get;

    static void Postfix(Item __instance, ref Sprite? __result) {
        if (__result is not null) { return; } //something already set an override, leave it alone? not sure when this happens normally

        __result = RaptorIdentify.GetInventorySprite(__instance);
    }
}

[HarmonyPatch] //swaps the in-world sprite of some items
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