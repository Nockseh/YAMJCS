using Barotrauma.Items.Components;
using HarmonyLib;
using Microsoft.Xna.Framework.Graphics;

namespace YAMJCS;

internal static partial class PatchTargets {
    internal static MethodBase Sonar_DrawSonar =>
        AccessTools.DeclaredMethod(
            typeof(Sonar),
            nameof(Sonar.DrawSonar),
            new[] { typeof(SpriteBatch), typeof(Rectangle) }) ??
        throw new Exception("Sonar.DrawSonar(SpriteBatch, Rectangle) not found");
}

internal static class RaptorLocator {
    internal const double RAPTOR_SCAN_INTERVAL = 10.0;
    private const float RAPTOR_HIDE_DIST = 500f; //todo move to config?

    private static readonly Color SubArrowColor = new Color(126, 211, 224, 100); //todo config?
    private static readonly Color RaptorArrowColor = new Color(50, 200, 50, 100); //todo config?

    // list is cached on a timer, positions are read every frame
    internal static readonly List<Character> BondedRaptors = new();

    internal static void Draw(SpriteBatch spriteBatch, Character character, Camera camera) {
        if (GUI.DisableHUD) { return; }
        if (character != Character.Controlled) { return; }
        if (character.Removed || character.IsDead) { return; }

        if (YAMJ.IsPlayerRaptor(character) && character.HasTalent(BondCaptain.TalentId)) {
            DrawSubmarineArrow(spriteBatch, camera);
        }

        if (BondCaptain.IsCaptain(character)) {
            DrawRaptorArrows(spriteBatch, character, camera);
        }
    }

    private static void DrawSubmarineArrow(SpriteBatch spriteBatch, Camera camera) {
        Submarine? sub = Submarine.MainSub;
        if (sub is null) { return; }
        if (Level.Loaded is not null && sub.WorldPosition.Y < Level.MaxEntityDepth) { return; }

        Sprite? icon = GUIStyle.SubLocationIcon.Value?.Sprite;
        if (icon is null) { return; }

        //same threshold vanilla uses for the spectator mode sub arrow
        float hideDist = Math.Max(sub.Borders.Width, sub.Borders.Height);
        GUI.DrawIndicator(spriteBatch, sub.WorldPosition, camera, hideDist, icon, SubArrowColor);
    }

    private static void DrawRaptorArrows(SpriteBatch spriteBatch, Character captain, Camera camera) {
        foreach (Character raptor in BondedRaptors) {
            // the cache can go stale between scans
            if (raptor.Removed || raptor.IsDead || !raptor.Enabled) { continue; }

            Sprite? icon = raptor.Info?.Job?.Prefab?.Icon;
            if (icon is null) { continue; }

            GUI.DrawIndicator(spriteBatch, raptor.WorldPosition, camera, new Range<float>(RAPTOR_HIDE_DIST, float.PositiveInfinity), icon,
                RaptorArrowColor, label: raptor.Name);
        }
    }

    internal static void RefreshRaptorList() {
        BondedRaptors.Clear();
        foreach (Character character in Character.CharacterList) {
            if (character.Removed || character.IsDead) { continue; }
            if (!YAMJ.IsPlayerRaptor(character)) { continue; }
            if (!character.HasTalent(BondCaptain.TalentId)) { continue; }
            BondedRaptors.Add(character);
        }
    }
}

[HarmonyPatch] //sub arrow for bonded raptors, raptor arrows for captains
internal static class CharHudDrawArrowsPatch {
    private static MethodBase TargetMethod() => PatchTargets.CharacterHUD_Draw;

    private static void Postfix(SpriteBatch spriteBatch, Character character, Camera cam) {
        RaptorLocator.Draw(spriteBatch, character, cam);
    }
}
[HarmonyPatch] //captainBond raptors always show as sonar markers
internal static class SonarRaptorMarkerPatch {
    private static MethodBase TargetMethod() => PatchTargets.Sonar_DrawSonar;

    private static void Postfix(Sonar __instance, SpriteBatch spriteBatch) {
        // vanilla returns out of its own marker pass when signalStrength <= 0.5 and flips
        // this on as it goes, so reading it stands in for the local we can't see
        if (__instance.signalWarningText != null && __instance.signalWarningText.Visible) { return; }

        // DrawSonar already set center / DisplayRadius / DisplayScale from rect, and either
        // added DisplayOffset to its local or zeroed the field, so this reproduces it
        Vector2 transducerCenter = __instance.GetTransducerPos() + __instance.DisplayOffset;

        foreach (Character raptor in RaptorLocator.BondedRaptors) {
            if (raptor.Removed || raptor.IsDead || !raptor.Enabled) { continue; }

            __instance.DrawMarker(
                     spriteBatch: spriteBatch,
                           label: raptor.Name,
                  iconIdentifier: Identifier.Empty,
                targetIdentifier: raptor,
                   worldPosition: raptor.WorldPosition,
              transducerPosition: transducerCenter,
                           scale: __instance.DisplayScale,
                          center: __instance.center,
                          radius: __instance.DisplayRadius * 0.95f, 
         onlyShowTextOnMouseOver: true);
        }
    }
}