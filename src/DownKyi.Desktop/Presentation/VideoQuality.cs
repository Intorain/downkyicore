namespace DownKyi.Presentation;

internal sealed class VideoQuality
{
    internal bool IsDurl { get; set; }

    public int Quality { get; set; }

    public string QualityFormat { get; set; } = string.Empty;

    public string SelectedVideoCodec { get; set; } = string.Empty;
}
