using HarmonyLib;
using Microsoft.Xna.Framework.Graphics;

namespace YAMJCS;

internal static partial class PatchTargets {
    public static MethodBase CharacterInfo_LoadHeadElement =>
        AccessTools.Method(
            typeof(CharacterInfo),
            nameof(CharacterInfo.LoadHeadElement),
            new[] {
                typeof(bool),
                typeof(bool)
            }) ??
        throw new Exception("CharacterInfo.LoadHeadElement(bool, bool) not found");

    public static MethodBase CharacterInfo_DrawIcon =>
        AccessTools.Method(
            typeof(CharacterInfo),
            nameof(CharacterInfo.DrawIcon),
            new[] {
                typeof(SpriteBatch),
                typeof(Vector2),
                typeof(Vector2),
                typeof(bool)
            }) ??
        throw new Exception("CharacterInfo.DrawIcon(SpriteBatch, Vector2, Vector2, bool) not found");

    public static MethodBase Character_LoadHeadAttachments =>
        AccessTools.Method(
            typeof(Character),
            nameof(Character.LoadHeadAttachments)) ??
        throw new Exception("Character.LoadHeadAttachments not found");
        
    public static MethodBase AppearanceCustomizationMenu_OpenHeadSelection =>
        AccessTools.Method(
            typeof(CharacterInfo.AppearanceCustomizationMenu),
            nameof(CharacterInfo.AppearanceCustomizationMenu.OpenHeadSelection)) ??
        throw new Exception("AppearanceCustomizationMenu.OpenHeadSelection() not found");
}

internal static class RaptorPortrait {
    private static CharacterInfo? template;
    private static ContentXElement? spriteElement;
    private static string? spriteFile;
    private static bool sourceResolved;
    private static PropertyInfo? jobPreferencesProperty;

    private static bool RaptorIsTopJobPreference() {
        if (GameMain.NetLobbyScreen is null) { return false; }
        
        //access GameMain.NetLobbyScreen.JobPreferences
        // jobPreferencesProperty ??= AccessTools.Property(typeof(NetLobbyScreen), "JobPreferences");
        // if (jobPreferencesProperty?.GetValue(GameMain.NetLobbyScreen) is not System.Collections.IEnumerable prefs) {
        //     YAMJ.Log("GameMain.NetLobbyScreen.JobPreferences not accessible");
        //     return false;
        // }

        IEnumerable prefs = GameMain.NetLobbyScreen.JobPreferences;
    
        foreach (object? entry in prefs) {
            if (entry is null) { return false; }
            object? prefab = AccessTools.Field(entry.GetType(), "Prefab")?.GetValue(entry);
            return prefab is JobPrefab job && string.Equals(job.Identifier.Value, YAMJ.PlayerRaptorJobId, StringComparison.OrdinalIgnoreCase);
        }
        return false;
    }

    public static bool ShouldUseRaptorHead(CharacterInfo? info) {
        if (info is null) { return false; }
        if (YAMJ.HasPlayerRaptorJob(info)) { return true; }
        
        //lobby screen builds charInfo with null job, must infer
        return ReferenceEquals(info, GameMain.Client?.CharacterInfo) && RaptorIsTopJobPreference();
    }

    public static Sprite? CreateHeadSprite() =>
        TryResolveSource() ? new Sprite(spriteElement, "", spriteFile) : null;
        
    public static Sprite? CreateHeadSprite(Vector2 sheetIndex) {
        Sprite? sprite = CreateHeadSprite();
        if (sprite is null) { return null; }

        Rectangle rect = sprite.SourceRect;
        sprite.SourceRect = new Rectangle(
            (int)sheetIndex.X * rect.Width,
            (int)sheetIndex.Y * rect.Height,
            rect.Width,
            rect.Height);
        return sprite;
    }
    
    public static Sprite? CreatePortraitSprite() =>
        TryResolveSource() ? new Sprite(spriteElement, "", spriteFile) { RelativeOrigin = Vector2.Zero } : null;

