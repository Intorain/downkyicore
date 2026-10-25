using System.Collections.Generic;
using System.Globalization;
using System.Text;
using Avalonia.Threading;

namespace DownKyi.Utils;

internal static class DictionaryResource
{
    /// <summary>
    /// 从资源获取字符串
    /// </summary>
    /// <param name="resourceKey"></param>
    /// <returns></returns>
    public static string GetString(string resourceKey)
    {
        var application = Avalonia.Application.Current;
        if (application == null)
        {
            return string.Empty;
        }

        var obj = Dispatcher.UIThread.Invoke(() =>
        {
            object? obj = null;
            application.TryGetResource(
                resourceKey,
                application.ActualThemeVariant,
                out obj);
            return obj;
        });
        return obj == null ? "" : (string)obj;
    }

    public static string GetString(string resourceKey, string fallback)
    {
        var value = GetString(resourceKey);
        return string.IsNullOrEmpty(value) ? fallback : value;
    }

    public static string Format(string resourceKey, string fallback, params object?[] arguments)
    {
        var format = GetString(resourceKey, fallback);
        return string.Format(
            CultureInfo.CurrentCulture,
            CompositeFormat.Parse(format),
            arguments);
    }

    public static T Get<T>(string resourceKey)
    {
        var application = Avalonia.Application.Current
            ?? throw new KeyNotFoundException($"Resource '{resourceKey}' is unavailable before application initialization.");
        var obj = Dispatcher.UIThread.Invoke(() =>
        {
            object? obj = null;
            application.TryGetResource(
                resourceKey,
                application.ActualThemeVariant,
                out obj);
            return obj;
        });
        return obj is T value
            ? value
            : throw new KeyNotFoundException($"Resource '{resourceKey}' was not found or is not a {typeof(T).Name}.");
    }

    public static T? GetIfApplicationInitialized<T>(string resourceKey)
        where T : class
    {
        if (Avalonia.Application.Current == null)
        {
            return null;
        }

        return Get<T>(resourceKey);
    }
}
