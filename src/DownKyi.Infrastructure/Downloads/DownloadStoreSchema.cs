using DownKyi.Application.Time;
using Microsoft.Data.Sqlite;

namespace DownKyi.Infrastructure.Downloads;

internal static class DownloadStoreSchema
{
    public static async Task InitializeAsync(
        SqliteConnection connection,
        string databasePath,
        bool databaseExisted,
        IClock clock,
        CancellationToken cancellationToken)
    {
        var shape = await DownloadStoreShapeReader
            .ReadAsync(connection, cancellationToken)
            .ConfigureAwait(false);
        if (!shape.IsCurrent && !shape.IsNew && databaseExisted)
        {
            await DownloadStoreBackup
                .CreateAsync(connection, databasePath, clock, cancellationToken)
                .ConfigureAwait(false);
        }

        using var transaction = (SqliteTransaction)await connection
            .BeginTransactionAsync(cancellationToken)
            .ConfigureAwait(false);
        if (shape.IsNew)
        {
            await CurrentDownloadStoreWriter
                .CreateAsync(connection, transaction, cancellationToken)
                .ConfigureAwait(false);
        }
        else
        {
            await CurrentDownloadStoreWriter
                .ProjectAsync(
                    connection,
                    transaction,
                    shape,
                    cancellationToken)
                .ConfigureAwait(false);
        }

        await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
    }
}
