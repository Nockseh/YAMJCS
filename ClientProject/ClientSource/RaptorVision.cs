using Barotrauma.Items.Components;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;

namespace YAMJCS;

public class RaptorVision {
    //TODO: make configurable
    private static readonly Color ThermalColor = new Color(255, 127, 0, 255); //default color
    private static readonly float ThermalRange = 1500.0f;
    private static readonly bool ShowDeadCharacters = false;
    private static readonly string RequiredTalent = "YAMJHeatSignature";
    private static readonly Keys VisionKey = Keys.V;

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
        
        //TODO: make this configurable
        if (PlayerInput.KeyHit(VisionKey)) {
            thermalVisionEnabled = !thermalVisionEnabled;
            YAMJ.Log($"Thermal vision {(thermalVisionEnabled ? "enabled" : "disabled")}.");
        }
    }

    public static void Draw(SpriteBatch spriteBatch, Character character) {
        if (!thermalVisionEnabled || GUI.DisableHUD || character != Character.Controlled || !IsEligible(character)) {
            return;
        }
        
        float effectState = (float)(Timing.TotalTimeUnpaused % 10000.0);
        StatusHUD.DrawThermalOverlay(spriteBatch, refEntity: character, user: character, ThermalColor, ThermalRange, effectState, ShowDeadCharacters);
    }
}