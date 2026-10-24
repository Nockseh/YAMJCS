using Barotrauma.Items.Components;
using FarseerPhysics;
using HarmonyLib;
using Microsoft.Xna.Framework.Graphics;

namespace YAMJCS;

internal static partial class PatchTargets {
    internal static MethodBase Character_AddAttacker =>
        AccessTools.Method(
            typeof(Character),
            nameof(Character.AddAttacker),
            new[] {
                typeof(Character), //character
                typeof(float) //damage
            }) ??
        throw new Exception("Character.AddAttacker(Character, float) not found");
    
    internal static MethodBase GameSession_StartRound =>
        AccessTools.Method(
            typeof(GameSession),
            nameof(GameSession.StartRound),
            new[] {
                typeof(LevelData),
                typeof(bool),
                typeof(SubmarineInfo),
                typeof(SubmarineInfo)
            }) ??
        throw new Exception("GameSession.StartRound(LevelData, bool, SubmarineInfo, SubmarineInfo) not found");
}

//also see: Client/RaptorLocator.cs
internal static class BondSecurity {
    internal static readonly Identifier TalentId = "yamjBondSecurity".ToIdentifier();
    internal static readonly Identifier MarkAfflictionId = "yamjMarked".ToIdentifier();
    private static AfflictionPrefab? _markAfflictionPrefab;
    internal static  AfflictionPrefab? MarkAfflictionPrefab => _markAfflictionPrefab ??= YAMJ.FindAfflictionPrefab(MarkAfflictionId);
    
    internal static Dictionary<Character, Character> HandlerBonds =  new();
}

#if CLIENT
[HarmonyPatch] //shows marked targets to all pRaptors with talent 
internal static class MarkVision {
    private static MethodBase TargetMethod() => PatchTargets.CharacterHUD_Draw;
    
    private static void Postfix(SpriteBatch spriteBatch, Character character, Camera cam) {
        if (character != Character.Controlled || !YAMJ.IsPlayerRaptor(character)) { return; }
        if (!character.HasTalent(BondSecurity.TalentId)) { return; }
        
        // arguments for ripped function below 
        Entity refEntity = character;
        Character user = character;
        Color overlayColor = new Color(255, 90, 200, 255); //todo config
        float range = YAMJConfig.RaptorVisionRange; //todo config???
        float effectState = (float)(Timing.TotalTimeUnpaused % 10000.0);
        
        //this section copy-pasted from vanilla StatusHUD.DrawThermalOverlay(), altered to include a filter 
        spriteBatch.End();
        float colorIntensityBase = 0.5f; //Multiplies the overlay color by this amount, the higher the value, the more bright/vibrant the color.
        float colorIntensityVariance = 0.05f; //The variance of the pulse effect affecting the color's brightness/vibrance 
        GameMain.LightManager.SolidColorEffect.Parameters["color"].SetValue(overlayColor.ToVector4() * (colorIntensityBase + MathF.Sin(effectState) * colorIntensityVariance));
        GameMain.LightManager.SolidColorEffect.CurrentTechnique = GameMain.LightManager.SolidColorEffect.Techniques["SolidColorBlur"];
        GameMain.LightManager.SolidColorEffect.Parameters["blurDistance"].SetValue(0.01f + MathF.Sin(effectState) * 0.005f);
        GameMain.LightManager.SolidColorEffect.CurrentTechnique.Passes[0].Apply();
        spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.Additive, transformMatrix: Screen.Selected.Cam.Transform, effect: GameMain.LightManager.SolidColorEffect);
        
        Character? handler = null;
        BondSecurity.HandlerBonds.TryGetValue(character, out handler);
        
