using Barotrauma.Lights;
using HarmonyLib;

namespace YAMJCS;

internal static class ReactiveGlow {
    private static float REACTIVE_GLOW_RANGE = 400f;
    private static Color REACTIVE_GLOW_COLOR = new Color(50, 255, 50);
    //todo move to shared config
    
    internal static void Update() {
        foreach (Character character in Character.CharacterList) {
            if (character.Removed || !YAMJ.IsPlayerRaptor(character)) { continue; }
            // vanilla fades limb lights out as a fish dies (FishAnimController.UpdateDying), let it 
            if (character.IsDead) { continue; }

            Limb? torso = character.AnimController?.GetLimb(LimbType.Torso);
            LightSource? light = torso?.LightSource;
            if (light is null) { continue; } //no light on a dedicated server, and none before the ragdoll loads
            
            if (AfflictionPrefabs.Reactive is null) {
                YAMJ.Log("ReactiveGlow: Reactive prefab is null");
                return;
            }

            float maxStrength = AfflictionPrefabs.Reactive.MaxStrength > 0f
                ? AfflictionPrefabs.Reactive.MaxStrength
                : 99f;
            float strength = character.CharacterHealth.GetAfflictionStrengthByIdentifier(AfflictionIds.Reactive, false);
            float intensity = Math.Clamp(strength / maxStrength, 0f, 1f);

            light.Range = REACTIVE_GLOW_RANGE * intensity;
            light.Color = REACTIVE_GLOW_COLOR * intensity;
        }
    }
}

[HarmonyPatch] //Reactive affliction causes glow effect
internal static class ReactiveGlowUpdate {
    private static MethodBase TargetMethod() => PatchTargets.Character_UpdateAll;

    private static void Postfix() {
        ReactiveGlow.Update();
    }
}