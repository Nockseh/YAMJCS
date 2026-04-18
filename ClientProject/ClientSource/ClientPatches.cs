using HarmonyLib;

namespace YAMJCS;

internal static class PatchTargets {
    public static MethodBase GUITextBox_Select => 
        AccessTools.Method(typeof(GUITextBox), "Select") ?? throw new Exception("GUITextBox.Select() not found");

    public static MethodBase CharacterHUD_AddToGUIUpdateList =>
        AccessTools.Method(typeof(CharacterHUD), "AddToGUIUpdateList") ?? throw new Exception("CharacterHUD.AddToGUIUpdateList() not found");

    public static MethodBase Character_CanAim_Get =>
        AccessTools.PropertyGetter(typeof(Character), nameof(Character.CanAim)) ??
        throw new Exception("Character.CanAim {get} not found");
}

[HarmonyPatch]
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

[HarmonyPatch]
internal static class CharHudAddToUpdateList { //basically fires every frame
    private static MethodBase TargetMethod() => PatchTargets.CharacterHUD_AddToGUIUpdateList;

    static void Postfix() {
        RaptorSpeech.Initialize(); //automatically ignores extra calls
        RaptorSpeech.Update();
    }
}

[HarmonyPatch]
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