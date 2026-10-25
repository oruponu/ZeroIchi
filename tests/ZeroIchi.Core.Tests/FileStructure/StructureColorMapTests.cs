using ZeroIchi.Models.FileStructure;

namespace ZeroIchi.Tests.FileStructure;

public sealed class StructureColorMapTests
{
    private static FileStructureNode Leaf(long offset, long length, ValueKind kind) =>
        new() { Name = "n", Offset = offset, Length = length, ValueKind = kind };

    private static StructureColorMap Build(params FileStructureNode[] leaves) =>
        StructureColorMap.Build(new FileStructureNode { Name = "root", Offset = 0, Length = 0, Children = leaves });

    [Fact]
    public void GetValueKind_FindsRangesGivenOutOfOrder()
    {
        var map = Build(Leaf(10, 2, ValueKind.Numeric), Leaf(0, 2, ValueKind.Ascii));

        Assert.Equal(ValueKind.Ascii, map.GetValueKind(1));
        Assert.Equal(ValueKind.Numeric, map.GetValueKind(11));
        Assert.Equal(ValueKind.None, map.GetValueKind(5));
    }

    [Fact]
    public void GetValueKind_LaterStartingRangeWinsWhereRangesOverlap()
    {
        var map = Build(Leaf(0, 4, ValueKind.Bytes), Leaf(2, 1, ValueKind.Numeric));

        Assert.Equal(ValueKind.Bytes, map.GetValueKind(1));
        Assert.Equal(ValueKind.Numeric, map.GetValueKind(2));
        Assert.Equal(ValueKind.Bytes, map.GetValueKind(3));
        Assert.Equal(ValueKind.None, map.GetValueKind(4));
    }

    [Fact]
    public void GetValueKind_RangesStartingTogetherPreferTheLaterNode()
    {
        var map = Build(Leaf(0, 2, ValueKind.Numeric), Leaf(0, 1, ValueKind.Ascii));

        Assert.Equal(ValueKind.Ascii, map.GetValueKind(0));
        Assert.Equal(ValueKind.Numeric, map.GetValueKind(1));
    }
}
