using Microsoft.Data.Sqlite;

namespace DownKyi.Infrastructure.Downloads;

internal static class DownloadStoreShapeReader
{
    public static async Task<DownloadStoreShape> ReadAsync(
        SqliteConnection connection,
        CancellationToken cancellationToken)
    {
        var tables = new Dictionary<string, HashSet<string>>(StringComparer.Ordinal);
        using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT schema.name, columns.name
            FROM sqlite_master schema
            INNER JOIN pragma_table_info(schema.name) columns
            WHERE schema.type = 'table'
            ORDER BY schema.name, columns.cid
            """;
        using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            var table = reader.GetString(0);
            if (!tables.TryGetValue(table, out var columns))
            {
                columns = new HashSet<string>(StringComparer.Ordinal);
                tables.Add(table, columns);
            }

            columns.Add(reader.GetString(1));
        }

        if (tables.Count == 0)
        {
            return new DownloadStoreShape(tables, IsNew: true, IsCurrent: false);
        }

        var isCurrent = !tables.ContainsKey("downloaded")
                        && DownloadStoreTargetSchema.CurrentColumns.All(table =>
                            HasColumns(tables, table.Key, table.Value))
                        && DownloadStoreTargetSchema.SupportTables.All(tables.ContainsKey);
        return new DownloadStoreShape(tables, IsNew: false, isCurrent);
    }

    private static bool HasColumns(
        Dictionary<string, HashSet<string>> tables,
        string table,
        IEnumerable<string> requiredColumns) =>
        tables.TryGetValue(table, out var columns)
        && requiredColumns.All(columns.Contains);
}
