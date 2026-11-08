using Barotrauma.Items.Components;
using FarseerPhysics;
using FarseerPhysics.Collision;
using FarseerPhysics.Dynamics;
using HarmonyLib;

namespace YAMJCS;

internal static partial class PatchTargets {
    internal static MethodBase Limb_ExecuteAttack =>
        AccessTools.Method(
            typeof(Limb),
            nameof(Limb.ExecuteAttack),
            new[] {
                typeof(IDamageable),
                typeof(Limb),
                typeof(AttackResult).MakeByRefType() //for out vars 
            }) ??
        throw new Exception("Limb.ExecuteAttack(IDamageable, Limb, out AttackResult) not found");

    internal static MethodBase Limb_UpdateAttack =>
        AccessTools.Method(
            typeof(Limb),
            nameof(Limb.UpdateAttack),
            new[] {
                typeof(float),
                typeof(Vector2),
                typeof(IDamageable),
                typeof(AttackResult).MakeByRefType(),
                typeof(float),
                typeof(Limb)
            }) ??
        throw new Exception("Limb.UpdateAttack(float, Vector2, IDamageable, AttackResult, float, Limb) not found");
        
    internal static MethodBase Character_Control =>
        AccessTools.DeclaredMethod(typeof(Character), nameof(Character.Control)) ??
        throw new Exception("Character.Control(float, Camera) not found");

}

internal static class CreatureAttack {
    private const float MINING_AMOUNT = 5f;
    internal static readonly Identifier MechanicBondId = "yamjBondMechanic".ToIdentifier();
    internal const string SpitLimbName = "SpitAttack";
    internal const string ShrapnelLimbName = "ShrapnelAttack";
    internal static readonly Identifier DoctorBondTalentId = "yamjBondDoctor".ToIdentifier();
    
    private static Limb? FindLimb(Character character, string limbName) {
        foreach (Limb limb in character.AnimController.Limbs) {
            if (limb.Name == limbName) { return limb; }
        }
        return null;
    }
    
    private static bool LimbIsUsable(Limb? limb, Character character) {
        if (limb is null) { return false; }
        if (limb.IsSevered || limb.IsStuck) { return false; }

        Attack? attack = limb.attack;
        if (attack is null) { return false; }
        if (attack.CoolDownTimer > 0f) { return false; }

        foreach (PropertyConditional conditional in attack.Conditionals) {
            if (!conditional.TargetSelf) { continue; }
            if (!conditional.Matches(character)) { return false; }
        }
        return true;
    }
    
    internal static void UpdateSelection(Character character) {
        Limb[] limbs = character.AnimController.Limbs;
        
        foreach (Limb limb in limbs) {
            Attack? attack = limb.attack;
            if (attack is not null && attack.IsRunning) { return; }
        }

        Limb? chosenSpit = null;
        if (character.IsKeyDown(InputType.Aim)) {
            Limb? shrapnelLimb = FindLimb(character, ShrapnelLimbName);
            Limb? spitLimb = FindLimb(character, SpitLimbName);
            
            if (LimbIsUsable(shrapnelLimb, character) && RaptorGut.FindShrapnelAmmo(character.Inventory) is not null) {
                chosenSpit = shrapnelLimb;
            } else if (LimbIsUsable(spitLimb, character)) {
                chosenSpit = spitLimb;
            }
        }

        foreach (Limb limb in limbs) {
            if (limb.attack is null) { continue; }
            if (chosenSpit is null) {
                //disable ranged attacks
                limb.Disabled = limb.Name == SpitLimbName || limb.Name == ShrapnelLimbName;
            } else {
                //disable all except chosen
                limb.Disabled = limb != chosenSpit;
            }
        }
    }
    
    internal static void TryMineOre(Limb limb) {
        if (limb.character.Submarine is not null) { return; } // submarines have their own sim coords, not usable 

        Vector2 limbPos = limb.SimPosition;
        float reach = ConvertUnits.ToSimUnits(limb.attack.DamageRange);
        AABB hitbox = new AABB(limbPos - new Vector2(reach), limbPos + new Vector2(reach));

        LevelResource? target = null;
        GameMain.World.QueryAABB(fixture => {
            if (fixture.Body?.UserData is not Item ore || ore.Removed) { return true; } // ore hitboxes are actually sensor triggers with the item as UserData
            LevelResource? resource = ore.GetComponent<LevelResource>();
            if (resource is null || !resource.Attached) { return true; }
            if (!resource.RequiredItems.Any()) { return true; }
            if (!limb.attack.IsValidTarget(ore)) { return true; }

            target = resource;
            return false; // returning false stops the search 
        }, ref hitbox);

        if (target is null) { return; }
        target.DeattachTimer += MINING_AMOUNT;
    }
}

