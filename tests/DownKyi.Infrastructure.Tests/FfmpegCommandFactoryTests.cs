using DownKyi.Application.Media;
using DownKyi.Infrastructure.FFmpeg;

namespace DownKyi.Infrastructure.Tests;

public sealed class FfmpegCommandFactoryTests
{
    [Fact]
    public void GenericMergePreservesOptionalAudioBehavior()
    {
        var command = FfmpegCommandFactory.BuildMerge(
            audioFile: null,
            videoFile: "segment.flv",
            outputFile: "output.mp4",
            transcodeAudioToMp3: false,
            embeddedAudioMode: FfmpegEmbeddedAudioMode.Optional);

        Assert.Contains("0:a?", command.Arguments);
        Assert.DoesNotContain("0:a:0", command.Arguments);
        Assert.DoesNotContain("-an", command.Arguments);
    }

    [Fact]
    public void MergeVideoOnlyContractDisablesAudioFromDurlInput()
    {
        var command = FfmpegCommandFactory.BuildMerge(
            audioFile: null,
            videoFile: "segment.flv",
            outputFile: "output.mp4",
            transcodeAudioToMp3: false,
            embeddedAudioMode: FfmpegEmbeddedAudioMode.Excluded);

        Assert.Contains("-an", command.Arguments);
        Assert.DoesNotContain("0:a:0", command.Arguments);
        Assert.DoesNotContain("0:a?", command.Arguments);
    }

    [Fact]
    public void MergeAudioVideoContractRequiresAudioFromDurlInput()
    {
        var command = FfmpegCommandFactory.BuildMerge(
            audioFile: null,
            videoFile: "segment.flv",
            outputFile: "output.mp4",
            transcodeAudioToMp3: false,
            embeddedAudioMode: FfmpegEmbeddedAudioMode.Required);

        Assert.Contains("0:a:0", command.Arguments);
        Assert.DoesNotContain("0:a?", command.Arguments);
        Assert.DoesNotContain("-an", command.Arguments);
    }

    [Fact]
    public void ConcatVideoOnlyContractDisablesAudioFromDurlInputs()
    {
        var command = FfmpegCommandFactory.BuildConcat(
            "segments.txt",
            "output.mp4",
            FfmpegConcatStrategy.StreamCopy,
            hardwareEncoder: null,
            embeddedAudioMode: FfmpegEmbeddedAudioMode.Excluded);

        Assert.Contains("-an", command.Arguments);
        Assert.DoesNotContain("0:a:0", command.Arguments);
        Assert.DoesNotContain("0:a?", command.Arguments);
    }

    [Fact]
    public void GenericConcatPreservesOptionalAudioBehavior()
    {
        var command = FfmpegCommandFactory.BuildConcat(
            "segments.txt",
            "output.mp4",
            FfmpegConcatStrategy.StreamCopy,
            hardwareEncoder: null,
            embeddedAudioMode: FfmpegEmbeddedAudioMode.Optional);

        Assert.Contains("0:a?", command.Arguments);
        Assert.DoesNotContain("0:a:0", command.Arguments);
        Assert.DoesNotContain("-an", command.Arguments);
    }

    [Fact]
    public void ConcatTreatsProcessingErrorsAsFatal()
    {
        var command = FfmpegCommandFactory.BuildConcat(
            "segments.txt",
            "output.mp4",
            FfmpegConcatStrategy.CpuEncoder,
            hardwareEncoder: null,
            embeddedAudioMode: FfmpegEmbeddedAudioMode.Optional);

        Assert.Contains("-xerror", command.Arguments);
    }

    [Fact]
    public void MediaMuxCommandsRejectEmptyOutputStreams()
    {
        var merge = FfmpegCommandFactory.BuildMerge(
            audioFile: "audio.m4s",
            videoFile: "video.m4s",
            outputFile: "merged.mp4",
            transcodeAudioToMp3: false,
            embeddedAudioMode: FfmpegEmbeddedAudioMode.Required);
        var concat = FfmpegCommandFactory.BuildConcat(
            "segments.txt",
            "concatenated.mp4",
            FfmpegConcatStrategy.CpuEncoder,
            hardwareEncoder: null,
            embeddedAudioMode: FfmpegEmbeddedAudioMode.Required);

        Assert.Contains("-abort_on", merge.Arguments);
        Assert.Contains("empty_output_stream", merge.Arguments);
        Assert.Contains("-abort_on", concat.Arguments);
        Assert.Contains("empty_output_stream", concat.Arguments);
    }

    [Fact]
    public void ConcatAudioVideoContractRequiresAudioFromDurlInputs()
    {
        var command = FfmpegCommandFactory.BuildConcat(
            "segments.txt",
            "output.mp4",
            FfmpegConcatStrategy.StreamCopy,
            hardwareEncoder: null,
            embeddedAudioMode: FfmpegEmbeddedAudioMode.Required);

        Assert.Contains("0:a:0", command.Arguments);
        Assert.DoesNotContain("0:a?", command.Arguments);
        Assert.DoesNotContain("-an", command.Arguments);
    }

}
