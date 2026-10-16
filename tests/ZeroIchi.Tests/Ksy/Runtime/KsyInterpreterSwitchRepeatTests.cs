using ZeroIchi.Models.FileStructure;
using ZeroIchi.Models.FileStructure.Ksy.Runtime;
using static ZeroIchi.Tests.Ksy.Runtime.KsyTestRunner;

namespace ZeroIchi.Tests.Ksy.Runtime;

public sealed class KsyInterpreterSwitchRepeatTests
{
    private const string SwitchYaml = """
        meta:
          id: x
        seq:
          - id: tag
            type: u1
            enum: kind
          - id: body
            type:
              switch-on: tag
              cases:
                kind::one: one
                kind::two: two
        types:
          one:
            seq:
              - id: a
                type: u1
          two:
            seq:
              - id: b
                type: u1
              - id: c
                type: u1
        enums:
          kind:
            1: one
            2: two
        """;

    [Fact]
    public void Parse_SwitchReadsTheMatchingCase()
    {
        var root = Parse(SwitchYaml, [2, 7, 8]);

        Assert.Equal("b,c", Names(Node(root, "body")));
        Assert.Equal((1L, 2L), (Node(root, "body").Offset, Node(root, "body").Length));
    }

    [Fact]
    public void Parse_SwitchWithoutAMatchOrSizeReadsNothing()
    {
        var root = Parse(SwitchYaml, [3, 7]);

        var body = Node(root, "body");
        Assert.Equal((1L, 0L, "", false), (body.Offset, body.Length, body.Description, body.HasChildren));
        Assert.Null(body.Error);
    }

    [Fact]
    public void Parse_SwitchWithoutAMatchButWithASizeReadsRawBytes()
    {
        var root = Parse("""
            meta:
              id: x
            seq:
              - id: tag
                type: u1
              - id: body
                size: 2
                type:
                  switch-on: tag
                  cases:
                    1: one
              - id: after
                type: u1
            types:
              one:
                seq:
                  - id: a
                    type: u1
            """, [9, 0xAA, 0xBB, 0x05]);

        Assert.Equal(("AA BB", ValueKind.Bytes), (Node(root, "body").Description, Node(root, "body").ValueKind));
        Assert.Equal("5", Node(root, "after").Description);
    }

    [Fact]
    public void Parse_SwitchFallsBackToTheDefaultCase()
    {
        var root = Parse("""
            meta:
              id: x
            seq:
              - id: tag
                type: u1
              - id: body
                type:
                  switch-on: tag
                  cases:
                    _: u2le
                    1: u1
            """, [9, 0x01, 0x02]);

        Assert.Equal(("513", 2L), (Node(root, "body").Description, Node(root, "body").Length));
    }

    [Fact]
    public void Parse_RepeatEosNamesEachElement()
    {
        var root = Parse("meta:\n  id: x\nseq:\n  - id: items\n    type: u1\n    repeat: eos\n", [4, 5, 6]);

        var items = Node(root, "items");
        Assert.Equal(("items[0],items[1],items[2]", 0L, 3L, "", ValueKind.None), (Names(items), items.Offset, items.Length, items.Description, items.ValueKind));
        Assert.Equal((2L, "6", "items"), (Node(root, "items/items[2]").Offset, Node(root, "items/items[2]").Description, Node(root, "items/items[2]").FieldId));
    }

    [Fact]
    public void Parse_EmptyRepeatStillHasAContainer()
    {
        var root = Parse("meta:\n  id: x\nseq:\n  - id: items\n    type: u1\n    repeat: eos\n", []);

        Assert.Equal("items", Names(root));
        Assert.Equal((0L, 0L, false), (Node(root, "items").Offset, Node(root, "items").Length, Node(root, "items").HasChildren));
    }

    [Fact]
    public void Parse_RepeatExprCanUseTheIndex()
    {
        var root = Parse("""
            meta:
              id: x
            seq:
              - id: count
                type: u1
              - id: chunks
                size: _index + 1
                repeat: expr
                repeat-expr: count
              - id: second
                size: chunks[1].length
            """, [3, 0xA, 0xB, 0xB, 0xC, 0xC, 0xC, 0xD, 0xD]);

        Assert.Equal("chunks[0],chunks[1],chunks[2]", Names(Node(root, "chunks")));
        Assert.Equal("0C 0C 0C", Node(root, "chunks/chunks[2]").Description);
        Assert.Equal((7L, 2L), (Node(root, "second").Offset, Node(root, "second").Length));
    }

