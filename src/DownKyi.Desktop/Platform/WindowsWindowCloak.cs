using System;
using System.Runtime.InteropServices;
using Avalonia.Controls;

namespace DownKyi.Platform;

internal static class WindowsWindowCloak
{
    private const int DwmWindowAttributeCloak = 13;

    internal static bool TrySetCloaked(Window window, bool cloaked)
    {
        ArgumentNullException.ThrowIfNull(window);
        if (!OperatingSystem.IsWindows())
        {
            return false;
        }

        var handle = window.TryGetPlatformHandle();
        if (handle is null
            || !string.Equals(handle.HandleDescriptor, "HWND", StringComparison.Ordinal))
        {
            return false;
        }

        var value = cloaked ? 1 : 0;
        return SafeNativeMethods.DwmSetWindowAttribute(
            handle.Handle,
            DwmWindowAttributeCloak,
            ref value,
            sizeof(int)) >= 0;
    }

    private static class SafeNativeMethods
    {
        [DllImport("dwmapi.dll")]
        [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
        internal static extern int DwmSetWindowAttribute(
            nint windowHandle,
            int attribute,
            ref int attributeValue,
            int attributeSize);
    }
}
