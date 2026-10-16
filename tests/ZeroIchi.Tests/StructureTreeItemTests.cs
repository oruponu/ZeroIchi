using Avalonia.Media;
using ZeroIchi.Models.FileStructure;
using ZeroIchi.ViewModels;

namespace ZeroIchi.Tests;

public sealed class StructureTreeItemTests
{
    private static StructureTreeItem Item(string description = "", string? error = null, bool hasRange = true,
        bool isIncomplete = false) =>
        new(new FileStructureNode
        {
            Name = "n",
            Offset = 0,
            Length = hasRange ? 1 : 0,
            Description = description,
            ValueKind = ValueKind.Numeric,
            Error = error is null ? null : FileStructureError.Other(error),
            HasRange = hasRange,
            IsIncomplete = isIncomplete,
        }, 0);

    [Fact]
    public void Description_FollowsTheValueWithTheError()
    {
        Assert.Equal("5", Item("5").Description);
        Assert.Equal("5  Validation failed: expected 1, got 5", Item("5", "Validation failed: expected 1, got 5").Description);
        Assert.Equal("Unexpected end of stream", Item("", "Unexpected end of stream").Description);
    }

    [Fact]
    public void Description_ShowsTheErrorInJapanese()
    {
        var item = new StructureTreeItem(new FileStructureNode
        {
            Name = "n",
            Offset = 0,
            Length = 0,
            Error = FileStructureError.EndOfData(1, false, 4, 2, "Unexpected end of stream"),
        }, 0);

        Assert.Equal("データ不足: 位置 0x1 で 4 バイト必要 (残り 2 バイト)", item.Description);
    }

    [Fact]
    public void Description_MarksIncompleteNodes()
    {
        Assert.Equal("5 (未完了)", Item("5", isIncomplete: true).Description);
        Assert.Equal("(未完了)", Item(isIncomplete: true).Description);
        Assert.Equal("(未完了)  broken", Item(error: "broken", isIncomplete: true).Description);
    }

    [Fact]
    public void UnparsedNode_ShowsItsReasonInTheDefaultColor()
    {
        var item = new StructureTreeItem(new FileStructureNode
        {
            Name = "",
            Offset = 3,
            Length = 5,
            IsUnparsed = true,
            Error = FileStructureError.Limit(FileStructureErrorKind.NodeBudget, 50, "Node budget exceeded"),
        }, 0);

        Assert.Equal(("未解析", "ノード数の上限 (50) に到達"), (item.Name, item.Description));
        Assert.Equal(Color.Parse("#808080"), Assert.IsAssignableFrom<ISolidColorBrush>(item.DescriptionForeground).Color);
    }

    [Fact]
    public void DescriptionForeground_IsRedWhenThereIsAnError()
    {
        var normal = Assert.IsAssignableFrom<ISolidColorBrush>(Item("5").DescriptionForeground);
        var error = Assert.IsAssignableFrom<ISolidColorBrush>(Item("5", "broken").DescriptionForeground);

        Assert.Equal(Color.Parse("#B5CEA8"), normal.Color);
        Assert.Equal(Color.Parse("#F48771"), error.Color);
    }

    [Fact]
    public void FontStyle_IsItalicWithoutAFileRange()
    {
        Assert.Equal(FontStyle.Normal, Item("5").FontStyle);
        Assert.Equal(FontStyle.Italic, Item("5", hasRange: false).FontStyle);
    }
}
