using DownKyi.Core.FFmpeg;

namespace DownKyi.Tests;

internal sealed class StubFfmpegMediaStreamValidator(bool result = true)
    : IFfmpegMediaStreamValidator
{
    public List<ValidationCall> Calls { get; } = [];

    public Task<bool> ValidateRequiredStreamsAsync(
        string mediaFile,
        bool requireAudio,
        bool requireVideo,
        CancellationToken cancellationToken = default) =>
        ValidateRequiredStreamsAsync(
            mediaFile,
            requireAudio,
            requireVideo,
            TimeSpan.Zero,
            cancellationToken);

    public Task<bool> ValidateRequiredStreamsAsync(
        string mediaFile,
        bool requireAudio,
        bool requireVideo,
        TimeSpan expectedVideoDuration,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        Calls.Add(new ValidationCall(
            mediaFile,
            requireAudio,
            requireVideo,
            expectedVideoDuration));
        return Task.FromResult(result);
    }

    internal sealed record ValidationCall(
        string MediaFile,
        bool RequireAudio,
        bool RequireVideo,
        TimeSpan ExpectedVideoDuration);
}
