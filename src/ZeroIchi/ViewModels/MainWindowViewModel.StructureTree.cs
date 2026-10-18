using CommunityToolkit.Mvvm.Input;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using ZeroIchi.Models;
using ZeroIchi.Models.FileStructure;
using ZeroIchi.Models.FileStructure.Ksy;

namespace ZeroIchi.ViewModels;

public partial class MainWindowViewModel
{
    private CancellationTokenSource? _structureCts;
    private Task _parseTail = Task.CompletedTask;
    private int _structurePauseCount;
    private bool _structureUpdatePending;
    private HashSet<(string name, int depth)> _expandedHint = [];
    private int _busySequence;
    private int _busyOwner;

    internal KsyFormatRegistry KsyFormats { get; init; } = KsyFormatRegistry.Default;

    internal TimeSpan StructureDebounce { get; init; } = TimeSpan.FromMilliseconds(250);

    internal TimeSpan StructureBusyDelay { get; init; } = TimeSpan.FromMilliseconds(300);

    internal Task StructureUpdate { get; private set; } = Task.CompletedTask;

    partial void OnSelectedStructureItemChanged(StructureTreeItem? value)
    {
        if (_isSyncingSelection) return;
        if (value is null || Document?.Buffer is not { } buffer || !value.Node.HasRange) return;
        var length = buffer.Length;
        var offset = Math.Min(value.Node.Offset, length);
        _isSyncingSelection = true;
        CursorPosition = offset;
        SelectionStart = offset;
        SelectionLength = Math.Min(value.Node.Length, length - offset);
        _isSyncingSelection = false;
    }

    private void SyncStructureTreeSelection(long cursorPosition)
    {
        StructureTreeItem? best = null;
        foreach (var item in StructureTreeItems)
        {
            var offset = item.Node.Offset;
            if (offset <= cursorPosition && cursorPosition < offset + item.Node.Length)
            {
                if (best is null || item.Depth >= best.Depth)
                    best = item;
            }
        }

        if (best != SelectedStructureItem)
        {
            _isSyncingSelection = true;
            SelectedStructureItem = best;
            _isSyncingSelection = false;
        }
    }

    partial void OnIsInspectorVisibleChanged(bool value)
    {
        if (value) UpdateInspector();
    }

    partial void OnIsInspectorBigEndianChanged(bool value) => UpdateInspector();

    private void ScheduleStructureUpdate(bool immediate)
    {
        _structureCts?.Cancel();
        _structureCts = null;

        if (Document is not { Buffer.Length: > 0 } document)
        {
            ClearStructure();
            StructureUpdate = Task.CompletedTask;
            return;
        }

        if (_structurePauseCount > 0)
        {
            _structureUpdatePending = true;
            return;
        }

        _structureCts = new CancellationTokenSource();
        StructureUpdate = UpdateStructureAsync(document, DataVersion, immediate, _structureCts.Token);
    }

    // Task.Run takes no token: cancelling before it starts would skip awaiting the previous parse
    private async Task UpdateStructureAsync(BinaryDocument document, int version, bool immediate,
        CancellationToken cancellationToken)
    {
        var busyId = 0;
        try
        {
            if (!immediate) await Task.Delay(StructureDebounce, cancellationToken);
            UpdateFileType(document.Buffer);
            var snapshot = document.CreateSnapshot();
            var formats = KsyFormats;
            var filePath = document.FilePath;
            var previous = _parseTail;
            var parse = Task.Run(async () =>
            {
                await previous;
                cancellationToken.ThrowIfCancellationRequested();
                return StructureAnalysis.Analyze(formats, snapshot, filePath, cancellationToken);
            });
            _parseTail = parse.ContinueWith(static _ => { }, TaskScheduler.Default);
            if (await Task.WhenAny(parse, Task.Delay(StructureBusyDelay, cancellationToken)) != parse
                && !parse.IsCompleted && !cancellationToken.IsCancellationRequested)
            {
                busyId = ++_busySequence;
                _busyOwner = busyId;
                IsStructureParsing = true;
            }
            var analysis = await parse;
            if (cancellationToken.IsCancellationRequested || Document != document || DataVersion != version) return;
            ApplyStructure(analysis);
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception e) when (e is not OutOfMemoryException)
        {
            if (!cancellationToken.IsCancellationRequested && Document == document) ApplyStructure(null);
        }
        finally
        {
            if (busyId != 0 && _busyOwner == busyId) IsStructureParsing = false;
        }
    }

    private void ApplyStructure(StructureAnalysis? analysis)
    {
        var expanded = StructureTreeItems.Count > 0 ? CollectExpanded() : _expandedHint;
        StructureTreeItems.Clear();
        SelectedStructureItem = null;

        if (analysis is null)
        {
            StructureColors = null;
            return;
        }

        StructureColors = analysis.Colors;
        foreach (var child in analysis.Root.Children)
            AddTreeItem(child, 0, expanded);
        SyncStructureTreeSelection(CursorPosition);
    }

    private void ClearStructure()
    {
        if (StructureTreeItems.Count > 0) _expandedHint = CollectExpanded();
        StructureTreeItems.Clear();
        StructureColors = null;
        SelectedStructureItem = null;
    }

    private HashSet<(string name, int depth)> CollectExpanded()
    {
        var expanded = new HashSet<(string name, int depth)>();
        foreach (var item in StructureTreeItems)
        {
            if (item.HasChildren && item.IsExpanded)
                expanded.Add((item.Name, item.Depth));
        }
        return expanded;
    }

    internal async Task PauseStructureUpdatesAsync()
    {
        _structurePauseCount++;
        if (!StructureUpdate.IsCompleted) _structureUpdatePending = true;
        _structureCts?.Cancel();
        _structureCts = null;
        await _parseTail;
    }

    internal void ResumeStructureUpdates()
    {
        _structurePauseCount--;
        if (_structurePauseCount > 0 || !_structureUpdatePending) return;

        _structureUpdatePending = false;
        ScheduleStructureUpdate(immediate: true);
    }

    private void AddTreeItem(FileStructureNode node, int depth, HashSet<(string name, int depth)> expanded)
    {
        var isExpanded = expanded.Contains((node.Name, depth));
        var item = new StructureTreeItem(node, depth, isExpanded) { ToggleExpandCommand = ToggleExpandCommand };
        StructureTreeItems.Add(item);

        if (item.IsExpanded)
        {
            foreach (var child in node.Children)
                AddTreeItem(child, depth + 1, expanded);
        }
    }

    [RelayCommand]
    private void ToggleExpand(StructureTreeItem item)
    {
        var index = StructureTreeItems.IndexOf(item);
        if (index < 0) return;

        if (item.IsExpanded)
        {
            item.IsExpanded = false;
            var removeStart = index + 1;
            while (removeStart < StructureTreeItems.Count
                   && StructureTreeItems[removeStart].Depth > item.Depth)
            {
                StructureTreeItems.RemoveAt(removeStart);
            }
        }
        else
        {
            item.IsExpanded = true;
            var insertIndex = index + 1;
            foreach (var child in item.Node.Children)
            {
                StructureTreeItems.Insert(insertIndex++, new StructureTreeItem(child, item.Depth + 1) { ToggleExpandCommand = ToggleExpandCommand });
            }
        }
    }

    private void UpdateInspector()
    {
        if (!IsInspectorVisible || Document?.Buffer is not { } buffer)
        {
            InspectorEntries = [];
            return;
        }

        InspectorEntries = DataInspector.Inspect(buffer, CursorPosition, IsInspectorBigEndian);
    }
}
