using Avalonia.Controls;
using Avalonia.Controls.Templates;
using Microsoft.Extensions.DependencyInjection;
using System;
using ZeroIchi.ViewModels;
using ZeroIchi.Views;

namespace ZeroIchi;

public class ViewLocator(IServiceProvider services) : IDataTemplate
{
    public Control? Build(object? data) => data switch
    {
        MainWindowViewModel => services.GetRequiredService<MainWindow>(),
        null => null,
        _ => new TextBlock { Text = $"No view for {data.GetType().Name}" },
    };

    public bool Match(object? data) => data is ViewModelBase;
}
