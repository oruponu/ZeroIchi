using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Avalonia.Media;
using System.Collections.Generic;
using System.Globalization;
using ZeroIchi.ViewModels;
using ZeroIchi.Views;

namespace ZeroIchi;

public partial class App : Application
{
    private static readonly int[] CjkFallbackCodepoints = ['漢', 'あ', 'ア', 'ㄅ', '한'];

    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        Resources["DefaultFontFamily"] = CreateDefaultFontFamily();

        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            desktop.MainWindow = new MainWindow
            {
                DataContext = new MainWindowViewModel(),
            };
        }

        base.OnFrameworkInitializationCompleted();
    }

    private static FontFamily CreateDefaultFontFamily()
    {
        var names = new List<string> { "Inter" };
        foreach (var codepoint in CjkFallbackCodepoints)
        {
            if (FontManager.Current.TryMatchCharacter(
                    codepoint, FontStyle.Normal, FontWeight.Normal, FontStretch.Normal,
                    null, CultureInfo.CurrentUICulture, out var matched)
                && !names.Contains(matched.FontFamily.Name))
            {
                names.Add(matched.FontFamily.Name);
            }
        }

        return new FontFamily(string.Join(", ", names));
    }
}
