using CommunityToolkit.Mvvm.Input;
using System;
using System.IO;
using System.Threading.Tasks;
using ZeroIchi.Localization;
using ZeroIchi.Models;
using ZeroIchi.Services;

namespace ZeroIchi.ViewModels;

public partial class MainWindowViewModel
{
    private bool _isCloseRequested;
    private SaveProgress? _shownSaveProgress;

    private bool IsNotSaving() => !IsSaving;

    [RelayCommand(CanExecute = nameof(IsNotSaving))]
    private async Task NewFileAsync()
    {
        await TryReplaceDocumentAsync(BinaryDocument.CreateNew, Strings.FileNewFailed);
    }

    [RelayCommand(CanExecute = nameof(IsNotSaving))]
    private async Task OpenFileAsync()
    {
        var path = await _dialog.PickOpenFileAsync(Strings.FileOpenTitle);
        if (path is null)
            return;

        await OpenFileAsync(path);
    }

    public async Task OpenFileAsync(string path)
    {
        await TryReplaceDocumentAsync(() => BinaryDocument.Open(path), Strings.FileOpenFailed);
    }

    private async Task<bool> TryReplaceDocumentAsync(Func<BinaryDocument> factory, string failureMessage)
    {
        if (IsSaving || !await ConfirmDiscardChangesAsync() || IsSaving || _isCloseRequested)
            return false;

        BinaryDocument? newDocument = null;
        await TryFileOperationAsync(failureMessage, () =>
        {
            newDocument = factory();
            return Task.CompletedTask;
        });
        if (newDocument is null)
            return false;

        await ReplaceDocumentAsync(newDocument);
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
                await DetachUnusableDocumentAsync();
                detail = $"{ex.Message}\n\n{Strings.FileUnusableNotice}";
            }

            await _dialog.ShowErrorAsync(failureMessage, detail);
            return false;
        }
    }

    private Task DetachUnusableDocumentAsync() => ReplaceDocumentAsync(BinaryDocument.CreateNew());

    private bool CanStartSave() => !IsSaving && _replacingDocumentCount == 0;

    [RelayCommand(CanExecute = nameof(IsNotSaving))]
    private async Task SaveFileAsync()
    {
        if (!CanStartSave() || Document is null || !Document.IsModified) return;

        if (Document.IsNew)
        {
            await SaveAsFileAsync();
            return;
        }

        await RunSaveAsync(Document, (d, p) => d.SaveAsync(p));
    }

    [RelayCommand(CanExecute = nameof(IsNotSaving))]
    private async Task SaveAsFileAsync()
    {
        if (!CanStartSave() || Document is null) return;

        var path = await _dialog.PickSaveFileAsync(Strings.FileSaveAsTitle, Document.FileName);
        if (path is null || !CanStartSave()) return;

        await RunSaveAsync(Document, (d, p) => d.SaveAsAsync(path, p));
    }

    private Task RunSaveAsync(BinaryDocument document, Func<BinaryDocument, IProgress<long>?, Task> save)
    {
        IsSaving = true;
        SaveCompletion = RunAsync();
        return SaveCompletion;

        async Task RunAsync()
        {
            try
            {
                await PauseBackgroundWorkAsync();
                try
                {
                    if (Document != document) return;
                    var progress = new SaveProgress(document.FileSize);
                    await TryFileOperationAsync(Strings.FileSaveFailed, () => ShowSaveProgressAsync(save(document, progress), progress));
                }
                finally
                {
                    ResumeBackgroundWork();
                }
            }
            finally
            {
                IsSaving = false;
            }
        }
    }

    private async Task ShowSaveProgressAsync(Task saving, SaveProgress progress)
    {
        var delay = SaveBusyDelay;
        while (await Task.WhenAny(saving, Task.Delay(delay)) != saving)
        {
            _shownSaveProgress = progress;
            SaveProgressText = progress.Text;
            delay = SaveProgressInterval;
        }

        _shownSaveProgress = null;
        SaveProgressText = "";
        await saving;
    }

    [RelayCommand]
    private void Exit()
    {
        _window.Close();
    }

    public async Task<bool> PrepareCloseAsync()
    {
        _isCloseRequested = true;
        var confirmed = false;
        try
        {
            while (IsSaving)
                await SaveCompletion;

            confirmed = await ConfirmDiscardChangesAsync();
            return confirmed;
        }
        finally
        {
            if (!confirmed)
                _isCloseRequested = false;
        }
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
        Title = $"{modified}{DisplayFileName(Document)} - ZeroIchi";
    }

    private static string DisplayFileName(BinaryDocument document) =>
        document.IsNew ? Strings.DocumentUntitled : document.FileName;
}
