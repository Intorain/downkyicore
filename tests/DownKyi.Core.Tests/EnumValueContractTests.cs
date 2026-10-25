using DownKyi.Application.FileName;
using DownKyi.Application.Settings;
using DownKyi.Core.BiliApi.History.Models;
using DownKyi.Core.BiliApi.Users;
using DownKyi.Core.BiliApi.Users.Models;
using DownKyi.Core.BiliApi.VideoStream;
using DownKyi.Core.FileName;

namespace DownKyi.Core.Tests;

public sealed class EnumValueContractTests
{
    [Fact]
    public void NoneMembersDoNotShiftPersistedOrProtocolValues()
    {
        Assert.Equal(0, (int)HistoryBusiness.None);
        Assert.Equal(1, (int)HistoryBusiness.ARCHIVE);
        Assert.Equal(4, (int)HistoryBusiness.ArticleList);
        Assert.Equal(0, (int)BangumiType.None);
        Assert.Equal(1, (int)BangumiType.ANIME);
        Assert.Equal(0, (int)FollowingOrder.None);
        Assert.Equal(1, (int)FollowingOrder.DEFAULT);
        Assert.Equal(0, (int)PublicationOrder.None);
        Assert.Equal(1, (int)PublicationOrder.PUBDATE);
        Assert.Equal(0, (int)PlayStreamType.None);
        Assert.Equal(1, (int)PlayStreamType.Video);
        Assert.Equal(0, (int)FileNamePart.None);
        Assert.Equal(1, (int)FileNamePart.Order);
        Assert.Equal(100, (int)FileNamePart.Slash);
        Assert.Equal(0, (int)ThemeMode.None);
        Assert.Equal(1, (int)ThemeMode.Default);
    }

    [Fact]
    public void EnumWireMappingsRemainExplicitAndLowercase()
    {
        Assert.Equal("pubdate", UserSpace.GetPublicationOrderValue(PublicationOrder.PUBDATE));
        Assert.Equal("click", UserSpace.GetPublicationOrderValue(PublicationOrder.CLICK));
    }
}
