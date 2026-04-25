using HarmonyLib;
using Barotrauma.Items.Components;
namespace YAMJCS;

internal static class PatchTargets {
    public static MethodBase GUITextBox_Select => 
        AccessTools.Method(
            typeof(GUITextBox),
            nameof(GUITextBox.Select)) ??
        throw new Exception("GUITextBox.Select() not found");

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
    
    public static MethodBase CrewManager_CanIssueOrders_Get =>
        AccessTools.PropertyGetter(
            typeof(CrewManager),
            nameof(CrewManager.CanIssueOrders)) ??
        throw new Exception("CrewManager.CanIssueOrders {get} not found");

    public static MethodBase CharacterInfo_LoadHeadElement =>
        AccessTools.Method(
            typeof(CharacterInfo),
            nameof(CharacterInfo.LoadHeadElement),
            new[] {
                typeof(bool),
                typeof(bool) }) ??
        throw new Exception("CharacterInfo.LoadHeadElement(bool, bool) not found");
}

internal static class ItemTags {
    public static readonly Identifier[] mediChem = { "medical", "chem" };
    public static readonly Identifier[] tech = {
        "logic", "sonar", "sensor", "signal", "sound", "lightcomponent", "mobilebattery", "detonator", "alienartifact",
        "smallalienartifact"
    };
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

[HarmonyPatch]
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

[HarmonyPatch]
internal static class ItemNameGet {
    static MethodBase TargetMethod() => PatchTargets.Item_Name_Get;

    static bool Prefix(Item __instance, ref string __result) {
        Character? controlled = Character.Controlled;
        if (YAMJ.IsPlayerRaptor(controlled)) {
            if (__instance.HasTag("weapon") && !YAMJ.HasTalent(controlled, "YAMJSpeechCombat")) {
                __result = TextManager.Get("entityname.dumb.weapon").Value;
                return false;
            }
            if (__instance.HasTag("tool") && !YAMJ.HasTalent(controlled, "YAMJCanUseTools")) {
                __result = TextManager.Get("entityname.dumb.repairTool").Value;
                return false;
            }

            if (!YAMJ.HasTalent(controlled, "YAMJSpeechAdvanced")) {
                if (__instance.HasTag("clothing")) {
                    __result = TextManager.Get("entityname.dumb.clothing").Value;
                    return false;
                }
                if (__instance.HasTag("deepdiving")) {
                    __result = TextManager.Get("entityname.dumb.deepdiving").Value;
                    return false;
                }
                if (__instance.HasTag(ItemTags.mediChem)) {
                    __result = TextManager.Get("entityname.dumb.medichem").Value;
                    return false;
                }
                if (__instance.HasTag("ammobox")) {
                    __result = TextManager.Get("entityname.dumb.ammobox").Value;
                    return false;
                }
                if (__instance.HasTag("scooter")) {
                    __result = TextManager.Get("entityname.dumb.scooter").Value;
                    return false;
                }
                if (__instance.HasTag(ItemTags.tech)) {
                    __result = TextManager.Get("entityname.dumb.tech").Value;
                    return false;
                }
            }
        }
        return true;
    }
}

[HarmonyPatch]
internal static class ItemDescGet {
    static MethodBase TargetMethod() => PatchTargets.Item_Description_Get;

    static bool Prefix(Item __instance, ref string __result) {
        Character? controlled = Character.Controlled;
        if (YAMJ.IsPlayerRaptor(controlled)) {
            if (__instance.HasTag("weapon") && !YAMJ.HasTalent(controlled, "YAMJSpeechCombat")) {
                __result = TextManager.Get("entitydescription.dumb.weapon").Value;
                return false;
            }
            if (__instance.HasTag("tool") && !YAMJ.HasTalent(controlled, "YAMJCanUseTools")) {
                __result = TextManager.Get("entitydescription.dumb.repairTool").Value;
                return false;
            }

            if (!YAMJ.HasTalent(controlled, "YAMJSpeechAdvanced")) {
                if (__instance.HasTag("clothing")) {
                    __result = TextManager.Get("entitydescription.dumb.clothing").Value;
                    return false;
                }
                if (__instance.HasTag("deepdiving")) {
                    __result = TextManager.Get("entitydescription.dumb.deepdiving").Value;
                    return false;
                }
                if (__instance.HasTag(ItemTags.mediChem)) {
                    __result = TextManager.Get("entitydescription.dumb.mediChem").Value;
                    return false;
                }
                if (__instance.HasTag("ammobox")) {
                    __result = TextManager.Get("entitydescription.dumb.ammobox").Value;
                    return false;
                }
                if (__instance.HasTag("scooter")) {
                    __result = TextManager.Get("entitydescription.dumb.scooter").Value;
                    return false;
                }
                if (__instance.HasTag(ItemTags.tech)) {
                    __result = TextManager.Get("entitydescription.dumb.tech").Value;
                    return false;
                }
            }
        }
        return true;
    }
}

[HarmonyPatch]
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

[HarmonyPatch]
public static class CharacterInfo_LoadHeadElement {
    static MethodBase TargetMethod() => PatchTargets.CharacterInfo_LoadHeadElement;
    
