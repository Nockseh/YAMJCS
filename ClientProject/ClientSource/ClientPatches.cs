using HarmonyLib;

namespace YAMJCS;

internal static class PatchTargets {
    public static MethodBase GUITextBox_Select => 
        AccessTools.Method(typeof(GUITextBox), "Select") ?? throw new Exception("GUITextBox.Select() not found");

    public static MethodBase CharacterHUD_AddToGUIUpdateList =>
        AccessTools.Method(typeof(CharacterHUD), "AddToGUIUpdateList") ?? throw new Exception("CharacterHUD.AddToGUIUpdateList() not found");
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
        
    }
}