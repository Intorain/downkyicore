using DownKyi.Application.Bilibili;
using DownKyi.Application.Downloads;
using DownKyi.Core.BiliApi.VideoStream;
using DownKyi.Core.BiliApi.VideoStream.Models;
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

public sealed class DownloadMediaDurlTests
{
    [Fact]
    public async Task MediaStageRejectsDuplicateDurlOrdersBeforeAnyTransfer()
    {
        var playUrl = new PlayUrl
        {
            Durl =
            [
                new PlayUrlDurl
                {
                    Order = 7,
                    SourceAddress = "https://example.invalid/segment-7-primary"
                },
                new PlayUrlDurl
                {
                    Order = 7,
                    SourceAddress = "https://example.invalid/segment-7-duplicate"
                }
            ]
        };
        using var fixture = await MediaStageFixture.CreateAsync(
            playUrl,
            downloadAudio: false,
            downloadVideo: true,
            finalizedMediaKindOverride: DownloadMediaKind.Durl).ConfigureAwait(true);

        var result = await fixture.Stage.ExecuteAsync(
            fixture.Context,
            TestContext.Current.CancellationToken);

        Assert.False(result.IsSuccess);
        Assert.Equal("download.playback.selection-unavailable", result.Error?.Code);
        Assert.Empty(fixture.Backend.Requests);
    }

    [Fact]
    public async Task MediaStageRejectsAddresslessDurlBeforeEarlierSegmentTransfer()
    {
        var playUrl = new PlayUrl
        {
            Durl =
            [
                new PlayUrlDurl
                {
                    Order = 1,
                    SourceAddress = "https://example.invalid/segment-1"
                },
                new PlayUrlDurl
                {
                    Order = 2,
                    SourceAddress = "  ",
                    BackupUrl = ["", "  "]
                }
            ]
        };
        using var fixture = await MediaStageFixture.CreateAsync(
            playUrl,
            downloadAudio: false,
            downloadVideo: true,
            finalizedMediaKindOverride: DownloadMediaKind.Durl).ConfigureAwait(true);

        var result = await fixture.Stage.ExecuteAsync(
            fixture.Context,
            TestContext.Current.CancellationToken);

        Assert.False(result.IsSuccess);
        Assert.Equal("download.playback.selection-unavailable", result.Error?.Code);
        Assert.Empty(fixture.Backend.Requests);
    }

