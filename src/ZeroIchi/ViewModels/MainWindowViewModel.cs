using Avalonia.Platform.Storage;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System;
using System.Text;
using System.Threading.Tasks;
using ZeroIchi.Models;

namespace ZeroIchi.ViewModels;

public partial class MainWindowViewModel : ViewModelBase
{
    private const int BytesPerLine = 16;

    private IStorageProvider? _storageProvider;

    [ObservableProperty]
    private BinaryDocument? _document;

    [ObservableProperty]
    private string _title = "ZeroIchi";

    [ObservableProperty]
    private string _fileInfo = "";

    [ObservableProperty]
    private string _hexDump = "";

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
        HexDump = GenerateHexDump(Document.Data, maxLines: 16);
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

    private static string GenerateHexDump(byte[] data, int maxLines)
    {
        var totalLines = Math.Min(maxLines, (data.Length + BytesPerLine - 1) / BytesPerLine);
        var sb = new StringBuilder(totalLines * 80);
        ReadOnlySpan<byte> span = data;

        for (var line = 0; line < totalLines; line++)
        {
            var offset = line * BytesPerLine;
            var lineSpan = span.Slice(offset, Math.Min(BytesPerLine, span.Length - offset));

            sb.Append($"{offset:X8}  ");
            FormatHexLine(sb, lineSpan);
            sb.Append(" |");
            FormatAsciiLine(sb, lineSpan);
            sb.Append('|');

            if (line < totalLines - 1)
                sb.AppendLine();
        }

        if (data.Length > maxLines * BytesPerLine)
        {
            sb.AppendLine();
            sb.Append($"... (残り {data.Length - maxLines * BytesPerLine} バイト)");
        }

        return sb.ToString();
    }

    private static void FormatHexLine(StringBuilder sb, ReadOnlySpan<byte> lineData)
    {
        Span<char> hexBuf = stackalloc char[3]; // "XX "

        for (var i = 0; i < BytesPerLine; i++)
        {
            if (i < lineData.Length)
            {
                lineData[i].TryFormat(hexBuf, out _, "X2");
                hexBuf[2] = ' ';
                sb.Append(hexBuf);
            }
            else
            {
                sb.Append("   ");
            }

            if (i == 7)
                sb.Append(' ');
        }
    }

    private static void FormatAsciiLine(StringBuilder sb, ReadOnlySpan<byte> lineData)
    {
        foreach (var b in lineData)
            sb.Append(b is >= 0x20 and <= 0x7E ? (char)b : '.');
    }
}
