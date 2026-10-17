using System.Diagnostics.CodeAnalysis;
using DownKyi.Infrastructure.Downloads;
using Microsoft.Data.Sqlite;

namespace DownKyi.Infrastructure.Tests;

public sealed class DownloadStoreShapeReaderTests
{
    private static readonly string[] CoreBaseColumns =
    [
        "id", "need_download_content", "bvid", "avid", "cid", "episode_id", "cover_url",
        "page_cover_url", "zone_id", "order", "main_title", "name", "duration",
        "video_codec_name", "resolution", "audio_codec", "file_path", "file_size", "page"
    ];

    private static readonly string[] CurrentBaseColumns =
    [
        .. CoreBaseColumns,
        "version", "created_at_utc", "updated_at_utc", "output_reservation_key", "nfo_request",
        "published_artifacts", "staging_token", "publishing_key", "publishing_file_name",
        "publishing_length", "publishing_sha256"
    ];

    private static readonly string[] CoreDownloadingColumns =
    [
        "id", "gid", "download_files", "downloaded_files", "play_stream_type", "download_status",
        "download_content", "download_status_title", "progress", "downloading_file_size", "max_speed",
        "speed_display"
    ];

    private static readonly string[] CurrentDownloadingColumns =
    [
        .. CoreDownloadingColumns,
        "phase", "failure_code", "failure_message", "failure_transient", "downloaded_bytes",
        "total_bytes", "bytes_per_second", "is_legacy_transfer"
    ];

    private static readonly string[] CurrentHistoryColumns =
    [
        "id", "cid", "zone_id", "order", "main_title", "name", "duration", "video_codec_name",
        "resolution", "audio_codec", "file_size", "published_artifacts", "finished_timestamp",
        "finished_time", "max_speed_display", "requested_content"
    ];

    [Fact]
    public async Task EmptyDatabaseIsNew()
    {
        using var connection = OpenConnection();

        var shape = await DownloadStoreShapeReader.ReadAsync(
            connection,
            TestContext.Current.CancellationToken);

        Assert.True(shape.IsNew);
        Assert.False(shape.IsCurrent);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(7)]
    [InlineData(12)]
    [InlineData(99)]
    public async Task CurrentColumnsAreCurrentRegardlessOfVersionNumber(int version)
    {
        using var connection = OpenConnection();
        CreateCurrentShape(connection);
        Execute(connection, $"PRAGMA user_version = {version}");

        var shape = await DownloadStoreShapeReader.ReadAsync(
            connection,
            TestContext.Current.CancellationToken);

        Assert.True(shape.IsCurrent);
    }

    [Fact]
    public async Task MissingOptionalColumnsRemainAReadableProjectionInput()
    {
        using var connection = OpenConnection();
        CreateTable(connection, "download_base", [.. CoreBaseColumns, "version"]);
        CreateTable(connection, "downloading", CoreDownloadingColumns);
        CreateTable(connection, "downloaded",
            ["id", "max_speed_display", "finished_timestamp", "finished_time"]);
        Execute(connection, "PRAGMA user_version = 314");

        var shape = await DownloadStoreShapeReader.ReadAsync(
            connection,
            TestContext.Current.CancellationToken);

        Assert.False(shape.IsCurrent);
        Assert.True(shape.HasColumn("download_base", "version"));
        Assert.False(shape.HasColumn("download_base", "staging_token"));
    }

    [Fact]
    public async Task MissingRequiredColumnIsJustAnotherNonCurrentShape()
    {
        using var connection = OpenConnection();
        CreateTable(connection, "download_base", CoreBaseColumns.Where(column => column != "cid"));
        CreateTable(connection, "downloading", CoreDownloadingColumns);

        var shape = await DownloadStoreShapeReader.ReadAsync(
            connection,
            TestContext.Current.CancellationToken);

        Assert.False(shape.IsCurrent);
        Assert.False(shape.HasColumn("download_base", "cid"));
    }

    private static void CreateCurrentShape(SqliteConnection connection)
    {
        CreateTable(connection, "download_base", CurrentBaseColumns);
        CreateTable(connection, "downloading", CurrentDownloadingColumns);
        CreateTable(connection, "download_history", CurrentHistoryColumns);
        CreateTable(connection, "download_quarantine",
            ["quarantine_id", "source_table", "record_id", "field_name", "reason", "quarantined_at_utc"]);
    }

    private static SqliteConnection OpenConnection()
    {
        var connection = new SqliteConnection("Data Source=:memory:");
        connection.Open();
        return connection;
    }

    private static void CreateTable(
        SqliteConnection connection,
        string table,
        IEnumerable<string> columns) =>
        Execute(
            connection,
            $"CREATE TABLE [{table}] ({string.Join(", ", columns.Select(column => $"[{column}] TEXT"))})");

    [SuppressMessage("Security", "CA2100:Review SQL queries for security vulnerabilities",
        Justification = "All identifiers and SQL fragments are fixed test schema fixtures.")]
    private static void Execute(SqliteConnection connection, string commandText)
    {
        using var command = connection.CreateCommand();
        command.CommandText = commandText;
        command.ExecuteNonQuery();
    }
}
