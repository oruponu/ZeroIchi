using Avalonia.Platform.Storage;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Threading.Tasks;
using ZeroIchi.Models;

namespace ZeroIchi.ViewModels;

public partial class MainWindowViewModel : ViewModelBase
{
    private IStorageProvider? _storageProvider;

    [ObservableProperty]
    private BinaryDocument? _document;

    [ObservableProperty]
    private string _title = "ZeroIchi";

    [ObservableProperty]
    private string _fileInfo = "";

    [ObservableProperty]
    private byte[]? _data;

    [ObservableProperty]
    private int _cursorPosition;

    [ObservableProperty]
    private int _selectionStart;

    [ObservableProperty]
    private int _selectionLength;

    public void SetStorageProvider(IStorageProvider storageProvider)
    {
        _storageProvider = storageProvider;
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

        Document = await BinaryDocument.OpenAsync(path);
        UpdateTitle();
        FileInfo = $"ファイル名: {Document.FileName}\nサイズ: {FormatFileSize(Document.FileSize)}";
        Data = Document.Data;
    }

    [RelayCommand]
    private async Task SaveFileAsync()
    {
        if (Document is null || !Document.IsModified) return;

        await Document.SaveAsync();
        UpdateTitle();
    }

    private void UpdateTitle()
    {
        if (Document is null)
        {
            Title = "ZeroIchi";
            return;
        }

        var modified = Document.IsModified ? "*" : "";
        Title = $"ZeroIchi - {Document.FileName}{modified}";
    }

    private static string FormatFileSize(long bytes)
    {
        return bytes switch
        {
            < 1024 => $"{bytes} B",
            < 1024 * 1024 => $"{bytes / 1024.0:F1} KB",
            < 1024 * 1024 * 1024 => $"{bytes / (1024.0 * 1024):F1} MB",
            _ => $"{bytes / (1024.0 * 1024 * 1024):F1} GB",
        };
    }
}
