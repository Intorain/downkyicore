namespace DownKyi.Architecture.Tests;

public sealed class DownloadAdmissionArchitectureTests
{
    private static readonly string RepositoryRoot = FindRepositoryRoot();

    [Fact]
    public void RemovedBackendDoesNotGateNewDownloadAdmission()
    {
        var service = ReadSource(
            "src", "DownKyi.Application", "Downloads", "DownloadTaskApplicationService.cs");
        var presenterPath = Path.Combine(
            RepositoryRoot,
            "src", "DownKyi.Desktop", "Services", "Download", "LegacyDownloadAdmissionPresenter.cs");

        Assert.Contains("Task.FromResult(OperationResult.Success())", service, StringComparison.Ordinal);
        Assert.DoesNotContain("IsLegacyUpgradeAdmissionBlockedAsync", service, StringComparison.Ordinal);
        Assert.False(File.Exists(presenterPath));
    }

    [Fact]
    public void AddFlowRequiresExplicitSelectionPreparationAndFinalization()
    {
        var contract = ReadSource(
            "src", "DownKyi.Desktop", "Services", "Download", "IAddToDownloadSession.cs");
        var service = ReadSource(
            "src", "DownKyi.Desktop", "Services", "Download", "AddToDownloadService.cs");
        var selection = ReadSource(
            "src", "DownKyi.Application", "Downloads", "DownloadAddSelection.cs");
        var prepared = ReadSource(
            "src", "DownKyi.Desktop", "Services", "Download", "PreparedDownload.cs");
        var resolver = ReadSource(
            "src", "DownKyi.Desktop", "Services", "Download", "DownloadContentConflictResolver.cs");
        var planner = ReadSource(
            "src", "DownKyi.Desktop", "Services", "Download", "DownloadActionPlanner.cs");
        var duplicatePolicy = ReadSource(
            "src", "DownKyi.Desktop", "Services", "Download", "DownloadDuplicatePolicy.cs");
        var actionCoverage = ReadSource(
            "src", "DownKyi.Desktop", "Services", "Download", "DownloadActionCoverage.cs");
        var content = ReadSource(
            "src", "DownKyi.Desktop", "Services", "Media", "ContentDownloadCoordinator.cs");
        var video = ReadSource(
            "src", "DownKyi.Desktop", "Services", "Video", "VideoDetailDownloadCoordinator.cs");

        Assert.Contains("Task<DownloadSelectionResult> SelectDownloadAsync", contract, StringComparison.Ordinal);
        Assert.Contains("Task<PreparedDownload> PrepareAsync", contract, StringComparison.Ordinal);
        Assert.Contains("FinalizedDownload finalizedDownload", contract, StringComparison.Ordinal);
        Assert.Contains("DownloadContentSelection RequestedContent", selection, StringComparison.Ordinal);
        Assert.Contains("DownloadMediaCapabilities AvailableMedia", prepared, StringComparison.Ordinal);
        Assert.Contains("DownloadMediaOutputModes SupportedModes", prepared, StringComparison.Ordinal);
        Assert.Contains("HasAnyMedia", prepared, StringComparison.Ordinal);
        Assert.Contains("Supports(DownloadContentSelection", prepared, StringComparison.Ordinal);
        Assert.Contains("TryGetCompatibleContent(", prepared, StringComparison.Ordinal);
        Assert.DoesNotContain("DownloadMediaCapabilities(bool Video, bool Audio)", prepared, StringComparison.Ordinal);
        Assert.Contains("DownloadContentSelection RequestedContent", prepared, StringComparison.Ordinal);
        Assert.Contains("DownloadContentSelection FinalizedContent", prepared, StringComparison.Ordinal);
        Assert.Contains("DownloadContentConflictChoices choices", resolver, StringComparison.Ordinal);
        Assert.Contains("DownloadContentConflictDialogContract.ShowAsync", resolver, StringComparison.Ordinal);
        Assert.Contains("DownloadPlanningStopReason", planner, StringComparison.Ordinal);
        Assert.Contains("_conflictResolver", planner, StringComparison.Ordinal);
        Assert.Contains(".ResolveAsync(", planner, StringComparison.Ordinal);
        Assert.Contains("_actionPlanner", content, StringComparison.Ordinal);
        Assert.Contains("_actionPlanner", video, StringComparison.Ordinal);
        Assert.Contains(
            "DownloadActionCoverage.RemoveCoveredActions",
            duplicatePolicy,
            StringComparison.Ordinal);
        Assert.DoesNotContain("MediaActionMatches", duplicatePolicy, StringComparison.Ordinal);
        Assert.Contains("MediaActionMatches", actionCoverage, StringComparison.Ordinal);
        Assert.DoesNotContain("MediaParametersMatch", actionCoverage, StringComparison.Ordinal);
        Assert.DoesNotContain("VideoCodecName", actionCoverage, StringComparison.Ordinal);
        Assert.DoesNotContain("AudioCodec.Name", actionCoverage, StringComparison.Ordinal);
        Assert.DoesNotContain("Resolution.Id", actionCoverage, StringComparison.Ordinal);
        Assert.DoesNotContain("HasPlayback", planner, StringComparison.Ordinal);
        Assert.DoesNotContain("_downloadContent", service, StringComparison.Ordinal);
        Assert.DoesNotContain("DownloadContentConflict", service, StringComparison.Ordinal);
        Assert.DoesNotContain("SetVideoInfoService", contract, StringComparison.Ordinal);
        Assert.DoesNotContain("GetVideo(", contract, StringComparison.Ordinal);
        Assert.DoesNotContain("ParseVideoAsync", contract, StringComparison.Ordinal);
    }

