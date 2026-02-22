using Avalonia.Controls;
using Avalonia.Input;
using System;
using ZeroIchi.Controls;
using ZeroIchi.ViewModels;

namespace ZeroIchi.Views;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
        HexView.AddHandler(HexViewControl.ByteModifiedEvent, OnByteModified);
    }

    protected override void OnDataContextChanged(EventArgs e)
    {
        base.OnDataContextChanged(e);

        if (DataContext is MainWindowViewModel vm)
        {
            vm.SetStorageProvider(StorageProvider);
        }
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);

        if (DataContext is MainWindowViewModel vm)
        {
            if (e.KeyModifiers.HasFlag(KeyModifiers.Control) && e.Key == Key.O)
            {
                vm.OpenFileCommand.Execute(null);
                e.Handled = true;
            }
            else if (e.KeyModifiers.HasFlag(KeyModifiers.Control | KeyModifiers.Shift) && e.Key == Key.S)
            {
                vm.SaveAsFileCommand.Execute(null);
                e.Handled = true;
            }
            else if (e.KeyModifiers.HasFlag(KeyModifiers.Control) && e.Key == Key.S)
            {
                vm.SaveFileCommand.Execute(null);
                e.Handled = true;
            }
        }
    }

    private void OnByteModified(object? sender, ByteModifiedEventArgs e)
    {
        if (DataContext is MainWindowViewModel vm)
        {
            vm.OnByteModified(e.Index, e.Value);
        }
    }
}
