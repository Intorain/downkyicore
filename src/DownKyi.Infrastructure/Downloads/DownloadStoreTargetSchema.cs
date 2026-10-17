namespace DownKyi.Infrastructure.Downloads;

internal static class DownloadStoreTargetSchema
{
    public static readonly string[] KnownTables =
    [
        "download_base",
        "downloading",
        "downloaded",
        "download_history",
        "download_quarantine"
    ];

    public static readonly IReadOnlyDictionary<string, string[]> RequiredInputColumns =
        new Dictionary<string, string[]>(StringComparer.Ordinal)
        {
            ["download_base"] =
            [
                "id", "need_download_content", "bvid", "avid", "cid", "episode_id", "cover_url",
                "page_cover_url", "zone_id", "order", "main_title", "name", "duration",
                "video_codec_name", "resolution", "audio_codec", "file_path", "file_size", "page"
            ],
            ["downloading"] =
            [
                "id", "gid", "download_files", "downloaded_files", "play_stream_type",
                "download_status", "download_content", "download_status_title", "progress",
                "downloading_file_size", "max_speed", "speed_display"
            ],
            ["downloaded"] =
            [
                "id", "max_speed_display", "finished_timestamp", "finished_time"
            ],
            ["download_history"] =
            [
                "id", "cid", "zone_id", "order", "main_title", "name", "duration",
                "video_codec_name", "resolution", "audio_codec", "file_size",
                "published_artifacts", "finished_timestamp", "finished_time", "max_speed_display"
            ]
        };

    public static readonly DownloadStoreTargetColumn[] AddedColumns =
    [
        new("download_base", "version"),
        new("download_base", "created_at_utc"),
        new("download_base", "updated_at_utc"),
        new("download_base", "output_reservation_key"),
        new("download_base", "nfo_request"),
        new("download_base", "published_artifacts"),
        new("download_base", "staging_token"),
        new("download_base", "publishing_key"),
        new("download_base", "publishing_file_name"),
        new("download_base", "publishing_length"),
        new("download_base", "publishing_sha256"),
        new("downloading", "phase"),
        new("downloading", "failure_code"),
        new("downloading", "failure_message"),
        new("downloading", "failure_transient"),
        new("downloading", "downloaded_bytes"),
        new("downloading", "total_bytes"),
        new("downloading", "bytes_per_second"),
        new("downloading", "is_legacy_transfer"),
        new("download_history", "requested_content")
    ];

    public static readonly IReadOnlyDictionary<string, string[]> CurrentColumns =
        new Dictionary<string, string[]>(StringComparer.Ordinal)
        {
            ["download_base"] =
            [
                .. RequiredInputColumns["download_base"],
                .. AddedColumns
                    .Where(column => column.Table == "download_base")
                    .Select(column => column.Name)
            ],
            ["downloading"] =
            [
                .. RequiredInputColumns["downloading"],
                .. AddedColumns
                    .Where(column => column.Table == "downloading")
                    .Select(column => column.Name)
            ],
            ["download_history"] =
            [
                .. RequiredInputColumns["download_history"],
                .. AddedColumns
                    .Where(column => column.Table == "download_history")
                    .Select(column => column.Name)
            ]
        };

    public static readonly string[] SupportTables =
    [
        "download_quarantine"
    ];
}

internal sealed record DownloadStoreTargetColumn(
    string Table,
    string Name);
