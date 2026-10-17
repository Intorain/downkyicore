using System.Collections.Immutable;
using DownKyi.Application.Downloads;
using DownKyi.Application.Time;
using DownKyi.Domain.Downloads;
using DownKyi.Infrastructure.Downloads;
using Microsoft.Data.Sqlite;

namespace DownKyi.Infrastructure.Tests;

public sealed class SqliteDownloadStorePathPolicyTests : IDisposable
{
    private readonly SqliteDownloadStoreFixture _fixture = new();

    public void Dispose() => _fixture.Dispose();

    [Fact]
    public async Task ForeignPathIsPreservedAndRekeyedForTheCurrentSystem()
    {
        var original = Path.Combine(_fixture.TempDirectory, "native-path");
        using (var first = _fixture.CreateStore())
        {
            Assert.True((await first.AddAsync(_fixture.CreatePausedTask("foreign-path", original),
                TestContext.Current.CancellationToken)).IsSuccess);
        }
        var foreignPath = OperatingSystem.IsWindows() ? "/foreign/path" : @"C:\foreign\path";
        var originalKey = await _fixture.ReadReservationKeyAsync("foreign-path");
        await _fixture.SetPathAndReservationKeyAsync("foreign-path", foreignPath, originalKey);
        using var reopened = _fixture.CreateStore();
        await reopened.InitializeAsync(TestContext.Current.CancellationToken);
        Assert.Equal(foreignPath, (await _fixture.ReadStoredStateAsync("foreign-path")).Path);
        Assert.NotEqual(originalKey, await _fixture.ReadReservationKeyAsync("foreign-path"));
        Assert.Equal(
            DownloadOutputPathKey.Create(
                foreignPath,
                DownloadOutputPathKey.UsesCaseInsensitiveComparison),
            await _fixture.ReadReservationKeyAsync("foreign-path"));
    }

    [Fact]
    public async Task ReopenRejectsConflictingActivePathsAfterCurrentSystemProjection()
    {
        var foreignPath = OperatingSystem.IsWindows() ? "/foreign/path" : @"C:\foreign\path";
        using var store = _fixture.CreateStore();
        await store.InitializeAsync(TestContext.Current.CancellationToken);

        Assert.True((await store.AddAsync(
            _fixture.CreateQueuedTask("new-foreign-path", foreignPath),
            TestContext.Current.CancellationToken)).IsSuccess);
        var existing = _fixture.CreateQueuedTask("update-foreign-path",
            Path.Combine(_fixture.TempDirectory, "safe-existing"));
        Assert.True((await store.AddAsync(existing,
            TestContext.Current.CancellationToken)).IsSuccess);
        var changed = existing.UpdateOutput(new DownloadOutput(foreignPath, null),
            _fixture.Clock.UtcNow.AddSeconds(1)).RequireValue();
        Assert.True((await store.UpdateAsync(
            changed,
            existing.Version,
            TestContext.Current.CancellationToken)).IsSuccess);
        Assert.Equal(foreignPath, (await _fixture.ReadStoredStateAsync(existing.Id.Value)).Path);

        using var reopened = _fixture.CreateStore();
        await Assert.ThrowsAsync<DownloadStoreProjectionException>(() =>
            reopened.InitializeAsync(TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task ReopenRejectsLogicalAliasesThatResolveToOnePhysicalOutput()
    {
        var firstPath = Path.Combine(_fixture.TempDirectory, "alias-one", "output");
        var secondPath = Path.Combine(_fixture.TempDirectory, "alias-two", "output");
        using (var first = _fixture.CreateStore())
        {
            Assert.True((await first.AddAsync(
                _fixture.CreateQueuedTask("alias-one", firstPath),
                TestContext.Current.CancellationToken)).IsSuccess);
            Assert.True((await first.AddAsync(
                _fixture.CreateQueuedTask("alias-two", secondPath),
                TestContext.Current.CancellationToken)).IsSuccess);
        }

        var physicalPath = Path.Combine(_fixture.TempDirectory, "physical", "output");
        using var reopened = _fixture.CreateStore(
            resolver: new SqliteDownloadStoreFixture.StubPhysicalOutputPathResolver(
                _ => physicalPath));

        await Assert.ThrowsAsync<DownloadStoreProjectionException>(() =>
            reopened.InitializeAsync(TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task ForwardSlashUncPathFollowsCurrentPlatformPolicyAcrossReopen()
    {
        const string path = "//server/share/downkyi-output";
        using (var first = _fixture.CreateStore())
        {
            Assert.True((await first.AddAsync(_fixture.CreateQueuedTask("slash-unc", path),
                TestContext.Current.CancellationToken)).IsSuccess);
        }

        using var reopened = _fixture.CreateStore();
        await reopened.InitializeAsync(TestContext.Current.CancellationToken);
        Assert.Equal(DownloadOutputPathKey.Create(path,
            DownloadOutputPathKey.UsesCaseInsensitiveComparison),
            await _fixture.ReadReservationKeyAsync("slash-unc"));
        Assert.True(await reopened.IsOutputPathReservedAsync(path,
            DownloadOutputPathKey.UsesCaseInsensitiveComparison,
            TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task ActiveUpdateRecomputesKeyAndUniqueRejectsEquivalentNewAdmission()
    {
        var original = Path.Combine(_fixture.TempDirectory, "update-original");
        var next = Path.Combine(_fixture.TempDirectory, "update-cafe\u0301");
        var equivalent = Path.Combine(_fixture.TempDirectory, "update-caf\u00e9");
        using var store = _fixture.CreateStore();
        var task = _fixture.CreateQueuedTask("update-path", original);
        Assert.True((await store.AddAsync(task, TestContext.Current.CancellationToken)).IsSuccess);
        await _fixture.SetReservationKeyAsync(task.Id.Value, null);
        var updated = task.UpdateOutput(new DownloadOutput(next, null),
            _fixture.Clock.UtcNow.AddSeconds(1)).RequireValue();
        Assert.True((await store.UpdateAsync(updated, task.Version,
            TestContext.Current.CancellationToken)).IsSuccess);
        Assert.Equal(DownloadOutputPathKey.Create(next,
            DownloadOutputPathKey.UsesCaseInsensitiveComparison),
            await _fixture.ReadReservationKeyAsync(task.Id.Value));
        Assert.False((await store.AddAsync(_fixture.CreateQueuedTask("update-duplicate", equivalent),
            TestContext.Current.CancellationToken)).IsSuccess);
    }
}
