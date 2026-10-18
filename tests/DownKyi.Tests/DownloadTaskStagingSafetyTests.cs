using DownKyi.Application.Downloads;
using DownKyi.Domain.Downloads;
using DownKyi.Services.Download;
using Microsoft.Extensions.Logging.Abstractions;

namespace DownKyi.Tests;

public sealed class DownloadTaskStagingSafetyTests : IDisposable
{
    private readonly string _directory = Path.Combine(
        Path.GetTempPath(), "downkyi-staging-safety", Guid.NewGuid().ToString("N"));

    [Fact]
    public void LastTaskCleanupRemovesEmptyStagingHierarchy()
    {
        var outputBase = Path.Combine(_directory, "output");
        var taskId = new DownloadTaskId("completed-task");
        var staging = new DownloadTaskStaging(NullLogger<DownloadTaskStaging>.Instance);
        staging.GetDirectory(taskId, outputBase, Guid.NewGuid().ToString("N"));

        Assert.True(staging.CleanupTask(taskId));

        Assert.False(Directory.Exists(Path.Combine(_directory, ".downkyi")));
    }

    [Fact]
    public void ParentCleanupWaitsForOtherTaskInSameOutputDirectory()
    {
        var outputBase = Path.Combine(_directory, "output");
        var firstTask = new DownloadTaskId("first-task");
        var secondTask = new DownloadTaskId("second-task");
        var staging = new DownloadTaskStaging(NullLogger<DownloadTaskStaging>.Instance);
        var firstDirectory = staging.GetDirectory(
            firstTask, outputBase, Guid.NewGuid().ToString("N"));
        var secondDirectory = staging.GetDirectory(
            secondTask, outputBase, Guid.NewGuid().ToString("N"));

        Assert.True(staging.CleanupTask(firstTask));

        Assert.False(Directory.Exists(firstDirectory));
        Assert.True(Directory.Exists(secondDirectory));
        Assert.True(Directory.Exists(Path.Combine(_directory, ".downkyi", "staging")));

        Assert.True(staging.CleanupTask(secondTask));
        Assert.False(Directory.Exists(Path.Combine(_directory, ".downkyi")));
    }

    [Fact]
    public void ParentCleanupPreservesUnknownContent()
    {
        var outputBase = Path.Combine(_directory, "output");
        var taskId = new DownloadTaskId("completed-task");
        var staging = new DownloadTaskStaging(NullLogger<DownloadTaskStaging>.Instance);
        staging.GetDirectory(taskId, outputBase, Guid.NewGuid().ToString("N"));
        var unknownFile = Path.Combine(_directory, ".downkyi", "keep.txt");
        File.WriteAllText(unknownFile, "keep");

        Assert.True(staging.CleanupTask(taskId));

        Assert.True(File.Exists(unknownFile));
        Assert.False(Directory.Exists(Path.Combine(_directory, ".downkyi", "staging")));
    }

    [Fact]
    public void ShutdownPreservesOwnedTransferForNextSession()
    {
        var outputBase = Path.Combine(_directory, "output");
        var task = CreateTask(outputBase, Path.Combine(_directory, "published.mp4"));
        var first = new DownloadTaskStaging(NullLogger<DownloadTaskStaging>.Instance);
        var directory = first.GetDirectory(task.Id, outputBase, task.Output.StagingToken);
        File.WriteAllBytes(Path.Combine(directory, "partial.m4s"), [1, 2, 3]);

        first.CleanupCurrentSession();

        var reopened = new DownloadTaskStaging(NullLogger<DownloadTaskStaging>.Instance);
        Assert.Equal(directory, reopened.GetDirectory(task.Id, outputBase, task.Output.StagingToken));
        Assert.Equal(new byte[] { 1, 2, 3 }, File.ReadAllBytes(Path.Combine(directory, "partial.m4s")));
        reopened.CleanupCurrentSession();
    }

