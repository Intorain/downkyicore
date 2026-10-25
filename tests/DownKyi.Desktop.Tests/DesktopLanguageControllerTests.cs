using Avalonia.Markup.Xaml.Styling;
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
                dictionaries[index] is ResourceInclude include &&
                include.Source?.OriginalString.Contains("/Languages/", StringComparison.Ordinal) == true);
            var original = dictionaries[languageIndex];
            var directory = Path.Combine(Path.GetTempPath(), $"downkyi-ui-language-{Guid.NewGuid():N}");
            var settingsPath = Path.Combine(directory, "settings.json");

            try
            {
                foreach (var (language, expectedLabel) in new[]
                         {
                             (UiLanguage.ChineseSimplified, "界面语言"),
                             (UiLanguage.ChineseTraditional, "介面語言"),
                             (UiLanguage.English, "Interface language"),
                             (UiLanguage.Japanese, "表示言語")
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
}
