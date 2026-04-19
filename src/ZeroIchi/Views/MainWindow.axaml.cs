using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using Avalonia.Threading;
using System;
using System.ComponentModel;
using System.Linq;
using ZeroIchi.Controls;
using ZeroIchi.ViewModels;

namespace ZeroIchi.Views;

public partial class MainWindow : Window, IShellWindow
{
    private bool _isClosingConfirmed;
    private MainWindowViewModel? _viewModel;

    public MainWindow()
    {
        InitializeComponent();
        HexView.AddHandler(HexViewControl.ByteModifiedEvent, OnByteModified);
        HexView.AddHandler(HexViewControl.BytesDeletedEvent, OnBytesDeleted);
        AddHandler(DragDrop.DropEvent, OnDrop);
        SearchTextBox.KeyDown += OnSearchTextBoxKeyDown;
        GoToOffsetTextBox.KeyDown += OnGoToOffsetTextBoxKeyDown;
        Opened += (_, _) => HexView.Focus();
    }

    protected override void OnDataContextChanged(EventArgs e)
    {
        base.OnDataContextChanged(e);

        _viewModel?.PropertyChanged -= OnViewModelPropertyChanged;
        _viewModel = DataContext as MainWindowViewModel;
        _viewModel?.PropertyChanged += OnViewModelPropertyChanged;
    }

    public void SetModalOverlayVisible(bool visible) => ModalOverlay.IsVisible = visible;

    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (_viewModel is null) return;

        switch (e.PropertyName)
        {
            case nameof(MainWindowViewModel.IsSearchVisible):
                if (_viewModel.IsSearchVisible)
                    Dispatcher.UIThread.Post(() => { SearchTextBox.Focus(); SearchTextBox.SelectAll(); });
                else
                    Dispatcher.UIThread.Post(() => HexView.Focus());
                break;
            case nameof(MainWindowViewModel.IsGoToOffsetVisible):
                if (_viewModel.IsGoToOffsetVisible)
                    Dispatcher.UIThread.Post(() => { GoToOffsetTextBox.Focus(); GoToOffsetTextBox.SelectAll(); });
                else
                    Dispatcher.UIThread.Post(() => HexView.Focus());
                break;
        }
    }

    protected override async void OnClosing(WindowClosingEventArgs e)
    {
        try
        {
            base.OnClosing(e);

            if (_isClosingConfirmed)
                return;

            if (DataContext is not MainWindowViewModel vm)
                return;

            if (vm.Document is not { IsModified: true })
                return;

            e.Cancel = true;

            if (!await vm.ConfirmDiscardChangesAsync())
                return;

            _isClosingConfirmed = true;
            Close();
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine(ex);
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
    }

    private void OnGoToOffsetTextBoxKeyDown(object? sender, KeyEventArgs e)
    {
        if (DataContext is not MainWindowViewModel vm) return;

        if (e.Key == Key.Enter)
        {
            vm.GoToOffsetCommand.Execute(null);
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
