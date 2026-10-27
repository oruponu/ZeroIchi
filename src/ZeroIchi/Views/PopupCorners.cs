using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using System;
using System.Runtime.InteropServices;

namespace ZeroIchi.Views;

internal static class PopupCorners
{
    private const int DwmwaWindowCornerPreference = 33;
    private const int DwmwcpRound = 2;

    public static void Register()
    {
        if (!OperatingSystem.IsWindowsVersionAtLeast(10, 0, 22000)) return;

        Control.LoadedEvent.AddClassHandler<PopupRoot>((root, _) => Round(root));
    }

    private static void Round(PopupRoot root)
    {
        if (root.TryGetPlatformHandle() is not { } handle) return;

        var preference = DwmwcpRound;
        DwmSetWindowAttribute(handle.Handle, DwmwaWindowCornerPreference, ref preference, sizeof(int));
    }

    [DllImport("dwmapi.dll")]
    private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attribute, ref int value, int size);
}
