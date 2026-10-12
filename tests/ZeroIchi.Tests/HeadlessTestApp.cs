using Avalonia;
using Avalonia.Headless;
using Avalonia.Media.Fonts;
using ZeroIchi.Tests;

[assembly: AvaloniaTestApplication(typeof(HeadlessTestApp))]

namespace ZeroIchi.Tests;

public sealed class HeadlessTestApp : Application
{
    public const string Collection = "Headless";

    public static AppBuilder BuildAvaloniaApp()
        => AppBuilder.Configure<HeadlessTestApp>()
            .UseHeadless(new AvaloniaHeadlessPlatformOptions { UseHeadlessDrawing = true })
            .ConfigureFonts(fontManager =>
            {
                fontManager.AddFontCollection(new EmbeddedFontCollection(
                    new Uri("fonts:App", UriKind.Absolute),
                    new Uri("avares://ZeroIchi/Assets/Fonts", UriKind.Absolute)));
            });

    public static Task RunAsync(Func<Task> action)
        => HeadlessUnitTestSession.GetOrStartForAssembly(typeof(HeadlessTestApp).Assembly)
            .Dispatch(async () =>
            {
                await action();
                return true;
            }, CancellationToken.None);
}

// Avalonia makes whichever thread creates a dispatcher first the UI thread, so headless tests
// must not run alongside other tests that create Avalonia objects
[CollectionDefinition(HeadlessTestApp.Collection, DisableParallelization = true)]
public sealed class HeadlessCollection;
