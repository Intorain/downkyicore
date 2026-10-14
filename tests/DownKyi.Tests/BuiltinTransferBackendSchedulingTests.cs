using DownKyi.Services.Download;

namespace DownKyi.Tests;

public sealed class BuiltinTransferBackendSchedulingTests
{
    [Theory]
    [InlineData(8 * 1024 * 1024, 4, 4)]
    [InlineData(20 * 1024 * 1024, 4, 4)]
    [InlineData(20 * 1024 * 1024, 8, 8)]
    [InlineData(64 * 1024 * 1024, 4, 4)]
    [InlineData(100 * 1024 * 1024, 4, 5)]
    public void KnownSizeReportsTheAdaptiveRangeQueue(
        long expectedBytes,
        int parallelCount,
        int expectedSegmentCount)
    {
        var segmentCount = BuiltinRangeDownloader.CalculateAdaptiveSegmentCount(
            expectedBytes,
            parallelCount);

        Assert.Equal(expectedSegmentCount, segmentCount);
    }

    [Fact]
    public void UnknownSizeIsReportedAfterRangeProbe()
    {
        var segmentCount = BuiltinRangeDownloader.CalculateAdaptiveSegmentCount(
            totalBytes: 0,
            parallelCount: 4);

        Assert.Equal(0, segmentCount);
    }

    [Theory]
    [InlineData(-1, 1, 1)]
    [InlineData(1, 0, 1)]
    [InlineData(1, 1, 0)]
    public void InvalidInputsAreRejected(long expectedBytes, int parallelCount, long segmentSize)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            BuiltinRangeDownloader.CalculateAdaptiveSegmentCount(
                expectedBytes,
                parallelCount,
                segmentSize));
    }
}
