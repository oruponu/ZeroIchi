using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Avalonia.Media;
using Microsoft.Extensions.DependencyInjection;
using System.Collections.Generic;
using System.Globalization;
using ZeroIchi.Infrastructure;
using ZeroIchi.Services;
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
            var services = new ServiceCollection();
            services.AddSingleton<MainWindow>();
            services.AddSingleton<Window>(sp => sp.GetRequiredService<MainWindow>());
            services.AddSingleton<IShellWindow>(sp => sp.GetRequiredService<MainWindow>());
            services.AddSingleton<IDialogService, AvaloniaDialogService>();
            services.AddSingleton<IWindowService, AvaloniaWindowService>();
            services.AddSingleton<IClipboardService, AvaloniaClipboardService>();
            services.AddSingleton<MainWindowViewModel>();
            services.AddSingleton<ViewLocator>();

            var provider = services.BuildServiceProvider();
            DataTemplates.Add(provider.GetRequiredService<ViewLocator>());

            var window = provider.GetRequiredService<MainWindow>();
            window.DataContext = provider.GetRequiredService<MainWindowViewModel>();
            desktop.MainWindow = window;
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
