using Barotrauma.Items.Components;
using HarmonyLib;

namespace YAMJCS;

internal static partial class PatchTargets {
    internal static MethodBase CharacterInventory_TryPutItem_Index =>
        AccessTools.Method(
            typeof(CharacterInventory),
            nameof(CharacterInventory.TryPutItem),
            new[] {
                typeof(Item),   //item
                typeof(int),    //index
                typeof(bool),   //allowSwapping
                typeof(bool),   //allowCombine
                typeof(Character),//user
                typeof(bool),   //createNetworkEvent = true
                typeof(bool),   //ignoreCondition = false
                typeof(bool)    //triggerOnInsertedEffects = true
            }) ??
        throw new Exception("CharacterInventory.TryPutItem(Item, int, bool, bool, Character, ...) not found");
        
    internal static MethodBase CharacterInventory_CanBePutInSlot =>
        AccessTools.Method(
            typeof(CharacterInventory),
            nameof(CharacterInventory.CanBePutInSlot),
            new[] {
                typeof(Item),
                typeof(int),
                typeof(bool)
            }) ??
        throw new Exception("CharacterInventory.CanBePutInSlot(Item, int, bool) not found");

    internal static MethodBase CharacterInventory_TryPutItem_AllowedSlots =>
        AccessTools.Method(
            typeof(CharacterInventory),
            nameof(CharacterInventory.TryPutItem),
            new[] {
                typeof(Item),
                typeof(Character),
                typeof(IEnumerable<InvSlotType>),
                typeof(bool),
                typeof(bool),
                typeof(bool)
            }) ??
        throw new Exception("CharacterInventory.TryPutItem(Item, Character, IEnumerable<InvSlotType>, ...) not found");
        
#if CLIENT
// unfortunately the client determines if it even sends the request to server so i gotta patch that too
// here seems like the best place to put it
    internal static MethodBase Item_AllowedSlots_Get =>
        AccessTools.PropertyGetter(
            typeof(Item),
            nameof(Item.AllowedSlots)
        ) ??
        throw new Exception("Item.AllowedSlots {get;} not found");
#endif

}

internal static class RaptorInventory {
    // every slot that counts as wearing the item, Hands and Any are deliberately left out
    internal const InvSlotType WearSlots = InvSlotType.Bag | InvSlotType.InnerClothes | InvSlotType.OuterClothes | InvSlotType.Head | InvSlotType.Headset | InvSlotType.Card;
    private static readonly Identifier BondWithMechanic = "yamjBondMechanic".ToIdentifier();
    private static readonly Identifier BondWithEngineer = "yamjBondEngineer".ToIdentifier();
    private static readonly Identifier BondWithDoctor = "yamjBondDoctor".ToIdentifier();

    // Helper Functions
    internal static bool CanBeWorn(Item item, Character character) {
        if (item.HasTag(YAMJ.RaptorOnlyTag) && !YAMJ.IsPlayerRaptor(character)) {
            return false;
        }
        
        return true;
    }
    
    internal static bool IsGutSlot(CharacterInventory inventory, int index) {
        if (!YAMJ.IsPlayerRaptor(inventory.character)) { return false; } //only pRaptor HealthInterface slots are considered "gut" slots
        InvSlotType[] slotTypes = inventory.SlotTypes;
        return slotTypes[index].HasFlag(InvSlotType.HealthInterface);
    }

    internal static bool CanDigest(Item item, Character character) {
        if (item.HasTag(YAMJ.BasicFood)) {
            return true;
        }

        if ((item.HasTag(YAMJ.MechanicFood) || item.HasTag(YAMJ.ShrapnelAmmo)) && character.HasTalent(BondWithMechanic)) {
            return true;
        }

        if (item.HasTag(YAMJ.EngineerFood) && character.HasTalent(BondWithEngineer)) {
            return true;
        }

        if (item.HasTag(YAMJ.DoctorFood) && character.HasTalent(BondWithDoctor)) {
            return true;
        }

        return false;
    }
    
    // GrantSlots() and RevokeSlots() are no longer used but might be useful
    /*
    internal static InvSlotType GrantSlots(Item item, InvSlotType slotTypes) {
        InvSlotType granted = InvSlotType.None;
        foreach (InvSlotType flag in EnumExtensions.GetIndividualFlags(slotTypes)) {
            
            // if (flag == InvSlotType.None || item.AllowedSlots.Contains(flag)) { continue; }
            // the client is patched to lie about AllowedSlots so i'm pretty sure you can't use the above code anymore
            bool alreadyDeclared = false;
            foreach (Pickable pickable in item.GetComponents<Pickable>()) {
                if (pickable.AllowedSlots.Contains(flag)) {
                    alreadyDeclared = true;
                    break;
                }
            }
            if (flag == InvSlotType.None || alreadyDeclared) { continue; }
            
            foreach (Pickable pickable in item.GetComponents<Pickable>()) {
                pickable.AllowedSlots.Add(flag);
            }
            granted = granted | flag;
        }
        return granted;
    }
    
    internal static void RevokeSlots(Item item, InvSlotType slotTypes) {
        foreach (InvSlotType flag in EnumExtensions.GetIndividualFlags(slotTypes)) {
            if (flag == InvSlotType.None) { continue; }
            foreach (Pickable pickable in item.GetComponents<Pickable>()) {
                pickable.AllowedSlots.Remove(flag);
            }
        }
    }
    */

