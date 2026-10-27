using DownKyi.Utils;

namespace DownKyi.Desktop.Tests;

public sealed class DictionaryResourceTests
{
    [AvaloniaFact]
    public async Task MissingLanguageKeyFailsAfterApplicationInitialization()
    {
        await AvaloniaTestDispatcher.RunAsync(() =>
        {
            DesktopTestResources.EnsureDownloadProjectionResources();

            Assert.Throws<KeyNotFoundException>(() =>
                DictionaryResource.GetString("MissingLanguageKey"));
            Assert.Equal(
                "fallback",
                DictionaryResource.GetString("MissingLanguageKey", "fallback"));

            return Task.CompletedTask;
        }).ConfigureAwait(true);
    }
}
