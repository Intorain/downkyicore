using DownKyi.Application.Desktop;
using DownKyi.Application.Downloads;
using DownKyi.Core.BiliApi.VideoStream.Models;
using DownKyi.Domain.Downloads;
using DownKyi.Domain.Results;
using DownKyi.Infrastructure.Downloads;
using DownKyi.Infrastructure.Time;
using DownKyi.Models;
using DownKyi.Presentation;
using DownKyi.Services.Download;
using DownKyi.ViewModels.DownloadManager;

namespace DownKyi.Tests;

public sealed class DownloadAddOwnerTests : IDisposable
{
    private readonly string _directory = Path.Combine(
        Path.GetTempPath(),
        "downkyi-download-add-owner-tests",
        Guid.NewGuid().ToString("N"));

    [Fact]
    public async Task DurableActiveDuplicateIsSkippedWhenUiListIsEmpty()
    {
        using var context = new DuplicatePolicyContext(AppDialogOutcome.Canceled);
        await context.PersistActiveAsync(CreateDownloadingItem());

        var shouldSkip = await context.Policy.ShouldSkipAsync(
            CreateRequestedItem(DownloadContentSelection.All),
            DownKyi.Core.Settings.RepeatDownloadStrategy.ReDownload,
            TestContext.Current.CancellationToken);

        Assert.True(shouldSkip);
        Assert.Empty(context.ListState.Downloading);
        Assert.Equal(0, context.Dialogs.ShowCount);
    }

    [Fact]
    public async Task UiOnlyActiveItemDoesNotAffectDuplicateDecision()
    {
        using var context = new DuplicatePolicyContext(AppDialogOutcome.Canceled);
        context.ListState.AddDownloading(CreateDownloadingItem());

        var shouldSkip = await context.Policy.ShouldSkipAsync(
            CreateRequestedItem(DownloadContentSelection.All),
            DownKyi.Core.Settings.RepeatDownloadStrategy.JumpOver,
            TestContext.Current.CancellationToken);

        Assert.False(shouldSkip);
        Assert.Single(context.ListState.Downloading);
    }

    [Fact]
    public async Task SameCidActiveTaskSkipsRegardlessOfRequestedContent()
    {
        using var context = new DuplicatePolicyContext(AppDialogOutcome.Canceled);
        var videoOnly = CreateDownloadingItem();
        videoOnly.DownloadBase.NeedDownloadContent =
            DownloadContentSelection.None with { Video = true };
        await context.PersistActiveAsync(videoOnly);

        var shouldSkip = await context.Policy.ShouldSkipAsync(
            CreateRequestedItem(DownloadContentSelection.None with { Subtitle = true }),
            DownKyi.Core.Settings.RepeatDownloadStrategy.ReDownload,
            TestContext.Current.CancellationToken);

        Assert.True(shouldSkip);
        Assert.Equal(0, context.Dialogs.ShowCount);
    }

    [Fact]
    public async Task ActiveDuplicateIsSkippedBeforeCompletedHistoryIsRead()
    {
        using var context = DuplicatePolicyContext.WithCompleted(AppDialogOutcome.Accepted);
        await context.PersistActiveAsync(CreateDownloadingItem());

        var shouldSkip = await context.Policy.ShouldSkipAsync(
            CreateRequestedItem(DownloadContentSelection.All),
            DownKyi.Core.Settings.RepeatDownloadStrategy.JumpOver,
            TestContext.Current.CancellationToken);

        Assert.True(shouldSkip);
        Assert.Equal(0, context.Store.HistoryByCidRequestCount);
    }

    [Fact]
    public async Task CompletedHistoryWithSameCidIsSkipped()
    {
        using var context = DuplicatePolicyContext.WithCompleted(AppDialogOutcome.Accepted);

        var shouldSkip = await context.Policy.ShouldSkipAsync(
            CreateRequestedItem(DownloadContentSelection.None with { Cover = true }),
            DownKyi.Core.Settings.RepeatDownloadStrategy.JumpOver,
            TestContext.Current.CancellationToken);

        Assert.True(shouldSkip);
        Assert.NotNull(context.Store.History);
        Assert.Equal(1, context.Store.HistoryByCidRequestCount);
        Assert.Equal(0, context.Dialogs.ShowCount);
    }

