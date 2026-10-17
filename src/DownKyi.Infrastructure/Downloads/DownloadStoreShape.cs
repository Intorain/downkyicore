namespace DownKyi.Infrastructure.Downloads;

internal sealed record DownloadStoreShape(
    IReadOnlyDictionary<string, HashSet<string>> Tables,
    bool IsNew,
    bool IsCurrent)
{
    public bool HasTable(string table) => Tables.ContainsKey(table);

    public bool HasColumn(string table, string column) =>
        Tables.TryGetValue(table, out var columns) && columns.Contains(column);
}
