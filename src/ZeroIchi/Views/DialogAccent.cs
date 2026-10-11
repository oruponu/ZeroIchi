using Avalonia.Controls;
using Avalonia.Media;

namespace ZeroIchi.Views;

internal static class DialogAccent
{
    public static void ApplyButtonForeground(Window dialog)
    {
        var foreground = Brushes.White;

        if (dialog.TryFindResource("SystemAccentColor", dialog.ActualThemeVariant, out var obj)
            && obj is Color accent)
        {
            var luminance = 0.2126 * (accent.R / 255.0)
                          + 0.7152 * (accent.G / 255.0)
                          + 0.0722 * (accent.B / 255.0);
            foreground = luminance > 0.5 ? Brushes.Black : Brushes.White;
        }

        dialog.Resources["AccentButtonForeground"] = foreground;
    }
}
