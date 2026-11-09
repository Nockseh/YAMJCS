using Barotrauma.Networking;
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

internal static class RaptorSitPose {
    internal static readonly float TorsoTilt = MathHelper.ToRadians(30); // pos = up
    internal static readonly float HeadTilt = MathHelper.ToRadians(-45); // pos = up
    internal const float TorsoDrop = 0.50f;  // sim units (~100 px ea.) torso height target drop
    internal const float BackShift = 0.50f;  // sim units (~100 px ea.) torso pushed back
    internal const float HeadLift = 0.00f;  // sim units (~100 px ea.) head height target boost

    //strength while sitting, starting from the Walk animation's values
    internal const float TorsoTorque = 5f;     // rotating the torso to its angle
    internal const float HeadTorque = 6f;      // rotating the head to its angle
    internal const float TorsoMoveForce = 5f;   // extra push of the torso towards its target
    internal const float HeadMoveForce = 2f;    // extra push of the head towards its target
    internal const float TorsoPullForce = 20f; // torso pull joint cap per kg; this is what mostly holds it in place
    internal const float HeadPullForce = 5f;  // head pull joint cap per kg
    private const float DefaultPullForce = 1000f; // vanilla cap per kg, restored when the raptor stops sitting
    
    //controllers whose pull joint caps were changed and still need restoring
    private static readonly ConditionalWeakTable<FishAnimController, object> changedPull = new();

    //called every walk update
    internal static void UpdatePullForces(FishAnimController anim, bool sitting) {
        Limb? torso = anim.GetLimb(LimbType.Torso);
        Limb? head = anim.GetLimb(LimbType.Head);
        if (sitting) {
            if (torso is not null) { torso.PullJointMaxForce = TorsoPullForce * torso.Mass; }
            if (head is not null) { head.PullJointMaxForce = HeadPullForce * head.Mass; }
            changedPull.AddOrUpdate(anim, anim);
        } else if (changedPull.TryGetValue(anim, out _)) {
            if (torso is not null) { torso.PullJointMaxForce = DefaultPullForce * torso.Mass; }
            if (head is not null) { head.PullJointMaxForce = DefaultPullForce * head.Mass; }
            changedPull.Remove(anim);
        }
    }

    internal static bool IsCrouchHeld(Character character) {
        #if CLIENT
            // other players' raptors in multiplayer: use what the server relayed
            if (GameMain.Client != null && character != Character.Controlled) {
                return remoteCrouching.Contains(character.ID);
            }
        #endif
        return character.IsKeyDown(InputType.Crouch);
    }   

    internal static bool Applies(AnimController? anim) {
        if (anim is not FishAnimController fish || !RaptorGait.Applies(fish)) { return false; }
        if (!fish.OnGround || Math.Abs(fish.TargetMovement.X) > 0.01f) { return false; }
        if (fish.Character.IsIncapacitated) { return false; }
        return IsCrouchHeld(fish.Character);
    }
    
    //-- Networking --//
    private const string NetId = "YAMJCS.RaptorCrouch";
    private const double ResendInterval = 2.0; //seconds
    private static INetworkingService? _networkingService;
    
    internal static void Initialize(INetworkingService? netService) {
        _networkingService = netService;
        if (_networkingService is null) {
            YAMJ.Log("No networking service; other players won't see raptors sit.");
            return;
        }
        #if CLIENT
            remoteCrouching.Clear();
            _networkingService.Receive(NetId, ReceiveCrouch);
        #elif SERVER
            lastSent.Clear();
        #endif
    }
    
    #if CLIENT
        private static readonly HashSet<ushort> remoteCrouching = new();

        private static void ReceiveCrouch(IReadMessage msg) {
            ushort characterId = msg.ReadUInt16();
            bool held = msg.ReadBoolean();
            if (held) {
                remoteCrouching.Add(characterId);
            } else {
                remoteCrouching.Remove(characterId);
            }
        }
    #endif
    
    #if SERVER
        private readonly record struct SentState(bool Held, double Time);
        private static readonly Dictionary<ushort, SentState> lastSent = new();
    
        //called every animation update for every creature
        internal static void ServerUpdate(Character character) {
            if (_networkingService is null || GameMain.Server is null) { return; }
            if (!YAMJ.IsPlayerRaptor(character)) { return; }

            bool held = YAMJConfig.AnimOverrideEnabled && !character.IsIncapacitated && character.IsKeyDown(InputType.Crouch);
            double currentTime = Timing.TotalTime;
            if (lastSent.TryGetValue(character.ID, out SentState sent)) {
                bool changed = sent.Held != held;
                bool shouldResend = held && currentTime - sent.Time >= ResendInterval;
                if (!changed && !shouldResend) { return; }
            } else if (!held) {
                return; // never sent & not held
            }

            lastSent[character.ID] = new SentState(held, currentTime);
            foreach (Client client in GameMain.Server.ConnectedClients) {
                IWriteMessage msg = _networkingService.Start(NetId);
                msg.WriteUInt16(character.ID);
                msg.WriteBoolean(held);
                _networkingService.SendToClient(msg, client.Connection, DeliveryMethod.Reliable);
            }
        }
    #endif
}

//makes the legs go limp while sitting
[HarmonyPatch(typeof(Limb), nameof(Limb.MoveToPos))]
internal static class RaptorPose_Legs {
    private static bool Prefix(Limb __instance) {
        //vanilla controller only moves the feet
        if (__instance.type is not (LimbType.LeftFoot or LimbType.RightFoot)) { return true; }
        return !RaptorSitPose.Applies(__instance.ragdoll as AnimController);
    }
}

