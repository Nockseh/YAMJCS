using HarmonyLib;
using FarseerPhysics;

namespace YAMJCS;

internal static class RaptorGait {
    internal readonly record struct Gait(
        float Center,
        float FrontReach,
        float KneeForward,
        float KneeFold,
        float KneeTorque,
        float FoldTorque);

    private const float IdleCenter = 50f; // ragdoll px, where the feet rest when standing still
    
    internal static readonly Gait Walk = new(
        Center: 25f,
        FrontReach: 1f,
        KneeForward: 60f,
        KneeFold: 120f,
        KneeTorque: 3f,
        FoldTorque: 3f);
    internal static readonly Gait Run = new(
        Center: -25f,
        FrontReach: 0.75f,
        KneeForward: 90f,
        KneeFold: 120f,
        KneeTorque: 10f,
        FoldTorque: 10f);

    internal static bool Applies(FishAnimController anim) =>
        YAMJConfig.AnimOverrideEnabled && YAMJ.IsPlayerRaptor(anim.Character) && !anim.InWater && !anim.IsClimbing && anim.CanWalk;

    internal static Gait Current(FishAnimController anim) {
        float currentSpeed = Math.Abs(anim.movement.X);
        float walkSpeed = Math.Max(anim.WalkParams.MovementSpeed, 0.01f);
        float runSpeed = Math.Max(anim.RunParams.MovementSpeed, walkSpeed + 0.01f);
        float moveT = Math.Clamp(currentSpeed / walkSpeed, 0f, 1f);
        float t = Math.Clamp((currentSpeed - walkSpeed) / (runSpeed - walkSpeed), 0f, 1f);

        float Lerp(float a, float b) => MathHelper.Lerp(a, b, t);
        return new Gait(
            Center: MathHelper.Lerp(IdleCenter, Lerp(Walk.Center, Run.Center), moveT),
            FrontReach: Lerp(Walk.FrontReach, Run.FrontReach),
            KneeForward: Lerp(Walk.KneeForward, Run.KneeForward),
            KneeFold: Lerp(Walk.KneeFold, Run.KneeFold),
            KneeTorque: Lerp(Walk.KneeTorque, Run.KneeTorque),
            FoldTorque: Lerp(Walk.FoldTorque, Run.FoldTorque));
    }
    
    internal static float ExtraStepOffset(FishAnimController anim, bool leftFoot) {
        Gait gait = Current(anim);
        float offset = ConvertUnits.ToSimUnits(gait.Center) * anim.RagdollParams.JointScale;

        // vanilla cycle term (left foot +, right foot -), facing-relative: > 0 while the foot is ahead
        if (Math.Abs(anim.TargetMovement.X) > 0.01f && anim.StepSize is Vector2 stepSize) {
            float side;
            if (leftFoot) {
                side = 1f;
            } else {
                side = -1f;
            }
            float cycle = side * MathF.Cos(anim.WalkPos) * stepSize.X * 3f * anim.Dir;
            if (cycle > 0f) { offset += cycle * (gait.FrontReach - 1f); }
        }
        return offset;
    }
    
    private static float SwingWeight(FishAnimController anim, bool leftFoot) {
        if (Math.Abs(anim.TargetMovement.X) <= 0.01f) { return 0f; }
        float side;
        if (leftFoot) {
            side = 1f;
        } else {
            side = -1f;
        }
        return Math.Max(0f, side * MathF.Sin(anim.WalkPos));
    }

    private static Vector2 AnchorOn(LimbJoint joint, Limb limb) {
        if (joint.LimbA == limb) {
            return joint.WorldAnchorA;
        } else {
            return joint.WorldAnchorB;
        }
    }

    internal static void DriveLeg(FishAnimController anim, in Gait g, LimbType thighType, LimbType calfType, bool leftFoot) {
        float weight = SwingWeight(anim, leftFoot);
        if (weight <= 0f) { return; }
        
        //find limbs
        Limb? thigh = anim.GetLimb(thighType);
        Limb? calf = anim.GetLimb(calfType);
        if (thigh is null || calf is null) { return; }
        
        //find joints
        LimbJoint? hip = null, knee = null, ankle = null;
        foreach (LimbJoint joint in anim.LimbJoints) {
            if (joint is null || joint.IsSevered) { continue; }
            bool hasThigh = joint.LimbA == thigh || joint.LimbB == thigh;
            bool hasCalf = joint.LimbA == calf || joint.LimbB == calf;
            if (hasThigh && hasCalf) { knee = joint; }
            else if (hasThigh) { hip = joint; }
            else if (hasCalf) { ankle = joint; }
        }
        if (hip is null || knee is null || ankle is null) { return; }
        
        //measure
        float dir = anim.Dir;
        Vector2 thighDir = AnchorOn(knee, thigh) - AnchorOn(hip, thigh);
        Vector2 calfDir = AnchorOn(ankle, calf) - AnchorOn(knee, calf);
        if (thighDir.LengthSquared() < 1e-6f || calfDir.LengthSquared() < 1e-6f) { return; }

        float thighAngle = MathF.Atan2(thighDir.X * dir, -thighDir.Y);
        float cross = thighDir.X * calfDir.Y - thighDir.Y * calfDir.X;
        float fold = -MathF.Atan2(cross, Vector2.Dot(thighDir, calfDir)) * dir;

        // move
        float thighDelta = (MathHelper.ToRadians(g.KneeForward) - thighAngle) * dir;
        thigh.body.SmoothRotate(thigh.body.Rotation + thighDelta, g.KneeTorque * weight, wrapAngle: false);

        float calfDelta = -(MathHelper.ToRadians(g.KneeFold) - fold) * dir;
        calf.body.SmoothRotate(calf.body.Rotation + calfDelta, g.FoldTorque * weight, wrapAngle: false);
    }
}

