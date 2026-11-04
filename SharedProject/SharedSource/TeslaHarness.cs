using Barotrauma.Abilities;
using HarmonyLib;

namespace YAMJCS;

internal static partial class PatchTargets {
    internal static ConstructorInfo AbilityAttackData_Ctor =>
        AccessTools.Constructor(
            typeof(AbilityAttackData),
            new[] {
                typeof(Attack), //sourceAttack
                typeof(Character), //target
                typeof(Character) //attacker
            }) ??
        throw new Exception("new AbilityAttackData(Attack, Character, Character) constructor not found");
        
    internal static MethodBase CharacterHealth_AddLimbAffliction =>
        AccessTools.DeclaredMethod(
            typeof(CharacterHealth),
            nameof(CharacterHealth.AddLimbAffliction),
            new [] {
                typeof(CharacterHealth.LimbHealth), //limbHealth
                typeof(Limb), //limb
                typeof(Affliction), //newAffliction
                typeof(bool), //allowStacking = true
                typeof(bool) //recalculateVitality = true
            }) ??
        throw new Exception("CharacterHealth.AddLimbAffliction(LimbHealth, Limb, Affliction, bool, bool) not found");
}

internal static class TeslaHarness {
    internal const float RETALIATE_RANGE = 500f;
    internal const float REDIRECT_RANGE = 900f;
    internal static readonly Identifier TeslaHarnessItemId = "yamjTeslaHarness".ToIdentifier();
    internal static readonly Identifier ElectricShockId = "electricshock".ToIdentifier();
    
    internal static Item? FindWornHarness(Character? character) {
        if (character is null || character.Removed || character.Inventory is null) { return null; }
        return character.GetEquippedItem(TeslaHarnessItemId, InvSlotType.OuterClothes);
    }

    internal static bool IsShock(Affliction? affliction) {
        if (affliction is null || affliction.Strength <= 0f) { return false; }
        return affliction.Identifier == ElectricShockId;
    }

    internal static void ApplyAfflictionUnscaled(Character target, AfflictionPrefab? prefab, float strength, Character? source) {
        if (prefab is null || strength <= 0f) { return; }

        CharacterHealth? health = target.CharacterHealth;
        if (health is null) { return; }

        float vitalityScale = health.MaxVitality / 100f;
        if (vitalityScale <= 0f) { vitalityScale = 1f; }

        health.AddAffliction(prefab.Instantiate(strength * vitalityScale, source));
    }

    // same as AbilityConditionAttackData ignoreNonHarmfulAttacks
    internal static bool IsHarmful(Attack? attack) {
        if (attack is null) { return false; }
        if (attack.Stun > 0f) { return true; }
        if (attack.Afflictions is null) { return false; }
        foreach (Affliction affliction in attack.Afflictions.Keys) {
            if (!affliction.Prefab.IsBuff) { return true; }
        }
        return false;
    }
    
    internal static Character? FindNearestWearer(Character victim) {
        Character? nearest = null;
        float nearestDistance = REDIRECT_RANGE * REDIRECT_RANGE;

        foreach (Character character in Character.CharacterList) {
            if (character == victim || character.Removed || character.IsDead) { continue; }
            if (!YAMJ.IsPlayerRaptor(character)) { continue; }
            if (!character.IsOnFriendlyTeam(victim)) { continue; }
            if (FindWornHarness(character) is null) { continue; }

            float distance = Vector2.DistanceSquared(victim.WorldPosition, character.WorldPosition);
            if (distance > nearestDistance) { continue; }

            nearest = character;
            nearestDistance = distance;
        }
        return nearest;
    }
}

[HarmonyPatch] //tesla harness retaliation on hit
internal static class TeslaHarnessDischarge {
    private static MethodBase TargetMethod() => PatchTargets.AbilityAttackData_Ctor;

    private static void Postfix(AbilityAttackData __instance) {
        if (GameMain.NetworkMember is { IsClient: true }) { return; } //todo see if client can predict
        AbilityAttackData attackData = __instance;
        Character? victim = attackData.Character;
        if (!YAMJ.IsPlayerRaptor(victim)) { return; }

        Item? harness = TeslaHarness.FindWornHarness(victim);
        if (harness is null || harness.Removed) { return; }
        
        Character attacker = attackData.Attacker;
        if (attacker is null || attacker.Removed || attacker == victim) { return; }
        
        if (!TeslaHarness.IsHarmful(attackData.SourceAttack)) { return; }

        if (harness.Condition < YAMJConfig.TeslaHarnessRetaliateCost) { return; }
        
        //absorb
        attackData.DamageMultiplier *= 1f - YAMJConfig.TeslaHarnessRetaliateAbsorb;

        harness.Condition -= YAMJConfig.TeslaHarnessRetaliateCost;
        
        //retaliate
        float range = TeslaHarness.RETALIATE_RANGE;
        if (Vector2.DistanceSquared(victim.WorldPosition, attacker.WorldPosition) > range * range) { return; }
        
        TeslaHarness.ApplyAfflictionUnscaled(attacker, AfflictionPrefab.Stun, YAMJConfig.TeslaHarnessRetaliateStun, victim);
        TeslaHarness.ApplyAfflictionUnscaled(attacker, AfflictionPrefab.Burn, YAMJConfig.TeslaHarnessRetaliateBurn, victim);
        //vfx affliction
        TeslaHarness.ApplyAfflictionUnscaled(attacker, AfflictionPrefabs.HarnessDischarge, 1f, victim);
    }
}

[HarmonyPatch] //electrical damage charges the tesla harness
internal static class TeslaHarnessShockCharge {
    private static MethodBase TargetMethod() => PatchTargets.CharacterHealth_AddLimbAffliction;

    // prefix to avoid affliction resistances
    private static void Prefix(CharacterHealth __instance, Affliction newAffliction) {
        if (GameMain.NetworkMember is { IsClient: true }) { return; }

        Character? wearer = __instance.Character;
        if (!TeslaHarness.IsShock(newAffliction)) { return; }
        if (!YAMJ.IsPlayerRaptor(wearer)) { return; }

        Item? harness = TeslaHarness.FindWornHarness(wearer);
        if (harness is null || harness.Removed) { return; }
        
        //setter should handle clamping
        harness.Condition += newAffliction.Strength * 0.5f;
    }
}

[HarmonyPatch] //harness wearers redirect nearby electric damage to selves
internal static class TeslaHarnessShockRedirect {
    private static MethodBase TargetMethod() => PatchTargets.CharacterHealth_AddLimbAffliction;
    private static bool Prefix(CharacterHealth __instance, Affliction newAffliction) {
        if (GameMain.NetworkMember is { IsClient: true }) { return true; } //server only for now todo check

        Character victim = __instance.Character;
        if (victim is null || victim.Removed || victim.IsDead) { return true; }
        if (!TeslaHarness.IsShock(newAffliction)) { return true; }

        if (TeslaHarness.FindWornHarness(victim) is not null) { return true; }

        Character? newVictim = TeslaHarness.FindNearestWearer(victim);
        if (newVictim is null) { return true; }

        TeslaHarness.ApplyAfflictionUnscaled(newVictim, newAffliction.Prefab, newAffliction.Strength, newAffliction.Source);
        TeslaHarness.ApplyAfflictionUnscaled(newVictim, AfflictionPrefabs.HarnessDischarge, 1f, victim); //vfx

        return false;
    }
}