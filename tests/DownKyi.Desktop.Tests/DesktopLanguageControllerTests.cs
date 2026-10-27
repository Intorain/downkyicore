using Avalonia.Styling;
using DownKyi.Application.Settings;
using DownKyi.Desktop.Appearance;
using DownKyi.Infrastructure.Settings;

namespace DownKyi.Desktop.Tests;

public sealed class DesktopLanguageControllerTests
{
    [AvaloniaFact]
    public async Task SavedLanguageSelectsItsResourceDictionaryAtStartup()
    {
        await AvaloniaTestDispatcher.RunAsync(async () =>
        {
            var application = DesktopTestResources.EnsureDownloadProjectionResources();
            var dictionaries = application.Resources.MergedDictionaries;
            var languageIndex = Enumerable.Range(0, dictionaries.Count).First(index =>
                dictionaries[index].TryGetResource(
                    "InterfaceLanguage",
                    ThemeVariant.Default,
                    out _));
            var original = dictionaries[languageIndex];
            var directory = Path.Combine(Path.GetTempPath(), $"downkyi-ui-language-{Guid.NewGuid():N}");
            var settingsPath = Path.Combine(directory, "settings.json");

            try
            {
                foreach (var (language, expectedLabel, expectedQualityTitle) in new[]
                         {
                             (UiLanguage.ChineseSimplified, "界面语言", "下载品质需要确认"),
                             (UiLanguage.ChineseTraditional, "介面語言", "下載品質需要確認"),
                             (UiLanguage.English, "Interface language", "Confirm download quality"),
                             (UiLanguage.Japanese, "表示言語", "ダウンロード品質を確認")
                         })
                {
                    using (var settings = new SettingsStore(settingsPath))
                    {
                        settings.Update(current => current with
                        {
                            Basic = current.Basic with { UiLanguage = language }
                        });
                        await settings.FlushAsync(TestContext.Current.CancellationToken).ConfigureAwait(true);
                    }

                    using var restored = new SettingsStore(settingsPath);
                    Assert.Equal(language, restored.Current.Basic.UiLanguage);
                    new DesktopLanguageController(restored).ApplySavedLanguage();
                    Assert.True(application.TryGetResource("InterfaceLanguage", ThemeVariant.Default, out var label));
                    Assert.Equal(expectedLabel, label);
                    Assert.True(application.TryGetResource(
                        "DownloadQualityBatchTitle",
                        ThemeVariant.Default,
                        out var qualityTitle));
                    Assert.Equal(expectedQualityTitle, qualityTitle);
                }
            }
            finally
            {
                dictionaries[languageIndex] = original;
                if (Directory.Exists(directory))
                {
                    Directory.Delete(directory, recursive: true);
                }
            }
        }).ConfigureAwait(true);
    }

    [AvaloniaFact]
    public async Task MaterializedLanguageDictionaryIsReplacedAtStartup()
    {
        await AvaloniaTestDispatcher.RunAsync(() =>
        {
            var application = DesktopTestResources.EnsureDownloadProjectionResources();
            var dictionaries = application.Resources.MergedDictionaries;
            var languageIndex = Enumerable.Range(0, dictionaries.Count).First(index =>
                dictionaries[index].TryGetResource(
                    "InterfaceLanguage",
                    ThemeVariant.Default,
                    out _));
            var original = dictionaries[languageIndex];
            var materialized = new Avalonia.Controls.ResourceDictionary
            {
                ["InterfaceLanguage"] = "compiled dictionary marker"
            };

            try
            {
                dictionaries[languageIndex] = materialized;

                using var settings = new SettingsStore(Path.Combine(
                    Path.GetTempPath(),
                    $"downkyi-ui-language-{Guid.NewGuid():N}",
                    "settings.json"));
                new DesktopLanguageController(settings).ApplySavedLanguage();

                Assert.NotSame(materialized, dictionaries[languageIndex]);
                Assert.True(application.TryGetResource(
                    "InterfaceLanguage",
                    ThemeVariant.Default,
                    out var label));
                Assert.Equal("界面语言", label);
            }
            finally
            {
                dictionaries[languageIndex] = original;
            }

            return Task.CompletedTask;
        }).ConfigureAwait(true);
    }
}
