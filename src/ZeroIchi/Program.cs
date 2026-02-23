using Avalonia;
using Avalonia.Media.Fonts;
using System;

namespace ZeroIchi;

sealed class Program
{
    // Initialization code. Don't use any Avalonia, third-party APIs or any
    // SynchronizationContext-reliant code before AppMain is called: things aren't initialized
    // yet and stuff might break.
    [STAThread]
    public static void Main(string[] args) => BuildAvaloniaApp()
        .StartWithClassicDesktopLifetime(args);

    // Avalonia configuration, don't remove; also used by visual designer.
    public static AppBuilder BuildAvaloniaApp()
        => AppBuilder.Configure<App>()
            .UsePlatformDetect()
            .WithInterFont()
            .ConfigureFonts(fontManager =>
            {
                fontManager.AddFontCollection(new EmbeddedFontCollection(
                    new Uri("fonts:App", UriKind.Absolute),
                    new Uri("avares://ZeroIchi/Assets/Fonts", UriKind.Absolute)));
            })
            .LogToTrace();
}
