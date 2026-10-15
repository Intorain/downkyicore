namespace DownKyi.Infrastructure.Downloads;

internal enum LegacyDownloadStoreKind
{
    New,
    Relational,
    Stateful,
    Reserved,
    AdmissionSafe,
    HistoryContentUpgrade,
    HistoryContentUpgradeWithLegacyHistory,
    LegacyTransferMarkerUpgrade,
    LegacyTransferMarkerUpgradeWithLegacyHistory,
    ActionClaimReservationUpgrade,
    ActionClaimReservationUpgradeWithLegacyHistory,
    CurrentWithLegacyHistory,
    Current,
    Unsupported
}

internal sealed record LegacyDownloadStoreFormat(
    LegacyDownloadStoreKind Kind,
    bool DatabaseExisted,
    int UserVersion,
    bool HasCoreTables,
    bool HasSchemaLedger,
    bool HasQuarantine,
    bool HasStateColumns,
    bool HasReservationKey,
    bool HasAdmissionGate,
    bool HasNfoRequest,
    bool HasPublishedArtifacts,
    bool HasStagingToken,
    bool HasPublishingArtifact,
    bool HasHistoryContent,
    bool HasLegacyTransferMarker,
    IReadOnlyList<string> SchemaDifferences)
{
    public bool IsNew => Kind == LegacyDownloadStoreKind.New;

    public bool IsCurrent => Kind == LegacyDownloadStoreKind.Current;

    public bool CanUpgradeCurrentShapeInPlace =>
        Kind is LegacyDownloadStoreKind.HistoryContentUpgrade
            or LegacyDownloadStoreKind.ActionClaimReservationUpgrade
            or LegacyDownloadStoreKind.LegacyTransferMarkerUpgrade;

    public bool RequiresLegacyHistoryRecovery =>
        Kind is LegacyDownloadStoreKind.CurrentWithLegacyHistory
            or LegacyDownloadStoreKind.LegacyTransferMarkerUpgradeWithLegacyHistory
            or LegacyDownloadStoreKind.ActionClaimReservationUpgradeWithLegacyHistory
            or LegacyDownloadStoreKind.HistoryContentUpgradeWithLegacyHistory;

    public bool IsSupported => Kind != LegacyDownloadStoreKind.Unsupported;
}

internal sealed record LegacyDownloadRow(
    string Id,
    string BasePath,
    int LegacyStatus,
    string? ReservationKey);

internal sealed record LegacyDownloadStoreSnapshot(
    LegacyDownloadStoreFormat Format,
    IReadOnlyList<LegacyDownloadRow> DownloadRows,
    IReadOnlyList<LegacyBaseRecord> BaseRecords);

internal sealed record LegacyBaseRecord(string Id, string? StagingToken);

internal sealed record LegacyPhaseUpdate(string Id, int Phase);

internal sealed record LegacyReservationUpdate(string Id, string ReservationKey);

internal sealed record LegacyStagingTokenUpdate(string Id, string StagingToken);

internal sealed record LegacyDownloadNormalizationPlan(
    IReadOnlyList<LegacyPhaseUpdate> PhaseUpdates,
    IReadOnlyList<LegacyReservationUpdate> ReservationUpdates,
    IReadOnlyList<string> QuarantineRecordIds,
    IReadOnlyList<LegacyStagingTokenUpdate> StagingTokenUpdates);
