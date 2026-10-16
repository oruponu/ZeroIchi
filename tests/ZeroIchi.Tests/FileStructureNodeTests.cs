using ZeroIchi.Models.FileStructure;

namespace ZeroIchi.Tests;

public sealed class FileStructureNodeTests
{
    [Fact]
    public void NewNode_HasRangeWithoutErrorAndIsComplete()
    {
        var node = new FileStructureNode { Name = "n", Offset = 0, Length = 1 };

        Assert.True(node.HasRange);
        Assert.Null(node.Error);
        Assert.False(node.IsIncomplete);
    }

    [Fact]
    public void Node_KeepsRangeErrorAndIncompleteFlag()
    {
        var node = new FileStructureNode
        {
            Name = "n",
            Offset = 0,
            Length = 0,
            HasRange = false,
            Error = FileStructureError.Other("Validation failed"),
            IsIncomplete = true,
        };

        Assert.False(node.HasRange);
        Assert.Equal("Validation failed", node.Error?.Message);
        Assert.True(node.IsIncomplete);
    }
}
