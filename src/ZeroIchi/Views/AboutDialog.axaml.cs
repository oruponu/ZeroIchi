using Avalonia.Controls;
using Avalonia.Input;
using System;
using System.Threading.Tasks;
using ZeroIchi.Infrastructure;
using ZeroIchi.Models;

namespace ZeroIchi.Views;

public sealed partial class AboutDialog : Window
{
    internal static readonly Uri RepositoryUri = new("https://github.com/oruponu/ZeroIchi");

    public AboutDialog()
    {
        InitializeComponent();

        VersionText.Text = $"バージョン {AppVersion.Current}";
        RepositoryLink.Click += async (_, _) => await Launcher.TryLaunchUriAsync(RepositoryUri);
        LicensesButton.Click += async (_, _) => await LicensesDialog.ShowAsync(this);
        OkButton.Click += (_, _) => Close();
        Opened += (_, _) => DialogAccent.ApplyButtonForeground(this);
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        if (e.Key == Key.Escape)
        {
            Close();
            e.Handled = true;
        }
    }

    public static async Task ShowAsync(Window owner)
    {
        await new AboutDialog().ShowDialog(owner);
    }
}
