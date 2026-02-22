using Avalonia;
using Avalonia.Platform.Storage;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MimeDetective;
using MimeDetective.Definitions;
using MimeDetective.Definitions.Licensing;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using ZeroIchi.Models;

namespace ZeroIchi.ViewModels;

public partial class MainWindowViewModel : ViewModelBase
{
    private static readonly IContentInspector Inspector = new ContentInspectorBuilder
    {
        Definitions = new CondensedBuilder
        {
            UsageType = UsageType.PersonalNonCommercial,
        }.Build(),
    }.Build();

    private IStorageProvider? _storageProvider;

    [ObservableProperty]
    private BinaryDocument? _document = BinaryDocument.CreateNew();

    [ObservableProperty]
    private string _title = "Untitled - ZeroIchi";

    [ObservableProperty]
    private byte[]? _data = [];

    [ObservableProperty]
    private int _cursorPosition;

    [ObservableProperty]
    private int _selectionStart;

    [ObservableProperty]
    private int _selectionLength;

    [ObservableProperty]
    private HashSet<int>? _modifiedIndices;

    [ObservableProperty]
    private string _statusBarPositionText = "00000000";

    [ObservableProperty]
    private string _statusBarFileTypeText = "";

    [ObservableProperty]
    private string _statusBarSizeText = "0 B";

    public void SetStorageProvider(IStorageProvider storageProvider)
    {
        _storageProvider = storageProvider;
    }

    [RelayCommand]
    private void NewFile()
    {
        Document = BinaryDocument.CreateNew();
        Data = Document.Data;
        ModifiedIndices = null;
        CursorPosition = 0;
        SelectionStart = 0;
        SelectionLength = 0;
        UpdateTitle();
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
        Document = await BinaryDocument.OpenAsync(path);
        UpdateTitle();
        Data = Document.Data;
        ModifiedIndices = null;
    }

    public void OnByteModified(int index, byte value)
    {
        if (Document is null) return;

        if (index == Document.Data.Length)
        {
            Document.AppendByte(value);
            Data = Document.Data;
        }
        else
        {
            Document.WriteByte(index, value);
        }

        ModifiedIndices = [.. Document.ModifiedIndices];
        UpdateTitle();
    }

    public void OnBytesDeleted(int index, int count)
    {
        if (Document is null) return;

        Document.DeleteBytes(index, count);
        Data = Document.Data;
        ModifiedIndices = [.. Document.ModifiedIndices];
        UpdateTitle();
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

        await Document.SaveAsync();
        ModifiedIndices = null;
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
        ModifiedIndices = null;
        UpdateTitle();
    }

    [RelayCommand]
    private static void Exit()
    {
        if (Application.Current?.ApplicationLifetime is Avalonia.Controls.ApplicationLifetimes.IClassicDesktopStyleApplicationLifetime lifetime)
        {
            lifetime.Shutdown();
        }
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

    partial void OnCursorPositionChanged(int value) => UpdateStatusBar();
    partial void OnSelectionStartChanged(int value) => UpdateStatusBar();
    partial void OnSelectionLengthChanged(int value) => UpdateStatusBar();
    partial void OnDataChanged(byte[]? value)
    {
        UpdateStatusBar();
        StatusBarFileTypeText = value is not null ? DetectFileType(value) : "";
    }

    private void UpdateStatusBar()
    {
        if (Data is null)
        {
            StatusBarPositionText = "";
            StatusBarFileTypeText = "";
            StatusBarSizeText = "";
            return;
        }

        if (SelectionLength > 0)
        {
            var selEnd = SelectionStart + SelectionLength - 1;
            StatusBarPositionText = $"{SelectionStart:X8} - {selEnd:X8} ({SelectionLength} バイト)";
        }
        else
        {
            StatusBarPositionText = $"{CursorPosition:X8}";
        }

        StatusBarSizeText = FormatFileSize(Data.Length);
    }

    private static string DetectFileType(byte[] data)
    {
        if (data.Length == 0) return "";

        var length = Math.Min(data.Length, 1024);
        var results = Inspector.Inspect(new ReadOnlySpan<byte>(data, 0, length));
        var match = results.ByFileExtension();

        return match.Length > 0 ? match[0].Extension : "";
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
