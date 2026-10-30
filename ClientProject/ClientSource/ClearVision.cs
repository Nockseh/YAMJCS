using HarmonyLib;
using Barotrauma.Lights;
using Microsoft.Xna.Framework.Graphics;

namespace YAMJCS;

internal static partial class PatchTargets {
    internal static MethodBase LightManager_UpdateObstructVision =>
        AccessTools.Method(
            typeof(LightManager),
            nameof(LightManager.UpdateObstructVision),
            new[] {
                typeof(GraphicsDevice),
                typeof(SpriteBatch),
                typeof(Camera),
                typeof(Vector2)
            }) ??
        throw new Exception("LightManager.UpdateObstructVision(GraphicsDevice, SpriteBatch, Camera, Vector2) not found");
}

[HarmonyPatch] //bond with security: crewmates near the raptor don't get suit vision cones 
internal static class ClearVisionPatch {
    static MethodBase TargetMethod() => PatchTargets.LightManager_UpdateObstructVision;

    static void Prefix(LightManager __instance) {
        Character? controlled = Character.Controlled;
        if (controlled is null) { return; }
        if (controlled.CharacterHealth.GetAfflictionStrengthByIdentifier(AfflictionIds.ClearVision, false) <= 0f) { return; }
        __instance.ObstructVisionAmount = 0f;
    }
}