using System;
using System.Collections.Generic;

namespace DownKyi.Services.Download;

internal static class DownloadRequestHeaderPolicy
{
    private const string BilibiliHost = "bilibili.com";

    public static DownloadRequestHeaders Create(
        string url,
        string userAgent,
        string? credentials)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(url);
        ValidateValue(userAgent, nameof(userAgent));
        var headers = new List<string>
        {
            "Origin: https://www.bilibili.com",
            "Referer: https://www.bilibili.com"
        };
        var carriesCredentials = IsCredentialEligibleUrl(url);
        if (carriesCredentials)
        {
            if (!string.IsNullOrWhiteSpace(credentials))
            {
                ValidateValue(credentials, "credentials");
                headers.Add($"Cookie: {credentials}");
            }
            else
            {
                carriesCredentials = false;
            }
        }

        return new DownloadRequestHeaders(
            headers,
            userAgent,
            carriesCredentials);
    }

    internal static bool IsCredentialEligibleUrl(string url)
    {
        return Uri.TryCreate(url, UriKind.Absolute, out var uri)
               && string.Equals(uri.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase)
               && (string.Equals(uri.IdnHost, BilibiliHost, StringComparison.OrdinalIgnoreCase)
                   || uri.IdnHost.EndsWith(
                       "." + BilibiliHost,
                       StringComparison.OrdinalIgnoreCase));
    }

    private static void ValidateValue(string value, string parameterName)
    {
        if (value.Any(character => character is '\r' or '\n' or '\0'
            || char.IsControl(character)))
        {
            throw new ArgumentException(
                "A download request header contains a control character.",
                parameterName);
        }
    }
}

internal sealed record DownloadRequestHeaders(
    IReadOnlyList<string> Headers,
    string UserAgent,
    bool CarriesCredentials);
