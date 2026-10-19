using DownKyi.Domain.Downloads;

namespace DownKyi.Core.Settings.Models;

public class VideoContentSettings
{
    public bool DownloadAudio { get; set; } = true;
    public bool DownloadVideo { get; set; } = true;
    public bool DownloadDanmaku { get; set; } = true;
    public bool DownloadSubtitle { get; set; }
    public bool DownloadCover { get; set; } = true;

    public SubtitleLanguagePreference PreferredSubtitleLanguage { get; set; }
    public SubtitleTypePreference PreferredSubtitleType { get; set; }

    public bool GenerateMovieMetadata { get; set; }
}
