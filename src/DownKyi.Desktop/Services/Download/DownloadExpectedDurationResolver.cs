using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using DownKyi.Domain.Downloads;

namespace DownKyi.Services.Download;

internal static class DownloadExpectedDurationResolver
{
    public static TimeSpan Resolve(DownloadExecutionContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        if (context.MediaKind == DownloadMediaKind.Dash && context.PlayUrl?.Dash.Duration > 0)
        {
            return CreateDurationFromSeconds(context.PlayUrl.Dash.Duration);
        }

        if (context.MediaKind == DownloadMediaKind.Durl)
        {
            var playUrlDuration = SumDurlDuration(
                context.PlayUrl?.Durl.Select(item => item.Length));
            if (playUrlDuration > TimeSpan.Zero)
            {
                return playUrlDuration;
            }

            var downloadedDuration = SumDurlDuration(
                context.DurlDownloads.Select(item => item.Durl.Length));
            if (downloadedDuration > TimeSpan.Zero)
            {
                return downloadedDuration;
            }
        }

        return ParseDurationText(context.Input.Metadata.DurationText);
    }

    private static TimeSpan SumDurlDuration(IEnumerable<long>? lengths)
    {
        if (lengths == null)
        {
            return TimeSpan.Zero;
        }

        long totalMilliseconds = 0;
        try
        {
            checked
            {
                foreach (var length in lengths)
                {
                    if (length <= 0)
                    {
                        return TimeSpan.Zero;
                    }

                    totalMilliseconds += length;
                }
            }
        }
        catch (OverflowException)
        {
            return TimeSpan.Zero;
        }

        return totalMilliseconds > 0 && totalMilliseconds <= TimeSpan.MaxValue.TotalMilliseconds
            ? TimeSpan.FromMilliseconds(totalMilliseconds)
            : TimeSpan.Zero;
    }

    private static TimeSpan ParseDurationText(string durationText)
    {
        if (string.IsNullOrWhiteSpace(durationText))
        {
            return TimeSpan.Zero;
        }

        var trimmedDuration = durationText.Trim();
        if (trimmedDuration.Contains(':', StringComparison.Ordinal))
        {
            return ParseColonDuration(trimmedDuration);
        }

        long totalSeconds = 0;
        long component = 0;
        var hasDigits = false;
        try
        {
            checked
            {
                foreach (var character in trimmedDuration)
                {
                    if (char.IsAsciiDigit(character))
                    {
                        component = component * 10 + character - '0';
                        hasDigits = true;
                        continue;
                    }

                    if (!hasDigits)
                    {
                        return TimeSpan.Zero;
                    }

                    var multiplier = character switch
                    {
                        'h' => 60L * 60L,
                        'm' => 60L,
                        's' => 1L,
                        _ => 0L
                    };
                    if (multiplier == 0)
                    {
                        return TimeSpan.Zero;
                    }

                    totalSeconds += component * multiplier;
                    component = 0;
                    hasDigits = false;
                }
            }
        }
        catch (OverflowException)
        {
            return TimeSpan.Zero;
        }

        return !hasDigits
            ? CreateDurationFromSeconds(totalSeconds)
            : TimeSpan.Zero;
    }

    private static TimeSpan ParseColonDuration(string durationText)
    {
        var components = durationText.Split(':');
        if (components.Length is < 2 or > 3 ||
            !long.TryParse(
                components[^2],
                NumberStyles.None,
                CultureInfo.InvariantCulture,
                out var minutes) ||
            !long.TryParse(
                components[^1],
                NumberStyles.None,
                CultureInfo.InvariantCulture,
                out var seconds))
        {
            return TimeSpan.Zero;
        }

        long hours = 0;
        if (components.Length == 3 &&
            !long.TryParse(
                components[0],
                NumberStyles.None,
                CultureInfo.InvariantCulture,
                out hours))
        {
            return TimeSpan.Zero;
        }

        if (minutes >= 60 || seconds >= 60)
        {
            return TimeSpan.Zero;
        }

        try
        {
            checked
            {
                return CreateDurationFromSeconds(hours * 60L * 60L + minutes * 60L + seconds);
            }
        }
        catch (OverflowException)
        {
            return TimeSpan.Zero;
        }
    }

    private static TimeSpan CreateDurationFromSeconds(long seconds) =>
        seconds > 0 && seconds <= TimeSpan.MaxValue.TotalSeconds
            ? TimeSpan.FromSeconds(seconds)
            : TimeSpan.Zero;
}
