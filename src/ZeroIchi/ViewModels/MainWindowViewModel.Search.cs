using CommunityToolkit.Mvvm.Input;
using System.Text;
using ZeroIchi.Models;
using ZeroIchi.Models.Search;

namespace ZeroIchi.ViewModels;

public partial class MainWindowViewModel
{
    private const int MaxSearchMatches = 100_000;

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

        if (!OffsetText.TryParse(text, IsHexGoToOffset, out var offset))
        {
            GoToOffsetError = "無効な値";
            return;
        }

        if (offset < 0 || offset >= buffer.Length)
        {
            GoToOffsetError = $"範囲外 (0 - {buffer.Length - 1:X})";
            return;
        }

        CursorPosition = offset;
        SelectionStart = offset;
        SelectionLength = 1;
        CloseGoToOffset();
    }

    partial void OnIsHexGoToOffsetChanged(bool value) => GoToOffsetError = "";

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

        var pattern = IsHexSearch ? HexText.Parse(SearchText) : Encoding.UTF8.GetBytes(SearchText);
        if (pattern is null || pattern.Length == 0)
        {
            ClearSearchResults();
            if (IsHexSearch) SearchStatusText = "無効な16進数";
            return;
        }

        SearchMatchLength = pattern.Length;
        var matches = ByteSearcher.FindMatches(buffer, pattern, MaxSearchMatches);

        SearchMatchOffsets = [.. matches];

        if (matches.Count == 0)
        {
            CurrentSearchMatchIndex = -1;
            SearchStatusText = "0/0";
            return;
        }

        var closestIndex = ByteSearcher.FindClosestMatch(matches, CursorPosition);
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
        var truncated = SearchMatchOffsets.Length >= MaxSearchMatches ? "+" : "";
        SearchStatusText = $"{CurrentSearchMatchIndex + 1}/{SearchMatchOffsets.Length}{truncated}";
    }
}
