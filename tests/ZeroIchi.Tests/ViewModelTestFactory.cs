using ZeroIchi.Services;
using ZeroIchi.ViewModels;

namespace ZeroIchi.Tests;

internal static class ViewModelTestFactory
{
    public static MainWindowViewModel CreateViewModel(IDialogService? dialog = null, IClipboardService? clipboard = null) =>
        new(dialog ?? new StubDialogService(), clipboard ?? new StubClipboardService(), new StubWindowService());
}
