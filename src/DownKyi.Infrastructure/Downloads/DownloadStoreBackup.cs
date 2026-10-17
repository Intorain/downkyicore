using System.Globalization;
using DownKyi.Application.Time;
using Microsoft.Data.Sqlite;

namespace DownKyi.Infrastructure.Downloads;

internal static class DownloadStoreBackup
{
    public static async Task CreateAsync(
        SqliteConnection source,
        string databasePath,
        IClock clock,
        CancellationToken cancellationToken)
    {
        var backupDirectory = Path.Combine(Path.GetDirectoryName(databasePath) ?? ".", "Backup");
        Directory.CreateDirectory(backupDirectory);
        var timestamp = clock.UtcNow.ToString("yyyyMMdd'T'HHmmssfff'Z'", CultureInfo.InvariantCulture);
        var backupPath = Path.Combine(
            backupDirectory,
            $"{Path.GetFileName(databasePath)}.schema-{timestamp}.bak");
        var connectionString = new SqliteConnectionStringBuilder
        {
            DataSource = backupPath,
            Mode = SqliteOpenMode.ReadWriteCreate,
            Pooling = false
        }.ToString();

        using var backup = new SqliteConnection(connectionString);
        await backup.OpenAsync(cancellationToken).ConfigureAwait(false);
        source.BackupDatabase(backup);
    }
}
