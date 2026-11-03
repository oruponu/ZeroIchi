using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;
using System.Globalization;
using ZeroIchi.Localization;
using ZeroIchi.Models;
using ZeroIchi.ViewModels;
using ZeroIchi.Views;

namespace ZeroIchi.Tests.Views;

[Collection(HeadlessTestApp.Collection)]
public sealed class SettingsDialogTests
{
    private static (SettingsDialog Dialog, SettingsDialogViewModel Vm, Func<bool> Closed) Show()
    {
        var vm = new SettingsDialogViewModel(new SettingsManager(new StubSettingsStore()), (_, _) => Task.CompletedTask);
        var dialog = new SettingsDialog { DataContext = vm };
        var closed = false;
        dialog.Closed += (_, _) => closed = true;
        dialog.Show();
        Dispatcher.UIThread.RunJobs();
        return (dialog, vm, () => closed);
    }

    [Fact]
    public Task Opens_WithTheCurrentLanguageSelected() => HeadlessTestApp.RunAsync(async () =>
    {
        var (dialog, vm, _) = Show();

        Assert.Same(vm.Languages[0], dialog.LanguageComboBox.SelectedItem);

        await HeadlessTestApp.CloseAsync(dialog);
        vm.Dispose();
    });

    [Fact]
    public Task SelectingEnglish_SwitchesTheDialogText() => HeadlessTestApp.RunAsync(async () =>
    {
        var (dialog, vm, _) = Show();
        try
        {
            dialog.LanguageComboBox.SelectedItem = vm.Languages[2];
            Dispatcher.UIThread.RunJobs();

            Assert.Equal("Settings", dialog.Title);
            Assert.Equal("Close", dialog.CloseButton.Content);
        }
        finally
        {
            Localizer.Instance.SetCulture(CultureInfo.GetCultureInfo("ja"));
            await HeadlessTestApp.CloseAsync(dialog);
            vm.Dispose();
        }
    });

    [Fact]
    public Task Close_ClosesTheDialog() => HeadlessTestApp.RunAsync(async () =>
    {
        var (dialog, vm, closed) = Show();

        dialog.CloseButton.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));

        await HeadlessTestApp.WaitUntilAsync(closed);
        vm.Dispose();
    });

    [Fact]
    public Task Escape_ClosesTheDialog() => HeadlessTestApp.RunAsync(async () =>
    {
        var (dialog, vm, closed) = Show();

        dialog.KeyPress(Key.Escape, RawInputModifiers.None, PhysicalKey.Escape, null);

        await HeadlessTestApp.WaitUntilAsync(closed);
        vm.Dispose();
    });
}
