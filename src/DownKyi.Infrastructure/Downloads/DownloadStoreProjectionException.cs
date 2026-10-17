namespace DownKyi.Infrastructure.Downloads;

public sealed class DownloadStoreProjectionException : InvalidOperationException
{
    public DownloadStoreProjectionException()
    {
    }

    public DownloadStoreProjectionException(string message)
        : base(message)
    {
    }

    public DownloadStoreProjectionException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
