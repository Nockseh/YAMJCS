using Barotrauma.Items.Components;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;

namespace YAMJCS;

public class RaptorVision {
    private static readonly string RequiredTalent = "YAMJHeatSignature";
    
    private static bool thermalVisionEnabled;

    public static bool IsEligible(Character? character) {
        return character is not null && YAMJ.IsPlayerRaptor(character) && !character.Removed && !character.IsDead && YAMJ.HasTalent(character, RequiredTalent);
    }

    public static void Update() {
        Character? controlled = Character.Controlled;

        if (!IsEligible(controlled)) {
            thermalVisionEnabled = false;
            return;
        }
        
        if (YAMJ.RaptorVisionKey.IsHit()) {
            thermalVisionEnabled = !thermalVisionEnabled;
        }
    }

    public static void Draw(SpriteBatch spriteBatch, Character character) {
        if (!thermalVisionEnabled || GUI.DisableHUD || character != Character.Controlled || !IsEligible(character)) {
            return;
        }
        
        float effectState = (float)(Timing.TotalTimeUnpaused % 10000.0);
        StatusHUD.DrawThermalOverlay(spriteBatch, refEntity: character, user: character, YAMJ.RaptorVisionColor, YAMJ.RaptorVisionRange, effectState, YAMJ.RaptorVisionShowDead);
    }
}