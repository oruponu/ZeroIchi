using Avalonia.Threading;
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
}
