using DownKyi.Domain.Downloads;
using DownKyi.Utils;

namespace DownKyi.ViewModels;

internal sealed record SubtitleLanguagePreferenceItem(
    string Name,
    SubtitleLanguagePreference Value);

internal sealed record SubtitleTypePreferenceItem(
    string Name,
    SubtitleTypePreference Value);

internal static class SubtitlePreferenceOptions
{
    public const SubtitleLanguagePreference DefaultLanguage =
        SubtitleLanguagePreference.ChineseSimplified;

    public static IReadOnlyList<SubtitleLanguagePreferenceItem> CreateLanguages() =>
    [
        new(DictionaryResource.GetString("SubtitleLanguageChineseSimplified"),
            SubtitleLanguagePreference.ChineseSimplified),
        new(DictionaryResource.GetString("SubtitleLanguageChineseTraditional"),
            SubtitleLanguagePreference.ChineseTraditional),
        new(DictionaryResource.GetString("SubtitleLanguageEnglish"),
            SubtitleLanguagePreference.English),
        new(DictionaryResource.GetString("SubtitleLanguageJapanese"),
            SubtitleLanguagePreference.Japanese),
        new(DictionaryResource.GetString("SubtitleLanguageKorean"),
            SubtitleLanguagePreference.Korean)
    ];

    public static IReadOnlyList<SubtitleTypePreferenceItem> CreateTypes() =>
    [
        new(DictionaryResource.GetString("SubtitlePreferenceStandardFirst"),
            SubtitleTypePreference.Standard),
        new(DictionaryResource.GetString("SubtitlePreferenceAiFirst"),
            SubtitleTypePreference.Ai)
    ];

    public static SubtitleLanguagePreference EffectiveLanguage(
        SubtitleLanguagePreference language) =>
        language == SubtitleLanguagePreference.Unspecified ? DefaultLanguage : language;
}
