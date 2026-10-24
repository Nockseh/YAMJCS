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

}

internal static class CreatureAttack {
    private const float MINING_AMOUNT = 5f;
    internal static readonly Identifier MechanicBondId = "yamjBondMechanic".ToIdentifier();
    
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

[HarmonyPatch] // cancels the shrapnel attack if the gut doesn't contain valid ammo 
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