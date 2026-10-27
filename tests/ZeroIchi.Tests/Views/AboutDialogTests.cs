using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;
using ZeroIchi.Models;
using ZeroIchi.Views;

namespace ZeroIchi.Tests.Views;

[Collection(HeadlessTestApp.Collection)]
public sealed class AboutDialogTests
{
    private static (AboutDialog Dialog, Func<bool> Closed) Show()
    {
        var dialog = new AboutDialog();
        var closed = false;
        dialog.Closed += (_, _) => closed = true;
        dialog.Show();
        Dispatcher.UIThread.RunJobs();
        return (dialog, () => closed);
    }

    [Fact]
    public Task Opens_WithTheAppVersion() => HeadlessTestApp.RunAsync(() =>
    {
        var (dialog, _) = Show();

        Assert.Equal($"バージョン {AppVersion.Current}", dialog.VersionText.Text);

        dialog.Close();
        return Task.CompletedTask;
    });

    [Fact]
    public void RepositoryUri_PointsToTheRepository()
    {
        Assert.Equal(new Uri("https://github.com/oruponu/ZeroIchi"), AboutDialog.RepositoryUri);
    }

    [Fact]
    public Task RepositoryLink_LeavesNavigationToTheDialog() => HeadlessTestApp.RunAsync(() =>
    {
        var (dialog, _) = Show();

        Assert.Null(dialog.RepositoryLink.NavigateUri);

        dialog.Close();
        return Task.CompletedTask;
    });

    [Fact]
    public Task Ok_ClosesTheDialog() => HeadlessTestApp.RunAsync(async () =>
    {
        var (dialog, closed) = Show();

        dialog.OkButton.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));

        await HeadlessTestApp.WaitUntilAsync(closed);
    });

    [Fact]
    public Task Escape_ClosesTheDialog() => HeadlessTestApp.RunAsync(async () =>
    {
        var (dialog, closed) = Show();

        dialog.KeyPress(Key.Escape, RawInputModifiers.None, PhysicalKey.Escape, null);

        await HeadlessTestApp.WaitUntilAsync(closed);
    });
}
