using DownKyi.Domain.Downloads;
using DownKyi.Domain.Results;

namespace DownKyi.Application.Downloads;

public interface IDownloadTaskSchedulerService
{
    Task<DownloadTask?> FindAsync(
        DownloadTaskId taskId,
        CancellationToken cancellationToken);

    Task<OperationResult<DownloadTask>> PauseAsync(
        DownloadTaskId taskId,
        CancellationToken cancellationToken);

    Task<OperationResult<DownloadTask>> ResumeAsync(
        DownloadTaskId taskId,
        CancellationToken cancellationToken);
}
