using Barotrauma.Items.Components;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using HarmonyLib;

namespace YAMJCS;

internal class RaptorVision {
    private static readonly Identifier HeatSignatureTalent = "yamjHeatSignature".ToIdentifier();
    private static readonly Identifier OreVisionTalent = "yamjBondMechanic".ToIdentifier();
    private const double ORE_SCAN_INTERVAL = 2.0;

    private static List<LevelResource> _oreCache = new();
    private static List<Item> _oresInRange = new();
    private static double _lastScanTime;
    private static bool _raptorVisionEnabled;

    private static bool IsEligible(Character? character) {
        return
            character is not null &&
            YAMJ.IsPlayerRaptor(character) &&
            !character.Removed &&
            !character.IsDead &&
            (character.HasTalent(HeatSignatureTalent) || character.HasTalent(OreVisionTalent));
    }

    internal static void Update() {
        Character? controlled = Character.Controlled;

        if (!IsEligible(controlled)) {
            _raptorVisionEnabled = false;
            return;
        }

        if (YAMJConfig.RaptorVisionKey.IsHit()) {
            _raptorVisionEnabled = !_raptorVisionEnabled;
        }
    }

    internal static void Draw(SpriteBatch spriteBatch, Character character, Camera camera) {
        if (!_raptorVisionEnabled || GUI.DisableHUD || character != Character.Controlled || !IsEligible(character)) { return; }

        float effectState = (float)(Timing.TotalTimeUnpaused % 10000.0);

        if (character.HasTalent(HeatSignatureTalent)) {
            StatusHUD.DrawThermalOverlay(spriteBatch, refEntity: character, user: character, YAMJConfig.RaptorVisionColor, YAMJConfig.RaptorVisionRange, effectState, YAMJConfig.RaptorVisionShowDead);
        }

        if (character.HasTalent(OreVisionTalent)) {
            DrawOreVision(spriteBatch, character, camera, effectState);
        }
    }

    private static void DrawOreVision(SpriteBatch spriteBatch, Character character, Camera camera, float effectState) {
        Color oreVisionColor = YAMJConfig.OreVisionColor;
        if (Timing.TotalTimeUnpaused - _lastScanTime >= ORE_SCAN_INTERVAL) {
            _lastScanTime = Timing.TotalTimeUnpaused;
            ScanForOres();
        }

        _oresInRange.Clear();
        foreach (LevelResource ore in _oreCache) {
            if (ore.Item.Removed || !ore.Attached) { continue; }

            if ((ore.Item.WorldPosition - character.WorldPosition).Length() > YAMJConfig.OreVisionRange) { continue; }

            _oresInRange.Add(ore.Item);
        }

        if (_oresInRange.Count == 0) { return; }
        
        spriteBatch.End();
        spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.Additive, transformMatrix: camera.Transform);
        float pulse = (MathF.Sin(effectState) + 1f) / 2f; //[0,1]
        foreach (Item ore in _oresInRange) {
            ore.Draw(spriteBatch, editing: false, back: true, overrideColor: oreVisionColor * pulse);
        }
        spriteBatch.End();
        spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.NonPremultiplied); // revert changes 

        // off-screen markers 
        foreach (Item ore in _oresInRange) {
            Rectangle screenRect = new Rectangle(0, 0, GameMain.GraphicsWidth, GameMain.GraphicsHeight);
            if (screenRect.Contains(camera.WorldToScreen(ore.WorldPosition))) { continue; }

            GUI.DrawIndicator(spriteBatch, ore.WorldPosition, camera, 0f, ore.Prefab.Sprite, oreVisionColor, scaleMultiplier: 0.5f);
        }
    }
    
    private static void ScanForOres() {
        _oreCache.Clear();
        foreach (Item item in Item.ItemList) {
            if (item.Submarine is not null) { continue; }

            LevelResource? resource = item.GetComponent<LevelResource>();
            if (resource is null || !resource.Attached) { continue; }

            if (!resource.RequiredItems.Any()) { continue; } // ores only, which require tools 

            _oreCache.Add(resource);
        }
    }
}

[HarmonyPatch] //draws raptor thermal + ore vision 
internal static class CharHudDrawVisionOverlayPatch {
    private static MethodBase TargetMethod() => PatchTargets.CharacterHUD_Draw;

    static void Prefix(SpriteBatch spriteBatch, Character character, Camera cam) {
        RaptorVision.Draw(spriteBatch, character, cam);
    }
}