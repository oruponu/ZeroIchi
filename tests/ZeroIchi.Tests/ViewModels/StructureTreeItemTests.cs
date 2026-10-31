using Avalonia.Media;
using System.Globalization;
using ZeroIchi.Localization;
using ZeroIchi.Models.FileStructure;
using ZeroIchi.Tests.Localization;
using ZeroIchi.ViewModels;

namespace ZeroIchi.Tests.ViewModels;

[Collection(HeadlessTestApp.Collection)]
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

    [Theory]
    [InlineData(ValueKind.None)]
    [InlineData(ValueKind.Numeric)]
    [InlineData(ValueKind.Ascii)]
    [InlineData(ValueKind.Bytes)]
    public void DescriptionForeground_IsImmutableSoAnyThreadCanRenderIt(ValueKind kind)
    {
        var item = new StructureTreeItem(new FileStructureNode { Name = "n", Offset = 0, Length = 1, ValueKind = kind }, 0);

        Assert.IsAssignableFrom<IImmutableBrush>(item.DescriptionForeground);
        Assert.IsAssignableFrom<IImmutableBrush>(Item("5", "broken").DescriptionForeground);
    }

    [Fact]
    public void RefreshText_RaisesNameAndDescription()
    {
        var item = Item("5");
        var names = new List<string?>();
        item.PropertyChanged += (_, e) => names.Add(e.PropertyName);

        item.RefreshText();

        Assert.Contains(nameof(StructureTreeItem.Name), names);
        Assert.Contains(nameof(StructureTreeItem.Description), names);
    }

    [Fact]
    public Task Texts_FollowTheCulture() => HeadlessTestApp.RunAsync(() =>
    {
        var unparsed = new StructureTreeItem(new FileStructureNode
        {
            Name = "",
            Offset = 0,
            Length = 1,
            IsUnparsed = true,
        }, 0);
        var incompleteWithError = new StructureTreeItem(new FileStructureNode
        {
            Name = "n",
            Offset = 0,
            Length = 0,
            IsIncomplete = true,
            Error = FileStructureError.EndOfData(1, false, 4, 2, "Unexpected end of stream"),
        }, 0);

        using var _ = FakeEnglishStrings.Use();
        try
        {
            Localizer.Instance.SetCulture(CultureInfo.GetCultureInfo("en-US"));

            Assert.Equal("en:未解析", unparsed.Name);
            Assert.StartsWith("en:(未完了)  ", incompleteWithError.Description);
            Assert.Contains("en:データ不足: en:位置 0x1 で", incompleteWithError.Description);
        }
        finally
        {
            Localizer.Instance.SetCulture(CultureInfo.GetCultureInfo("ja"));
        }
        return Task.CompletedTask;
    });

    [Fact]
    public void FontStyle_IsItalicWithoutAFileRange()
    {
        Assert.Equal(FontStyle.Normal, Item("5").FontStyle);
        Assert.Equal(FontStyle.Italic, Item("5", hasRange: false).FontStyle);
    }
}
