using ZeroIchi.ViewModels;

namespace ZeroIchi.Tests;

internal static class ViewModelTestExtensions
{
    internal static async Task DisposeDocumentAsync(this MainWindowViewModel vm)
    {
        await vm.PauseStructureUpdatesAsync();
        vm.Document?.Dispose();
    }
}
