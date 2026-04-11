namespace YAMJCS;

internal static class YAMJServer
{
    public const string PlayerRaptorSpecies = "Mudraptor_player";
    public const string PlayerRaptorHuskSpecies = "Mudraptor_playerhusk";
    public const string PlayerRaptorJobId = "PlayerMudraptorJob";

    public static bool HasMudraptorJob(CharacterInfo? characterInfo)
    {
        string? jobId = characterInfo?.Job?.Prefab?.Identifier.Value;
        return string.Equals(jobId, PlayerRaptorJobId, StringComparison.OrdinalIgnoreCase);
    }

    public static bool IsMudraptor(Character? character)
    {
        if (character is null) { return false; }

        string species = character.SpeciesName.Value;
        return string.Equals(species, PlayerRaptorSpecies, StringComparison.OrdinalIgnoreCase)
               || string.Equals(species, PlayerRaptorHuskSpecies, StringComparison.OrdinalIgnoreCase);
    }

    public static void Log(string message)
    {
        Plugin.PluginInstance?.LoggerService?.Log($"[YAMJCS] {message}");
    }

    public static CharacterPrefab? FindMudraptorPrefab()
    {
        return CharacterPrefab.FindBySpeciesName(PlayerRaptorSpecies.ToIdentifier());
    }
}