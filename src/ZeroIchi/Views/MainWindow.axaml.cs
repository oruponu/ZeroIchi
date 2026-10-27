using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using Avalonia.Threading;
using System;
using System.ComponentModel;
using System.Linq;
using System.Windows.Input;
using ZeroIchi.Controls;
using ZeroIchi.ViewModels;

namespace ZeroIchi.Views;

public sealed partial class MainWindow : Window, IShellWindow
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

        // Registered here instead of XAML because window KeyBindings are evaluated before TextBox handles KeyDown.
        AddEditKeyBinding(Key.Z, KeyModifiers.Control, vm => vm.UndoCommand);
        AddEditKeyBinding(Key.Z, KeyModifiers.Control | KeyModifiers.Shift, vm => vm.RedoCommand);
        AddEditKeyBinding(Key.Y, KeyModifiers.Control, vm => vm.RedoCommand);
        AddEditKeyBinding(Key.C, KeyModifiers.Control, vm => vm.CopyCommand);
        AddEditKeyBinding(Key.X, KeyModifiers.Control, vm => vm.CutCommand);
        AddEditKeyBinding(Key.V, KeyModifiers.Control, vm => vm.PasteCommand);
        AddEditKeyBinding(Key.A, KeyModifiers.Control, vm => vm.SelectAllCommand);
    }

    private void AddEditKeyBinding(Key key, KeyModifiers modifiers, Func<MainWindowViewModel, ICommand> selectCommand)
    {
        KeyBindings.Add(new KeyBinding
        {
            Gesture = new KeyGesture(key, modifiers),
            Command = new TextInputAwareCommand(
                () => DataContext is MainWindowViewModel vm ? selectCommand(vm) : null,
                () => FocusManager?.GetFocusedElement()),
        });
    }

    protected override void OnDataContextChanged(EventArgs e)
    {
        base.OnDataContextChanged(e);

        _viewModel?.PropertyChanged -= OnViewModelPropertyChanged;
        _viewModel?.SearchFocusRequested -= OnSearchFocusRequested;
        _viewModel?.GoToOffsetFocusRequested -= OnGoToOffsetFocusRequested;
        _viewModel = DataContext as MainWindowViewModel;
        _viewModel?.PropertyChanged += OnViewModelPropertyChanged;
        _viewModel?.SearchFocusRequested += OnSearchFocusRequested;
        _viewModel?.GoToOffsetFocusRequested += OnGoToOffsetFocusRequested;
    }

    public void SetModalOverlayVisible(bool visible) => ModalOverlay.Opacity = visible ? 1 : 0;

    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (_viewModel is null) return;

        switch (e.PropertyName)
        {
            case nameof(MainWindowViewModel.IsSearchVisible) when !_viewModel.IsSearchVisible:
            case nameof(MainWindowViewModel.IsGoToOffsetVisible) when !_viewModel.IsGoToOffsetVisible:
                Dispatcher.UIThread.Post(() => HexView.Focus());
                break;
        }
    }

    private void OnSearchFocusRequested(object? sender, EventArgs e)
        => Dispatcher.UIThread.Post(() => { SearchTextBox.Focus(); SearchTextBox.SelectAll(); });

    private void OnGoToOffsetFocusRequested(object? sender, EventArgs e)
        => Dispatcher.UIThread.Post(() => { GoToOffsetTextBox.Focus(); GoToOffsetTextBox.SelectAll(); });

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
            vm.OnByteModified(e.Index, e.Value, e.CursorPositionAfter);
        }
    }

    private void OnBytesDeleted(object? sender, BytesDeletedEventArgs e)
    {
        if (DataContext is MainWindowViewModel vm)
        {
            vm.OnBytesDeleted(e.Index, e.Count, e.CursorPositionAfter);
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