    static bool Prefix(ref CharacterInfo __instance, bool loadHeadSprite, bool loadHeadSpriteTags) {
        if (!YAMJ.HasPlayerRaptorJob(__instance)) return true;
        
        CharacterInfo raptorInfo = new CharacterInfo("Mudraptor_player".ToIdentifier());
        if (raptorInfo.Ragdoll?.MainElement == null) {
            YAMJ.Log("raptorInfo.Ragdoll.MainElement is null");
            return true;
        }

        if (raptorInfo.Head == null) {
            YAMJ.Log("raptorInfo.Head is null");
            return true;
        }
        
        foreach (var limbElement in raptorInfo.Ragdoll.MainElement.Elements()) {
            if (!limbElement.GetAttributeString("type", "").Equals("head", StringComparison.OrdinalIgnoreCase)) {
                continue;
            }

            ContentXElement spriteElement = limbElement.GetChildElement("sprite");
            if (spriteElement == null) {
                YAMJ.Log("headElement has no sprite child element");
                return false;
            }

            string spritePath = spriteElement.GetAttributeContentPath("texture")?.Value;
            if (string.IsNullOrEmpty(spritePath)) {
                YAMJ.Log("spritePath is null or empty");
                return false;
            }

            spritePath = raptorInfo.ReplaceVars(spritePath);

            string dir = Path.GetDirectoryName(spritePath);
            string fileName = Path.GetFileNameWithoutExtension(spritePath);

            if (string.IsNullOrEmpty(dir) || string.IsNullOrEmpty(fileName)) {
                YAMJ.Log("is null or empty (dir, fileName)");
                return false;
            }

            foreach (string file in Directory.GetFiles(dir)) {
                if (!file.EndsWith(".png", StringComparison.OrdinalIgnoreCase)) continue;

                string fileWithoutTags = Path.GetFileNameWithoutExtension(file)
                    .Split('[', ']')
                    .First();

                if (fileWithoutTags != fileName) continue;

                if (loadHeadSprite) {
                    SetPrivateProperty(__instance, "HeadSprite", new Sprite(spriteElement, "", file));
                    SetPrivateProperty(__instance, "Portrait", new Sprite(spriteElement, "", file) {
                        RelativeOrigin = Vector2.Zero
                    });

                    Sprite headSprite = __instance.HeadSprite;
                    Point sheet = GetRaptorSheetIndex(__instance);
                    headSprite.SourceRect = new Rectangle(
                        sheet.X * headSprite.SourceRect.Width,
                        sheet.Y * headSprite.SourceRect.Height,
                        headSprite.SourceRect.Width,
                        headSprite.SourceRect.Height);
                    AccessTools.Field(typeof(CharacterInfo), "attachmentSprites").SetValue(__instance, new List<WearableSprite>());
                }

                if (loadHeadSpriteTags) {
                    var tags = file.Split('[', ']')
                        .Skip(1)
                        .Select(id => id.ToIdentifier())
                        .ToList();

                    if (tags.Any()) {
                        tags.RemoveAt(tags.Count - 1);
                    }

                    AccessTools.Property(typeof(CharacterInfo), "SpriteTags")
                        .SetValue(__instance, tags);

                    AccessTools.Field(typeof(CharacterInfo), "spriteTagsLoaded")
                        .SetValue(__instance, true);
                }
                return false;
            }
            return false;
        }
        return false;
    }
    
    private static void SetPrivateProperty(object instance, string propertyName, object value)
    {
        AccessTools.PropertySetter(instance.GetType(), propertyName)
            .Invoke(instance, new[] { value });
    }

    private static Point GetRaptorSheetIndex(CharacterInfo info)
    {
        int index = Math.Abs(info.GetIdentifierUsingOriginalName()) % 16;
        return new Point(index % 4, index / 4);
    }
}