    [Fact]
    public async Task MediaStageTransfersValidatedDurlsInOrder()
    {
        var playUrl = new PlayUrl
        {
            Durl =
            [
                new PlayUrlDurl
                {
                    Order = 9,
                    SourceAddress = "https://example.invalid/segment-9"
                },
                new PlayUrlDurl
                {
                    Order = 2,
                    SourceAddress = "https://example.invalid/segment-2"
                },
                new PlayUrlDurl
                {
                    Order = 5,
                    SourceAddress = "",
                    BackupUrl = ["https://example.invalid/segment-5"]
                }
            ]
        };
        using var fixture = await MediaStageFixture.CreateAsync(
            playUrl,
            downloadAudio: false,
            downloadVideo: true).ConfigureAwait(true);

        var result = await fixture.Stage.ExecuteAsync(
            fixture.Context,
            TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
        Assert.Equal(
            [
                "https://example.invalid/segment-2",
                "https://example.invalid/segment-5",
                "https://example.invalid/segment-9"
            ],
            fixture.Backend.Requests.Select(request => Assert.Single(request.Urls)));
    }

    [Fact]
    public async Task MediaStageRejectsMalformedRefreshedDurlManifestBeforeRetryTransfer()
    {
        var playUrl = new PlayUrl
        {
            Durl =
            [
                new PlayUrlDurl
                {
                    Order = 1,
                    SourceAddress = "https://example.invalid/original-segment"
                }
            ]
        };
        var refreshRequestCount = 0;
        var apiClient = new TestBilibiliApiClient
        {
            GetStringAsyncHandler = (_, _) =>
            {
                refreshRequestCount++;
                return Task.FromResult(
                    """
                    {
                      "code": 0,
                      "message": "success",
                      "data": {
                        "quality": 80,
                        "video_codecid": 7,
                        "durl": [
                          { "order": 1, "url": "https://example.invalid/refreshed-a" },
                          { "order": 1, "url": "https://example.invalid/refreshed-b" }
                        ]
                      }
                    }
                    """);
            }
        };
        using var fixture = await MediaStageFixture.CreateAsync(
            playUrl,
            downloadAudio: false,
            downloadVideo: true,
            apiClient: apiClient,
            backendResults:
            [
                DownloadTransferResult.Failed(
                    DownloadTransferFailureKind.ExpiredAddress,
                    "download.transfer.http-403")
            ]).ConfigureAwait(true);

        var result = await fixture.Stage.ExecuteAsync(
            fixture.Context,
            TestContext.Current.CancellationToken);

        Assert.False(result.IsSuccess);
        Assert.Equal("download.playback.selection-unavailable", result.Error?.Code);
        Assert.Equal(2, refreshRequestCount);
        Assert.Single(fixture.Backend.Requests);
    }

    [Fact]
    public async Task MediaStageRejectsAudioOnlyDurlWithoutStartingTransfer()
    {
        using var fixture = await MediaStageFixture.CreateAsync(
            CreateDurlPlayUrl(),
            downloadAudio: true,
            downloadVideo: false).ConfigureAwait(true);

        var result = await fixture.Stage.ExecuteAsync(
            fixture.Context,
            TestContext.Current.CancellationToken);

        Assert.False(result.IsSuccess);
        Assert.Equal("download.media.durl-audio-only", result.Error?.Code);
        Assert.Empty(fixture.Backend.Requests);
    }

    [Theory]
    [InlineData(64, 7)]
    [InlineData(80, 12)]
    public async Task MediaStageRejectsDurlThatDoesNotMatchFinalizedVideoSelection(
        int quality,
        int codecId)
    {
        using var fixture = await MediaStageFixture.CreateAsync(
            CreateDurlPlayUrl(quality, codecId),
            downloadAudio: false,
            downloadVideo: true).ConfigureAwait(true);

        var result = await fixture.Stage.ExecuteAsync(
            fixture.Context,
            TestContext.Current.CancellationToken);

        Assert.False(result.IsSuccess);
        Assert.Equal("download.playback.selection-unavailable", result.Error?.Code);
        Assert.Empty(fixture.Backend.Requests);
    }

    [Fact]
    public async Task MediaStageRejectsPlaybackTransportThatDiffersFromFinalizedContract()
    {
        using var fixture = await MediaStageFixture.CreateAsync(
            CreateDurlPlayUrl(),
            downloadAudio: false,
            downloadVideo: true,
            finalizedMediaKindOverride: DownloadMediaKind.Dash).ConfigureAwait(true);

        var result = await fixture.Stage.ExecuteAsync(
            fixture.Context,
            TestContext.Current.CancellationToken);

        Assert.False(result.IsSuccess);
        Assert.Equal("download.playback.selection-unavailable", result.Error?.Code);
        Assert.Empty(fixture.Backend.Requests);
    }

    [Fact]
    public async Task MediaStageRejectsRefreshedDurlThatChangesFinalizedQuality()
    {
        BilibiliHttpRequest? refreshRequest = null;
        var apiClient = new TestBilibiliApiClient
        {
            GetStringAsyncHandler = (request, _) =>
            {
                refreshRequest = request;
                return Task.FromResult(
                    """
                    {
                      "code": 0,
                      "message": "success",
                      "data": {
                        "quality": 64,
                        "video_codecid": 7,
                        "durl": [
                          { "order": 1, "url": "https://example.invalid/refreshed-segment" }
                        ]
                      }
                    }
                    """);
            }
        };
        using var fixture = await MediaStageFixture.CreateAsync(
            CreateDurlPlayUrl(),
            downloadAudio: false,
            downloadVideo: true,
            apiClient: apiClient,
            backendResults:
            [
                DownloadTransferResult.Failed(
                    DownloadTransferFailureKind.ExpiredAddress,
                    "download.transfer.http-403")
            ]).ConfigureAwait(true);

        var result = await fixture.Stage.ExecuteAsync(
            fixture.Context,
            TestContext.Current.CancellationToken);

        Assert.False(result.IsSuccess);
        Assert.Equal("download.playback.selection-unavailable", result.Error?.Code);
        Assert.NotNull(refreshRequest);
        Assert.Contains("qn=80", refreshRequest.RequestAddress, StringComparison.Ordinal);
        Assert.Single(fixture.Backend.Requests);
    }

    [Fact]
    public async Task MediaStageReportsUnavailableWhenRefreshedDurlOmitsPendingOrder()
    {
        var playUrl = CreateDurlPlayUrl();
        playUrl.Durl =
        [
            new PlayUrlDurl
            {
                Order = 2,
                SourceAddress = "https://example.invalid/original-segment-2"
            }
        ];
        var apiClient = new TestBilibiliApiClient
        {
            GetStringAsyncHandler = (_, _) => Task.FromResult(
                """
                {
                  "code": 0,
                  "message": "success",
                  "data": {
                    "quality": 80,
                    "video_codecid": 7,
                    "durl": [
                      { "order": 1, "url": "https://example.invalid/refreshed-segment-1" }
                    ]
                  }
                }
                """)
        };
        using var fixture = await MediaStageFixture.CreateAsync(
            playUrl,
            downloadAudio: false,
            downloadVideo: true,
            apiClient: apiClient,
            backendResults:
            [
                DownloadTransferResult.Failed(
                    DownloadTransferFailureKind.ExpiredAddress,
                    "download.transfer.http-403")
            ]).ConfigureAwait(true);

        var result = await fixture.Stage.ExecuteAsync(
            fixture.Context,
            TestContext.Current.CancellationToken).ConfigureAwait(true);

        Assert.False(result.IsSuccess);
        Assert.Equal("download.playback.selection-unavailable", result.Error?.Code);
        Assert.Single(fixture.Backend.Requests);
    }
}
