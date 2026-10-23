using DownKyi.Core.BiliApi;
using DownKyi.Core.BiliApi.Users.Models;
using DownKyi.Services.UserSpace;
using Microsoft.Extensions.Logging.Abstractions;

namespace DownKyi.Tests;

public sealed class UserSpacePageCoordinatorTests
{
    [Fact]
    public async Task PreCanceledPublicationLoadDoesNotStartApiWork()
    {
        using var settings = new TestSettingsStore();
        var coordinator = CreateCoordinator(settings);
        using var cancellation = new CancellationTokenSource();
        await cancellation.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => coordinator.LoadPublicationPageAsync(
            42,
            1,
            30,
            0,
            null,
            cancellation.Token));
    }

    [Fact]
    public void PublicationMappingPreservesServerTotalAndMediaIdentity()
    {
        var publication = new SpacePublication
        {
            Page = new SpacePublicationPage { Count = 35, Pn = 2, Ps = 30 },
            List = new SpacePublicationList
            {
                Vlist =
                [
                    new SpacePublicationListVideo
                    {
                        Aid = 100,
                        Bvid = "BV1fixture01",
                        Title = "fixture",
                        Length = "01:30",
                        Created = 1_700_000_000,
                        Play = 12,
                        Pic = "cover"
                    }
                ]
            }
        };

        var result = UserSpacePageCoordinator.MapPublicationPage(
            publication,
            new TestNavigationService(),
            NullLogger.Instance,
            CancellationToken.None);

        Assert.Equal(35, result.TotalCount);
        Assert.Equal("BV1fixture01", Assert.Single(result.Medias).Bvid);
    }

    [Fact]
    public async Task PreCanceledProfileLoadDoesNotStartApiWork()
    {
        using var settings = new TestSettingsStore();
        var coordinator = CreateCoordinator(settings);
        using var cancellation = new CancellationTokenSource();
        await cancellation.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => coordinator.LoadMyProfileAsync(42, cancellation.Token));
    }

    [Fact]
    public async Task ProfileUsesWbiTopPhotoWithoutRequestingLegacySettings()
    {
        const string topPhoto = "http://i0.hdslb.com/bfs/space/header.png";
        var requests = new List<string>();
        var client = new TestBilibiliApiClient
        {
            GetStringAsyncHandler = (request, _) =>
            {
                requests.Add(request.RequestAddress);
                var response = request.RequestAddress switch
                {
                    var address when address.Contains("/x/space/wbi/acc/info?", StringComparison.Ordinal) =>
                        $$$"""{"code":0,"data":{"mid":42,"top_photo":"{{{topPhoto}}}"}}""",
                    var address when address.Contains("/x/space/myinfo", StringComparison.Ordinal) =>
                        """{"code":0,"data":{"mid":42,"name":"fixture"}}""",
                    _ => throw new InvalidOperationException(
                        $"Unexpected Bilibili request: {request.RequestAddress}")
                };
                return Task.FromResult(response);
            }
        };
        using var settings = new TestSettingsStore();
        var coordinator = CreateCoordinator(settings, client);

        var profile = await coordinator.LoadMyProfileAsync(42, CancellationToken.None);

        Assert.Equal(
            "https://i0.hdslb.com/bfs/space/header.png",
            Assert.IsType<MySpaceProfileSnapshot>(profile).Background);
        Assert.DoesNotContain(
            requests,
            address => address.Contains("/ajax/settings/getSettings", StringComparison.Ordinal));
    }

    [Fact]
    public async Task ProfileBackgroundFailureUsesFallbackWithoutHidingProfile()
    {
        var requests = new List<string>();
        var client = new TestBilibiliApiClient
        {
            GetStringAsyncHandler = (request, _) =>
            {
                requests.Add(request.RequestAddress);
                return request.RequestAddress switch
                {
                    var address when address.Contains("/x/space/myinfo", StringComparison.Ordinal) =>
                        Task.FromResult("""{"code":0,"data":{"mid":42,"name":"fixture"}}"""),
                    var address when address.Contains("/x/space/wbi/acc/info?", StringComparison.Ordinal) =>
                        Task.FromException<string>(new BilibiliApiResponseException("header unavailable")),
                    _ => Task.FromException<string>(new InvalidOperationException(
                        $"Unexpected Bilibili request: {request.RequestAddress}"))
                };
            }
        };
        using var settings = new TestSettingsStore();
        var coordinator = CreateCoordinator(settings, client);

        var profile = await coordinator.LoadMyProfileAsync(42, CancellationToken.None);

        var snapshot = Assert.IsType<MySpaceProfileSnapshot>(profile);
        Assert.Equal("fixture", snapshot.UserName);
        Assert.Equal(UserSpaceBackground.FallbackAddress, snapshot.Background);
        Assert.Contains("/x/space/myinfo", requests[0], StringComparison.Ordinal);
    }

    [Fact]
    public void UnsupportedBackgroundAddressUsesLocalFallback()
    {
        Assert.Equal(
            UserSpaceBackground.FallbackAddress,
            UserSpaceBackground.Resolve("file://server/share/header.png"));
    }

    [Fact]
    public async Task PreCanceledStatsLoadDoesNotStartApiWork()
    {
        using var settings = new TestSettingsStore();
        var coordinator = CreateCoordinator(settings);
        using var cancellation = new CancellationTokenSource();
        await cancellation.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => coordinator.LoadMyStatsAsync(42, cancellation.Token));
    }

    [Fact]
    public async Task PreCanceledBangumiLoadDoesNotStartApiWork()
    {
        using var settings = new TestSettingsStore();
        var coordinator = CreateCoordinator(settings);
        using var cancellation = new CancellationTokenSource();
        await cancellation.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => coordinator.LoadBangumiFollowPageAsync(
            42,
            BangumiType.ANIME,
            1,
            15,
            cancellation.Token));
    }

    private static UserSpacePageCoordinator CreateCoordinator(
        TestSettingsStore settings,
        TestBilibiliApiClient? client = null)
    {
        return new UserSpacePageCoordinator(
            settings.Store,
            new TestWbiKeyProvider(),
            new TestNavigationService(),
            NullLogger<UserSpacePageCoordinator>.Instance,
            client ?? new TestBilibiliApiClient());
    }
}