    public static List<Identifier> GetSpriteTags() {
        if (!TryResolveSource()) {
            return new List<Identifier>();
        }

        List<Identifier> tags = spriteFile!
            .Split('[', ']')
            .Skip(1)
            .Select(id => id.ToIdentifier())
            .ToList();

        if (tags.Count > 0) {
            tags.RemoveAt(tags.Count - 1); //the trailing ".png" fragment
        }

        return tags;
    }
    
    private static bool TryResolveSource() {
        if (sourceResolved) {
            return spriteElement is not null && spriteFile is not null;
        }

        sourceResolved = true;

        template ??= new CharacterInfo(YAMJ.PlayerRaptorSpecies.ToIdentifier());
        if (template.Ragdoll?.MainElement is null) {
            YAMJ.Log("Raptor portrait: the raptor ragdoll has no main element.");
            return false;
        }

        ContentXElement? headElement = template.Ragdoll.MainElement
            .Elements()
            .FirstOrDefault(e => e.GetAttributeString("type", "").Equals("head", StringComparison.OrdinalIgnoreCase));
        if (headElement is null) {
            YAMJ.Log("Raptor portrait: the raptor ragdoll has no head limb.");
            return false;
        }

        ContentXElement? element = headElement.GetChildElement("sprite");
        string? path = element?.GetAttributeContentPath("texture")?.Value;
        if (element is null || string.IsNullOrEmpty(path)) {
            YAMJ.Log("Raptor portrait: the raptor head limb has no sprite texture.");
            return false;
        }

        path = template.ReplaceVars(path);
        string? dir = Path.GetDirectoryName(path);
        string? fileName = Path.GetFileNameWithoutExtension(path);
        if (string.IsNullOrEmpty(dir) || string.IsNullOrEmpty(fileName)) {
            YAMJ.Log($"Raptor portrait: could not split the head sprite path '{path}'.");
            return false;
        }

        //the on-disk name may carry [tags] the ragdoll path doesn't, so match on the stem
        foreach (string file in Directory.GetFiles(dir)) {
            if (!file.EndsWith(".png", StringComparison.OrdinalIgnoreCase)) {
                continue;
            }

            if (Path.GetFileNameWithoutExtension(file).Split('[', ']').First() != fileName) {
                continue;
            }

            spriteElement = element;
            spriteFile = file;
            return true;
        }

        YAMJ.Log($"Raptor portrait: no head sheet matching '{fileName}' in '{dir}'.");
        return false;
    }

    public static void SetPrivateProperty(object instance, string propertyName, object? value) {
        AccessTools.PropertySetter(instance.GetType(), propertyName)
            .Invoke(instance, new[] { value });
    }
}

[HarmonyPatch] //replaces human head draws with playerRaptor
internal static class CharacterInfoLoadHeadElement {
    static MethodBase TargetMethod() => PatchTargets.CharacterInfo_LoadHeadElement;

    static bool Prefix(CharacterInfo __instance, bool loadHeadSprite, bool loadHeadSpriteTags) {
        if (!RaptorPortrait.ShouldUseRaptorHead(__instance)) {
            return true;
        }

        if (loadHeadSprite) {
            Sprite? headSprite = RaptorPortrait.CreateHeadSprite();
            if (headSprite is null) {
                return true;
            } //fall back to the human head rather than draw nothing

            RaptorPortrait.SetPrivateProperty(__instance, nameof(CharacterInfo.HeadSprite), headSprite);
            RaptorPortrait.SetPrivateProperty(__instance, nameof(CharacterInfo.Portrait),
                RaptorPortrait.CreatePortraitSprite());

            //the human hair/beard/moustache/face sprites would otherwise be drawn over the snout
            AccessTools.Field(typeof(CharacterInfo), "attachmentSprites")
                .SetValue(__instance, new List<WearableSprite>());
        }

        if (loadHeadSpriteTags) {
            AccessTools.Property(typeof(CharacterInfo), nameof(CharacterInfo.SpriteTags))
                .SetValue(__instance, RaptorPortrait.GetSpriteTags());
            AccessTools.Field(typeof(CharacterInfo), "spriteTagsLoaded")
                .SetValue(__instance, true);
        }

        return false;
    }
}

