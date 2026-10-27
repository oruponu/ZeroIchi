using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.LogicalTree;
using System.Linq;

namespace ZeroIchi.Views;

internal static class DefaultButtonAccent
{
    public static void Attach(Window dialog)
    {
        dialog.AddHandler(InputElement.GotFocusEvent, (_, e) =>
        {
            var hasAccent = e.Source is not Button { IsDefault: false };
            foreach (var button in dialog.GetLogicalDescendants().OfType<Button>().Where(b => b.IsDefault))
            {
                button.Classes.Set("accent", hasAccent);
            }
        });
    }
}
