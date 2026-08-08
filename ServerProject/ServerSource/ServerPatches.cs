using System.Reflection;
using Barotrauma.Items.Components;
using HarmonyLib;

namespace YAMJCS;

internal static class PatchTargets {
    
    public static MethodBase Character_Create_FromPrefab =>
        AccessTools.Method(
            typeof(Character),
            nameof(Character.Create),
            new[] {
                typeof(CharacterPrefab),
                typeof(Vector2),
                typeof(string),
                typeof(CharacterInfo),
                typeof(ushort),
                typeof(bool),
                typeof(bool),
                typeof(bool),
                typeof(RagdollParams),
                typeof(bool)
            }) ?? 
        throw new Exception("Character.Create(CharacterPrefab, ...) not found");

    public static MethodBase Repairable_CheckCharacterSuccess =>
        AccessTools.Method(
            typeof(Repairable),
            nameof(Repairable.CheckCharacterSuccess)) ??
        throw new Exception("Repairable.CheckCharacterSuccess(...) not found");

    public static MethodBase FishAnimController_DragCharacter =>
        AccessTools.Method(
            typeof(FishAnimController),
            nameof(FishAnimController.DragCharacter)) ??
        throw new Exception("FishAnimController.DragCharacter not found");

    // public static MethodBase CustomInterface_ButtonClicked =>
    //     AccessTools.Method(
    //         typeof(CustomInterface),
    //         nameof(CustomInterface.ButtonClicked)) ??
    //     throw new Exception("CustomInterface.ButtonClicked() not found");

    public static MethodBase Character_DoInteractionUpdate =>
        AccessTools.Method(
            typeof(Character),
            nameof(Character.DoInteractionUpdate)) ??
        throw new Exception("Character.DoInteractionUpdate() not found");
}

[HarmonyPatch]
internal static class CharacterCreateFromPrefabPatch {
    static MethodBase TargetMethod() => PatchTargets.Character_Create_FromPrefab;

    static void Prefix(ref CharacterPrefab prefab, CharacterInfo? characterInfo) {
        if (!YAMJ.HasPlayerRaptorJob(characterInfo)) {
            return;
        }

        CharacterPrefab? mudraptorPrefab = YAMJ.FindMudraptorPrefab();
        if (mudraptorPrefab is null) {
            YAMJ.Log("Mudraptor prefab not found in Character.Create(CharacterPrefab, ...) prefix.");
            return;
        }

        prefab = mudraptorPrefab;
        YAMJ.Log("Redirected Character.Create() to playerRaptor spawn");
    }

    static void Postfix(Character? __result, CharacterInfo? characterInfo) {
        if (__result is null) return;
        if (!YAMJ.HasPlayerRaptorJob(characterInfo)) return;
        //GameMain.GameSession?.CrewManager?.AddCharacter(__result); //don't use this, copies to data then spawns from data

        CrewManager crewManager = GameMain.GameSession?.CrewManager;
        if (crewManager is null) return;
        //always says npc playRaptors are New hires but works good enough for now
        if (!GameMain.GameSession.CrewManager.GetCharacterInfos(true).Contains(__result.Info)) {
            GameMain.NetworkMember?.CreateEntityEvent(__result, new Character.AddToCrewEventData(__result.TeamID, __result.Inventory.AllItems));
            YAMJ.Log("Sent AddToCrew event for player raptor " + __result.Name);
        }

        if (YAMJ.IsPlayerRaptor(__result)) {
            Affliction affliction = YAMJ.HungerPrefab.Instantiate(0.01f);
            __result.CharacterHealth.ApplyAffliction(null, affliction);
        }
    }
}

[HarmonyPatch]
internal static class CheckRepairSuccess {
    static MethodBase TargetMethod() => PatchTargets.Repairable_CheckCharacterSuccess;

    static void Postfix(Character character, Item bestRepairItem, ref bool __result) {
        if (!__result) return;
        if (YAMJ.IsPlayerRaptor(character)) {
            if (!YAMJ.HasTalent(character, "YAMJCanUseTools")) {
                __result = false;
            }
        }
    }
}

[HarmonyPatch]
internal static class CorpseEatingPatch {
    static MethodBase TargetMethod() => PatchTargets.FishAnimController_DragCharacter;

    static void Postfix(Character target, float deltaTime, ref FishAnimController __instance) {
        if (YAMJ.IsPlayerRaptor(__instance.Character) && target.IsDead) {
            Affliction reduceHunger = new Affliction(YAMJ.HungerPrefab, YAMJ.EatingHungerReduction * deltaTime * -1f);
            __instance.Character.CharacterHealth.ApplyAffliction(null, reduceHunger);
            Affliction eatingBuff = new Affliction(YAMJ.EatingBuffPrefab, 2f);
            __instance.Character.CharacterHealth.ApplyAffliction(null, eatingBuff, false);
        }
    }
}


[HarmonyPatch(typeof(EnemyAIController), nameof(EnemyAIController.GetTargetingTags))]
internal static class VouchTargetingPatch {
    private static readonly Identifier MudraptorGroup = "mudraptor".ToIdentifier();
    private static readonly Identifier VouchBuff = "YAMJVouchBuff".ToIdentifier();
    
    private static void Postfix(EnemyAIController __instance, AITarget target, IEnumerable<Identifier> __result)
    {
        List<Identifier> targetTags = __result as List<Identifier>;
        if (targetTags == null || targetTags.Count == 0) { return; }

        Character targetCharacter = target.Entity as Character;
        if (targetCharacter == null || targetCharacter.IsDead) { return; }
        
        Character enemyCharacter = __instance.Character;
        
        //is mudraptor
        if (!CharacterParams.CompareGroup(enemyCharacter.Params.Group, MudraptorGroup)) { return; }
            
        //has vouch buff, has not been attacked by
        if (targetCharacter.CharacterHealth.GetAfflictionStrengthByIdentifier(VouchBuff, true) <= 0.0f) { return; }
        if (enemyCharacter.GetDamageDoneByAttacker(targetCharacter) > 0.0f) { return; }

        // no tags and no matching TargetParams = UpdateTargets drops this character
        targetTags.Clear();
    }
}

[HarmonyPatch]
internal static class RaptorBabyPickup {
    static MethodBase TargetMethod() => PatchTargets.Character_DoInteractionUpdate;

    static void Postfix(Character __instance) {
        if (!__instance.IsPlayer || __instance.Removed || __instance.IsDead || !__instance.CanInteract) {
            return;
        }
    }
}