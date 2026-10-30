using Avalonia;
using Avalonia.Controls.Primitives;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.Threading;
using Avalonia.VisualTree;
using ZeroIchi.ViewModels;
using ZeroIchi.Views;
using static ZeroIchi.Tests.ViewModelTestFactory;

namespace ZeroIchi.Tests.Views;

[Collection(HeadlessTestApp.Collection)]
public sealed class MainWindowModeToggleTests
{
    private static async Task RunAsync(string group, Func<MainWindowViewModel, MainWindow, Task> action)
    {
        var vm = CreateViewModel();
        var window = new MainWindow { DataContext = vm };
        window.Show();
        if (group == "search") vm.OpenSearchCommand.Execute(null);
        if (group == "goto") vm.OpenGoToOffsetCommand.Execute(null);
        Dispatcher.UIThread.RunJobs();
        try
        {
            await action(vm, window);
        }
        finally
        {
            await HeadlessTestApp.CloseAsync(window);
        }
    }

    private static bool ReadMode(MainWindowViewModel vm, string group) => group switch
    {
        "search" => vm.IsHexSearch,
        "goto" => vm.IsHexGoToOffset,
        _ => vm.IsInspectorBigEndian,
    };

    private static void Click(MainWindow window, string content)
    {
        var button = window.GetVisualDescendants()
            .OfType<ToggleButton>()
            .Single(b => b.IsEffectivelyVisible && Equals(b.Content, content));
        var center = button.TranslatePoint(new Point(button.Bounds.Width / 2, button.Bounds.Height / 2), window)!.Value;
        window.MouseDown(center, MouseButton.Left);
        window.MouseUp(center, MouseButton.Left);
        Dispatcher.UIThread.RunJobs();
    }

    [Theory]
    [InlineData("search", "Hex")]
    [InlineData("goto", "Hex")]
    [InlineData("inspector", "LE")]
    public Task ClickingTheSelectedMode_KeepsIt(string group, string selected) => HeadlessTestApp.RunAsync(() =>
        RunAsync(group, (vm, window) =>
        {
            var before = ReadMode(vm, group);

            Click(window, selected);

            Assert.Equal(before, ReadMode(vm, group));
            return Task.CompletedTask;
        }));

    [Theory]
    [InlineData("search", "ASCII")]
    [InlineData("goto", "Dec")]
    [InlineData("inspector", "BE")]
    public Task ClickingTheOtherMode_SwitchesToIt(string group, string other) => HeadlessTestApp.RunAsync(() =>
        RunAsync(group, (vm, window) =>
        {
            var before = ReadMode(vm, group);

            Click(window, other);

            Assert.NotEqual(before, ReadMode(vm, group));
            return Task.CompletedTask;
        }));
}
