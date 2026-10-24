using System.Text.Json;
using DownKyi.Application.FileName;
using DownKyi.Infrastructure.Settings.Models;

namespace DownKyi.Infrastructure.Tests;

public sealed class VideoSettingsContractTests
{
    [Fact]
    public void CollectionSettingsRoundTripWithoutChangingJsonShape()
    {
        var settings = new VideoSettings
        {
            HistoryVideoRootPaths = new[] { "D:/Videos", "E:/Archive" },
            FileNameParts = new[] { FileNamePart.MainTitle, FileNamePart.PageTitle }
        };

        var json = JsonSerializer.Serialize(settings);
        var restored = JsonSerializer.Deserialize<VideoSettings>(json);

        Assert.NotNull(restored);
        Assert.Equal(settings.HistoryVideoRootPaths, restored.HistoryVideoRootPaths);
        Assert.Equal(settings.FileNameParts, restored.FileNameParts);
        Assert.Contains("\"HistoryVideoRootPaths\"", json, StringComparison.Ordinal);
        Assert.Contains("\"FileNameParts\"", json, StringComparison.Ordinal);
        Assert.DoesNotContain("\"VideoParseType\"", json, StringComparison.Ordinal);
    }
}
