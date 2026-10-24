using DownKyi.Core.BiliApi.Sign;
using DownKyi.Core.BiliApi.Users;

namespace DownKyi.Core.Tests;

public sealed class UserSpaceLoginStateTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task SpaceQueriesUseTheSuppliedLoginState(bool hasSavedLoginInfo)
    {
        var requests = new List<string>();
        var client = new StubBilibiliApiClient((request, cancellationToken) =>
        {
            cancellationToken.ThrowIfCancellationRequested();
            requests.Add(request.RequestAddress);
            return Task.FromResult("""
                {"code":0,"data":{"list":{"tlist":{},"vlist":[]},"page":{"count":0}}}
                """);
        });
        var keys = new WbiKeys(
            "7cd084941338484aae1ad9425b84077c",
            "4932caff0ff746eab6f01bf08b70ac45");
        var cancellationToken = TestContext.Current.CancellationToken;

        await client.GetUserInfoForSpaceAsync(keys: keys,
            unixTimeSeconds: 1, mid: 42, hasSavedLoginInfo: hasSavedLoginInfo,
            cancellationToken: cancellationToken);
        await client.GetPublicationPageAsync(keys, 1, 42, 1, 20, hasSavedLoginInfo,
            cancellationToken: cancellationToken);
        await client.GetPublicationTypeAsync(keys, 1, 42, hasSavedLoginInfo, cancellationToken);

        Assert.Equal(3, requests.Count);
        Assert.All(requests, request =>
        {
            Assert.Equal(!hasSavedLoginInfo, request.Contains("dm_img_str=", StringComparison.Ordinal));
            Assert.Equal(!hasSavedLoginInfo, request.Contains("dm_img_list=", StringComparison.Ordinal));
            Assert.Equal(!hasSavedLoginInfo, request.Contains("dm_cover_img_str=", StringComparison.Ordinal));
            Assert.Equal(!hasSavedLoginInfo, request.Contains("dm_img_inter=", StringComparison.Ordinal));
            Assert.Contains("w_rid=", request, StringComparison.Ordinal);
        });
    }
}
