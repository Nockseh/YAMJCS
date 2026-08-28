using System.Reflection;
using HarmonyLib;
using TypeExtensions = System.TypeExtensions;

namespace YAMJCS {
    
    internal static partial class PatchTargets {
            
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

[HarmonyPatch] //allows baby raptors to be picked up with LMB
internal static class RaptorBbyPickup {
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
    
}