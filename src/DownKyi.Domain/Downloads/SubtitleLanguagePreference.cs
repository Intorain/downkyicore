namespace DownKyi.Domain.Downloads;

public enum SubtitleLanguagePreference
{
    Unspecified,
    ChineseSimplified,
    ChineseTraditional,
    English,
    Japanese,
    Korean
}

public enum SubtitleTypePreference
{
    Standard,
    Ai
}

public readonly record struct SubtitleTrackPreference(
    SubtitleLanguagePreference Language,
    SubtitleTypePreference Type)
{
    public bool IsConfigured => Language != SubtitleLanguagePreference.Unspecified;
}

public readonly record struct SubtitleTrackCandidate(long TrackId, string Language, int Type);

public static class SubtitleTrackSelector
{
    public static long? SelectPreferredTrack(
        IEnumerable<SubtitleTrackCandidate> tracks,
        SubtitleTrackPreference preference)
    {
        ArgumentNullException.ThrowIfNull(tracks);
        if (!preference.IsConfigured)
        {
            return null;
        }

        var matchingLanguage = tracks
            .Where(track => MatchesLanguage(track.Language, preference.Language))
            .ToArray();
        var preferredType = preference.Type == SubtitleTypePreference.Ai ? 1 : 0;
        var fallbackType = preferredType == 0 ? 1 : 0;
        var preferred = matchingLanguage.FirstOrDefault(track => track.Type == preferredType);
        if (preferred.TrackId > 0)
        {
            return preferred.TrackId;
        }

        var fallback = matchingLanguage.FirstOrDefault(track => track.Type == fallbackType);
        return fallback.TrackId > 0 ? fallback.TrackId : null;
    }

    private static bool MatchesLanguage(string language, SubtitleLanguagePreference preference)
    {
        var normalized = language.Trim().ToUpperInvariant();
        if (normalized.StartsWith("AI-", StringComparison.Ordinal))
        {
            normalized = normalized[3..];
        }

        return preference switch
        {
            SubtitleLanguagePreference.ChineseSimplified =>
                normalized is "ZH" or "ZH-CN" or "ZH-HANS" or "ZH-HANS-CN",
            SubtitleLanguagePreference.ChineseTraditional =>
                normalized is "ZH-TW" or "ZH-HK" or "ZH-MO" or "ZH-HANT"
                    or "ZH-HANT-TW" or "ZH-HANT-HK",
            SubtitleLanguagePreference.English => HasLanguagePrefix(normalized, "EN"),
            SubtitleLanguagePreference.Japanese =>
                HasLanguagePrefix(normalized, "JA") || HasLanguagePrefix(normalized, "JP"),
            SubtitleLanguagePreference.Korean => HasLanguagePrefix(normalized, "KO"),
            _ => false
        };
    }

    private static bool HasLanguagePrefix(string language, string prefix) =>
        language.Equals(prefix, StringComparison.Ordinal)
        || language.StartsWith($"{prefix}-", StringComparison.Ordinal);
}
