using DownKyi.Infrastructure.Downloads;

namespace DownKyi.Architecture.Tests;

public sealed class DownloadStoreSchemaArchitectureTests
{
    private static readonly string RepositoryRoot = FindRepositoryRoot();

    [Fact]
    public void CoordinatorOwnsAtomicShapeProjectionWithoutVersionBranchesOrSqlRules()
    {
        var source = ReadDownloadSource("DownloadStoreSchema.cs");

        Assert.Contains("DownloadStoreShapeReader", source, StringComparison.Ordinal);
        Assert.Contains("CurrentDownloadStoreWriter", source, StringComparison.Ordinal);
        Assert.Contains("BeginTransactionAsync", source, StringComparison.Ordinal);
        Assert.Contains("CommitAsync", source, StringComparison.Ordinal);
        Assert.Contains("DownloadStoreBackup", source, StringComparison.Ordinal);
        Assert.DoesNotContain("catch", source, StringComparison.Ordinal);
        Assert.DoesNotContain("CommandText", source, StringComparison.Ordinal);
        Assert.DoesNotContain("CREATE TABLE", source, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("ALTER TABLE", source, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Legacy", source, StringComparison.Ordinal);
        Assert.DoesNotContain("CurrentVersion", source, StringComparison.Ordinal);
        Assert.DoesNotContain("user_version", source, StringComparison.Ordinal);
        Assert.Contains("HasRequiredProjectionInputs", source, StringComparison.Ordinal);
        Assert.DoesNotContain("IPhysicalOutputPathResolver", source, StringComparison.Ordinal);
    }

    [Fact]
    public void ShapeReaderOnlyComparesTablesAndColumns()
    {
        var source = ReadDownloadSource("DownloadStoreShapeReader.cs");

        Assert.Contains("sqlite_master", source, StringComparison.Ordinal);
        Assert.Contains("pragma_table_info", source, StringComparison.Ordinal);
        Assert.Contains("HasColumns", source, StringComparison.Ordinal);
        Assert.DoesNotContain("SchemaDifferences", source, StringComparison.Ordinal);
        Assert.DoesNotContain("CurrentVersion", source, StringComparison.Ordinal);
        Assert.DoesNotContain("ReadUserVersionAsync", source, StringComparison.Ordinal);
        Assert.DoesNotContain("LegacyDownloadStoreKind", source, StringComparison.Ordinal);
        Assert.DoesNotContain("ALTER TABLE", source, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("CREATE TABLE", source, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("UPDATE download", source, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("INSERT INTO", source, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("IPhysicalOutputPathResolver", source, StringComparison.Ordinal);
    }

    [Fact]
    public void CurrentWriterProjectsMissingColumnsWithoutInterpretingLegacyRows()
    {
        var source = ReadDownloadSource("CurrentDownloadStoreWriter.cs");

        Assert.Contains("CREATE TABLE download_base", source, StringComparison.Ordinal);
        Assert.Contains("CREATE TABLE download_history", source, StringComparison.Ordinal);
        Assert.Contains("ALTER TABLE download_base ADD COLUMN", source, StringComparison.Ordinal);
        Assert.Contains("ALTER TABLE downloading ADD COLUMN", source, StringComparison.Ordinal);
        Assert.Contains("shape.HasColumn", source, StringComparison.Ordinal);
        Assert.Contains("ProjectHistoryAsync", source, StringComparison.Ordinal);
        Assert.Contains("AlignTablesAsync", source, StringComparison.Ordinal);
        Assert.Contains(
            "SET staging_token = lower(hex(randomblob(16)))",
            source,
            StringComparison.Ordinal);
        Assert.DoesNotContain("SuppressMessage", source, StringComparison.Ordinal);
        Assert.DoesNotContain("UserVersion", source, StringComparison.Ordinal);
        Assert.DoesNotContain("user_version", source, StringComparison.Ordinal);
        Assert.DoesNotContain("DownloadStoreSchemaV", source, StringComparison.Ordinal);
        Assert.DoesNotContain("IPhysicalOutputPathResolver", source, StringComparison.Ordinal);
        Assert.DoesNotContain("MapLegacyStatus", source, StringComparison.Ordinal);
        Assert.DoesNotContain("DownloadOutputPathKey", source, StringComparison.Ordinal);
        Assert.DoesNotContain("SET source_table = 'downloading'", source, StringComparison.Ordinal);
    }

    [Fact]
    public void HistoricalVersionMigrationOwnersAreRemoved()
    {
        var owners = typeof(SqliteDownloadTaskStoreOptions)
            .Assembly
            .GetTypes()
            .Where(type => type.Namespace == "DownKyi.Infrastructure.Downloads")
            .Select(type => type.Name)
            .Where(name => name.StartsWith("DownloadStoreSchemaV", StringComparison.Ordinal))
            .ToArray();

        Assert.Empty(owners);
    }

    private static string ReadDownloadSource(string fileName)
    {
        return File.ReadAllText(Path.Combine(
            RepositoryRoot,
            "src",
            "DownKyi.Infrastructure",
            "Downloads",
            fileName));
    }

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory != null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "DownKyi.sln")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new DirectoryNotFoundException("Could not locate the repository root.");
    }
}
