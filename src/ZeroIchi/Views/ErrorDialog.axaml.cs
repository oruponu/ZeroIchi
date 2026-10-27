using Avalonia.Controls;
using Avalonia.Input;
using System.Threading.Tasks;

namespace ZeroIchi.Views;

public sealed partial class ErrorDialog : Window
{
    public ErrorDialog()
    {
        InitializeComponent();

        OkButton.Click += (_, _) => Close();
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

    public static async Task ShowAsync(Window owner, string message, string detail)
    {
        var dialog = new ErrorDialog();
        dialog.MessageText.Text = message;
        dialog.DetailText.Text = detail;
        await dialog.ShowDialog(owner);
    }
}
