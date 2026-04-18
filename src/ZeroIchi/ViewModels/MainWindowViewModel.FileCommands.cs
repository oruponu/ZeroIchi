using Avalonia.Platform.Storage;
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
        if (_storageProvider is null)
            return;

        var files = await _storageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "ファイルを開く",
        });

        if (files.Count == 0)
            return;

        if (files[0].TryGetLocalPath() is not { } path)
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
        if (_storageProvider is null || Document is null) return;

        var file = await _storageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            Title = "名前を付けて保存",
            SuggestedFileName = Document.FileName,
        });

        if (file?.TryGetLocalPath() is not { } path) return;

        await Document.SaveAsAsync(path);
        UpdateTitle();
    }

    [RelayCommand]
    private void Exit()
    {
        _closeAction?.Invoke();
    }

    public async Task<bool> ConfirmDiscardChangesAsync()
    {
        if (Document is not { IsModified: true } || _showSaveChangesDialog is null)
            return true;

        var result = await _showSaveChangesDialog();

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
