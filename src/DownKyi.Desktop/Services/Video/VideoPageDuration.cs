using DownKyi.Core.Utils;

namespace DownKyi.Services.Video;

internal static class VideoPageDuration
{
    public static string FromSeconds(long duration) =>
        duration > 0 ? Format.FormatDuration(duration) : "N/A";

    public static string FromMilliseconds(long duration) =>
        duration > 0 ? Format.FormatDuration(duration / 1000) : "N/A";
}
