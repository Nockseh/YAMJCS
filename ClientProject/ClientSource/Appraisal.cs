using Barotrauma.Items.Components;
using HarmonyLib;

namespace YAMJCS;

internal static partial class PatchTargets {
    internal static MethodBase Item_Name_Get =>
        AccessTools.PropertyGetter(
            typeof(Item),
            nameof(Item.Name)) ??
        throw new Exception("Item.Name {get} not found");
        
    internal static MethodBase Item_Description_Get =>
        AccessTools.PropertyGetter(
            typeof(Item),
            nameof(Item.Description)) ??
        throw new Exception("Item.Description {get} not found");
}

internal static class Appraisal {
    
    private static readonly (Identifier[] itemTag, Identifier[] appraisalTalents, string langTag)[] itemCategories = {
    // tags higher on the list take priority
        (["textdisplay"], 
            ["yamjSpeechCombat", "yamjSpeechAdvanced"],  
            "textdisplay"),
    
        (["weapon"], 
            ["yamjSpeechCombat", "yamjSpeechAdvanced"],  
            "weapon"),
            
        (["tool"],
            ["yamjCanUseTools", "yamjSpeechBasic", "yamjSpeechAdvanced"],   
            "repairTool"),
            
        (["ore"],
            ["yamjSpeechAdvanced"],
            "ore"),
            
        (["clothing"],
            ["yamjSpeechAdvanced"],
            "clothing"),
            
        (["deepdiving"],
            ["yamjSpeechAdvanced"],
            "deepdiving"),
            
        (["medical", "chem"],
            ["yamjSpeechAdvanced"],
            "medichem"),
            
        (["ammobox"],
            ["yamjSpeechCombat", "yamjSpeechAdvanced"],
            "ammobox"),
            
        (["handheldammo"],
            ["yamjSpeechCombat", "yamjSpeechAdvanced"],
            "ammosmall"),
            
        (["scooter"],
            ["yamjSpeechAdvanced"],
            "scooter"),
            
        (["logic", "sonar", "sensor", "signal", "sound", "lightcomponent", "mobilebattery", "detonator"],
            ["yamjSpeechAdvanced"],
            "tech"),
            
        (["alienartifact"],
            ["yamjSpeechAdvanced"],
            "alienlarge"),
            
        (["smallalienartifact"],
            ["yamjSpeechAdvanced"],
            "aliensmall"),
        
        (["helmet"],
            ["yamjSpeechAdvanced", "yamjSpeechCombat"],
            "helmet"),
    };

    private static Dictionary<Identifier, Identifier[]> additionalTagTable = new() {
        //["ironhelmet"] = ["helmet"],
        //["chitinhelmet"] = ["helmet"],
        //["ballistichelmet1"] = ["helmet"],
        //["ballistichelmet2"] = ["helmet"],
        //["ballistichelmet3"] = ["helmet"],
    };
    
    // tags worked out from an item's components, filled in by InferTags
    private static readonly Dictionary<Identifier, Identifier[]?> inferredTagCache = new();
    
    private static readonly Dictionary<string, ItemPrefab?> artPrefabCache = new();
    
    internal static string? ResolveDumbKey(Item? item) {
        if (item is null) { return null; }
        
        if (item.HasTag(YAMJ.RaptorOnlyTag)) { return null; } //any item that is FOR raptors is auto-identified 
        
        Identifier[]? additionalTags = GetAdditionalTags(item);
        Identifier[]? inferredTags = InferTags(item);
        
        string? key = null;
        foreach ((Identifier[] itemTags, Identifier[] appraisalTalents, string langTag) in itemCategories) {
            if (!HasAnyTag(item, itemTags, additionalTags, inferredTags)) { continue; }
            if (YAMJ.HasAnyTalent(Character.Controlled, appraisalTalents)) { return null; } //any identifiable category identifies the item 
            key ??= langTag; //remember the first unidentifiable match and keep looking 
        }

        return key;
    }

    //yes i know Item.HasTag(IEnumerable<>) exists, this is cheaper on allocation 
    private static bool HasAnyTag(Item item, Identifier[] tags, Identifier[]? additionalTags, Identifier[]? inferredTags) {
        foreach (Identifier tag in tags) {
            if (item.HasTag(tag)) { return true; }
            if (ContainsTag(additionalTags, tag)) { return true; }
            if (ContainsTag(inferredTags, tag)) { return true; }
        }
        return false;
    }
    
