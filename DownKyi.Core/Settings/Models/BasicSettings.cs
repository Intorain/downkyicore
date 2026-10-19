namespace DownKyi.Core.Settings.Models;

/// <summary>
/// 基本
/// </summary>
public class BasicSettings
{
    public ThemeMode ThemeMode { get; set; } = ThemeMode.Default;
    public AfterDownloadOperation AfterDownload { get; set; } = AfterDownloadOperation.NotSet;
    public AllowStatus IsListenClipboard { get; set; } = AllowStatus.None;
    public DownloadFinishedSort DownloadFinishedSort { get; set; } = DownloadFinishedSort.NotSet;
    public bool RepeatFileAutoAddNumberSuffix { get; set; }
}
