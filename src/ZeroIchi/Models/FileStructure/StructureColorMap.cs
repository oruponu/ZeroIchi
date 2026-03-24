using System;
using System.Collections.Generic;

namespace ZeroIchi.Models.FileStructure;

public sealed class StructureColorMap
{
    private readonly long[] _offsets;
    private readonly int[] _lengths;
    private readonly ValueKind[] _kinds;

    private StructureColorMap(List<(long Offset, int Length, ValueKind Kind)> ranges)
    {
        _offsets = new long[ranges.Count];
        _lengths = new int[ranges.Count];
        _kinds = new ValueKind[ranges.Count];

        for (var i = 0; i < ranges.Count; i++)
        {
            _offsets[i] = ranges[i].Offset;
            _lengths[i] = ranges[i].Length;
            _kinds[i] = ranges[i].Kind;
        }
    }

    public ValueKind GetValueKind(long offset)
    {
        var idx = Array.BinarySearch(_offsets, offset);
        if (idx >= 0)
            return _kinds[idx];

        idx = ~idx - 1;
        if (idx >= 0 && offset < _offsets[idx] + _lengths[idx])
            return _kinds[idx];

        return ValueKind.None;
    }

    public static StructureColorMap Build(FileStructureNode root)
    {
        var ranges = new List<(long Offset, int Length, ValueKind Kind)>();
        CollectLeafRanges(root, ranges);
        return new StructureColorMap(ranges);
    }

    private static void CollectLeafRanges(FileStructureNode node,
        List<(long Offset, int Length, ValueKind Kind)> ranges)
    {
        if (node.Children.Count == 0)
        {
            if (node.ValueKind != ValueKind.None && node.Length > 0)
                ranges.Add((node.Offset, node.Length, node.ValueKind));
            return;
        }

        foreach (var child in node.Children)
            CollectLeafRanges(child, ranges);
    }
}
