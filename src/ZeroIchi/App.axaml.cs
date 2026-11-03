using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Avalonia.Media;
using Microsoft.Extensions.DependencyInjection;
using System.Collections.Generic;
using System.Globalization;
using System.Threading.Tasks;
using ZeroIchi.Infrastructure;
using ZeroIchi.Localization;
using ZeroIchi.Models;
using ZeroIchi.Models.FileStructure.Ksy;
using ZeroIchi.Services;
using ZeroIchi.ViewModels;
using ZeroIchi.Views;

namespace ZeroIchi;

public sealed partial class App : Application
{
    private static readonly int[] CjkFallbackCodepoints = ['漢', 'あ', 'ア', 'ㄅ', '한'];

    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        _ = Task.Run(KsyFormatRegistry.Default.Preload);
        _ = Task.Run(MainWindowViewModel.WarmUpFileTypeDetection);
        var settings = new SettingsManager(new JsonSettingsStore(JsonSettingsStore.DefaultPath));
        Localizer.Instance.SetCulture(LanguageSetting.Resolve(settings.Current.Language));
        ApplyDefaultFontFamily();
        Localizer.Instance.CultureChanged += (_, _) => ApplyDefaultFontFamily();
        TextFlyoutStrings.Attach(Resources);
        PopupCorners.Register();

        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            var services = new ServiceCollection();
            services.AddSingleton(settings);
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

    internal static CultureInfo SelectFontFallbackCulture(CultureInfo displayCulture, CultureInfo systemCulture) =>
        displayCulture.TwoLetterISOLanguageName == "ja" ? displayCulture : systemCulture;

    private void ApplyDefaultFontFamily()
    {
        var fontFamily = CreateDefaultFontFamily(
            SelectFontFallbackCulture(Localizer.Instance.Culture, CultureInfo.CurrentUICulture));
        Resources["DefaultFontFamily"] = fontFamily;
        Resources["ContentControlThemeFontFamily"] = fontFamily;
    }

    private static FontFamily CreateDefaultFontFamily(CultureInfo culture)
    {
        var names = new List<string> { "Inter" };
        foreach (var codepoint in CjkFallbackCodepoints)
        {
            if (FontManager.Current.TryMatchCharacter(
                    codepoint, FontStyle.Normal, FontWeight.Normal, FontStretch.Normal,
                    null, culture, out var matched)
                && !names.Contains(matched.FontFamily.Name))
            {
                names.Add(matched.FontFamily.Name);
            }
        }

        return new FontFamily(string.Join(", ", names));
    }
}
