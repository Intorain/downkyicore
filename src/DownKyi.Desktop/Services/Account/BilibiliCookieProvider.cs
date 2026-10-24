using DownKyi.Application.Bilibili;
using DownKyi.Infrastructure.Bilibili;

namespace DownKyi.Services.Account;

internal sealed class BilibiliCookieProvider : IBilibiliCookieProvider
{
    public string GetCookieHeader()
    {
        return LoginHelper.GetLoginInfoCookiesString();
    }
}
