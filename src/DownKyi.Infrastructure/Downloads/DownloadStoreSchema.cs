using DownKyi.Application.Downloads;
using DownKyi.Application.Time;
using Microsoft.Data.Sqlite;

namespace DownKyi.Infrastructure.Downloads;

internal static class DownloadStoreSchema
{
    // Historical versions are immutable feature identities. Never infer them from
    // CurrentVersion - N because adding a new version would reclassify every older store.
    internal const int SchemaMetadataVersion = 1;
    internal const int StatefulDownloadsVersion = 2;
    internal const int OutputReservationVersion = 3;
    internal const int AdmissionGateVersion = 4;
    internal const int NfoRequestVersion = 5;
    internal const int PublishedArtifactsVersion = 6;
    internal const int StagingTokenVersion = 7;
    internal const int PublishingArtifactVersion = 8;
    internal const int HistoryProjectionVersion = 9;
    internal const int HistoryContentVersion = 10;
    internal const int ActionClaimReservationVersion = 11;
    internal const int LegacyTransferMarkerVersion = 12;

    public const int CurrentVersion = LegacyTransferMarkerVersion;

    public static async Task InitializeAsync(
        SqliteConnection connection,
        string databasePath,
        bool databaseExisted,
        IClock clock,
        IPhysicalOutputPathResolver physicalOutputPathResolver,
        CancellationToken cancellationToken)
    {
        var format = await LegacyDownloadStoreFormatDetector
            .DetectAsync(connection, databaseExisted, cancellationToken)
            .ConfigureAwait(false);
        if (format.IsCurrent)
        {
            return;
        }

        if (format.DatabaseExisted)
        {
            await DownloadStoreSchemaLifecycle
                .BackupAsync(connection, databasePath, format.UserVersion, clock, cancellationToken)
                .ConfigureAwait(false);
        }

        using var transaction = (SqliteTransaction)await connection
            .BeginTransactionAsync(cancellationToken)
            .ConfigureAwait(false);
        try
        {
            if (format.IsNew)
            {
                await CurrentDownloadStoreWriter
                    .CreateAsync(connection, transaction, clock.UtcNow, cancellationToken)
                    .ConfigureAwait(false);
            }
            else if (format.CanUpgradeCurrentShapeInPlace)
            {
                await CurrentDownloadStoreWriter
                    .UpgradeCurrentShapeAsync(
                        connection,
                        transaction,
                        format,
                        clock.UtcNow,
                        cancellationToken)
                    .ConfigureAwait(false);
            }
            else
            {
                var snapshot = await LegacyDownloadStoreReader
                    .ReadAsync(connection, transaction, format, cancellationToken)
                    .ConfigureAwait(false);
                var plan = LegacyDownloadStoreNormalizer.Normalize(
                    snapshot,
                    physicalOutputPathResolver);
                if (format.RequiresLegacyHistoryRecovery)
                {
                    if (!format.HasHistoryContent)
                    {
                        await CurrentDownloadStoreWriter
                            .AddHistoryContentColumnAsync(connection, transaction, cancellationToken)
                            .ConfigureAwait(false);
                    }
                    await CurrentDownloadStoreWriter
                        .RecoverCurrentWithLegacyHistoryAsync(
                            connection,
                            transaction,
                            format,
                            plan,
                            clock.UtcNow,
                            cancellationToken)
                        .ConfigureAwait(false);
                }
                else
                {
                    await CurrentDownloadStoreWriter
                        .UpgradeAsync(
                            connection,
                            transaction,
                            format,
                            plan,
                            clock.UtcNow,
                            cancellationToken)
                        .ConfigureAwait(false);
                }
            }

            await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
        }
        catch
        {
            await transaction.RollbackAsync(CancellationToken.None).ConfigureAwait(false);
            throw;
        }
    }
}
