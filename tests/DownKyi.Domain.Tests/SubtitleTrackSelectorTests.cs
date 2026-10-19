using DownKyi.Domain.Downloads;

namespace DownKyi.Domain.Tests;

public sealed class SubtitleTrackSelectorTests
{
    [Fact]
    public void SelectsPreferredTypeWithinRequestedLanguage()
    {
        var selected = SubtitleTrackSelector.SelectPreferredTrack(
        [
            new(11, "zh-CN", 0),
            new(22, "ai-zh", 1),
            new(33, "en-US", 0)
        ], new SubtitleTrackPreference(
            SubtitleLanguagePreference.ChineseSimplified,
            SubtitleTypePreference.Ai));

        Assert.Equal(22, selected);
    }

    [Fact]
    public void FallsBackToOtherTypeButNeverAnotherLanguage()
    {
        var selected = SubtitleTrackSelector.SelectPreferredTrack(
        [
            new(11, "ai-zh-TW", 1),
            new(22, "en-US", 0)
        ], new SubtitleTrackPreference(
            SubtitleLanguagePreference.ChineseTraditional,
            SubtitleTypePreference.Standard));
        var missing = SubtitleTrackSelector.SelectPreferredTrack(
        [
            new(22, "en-US", 0)
        ], new SubtitleTrackPreference(
            SubtitleLanguagePreference.ChineseTraditional,
            SubtitleTypePreference.Standard));

        Assert.Equal(11, selected);
        Assert.Null(missing);
    }
}
