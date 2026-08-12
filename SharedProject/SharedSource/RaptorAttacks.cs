using HarmonyLib;

namespace YAMJCS;

internal static partial class PatchTargets {
    public static MethodBase Attack_DoDamage =>
        AccessTools.Method(
            typeof(Attack),
            nameof(Attack.DoDamage),
            new[] {
                typeof(Character),
                typeof(IDamageable),
                typeof(Vector2),
                typeof(float),
                typeof(bool),
                typeof(PhysicsBody),
                typeof(Limb)
            }) ??
        throw new Exception("Attack.DoDamage(Character, IDamageable, Vector2, float, bool, PhysicsBody, Limb) not found");
}

public class RaptorAttacks {
        
}
    
[HarmonyPatch] // "Can Opener" talent: deal more damage to structures.
internal static class CanOpenerPatch {
    private const float StructureDamageBonus = 3.00f;
    private static readonly Identifier TalentId = "YAMJCanOpener".ToIdentifier();

    static MethodBase TargetMethod() => PatchTargets.Attack_DoDamage;
    
    static void Prefix(Attack __instance, Character attacker, IDamageable target, Limb sourceLimb, out float? __state) {
        __state = null; //using __state again to restore default after modifying

        if (sourceLimb is null) { return; } // creature attacks only
        if (__instance.SourceItem is not null) { return; } // no item attacks (redundant?)

        Structure? structure = target as Structure;
        if (structure is null) { return; } //structures only
        
        if (!YAMJ.IsPlayerRaptor(attacker)) { return; }
        if (!YAMJ.HasTalent(attacker, TalentId)) { return; }
        
        __state = __instance.DamageMultiplier;
        __instance.DamageMultiplier *= 1f + StructureDamageBonus; //other damage mults exist (genes) so we mult the mult instead of add
    }

    static void Postfix(Attack __instance, float? __state) {
        if (__state.HasValue) {
            __instance.DamageMultiplier = __state.Value; //restore default
        }
    }
}