using DownKyi.Application.Bilibili;
using DownKyi.Application.Downloads;
using DownKyi.Core.BiliApi.VideoStream;
using DownKyi.Core.BiliApi.VideoStream.Models;
using DownKyi.Core.Settings;
using DownKyi.Domain.Downloads;
using DownKyi.Domain.Results;
using DownKyi.Infrastructure.Downloads;
using DownKyi.Infrastructure.Time;
using DownKyi.Models;
using DownKyi.Services.Download;
using DownKyi.ViewModels.DownloadManager;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging.Abstractions;
using Newtonsoft.Json;

using static DownKyi.Tests.DownloadPipelineTestSupport;

namespace DownKyi.Tests;

public sealed class DownloadPipelineTests
{
    [Fact]
    public async Task StageSequenceStopsAtFirstFailureAndPreservesOrder()
    {
        using var settings = new TestSettingsStore();
        var calls = new List<string>();
        using var cancellation = new CancellationTokenSource();
        var context = CreateContext(settings.Store.Current);
        IDownloadPipelineStage[] stages =
        [
            new RecordingStage("resolve", calls, succeed: true, cancellation.Token),
            new RecordingStage("media", calls, succeed: true, cancellation.Token),
            new RecordingStage("mux", calls, succeed: false, cancellation.Token),
            new RecordingStage("finalize", calls, succeed: true, cancellation.Token)
        ];

        var run = await DownloadPipeline.ExecuteStagesAsync(
            stages,
            context,
            cancellation.Token);

        Assert.False(run.Result.IsSuccess);
        Assert.Equal("mux", run.FailedStage);
        Assert.Equal("test.stage.failed", run.Result.Error?.Code);
        Assert.Equal(["resolve", "media", "mux"], calls);
    }

    [Fact]
    public async Task StageSequenceReturnsSuccessOnlyAfterEveryStageCompletes()
    {
        using var settings = new TestSettingsStore();
        var calls = new List<string>();
        var context = CreateContext(settings.Store.Current);
        IDownloadPipelineStage[] stages =
        [
            new RecordingStage("resolve", calls),
            new RecordingStage("media", calls),
            new RecordingStage("finalize", calls)
        ];

        var run = await DownloadPipeline.ExecuteStagesAsync(
            stages,
            context,
            TestContext.Current.CancellationToken);

        Assert.True(run.Result.IsSuccess);
        Assert.Null(run.FailedStage);
        Assert.Equal(["resolve", "media", "finalize"], calls);
    }

}
