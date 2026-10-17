using DownKyi.Application.Downloads;
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
        IPhysicalOutputPathResolver physicalOutputPathResolver,
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
        if (!shape.IsNew && !HasRequiredProjectionInputs(shape))
        {
            throw new DownloadStoreProjectionException(
                "The download database is missing columns required to preserve its records.");
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
                    physicalOutputPathResolver,
                    cancellationToken)
                .ConfigureAwait(false);
        }

        await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
    }

    private static bool HasRequiredProjectionInputs(DownloadStoreShape shape) =>
        DownloadStoreTargetSchema.RequiredInputColumns.All(table =>
            table.Key is not ("downloaded" or "download_history")
                ? shape.HasColumns(table.Key, table.Value)
                : !shape.HasTable(table.Key) || shape.HasColumns(table.Key, table.Value));
}
