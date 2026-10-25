namespace DownKyi.Application.Media;

public sealed record FfmpegConcatSegment(int Order, string FilePath);

public enum FfmpegOperationFailureKind
{
    None,
    NoInput,
    ProcessUnavailable,
    ProcessFailure,
    Timeout,
    OutputInvalid,
    DestinationConflict,
    DestinationAccessDenied,
    OutputIoFailure
}

public sealed record FfmpegOperationResult
{
    public FfmpegOperationResult(
        bool succeeded,
        string? outputPath,
        string? failureReason)
        : this(
            succeeded,
            outputPath,
            failureReason,
            succeeded ? FfmpegOperationFailureKind.None : FfmpegOperationFailureKind.ProcessFailure)
    {
    }

    public FfmpegOperationResult(
        bool succeeded,
        string? outputPath,
        string? failureReason,
        FfmpegOperationFailureKind failureKind)
    {
        if (succeeded != (failureKind == FfmpegOperationFailureKind.None))
        {
            throw new ArgumentException(
                "Successful FFmpeg operations require a None failure kind and failed operations require a non-None kind.",
                nameof(failureKind));
        }

        Succeeded = succeeded;
        OutputPath = outputPath;
        FailureReason = failureReason;
        FailureKind = failureKind;
    }

    public bool Succeeded { get; }

    public string? OutputPath { get; }

    public string? FailureReason { get; }

    public FfmpegOperationFailureKind FailureKind { get; }

    public static FfmpegOperationResult Failure(string reason)
    {
        return Failure(reason, FfmpegOperationFailureKind.ProcessFailure);
    }

    public static FfmpegOperationResult Failure(
        string reason,
        FfmpegOperationFailureKind failureKind)
    {
        return new FfmpegOperationResult(
            false,
            null,
            reason,
            failureKind);
    }
}
