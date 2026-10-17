using DownKyi.Domain.Downloads;

namespace DownKyi.Application.Downloads;

public sealed record DownloadAddSelection
{
    public DownloadAddSelection(
        string directory,
        DownloadContentSelection requestedContent,
        bool redownloadCompleted = false)
    {
        ArgumentException.ThrowIfNullOrEmpty(directory);
        ArgumentNullException.ThrowIfNull(requestedContent);

        Directory = directory;
        RequestedContent = requestedContent;
        RedownloadCompleted = redownloadCompleted;
    }

    public string Directory { get; }

    public DownloadContentSelection RequestedContent { get; }

    public bool RedownloadCompleted { get; }
}
