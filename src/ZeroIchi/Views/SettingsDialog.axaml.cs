using Avalonia.Controls;
using Avalonia.Input;

namespace ZeroIchi.Views;

public sealed partial class SettingsDialog : Window
{
    public SettingsDialog()
    {
        InitializeComponent();

        CloseButton.Click += (_, _) => Close();
        DefaultButtonAccent.Attach(this);
        DialogAnimation.Attach(this);
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
}
