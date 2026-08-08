using System.Reflection;
using HarmonyLib;
using TypeExtensions = System.TypeExtensions;

namespace YAMJCS {
    
    internal static partial class PatchTargets {
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
            
        public static MethodBase Character_DoInteractionUpdate =>
            AccessTools.Method(
                typeof(Character),
                nameof(Character.DoInteractionUpdate),
                new[] {
                    typeof(float),
                    typeof(Vector2)
                }) ??
            throw new Exception("Character.DoInteractionUpdate(float, Vector2) not found");
    }
    
internal static class RaptorGear {
    // every slot that counts as wearing the item, Hands and Any are deliberately left out
    public const InvSlotType WearSlots = InvSlotType.Bag | InvSlotType.InnerClothes | InvSlotType.OuterClothes | InvSlotType.Head | InvSlotType.Headset | InvSlotType.Card;

    public static bool IsRestricted(Item? item, Character? character) {
        if (item is not null && item.HasTag(YAMJ.RaptorOnlyTag) && !YAMJ.IsPlayerRaptor(character)) {
            return true;
        }
        return false;
    }
}

[HarmonyPatch] //allows baby raptors to be picked up with LMB
internal static class RaptorPetPickup {
    static MethodBase TargetMethod() => PatchTargets.Character_DoInteractionUpdate;

    static void Postfix(Character __instance) {
        Character? focusedCharacter = __instance.FocusedCharacter;
        if (focusedCharacter is null || !YAMJ.IsRaptorBaby(focusedCharacter)) { return; }
        if (focusedCharacter.Removed || focusedCharacter.IsDead) { return; }
        if (!__instance.CanInteract || __instance.IsIncapacitated) { return; }

        #if CLIENT
            focusedCharacter.SetCustomInteract(null, CharacterHUD.GetCachedHudText("yamjHint.pickUp", InputType.Select));
        #endif

        if (!__instance.IsKeyHit(InputType.Select)) { return; }

        // the server owns the spawn/despawn, a client predicting it would only desync itself
        if (GameMain.NetworkMember != null && GameMain.NetworkMember.IsClient) { return; }
        
        if (Entity.Spawner is null) { return; }
        // server-side IsKeyHit(Select) isn't edge-detected, so the same click can arrive on
        // several consecutive frames, this keeps it from spawning a second pet item
        if (Entity.Spawner.IsInRemoveQueue(focusedCharacter)) { return; }

        ItemPrefab? petItem = YAMJ.FindRaptorBabyItemPrefab();
        if (petItem is null) {
            YAMJ.Log($"Item prefab '{YAMJ.RaptorBabyItemId}' not found, cannot pick up {focusedCharacter.Name}.");
            return;
        }

        if (!__instance.Inventory.CanProbablyBePut(petItem)) {
            #if CLIENT
                YAMJClient.ShowWarning(TextManager.Get("yamjMsg.noRoomInInventory").Value);
            #endif
            return;
        }

        float storedHealth = MathHelper.Clamp(focusedCharacter.HealthPercentage, 1f, 100f);
        Entity.Spawner.AddEntityToRemoveQueue(focusedCharacter);
        Entity.Spawner.AddItemToSpawnQueue(petItem, __instance.Inventory, condition: storedHealth, spawnIfInventoryFull: true);
    }
}

[HarmonyPatch] //blocks non-raptors from dragging raptor-only gear into an equip slot
internal static class RaptorGearCanBePutInSlot {
    static MethodBase TargetMethod() => PatchTargets.CharacterInventory_CanBePutInSlot;

    static void Postfix(CharacterInventory __instance, Item item, int i, ref bool __result) {
        if (!__result) { return; }
        if (!RaptorGear.IsRestricted(item, __instance.character)) { return; }

        InvSlotType[] slotTypes = __instance.SlotTypes;
        if (slotTypes is null || i < 0 || i >= slotTypes.Length) { return; }
        if ((slotTypes[i] & RaptorGear.WearSlots) == InvSlotType.None) { return; }

        __result = false;
    }
}

[HarmonyPatch] //blocks auto-equipping raptor-only gear on pickup, spawn loadouts and bot AI
internal static class RaptorGearAutoEquip {
    static MethodBase TargetMethod() => PatchTargets.CharacterInventory_TryPutItem_AllowedSlots;

    static void Prefix(CharacterInventory __instance, Item item, ref IEnumerable<InvSlotType>? allowedSlots) {
        if (allowedSlots is null) { return; }
        if (!RaptorGear.IsRestricted(item, __instance.character)) { return; }

        allowedSlots = allowedSlots
            .Where(slot => (slot & RaptorGear.WearSlots) == InvSlotType.None)
            .ToList();
    }
}
    
}