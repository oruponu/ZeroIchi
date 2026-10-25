using Avalonia.Controls;
using Avalonia.LogicalTree;
using ZeroIchi.Views;

namespace ZeroIchi.Tests.Views;

[Collection(HeadlessTestApp.Collection)]
public sealed class MainWindowKeyBindingTests
{
    [Fact]
    public Task MenuInputGestures_HaveKeyBindings() => HeadlessTestApp.RunAsync(() =>
    {
        var window = new MainWindow();

        var menuGestures = window.GetLogicalDescendants()
            .OfType<MenuItem>()
            .Select(item => item.InputGesture)
            .OfType<Avalonia.Input.KeyGesture>()
            .ToList();
        var boundGestures = window.KeyBindings.Select(binding => binding.Gesture).ToList();

        Assert.NotEmpty(menuGestures);
        Assert.All(menuGestures, gesture => Assert.Contains(gesture, boundGestures));
        return Task.CompletedTask;
    });
}
