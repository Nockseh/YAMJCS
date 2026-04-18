namespace YAMJCS;

internal static class YAMJ
{
    public const string PlayerRaptorSpecies = "Mudraptor_player";
    public const string PlayerRaptorHuskSpecies = "Mudraptor_playerhusk";
    public const string PlayerRaptorJobId = "PlayerMudraptorJob";
    
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

    public static void SharedDispose() {
        LoggerService = null;
    }
}