//torque and move force overrides for sitting pose
[HarmonyPatch]
internal static class RaptorPose_SitForces {
    private static IEnumerable<MethodBase> TargetMethods() {
        yield return AccessTools.PropertyGetter(typeof(FishAnimController), nameof(FishAnimController.TorsoTorque));
        yield return AccessTools.PropertyGetter(typeof(FishAnimController), nameof(FishAnimController.HeadTorque));
        yield return AccessTools.PropertyGetter(typeof(FishAnimController), nameof(FishAnimController.TorsoMoveForce));
        yield return AccessTools.PropertyGetter(typeof(FishAnimController), nameof(FishAnimController.HeadMoveForce));
    }

    private static void Postfix(FishAnimController __instance, MethodBase __originalMethod, ref float __result) {
        if (!RaptorSitPose.Applies(__instance)) { return; }
        switch (__originalMethod.Name) {
            case "get_" + nameof(FishAnimController.TorsoTorque): __result = RaptorSitPose.TorsoTorque; break;
            case "get_" + nameof(FishAnimController.HeadTorque): __result = RaptorSitPose.HeadTorque; break;
            case "get_" + nameof(FishAnimController.TorsoMoveForce): __result = RaptorSitPose.TorsoMoveForce; break;
            case "get_" + nameof(FishAnimController.HeadMoveForce): __result = RaptorSitPose.HeadMoveForce; break;
        }
    }
}

//torso angle edit for climbing/sitting poses
[HarmonyPatch(typeof(AnimController), nameof(AnimController.TorsoAngle), MethodType.Getter)]
internal static class RaptorPose_Torso {
    private static void Postfix(AnimController __instance, ref float? __result) {
        if (!__result.HasValue) { return; }
        if (RaptorClimbPose.Applies(__instance)) {
            __result += RaptorClimbPose.TorsoTilt;
        } else if (RaptorSitPose.Applies(__instance)) {
            __result += RaptorSitPose.TorsoTilt;
        }
    }
}

//torso height edit for sitting pose
[HarmonyPatch(typeof(AnimController), nameof(AnimController.TorsoPosition), MethodType.Getter)]
internal static class RaptorPose_TorsoPos {
    private static void Postfix(AnimController __instance, ref float? __result) {
        if (__result.HasValue && RaptorSitPose.Applies(__instance)) { __result -= RaptorSitPose.TorsoDrop; }
    }
}

//head angle edit for climbing/sitting pose
[HarmonyPatch(typeof(AnimController), nameof(AnimController.HeadAngle), MethodType.Getter)]
internal static class RaptorPose_HeadAngle {
    private static void Postfix(AnimController __instance, ref float? __result) {
        if (!__result.HasValue) { return; }
        if (RaptorClimbPose.Applies(__instance)) {
            __result += RaptorClimbPose.HeadTilt;
        } else if (RaptorSitPose.Applies(__instance)) {
            __result += RaptorSitPose.HeadTilt;
        }
    }
}

//head height edit for climbing/sitting pose
[HarmonyPatch(typeof(AnimController), nameof(AnimController.HeadPosition), MethodType.Getter)]
internal static class RaptorPose_HeadPos {
    private static void Postfix(AnimController __instance, ref float? __result) {
        if (!__result.HasValue) { return; }
        if (RaptorClimbPose.Applies(__instance)) {
            __result += RaptorClimbPose.HeadLift;
        } else if (RaptorSitPose.Applies(__instance)) {
            __result += RaptorSitPose.HeadLift;
        }
    }
}

//tail angle edit for climbing pose
[HarmonyPatch(typeof(FishAnimController), nameof(FishAnimController.TailAngle), MethodType.Getter)]
internal static class RaptorPose_TailAngle {
    private static void Postfix(FishAnimController __instance, ref float? __result) {
        if (__result.HasValue && RaptorClimbPose.Applies(__instance)) { __result += RaptorClimbPose.TailTilt; }
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
internal static class LimbStepOffsetGet {
    private static void Postfix(Limb __instance, ref Vector2 __result) {
        if (__instance.type is not (LimbType.LeftFoot or LimbType.RightFoot)) { return; }
        if (__instance.ragdoll is not FishAnimController anim || !RaptorGait.Applies(anim)) { return; }
        __result.X += RaptorGait.ExtraStepOffset(anim, __instance.type == LimbType.LeftFoot);
    }
}

//apply extra force/torque to legs during walk anim; pushes torso back for sit pose
[HarmonyPatch(typeof(FishAnimController), nameof(FishAnimController.UpdateWalkAnim))]
internal static class FishControllerUpdateWalkAnim {
    private static void Postfix(FishAnimController __instance) {
        if (!RaptorGait.Applies(__instance)) { return; }
        if (__instance.SimplePhysicsEnabled) { return; }
        bool sitting = RaptorSitPose.Applies(__instance);
        RaptorSitPose.UpdatePullForces(__instance, sitting);
        if (sitting) {
            if (__instance.GetLimb(LimbType.Torso) is { } torso) {
                torso.PullJointWorldAnchorB -= new Vector2(RaptorSitPose.BackShift * __instance.Dir, 0f);
            }
            return;
        }
        RaptorGait.Gait gait = RaptorGait.Current(__instance);
        RaptorGait.DriveLeg(__instance, gait, LimbType.LeftThigh, LimbType.LeftLeg, leftFoot: true);
        RaptorGait.DriveLeg(__instance, gait, LimbType.RightThigh, LimbType.RightLeg, leftFoot: false);
    }
}

#if SERVER
//send pRaptors crouch key status to clients, runs every update
[HarmonyPatch(typeof(FishAnimController), nameof(FishAnimController.UpdateAnim))]
internal static class RaptorSitNet_ServerUpdate {
    private static void Postfix(FishAnimController __instance) {
        RaptorSitPose.ServerUpdate(__instance.Character);
    }
}
#endif