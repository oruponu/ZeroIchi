using Avalonia.Controls;
using Avalonia.Input;
using System.Threading.Tasks;
using ZeroIchi.Models;

namespace ZeroIchi.Views;

public sealed partial class LicensesDialog : Window
{
    public LicensesDialog()
    {
        InitializeComponent();

        var document = ThirdPartyNotices.Load();
        PreambleText.Text = document.Preamble;
        NoticeList.SelectionChanged += (_, _) =>
        {
            BodyText.Text = (NoticeList.SelectedItem as ThirdPartyNotice)?.Body;
            BodyText.CaretIndex = 0;
        };
        NoticeList.ItemsSource = document.Notices;
        NoticeList.SelectedIndex = 0;

        CloseButton.Click += (_, _) => Close();
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
        await new LicensesDialog().ShowDialog(owner);
    }
}