    // Custom CharacterInventory.PutItem() for allowing indexed non-Any slots
    private static readonly MethodInfo? PutItemMethod =
        AccessTools.Method(
            typeof(CharacterInventory),
            nameof(CharacterInventory.PutItem),
            new[] {
                typeof(Item),   //item
                typeof(int),    //i
                typeof(Character),//user
                typeof(bool),   //removeItem = true
                typeof(bool),   //createNetworkEvent = true
                typeof(bool)    //triggerOnInsertedEffects = true
            });

    internal static bool PutInSlot(CharacterInventory inventory, Item item, int index, Character user, bool createNetworkEvent, bool triggerOnInsertedEffects) {
        if (PutItemMethod is null) {
            YAMJ.Log("CharacterInventory.PutItem() not found");
            return false;
        }
        PutItemMethod.Invoke(inventory, new object[] { item, index, user, true, createNetworkEvent, triggerOnInsertedEffects });
        return true;
    }
}

[HarmonyPatch] //custom inventory slot logic - manual item placement
internal static class CharacterInventory_TryPutItem {
    static MethodBase TargetMethod() => PatchTargets.CharacterInventory_TryPutItem_Index;
    
    static bool Prefix(CharacterInventory __instance, Item item, int index, Character user, bool createNetworkEvent, bool triggerOnInsertedEffects, ref bool __result) {
        
        //necessary to replicate vanilla safeguards cus i might exit early
        if (item is null) { return true; }
        InvSlotType[] slotTypes = __instance.SlotTypes;
        if (slotTypes is null || index < 0 || index >= slotTypes.Length) { return true; }
        InvSlotType slotType = slotTypes[index];

        // disallow non-raptors from equipping raptor gear
        if (slotType.HasAnyFlag(RaptorInventory.WearSlots) && !RaptorInventory.CanBeWorn(item, __instance.character)) {
            __result = false;
            return false;
        }

        // allow raptors to eat things
        if (RaptorInventory.IsGutSlot(__instance, index)) {
            if (!RaptorInventory.CanDigest(item, __instance.character)) {
                __result = false;
                return false;
            }

            if (__instance.GetItemsAt(index).Any()) { return true; }
            
            //vanilla only uses indexes for non-Any slots, must use custom PutInSlot() method
            if (RaptorInventory.PutInSlot(__instance, item, index, user, createNetworkEvent, triggerOnInsertedEffects)) {
                __result = true;
                return false;
            }
        }

        // default
        return true;
    }
}

[HarmonyPatch] //custom inventory slot logic - allowance prediction, slot glow ui, ai behavior
internal static class CharacterInventory_CanBePutInSlot {
    static MethodBase TargetMethod() => PatchTargets.CharacterInventory_CanBePutInSlot;
    
    static void Postfix(CharacterInventory __instance, Item item, int i, ref bool __result) {
        //vanilla safeguards
        if (item is null) { return; }
        InvSlotType[] slotTypes = __instance.SlotTypes;
        if (slotTypes is null || i < 0 || i >= slotTypes.Length) { return; }
        InvSlotType slotType = slotTypes[i];
        
        // pRaptor gear is only wearable by raptors
        if (slotType.HasAnyFlag(RaptorInventory.WearSlots)) {
            if (!RaptorInventory.CanBeWorn(item, __instance.character)) {
                __result = false;
                return;
            }
        }
        
        // some things are edible (HealthInterface slots on pRaptors)
        if (RaptorInventory.IsGutSlot(__instance, i)) {
            if (__instance.GetItemsAt(i).Any()) {
                __result = false;
                return; //replicating vanilla safeguards again; non-Any slots only allow SINGLE stacks
            }

            if (RaptorInventory.CanDigest(item, __instance.character)) {
                __result = true;
                return;
            }
            else {
                __result = false;
                return;
            }
        }
    }
}

[HarmonyPatch] //custom inventory slot logic - auto-equip handling
internal static class CharacterInventory_AutoEquipPatch {
    static MethodBase TargetMethod() => PatchTargets.CharacterInventory_TryPutItem_AllowedSlots;

    static void Prefix(CharacterInventory __instance, Item item, ref IEnumerable<InvSlotType>? allowedSlots) {
        //disallow raptor gear from being auto-equipped by humans
        if (item is null || allowedSlots is null) { return; }
        if (!RaptorInventory.CanBeWorn(item, __instance.character)) {
            allowedSlots = allowedSlots
                .Where(slot => (slot & RaptorInventory.WearSlots) == InvSlotType.None)
                .ToList();
        }
        
        //disallow raptors from eating genes without talent (since they have HealthInterface slotType already)
        if (YAMJ.IsPlayerRaptor(__instance.character) && !RaptorInventory.CanDigest(item, __instance.character)) {
            allowedSlots = allowedSlots
                .Where(slot => !slot.HasFlag(InvSlotType.HealthInterface))
                .ToList();
        }
    }
}

#if CLIENT
[HarmonyPatch] //client reads edibles as having HealthInterface slotType. THIS IS READ SEVERAL TIMES EVERY FRAME.
internal static class Item_AllowedSlots_Get {
    static MethodBase TargetMethod() => PatchTargets.Item_AllowedSlots_Get;
    
    static void Postfix(Item __instance, ref IEnumerable<InvSlotType> __result) {
        if (Inventory.DraggingItems.Count == 0 && CharacterHealth.OpenHealthWindow is null) { return; }

        Character? controlled = Character.Controlled;
        if (controlled is null || !YAMJ.IsPlayerRaptor(controlled)) { return; }
        if (!RaptorInventory.CanDigest(__instance, controlled)) { return; }
        if (__result.Contains(InvSlotType.HealthInterface)) { return; }

        __result = __result.Append(InvSlotType.HealthInterface);
    }
}
#endif