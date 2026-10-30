using HarmonyLib;

namespace YAMJCS;

internal static class BondDoctor {
    internal static readonly Identifier TalentId = "yamjBondDoctor".ToIdentifier();
    internal static readonly Identifier InfectionId = "yamjDoctorInfection".ToIdentifier();
    internal static readonly Identifier InfectionCooldownId = "yamjDoctorInfectionCooldown".ToIdentifier();

    private static readonly Identifier DamageType = "damage".ToIdentifier();
    private static readonly Identifier BurnType = "burn".ToIdentifier();
    private static readonly Identifier BleedingType = "bleeding".ToIdentifier();

    private static AfflictionPrefab? _infectionPrefab;
    internal static AfflictionPrefab? InfectionPrefab => _infectionPrefab ??= YAMJ.FindAfflictionPrefab(InfectionId);

    private static AfflictionPrefab? _infectionCooldownPrefab;
    internal static AfflictionPrefab? InfectionCooldownPrefab => _infectionCooldownPrefab ??= YAMJ.FindAfflictionPrefab(InfectionCooldownId);

    internal static bool CanInfect(Character raptor, Character target) {
        if (target == raptor || target.Removed || target.IsDead) { return false; }
        
        if (!raptor.HasTalent(TalentId)) { return false; }
        
        if (!target.IsOnFriendlyTeam(raptor)) { return false; }
        
        if (!target.IsHuman && !YAMJ.IsPlayerRaptor(target)) { return false; }
        
        if (!target.IsUnconscious) { return false; }

        CharacterHealth health = target.CharacterHealth;
        if (health.GetAfflictionStrengthByIdentifier(InfectionId, false) > 0f) { return false; }
        if (health.GetAfflictionStrengthByIdentifier(InfectionCooldownId, false) > 0f) { return false; }

        return true;
    }

    internal static void Infect(Character target) {
        CharacterHealth health = target.CharacterHealth;
        
        float fraction = YAMJConfig.DoctorBondInfectionHealFraction;
        HealFraction(health, DamageType, fraction);
        HealFraction(health, BurnType, fraction);
        HealFraction(health, BleedingType, fraction);
        
        YAMJ.ApplyAtMaxStrength(target, InfectionPrefab);
        YAMJ.ApplyAtMaxStrength(target, InfectionCooldownPrefab);
    }

    private static void HealFraction(CharacterHealth health, Identifier afflictionType, float fraction) {
        if (fraction <= 0f) { return; }

        float total = health.GetAfflictionStrengthByType(afflictionType);
        if (total <= 0f) { return; }

        health.ReduceAfflictionOnAllLimbs(afflictionType, total * fraction);
    }
}

[HarmonyPatch] //grabbing a downed crewmate infects them (Bond with Medic)
internal static class InfectOnGrab {
    private static MethodBase TargetMethod() => PatchTargets.Character_DoInteractionUpdate;

    private static void Postfix(Character __instance) {
        if (!YAMJ.IsPlayerRaptor(__instance)) { return; }
        if (GameMain.NetworkMember is { IsClient: true }) { return; } //server authority

        Character? grabbed = __instance.SelectedCharacter;
        if (grabbed is null) { return; }
        if (!BondDoctor.CanInfect(__instance, grabbed)) { return; }

        BondDoctor.Infect(grabbed);
    }
}