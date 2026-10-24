using DownKyi.Application.Settings;
using DownKyi.Domain.Downloads;
using DownKyi.Services.Download;
using static DownKyi.Tests.DownloadPipelineTestSupport;

namespace DownKyi.Tests;

public sealed class MuxAndFinalizeStageTests
{
    [Fact]
    public void OutputLayoutSelectsDashPathFromRequestedStreamShape()
    {
        using var settings = new TestSettingsStore();
        var videoContext = CreateContext(settings.Store.Current);
        videoContext.VideoFile = "video-stream";
        Assert.EndsWith(
            ".mp4",
            DownloadOutputLayout.GetDashMediaPath(
                videoContext.WorkingBasePath,
                hasVideo: true,
                audioCodecId: videoContext.Input.Metadata.AudioCodec.Id,
                transcodeAacToMp3: videoContext.Input.VideoSettings.IsTranscodingAacToMp3),
            StringComparison.Ordinal);

        var audioContext = CreateContext(
            settings.Store.Current,
            requestedContent: DownloadContentSelection.None with { Audio = true });
        Assert.EndsWith(
            ".mp3",
            DownloadOutputLayout.GetDashMediaPath(
                audioContext.WorkingBasePath,
                hasVideo: false,
                audioCodecId: audioContext.Input.Metadata.AudioCodec.Id,
                transcodeAacToMp3: audioContext.Input.VideoSettings.IsTranscodingAacToMp3),
            StringComparison.Ordinal);

        var losslessContext = CreateContext(
            settings.Store.Current with
            {
                Video = settings.Store.Current.Video with
                {
                    IsTranscodingAacToMp3 = AllowStatus.No
                }
            },
            audioCodecId: 30251);
        Assert.EndsWith(
            ".flac",
            DownloadOutputLayout.GetDashMediaPath(
                losslessContext.WorkingBasePath,
                hasVideo: false,
                audioCodecId: losslessContext.Input.Metadata.AudioCodec.Id,
                transcodeAacToMp3: losslessContext.Input.VideoSettings.IsTranscodingAacToMp3),
            StringComparison.Ordinal);
    }

    [Fact]
    public void FinalizeStageUsesInjectedClockForCompletionSummary()
    {
        var finishedAt = new DateTimeOffset(
            2026,
            7,
            26,
            1,
            2,
            3,
            TimeSpan.Zero);

        var completion = FinalizeStage.CreateCompletionSummary(
            maximumBytesPerSecond: 1_250_000,
            timeProvider: new FixedTimeProvider(finishedAt));

        Assert.Equal(finishedAt.ToUnixTimeSeconds(), completion.FinishedTimestamp);
        Assert.False(string.IsNullOrEmpty(completion.FinishedTimeText));
        Assert.False(string.IsNullOrEmpty(completion.MaximumSpeedText));
    }

}
