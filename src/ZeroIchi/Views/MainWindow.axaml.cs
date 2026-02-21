using Avalonia.Controls;
using System;
using ZeroIchi.ViewModels;

namespace ZeroIchi.Views;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
    }

    protected override void OnDataContextChanged(EventArgs e)
    {
        base.OnDataContextChanged(e);

        if (DataContext is MainWindowViewModel vm)
        {
            vm.SetStorageProvider(StorageProvider);
        }
    }
}