    private static bool ContainsTag(Identifier[]? tags, Identifier tag) {
        return tags is not null && Array.IndexOf(tags, tag) >= 0;
    }
    
    private static Identifier[]? GetAdditionalTags(Item item) {
        additionalTagTable.TryGetValue(item.Prefab.Identifier, out Identifier[]? tags);
        return tags;
    }
    
    //tags worked out from the item's components, built the first time a prefab is seen and cached
    private static Identifier[]? InferTags(Item item) {
        Identifier prefabId = item.Prefab.Identifier;
        if (inferredTagCache.TryGetValue(prefabId, out Identifier[]? cached)) { return cached; }

        List<Identifier>? inferred = null;
        if (IsHelmet(item)) { (inferred ??= new()).Add("helmet".ToIdentifier()); }

        Identifier[]? result = inferred?.ToArray();
        inferredTagCache[prefabId] = result; //null cached, so items with nothing to infer don't re-run 
        return result;
    }

    //worn on the head and resists at least one kind of damage
    private static bool IsHelmet(Item item) {
        Wearable? wearable = item.GetComponent<Wearable>();
        if (wearable is null) { return false; }

        bool isHeadSlot = false;
        foreach (InvSlotType slotType in wearable.AllowedSlots) {
            if (slotType.HasFlag(InvSlotType.Head)) {
                isHeadSlot = true;
                break;
            }
        }
        if (!isHeadSlot) { return false; }

        foreach (DamageModifier damageModifier in wearable.DamageModifiers) {
            if (damageModifier.DamageMultiplier < 0.8f) {
                return true;
            }
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
    internal static Sprite? GetWorldSprite(Item item) {
        string? key = ResolveDumbKey(item);
        if (key is null) { return null; }

        Sprite? sprite = GetDumbPrefab(key)?.Sprite;
        sprite?.EnsureLazyLoaded();
        return sprite;
    }
    
    //replacement for the item's inventory icon
    internal static Sprite? GetInventorySprite(Item item) {
        string? key = ResolveDumbKey(item);
        if (key is null) { return null; }

        ItemPrefab? prefab = GetDumbPrefab(key);
        if (prefab is null) { return null; }

        Sprite? sprite = prefab.InventoryIcon ?? prefab.Sprite;
        sprite?.EnsureLazyLoaded();
        return sprite;
    }

    internal static void ClearCache() {
        artPrefabCache.Clear();
        inferredTagCache.Clear();
    }
}

[HarmonyPatch] //swaps the inventory icon of some items
internal static class ItemOverrideInventorySpriteGet {
    static MethodBase TargetMethod() => PatchTargets.Item_OverrideInventorySprite_Get;

    static void Postfix(Item __instance, ref Sprite? __result) {
        Character? controlled = Character.Controlled;
        if (!YAMJ.IsPlayerRaptor(controlled)) { return; }
        
        if (__result is not null) { return; } //something already set an override, leave it alone? not sure when this happens normally
        
        __result = Appraisal.GetInventorySprite(__instance);
    }
}

[HarmonyPatch] //swaps the in-world sprite of some items
internal static class ItemDrawSpriteSwap {
    static MethodBase TargetMethod() => PatchTargets.Item_Draw;

    static void Prefix(Item __instance, ref Sprite? ___activeSprite, out Sprite? __state) {
        __state = null;
        Character? controlled = Character.Controlled;
        if (!YAMJ.IsPlayerRaptor(controlled)) { return; }
        
        if (___activeSprite is null) { return; } //spriteless item, don't give it one

        Sprite? replacement = Appraisal.GetWorldSprite(__instance);
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
        if (!YAMJ.IsPlayerRaptor(controlled)) { return true; }
        
        string? key = Appraisal.ResolveDumbKey(__instance);
        if (key is not null) {
            __result = TextManager.Get("entityname.dumb." + key).Value;
            return false;
        }
        return true;
    }
}

[HarmonyPatch] //changes the description of some items
internal static class ItemDescGet {
    static MethodBase TargetMethod() => PatchTargets.Item_Description_Get;

    static bool Prefix(Item __instance, ref string __result) {
        Character? controlled = Character.Controlled;
        if (!YAMJ.IsPlayerRaptor(controlled)) { return true; }

        string? key = Appraisal.ResolveDumbKey(__instance);
        if (key is not null) {
            __result = TextManager.Get("entitydescription.dumb." + key).Value;
            return false;
        }
        return true;
    }
}