    [Fact]
    public void DuplicateOutputAndRecoveryUseTheSharedActionClaimsContract()
    {
        var contract = ReadSource(
            "src", "DownKyi.Domain", "Downloads", "DownloadContentSelection.cs");
        var coverage = ReadSource(
            "src", "DownKyi.Desktop", "Services", "Download", "DownloadActionCoverage.cs");
        var duplicatePolicy = ReadSource(
            "src", "DownKyi.Desktop", "Services", "Download", "DownloadDuplicatePolicy.cs");
        var outputResolver = ReadSource(
            "src", "DownKyi.Desktop", "Services", "Download", "DownloadOutputPathResolver.cs");
        var reservations = ReadSource(
            "src", "DownKyi.Infrastructure", "Downloads", "SqliteDownloadStoreOutputReservations.cs");
        var recovery = ReadSource(
            "src", "DownKyi.Infrastructure", "Downloads", "DownloadStoreReservationKeyCompatibility.cs");

        Assert.Contains("record struct DownloadActionClaims", contract, StringComparison.Ordinal);
        Assert.Contains("Nfo = 32", contract, StringComparison.Ordinal);
        Assert.Contains("SubtractFrom(DownloadContentSelection", contract, StringComparison.Ordinal);
        Assert.Contains("coveredClaims.SubtractFrom(requestedContent)", coverage, StringComparison.Ordinal);
        Assert.Contains("MatchesOutputOwner", duplicatePolicy, StringComparison.Ordinal);
        Assert.Contains("IPhysicalOutputPathResolver", duplicatePolicy, StringComparison.Ordinal);
        Assert.Contains("_physicalOutputPathResolver", duplicatePolicy, StringComparison.Ordinal);
        Assert.Contains("ResolvePhysicalBasePath", coverage, StringComparison.Ordinal);
        Assert.DoesNotContain("OperatingSystem.IsMacOS", coverage, StringComparison.Ordinal);
        Assert.Contains("AllowExistingBasePath: hasMatchingOutputOwner", duplicatePolicy, StringComparison.Ordinal);
        Assert.Contains("DownloadActionClaim.Nfo", outputResolver, StringComparison.Ordinal);
        Assert.Contains("reader.GetInt64(2) != requestedCid", reservations, StringComparison.Ordinal);
        Assert.Contains("existingClaims.Overlaps(requestedClaims)", reservations, StringComparison.Ordinal);
        Assert.Contains("db.nfo_request IS NOT NULL", reservations, StringComparison.Ordinal);
        Assert.Contains("DownloadTaskSqlReader.SelectColumns", recovery, StringComparison.Ordinal);
        Assert.Contains("DownloadTaskRecordMapper.Read", recovery, StringComparison.Ordinal);
        Assert.Contains("SqliteDownloadStoreQuarantine.RecordAsync", recovery, StringComparison.Ordinal);
        Assert.Contains("task.Plan.ActionClaims", recovery, StringComparison.Ordinal);
        Assert.DoesNotContain("DownloadStoreJson.ReadContentSelection", recovery, StringComparison.Ordinal);
        Assert.Contains("reservation.Cid != row.Cid", recovery, StringComparison.Ordinal);
        Assert.Contains("reservation.Claims.Overlaps(row.Claims)", recovery, StringComparison.Ordinal);
        Assert.DoesNotContain("DownloadOutputClaims", coverage, StringComparison.Ordinal);
        Assert.DoesNotContain("DownloadOutputClaims", reservations, StringComparison.Ordinal);
        Assert.DoesNotContain("DownloadOutputClaims", recovery, StringComparison.Ordinal);
    }

    private static string ReadSource(params string[] segments)
    {
        return File.ReadAllText(Path.Combine([RepositoryRoot, .. segments]));
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
