using CommunityToolkit.Mvvm.Input;
using System;
using System.IO;
using System.Threading.Tasks;
using ZeroIchi.Models;
using ZeroIchi.Services;

namespace ZeroIchi.ViewModels;

public partial class MainWindowViewModel
{
    private const string UnusableDocumentNotice =
        "表示中のファイルを読み取れなくなったため閉じました。ファイルが正しく保存されていない可能性があります。開き直して内容を確認してください。";

    [RelayCommand]
    private async Task NewFileAsync()
    {
        await TryReplaceDocumentAsync(BinaryDocument.CreateNew, "新しいファイルを作成できませんでした");
    }

    [RelayCommand]
    private async Task OpenFileAsync()
    {
        var path = await _dialog.PickOpenFileAsync("ファイルを開く");
        if (path is null)
            return;

        await OpenFileAsync(path);
    }

    public async Task OpenFileAsync(string path)
    {
        await TryReplaceDocumentAsync(() => BinaryDocument.Open(path), "ファイルを開けませんでした");
    }

    private async Task<bool> TryReplaceDocumentAsync(Func<BinaryDocument> factory, string failureMessage)
    {
        if (!await ConfirmDiscardChangesAsync())
            return false;

        BinaryDocument? newDocument = null;
        await TryFileOperationAsync(failureMessage, () =>
        {
            newDocument = factory();
            return Task.CompletedTask;
        });
        if (newDocument is null)
            return false;

        ReplaceDocument(newDocument);
        return true;
    }

    internal async Task<bool> TryFileOperationAsync(string failureMessage, Func<Task> operation)
    {
        try
        {
            await operation();
            return true;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            var detail = ex.Message;
            if (Document is { IsUsable: false })
            {
                DetachUnusableDocument();
                detail = $"{ex.Message}\n\n{UnusableDocumentNotice}";
            }

            await _dialog.ShowErrorAsync(failureMessage, detail);
            return false;
        }
    }

    private void DetachUnusableDocument() => ReplaceDocument(BinaryDocument.CreateNew());

    [RelayCommand]
    private async Task SaveFileAsync()
    {
        if (Document is null || !Document.IsModified) return;

        if (Document.IsNew)
        {
            await SaveAsFileAsync();
            return;
        }

        var document = Document;
        await TryFileOperationAsync("保存できませんでした", () =>
        {
            document.Save();
            return Task.CompletedTask;
        });
    }

    [RelayCommand]
    private async Task SaveAsFileAsync()
    {
        if (Document is null) return;

        var path = await _dialog.PickSaveFileAsync("名前を付けて保存", Document.FileName);
        if (path is null) return;

        var document = Document;
        await TryFileOperationAsync("保存できませんでした", () => document.SaveAsAsync(path));
    }

    [RelayCommand]
    private void Exit()
    {
        _window.Close();
    }

    public async Task<bool> ConfirmDiscardChangesAsync()
    {
        if (Document is not { IsModified: true })
            return true;

        var result = await _dialog.ShowSaveChangesDialogAsync();

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
