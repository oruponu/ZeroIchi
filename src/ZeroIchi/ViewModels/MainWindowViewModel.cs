using Avalonia.Platform.Storage;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Collections.Generic;
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
    private IReadOnlyList<HexLine>? _hexLines;

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
        Title = $"{Document.FileName} - ZeroIchi";
        FileInfo = $"ファイル名: {Document.FileName}\nサイズ: {FormatFileSize(Document.FileSize)}";
        HexLines = GenerateHexLines(Document.Data);
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

    private static HexLine[] GenerateHexLines(byte[] data)
    {
        var totalLines = (data.Length + HexLine.BytesPerLine - 1) / HexLine.BytesPerLine;
        var lines = new HexLine[totalLines];

        for (var i = 0; i < totalLines; i++)
            lines[i] = HexLine.Create(data, i);

        return lines;
    }
}
