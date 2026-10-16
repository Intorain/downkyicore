using DownKyi.Domain.Downloads;
using DownKyi.Domain.Results;

namespace DownKyi.Application.Downloads;

public interface IDownloadTaskCatalog : IDownloadTaskLookup
{
    event EventHandler<DownloadTaskChangedEventArgs>? TaskChanged;

    Task<OperationResult<DownloadTask>> AddAsync(
        DownloadTask task,
        CancellationToken cancellationToken);
}
