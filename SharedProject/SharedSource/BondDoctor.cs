using HarmonyLib;

namespace YAMJCS;

internal static class BondDoctor {
    internal static readonly Identifier TalentId = "yamjBondDoctor".ToIdentifier();

    private static readonly Identifier DamageType = "damage".ToIdentifier();
    private static readonly Identifier BurnType = "burn".ToIdentifier();
    private static readonly Identifier BleedingType = "bleeding".ToIdentifier();
    
    internal static bool CanInfect(Character raptor, Character target) {
        if (target == raptor || target.Removed || target.IsDead) { return false; }
        
        if (!raptor.HasTalent(TalentId)) { return false; }
        
        if (!target.IsOnFriendlyTeam(raptor)) { return false; }
        
        if (!target.IsHuman && !YAMJ.IsPlayerRaptor(target)) { return false; }
        
        if (!target.IsUnconscious) { return false; }

        CharacterHealth health = target.CharacterHealth;
        if (health.GetAfflictionStrengthByIdentifier(AfflictionIds.Infection, false) > 0f) { return false; }
        if (health.GetAfflictionStrengthByIdentifier(AfflictionIds.InfectionCooldown, false) > 0f) { return false; }

        return true;
    }

    internal static void Infect(Character target) {
        CharacterHealth health = target.CharacterHealth;
        
        float fraction = YAMJConfig.DoctorBondInfectionHealFraction;
        HealFraction(health, DamageType, fraction);
        HealFraction(health, BurnType, fraction);
        HealFraction(health, BleedingType, fraction);
        
        YAMJ.ApplyAtMaxStrength(target, AfflictionPrefabs.Infection);
        YAMJ.ApplyAtMaxStrength(target, AfflictionPrefabs.InfectionCooldown);
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