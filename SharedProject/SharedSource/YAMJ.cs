namespace YAMJCS;

internal static class YAMJ
{
    //config (temp)
    public const float EatingHungerReduction = 4f;
    //constants
    public const string PlayerRaptorSpecies = "Mudraptor_player";
    public const string PlayerRaptorHuskSpecies = "Mudraptor_playerhusk";
    public const string PlayerRaptorJobId = "PlayerMudraptorJob";
    //vars
    public static AfflictionPrefab HungerPrefab;
    public static AfflictionPrefab EatingBuffPrefab;
    
    public static ILoggerService? LoggerService { get; set; }
    public static void Log(string message) {
        LoggerService?.Log($"[YAMJCS] {message}");
    }

    public static CharacterPrefab? FindMudraptorPrefab()
    {
        return CharacterPrefab.FindBySpeciesName(PlayerRaptorSpecies.ToIdentifier());
    }
    
    public static bool HasPlayerRaptorJob(CharacterInfo? characterInfo)
    {
        string? jobId = characterInfo?.Job?.Prefab?.Identifier.Value;
        return string.Equals(jobId, PlayerRaptorJobId, StringComparison.OrdinalIgnoreCase);
    }

    public static bool IsPlayerRaptor(Character? character) {
        if (character is null) return false;
        if (character.SpeciesName.Value == PlayerRaptorSpecies || character.SpeciesName.Value == PlayerRaptorHuskSpecies) {
            return true;
        } else {
            return false;
        }
    }

    public static bool HasTalent(Character character, string talentId) {
        HashSet<Identifier> talents = character.Info.UnlockedTalents;
        if (talents == null || talents.Count == 0) return false;
        if (talents.Contains(talentId.ToIdentifier())) {
            return true;
        } else {
            return false;
        }
    }

    public static void SharedDispose() {
        LoggerService = null;
    }
}