using Avalonia.Markup.Xaml.Styling;
using Avalonia.Styling;
using Avalonia.Threading;
using DownKyi.Application.Settings;

namespace DownKyi.Desktop.Appearance;

internal sealed class DesktopLanguageController
{
    private readonly ISettingsStore _settingsStore;

    public DesktopLanguageController(ISettingsStore settingsStore)
    {
        _settingsStore = settingsStore ?? throw new ArgumentNullException(nameof(settingsStore));
    }

    public void ApplySavedLanguage()
    {
        Dispatcher.UIThread.VerifyAccess();
        var application = Avalonia.Application.Current
            ?? throw new InvalidOperationException("The Avalonia application must be initialized before applying a language.");
        var dictionaries = application.Resources.MergedDictionaries;
        var languageDictionaryIndex = -1;
        for (var index = 0; index < dictionaries.Count; index++)
        {
            if (dictionaries[index].TryGetResource(
                    "InterfaceLanguage",
                    ThemeVariant.Default,
                    out _))
            {
                languageDictionaryIndex = index;
                break;
            }
        }

        if (languageDictionaryIndex < 0)
        {
            throw new InvalidOperationException("The desktop language resource dictionary is missing.");
        }

        var source = _settingsStore.Current.Basic.UiLanguage switch
        {
            UiLanguage.ChineseSimplified => "/Languages/zh-CN.axaml",
            UiLanguage.ChineseTraditional => "/Languages/zh-TW.axaml",
            UiLanguage.English => "/Languages/en-US.axaml",
            UiLanguage.Japanese => "/Languages/ja-JP.axaml",
            _ => throw new InvalidOperationException("The configured desktop language is unsupported.")
        };
        dictionaries[languageDictionaryIndex] = new ResourceInclude(new Uri("avares://DownKyi.Desktop/"))
        {
            Source = new Uri($"avares://DownKyi.Desktop{source}")
        };
    }
}
