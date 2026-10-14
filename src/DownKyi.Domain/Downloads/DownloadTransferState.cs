using System.Collections.Immutable;

namespace DownKyi.Domain.Downloads;

public sealed class DownloadTransferState
{
    public DownloadTransferState(
        string? backendIdentity,
        IEnumerable<string> completedFileKeys,
        string? activeContent = null,
        string? statusText = null,
        long maximumBytesPerSecond = 0,
        bool isLegacyTransfer = false)
    {
        ArgumentNullException.ThrowIfNull(completedFileKeys);
        ArgumentOutOfRangeException.ThrowIfNegative(maximumBytesPerSecond);

        BackendIdentity = backendIdentity;
        CompletedFileKeys = completedFileKeys.ToImmutableArray();
        ActiveContent = activeContent;
        StatusText = statusText;
        MaximumBytesPerSecond = maximumBytesPerSecond;
        IsLegacyTransfer = isLegacyTransfer;
    }

    public static DownloadTransferState Empty { get; } = new(null, []);

    public static DownloadTransferState LegacyEmpty { get; } = new(null, [], isLegacyTransfer: true);

    public string? BackendIdentity { get; }

    public ImmutableArray<string> CompletedFileKeys { get; }

    public string? ActiveContent { get; }

    public string? StatusText { get; }

    public long MaximumBytesPerSecond { get; }

    public bool IsLegacyTransfer { get; }
}
