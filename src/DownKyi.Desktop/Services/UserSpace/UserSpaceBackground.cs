using DownKyi.Application.Bilibili;

namespace DownKyi.Services.UserSpace;

internal static class UserSpaceBackground
{
    internal const string FallbackAddress =
        "avares://DownKyi.Desktop/Resources/backgound/9-绿荫秘境.png";

    internal static string Resolve(string? address) =>
        BilibiliResourceAddress.TryNormalizeHttps(address, out var normalizedAddress)
            ? normalizedAddress
            : FallbackAddress;
}
