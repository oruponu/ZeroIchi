using CommunityToolkit.Mvvm.Input;
using System;
using System.Buffers;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace ZeroIchi.ViewModels;

public partial class MainWindowViewModel
{
    [RelayCommand]
    private void OpenGoToOffset()
    {
        IsGoToOffsetVisible = true;
        GoToOffsetError = "";
    }

    [RelayCommand]
    private void CloseGoToOffset()
    {
        IsGoToOffsetVisible = false;
        GoToOffsetText = "";
        GoToOffsetError = "";
    }

    [RelayCommand]
    private void GoToOffset()
    {
        if (Document?.Buffer is not { } buffer)
            return;

        var text = GoToOffsetText.Trim();
        if (string.IsNullOrEmpty(text))
            return;

        if (!TryParseOffset(text, out var offset))
        {
            GoToOffsetError = "無効な値";
            return;
        }

        if (offset < 0 || offset >= buffer.Length)
        {
            GoToOffsetError = $"範囲外 (0 - {buffer.Length - 1:X})";
            return;
        }

        CursorPosition = (int)offset;
        SelectionStart = (int)offset;
        SelectionLength = 1;
        CloseGoToOffset();
    }

    private static bool TryParseOffset(ReadOnlySpan<char> text, out long offset)
    {
        if (text.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
            return long.TryParse(text[2..], NumberStyles.HexNumber, CultureInfo.InvariantCulture, out offset);

        if (long.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out offset))
            return true;

        return long.TryParse(text, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out offset);
    }

    [RelayCommand]
    private void OpenSearch()
    {
        IsSearchVisible = true;
    }

    [RelayCommand]
    private void CloseSearch()
    {
        IsSearchVisible = false;
        ClearSearchResults();
    }

    [RelayCommand(CanExecute = nameof(CanCloseOverlay))]
    private void CloseOverlay()
    {
        if (IsGoToOffsetVisible) CloseGoToOffset();
        else if (IsSearchVisible) CloseSearch();
    }

    private bool CanCloseOverlay() => IsGoToOffsetVisible || IsSearchVisible;

    [RelayCommand]
    private void FindNext()
    {
        if (SearchMatchOffsets.Length == 0)
        {
            ExecuteSearch();
            return;
        }

        CurrentSearchMatchIndex = (CurrentSearchMatchIndex + 1) % SearchMatchOffsets.Length;
        NavigateToCurrentMatch();
    }

    [RelayCommand]
    private void FindPrevious()
    {
        if (SearchMatchOffsets.Length == 0)
        {
            ExecuteSearch();
            return;
        }

        CurrentSearchMatchIndex = (CurrentSearchMatchIndex - 1 + SearchMatchOffsets.Length) % SearchMatchOffsets.Length;
        NavigateToCurrentMatch();
    }

    partial void OnSearchTextChanged(string value) => ExecuteSearch();
    partial void OnIsHexSearchChanged(bool value) => ExecuteSearch();

    private void ClearSearchResults()
    {
        SearchMatchOffsets = [];
        SearchMatchLength = 0;
        CurrentSearchMatchIndex = -1;
        SearchStatusText = "";
    }

    private void ExecuteSearch()
    {
        var buffer = Document?.Buffer;
        if (buffer is null || string.IsNullOrEmpty(SearchText))
        {
            ClearSearchResults();
            return;
        }

        var pattern = IsHexSearch ? ParseHexString(SearchText) : Encoding.UTF8.GetBytes(SearchText);
        if (pattern is null || pattern.Length == 0)
        {
            ClearSearchResults();
            if (IsHexSearch) SearchStatusText = "無効な16進数";
            return;
        }

        SearchMatchLength = pattern.Length;
        var dataLength = (int)buffer.Length;
        var matches = new List<int>();

        const int chunkSize = 65536;
        var overlap = pattern.Length - 1;
        var chunk = ArrayPool<byte>.Shared.Rent(chunkSize + overlap);
        try
        {
            for (var offset = 0; offset < dataLength;)
            {
                var readStart = offset == 0 ? 0 : offset - overlap;
                var readLength = Math.Min(chunkSize + overlap, dataLength - readStart);
                buffer.ReadBytes(readStart, chunk, 0, readLength);

                var searchStart = offset == 0 ? 0 : overlap;
                var span = chunk.AsSpan(searchStart, readLength - searchStart);
                var pos = 0;
                while (pos <= span.Length - pattern.Length)
                {
                    var idx = span[pos..].IndexOf(pattern);
                    if (idx < 0) break;
                    matches.Add(readStart + searchStart + pos + idx);
                    pos += idx + 1;
                }

                offset += chunkSize;
            }
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(chunk);
        }

        SearchMatchOffsets = [.. matches];

        if (matches.Count == 0)
        {
            CurrentSearchMatchIndex = -1;
            SearchStatusText = "0/0";
            return;
        }

        var closestIndex = FindClosestMatch(matches, CursorPosition);
        CurrentSearchMatchIndex = closestIndex;
        NavigateToCurrentMatch();
    }

    private void NavigateToCurrentMatch()
    {
        if (CurrentSearchMatchIndex < 0 || CurrentSearchMatchIndex >= SearchMatchOffsets.Length)
            return;

        var matchOffset = SearchMatchOffsets[CurrentSearchMatchIndex];
        CursorPosition = matchOffset;
        SelectionStart = matchOffset;
        SelectionLength = SearchMatchLength;
        SearchStatusText = $"{CurrentSearchMatchIndex + 1}/{SearchMatchOffsets.Length}";
    }

    private static int FindClosestMatch(List<int> matches, int position)
    {
        var lo = 0;
        var hi = matches.Count - 1;
        while (lo < hi)
        {
            var mid = lo + (hi - lo) / 2;
            if (matches[mid] < position)
                lo = mid + 1;
            else
                hi = mid;
        }
        if (lo >= matches.Count) return matches.Count - 1;
        if (lo == 0) return 0;

        var distBefore = position - matches[lo - 1];
        var distAfter = matches[lo] - position;
        return distBefore <= distAfter ? lo - 1 : lo;
    }
}
