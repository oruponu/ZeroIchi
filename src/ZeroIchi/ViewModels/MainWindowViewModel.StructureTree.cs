using CommunityToolkit.Mvvm.Input;
using System.Collections.Generic;
using ZeroIchi.Models;
using ZeroIchi.Models.Buffers;
using ZeroIchi.Models.FileStructure;

namespace ZeroIchi.ViewModels;

public partial class MainWindowViewModel
{
    partial void OnSelectedStructureItemChanged(StructureTreeItem? value)
    {
        if (_isSyncingSelection) return;
        if (value is null || Document?.Buffer is null) return;
        _isSyncingSelection = true;
        CursorPosition = (int)value.Node.Offset;
        SelectionStart = (int)value.Node.Offset;
        SelectionLength = value.Node.Length;
        _isSyncingSelection = false;
    }

    private void SyncStructureTreeSelection(int cursorPosition)
    {
        StructureTreeItem? best = null;
        foreach (var item in StructureTreeItems)
        {
            var offset = (int)item.Node.Offset;
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

    private void UpdateStructureTree(ByteBuffer buffer)
    {
        var expanded = new HashSet<(string name, int depth)>();
        foreach (var item in StructureTreeItems)
        {
            if (item.HasChildren && item.IsExpanded)
                expanded.Add((item.Name, item.Depth));
        }

        StructureTreeItems.Clear();
        SelectedStructureItem = null;

        var definition = DefinitionRegistry.TryMatch(buffer);
        if (definition is null)
        {
            StructureColors = null;
            return;
        }

        var root = StructureParser.Parse(definition, buffer);
        StructureColors = StructureColorMap.Build(root);
        foreach (var child in root.Children)
            AddTreeItem(child, 0, expanded);
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
