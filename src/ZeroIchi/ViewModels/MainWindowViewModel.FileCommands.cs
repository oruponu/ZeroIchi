using CommunityToolkit.Mvvm.Input;
using System;
using System.Threading.Tasks;
using ZeroIchi.Models;
using ZeroIchi.Services;

namespace ZeroIchi.ViewModels;

public partial class MainWindowViewModel
{
    [RelayCommand]
    private async Task NewFileAsync()
    {
        await TryReplaceDocumentAsync(BinaryDocument.CreateNew);
    }

    [RelayCommand]
    private async Task OpenFileAsync()
    {
        var path = await dialog.PickOpenFileAsync("ファイルを開く");
        if (path is null)
            return;

        await OpenFileAsync(path);
    }

    public async Task OpenFileAsync(string path)
    {
        await TryReplaceDocumentAsync(() => BinaryDocument.Open(path));
    }

    private async Task<bool> TryReplaceDocumentAsync(Func<BinaryDocument> factory)
    {
        if (!await ConfirmDiscardChangesAsync())
            return false;

        ReplaceDocument(factory());
        return true;
    }

    [RelayCommand]
    private async Task SaveFileAsync()
    {
        if (Document is null || !Document.IsModified) return;

        if (Document.IsNew)
        {
            await SaveAsFileAsync();
            return;
        }

        Document.Save();
        UpdateUndoRedoState();
        UpdateTitle();
    }

    [RelayCommand]
    private async Task SaveAsFileAsync()
    {
        if (Document is null) return;

        var path = await dialog.PickSaveFileAsync("名前を付けて保存", Document.FileName);
        if (path is null) return;

        await Document.SaveAsAsync(path);
        UpdateUndoRedoState();
        UpdateTitle();
    }

    [RelayCommand]
    private void Exit()
    {
        window.Close();
    }

    public async Task<bool> ConfirmDiscardChangesAsync()
    {
        if (Document is not { IsModified: true })
            return true;

        var result = await dialog.ShowSaveChangesDialogAsync();

        if (result == SaveChangesResult.Save)
        {
            await SaveFileAsync();
            return Document is not { IsModified: true };
        }

        return result == SaveChangesResult.Discard;
    }

    private void UpdateTitle()
    {
        if (Document is null)
        {
            Title = "ZeroIchi";
            return;
        }

        var modified = Document.IsModified ? "*" : "";
        Title = $"{modified}{Document.FileName} - ZeroIchi";
    }
}
