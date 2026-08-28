using Barotrauma.Items.Components;
using HarmonyLib;

namespace YAMJCS;

internal static partial class PatchTargets {
    public static MethodBase CharacterInventory_TryPutItem_Index =>
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
        
    public static MethodBase CharacterInventory_CanBePutInSlot =>
        AccessTools.Method(
            typeof(CharacterInventory),
            nameof(CharacterInventory.CanBePutInSlot),
            new[] {
                typeof(Item),
                typeof(int),
                typeof(bool)
            }) ??
        throw new Exception("CharacterInventory.CanBePutInSlot(Item, int, bool) not found");

    public static MethodBase CharacterInventory_TryPutItem_AllowedSlots =>
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
}

internal static class RaptorInventory {
    // every slot that counts as wearing the item, Hands and Any are deliberately left out
    public const InvSlotType WearSlots = InvSlotType.Bag | InvSlotType.InnerClothes | InvSlotType.OuterClothes | InvSlotType.Head | InvSlotType.Headset | InvSlotType.Card;
    private static readonly Identifier BondWithMechanic = "YAMJBond_Mechanic".ToIdentifier();
    private static readonly Identifier BondWithEngineer = "YAMJBond_Engineer".ToIdentifier();
    private static readonly Identifier BondWithDoctor = "YAMJBond_Doctor".ToIdentifier();
    private static readonly Identifier[] Edible = new Identifier[] {"petfood1", "petfood2", "petfood3", "monsterfood"}; //TODO expand
    private static readonly Identifier[] MechanicEdible = new Identifier[] {"ore","scrap"};
    private static readonly Identifier[] EngineerEdible = new Identifier[] {"reactorfuel"};
    private static readonly Identifier[] DoctorEdible = new Identifier[] {"unidentifiedgeneticmaterial"};

    public static bool CanBeWorn(Item item, Character character) {
        if (item.HasTag(YAMJ.RaptorOnlyTag) && !YAMJ.IsPlayerRaptor(character)) {
            return false;
        }
        
        return true;
    }
    
    public static bool IsGutSlot(CharacterInventory inventory, int index) {
        if (!YAMJ.IsPlayerRaptor(inventory.character)) { return false; } //only pRaptor HealthInterface slots are considered "gut" slots
        InvSlotType[] slotTypes = inventory.SlotTypes;
        return slotTypes[index].HasFlag(InvSlotType.HealthInterface);
    }

    public static bool CanDigest(Item item, Character character) {
        if (item.HasTag(Edible)) {
            return true;
        }

        if (item.HasTag(MechanicEdible) && character.HasTalent(BondWithMechanic)) {
            return true;
        }

        if (item.HasTag(EngineerEdible) && character.HasTalent(BondWithEngineer)) {
            return true;
        }

        if (item.HasTag(DoctorEdible) && character.HasTalent(BondWithDoctor)) {
            return true;
        }

        return false;
    }

    public static InvSlotType GrantSlots(Item item, InvSlotType slotTypes) {
        InvSlotType granted = InvSlotType.None;
        foreach (InvSlotType flag in EnumExtensions.GetIndividualFlags(slotTypes)) {
            if (flag == InvSlotType.None || item.AllowedSlots.Contains(flag)) {
                continue;
            }
            foreach (Pickable pickable in item.GetComponents<Pickable>()) {
                pickable.AllowedSlots.Add(flag);
            }
            granted = granted | flag;
        }
        return granted;
    }
    
    public static void RevokeSlots(Item item, InvSlotType slotTypes) {
        foreach (InvSlotType flag in EnumExtensions.GetIndividualFlags(slotTypes)) {
            if (flag == InvSlotType.None) { continue; }
            foreach (Pickable pickable in item.GetComponents<Pickable>()) {
                pickable.AllowedSlots.Remove(flag);
            }
        }
    }
}

[HarmonyPatch] //custom inventory slot logic - manual item placement
internal static class CharacterInventory_TryPutItem {
    static MethodBase TargetMethod() => PatchTargets.CharacterInventory_TryPutItem_Index;
    
    static bool Prefix(CharacterInventory __instance, Item item, int index, ref bool __result, out InvSlotType __state) {
        __state = InvSlotType.None;
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
            if (RaptorInventory.CanDigest(item, __instance.character)) {
                __state = RaptorInventory.GrantSlots(item, InvSlotType.HealthInterface);
                return true;
            }
            else {
                __result = false;
                return false;
            }
        }
        
        // default
        return true;
    }
    
    static void Postfix(Item item, InvSlotType __state) {
        // cleanup for item allowedSlots mutation
        if (__state == InvSlotType.None) { return; }
        RaptorInventory.RevokeSlots(item, __state);
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