using ZeroIchi.Models.FileStructure;
using ZeroIchi.Models.FileStructure.Ksy.Runtime;
using static ZeroIchi.Tests.Ksy.Runtime.KsyTestRunner;

namespace ZeroIchi.Tests.Ksy.Runtime;

public sealed class KsyInterpreterTests
{
    [Fact]
    public void Parse_IntegersFollowMetaEndianAndSuffix()
    {
        var root = Parse("""
            meta:
              id: sample
              endian: le
            seq:
              - id: a
                type: u2
              - id: b
                type: u2be
              - id: c
                type: s1
            """, [0x01, 0x02, 0x01, 0x02, 0xFF]);

        Assert.Equal("sample", root.Name);
        Assert.Equal(5, root.Length);
        Assert.Equal("a,b,c", Names(root));
        var a = Node(root, "a");
        Assert.Equal((0L, 2L, "513", ValueKind.Numeric, "a"), (a.Offset, a.Length, a.Description, a.ValueKind, a.FieldId));
        Assert.Equal("258", Node(root, "b").Description);
        Assert.Equal((4L, 1L, "-1"), (Node(root, "c").Offset, Node(root, "c").Length, Node(root, "c").Description));
    }

    [Fact]
    public void Parse_FloatIsDescribedInvariantly()
    {
        var root = Parse("meta:\n  id: x\nseq:\n  - id: f\n    type: f4be\n", [0x3F, 0xC0, 0x00, 0x00]);

        Assert.Equal("1.5", Node(root, "f").Description);
        Assert.Equal(ValueKind.Numeric, Node(root, "f").ValueKind);
    }

    [Fact]
    public void Parse_BitFieldsCoverTheBytesTheyTouch()
    {
        var root = Parse("""
            meta:
              id: x
            seq:
              - id: flag
                type: b1
              - id: mid
                type: b10
              - id: tail
                type: b5
              - id: after
                type: u1
            """, [0b1000_0000, 0b0100_0000, 0x07]);

        Assert.Equal((0L, 1L, "true"), (Node(root, "flag").Offset, Node(root, "flag").Length, Node(root, "flag").Description));
        Assert.Equal((0L, 2L, "2"), (Node(root, "mid").Offset, Node(root, "mid").Length, Node(root, "mid").Description));
        Assert.Equal((1L, 1L, "0"), (Node(root, "tail").Offset, Node(root, "tail").Length, Node(root, "tail").Description));
        Assert.Equal((2L, "7"), (Node(root, "after").Offset, Node(root, "after").Description));
    }

    [Fact]
    public void Parse_StringsAreDecodedAndTruncatedInTheDescription()
    {
        var text = new string('a', 70);
        var data = "AB\0CD"u8.ToArray().Concat(System.Text.Encoding.ASCII.GetBytes(text)).ToArray();

        var root = Parse("""
            meta:
              id: x
              encoding: ASCII
            seq:
              - id: z
                type: strz
              - id: s
                type: str
                size: 2
              - id: long
                type: str
                size-eos: true
            """, data);

        Assert.Equal((0L, 3L, "AB", ValueKind.Ascii), (Node(root, "z").Offset, Node(root, "z").Length, Node(root, "z").Description, Node(root, "z").ValueKind));
        Assert.Equal("CD", Node(root, "s").Description);
        Assert.Equal(new string('a', 64) + " ...", Node(root, "long").Description);
    }

    [Fact]
    public void Parse_StringWithPaddingAndTerminatorInsideTheSize()
    {
        var root = Parse("""
            meta:
              id: x
              encoding: ASCII
            seq:
              - id: name
                type: str
                size: 6
                terminator: 0
                pad-right: 0x20
              - id: next
                type: u1
            """, [(byte)'h', (byte)'i', 0, (byte)'x', 0x20, 0x20, 9]);

        Assert.Equal((0L, 6L, "hi"), (Node(root, "name").Offset, Node(root, "name").Length, Node(root, "name").Description));
        Assert.Equal("9", Node(root, "next").Description);
    }

    [Fact]
    public void Parse_BytesShowSixteenBytesInHex()
    {
        var data = Enumerable.Range(0, 20).Select(i => (byte)i).ToArray();

        var root = Parse("meta:\n  id: x\nseq:\n  - id: short\n    size: 2\n  - id: rest\n    size-eos: true\n", data);

        Assert.Equal(("00 01", ValueKind.Bytes), (Node(root, "short").Description, Node(root, "short").ValueKind));
        Assert.Equal("02 03 04 05 06 07 08 09 0A 0B 0C 0D 0E 0F 10 11 ...", Node(root, "rest").Description);
    }

    [Fact]
    public void Parse_HugeBytesAreNotLoaded()
    {
        var buffer = new SparseByteBuffer(3L << 30, (0, [0xAB]));

        var root = Parse("meta:\n  id: x\nseq:\n  - id: blob\n    size-eos: true\n", buffer);

        var blob = Node(root, "blob");
        Assert.Equal(3L << 30, blob.Length);
        Assert.StartsWith("AB 00 00", blob.Description);
        Assert.Null(blob.Error);
    }

    [Fact]
    public void Parse_ZeroLengthFieldIsShown()
    {
        var root = Parse("meta:\n  id: x\nseq:\n  - id: empty\n    size: 0\n  - id: b\n    type: u1\n", [5]);

        Assert.Equal((0L, 0L, ""), (Node(root, "empty").Offset, Node(root, "empty").Length, Node(root, "empty").Description));
    }