    [Fact]
    public void Parse_RepeatUntilIncludesTheLastElement()
    {
        var root = Parse("meta:\n  id: x\nseq:\n  - id: items\n    type: u1\n    repeat: until\n    repeat-until: _ == 0\n  - id: after\n    type: u1\n", [3, 0, 9]);

        Assert.Equal("items[0],items[1]", Names(Node(root, "items")));
        Assert.Equal("9", Node(root, "after").Description);
    }

    [Fact]
    public void Parse_RepeatedBitsCoverTheTouchedBytes()
    {
        var root = Parse("meta:\n  id: x\nseq:\n  - id: pad\n    type: b4\n  - id: nibbles\n    type: b4\n    repeat: expr\n    repeat-expr: 3\n", [0x12, 0x34]);

        var nibbles = Node(root, "nibbles");
        Assert.Equal((0L, 2L), (nibbles.Offset, nibbles.Length));
        Assert.Equal(("2", "3", "4"), (Node(root, "nibbles/nibbles[0]").Description, Node(root, "nibbles/nibbles[1]").Description, Node(root, "nibbles/nibbles[2]").Description));
    }

    [Fact]
    public void Parse_EndlessRepeatOfEmptyElementsStopsAtTheBudget()
    {
        var root = Parse("""
            meta:
              id: x
            seq:
              - id: items
                type: empty
                repeat: eos
            types:
              empty:
                seq: []
            """, [1], new KsyParseOptions { NodeBudget = 50 });

        Assert.Equal("NodeBudget(50)", Summary(Node(root, "未解析").Error));
        Assert.True(Node(root, "items").IsIncomplete);
    }

    [Fact]
    public void Parse_ValidationFailuresAreMarkedAndParsingContinues()
    {
        var root = Parse("""
            meta:
              id: x
            seq:
              - id: eq
                type: u1
                valid: 1
              - id: range
                type: u1
                valid:
                  min: 2
                  max: 4
              - id: any
                type: u1
                valid:
                  any-of: [1, 2]
              - id: known
                type: u1
                enum: kind
                valid:
                  in-enum: true
              - id: even
                type: u1
                valid:
                  expr: _ % 2 == 0
              - id: items
                type: u1
                repeat: expr
                repeat-expr: 2
                valid:
                  max: 5
              - id: low
                type: u1
                valid:
                  min: 5
            enums:
              kind:
                1: one
            """, [2, 9, 3, 7, 3, 1, 6, 1]);

        Assert.Equal("Validation failed: expected 1, got 2", Node(root, "eq").Error?.Message);
        Assert.Equal("Validation failed: 9 is greater than 4", Node(root, "range").Error?.Message);
        Assert.Equal("Validation failed: 3 is not any of the allowed values", Node(root, "any").Error?.Message);
        Assert.Equal("Validation failed: 7 is not defined in enum 'kind'", Node(root, "known").Error?.Message);
        Assert.Equal("Validation failed: 3 does not satisfy the expression", Node(root, "even").Error?.Message);
        Assert.Null(Node(root, "items/items[0]").Error);
        Assert.Equal("Validation failed: 6 is greater than 5", Node(root, "items/items[1]").Error?.Message);
        Assert.Equal("Validation failed: 1 is less than 5", Node(root, "low").Error?.Message);
        Assert.Equal("ValidEq(1, 2)", Summary(Node(root, "eq").Error));
        Assert.Equal("ValidMax(9, 4)", Summary(Node(root, "range").Error));
        Assert.Equal("ValidAnyOf(3)", Summary(Node(root, "any").Error));
        Assert.Equal("ValidInEnum(kind, 7)", Summary(Node(root, "known").Error));
        Assert.Equal("ValidExpr(3)", Summary(Node(root, "even").Error));
        Assert.Equal("ValidMin(1, 5)", Summary(Node(root, "low").Error));
        Assert.DoesNotContain(root.Children, c => c.IsUnparsed);
    }

    [Fact]
    public void Parse_ValidationWithinRangeHasNoError()
    {
        var root = Parse("meta:\n  id: x\nseq:\n  - id: v\n    type: u1\n    valid:\n      min: 2\n      max: 4\n", [2]);

        Assert.Null(Node(root, "v").Error);
    }
}
