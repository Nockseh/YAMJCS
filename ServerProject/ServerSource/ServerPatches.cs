using System.Reflection;
using Barotrauma.Items.Components;
using HarmonyLib;

namespace YAMJCS;

internal static partial class PatchTargets {
    
    internal static MethodBase Character_Create_FromPrefab =>
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

    internal static MethodBase Repairable_CheckCharacterSuccess =>
        AccessTools.Method(
            typeof(Repairable),
            nameof(Repairable.CheckCharacterSuccess)) ??
        throw new Exception("Repairable.CheckCharacterSuccess(...) not found");

    internal static MethodBase FishAnimController_DragCharacter =>
        AccessTools.Method(
            typeof(FishAnimController),
            nameof(FishAnimController.DragCharacter)) ??
        throw new Exception("FishAnimController.DragCharacter not found");
}

[HarmonyPatch]
internal static class CharacterCreateFromPrefabPatch {
    static MethodBase TargetMethod() => PatchTargets.Character_Create_FromPrefab;

    static void Prefix(ref CharacterPrefab prefab, CharacterInfo? characterInfo) {
        if (!YAMJ.HasPlayerRaptorJob(characterInfo)) {
            return;
        }

        CharacterPrefab? mudraptorPrefab = YAMJ.FindPlayerRaptorPrefab();
        if (mudraptorPrefab is null) {
            return;
        }

        prefab = mudraptorPrefab;
        YAMJ.Log("Redirected Character.Create() to playerRaptor spawn");
    }

    static void Postfix(Character? __result, CharacterInfo? characterInfo) {
        if (__result is null) return;
        if (!YAMJ.HasPlayerRaptorJob(characterInfo)) return;
        //GameMain.GameSession?.CrewManager?.AddCharacter(__result); //don't use this, copies to data then spawns from data

        if (GameMain.GameSession is null) return;
        CrewManager? crewManager = GameMain.GameSession.CrewManager;
        if (crewManager is null) return;
        //always says npc playRaptors are New hires but works good enough for now todo fix
        if (!crewManager.GetCharacterInfos(true).Contains(__result.Info)) {
            GameMain.NetworkMember?.CreateEntityEvent(__result, new Character.AddToCrewEventData(__result.TeamID, __result.Inventory.AllItems));
            YAMJ.Log("Sent AddToCrew event for player raptor " + __result.Name);
        }
    }
}

[HarmonyPatch]
internal static class CheckRepairSuccess {
    static MethodBase TargetMethod() => PatchTargets.Repairable_CheckCharacterSuccess;

    static void Postfix(Character character, Item bestRepairItem, ref bool __result) {
        if (!__result) return;
        if (YAMJ.IsPlayerRaptor(character)) {
            if (!character.HasTalent("yamjCanUseTools".ToIdentifier())) {
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
            RaptorGut.ModifyHunger(__instance.Character.CharacterHealth, YAMJConfig.EatingHungerReduction * deltaTime * -1f);
            Affliction eatingBuff = new Affliction(YAMJ.EatingBuffPrefab, 2f);
            __instance.Character.CharacterHealth.ApplyAffliction(null, eatingBuff, false);
        }
    }
}


[HarmonyPatch(typeof(EnemyAIController), nameof(EnemyAIController.GetTargetingTags))]
internal static class VouchTargetingPatch {
    private static readonly Identifier MudraptorGroup = "mudraptor".ToIdentifier();
    private static readonly Identifier VouchBuff = "yamjVouchBuff".ToIdentifier();
    
    private static void Postfix(EnemyAIController __instance, AITarget aiTarget, IEnumerable<Identifier> __result)
    {
        List<Identifier>? targetTags = __result as List<Identifier>;
        if (targetTags is null || targetTags.Count == 0) { return; }

        Character? targetCharacter = aiTarget.Entity as Character;
        if (targetCharacter is null || targetCharacter.IsDead) { return; }
        
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