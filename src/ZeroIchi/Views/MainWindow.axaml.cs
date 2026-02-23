using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using System;
using System.Linq;
using ZeroIchi.Controls;
using ZeroIchi.ViewModels;

namespace ZeroIchi.Views;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
        HexView.AddHandler(HexViewControl.ByteModifiedEvent, OnByteModified);
        HexView.AddHandler(HexViewControl.BytesDeletedEvent, OnBytesDeleted);
        AddHandler(DragDrop.DropEvent, OnDrop);
    }

    protected override void OnDataContextChanged(EventArgs e)
    {
        base.OnDataContextChanged(e);

        if (DataContext is MainWindowViewModel vm)
        {
            vm.SetStorageProvider(StorageProvider);
            vm.SetClipboard(Clipboard);
        }
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);

        if (DataContext is MainWindowViewModel vm)
        {
            if (e.KeyModifiers.HasFlag(KeyModifiers.Control) && e.Key == Key.N)
            {
                vm.NewFileCommand.Execute(null);
                e.Handled = true;
            }
            else if (e.KeyModifiers.HasFlag(KeyModifiers.Control) && e.Key == Key.O)
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
            else if (e.KeyModifiers.HasFlag(KeyModifiers.Control | KeyModifiers.Shift) && e.Key == Key.Z)
            {
                vm.RedoCommand.Execute(null);
                e.Handled = true;
            }
            else if (e.KeyModifiers.HasFlag(KeyModifiers.Control) && e.Key == Key.Z)
            {
                vm.UndoCommand.Execute(null);
                e.Handled = true;
            }
            else if (e.KeyModifiers.HasFlag(KeyModifiers.Control) && e.Key == Key.Y)
            {
                vm.RedoCommand.Execute(null);
                e.Handled = true;
            }
            else if (e.KeyModifiers.HasFlag(KeyModifiers.Control) && e.Key == Key.C)
            {
                vm.CopyCommand.Execute(null);
                e.Handled = true;
            }
            else if (e.KeyModifiers.HasFlag(KeyModifiers.Control) && e.Key == Key.X)
            {
                vm.CutCommand.Execute(null);
                e.Handled = true;
            }
            else if (e.KeyModifiers.HasFlag(KeyModifiers.Control) && e.Key == Key.V)
            {
                vm.PasteCommand.Execute(null);
                e.Handled = true;
            }
            else if (e.KeyModifiers.HasFlag(KeyModifiers.Control) && e.Key == Key.A)
            {
                vm.SelectAllCommand.Execute(null);
                e.Handled = true;
            }
        }
    }

    private void OnByteModified(object? sender, ByteModifiedEventArgs e)
    {
        if (DataContext is MainWindowViewModel vm)
        {
            vm.OnByteModified(e.Index, e.Value);
            vm.CaptureCursorAfterEdit();
        }
    }

    private void OnBytesDeleted(object? sender, BytesDeletedEventArgs e)
    {
        if (DataContext is MainWindowViewModel vm)
        {
            vm.OnBytesDeleted(e.Index, e.Count);
        }
    }

    private void OnMinimizeClick(object? sender, RoutedEventArgs e) => WindowState = WindowState.Minimized;

    private void OnMaximizeRestoreClick(object? sender, RoutedEventArgs e)
    {
        WindowState = WindowState == WindowState.Maximized
            ? WindowState.Normal
            : WindowState.Maximized;
    }

    private void OnCloseClick(object? sender, RoutedEventArgs e) => Close();

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == WindowStateProperty)
        {
            var isMaximized = WindowState == WindowState.Maximized;
            MaximizeIcon?.IsVisible = !isMaximized;
            RestoreIcon?.IsVisible = isMaximized;
            RootPanel?.Margin = isMaximized ? new Thickness(8) : new Thickness(0);
        }
    }

    private async void OnDrop(object? sender, DragEventArgs e)
    {
        try
        {
            if (DataContext is not MainWindowViewModel vm)
                return;

            var files = e.DataTransfer.TryGetFiles()?.OfType<IStorageFile>().ToList();
            if (files is not { Count: > 0 })
                return;

            if (files[0].TryGetLocalPath() is not { } path)
                return;

            await vm.OpenFileAsync(path);
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine(ex);
        }
    }
}
