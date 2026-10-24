using HarmonyLib;

namespace YAMJCS;

internal static class BondCaptain {
    internal static readonly Identifier TalentId = "yamjBondCaptain".ToIdentifier();
    internal static readonly Identifier CaptainJobId = "captain".ToIdentifier();
    internal static readonly Identifier SicBuffId = "yamjSicBuff".ToIdentifier();
    internal static readonly Identifier CaptainProxBuffId = "yamjCaptainProxBuff".ToIdentifier();
    internal static readonly Identifier SicCooldownId = "yamjSicCooldown".ToIdentifier();
    internal static readonly Identifier SicAwayBuffId = "yamjSicAwayBuff".ToIdentifier();
    
    private static AfflictionPrefab? _captainProxBuffPrefab;
    internal static AfflictionPrefab? CaptainProxBuffPrefab => _captainProxBuffPrefab ??= YAMJ.FindAfflictionPrefab(CaptainProxBuffId);
    
    private static AfflictionPrefab? _sicCooldownPrefab;
    internal static AfflictionPrefab? SicCooldownPrefab => _sicCooldownPrefab ??= YAMJ.FindAfflictionPrefab(SicCooldownId);
    
    private static AfflictionPrefab? _sicBuffPrefab;
    internal static AfflictionPrefab? SicBuffPrefab => _sicBuffPrefab ??= YAMJ.FindAfflictionPrefab(SicBuffId);

    private static AfflictionPrefab? _sicAwayBuffPrefab;
    internal static AfflictionPrefab? SicAwayBuffPrefab => _sicAwayBuffPrefab ??= YAMJ.FindAfflictionPrefab(SicAwayBuffId);
    
    //todo move to config
    private static readonly float auraRange = 2000f;

    internal static bool IsCaptain(Character? character) {
        if (character is null || character.Removed) { return false; }
        Identifier jobId = character.Info?.Job?.Prefab?.Identifier ?? Identifier.Empty;
        return jobId == CaptainJobId;
    }
    
    internal static void UpdateProximity() {
    
        //sweep all characters
        List<Character> captainList = new List<Character>();
        List<Character> raptorList = new List<Character>();
        foreach (Character character in Character.CharacterList) {
            if (character.Removed || character.IsDead) {
                continue;
            }

            if (IsCaptain(character)) {
                captainList.Add(character);
            }

            if (YAMJ.IsPlayerRaptor(character)) {
                raptorList.Add(character);
            }
        }
        
        //check all captains for nearby raptors with talent
        foreach (Character captain in captainList) {
            Vector2 cPos = captain.WorldPosition;
            bool isInRange = false;
            
            foreach (Character raptor in raptorList) {
                if (!raptor.HasTalent(TalentId)) { continue; }
                Vector2 rPos = raptor.WorldPosition;
                if (Vector2.DistanceSquared(cPos, rPos) <= auraRange * auraRange) {
                    isInRange = true;
                    break;
                }
            }

            if (CaptainProxBuffPrefab is not null) {
                if (isInRange) {
                    captain.CharacterHealth.AddAffliction(CaptainProxBuffPrefab.Instantiate(1f));
                }
                else {
                    captain.CharacterHealth.ReduceAfflictionOnAllLimbs(CaptainProxBuffId, CaptainProxBuffPrefab.MaxStrength * 2f);
                }
            } else {
                YAMJ.Log("YAMJ Bond with Captain proximity buff prefab not found");
            }
        }
    }
}

[HarmonyPatch] //allows captains to sic player raptors with the bond talent
internal static class SicRaptor {
    private static MethodBase TargetMethod() => PatchTargets.Character_DoInteractionUpdate;

    private static void Postfix(Character __instance) {
        if (!BondCaptain.IsCaptain(__instance)) { return; }
        
        Character? focusedCharacter = __instance.FocusedCharacter;
        if (focusedCharacter is null || focusedCharacter.IsDead || !YAMJ.IsPlayerRaptor(focusedCharacter) || !focusedCharacter.HasTalent(BondCaptain.TalentId)) {
            return;
        }
        if (!HumanAIController.IsFriendly(__instance, focusedCharacter)) { return; }
        
        #if CLIENT
            focusedCharacter.SetCustomInteract(null, CharacterHUD.GetCachedHudText("yamjHint.sicRaptor", InputType.Select));
        #endif

        if (!__instance.IsKeyHit(InputType.Select)) { return; } //reminder: not edge-detected on server
        if (GameMain.NetworkMember is { IsClient: true }) { return; } //server authority
        
        CharacterHealth raptorHealth = focusedCharacter.CharacterHealth;
        if (raptorHealth.GetAfflictionStrengthByIdentifier(BondCaptain.SicCooldownId, false) > 0f) { return; }

        YAMJ.ApplyAtMaxStrength(focusedCharacter, BondCaptain.SicCooldownPrefab);
        YAMJ.ApplyAtMaxStrength(focusedCharacter, BondCaptain.SicBuffPrefab);
        YAMJ.ApplyAtMaxStrength(__instance, BondCaptain.SicAwayBuffPrefab);
        
    }
}