    [Fact]
    public void Parse_SizeExpressionUsesEarlierFields()
    {
        var root = Parse("meta:\n  id: x\nseq:\n  - id: len\n    type: u1\n  - id: body\n    size: len * 2\n", [2, 1, 2, 3, 4, 5]);

        Assert.Equal((1L, 4L), (Node(root, "body").Offset, Node(root, "body").Length));
    }

    [Fact]
    public void Parse_ContentsMismatchIsMarkedAndParsingContinues()
    {
        var root = Parse("""
            meta:
              id: x
            seq:
              - id: ok
                contents: [0x52, 0x49]
              - id: bad
                contents: "FF"
              - id: after
                type: u1
            """, [0x52, 0x49, 0x46, 0x47, 0x09]);

        Assert.Null(Node(root, "ok").Error);
        Assert.Equal("Contents mismatch: expected 46 46, got 46 47", Node(root, "bad").Error?.Message);
        Assert.Equal("9", Node(root, "after").Description);
    }

    [Fact]
    public void Parse_ContentsAreComparedAfterTheProcess()
    {
        var root = Parse("""
            meta:
              id: x
            seq:
              - id: magic
                contents: [0x01, 0x02]
                process: xor(0xFF)
            """, [0xFE, 0xFD]);

        var magic = Node(root, "magic");
        Assert.Null(magic.Error);
        Assert.Equal(("01 02", 0L, 2L), (magic.Description, magic.Offset, magic.Length));
    }

    [Fact]
    public void Parse_EnumShowsTheValueAndItsName()
    {
        var root = Parse("""
            meta:
              id: x
            seq:
              - id: known
                type: u1
                enum: kind
              - id: unknown
                type: u1
                enum: kind
            enums:
              kind:
                3: sos
            """, [3, 7]);

        Assert.Equal(("3 (sos)", ValueKind.Numeric), (Node(root, "known").Description, Node(root, "known").ValueKind));
        Assert.Equal("7", Node(root, "unknown").Description);
    }

    [Fact]
    public void Parse_FalseConditionSkipsTheFieldWithoutANode()
    {
        var root = Parse("""
            meta:
              id: x
            seq:
              - id: flag
                type: u1
              - id: opt
                type: u1
                if: flag == 1
              - id: last
                type: u1
            """, [0, 9]);

        Assert.Equal("flag,last", Names(root));
        Assert.Equal((1L, "9"), (Node(root, "last").Offset, Node(root, "last").Description));
    }

    [Fact]
    public void Parse_ProcessedBytesKeepTheirFileRange()
    {
        var root = Parse("meta:\n  id: x\nseq:\n  - id: data\n    size: 2\n    process: xor(0xFF)\n", [0x00, 0x0F]);

        var data = Node(root, "data");
        Assert.Equal((0L, 2L, "FF F0", true), (data.Offset, data.Length, data.Description, data.HasRange));
    }

    [Fact]
    public void Parse_EndOfStreamStopsWithAPartialTree()
    {
        var root = Parse("meta:\n  id: x\nseq:\n  - id: a\n    type: u1\n  - id: b\n    type: u4be\n  - id: c\n    type: u1\n", [1, 2, 3]);

        Assert.Equal("a,b,未解析", Names(root));
        var b = Node(root, "b");
        Assert.True(b.IsIncomplete);
        Assert.Equal((1L, 0L, ""), (b.Offset, b.Length, b.Description));
        Assert.Contains("needed 4 byte(s) at position 1, but only 2 remain", b.Error?.Message);
        var unparsed = Node(root, "未解析");
        Assert.Equal((1L, 2L), (unparsed.Offset, unparsed.Length));
        Assert.Equal(b.Error, unparsed.Error);
    }

    [Fact]
    public void Parse_RuntimeExpressionErrorNamesTheAttribute()
    {
        var root = Parse("meta:\n  id: x\nseq:\n  - id: n\n    type: u1\n  - id: body\n    size: n - 5\n", [1]);

        Assert.Equal("/seq/1/size: -4 cannot be used as a position or size", Node(root, "body").Error?.Message);
    }

    [Fact]
    public void Parse_ReferenceToAFieldNotYetReadIsAnError()
    {
        var root = Parse("meta:\n  id: x\nseq:\n  - id: a\n    size: b\n  - id: b\n    type: u1\n", [1]);

        Assert.Equal("/seq/0/size: 'b' has not been read yet", Node(root, "a").Error?.Message);
    }

    [Fact]
    public void Parse_NodeBudgetStopsWithoutMarkingAnError()
    {
        var root = Parse("meta:\n  id: x\nseq:\n  - id: a\n    type: u1\n  - id: b\n    type: u1\n  - id: c\n    type: u1\n",
            [1, 2, 3], new KsyParseOptions { NodeBudget = 2 });

        Assert.Equal("a,b,未解析", Names(root));
        Assert.All(root.Children.Take(2), n => Assert.Null(n.Error));
        var unparsed = Node(root, "未解析");
        Assert.Equal((2L, 1L, "NodeBudget(2)"), (unparsed.Offset, unparsed.Length, Summary(unparsed.Error)));
    }
}
