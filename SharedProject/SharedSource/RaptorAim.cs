using FarseerPhysics;
using HarmonyLib;

namespace YAMJCS;

internal static partial class PatchTargets {
    internal static MethodBase FishAnimController_UpdateAnim =>
        AccessTools.DeclaredMethod(
            typeof(FishAnimController),
            nameof(FishAnimController.UpdateAnim),
            new[] {
                typeof(float)
            }) ??
        throw new Exception("FishAnimController.UpdateAnim(float) not found");
}

internal static class RaptorAim {
    // Configuration //
    internal const float HeadAimTorque = 100f;
    internal const float FlipDeadzone = 100f;
    
    private static LimbJoint? FindNeckJoint(Ragdoll ragdoll) {
        foreach (LimbJoint joint in ragdoll.LimbJoints) {
            if (joint.Params.Limb1 == 0 && joint.Params.Limb2 == 2) {
                return joint;
            }
        }
        return null;
    }
    
    private static void TryFlip(FishAnimController animController, Character character) {
        if (animController.TargetDir == Direction.None || animController.TargetDir == animController.dir) { return; }
        IFishAnimation fishAnim = animController.CurrentFishAnimation;
        if (animController.IsStuck || !fishAnim.Flip) { return; }
        if (character.AIController != null && !character.AIController.CanFlip) { return; }
        if (animController.flipTimer <= fishAnim.FlipDelay || animController.flipCooldown > 0f) { return; }

        animController.Flip();
        FishSwimParams? swim = animController.CurrentSwimParams;
        if (!animController.inWater || (swim != null && swim.Mirror)) {
            animController.Mirror(swim == null || swim.MirrorLerp);
        }
        animController.flipTimer = 0f;
        animController.flipCooldown = fishAnim.FlipCooldown;
    }
    
    internal static void Aim(FishAnimController animController) {
        Character character = animController.Character;
        if (character.Removed || character.IsIncapacitated) { return; }
        if (!YAMJ.IsPlayerRaptor(character)) { return; }
        if (!character.IsKeyDown(InputType.Aim)) { return; }

        Limb? head = animController.GetLimb(LimbType.Head);

        Vector2 diff = ConvertUnits.ToSimUnits(character.CursorPosition) - head.SimPosition;
        if (diff.LengthSquared() < 0.0001f) { return; }

        animController.aiming = true;
        
        /* Old Directional Flip
        if (diff.X < 0f) {
            anim.TargetDir = Direction.Left;
        } else {
            anim.TargetDir = Direction.Right;
        }
        */
        
        //new directional flip with deadzone and measuring from CM 
        if (!character.IsRemotelyControlled) {
            float centerX = ConvertUnits.ToDisplayUnits(animController.GetCenterOfMass().X);
            float offsetX = character.CursorPosition.X - centerX;
            if (offsetX < -FlipDeadzone) {
                animController.TargetDir = Direction.Left;
            } else if (offsetX > FlipDeadzone) {
                animController.TargetDir = Direction.Right;
            }
            TryFlip(animController, character); //this must occur before head rotation 
        }
        
        float aimAngle = MathUtils.VectorToAngle(diff) + head.Params.GetSpriteOrientation() - (MathF.PI / 2f);
        if (head.body.Dir < 0f) { aimAngle += MathF.PI; }

        LimbJoint? neck = FindNeckJoint(animController);
        if (neck?.revoluteJoint != null) {
            float chestRotation = neck.LimbB.body.Rotation;
            float refAngle = neck.revoluteJoint.ReferenceAngle;
            float jointAngle = MathUtils.WrapAnglePi(chestRotation - refAngle - aimAngle);
            jointAngle = Math.Clamp(jointAngle, Math.Min(neck.LowerLimit, neck.UpperLimit), Math.Max(neck.LowerLimit, neck.UpperLimit) );
            float target = chestRotation - refAngle - jointAngle;
            //apply rotation 
            head.body.SuppressSmoothRotationCalls = false;
            head.body.SmoothRotate(target, HeadAimTorque);
            head.body.SuppressSmoothRotationCalls = true;
        }
        
    }
}

[HarmonyPatch] //allows raptors to aim their head at the cursor with the aim keybind 
    internal static class FishAnimUpdateAnim {
    static MethodBase TargetMethod() => PatchTargets.FishAnimController_UpdateAnim;

        static void Prefix(FishAnimController __instance) {
            RaptorAim.Aim(__instance);
        }
}