    [Fact]
    public async Task ConcurrentContextCreationOpensOneTaskLock()
    {
        var outputBase = Path.Combine(_directory, "output");
        var task = CreateTask(outputBase, Path.Combine(_directory, "published.mp4"));
        var staging = new DownloadTaskStaging(NullLogger<DownloadTaskStaging>.Instance);
        using var start = new ManualResetEventSlim();
        var workers = Enumerable.Range(0, 16).Select(_ => Task.Run(() =>
        {
            start.Wait();
            return staging.GetDirectory(task.Id, outputBase, task.Output.StagingToken);
        })).ToArray();

        start.Set();
        var directories = await Task.WhenAll(workers);

        Assert.All(directories, directory => Assert.Equal(directories[0], directory));
        staging.CleanupCurrentSession();
    }

    [Fact]
    public void TaskCleanupDoesNotFollowDirectorySymlinkInsideStaging()
    {
        var outputBase = Path.Combine(_directory, "output");
        var outside = Path.Combine(_directory, "outside-user-directory");
        Directory.CreateDirectory(outside);
        var sentinel = Path.Combine(outside, "DO-NOT-DELETE.txt");
        File.WriteAllBytes(sentinel, [91, 0, 255, 17]);
        var taskId = new DownloadTaskId("linked-child");
        var staging = new DownloadTaskStaging(NullLogger<DownloadTaskStaging>.Instance);
        var taskDirectory = staging.GetDirectory(taskId, outputBase, Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(taskDirectory);
        Directory.CreateSymbolicLink(Path.Combine(taskDirectory, "link"), outside);

        staging.CleanupTask(taskId);

        Assert.Equal(new byte[] { 91, 0, 255, 17 }, File.ReadAllBytes(sentinel));
        staging.CleanupCurrentSession();
    }

    [Fact]
    public void TaskCleanupRefusesRedirectedStagingDirectory()
    {
        var outputBase = Path.Combine(_directory, "output");
        var outside = Path.Combine(_directory, "outside-user-directory");
        Directory.CreateDirectory(outside);
        var sentinel = Path.Combine(outside, "DO-NOT-DELETE.txt");
        File.WriteAllBytes(sentinel, [91, 0, 255, 17]);
        var taskId = new DownloadTaskId("linked-staging");
        var staging = new DownloadTaskStaging(NullLogger<DownloadTaskStaging>.Instance);
        var taskDirectory = staging.GetDirectory(taskId, outputBase, Guid.NewGuid().ToString("N"));
        Directory.Delete(taskDirectory);
        Directory.CreateSymbolicLink(taskDirectory, outside);

        staging.CleanupTask(taskId);

        Assert.Equal(new byte[] { 91, 0, 255, 17 }, File.ReadAllBytes(sentinel));
        staging.CleanupCurrentSession();
        Assert.Equal(new byte[] { 91, 0, 255, 17 }, File.ReadAllBytes(sentinel));
    }

    private static DownloadTask CreateTask(
        string outputBase,
        string published,
        IEnumerable<KeyValuePair<string, string>>? transferFiles = null)
    {
        var now = DateTimeOffset.UnixEpoch;
        var task = DownloadTask.Create(
            new DownloadTaskId("prior-task"),
            new DownloadTaskMetadata(
                new DownloadMediaIdentity("BV1TEST", 1, 2, 3, 1, 1),
                "Collection", "Task", "00:01", "avc1",
                new DownloadQuality(80, "1080P"),
                new DownloadQuality(30280, "AAC"),
                string.Empty, string.Empty, 0),
            new DownloadPlan(DownloadContentSelection.None, transferFiles ?? [], 0, nfoRequest: null),
            new DownloadOutput(outputBase, null),
            now);
        task = task.Start(now.AddSeconds(1)).RequireValue();
        var publishing = new DownloadPublishingArtifact(
            "media", Path.GetFileName(published), 0, new string('A', 64));
        task = task.BeginPublishingArtifact(publishing,
            now.AddSeconds(2)).RequireValue();
        return task.RecordPublishedArtifact(publishing, published, now.AddSeconds(3)).RequireValue();
    }

    public void Dispose()
    {
        if (Directory.Exists(_directory))
        {
            Directory.Delete(_directory, recursive: true);
        }
    }
}
