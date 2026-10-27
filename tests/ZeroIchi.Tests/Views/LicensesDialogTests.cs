using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Media.Immutable;
using Avalonia.Themes.Fluent;
using Avalonia.Threading;
using Avalonia.VisualTree;
using ZeroIchi.Models;
using ZeroIchi.Views;

namespace ZeroIchi.Tests.Views;

[Collection(HeadlessTestApp.Collection)]
public sealed class LicensesDialogTests
{
    private static LicensesDialog Show()
    {
        var dialog = new LicensesDialog();
        dialog.Show();
        Dispatcher.UIThread.RunJobs();
        return dialog;
    }

    [Fact]
    public Task Selecting_AnotherNotice_ScrollsTheBodyBackToTheStart() => HeadlessTestApp.RunAsync(async () =>
    {
        var theme = new FluentTheme();
        Application.Current!.Styles.Add(theme);
        try
        {
            var dialog = Show();
            var scrollViewer = dialog.BodyText.GetVisualDescendants().OfType<ScrollViewer>().First();
            dialog.NoticeList.SelectedIndex = dialog.NoticeList.ItemCount - 1;
            Dispatcher.UIThread.RunJobs();
            scrollViewer.Offset = new Vector(50, 20000);
            Dispatcher.UIThread.RunJobs();
            Assert.NotEqual(default, scrollViewer.Offset);

            dialog.NoticeList.SelectedIndex = 1;
            Dispatcher.UIThread.RunJobs();

            Assert.Equal(default, scrollViewer.Offset);
            await HeadlessTestApp.CloseAsync(dialog);
        }
        finally
        {
            Application.Current!.Styles.Remove(theme);
        }
    });

    [Fact]
    public Task FocusingTheBody_KeepsItsBackgroundAndBorderHidden() => HeadlessTestApp.RunAsync(async () =>
    {
        var theme = new FluentTheme();
        Application.Current!.Styles.Add(theme);
        try
        {
            var dialog = Show();
            var border = dialog.BodyText.GetVisualDescendants().OfType<Border>().First(b => b.Name == "PART_BorderElement");

            dialog.BodyText.Focus();
            Dispatcher.UIThread.RunJobs();

            Assert.True(dialog.BodyText.IsFocused);
            Assert.Equal(Colors.Transparent, Assert.IsType<ImmutableSolidColorBrush>(border.Background?.ToImmutable()).Color);
            Assert.Equal(default, border.BorderThickness);
            await HeadlessTestApp.CloseAsync(dialog);
        }
        finally
        {
            Application.Current!.Styles.Remove(theme);
        }
    });

    [Fact]
    public Task Opens_WithEveryNoticeListedAndTheFirstSelected() => HeadlessTestApp.RunAsync(() =>
    {
        var notices = ThirdPartyNotices.Load().Notices;
        var dialog = Show();

        Assert.Equal(notices.Count, dialog.NoticeList.ItemCount);
        Assert.Equal(0, dialog.NoticeList.SelectedIndex);
        Assert.Equal(notices[0].Body, dialog.BodyText.Text);

        dialog.Close();
        return Task.CompletedTask;
    });

    [Fact]
    public Task Selecting_AnotherNotice_ShowsItsBody() => HeadlessTestApp.RunAsync(() =>
    {
        var notices = ThirdPartyNotices.Load().Notices;
        var dialog = Show();

        dialog.NoticeList.SelectedIndex = 1;
        Dispatcher.UIThread.RunJobs();

        Assert.Equal(notices[1].Body, dialog.BodyText.Text);

        dialog.Close();
        return Task.CompletedTask;
    });

    [Fact]
    public Task Opens_WithThePreamble() => HeadlessTestApp.RunAsync(() =>
    {
        var dialog = Show();

        Assert.Equal(ThirdPartyNotices.Load().Preamble, dialog.PreambleText.Text);

        dialog.Close();
        return Task.CompletedTask;
    });

    [Fact]
    public Task Escape_ClosesTheDialog() => HeadlessTestApp.RunAsync(async () =>
    {
        var dialog = Show();
        var closed = false;
        dialog.Closed += (_, _) => closed = true;

        dialog.KeyPress(Key.Escape, RawInputModifiers.None, PhysicalKey.Escape, null);

        await HeadlessTestApp.WaitUntilAsync(() => closed);
    });
}
