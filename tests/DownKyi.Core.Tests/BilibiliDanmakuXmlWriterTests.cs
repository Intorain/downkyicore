using System.Globalization;
using System.Xml.Linq;
using DownKyi.Core.BiliApi.DanmakuApi;
using DownKyi.Core.BiliApi.DanmakuApi.Models;

namespace DownKyi.Core.Tests;

public sealed class BilibiliDanmakuXmlWriterTests
{
    [Fact]
    public void WritesBilibiliEightFieldMetadataAndRoundTripsContent()
    {
        var originalCulture = CultureInfo.CurrentCulture;
        CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("fr-FR");
        try
        {
            var output = BilibiliDanmakuXmlWriter.CreateDocument(
                [
                    new BiliDanmaku
                    {
                        Id = 987654321,
                        Progress = 1_234,
                        Mode = 7,
                        Fontsize = 30,
                        Color = 16_711_680,
                        Ctime = 1_700_000_000,
                        Pool = 1,
                        MidHash = "abc123ef",
                        Weight = 99,
                        Content = "中文\n<>&\"'"
                    }
                ],
                TestContext.Current.CancellationToken);
            var document = XDocument.Parse(
                output.ToString(SaveOptions.DisableFormatting),
                LoadOptions.PreserveWhitespace);
            Assert.Equal("i", document.Root?.Name.LocalName);
            var element = Assert.Single(document.Root!.Elements("d"));
            Assert.Equal("中文\n<>&\"'", element.Value);
            Assert.Equal(
                "1.234,7,30,16711680,1700000000,1,abc123ef,987654321",
                element.Attribute("p")?.Value);
            Assert.Equal(8, element.Attribute("p")!.Value.Split(',').Length);
            Assert.DoesNotContain(",99,", element.Attribute("p")!.Value, StringComparison.Ordinal);
        }
        finally
        {
            CultureInfo.CurrentCulture = originalCulture;
        }
    }

    [Fact]
    public async Task CancellationIsVisibleBeforeXmlGeneration()
    {
        using var cancellation = new CancellationTokenSource();
        await cancellation.CancelAsync();

        Assert.ThrowsAny<OperationCanceledException>(() =>
            BilibiliDanmakuXmlWriter.CreateDocument([], cancellation.Token));
    }
}
