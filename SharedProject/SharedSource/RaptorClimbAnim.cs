using HarmonyLib;

namespace YAMJCS;

internal static class RaptorClimbPose {
    internal static readonly float HeadTilt = MathHelper.ToRadians(-5);
    internal static readonly float TorsoTilt = MathHelper.ToRadians(30);
    internal static readonly float TailTilt = MathHelper.ToRadians(40);
    internal const float HeadLift = 0.35f; // sim units (~35 px) head height target boost

    internal static bool Applies(AnimController anim) =>
        anim.IsClimbing && YAMJ.IsPlayerRaptor(anim.Character);
}

[HarmonyPatch(typeof(AnimController), nameof(AnimController.TorsoAngle), MethodType.Getter)]
internal static class RaptorClimbPose_Torso {
    private static void Postfix(AnimController __instance, ref float? __result) {
        if (__result.HasValue && RaptorClimbPose.Applies(__instance)) { __result += RaptorClimbPose.TorsoTilt; }
    }
}

[HarmonyPatch(typeof(AnimController), nameof(AnimController.HeadAngle), MethodType.Getter)]
internal static class RaptorClimbPose_Head {
    private static void Postfix(AnimController __instance, ref float? __result) {
        if (__result.HasValue && RaptorClimbPose.Applies(__instance)) { __result += RaptorClimbPose.HeadTilt; }
    }
}

[HarmonyPatch(typeof(FishAnimController), nameof(FishAnimController.TailAngle), MethodType.Getter)]
internal static class RaptorClimbPose_Tail {
    private static void Postfix(FishAnimController __instance, ref float? __result) {
        if (__result.HasValue && RaptorClimbPose.Applies(__instance)) { __result += RaptorClimbPose.TailTilt; }
    }
}

[HarmonyPatch(typeof(AnimController), nameof(AnimController.HeadPosition), MethodType.Getter)]
internal static class RaptorClimbPose_HeadHeight {
    private static void Postfix(AnimController __instance, ref float? __result) {
        if (__result.HasValue && RaptorClimbPose.Applies(__instance)) { __result += RaptorClimbPose.HeadLift; }
    }
}

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