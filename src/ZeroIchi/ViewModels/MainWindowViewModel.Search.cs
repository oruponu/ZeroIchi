using CommunityToolkit.Mvvm.Input;
using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using ZeroIchi.Models;
using ZeroIchi.Models.Buffers;
using ZeroIchi.Models.Search;

namespace ZeroIchi.ViewModels;

public partial class MainWindowViewModel
{
    private const int MaxSearchMatches = 100_000;

    private CancellationTokenSource? _searchCts;
    private Task _searchTail = Task.CompletedTask;
    private SearchMatchSet? _searchSet;
    private long _searchStart;
    private bool _searchSwapped;
    private (SearchDirection Direction, long From)? _pendingSearchMove;

    internal TimeSpan SearchFlushInterval { get; init; } = TimeSpan.FromMilliseconds(100);

    internal TimeSpan SearchBusyDelay { get; init; } = TimeSpan.FromMilliseconds(300);

    internal Func<ByteBuffer, ByteBuffer> SearchSource { get; init; } = static snapshot => snapshot;

    internal Task SearchUpdate { get; private set; } = Task.CompletedTask;

    public event EventHandler? GoToOffsetFocusRequested;

    public event EventHandler? SearchFocusRequested;

    private enum SearchDirection
    {
        Forward,
        Backward,
    }

    [RelayCommand]
    private void OpenGoToOffset()
    {
        if (IsSearchVisible) CloseSearch();
        IsGoToOffsetVisible = true;
        GoToOffsetError = "";
        GoToOffsetFocusRequested?.Invoke(this, EventArgs.Empty);
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
        if (IsGoToOffsetVisible) CloseGoToOffset();
        IsSearchVisible = true;
        SearchFocusRequested?.Invoke(this, EventArgs.Empty);
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
    private void FindNext() => NavigateSearch(SearchDirection.Forward);

    [RelayCommand]
    private void FindPrevious() => NavigateSearch(SearchDirection.Backward);

    partial void OnSearchTextChanged(string value) => StartSearch(SearchDirection.Forward);
    partial void OnIsHexSearchChanged(bool value) => StartSearch(SearchDirection.Forward);

    private void NavigateSearch(SearchDirection direction)
    {
        var set = _searchSet;
        if (set is null || set is { IsComplete: true, Count: 0 })
        {
            StartSearch(direction);
            return;
        }

        _pendingSearchMove = _searchSwapped && CurrentSearchMatchOffset >= 0
            ? (direction, CurrentSearchMatchOffset)
            : InitialSearchMove(direction, _searchStart);

        if (!_searchSwapped)
            return;

        ResolvePendingSearchMove(set);
        UpdateSearchStatus(set);
    }

    private static (SearchDirection Direction, long From) InitialSearchMove(SearchDirection direction, long start) =>
        direction == SearchDirection.Forward ? (direction, start - 1) : (direction, start);

    private void ClearSearchResults()
    {
        _searchCts?.Cancel();
        _searchCts = null;
        _searchSet = null;
        _searchSwapped = false;
        _pendingSearchMove = null;
        SearchMatchOffsets = [];
        SearchMatchLength = 0;
        CurrentSearchMatchOffset = -1;
        CurrentSearchMatchIndex = -1;
        SearchStatusText = "";
        IsSearching = false;
    }

    private void StartSearch(SearchDirection direction)
    {
        _searchCts?.Cancel();
        _searchCts = null;

        var document = Document;
        if (document?.Buffer is not { } buffer || string.IsNullOrEmpty(SearchText))
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

        var start = Math.Min(CursorPosition, buffer.Length);
        var set = new SearchMatchSet(start, buffer.Length);
        _searchSet = set;
        _searchStart = start;
        _searchSwapped = false;
        _pendingSearchMove = InitialSearchMove(direction, start);
        _searchCts = new CancellationTokenSource();
        SearchUpdate = RunSearchAsync(document, pattern, start, set, _searchCts.Token);
    }

    // Task.Run takes no token: cancelling before it starts would skip awaiting the previous scan
    private async Task RunSearchAsync(BinaryDocument document, byte[] pattern, long start, SearchMatchSet set,
        CancellationToken cancellationToken)
    {
        var startedAt = Stopwatch.GetTimestamp();
        try
        {
            var snapshot = SearchSource(document.CreateSnapshot());
            var reports = new ConcurrentQueue<SearchProgress>();
            var previous = _searchTail;
            var scan = Task.Run(async () =>
            {
                await previous;
                cancellationToken.ThrowIfCancellationRequested();
                ByteSearcher.Scan(snapshot, pattern, start, MaxSearchMatches, reports.Enqueue, cancellationToken);
            });
            _searchTail = scan.ContinueWith(static _ => { }, TaskScheduler.Default);

            while (true)
            {
                await Task.WhenAny(scan, Task.Delay(SearchFlushInterval, cancellationToken));
                if (cancellationToken.IsCancellationRequested) return;

                var done = scan.IsCompleted;
                if (done) await scan;
                while (reports.TryDequeue(out var progress))
                    set.Add(progress);
                if (done) set.Complete();

                ApplySearchProgress(set, pattern.Length);
                if (set.IsComplete)
                {
                    IsSearching = false;
                    return;
                }
                if (Stopwatch.GetElapsedTime(startedAt) >= SearchBusyDelay) IsSearching = true;
            }
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception e) when (e is not OutOfMemoryException)
        {
            if (cancellationToken.IsCancellationRequested || _searchSet != set) return;
            ClearSearchResults();
            SearchStatusText = "検索できませんでした";
        }
    }

    private void ApplySearchProgress(SearchMatchSet set, int patternLength)
    {
        if (!_searchSwapped)
        {
            if (set.Count == 0 && !set.IsComplete) return;

            _searchSwapped = true;
            SearchMatchLength = patternLength;
            CurrentSearchMatchOffset = -1;
            SearchMatchOffsets = set.ToArray();
        }
        else if (set.Count != SearchMatchOffsets.Length)
        {
            SearchMatchOffsets = set.ToArray();
        }

        ResolvePendingSearchMove(set);
        UpdateSearchStatus(set);
    }

    private void ResolvePendingSearchMove(SearchMatchSet set)
    {
        if (_pendingSearchMove is not { } move)
            return;

        var target = move.Direction == SearchDirection.Forward
            ? set.FindNext(move.From)
            : set.FindPrevious(move.From);
        if (!target.IsResolved)
            return;

        _pendingSearchMove = null;
        if (target.Offset >= 0) MoveToSearchMatch(target.Offset);
    }

    private void MoveToSearchMatch(long offset)
    {
        CursorPosition = offset;
        SelectionStart = offset;
        SelectionLength = SearchMatchLength;
        CurrentSearchMatchOffset = offset;
    }

    private void UpdateSearchStatus(SearchMatchSet set)
    {
        var offsets = SearchMatchOffsets;
        var count = offsets.Length;
        var truncated = count >= MaxSearchMatches ? "+" : "";
        var index = CurrentSearchMatchOffset >= 0 ? Array.BinarySearch(offsets, CurrentSearchMatchOffset) : -1;
        if (index < 0) index = -1;
        CurrentSearchMatchIndex = index;

        if (index >= 0)
            SearchStatusText = $"{index + 1}/{count}{truncated}";
        else if (count > 0)
            SearchStatusText = $"-/{count}{truncated}";
        else
            SearchStatusText = set.IsComplete ? "0/0" : "";
    }
}
