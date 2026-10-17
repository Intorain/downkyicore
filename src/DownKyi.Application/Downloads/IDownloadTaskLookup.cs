using DownKyi.Domain.Downloads;

namespace DownKyi.Application.Downloads;

public interface IDownloadTaskLookup
{
    Task<DownloadTask?> FindAsync(
        DownloadTaskId taskId,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<DownloadTask>> GetUnfinishedAsync(CancellationToken cancellationToken);

    Task<IReadOnlyList<DownloadTask>> GetUnfinishedByCidAsync(
        long cid,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<DownloadTask>> GetUnfinishedByCidsAsync(
        IReadOnlyCollection<long> cids,
        CancellationToken cancellationToken);
}
