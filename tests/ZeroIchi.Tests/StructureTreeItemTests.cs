using Avalonia.Media;
using ZeroIchi.Models.FileStructure;
using ZeroIchi.ViewModels;

namespace ZeroIchi.Tests;

public sealed class StructureTreeItemTests
{
    private static StructureTreeItem Item(string description = "", string? error = null, bool hasRange = true) =>
        new(new FileStructureNode
        {
            Name = "n",
            Offset = 0,
            Length = hasRange ? 1 : 0,
            Description = description,
            ValueKind = ValueKind.Numeric,
            Error = error,
            HasRange = hasRange,
        }, 0);

    [Fact]
    public void Description_FollowsTheValueWithTheError()
    {
        Assert.Equal("5", Item("5").Description);
        Assert.Equal("5  Validation failed: expected 1, got 5", Item("5", "Validation failed: expected 1, got 5").Description);
        Assert.Equal("Unexpected end of stream", Item("", "Unexpected end of stream").Description);
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
