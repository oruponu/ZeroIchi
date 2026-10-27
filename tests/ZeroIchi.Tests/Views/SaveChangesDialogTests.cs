using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Threading;
using ZeroIchi.Services;
using ZeroIchi.Views;

namespace ZeroIchi.Tests.Views;

[Collection(HeadlessTestApp.Collection)]
public sealed class SaveChangesDialogTests
{
    private static SaveChangesDialog Show()
    {
        var dialog = new SaveChangesDialog();
        dialog.Show();
        Dispatcher.UIThread.RunJobs();
        return dialog;
    }

    [Fact]
    public Task Opens_WithTheSaveButtonAccented() => HeadlessTestApp.RunAsync(() =>
    {
        var dialog = Show();

        Assert.Contains("accent", dialog.SaveButton.Classes);

        dialog.Close();
        return Task.CompletedTask;
    });

    [Fact]
    public Task FocusingAnotherButton_RemovesTheAccentFromTheSaveButton() => HeadlessTestApp.RunAsync(() =>
    {
        var dialog = Show();

        dialog.DiscardButton.Focus();
        Dispatcher.UIThread.RunJobs();

        Assert.DoesNotContain("accent", dialog.SaveButton.Classes);

        dialog.Close();
        return Task.CompletedTask;
    });

    [Fact]
    public Task FocusingTheSaveButtonAgain_RestoresTheAccent() => HeadlessTestApp.RunAsync(() =>
    {
        var dialog = Show();

        dialog.CancelButton.Focus();
        Dispatcher.UIThread.RunJobs();
        dialog.SaveButton.Focus();
        Dispatcher.UIThread.RunJobs();

        Assert.Contains("accent", dialog.SaveButton.Classes);

        dialog.Close();
        return Task.CompletedTask;
    });

    [Fact]
    public Task Opens_FadingInTheDialog() => HeadlessTestApp.RunAsync(async () =>
    {
        var dialog = Show();
        var surface = (Control)dialog.Content!;

        Assert.True(surface.Opacity < 1);
        await HeadlessTestApp.WaitUntilAsync(() => surface.Opacity == 1);

        dialog.Close();
    });

    [Fact]
    public Task Choosing_WaitsForTheCloseAnimationAndKeepsTheResult() => HeadlessTestApp.RunAsync(async () =>
    {
        var owner = new Window();
        owner.Show();
        var result = SaveChangesDialog.ShowAsync(owner);
        Dispatcher.UIThread.RunJobs();
        var dialog = Assert.IsType<SaveChangesDialog>(Assert.Single(owner.OwnedWindows));
        await HeadlessTestApp.WaitUntilAsync(() => ((Control)dialog.Content!).Opacity == 1);

        dialog.DiscardButton.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        Dispatcher.UIThread.RunJobs();

        Assert.False(result.IsCompleted);
        Assert.False(((Control)dialog.Content!).IsHitTestVisible);
        await HeadlessTestApp.WaitUntilAsync(() => result.IsCompleted);
        Assert.Equal(SaveChangesResult.Discard, await result);

        owner.Close();
    });
}