    [Fact]
    public async Task CompletedHistoryWithDifferentCidDoesNotSkip()
    {
        using var context = DuplicatePolicyContext.WithCompleted(AppDialogOutcome.Accepted);
        var requested = CreateRequestedItem(DownloadContentSelection.All);
        requested.DownloadBase.Cid++;

        var shouldSkip = await context.Policy.ShouldSkipAsync(
            requested,
            DownKyi.Core.Settings.RepeatDownloadStrategy.JumpOver,
            TestContext.Current.CancellationToken);

        Assert.False(shouldSkip);
        Assert.Equal(1, context.Store.HistoryByCidRequestCount);
        Assert.Equal(0, context.Dialogs.ShowCount);
    }

    [Fact]
    public async Task UiOnlyCompletedItemDoesNotAffectDuplicateDecision()
    {
        using var context = new DuplicatePolicyContext(AppDialogOutcome.Canceled);
        context.ListState.AddDownloaded(DownloadTaskProjectionMapper.ToDownloadedItem(
            DuplicatePolicyContext.CreateCompletedHistory()));

        var shouldSkip = await context.Policy.ShouldSkipAsync(
            CreateRequestedItem(DownloadContentSelection.All),
            DownKyi.Core.Settings.RepeatDownloadStrategy.JumpOver,
            TestContext.Current.CancellationToken);

        Assert.False(shouldSkip);
        Assert.Single(context.ListState.Downloaded);
    }

    [Fact]
    public async Task CompletedDuplicateReDownloadAllowsNewTaskWithoutDeletingHistory()
    {
        using var context = DuplicatePolicyContext.WithCompleted(AppDialogOutcome.Canceled);

        var shouldSkip = await context.Policy.ShouldSkipAsync(
            CreateRequestedItem(DownloadContentSelection.All),
            DownKyi.Core.Settings.RepeatDownloadStrategy.ReDownload,
            TestContext.Current.CancellationToken);

        Assert.False(shouldSkip);
        Assert.NotNull(context.Store.History);
        Assert.Equal(0, context.Store.DeleteHistoryCount);
        Assert.Equal(0, context.Dialogs.ShowCount);
    }

    [Fact]
    public async Task RejectedDuplicateConfirmationPreservesCompletedRecord()
    {
        using var context = DuplicatePolicyContext.WithCompleted(AppDialogOutcome.Canceled);

        var shouldSkip = await context.Policy.ShouldSkipAsync(
            CreateRequestedItem(DownloadContentSelection.All),
            DownKyi.Core.Settings.RepeatDownloadStrategy.Ask,
            TestContext.Current.CancellationToken);

        Assert.True(shouldSkip);
        Assert.NotNull(context.Store.History);
        Assert.Equal(0, context.Store.DeleteHistoryCount);
        Assert.Equal(1, context.Dialogs.ShowCount);
    }

    [Fact]
    public async Task AcceptedDuplicateConfirmationAllowsDownloadAndPreservesHistory()
    {
        using var context = DuplicatePolicyContext.WithCompleted(AppDialogOutcome.Accepted);

        var shouldSkip = await context.Policy.ShouldSkipAsync(
            CreateRequestedItem(DownloadContentSelection.All),
            DownKyi.Core.Settings.RepeatDownloadStrategy.Ask,
            TestContext.Current.CancellationToken);

        Assert.False(shouldSkip);
        Assert.NotNull(context.Store.History);
        Assert.Equal(0, context.Store.DeleteHistoryCount);
        Assert.Equal(1, context.Dialogs.ShowCount);
    }

    [Fact]
    public async Task VersionNineHistoryWithoutCurrentMetadataStillSkipsTheSameCid()
    {
        var legacyHistory = DuplicatePolicyContext.CreateVersionNineHistory();
        using var context = new DuplicatePolicyContext(
            AppDialogOutcome.Accepted,
            history: legacyHistory);
        var requestedQuality = CreateVideoQuality();
        requestedQuality.Quality = 64;
        requestedQuality.QualityFormat = "720P";

        var shouldSkip = await context.Policy.ShouldSkipAsync(
            CreateRequestedItem(
                DownloadContentSelection.None with { Subtitle = true },
                videoQuality: requestedQuality),
            DownKyi.Core.Settings.RepeatDownloadStrategy.JumpOver,
            TestContext.Current.CancellationToken);

        Assert.True(shouldSkip);
        Assert.Null(context.Store.History!.RequestedContent);
        Assert.Empty(context.Store.History.PublishedArtifacts);
    }