internal static class RaptorClimbPose {
    internal static readonly float HeadTilt = MathHelper.ToRadians(-5);
    internal static readonly float TorsoTilt = MathHelper.ToRadians(30);
    internal static readonly float TailTilt = MathHelper.ToRadians(40);
    internal const float HeadLift = 0.35f; // sim units (~35 px) head height target boost

    // pose only; RaptorLadderCentering stays on regardless, climbing needs it
    internal static bool Applies(AnimController anim) =>
        YAMJConfig.AnimOverrideEnabled && anim.IsClimbing && YAMJ.IsPlayerRaptor(anim.Character);
}

//torso angle edit for climbing anim
[HarmonyPatch(typeof(AnimController), nameof(AnimController.TorsoAngle), MethodType.Getter)]
internal static class RaptorClimbPose_Torso {
    private static void Postfix(AnimController __instance, ref float? __result) {
        if (__result.HasValue && RaptorClimbPose.Applies(__instance)) { __result += RaptorClimbPose.TorsoTilt; }
    }
}

//head angle edit for climbing anim
[HarmonyPatch(typeof(AnimController), nameof(AnimController.HeadAngle), MethodType.Getter)]
internal static class RaptorClimbPose_Head {
    private static void Postfix(AnimController __instance, ref float? __result) {
        if (__result.HasValue && RaptorClimbPose.Applies(__instance)) { __result += RaptorClimbPose.HeadTilt; }
    }
}

//tail angle edit for climbing anim
[HarmonyPatch(typeof(FishAnimController), nameof(FishAnimController.TailAngle), MethodType.Getter)]
internal static class RaptorClimbPose_Tail {
    private static void Postfix(FishAnimController __instance, ref float? __result) {
        if (__result.HasValue && RaptorClimbPose.Applies(__instance)) { __result += RaptorClimbPose.TailTilt; }
    }
}

//head height edit for climbing anim
[HarmonyPatch(typeof(AnimController), nameof(AnimController.HeadPosition), MethodType.Getter)]
internal static class RaptorClimbPose_HeadHeight {
    private static void Postfix(AnimController __instance, ref float? __result) {
        if (__result.HasValue && RaptorClimbPose.Applies(__instance)) { __result += RaptorClimbPose.HeadLift; }
    }
}

//assists raptors in climbing ladders by centering them properly when climbing
[HarmonyPatch(typeof(FishAnimController), nameof(FishAnimController.UpdateAnim))]
internal static class RaptorLadderCentering {
    private const float CenterGain = 12f; // how hard it snaps onto the ladder line (1/s)
    private const float MaxCorrection = 3f; // cap on sideways correction speed (sim units/s)
    private const float Offset = 0.10f; //20px body offset from ladder

    private static void Postfix(FishAnimController __instance) {
        Character character = __instance.Character;
        if (!__instance.IsClimbing || !YAMJ.IsPlayerRaptor(character)) { return; }
        if (character.SelectedSecondaryItem?.GetComponent<Barotrauma.Items.Components.Ladder>() is not { } ladder) { return; }

        PhysicsBody collider = __instance.Collider;

        float targetX = ladder.Item.SimPosition.X - __instance.Dir * Offset;
        float distX = targetX - collider.SimPosition.X;
        Vector2 vel = collider.LinearVelocity;
        vel.X = Math.Clamp(distX * CenterGain, -MaxCorrection, MaxCorrection);
        collider.LinearVelocity = vel;

        collider.FarseerBody.FixedRotation = false;
        collider.AngularVelocity = MathUtils.GetShortestAngle(collider.Rotation, 0f) * 60f;
    }
}

//changes how the feet targets are positioned during walk anim
[HarmonyPatch(typeof(Limb), nameof(Limb.StepOffset), MethodType.Getter)]
internal static class RaptorGait_StepOffset {
    private static void Postfix(Limb __instance, ref Vector2 __result) {
        if (__instance.type is not (LimbType.LeftFoot or LimbType.RightFoot)) { return; }
        if (__instance.ragdoll is not FishAnimController anim || !RaptorGait.Applies(anim)) { return; }
        __result.X += RaptorGait.ExtraStepOffset(anim, __instance.type == LimbType.LeftFoot);
    }
}

//apply extra force/torque to legs during walk anim
[HarmonyPatch(typeof(FishAnimController), nameof(FishAnimController.UpdateWalkAnim))]
internal static class RaptorGait_WalkAnim {
    private static void Postfix(FishAnimController __instance) {
        if (!RaptorGait.Applies(__instance)) { return; }
        if (__instance.SimplePhysicsEnabled) { return; }
        RaptorGait.Gait gait = RaptorGait.Current(__instance);
        RaptorGait.DriveLeg(__instance, gait, LimbType.LeftThigh, LimbType.LeftLeg, leftFoot: true);
        RaptorGait.DriveLeg(__instance, gait, LimbType.RightThigh, LimbType.RightLeg, leftFoot: false);
    }
}