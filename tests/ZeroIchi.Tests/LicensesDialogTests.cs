using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.Threading;
using ZeroIchi.Models;
using ZeroIchi.Views;

namespace ZeroIchi.Tests;

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
    public Task Escape_ClosesTheDialog() => HeadlessTestApp.RunAsync(() =>
    {
        var dialog = Show();
        var closed = false;
        dialog.Closed += (_, _) => closed = true;

        dialog.KeyPress(Key.Escape, RawInputModifiers.None, PhysicalKey.Escape, null);
        Dispatcher.UIThread.RunJobs();

        Assert.True(closed);
        return Task.CompletedTask;
    });
}