    [Fact]
    public async Task DuplicatePolicyPropagatesCancellationBeforeInspectingLists()
    {
        using var context = new DuplicatePolicyContext(AppDialogOutcome.Accepted);
        using var cancellation = new CancellationTokenSource();
        await cancellation.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            context.Policy.ShouldSkipAsync(
                CreateRequestedItem(DownloadContentSelection.All),
                DownKyi.Core.Settings.RepeatDownloadStrategy.Ask,
                cancellation.Token));
        Assert.Equal(0, context.Dialogs.ShowCount);
    }

    [Fact]
    public async Task DurableHistoryReadDoesNotCaptureTheCallingSynchronizationContext()
    {
        using var context = DuplicatePolicyContext.WithCompleted(AppDialogOutcome.Accepted);
        var historyPage = new TaskCompletionSource<DownloadHistoryPage>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        context.Store.PendingHistoryPage = historyPage.Task;
        var originalContext = SynchronizationContext.Current;
        var blockedContext = new NonPumpingSynchronizationContext();
        Task<IReadOnlyList<DownloadedItem>> read;
        try
        {
            SynchronizationContext.SetSynchronizationContext(blockedContext);
            read = context.ProjectionStore.GetDownloadedAsync(
                TestContext.Current.CancellationToken);
        }
        finally
        {
            SynchronizationContext.SetSynchronizationContext(originalContext);
        }

        historyPage.SetResult(new DownloadHistoryPage([context.Store.History!], null));
        var items = await read.WaitAsync(
            TestContext.Current.CancellationToken);

        Assert.Single(items);
        Assert.Equal(0, blockedContext.PostCount);
    }

    [Fact]
    public void DraftFactoryPreservesTaskIdentityQualityContentAndStreamType()
    {
        Directory.CreateDirectory(_directory);
        using var settingsStore = new DownKyi.Core.Settings.SettingsStore(
            Path.Combine(_directory, "settings.json"));
        var page = CreatePage();
        page.AudioQualityFormat = "高质量";
        page.EpisodeId = 99;
        page.Page = 2;
        page.Duration = "01:23";
        page.FirstFrame = "frame";
        var video = new VideoInfoView
        {
            CoverUrl = "cover",
            Title = "main",
            TypeId = -10,
            VideoZone = "Technology>Software"
        };
        var section = new VideoSection
        {
            Title = "section",
            VideoPages = [page]
        };
        var content = new DownloadContentSelection(
            Audio: true,
            Video: false,
            Danmaku: true,
            Subtitle: false,
            Cover: true);

        var item = CreateDraft(
            _directory,
            video,
            section,
            sectionCount: 2,
            page,
            CreateVideoQuality(),
            settingsStore.Current,
            content);

        Assert.Equal(page.Bvid, item.DownloadBase.Bvid);
        Assert.Equal(page.Avid, item.DownloadBase.Avid);
        Assert.Equal(page.Cid, item.DownloadBase.Cid);
        Assert.Equal(page.EpisodeId, item.DownloadBase.EpisodeId);
        Assert.Equal(page.Page, item.DownloadBase.Page);
        Assert.Equal(0, item.Resolution.Id);
        Assert.Equal(string.Empty, item.Resolution.Name);
        Assert.Equal(string.Empty, item.VideoCodecName);
        Assert.Equal(
            DownKyi.Core.BiliApi.VideoStream.PlayStreamType.Cheese,
            item.Downloading.PlayStreamType);
        Assert.Equal(DownKyi.Models.DownloadStatus.NotStarted, item.Downloading.DownloadStatus);
        Assert.Equal(
            content with
            {
                MediaKind = DownloadMediaKind.Dash,
                DanmakuOutputFormat = DownloadDanmakuOutputFormat.Ass
            },
            item.DownloadBase.NeedDownloadContent);
        Assert.StartsWith(_directory, item.DownloadBase.FilePath, StringComparison.Ordinal);
        Assert.Contains("section", item.DownloadBase.FilePath, StringComparison.Ordinal);
    }

    [Fact]
    public void DraftFactoryDefersCollisionResolutionToAtomicAdmission()
    {
        Directory.CreateDirectory(_directory);
        using var settingsStore = new DownKyi.Core.Settings.SettingsStore(
            Path.Combine(_directory, "settings.json"));
        var settings = settingsStore.Current with
        {
            Basic = settingsStore.Current.Basic with
            {
                RepeatFileAutoAddNumberSuffix = true
            }
        };
        var page = CreatePage();
        page.AudioQualityFormat = "高质量";
        var video = new VideoInfoView
        {
            Title = "main",
            VideoZone = "Technology"
        };
        var section = new VideoSection
        {
            Title = "section",
            VideoPages = [page]
        };
        var first = CreateDraft(
            _directory,
            video,
            section,
            sectionCount: 1,
            page,
            CreateVideoQuality(),
            settings,
            DownloadContentSelection.All);
        Directory.CreateDirectory(Path.GetDirectoryName(first.DownloadBase.FilePath)!);
        File.WriteAllText($"{first.DownloadBase.FilePath}.mp4", "occupied");

        var second = CreateDraft(
            _directory,
            video,
            section,
            sectionCount: 1,
            page,
            CreateVideoQuality(),
            settings,
            DownloadContentSelection.All);

        Assert.Equal(first.DownloadBase.FilePath, second.DownloadBase.FilePath);
    }

    [Fact]
    public void DraftFactoryRejectsAudioOnlyDurlContract()
    {
        Directory.CreateDirectory(_directory);
        using var settingsStore = new DownKyi.Core.Settings.SettingsStore(
            Path.Combine(_directory, "settings.json"));
        var page = CreatePage();
        page.PlaybackAvailability = PlayUrlAvailability.From(CreateDurlPlayUrl());
        var video = new VideoInfoView
        {
            Title = "main",
            VideoZone = "Technology"
        };
        var section = new VideoSection
        {
            Title = "section",
            VideoPages = [page]
        };

        var error = Assert.Throws<InvalidOperationException>(() => CreateDraft(
            _directory,
            video,
            section,
            sectionCount: 1,
            page,
            CreateVideoQuality(isDurl: true),
            settingsStore.Current,
            DownloadContentSelection.None with { Audio = true }));

        Assert.Equal(
            "A media download draft requires a supported finalized playback selection.",
            error.Message);
    }

    [Fact]
    public void DraftFactoryUsesIndependentDashAudioWithoutSelectedVideo()
    {
        Directory.CreateDirectory(_directory);
        using var settingsStore = new DownKyi.Core.Settings.SettingsStore(
            Path.Combine(_directory, "settings.json"));
        var page = CreatePage();
        page.AudioQualityFormat = "高质量";
        page.VideoQuality = null;
        page.PlaybackAvailability = PlayUrlAvailability.From(new PlayUrl
        {
            Quality = 80,
            VideoCodecid = 7,
            Durl = [new PlayUrlDurl { SourceAddress = "https://media.invalid/video.mp4" }],
            Dash = new PlayUrlDash
            {
                Audio = [new PlayUrlDashVideo
                {
                    Id = 30280,
                    BaseAddress = "https://media.invalid/audio.m4s"
                }]
            }
        });
        var video = new VideoInfoView { Title = "main", VideoZone = "Technology" };
        var section = new VideoSection { VideoPages = [page] };

        var item = CreateDraft(
            _directory,
            video,
            section,
            1,
            page,
            null,
            settingsStore.Current,
            DownloadContentSelection.None with { Audio = true });

        Assert.Equal(DownloadMediaKind.Dash, item.DownloadBase.NeedDownloadContent.MediaKind);
        Assert.Equal(0, item.Resolution.Id);
        Assert.Equal(string.Empty, item.VideoCodecName);
        Assert.Equal(30280, item.AudioCodec.Id);
    }

    [Fact]
    public void DraftFactoryUsesFinalizedPlaybackSnapshotAfterPageChanges()
    {
        Directory.CreateDirectory(_directory);
        using var settingsStore = new DownKyi.Core.Settings.SettingsStore(
            Path.Combine(_directory, "settings.json"));
        var page = CreatePage();
        page.AudioQualityFormat = "高质量";
        var videoQuality = Assert.IsType<VideoQuality>(page.VideoQuality);
        var audioQualityFormat = page.AudioQualityFormat;
        var availableMedia = DownloadMediaCapabilities.From(
            page.PlaybackAvailability,
            videoQuality,
            audioQualityFormat);
        var video = new VideoInfoView { Title = "main", VideoZone = "Technology" };
        var section = new VideoSection { VideoPages = [page] };

        page.VideoQuality = null;
        page.AudioQualityFormat = string.Empty;
        page.PlaybackAvailability = null;

        var item = DownloadTaskDraftFactory.Create(
            _directory,
            video,
            section,
            1,
            page,
            videoQuality,
            audioQualityFormat,
            availableMedia,
            settingsStore.Current,
            DownloadContentSelection.None with { Audio = true, Video = true });

        Assert.Equal(DownloadMediaKind.Dash, item.DownloadBase.NeedDownloadContent.MediaKind);
        Assert.Equal(80, item.Resolution.Id);
        Assert.Equal("H.264/AVC", item.VideoCodecName);
        Assert.Equal(30280, item.AudioCodec.Id);
    }

    [Fact]
    public void DraftFactoryPersistsFinalizedSelectedMediaKind()
    {
        Directory.CreateDirectory(_directory);
        using var settingsStore = new DownKyi.Core.Settings.SettingsStore(
            Path.Combine(_directory, "settings.json"));
        var page = CreatePage();
        page.PlaybackAvailability = PlayUrlAvailability.From(CreateDurlPlayUrl());
        var video = new VideoInfoView
        {
            Title = "main",
            TypeId = 13,
            VideoZone = "Anime"
        };
        var section = new VideoSection
        {
            Title = "section",
            VideoPages = [page]
        };

        var item = CreateDraft(
            _directory,
            video,
            section,
            sectionCount: 1,
            page,
            CreateVideoQuality(isDurl: true),
            settingsStore.Current,
            DownloadContentSelection.None with { Video = true });

        Assert.Equal(DownloadMediaKind.Durl, item.DownloadBase.NeedDownloadContent.MediaKind);
        Assert.Equal(0, item.AudioCodec.Id);
        Assert.Contains(
            page.PlaybackAvailability!.Video,
            video => video.StreamKind == PlayUrlStreamKind.Durl);
    }

    [Fact]
    public void DraftFactoryUsesTheSelectedCandidateWhenQualityIdsMatchAcrossStreamKinds()
    {
        Directory.CreateDirectory(_directory);
        using var settingsStore = new DownKyi.Core.Settings.SettingsStore(
            Path.Combine(_directory, "settings.json"));
        var page = CreatePage();
        page.PlaybackAvailability = new PlayUrlAvailability(
        [
            new PlayUrlVideoAvailability(80, 12, PlayUrlStreamKind.Dash, "1080P DASH"),
            new PlayUrlVideoAvailability(80, 7, PlayUrlStreamKind.Durl, "1080P DURL")
        ],
        []);
        var dash = CreateVideoQuality(isDurl: false);
        dash.Quality = 80;
        dash.SelectedVideoCodec = "H.265/HEVC";
        var durl = CreateVideoQuality(isDurl: true);
        durl.Quality = 80;
        durl.SelectedVideoCodec = "H.264/AVC";
        page.VideoQualityList = [dash, durl];
        page.VideoQuality = durl;
        var video = new VideoInfoView { Title = "main", VideoZone = "Technology" };
        var section = new VideoSection { VideoPages = [page] };

        var item = CreateDraft(
            _directory,
            video,
            section,
            sectionCount: 1,
            page,
            page.VideoQuality,
            settingsStore.Current,
            DownloadContentSelection.None with { Video = true });

        Assert.Equal(DownloadMediaKind.Durl, item.DownloadBase.NeedDownloadContent.MediaKind);
        Assert.Equal("H.264/AVC", item.VideoCodecName);
        Assert.Equal(80, item.Resolution.Id);
    }

    public void Dispose()
    {
        if (Directory.Exists(_directory))
        {
            Directory.Delete(_directory, recursive: true);
        }
    }

    private static DownloadingItem CreateDraft(
        string directory,
        VideoInfoView video,
        VideoSection section,
        int sectionCount,
        VideoPage page,
        VideoQuality? videoQuality,
        DownKyi.Core.Settings.ApplicationSettings settings,
        DownloadContentSelection content)
    {
        var audioQualityFormat = page.AudioQualityFormat;
        return DownloadTaskDraftFactory.Create(
            directory,
            video,
            section,
            sectionCount,
            page,
            videoQuality,
            audioQualityFormat,
            DownloadMediaCapabilities.From(
                page.PlaybackAvailability,
                videoQuality,
                audioQualityFormat),
            settings,
            content);
    }

    private static VideoPage CreatePage()
    {
        return new VideoPage
        {
            Avid = 42,
            Bvid = "BV1test",
            Cid = 84,
            IsSelected = true,
            Name = "page",
            Order = 1,
            OriginalPublishTime = new DateTime(2024, 1, 2),
            PublishTime = "2024-01-02",
            PlaybackAvailability = PlayUrlAvailability.From(CreateDashPlayUrl()),
            VideoQuality = CreateVideoQuality()
        };
    }

    private static PlayUrl CreateDashPlayUrl()
    {
        return new PlayUrl
        {
            Dash = new PlayUrlDash
            {
                Video =
                [
                    new PlayUrlDashVideo
                    {
                        Id = 80,
                        CodecId = 7,
                        BaseAddress = "https://media.invalid/video-80"
                    }
                ],
                Audio =
                [
                    new PlayUrlDashVideo
                    {
                        Id = 30280,
                        BaseAddress = "https://media.invalid/audio-30280"
                    }
                ]
            }
        };
    }

    private static PlayUrl CreateDurlPlayUrl()
    {
        return new PlayUrl
        {
            Quality = 80,
            VideoCodecid = 7,
            Durl =
            [
                new PlayUrlDurl
                {
                    Order = 1,
                    SourceAddress = "https://media.invalid/segment-1"
                }
            ]
        };
    }

    private static VideoQuality CreateVideoQuality(bool isDurl = false)
    {
        return new VideoQuality
        {
            Quality = 80,
            QualityFormat = "1080P",
            IsDurl = isDurl,
            SelectedVideoCodec = "H.264/AVC"
        };
    }

    private static DownloadingItem CreateDownloadingItem()
    {
        return new DownloadingItem
        {
            DownloadBase = new DownloadBase
            {
                Id = "duplicate-task",
                Avid = 42,
                Bvid = "BV1test",
                Cid = 84,
                FilePath = Path.Combine(
                    Path.GetDirectoryName(typeof(DownloadAddOwnerTests).Assembly.Location)!,
                    Path.GetFileNameWithoutExtension(typeof(DownloadAddOwnerTests).Assembly.Location)),
                Name = "page",
                Resolution = new DownKyi.Core.BiliApi.BiliUtils.Quality
                {
                    Id = 80,
                    Name = "1080P"
                },
                VideoCodecName = "H.264/AVC"
            },
            Downloading = new Downloading
            {
                DownloadStatus = DownKyi.Models.DownloadStatus.NotStarted,
                PlayStreamType = DownKyi.Core.BiliApi.VideoStream.PlayStreamType.Video
            }
        };
    }

    private static DownloadingItem CreateRequestedItem(
        DownloadContentSelection content,
        VideoPage? page = null,
        VideoQuality? videoQuality = null)
    {
        page ??= CreatePage();
        videoQuality ??= page.VideoQuality;
        var item = CreateDownloadingItem();
        item.DownloadBase.Cid = page.Cid;
        item.DownloadBase.NeedDownloadContent = content;
        item.DownloadBase.Resolution = content.Video && videoQuality != null
            ? new DownKyi.Core.BiliApi.BiliUtils.Quality
            {
                Id = videoQuality.Quality,
                Name = videoQuality.QualityFormat
            }
            : new DownKyi.Core.BiliApi.BiliUtils.Quality();
        item.DownloadBase.VideoCodecName = content.Video
            ? videoQuality?.SelectedVideoCodec ?? string.Empty
            : string.Empty;
        item.DownloadBase.AudioCodec = content.Audio
            ? new DownKyi.Core.BiliApi.BiliUtils.Quality
            {
                Name = page.AudioQualityFormat
            }
            : new DownKyi.Core.BiliApi.BiliUtils.Quality();
        return item;
    }

    private sealed class DuplicatePolicyContext : IDisposable
    {
        private readonly DownloadTaskApplicationService _taskService;
        private readonly DownloadTaskProjectionStore _projectionStore;

        public DuplicatePolicyContext(
            AppDialogOutcome outcome,
            DownloadHistoryRecord? history = null)
        {
            Store = new MutableDownloadTaskStore(null, history);
            var historyService = DownloadHistoryService.CreateForSharedStore(Store);
            _taskService = new DownloadTaskApplicationService(Store, historyService, new SystemClock());
            _projectionStore = new DownloadTaskProjectionStore(
                _taskService,
                historyService,
                new SystemClock());
            ListState = new DownloadListState();
            Dialogs = new StubDialogService(outcome);
            Policy = new DownloadDuplicatePolicy(
                _projectionStore,
                Dialogs);
        }

        public DownloadDuplicatePolicy Policy { get; }

        public DownloadTaskProjectionStore ProjectionStore => _projectionStore;

        public DownloadListState ListState { get; }

        public MutableDownloadTaskStore Store { get; }

        public StubDialogService Dialogs { get; }

        public Task PersistActiveAsync(DownloadingItem item) =>
            _projectionStore.AddDownloadingAsync(
                item,
                TestContext.Current.CancellationToken);

        public static DuplicatePolicyContext WithCompleted(
            AppDialogOutcome outcome)
        {
            return new DuplicatePolicyContext(
                outcome,
                history: CreateCompletedHistory());
        }

        public static DownloadHistoryRecord CreateCompletedHistory()
        {
            var draft = CreateDownloadingItem();
            var queued = DownloadTaskProjectionMapper.CreateNewTask(
                draft,
                DateTimeOffset.UnixEpoch);
            Assert.True(queued.UpdateOutput(
                    new DownloadOutput(queued.Output.BasePath, null, []),
                    DateTimeOffset.UnixEpoch.AddMilliseconds(500))
                .TryGetValue(out var withOutput));
            Assert.True(withOutput.Start(DateTimeOffset.UnixEpoch.AddSeconds(1))
                .TryGetValue(out var started));
            Assert.True(started.Complete(
                new DownloadCompletion(2, "finished", null),
                DateTimeOffset.UnixEpoch.AddSeconds(2))
                .TryGetValue(out var completed));
            return DownloadHistoryRecord.FromCompletedTask(completed);
        }

        public static DownloadHistoryRecord CreateVersionNineHistory()
        {
            var current = CreateCompletedHistory();
            return new DownloadHistoryRecord(
                current.Id,
                current.Cid,
                current.ZoneId,
                current.Order,
                current.MainTitle,
                current.Name,
                current.DurationText,
                current.VideoCodecName,
                current.ResolutionId,
                current.ResolutionName,
                current.AudioCodecName,
                current.FileSizeText,
                [],
                current.FinishedTimestamp,
                current.FinishedTimeText,
                current.MaximumSpeedText);
        }

        public void Dispose()
        {
            _projectionStore.Dispose();
            _taskService.Dispose();
        }
    }

    private sealed class NonPumpingSynchronizationContext : SynchronizationContext
    {
        public int PostCount { get; private set; }

        public override void Post(SendOrPostCallback callback, object? state)
        {
            ArgumentNullException.ThrowIfNull(callback);
            PostCount++;
        }
    }

    private sealed class StubDialogService(AppDialogOutcome outcome) : IAppDialogService
    {
        public int ShowCount { get; private set; }

        public Task<AppDialogResult> ShowAsync(
            AppDialogRequest request,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            ShowCount++;
            return Task.FromResult(new AppDialogResult(
                outcome,
                new Dictionary<string, object?>()));
        }
    }

    private sealed class MutableDownloadTaskStore(
        DownloadTask? current,
        DownloadHistoryRecord? history) :
        IDownloadTaskStore,
        IDownloadHistoryStore,
        IDownloadCompletionStore
    {
        public DownloadTask? Current { get; private set; } = current;

        public DownloadHistoryRecord? History { get; private set; } = history;

        public int UpdateCount { get; private set; }

        public int DeleteHistoryCount { get; private set; }

        public int HistoryPageRequestCount { get; private set; }

        public int HistoryByCidRequestCount { get; private set; }

        public Task<DownloadHistoryPage>? PendingHistoryPage { get; set; }

        public DownloadTaskId? DeletedHistoryId { get; private set; }

        public Task<OperationResult> AddAsync(
            DownloadTask task,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Current = task;
            return Task.FromResult(OperationResult.Success());
        }

        public Task<OperationResult> AddHistoryAsync(
            DownloadHistoryRecord history,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            History = history;
            return Task.FromResult(OperationResult.Success());
        }

        public Task<OperationResult> ClearHistoryAsync(CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            History = null;
            return Task.FromResult(OperationResult.Success());
        }

        public Task<OperationResult> DeleteAsync(
            DownloadTaskId taskId,
            CancellationToken cancellationToken) =>
            Task.FromResult(OperationResult.Success());

        public Task<DownloadTask?> FindAsync(
            DownloadTaskId taskId,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(Current?.Id == taskId ? Current : null);
        }

        public Task<IReadOnlyList<string>> GetActiveOutputReservationKeysAsync(
            bool ignoreCase,
            CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<string>>([]);

        public Task<DownloadHistoryPage> GetHistoryPageAsync(
            DownloadHistoryCursor? cursor,
            int pageSize,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            HistoryPageRequestCount++;
            if (PendingHistoryPage != null)
            {
                return PendingHistoryPage;
            }

            IReadOnlyList<DownloadHistoryRecord> items = History == null
                ? []
                : [History];
            return Task.FromResult(new DownloadHistoryPage(items, null));
        }

        public Task<IReadOnlyList<DownloadHistoryRecord>> GetHistoryByCidAsync(
            long cid,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            HistoryByCidRequestCount++;
            IReadOnlyList<DownloadHistoryRecord> items = History?.Cid == cid
                ? [History]
                : [];
            return Task.FromResult(items);
        }

        public Task<IReadOnlyList<QuarantinedDownloadRecord>> GetQuarantinedRecordsAsync(
            CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<QuarantinedDownloadRecord>>([]);

        public Task<IReadOnlyList<DownloadTask>> GetUnfinishedAsync(
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            IReadOnlyList<DownloadTask> tasks = Current == null ? [] : [Current];
            return Task.FromResult(tasks);
        }

        public Task<IReadOnlyList<DownloadTask>> GetUnfinishedByCidAsync(
            long cid,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            IReadOnlyList<DownloadTask> tasks = Current?.Metadata.Media.Cid == cid
                ? [Current]
                : [];
            return Task.FromResult(tasks);
        }

        public Task<bool> IsOutputPathReservedAsync(
            string basePath,
            bool ignoreCase,
            CancellationToken cancellationToken) => Task.FromResult(false);

        public Task InitializeAsync(CancellationToken cancellationToken) => Task.CompletedTask;

        public Task<OperationResult> UpdateAsync(
            DownloadTask task,
            long expectedVersion,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (Current == null || Current.Version != expectedVersion)
            {
                return Task.FromResult(OperationResult.Failure(new OperationError(
                    "download.store.conflict",
                    "Version mismatch.",
                    OperationErrorKind.Conflict)));
            }

            Current = task;
            UpdateCount++;
            return Task.FromResult(OperationResult.Success());
        }

        public Task<OperationResult> CompleteAsync(
            DownloadTask task,
            DownloadHistoryRecord history,
            long expectedVersion,
            CancellationToken cancellationToken)
        {
            Current = null;
            History = history;
            UpdateCount++;
            return Task.FromResult(OperationResult.Success());
        }

        public Task<OperationResult> DeleteHistoryAsync(
            DownloadTaskId taskId,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            DeletedHistoryId = taskId;
            DeleteHistoryCount++;
            if (History?.Id == taskId)
            {
                History = null;
            }

            return Task.FromResult(OperationResult.Success());
        }

        public Task<OperationResult> UpdateProgressAsync(
            DownloadProgressWrite progressWrite,
            CancellationToken cancellationToken) =>
            Task.FromResult(OperationResult.Success());
    }
}
