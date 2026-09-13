using Barotrauma.Items.Components;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;

namespace YAMJCS;

public class RaptorVision {
    private static readonly Identifier HeatSignatureTalent = "YAMJHeatSignature".ToIdentifier();
    private static readonly Identifier OreVisionTalent = "YAMJBondMechanic".ToIdentifier();
    private const double ORE_SCAN_INTERVAL = 2.0;

    private static readonly List<LevelResource> oreCache = new();
    private static readonly List<Item> oresInRange = new();
    private static double lastScanTime;

    private static bool _raptorVisionEnabled;

    private static bool IsEligible(Character? character) {
        return
            character is not null &&
            YAMJ.IsPlayerRaptor(character) &&
            !character.Removed &&
            !character.IsDead &&
            (YAMJ.HasTalent(character, HeatSignatureTalent) || YAMJ.HasTalent(character, OreVisionTalent));
    }

    public static void Update() {
        Character? controlled = Character.Controlled;

        if (!IsEligible(controlled)) {
            _raptorVisionEnabled = false;
            return;
        }

        if (YAMJConfig.RaptorVisionKey.IsHit()) {
            _raptorVisionEnabled = !_raptorVisionEnabled;
        }
    }

    public static void Draw(SpriteBatch spriteBatch, Character character, Camera camera) {
        if (!_raptorVisionEnabled || GUI.DisableHUD || character != Character.Controlled || !IsEligible(character)) { return; }

        float effectState = (float)(Timing.TotalTimeUnpaused % 10000.0);

        if (YAMJ.HasTalent(character, HeatSignatureTalent)) {
            StatusHUD.DrawThermalOverlay(spriteBatch, refEntity: character, user: character, YAMJConfig.RaptorVisionColor, YAMJConfig.RaptorVisionRange, effectState, YAMJConfig.RaptorVisionShowDead);
        }

        if (YAMJ.HasTalent(character, OreVisionTalent)) {
            DrawOreVision(spriteBatch, character, camera, effectState);
        }
    }

    private static void DrawOreVision(SpriteBatch spriteBatch, Character character, Camera camera, float effectState) {
        if (Timing.TotalTimeUnpaused - lastScanTime >= ORE_SCAN_INTERVAL) {
            lastScanTime = Timing.TotalTimeUnpaused;
            ScanForOres();
        }

        oresInRange.Clear();
        foreach (LevelResource ore in oreCache) {
            if (ore.Item.Removed || !ore.Attached) { continue; }

            if ((ore.Item.WorldPosition - character.WorldPosition).Length() > YAMJConfig.OreVisionRange) { continue; }

            oresInRange.Add(ore.Item);
        }

        if (oresInRange.Count == 0) { return; }
        
        spriteBatch.End();
        spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.Additive, transformMatrix: camera.Transform);
        float pulse = (MathF.Sin(effectState) + 1f) / 2f; //[0,1]
        foreach (Item ore in oresInRange) {
            ore.Draw(spriteBatch, editing: false, back: true, overrideColor: YAMJConfig.OreVisionColor * pulse);
        }
        spriteBatch.End();
        spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.NonPremultiplied); // revert changes 

        // off-screen markers 
        foreach (Item ore in oresInRange) {
            Rectangle screenRect = new Rectangle(0, 0, GameMain.GraphicsWidth, GameMain.GraphicsHeight);
            if (screenRect.Contains(camera.WorldToScreen(ore.WorldPosition))) { continue; }

            GUI.DrawIndicator(spriteBatch, ore.WorldPosition, camera, 0f, ore.Prefab.InventoryIcon, YAMJConfig.OreVisionColor, scaleMultiplier: 0.5f);
        }
    }
    
    private static void ScanForOres() {
        oreCache.Clear();
        foreach (Item item in Item.ItemList) {
            if (item.Submarine is not null) { continue; }

            LevelResource? resource = item.GetComponent<LevelResource>();
            if (resource is null || !resource.Attached) { continue; }

            if (!resource.RequiredItems.Any()) { continue; } // ores only, which require tools 

            oreCache.Add(resource);
        }
    }
}