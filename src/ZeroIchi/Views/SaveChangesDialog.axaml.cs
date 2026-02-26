using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using System;
using System.Threading.Tasks;

namespace ZeroIchi.Views;

public enum SaveChangesResult { Save, Discard, Cancel }

public partial class SaveChangesDialog : Window
{
    private SaveChangesResult Result { get; set; } = SaveChangesResult.Cancel;

    public SaveChangesDialog()
    {
        InitializeComponent();

        SaveButton.Click += (_, _) => { Result = SaveChangesResult.Save; Close(); };
        DiscardButton.Click += (_, _) => { Result = SaveChangesResult.Discard; Close(); };
        CancelButton.Click += (_, _) => Close();

        Opened += (_, _) => UpdateAccentButtonForeground();
    }

    private void UpdateAccentButtonForeground()
    {
        var foreground = Brushes.White;

        if (this.TryFindResource("SystemAccentColor", ActualThemeVariant, out var obj)
            && obj is Color accent)
        {
            var luminance = 0.2126 * (accent.R / 255.0)
                          + 0.7152 * (accent.G / 255.0)
                          + 0.0722 * (accent.B / 255.0);
            foreground = luminance > 0.5 ? Brushes.Black : Brushes.White;
        }

        Resources["AccentButtonForeground"] = foreground;
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        switch (e.Key)
        {
            case Key.S:
                Result = SaveChangesResult.Save;
                Close();
                e.Handled = true;
                break;
            case Key.N:
                Result = SaveChangesResult.Discard;
                Close();
                e.Handled = true;
                break;
            case Key.Escape:
                Close();
                e.Handled = true;
                break;
        }
    }

    public static async Task<SaveChangesResult> ShowAsync(Window owner, Action<bool>? setOverlay = null)
    {
        var dialog = new SaveChangesDialog();
        setOverlay?.Invoke(true);

        try
        {
            await dialog.ShowDialog(owner);
            return dialog.Result;
        }
        finally
        {
            setOverlay?.Invoke(false);
        }
    }
}
