using CommunityToolkit.Mvvm.Input;
using System.Threading.Tasks;
using ZeroIchi.Models;
using ZeroIchi.Services;

namespace ZeroIchi.ViewModels;

public partial class MainWindowViewModel
{
    [RelayCommand]
    private async Task NewFileAsync()
    {
        if (!await ConfirmDiscardChangesAsync())
            return;

        ReplaceDocument(BinaryDocument.CreateNew());
        CursorPosition = 0;
        SelectionStart = 0;
        SelectionLength = 0;
        _undoRedoManager.Clear();
        UpdateUndoRedoState();
        UpdateTitle();
        ClearSearchResults();
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
        ReplaceDocument(BinaryDocument.Open(path));
        UpdateTitle();
        CursorPosition = 0;
        SelectionStart = 0;
        SelectionLength = 0;
        _undoRedoManager.Clear();
        UpdateUndoRedoState();
        ClearSearchResults();
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
        UpdateTitle();
    }

    [RelayCommand]
    private async Task SaveAsFileAsync()
    {
        if (Document is null) return;

        var path = await dialog.PickSaveFileAsync("名前を付けて保存", Document.FileName);
        if (path is null) return;

        await Document.SaveAsAsync(path);
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