[HarmonyPatch] //replaces human head draws with playerRaptor
internal static class CharacterInfoDrawIcon {
    static MethodBase TargetMethod() => PatchTargets.CharacterInfo_DrawIcon;

    static bool Prefix(CharacterInfo __instance, SpriteBatch spriteBatch, Vector2 screenPos, Vector2 targetAreaSize,
        bool flip) {
        if (!RaptorPortrait.ShouldUseRaptorHead(__instance)) {
            return true;
        }

        //the getter runs CalculateHeadPosition, so the rect already points at the right raptor cell
        Sprite headSprite = __instance.HeadSprite;
        if (headSprite is null) {
            return false;
        }

        float scale = Math.Min(targetAreaSize.X / headSprite.size.X, targetAreaSize.Y / headSprite.size.Y);
        Vector2 origin = headSprite.Origin;
        if (flip) {
            origin.X = headSprite.size.X - origin.X;
        }

        headSprite.Draw(
            spriteBatch,
            screenPos,
            origin: origin,
            scale: scale,
            color: Color.White,
            spriteEffect: flip ? SpriteEffects.FlipHorizontally : SpriteEffects.None);
        return false;
    }

    [HarmonyPatch]
    internal static class CharacterLoadHeadAttachments {
        static MethodBase TargetMethod() => PatchTargets.Character_LoadHeadAttachments;

        static void Postfix(Character __instance) {
            if (!YAMJ.IsPlayerRaptor(__instance)) {
                return;
            }

            Limb? head = __instance.AnimController?.GetLimb(LimbType.Head);
            if (head is null) {
                return;
            }

            head.OtherWearables.ForEach(w => w.Sprite?.Remove());
            head.OtherWearables.Clear();
            head.HairWithHatSprite?.Sprite?.Remove();
            head.HairWithHatSprite = null;
        }
    }
}

[HarmonyPatch] //prevents playerRaptors from having HeadAttachments (beard, hair, etc.)
internal static class CharacterLoadHeadAttachments {
    static MethodBase TargetMethod() => PatchTargets.Character_LoadHeadAttachments;

    static void Postfix(Character __instance) {
        if (!YAMJ.IsPlayerRaptor(__instance)) { return; }

        Limb? head = __instance.AnimController?.GetLimb(LimbType.Head);
        if (head is null) { return; }

        head.OtherWearables.ForEach(w => w.Sprite?.Remove());
        head.OtherWearables.Clear();
        head.HairWithHatSprite?.Sprite?.Remove();
        head.HairWithHatSprite = null;
    }
}

[HarmonyPatch] //draws playerRaptors on the lobby's character appearance customizer
internal static class AppearanceMenuOpenHeadSelection {
    static MethodBase TargetMethod() => PatchTargets.AppearanceCustomizationMenu_OpenHeadSelection;

    static void Postfix(CharacterInfo.AppearanceCustomizationMenu __instance) {
        if (!RaptorPortrait.ShouldUseRaptorHead(__instance.CharacterInfo)) { return; }
        if (__instance.HeadSelectionList is null) { return; }

        //the menu removes everything in this list when the grid is rebuilt or disposed
        List<Sprite>? owned = AccessTools
            .Field(typeof(CharacterInfo.AppearanceCustomizationMenu), "characterSprites")
            .GetValue(__instance) as List<Sprite>;

        foreach (GUIButton button in __instance.HeadSelectionList.Content.GetAllChildren<GUIButton>()) {
            if (button.UserData is not CharacterInfo.HeadPreset preset) { continue; }
            if (button.GetChild<GUIImage>() is not GUIImage image) { continue; }

            Sprite? raptorHead = RaptorPortrait.CreateHeadSprite(preset.SheetIndex);
            if (raptorHead is null) { return; }

            owned?.Add(raptorHead);
            image.Sprite = raptorHead;
        }
    }
}