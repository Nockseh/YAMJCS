using HarmonyLib;
using Barotrauma.Items.Components;
using Microsoft.Xna.Framework.Graphics;
namespace YAMJCS;

internal static partial class PatchTargets {
    public static MethodBase GUITextBox_Select => 
        AccessTools.Method(
            typeof(GUITextBox),
            nameof(GUITextBox.Select)) ??
        throw new Exception("GUITextBox.Select() not found");

    public static MethodBase CharacterHUD_Draw =>
        AccessTools.Method(
            typeof(CharacterHUD),
            nameof(CharacterHUD.Draw),
            new[] {
                typeof(SpriteBatch),
                typeof(Character),
                typeof(Camera)
            }) ??
        throw new Exception("CharacterHUD.Draw(SpriteBatch, Character, Camera) not found");

    public static MethodBase CharacterHUD_AddToGUIUpdateList =>
        AccessTools.Method(
            typeof(CharacterHUD),
            nameof(CharacterHUD.AddToGUIUpdateList)) ??
        throw new Exception("CharacterHUD.AddToGUIUpdateList() not found");

    public static MethodBase Character_CanAim_Get =>
        AccessTools.PropertyGetter(
            typeof(Character),
            nameof(Character.CanAim)) ??
        throw new Exception("Character.CanAim {get} not found");
    
    public static MethodBase Repairable_CreateGUI =>
        AccessTools.DeclaredMethod(
            typeof(Repairable),
            nameof(Repairable.CreateGUI)) ??
        throw new Exception("Repairable.CreateGUI() not found");
    
    public static MethodBase CrewManager_CanIssueOrders_Get =>
        AccessTools.PropertyGetter(
            typeof(CrewManager),
            nameof(CrewManager.CanIssueOrders)) ??
        throw new Exception("CrewManager.CanIssueOrders {get} not found");

    public static MethodBase Item_OverrideInventorySprite_Get =>
        AccessTools.PropertyGetter(
            typeof(Item),
            nameof(Item.OverrideInventorySprite)) ??
        throw new Exception("Item.OverrideInventorySprite {get} not found");
        
    public static MethodBase Item_Draw =>
        AccessTools.Method(
            typeof(Item),
            nameof(Item.Draw),
            new[] {
                typeof(SpriteBatch),
                typeof(bool),
                typeof(bool),
                typeof(Color?),
                typeof(float?)
            }) ??
        throw new Exception("Item.Draw(SpriteBatch, bool, bool, Color?, float?) not found");
}

[HarmonyPatch] //prevents pRaptors from using the chatbox to send msgs
internal static class ChatBoxFocusPatch {
    private static MethodBase TargetMethod() => PatchTargets.GUITextBox_Select;

    static bool Prefix(GUITextBox __instance) {
        ChatBox? chatBox = ChatBox.GetChatBox();
        if (chatBox is null) return true;
        
        Character? controlled = Character.Controlled;
        if (controlled is null || !String.Equals(controlled.SpeciesName.Value, YAMJ.PlayerRaptorSpecies)) return true;
        
        if (!ReferenceEquals(__instance, chatBox.InputBox)) return true;

        YAMJClient.ShowWarning(TextManager.Get("yamjMsg.cantSpeak").Value);
        __instance.Deselect();
        return false;
    }
}

[HarmonyPatch] //draws raptor thermal + ore vision 
internal static class CharHudDraw {
    private static MethodBase TargetMethod() => PatchTargets.CharacterHUD_Draw;

    static void Prefix(SpriteBatch spriteBatch, Character character, Camera cam) {
        RaptorVision.Draw(spriteBatch, character, cam);
    }
}

[HarmonyPatch] //fires every frame, mostly for hud and keybind stuff 
internal static class CharHudAddToUpdateList { //basically fires every frame 
    private static MethodBase TargetMethod() => PatchTargets.CharacterHUD_AddToGUIUpdateList;

    static void Postfix() {
        //TODO: initialize these somewhere cleaner where they don't have to ignore every frame
        RaptorSpeech.Initialize(); //automatically ignores extra calls
        RaptorSpeech.Update();
        
        RaptorHeadset.Initialize(); //automatically ignores extra calls
        RaptorHeadset.Update();
        
        RaptorVision.Update();
    }
}

[HarmonyPatch] //prevents pRaptors from using some handheld items (unless talented)
internal static class CharacterCanAimGet {
    static MethodBase TargetMethod() => PatchTargets.Character_CanAim_Get;

    static void Postfix(Character __instance, ref bool __result) {
        if (!__result) { return; }
        if (YAMJ.IsPlayerRaptor(__instance)) {
            IEnumerable<Item> heldItems = __instance.HeldItems;
            if (heldItems is null) {
                __result = false;
                return;
            }
            
            bool holdingWeapon = false;
            bool holdingTool = false;
            foreach (Item item in heldItems) {
                if (item.HasTag("weapon".ToIdentifier())) {
                    holdingWeapon = true;
                }
                if (item.HasTag("tool".ToIdentifier())) {
                    holdingTool = true;
                }
            }
            
            if (!YAMJ.HasTalent(__instance, "YAMJSpeechCombat") && holdingWeapon) {
                __result = false;
            }
            if (!YAMJ.HasTalent(__instance, "YAMJCanUseTools") && holdingTool) {
                __result = false;
            }
            
            if (__instance == Character.Controlled && !__result) {
                YAMJClient.ShowWarning(TextManager.Get("yamjMsg.cantUseItem").Value);
            }
        }
    }
}

[HarmonyPatch] //prevents pRaptors from pressing the repair button (unless talented)
internal static class RepairableCreateGUI {
    static MethodBase TargetMethod() => PatchTargets.Repairable_CreateGUI;

    static void Postfix(Repairable __instance) {
        if (__instance.RepairButton is null) return;

        GUIButton.OnClickedHandler oldOnClicked = __instance.RepairButton.OnClicked;

        __instance.RepairButton.OnClicked = (btn, obj) => {
            if (YAMJ.IsPlayerRaptor(Character.Controlled) && !YAMJ.HasTalent(Character.Controlled, "YAMJCanUseTools")) {
                YAMJClient.ShowWarning(TextManager.Get("yamjMsg.cantRepair").Value);
            }

            return oldOnClicked?.Invoke(btn, obj) ?? true;
        };
    }
}

[HarmonyPatch] //prevents pRaptors from issuing orders via the command wheel
internal static class CanIssueOrdersGet {
    static MethodBase TargetMethod() => PatchTargets.CrewManager_CanIssueOrders_Get;

    static bool Prefix(ref bool __result) {
        if (YAMJ.IsPlayerRaptor(Character.Controlled)) {
            __result = false;
            return false;
        } else {
            return true;
        }
    }
}