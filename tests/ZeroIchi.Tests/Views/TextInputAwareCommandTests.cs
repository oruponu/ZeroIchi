using Avalonia.Controls;
using CommunityToolkit.Mvvm.Input;
using ZeroIchi.Views;

namespace ZeroIchi.Tests.Views;

public sealed class TextInputAwareCommandTests
{
    [Fact]
    public void CanExecute_TextBoxFocused_ReturnsFalse()
    {
        var command = new TextInputAwareCommand(() => new RelayCommand(() => { }), () => new TextBox());

        Assert.False(command.CanExecute(null));
    }

    [Fact]
    public void CanExecute_OtherElementFocused_ReturnsInnerResult()
    {
        var focused = new Border();
        var enabled = new TextInputAwareCommand(() => new RelayCommand(() => { }, () => true), () => focused);
        var disabled = new TextInputAwareCommand(() => new RelayCommand(() => { }, () => false), () => focused);

        Assert.True(enabled.CanExecute(null));
        Assert.False(disabled.CanExecute(null));
    }

    [Fact]
    public void CanExecute_NothingFocused_ReturnsInnerResult()
    {
        var command = new TextInputAwareCommand(() => new RelayCommand(() => { }), () => null);

        Assert.True(command.CanExecute(null));
    }

    [Fact]
    public void CanExecute_NoCommand_ReturnsFalse()
    {
        var command = new TextInputAwareCommand(() => null, () => null);

        Assert.False(command.CanExecute(null));
    }

    [Fact]
    public void Execute_RunsInnerCommand()
    {
        var executed = false;
        var command = new TextInputAwareCommand(() => new RelayCommand(() => executed = true), () => null);

        command.Execute(null);

        Assert.True(executed);
    }
}