        foreach (Character c in Character.CharacterList) {
            if (c == user || !c.Enabled || c.Removed) { continue; }

            float markStrength =  c.CharacterHealth.GetAfflictionStrengthByIdentifier(BondSecurity.MarkAfflictionId);
            if ( !(markStrength > 0f) && c != handler) { continue; }
            if (c.IsDead) { continue; }

            float dist = Vector2.DistanceSquared(refEntity.WorldPosition, c.WorldPosition);
            if (dist > range * range) { continue; }

            Sprite pingCircle = GUIStyle.UIThermalGlow.Value.Sprite;
            foreach (Limb limb in c.AnimController.Limbs) {
                if (limb.Mass < 0.5f && limb != c.AnimController.MainLimb) { continue; }
                float noise1 = PerlinNoise.GetPerlin((effectState + limb.Params.ID + c.ID) * 0.01f, (effectState + limb.Params.ID + c.ID) * 0.02f);
                float noise2 = PerlinNoise.GetPerlin((effectState + limb.Params.ID + c.ID) * 0.01f, (effectState + limb.Params.ID + c.ID) * 0.008f);
                Vector2 spriteScale = ConvertUnits.ToDisplayUnits(limb.body.GetSize()) / pingCircle.size * (noise1 * 0.5f + 2f);
                Vector2 drawPos = new Vector2(limb.body.DrawPosition.X + (noise1 - 0.5f) * 100, -limb.body.DrawPosition.Y + (noise2 - 0.5f) * 100);
                pingCircle.Draw(spriteBatch, drawPos, 0.0f, scale: Math.Max(spriteScale.X, spriteScale.Y));
            }
        }

        spriteBatch.End();
        spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.NonPremultiplied);
    }
}
#endif

[HarmonyPatch] //allows pRaptors with talent to designate a friendly human as a handler 
internal static class DesignateHandler {
    private static MethodBase TargetMethod() => PatchTargets.Character_DoInteractionUpdate;

    private static void Postfix(Character __instance) {
        if (!YAMJ.IsPlayerRaptor(__instance) || !__instance.HasTalent(BondSecurity.TalentId)) { return; }
        if ( !__instance.CanInteract || __instance.IsIncapacitated) { return; }
        
        Character? focusedCharacter = __instance.FocusedCharacter;
        if (focusedCharacter is null || !focusedCharacter.IsHuman || focusedCharacter.IsDead || focusedCharacter.Removed) { return; }
        if (!HumanAIController.IsFriendly(__instance, focusedCharacter)) { return; }
        
#if CLIENT
        focusedCharacter.SetCustomInteract(null, CharacterHUD.GetCachedHudText("yamjHint.designateHandler", InputType.Select));
#endif
        
        if (!__instance.IsKeyHit(InputType.Select)) { return; }

        BondSecurity.HandlerBonds[__instance] = focusedCharacter;
    }
}

[HarmonyPatch] //characters designated as handlers will apply mark to targets 
internal static class MarkTarget {
    private static MethodBase TargetMethod() => PatchTargets.Character_AddAttacker;

    private static void Postfix(Character __instance, Character character, float damage) {
        Character victim = __instance;
        Character attacker = character;
        if (attacker.Removed || victim.Removed || victim.IsDead) { return; }
        if (!BondSecurity.HandlerBonds.ContainsValue(attacker)) { return; }
        
        //handlers cant mark their own raptors
        Character? victimsHandler = null;
        BondSecurity.HandlerBonds.TryGetValue(victim, out victimsHandler);
        if (victimsHandler == attacker) { return; }
        
        Affliction? mark = victim.CharacterHealth.GetAffliction(BondSecurity.MarkAfflictionId, false);

        if (BondSecurity.MarkAfflictionPrefab is null) {
            YAMJ.Log("MarkAfflictionPrefab is null, aborting");
            return;
        }
        
        if (mark is not null) {
            mark.Strength = BondSecurity.MarkAfflictionPrefab.MaxStrength;
            victim.HealthUpdateInterval = 0f; //trigger sync manually 
        } else {
            mark = BondSecurity.MarkAfflictionPrefab.Instantiate(BondSecurity.MarkAfflictionPrefab.MaxStrength * (victim.MaxVitality /100f), attacker);
            victim.CharacterHealth.AddAffliction(mark, false);
        }
    }
}

[HarmonyPatch] //refresh HandlerBonds since game makes new Character objects every round 
internal static class ClearHandlerBonds {
    private static MethodBase TargetMethod() => PatchTargets.GameSession_StartRound;

    private static void Postfix() {
        BondSecurity.HandlerBonds.Clear();
    }
}