[HarmonyPatch] // decides which creature attack will be used
internal static class AttackSelectionPatch {
    static MethodBase TargetMethod() => PatchTargets.Character_Control;

    static void Prefix(Character __instance) {
        if (__instance.Removed || __instance.IsDead) { return; }
        if (!__instance.IsPlayer) { return; }
        if (!YAMJ.IsPlayerRaptor(__instance)) { return; }
        if (__instance.AnimController is null) { return; }
        
        //dont try to assert other players attacks
        if (GameMain.NetworkMember is { IsClient: true } && __instance != Character.Controlled) { return; }

        CreatureAttack.UpdateSelection(__instance);
    }
}

[HarmonyPatch] // bond w/ doctor: any bite has chance to inject neurotoxin
internal static class PoisonBitePatch {
    static MethodBase TargetMethod() => PatchTargets.Limb_ExecuteAttack;

    static void Postfix(Limb __instance, IDamageable damageTarget, ref AttackResult attackResult) {
        if (GameMain.NetworkMember is { IsClient: true }) { return; }
        if (attackResult.Damage <= 0f) { return; }

        Character? attacker = __instance.character;
        if (!YAMJ.IsPlayerRaptor(attacker)) { return; }

        Character? victim = damageTarget as Character;
        if (victim is null || victim.Removed || victim.IsDead) { return; }

        if (__instance.Name != "Jaw" && __instance.Name != "WaterAttack") { return; }
        if (!attacker.HasTalent(CreatureAttack.DoctorBondTalentId)) { return; }
        if (Rand.Value() < YAMJConfig.PoisonBiteChance) { return; }

        victim.CharacterHealth.ApplyAffliction(null, AfflictionPrefabs.Neurotoxin.Instantiate(YAMJConfig.PoisonBiteNeuro, attacker));
        victim.HealthUpdateInterval = 0f; //forces health sync
    }
}

[HarmonyPatch] // bond with mechanic: creature attacks mine ores 
internal static class RaptorBeakMiningPatch {
    private static MethodBase TargetMethod() => PatchTargets.Limb_UpdateAttack;

    static void Prefix(Limb __instance) {
        Attack? attack = __instance.attack;
        if (attack is null || attack.IsRunning) { return; } // fires on the FIRST frame of the swing ONLY 
        if (attack.Ranged || attack.HitDetectionType == HitDetection.None) { return; } // melee only 

        Character? character = __instance.character;
        if (character is null || character.Removed || !character.IsPlayer) { return; }
        if (!YAMJ.IsPlayerRaptor(character)) { return; }
        if (!character.HasTalent(CreatureAttack.MechanicBondId)) { return; }

        CreatureAttack.TryMineOre(__instance);
    }
}

[HarmonyPatch] // removes condition from shrapnel ammo on spit
internal static class RaptorShrapnelSpit {
    private static MethodBase TargetMethod() => PatchTargets.Limb_ExecuteAttack;

    static bool Prefix(Limb __instance) {
        Character character = __instance.character;
        if (character is null || character.Removed) { return true; }
        if (!YAMJ.IsPlayerRaptor(character) || __instance.Name != "ShrapnelAttack") { return true; }
        
        Item? ammo = RaptorGut.FindShrapnelAmmo(character.Inventory);
        if (ammo is null) { return false; }
        ammo.Condition -= YAMJConfig.ShrapnelSpitCost;
        if (ammo.Condition <= 0f) { Entity.Spawner?.AddItemToRemoveQueue(ammo); }
        return true;
    }
}

[HarmonyPatch] // target-specific creature attack damage multipliers (Can Opener talent) 
internal static class CanOpenerAttackPatch {
    static MethodBase TargetMethod() => PatchTargets.Attack_DoDamage;
    
    static void Prefix(Attack __instance, Character attacker, IDamageable target, Limb sourceLimb, out float? __state) {
        __state = null; //using __state again to restore default after modifying

        if (sourceLimb is null) { return; } // creature attacks only
        if (__instance.SourceItem is not null) { return; } // no item attacks (redundant?)

        Structure? structure = target as Structure;
        if (structure is null) { return; } //structures only
        
        if (!YAMJ.IsPlayerRaptor(attacker)) { return; }
        if (!attacker.HasTalent(CanOpener.TalentId)) { return; }
        
        __state = __instance.DamageMultiplier;
        __instance.DamageMultiplier *= 1f + YAMJConfig.CanOpenerDamageBonus; //other damage mults exist (genes) so we mult the mult instead of add
    }

    static void Postfix(Attack __instance, float? __state) {
        if (__state.HasValue) {
            __instance.DamageMultiplier = __state.Value; //restore default
        }
    }
}