using Avalonia.Controls;
using Avalonia.Input;
using System;
using System.Threading.Tasks;
using ZeroIchi.Services;

namespace ZeroIchi.Views;

public sealed partial class SaveChangesDialog : Window
{
    private SaveChangesResult Result { get; set; } = SaveChangesResult.Cancel;

    public SaveChangesDialog()
    {
        InitializeComponent();

        SaveButton.Click += (_, _) => { Result = SaveChangesResult.Save; Close(); };
        DiscardButton.Click += (_, _) => { Result = SaveChangesResult.Discard; Close(); };
        CancelButton.Click += (_, _) => Close();

        Opened += (_, _) => DialogAccent.ApplyButtonForeground(this);
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
