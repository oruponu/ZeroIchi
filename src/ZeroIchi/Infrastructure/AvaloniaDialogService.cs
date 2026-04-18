using Avalonia.Controls;
using Avalonia.Platform.Storage;
using System.Threading.Tasks;
using ZeroIchi.Services;
using ZeroIchi.Views;

namespace ZeroIchi.Infrastructure;

public class AvaloniaDialogService(Window window, IShellWindow shell) : IDialogService
{
    public async Task<string?> PickOpenFileAsync(string title)
    {
        var files = await window.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = title,
        });

        if (files.Count == 0)
            return null;

        return files[0].TryGetLocalPath();
    }

    public async Task<string?> PickSaveFileAsync(string title, string? suggestedFileName)
    {
        var file = await window.StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            Title = title,
            SuggestedFileName = suggestedFileName,
        });

        return file?.TryGetLocalPath();
    }

    public async Task<SaveChangesResult> ShowSaveChangesDialogAsync()
    {
        shell.SetModalOverlayVisible(true);
        try
        {
            return await SaveChangesDialog.ShowAsync(window);
        }
        finally
        {
            shell.SetModalOverlayVisible(false);
        }
    }
}
