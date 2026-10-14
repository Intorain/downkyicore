using DownKyi.Services.Download;

namespace DownKyi.Tests;

public sealed class DownloadBandwidthLimiterTests
{
    [Fact]
    public async Task OverallLimitIsSharedAcrossTransfers()
    {
        var clock = new ManualTimeProvider(DateTimeOffset.UnixEpoch);
        var delays = new List<TimeSpan>();
        var limiter = CreateLimiter(
            new DownloadBandwidthLimits(10 * 1024, 0),
            clock,
            delays);
        var firstTransfer = limiter.CreateTransferLease();
        var secondTransfer = limiter.CreateTransferLease();

        var first = await firstTransfer.AcquireAsync(
            4096,
            TestContext.Current.CancellationToken);
        var second = await secondTransfer.AcquireAsync(
            4096,
            TestContext.Current.CancellationToken);

        Assert.Equal(1024, first.ByteCount);
        Assert.Equal(1024, second.ByteCount);
        Assert.Equal(TimeSpan.FromMilliseconds(100), Assert.Single(delays));
    }

    [Fact]
    public async Task TransferLimitIsSharedByItsWorkersButNotOtherTransfers()
    {
        var clock = new ManualTimeProvider(DateTimeOffset.UnixEpoch);
        var delays = new List<TimeSpan>();
        var limiter = CreateLimiter(
            new DownloadBandwidthLimits(0, 5 * 1024),
            clock,
            delays);
        var firstTransfer = limiter.CreateTransferLease();
        var secondTransfer = limiter.CreateTransferLease();

        var firstWorker = await firstTransfer.AcquireAsync(
            4096,
            TestContext.Current.CancellationToken);
        var secondWorker = await firstTransfer.AcquireAsync(
            4096,
            TestContext.Current.CancellationToken);
        var otherTransfer = await secondTransfer.AcquireAsync(
            4096,
            TestContext.Current.CancellationToken);

        Assert.Equal(512, firstWorker.ByteCount);
        Assert.Equal(512, secondWorker.ByteCount);
        Assert.Equal(512, otherTransfer.ByteCount);
        Assert.Equal(TimeSpan.FromMilliseconds(100), Assert.Single(delays));
    }

    [Fact]
    public async Task LowerOfOverallAndTransferLimitsOwnsTheReadGrant()
    {
        var limiter = CreateLimiter(
            new DownloadBandwidthLimits(20 * 1024, 5 * 1024),
            new ManualTimeProvider(DateTimeOffset.UnixEpoch),
            []);
        var transfer = limiter.CreateTransferLease();

        var permit = await transfer.AcquireAsync(
            4096,
            TestContext.Current.CancellationToken);

        Assert.Equal(512, permit.ByteCount);
    }

    [Fact]
    public async Task UnreadBytesAreReturnedToBothBuckets()
    {
        var delays = new List<TimeSpan>();
        var limiter = CreateLimiter(
            new DownloadBandwidthLimits(10 * 1024, 10 * 1024),
            new ManualTimeProvider(DateTimeOffset.UnixEpoch),
            delays);
        var transfer = limiter.CreateTransferLease();
        var first = await transfer.AcquireAsync(
            4096,
            TestContext.Current.CancellationToken);

        transfer.ReturnUnused(first, unusedBytes: 512);
        var second = await transfer.AcquireAsync(
            512,
            TestContext.Current.CancellationToken);

        Assert.Equal(512, second.ByteCount);
        Assert.Empty(delays);
    }

    [Fact]
    public async Task CancellationInterruptsAThrottledWait()
    {
        var waiting = new TaskCompletionSource(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var limiter = new DownloadBandwidthLimiter(
            static () => new DownloadBandwidthLimits(10 * 1024, 0),
            TimeProvider.System,
            (_, cancellationToken) =>
            {
                waiting.TrySetResult();
                return Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
            });
        var transfer = limiter.CreateTransferLease();
        await transfer.AcquireAsync(4096, TestContext.Current.CancellationToken);
        using var cancellation = new CancellationTokenSource();

        var blocked = transfer.AcquireAsync(4096, cancellation.Token).AsTask();
        await waiting.Task.WaitAsync(TestContext.Current.CancellationToken);
        await cancellation.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => blocked);
    }

    private static DownloadBandwidthLimiter CreateLimiter(
        DownloadBandwidthLimits limits,
        ManualTimeProvider clock,
        List<TimeSpan> delays)
    {
        return new DownloadBandwidthLimiter(
            () => limits,
            clock,
            (delay, cancellationToken) =>
            {
                cancellationToken.ThrowIfCancellationRequested();
                delays.Add(delay);
                clock.Advance(delay);
                return Task.CompletedTask;
            });
    }

    private sealed class ManualTimeProvider(DateTimeOffset utcNow) : TimeProvider
    {
        private readonly Lock _sync = new();
        private DateTimeOffset _utcNow = utcNow;
        private long _timestamp;

        public override long TimestampFrequency => TimeSpan.TicksPerSecond;

        public override DateTimeOffset GetUtcNow()
        {
            lock (_sync)
            {
                return _utcNow;
            }
        }

        public override long GetTimestamp()
        {
            lock (_sync)
            {
                return _timestamp;
            }
        }

        public void Advance(TimeSpan elapsed)
        {
            lock (_sync)
            {
                _utcNow += elapsed;
                _timestamp += elapsed.Ticks;
            }
        }
    }
}
