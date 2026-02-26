using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using System;
using System.Linq;
using System.Threading.Tasks;
using ZeroIchi.Controls;
using ZeroIchi.ViewModels;

namespace ZeroIchi.Views;

public partial class MainWindow : Window
{
    private bool _isClosingConfirmed;

    public MainWindow()
    {
        InitializeComponent();
        HexView.AddHandler(HexViewControl.ByteModifiedEvent, OnByteModified);
        HexView.AddHandler(HexViewControl.BytesDeletedEvent, OnBytesDeleted);
        AddHandler(DragDrop.DropEvent, OnDrop);
        SearchTextBox.KeyDown += OnSearchTextBoxKeyDown;
        Opened += (_, _) => HexView.Focus();
    }

    protected override void OnDataContextChanged(EventArgs e)
    {
        base.OnDataContextChanged(e);

        if (DataContext is MainWindowViewModel vm)
        {
            vm.SetStorageProvider(StorageProvider);
            vm.SetClipboard(Clipboard);
            vm.SetCloseAction(Close);
            vm.SetConfirmDiscardChanges(ConfirmDiscardChangesAsync);
        }
    }

    protected override async void OnClosing(WindowClosingEventArgs e)
    {
        try
        {
            base.OnClosing(e);

            if (_isClosingConfirmed)
                return;

            if (DataContext is MainWindowViewModel { Document.IsModified: true })
            {
                e.Cancel = true;

                if (!await ConfirmDiscardChangesAsync())
                    return;

                _isClosingConfirmed = true;
                Close();
            }
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine(ex);
        }
    }

    private async Task<bool> ConfirmDiscardChangesAsync()
    {
        if (DataContext is not MainWindowViewModel vm)
            return false;

        var result = await SaveChangesDialog.ShowAsync(this,
            visible => ModalOverlay.IsVisible = visible);

        if (result == SaveChangesResult.Save)
        {
            await vm.SaveFileCommand.ExecuteAsync(null);
            return vm.Document is not { IsModified: true };
        }

        return result == SaveChangesResult.Discard;
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
            else if (e.KeyModifiers.HasFlag(KeyModifiers.Control) && e.Key == Key.F)
            {
                vm.OpenSearchCommand.Execute(null);
                SearchTextBox.Focus();
                SearchTextBox.SelectAll();
                e.Handled = true;
            }
            else if (e.Key == Key.F3 && e.KeyModifiers.HasFlag(KeyModifiers.Shift))
            {
                vm.FindPreviousCommand.Execute(null);
                e.Handled = true;
            }
            else if (e.Key == Key.F3)
            {
                vm.FindNextCommand.Execute(null);
                e.Handled = true;
            }
            else if (e.Key == Key.Escape && vm.IsSearchVisible)
            {
                vm.CloseSearchCommand.Execute(null);
                HexView.Focus();
                e.Handled = true;
            }
        }
    }

    private void OnSearchTextBoxKeyDown(object? sender, KeyEventArgs e)
    {
        if (DataContext is not MainWindowViewModel vm) return;

        if (e.Key == Key.Enter)
        {
            if (e.KeyModifiers.HasFlag(KeyModifiers.Shift))
                vm.FindPreviousCommand.Execute(null);
            else
                vm.FindNextCommand.Execute(null);
            e.Handled = true;
        }
        else if (e.Key == Key.Escape)
        {
            vm.CloseSearchCommand.Execute(null);
            HexView.Focus();
            e.Handled = true;
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
