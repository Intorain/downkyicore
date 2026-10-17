using DownKyi.Core.FFmpeg;
using DownKyi.Core.Settings;
using Microsoft.Extensions.Logging.Abstractions;

namespace DownKyi.Core.Tests;

public sealed class FfmpegProcessorMergeTests : IDisposable
{
    private readonly string _directory = Path.Combine(
        Path.GetTempPath(),
        $"downkyi-merge-{Guid.NewGuid():N}");
    private readonly SettingsStore _settings;

    public FfmpegProcessorMergeTests()
    {
        Directory.CreateDirectory(_directory);
        _settings = new SettingsStore(Path.Combine(_directory, "settings.json"));
    }

    [Fact]
    public async Task MergeProcessFailureDoesNotRunAdditionalDecodeOrDeleteInputs()
    {
        var audio = CreateInput("audio.m4s");
        var video = CreateInput("video.m4s");
        var runner = new MergeFailureRunner(processStarted: true);
        var processor = new FfmpegProcessor(
            _settings,
            NullLoggerFactory.Instance,
            runner);

        var result = await processor.MergeMediaAsync(
            _settings.Current.Video,
            audio,
            video,
            Path.Combine(_directory, "output.mp4"),
            overwriteDestination: false,
            cancellationToken: TestContext.Current.CancellationToken);

        Assert.False(result.Succeeded);
        Assert.Equal(FfmpegOperationFailureKind.ProcessFailure, result.FailureKind);
        Assert.Equal(1, runner.CallCount);
        Assert.True(File.Exists(audio));
        Assert.True(File.Exists(video));
    }

    [Fact]
    public async Task MergeUnavailableProcessPreservesFailureKindAndInputs()
    {
        var audio = CreateInput("unavailable-audio.m4s");
        var video = CreateInput("unavailable-video.m4s");
        var runner = new MergeFailureRunner(processStarted: false);
        var processor = new FfmpegProcessor(
            _settings,
            NullLoggerFactory.Instance,
            runner);

        var result = await processor.MergeMediaAsync(
            _settings.Current.Video,
            audio,
            video,
            Path.Combine(_directory, "unavailable-output.mp4"),
            overwriteDestination: false,
            cancellationToken: TestContext.Current.CancellationToken);

        Assert.False(result.Succeeded);
        Assert.Equal(FfmpegOperationFailureKind.ProcessUnavailable, result.FailureKind);
        Assert.Equal(1, runner.CallCount);
        Assert.True(File.Exists(audio));
        Assert.True(File.Exists(video));
    }

    [Fact]
    public async Task DestinationConflictDoesNotOverwriteDestinationOrDeleteInputs()
    {
        var audio = CreateInput("destination-audio.m4s");
        var video = CreateInput("destination-video.m4s");
        var destination = Path.Combine(_directory, "destination.mp4");
        byte[] originalDestination = [9, 8, 7];
        await File.WriteAllBytesAsync(
            destination,
            originalDestination,
            TestContext.Current.CancellationToken);
        var runner = new SuccessfulMergeRunner();
        var processor = new FfmpegProcessor(
            _settings,
            NullLoggerFactory.Instance,
            runner);

        var result = await processor.MergeMediaAsync(
            _settings.Current.Video,
            audio,
            video,
            destination,
            overwriteDestination: false,
            cancellationToken: TestContext.Current.CancellationToken);

        Assert.False(result.Succeeded);
        Assert.Equal(FfmpegOperationFailureKind.DestinationConflict, result.FailureKind);
        Assert.Equal(1, runner.CallCount);
        Assert.True(File.Exists(audio));
        Assert.True(File.Exists(video));
        Assert.Equal(
            originalDestination,
            await File.ReadAllBytesAsync(destination, TestContext.Current.CancellationToken));
    }

    public void Dispose()
    {
        _settings.Dispose();
        Directory.Delete(_directory, recursive: true);
        GC.SuppressFinalize(this);
    }

    private string CreateInput(string fileName)
    {
        var path = Path.Combine(_directory, fileName);
        File.WriteAllBytes(path, [1, 2, 3]);
        return path;
    }

    private sealed class MergeFailureRunner(bool processStarted) : IFfmpegProcessRunner
    {
        public int CallCount { get; private set; }

        public Task<FfmpegProcessResult> RunAsync(
            FfmpegCommand command,
            TimeSpan timeout,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            CallCount++;
            Assert.Equal("merge-media", command.Operation);
            return Task.FromResult(new FfmpegProcessResult(
                false,
                processStarted ? 1 : -1,
                string.Empty,
                "mux failed",
                false,
                ProcessStarted: processStarted));
        }
    }

    private sealed class SuccessfulMergeRunner : IFfmpegProcessRunner
    {
        public int CallCount { get; private set; }

        public async Task<FfmpegProcessResult> RunAsync(
            FfmpegCommand command,
            TimeSpan timeout,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            CallCount++;
            Assert.Equal("merge-media", command.Operation);
            await File.WriteAllBytesAsync(
                command.Arguments[^1],
                [1, 2, 3],
                cancellationToken).ConfigureAwait(false);
            return new FfmpegProcessResult(true, 0, string.Empty, string.Empty, false);
        }
    }
}
