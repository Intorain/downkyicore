using DownKyi.Core.FFmpeg;
using Microsoft.Extensions.Logging.Abstractions;

namespace DownKyi.Core.Tests;

public sealed class FfmpegSeekabilityIntegrationTests : IDisposable
{
    private static readonly TimeSpan ProcessTimeout = TimeSpan.FromMinutes(2);
    private readonly string _testDirectory = Path.Combine(
        Path.GetTempPath(),
        $"downkyi-ffmpeg-integration-{Guid.NewGuid():N}");

    public FfmpegSeekabilityIntegrationTests()
    {
        Directory.CreateDirectory(_testDirectory);
    }

    [Fact]
    [Trait("Category", "FfmpegIntegration")]
    public async Task MultiSegmentDurlOutputIsFinalizedAfterFfmpegSuccess()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var processRunner = new FfmpegProcessRunner();
        if (!await IsToolAvailableAsync(
                processRunner,
                FfmpegExecutableLocator.Ffmpeg,
                cancellationToken).ConfigureAwait(true))
        {
            Assert.Skip("ffmpeg is required for the concat integration test.");
        }

        var first = Path.Combine(_testDirectory, "segment-1.mp4");
        var second = Path.Combine(_testDirectory, "segment-2.mp4");
        await CreateSegmentAsync(processRunner, first, "red", cancellationToken).ConfigureAwait(true);
        await CreateSegmentAsync(processRunner, second, "blue", cancellationToken).ConfigureAwait(true);

        var runtime = new FfmpegConcatRuntime(
            processRunner,
            new AsyncConcurrencyGate(() => 1),
            NullLogger<FfmpegConcatRuntime>.Instance);
        var output = Path.Combine(_testDirectory, "seekable.mp4");

        var result = await runtime.ConcatAsync(
            [
                new FfmpegConcatSegment(2, second),
                new FfmpegConcatSegment(1, first)
            ],
            output,
            hardwareEncoder: null,
            allowStreamCopy: false,
            overwriteDestination: true,
            cancellationToken: cancellationToken).ConfigureAwait(true);

        Assert.True(result.Succeeded, result.FailureReason);
        Assert.True(File.Exists(output));
        Assert.NotEqual(0, new FileInfo(output).Length);
    }

    [Fact]
    [Trait("Category", "FfmpegIntegration")]
    public async Task CorruptSegmentDoesNotCommitConcatOutput()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var processRunner = new FfmpegProcessRunner();
        if (!await IsToolAvailableAsync(
                processRunner,
                FfmpegExecutableLocator.Ffmpeg,
                cancellationToken).ConfigureAwait(true))
        {
            Assert.Skip("ffmpeg is required for the concat integration test.");
        }

        var clean = Path.Combine(_testDirectory, "clean.ts");
        var corrupt = Path.Combine(_testDirectory, "corrupt.ts");
        await CreateTransportStreamSegmentAsync(processRunner, clean, cancellationToken).ConfigureAwait(true);
        await CreateCorruptSegmentAsync(processRunner, clean, corrupt, cancellationToken).ConfigureAwait(true);

        var runtime = new FfmpegConcatRuntime(
            processRunner,
            new AsyncConcurrencyGate(() => 1),
            NullLogger<FfmpegConcatRuntime>.Instance);
        var output = Path.Combine(_testDirectory, "must-not-exist.mp4");

        var result = await runtime.ConcatAsync(
            [
                new FfmpegConcatSegment(1, clean),
                new FfmpegConcatSegment(2, corrupt)
            ],
            output,
            hardwareEncoder: null,
            allowStreamCopy: false,
            overwriteDestination: true,
            embeddedAudioMode: FfmpegEmbeddedAudioMode.Excluded,
            cancellationToken: cancellationToken).ConfigureAwait(true);

        Assert.False(result.Succeeded);
        Assert.Equal(FfmpegOperationFailureKind.ProcessFailure, result.FailureKind);
        Assert.False(File.Exists(output));
    }

    public void Dispose()
    {
        Directory.Delete(_testDirectory, recursive: true);
        GC.SuppressFinalize(this);
    }

    private static async Task<bool> IsToolAvailableAsync(
        FfmpegProcessRunner processRunner,
        string executable,
        CancellationToken cancellationToken)
    {
        var result = await processRunner.RunAsync(
            new FfmpegCommand(executable, ["-version"], "version-check"),
            TimeSpan.FromSeconds(10),
            cancellationToken).ConfigureAwait(false);
        return result.Succeeded;
    }

    private static async Task CreateSegmentAsync(
        FfmpegProcessRunner processRunner,
        string output,
        string color,
        CancellationToken cancellationToken)
    {
        var result = await processRunner.RunAsync(
            new FfmpegCommand(
                FfmpegExecutableLocator.Ffmpeg,
                [
                    "-hide_banner",
                    "-nostdin",
                    "-y",
                    "-f", "lavfi",
                    "-i", $"color=c={color}:s=320x240:r=30:d=2",
                    "-c:v", "libx264",
                    "-pix_fmt", "yuv420p",
                    output
                ],
                "create-test-segment"),
            ProcessTimeout,
            cancellationToken).ConfigureAwait(false);

        Assert.True(result.Succeeded, result.StandardError);
    }

    private static async Task CreateTransportStreamSegmentAsync(
        FfmpegProcessRunner processRunner,
        string output,
        CancellationToken cancellationToken)
    {
        var result = await processRunner.RunAsync(
            new FfmpegCommand(
                FfmpegExecutableLocator.Ffmpeg,
                [
                    "-hide_banner",
                    "-nostdin",
                    "-y",
                    "-f", "lavfi",
                    "-i", "testsrc2=s=320x240:r=30:d=2",
                    "-c:v", "libx264",
                    "-g", "60",
                    "-pix_fmt", "yuv420p",
                    "-f", "mpegts",
                    output
                ],
                "create-transport-stream-segment"),
            ProcessTimeout,
            cancellationToken).ConfigureAwait(false);

        Assert.True(result.Succeeded, result.StandardError);
    }

    private static async Task CreateCorruptSegmentAsync(
        FfmpegProcessRunner processRunner,
        string input,
        string output,
        CancellationToken cancellationToken)
    {
        var result = await processRunner.RunAsync(
            new FfmpegCommand(
                FfmpegExecutableLocator.Ffmpeg,
                [
                    "-hide_banner",
                    "-nostdin",
                    "-y",
                    "-i", input,
                    "-map", "0:v:0",
                    "-c:v", "copy",
                    "-bsf:v", "noise=amount='if(eq(n,30),10,0)'",
                    "-f", "mpegts",
                    output
                ],
                "create-corrupt-test-segment"),
            ProcessTimeout,
            cancellationToken).ConfigureAwait(false);

        Assert.True(result.Succeeded, result.StandardError);
    }

}
