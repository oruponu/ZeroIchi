using Avalonia.Controls;
using System;
using System.Windows.Input;

namespace ZeroIchi.Views;

public sealed class TextInputAwareCommand(Func<ICommand?> getCommand, Func<object?> getFocusedElement) : ICommand
{
    public event EventHandler? CanExecuteChanged { add { } remove { } }

    public bool CanExecute(object? parameter)
        => getFocusedElement() is not TextBox && getCommand()?.CanExecute(parameter) == true;

    public void Execute(object? parameter) => getCommand()?.Execute(parameter);
}
