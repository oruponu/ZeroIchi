using ZeroIchi.Models.FileStructure;
using ZeroIchi.Models.FileStructure.Ksy.Runtime;
using static ZeroIchi.Tests.Ksy.Runtime.KsyTestRunner;

namespace ZeroIchi.Tests.Ksy.Runtime;

public sealed class KsyInterpreterInstanceTests
{
    [Fact]
    public void Parse_ValueInstanceIsAComputedNodeWithoutRange()
    {
        var root = Parse("""
            meta:
              id: x
            seq:
              - id: a
                type: u1
            instances:
              doubled:
                value: a * 2
            """, [21]);

        Assert.Equal("a,doubled", Names(root));
        var doubled = Node(root, "doubled");
        Assert.Equal((false, 0L, 0L, "42", ValueKind.None), (doubled.HasRange, doubled.Offset, doubled.Length, doubled.Description, doubled.ValueKind));
    }

    [Fact]
    public void Parse_ParseInstanceReadsAtItsPositionAndRestoresTheStream()
    {
        var root = Parse("""
            meta:
              id: x
            seq:
              - id: len
                size: header_len
              - id: after
                type: u1
            instances:
              header_len:
                pos: 3
                type: u1
            """, [0xAA, 0xBB, 0xCC, 2]);

        Assert.Equal((0L, 2L), (Node(root, "len").Offset, Node(root, "len").Length));
        Assert.Equal((2L, "204"), (Node(root, "after").Offset, Node(root, "after").Description));
        var instance = Node(root, "header_len");
        Assert.Equal((3L, 1L, "2", true), (instance.Offset, instance.Length, instance.Description, instance.HasRange));
    }

    [Fact]
    public void Parse_InstancesAreShownInDeclarationOrderAfterTheSeq()
    {
        var root = Parse("""
            meta:
              id: x
            seq:
              - id: a
                type: u1
                if: second == 2
            instances:
              first:
                value: 1
              second:
                value: 2
            """, [5]);

        Assert.Equal("a,first,second", Names(root));
    }

    [Fact]
    public void Parse_ChildInstanceCanReadALaterFieldOfItsParent()
    {
        var root = Parse("""
            meta:
              id: x
            seq:
              - id: child
                type: child
              - id: later
                type: u1
            types:
              child:
                seq:
                  - id: v
                    type: u1
                instances:
                  sum:
                    value: v + _parent.later
            """, [1, 2]);

        var sum = Node(root, "child/sum");
        Assert.Equal(("3", null), (sum.Description, sum.Error));
    }

    [Fact]
    public void Parse_FailedParseInstanceRestoresThePosition()
    {
        var root = Parse("""
            meta:
              id: x
            seq:
              - id: a
                type: u1
            instances:
              bad:
                pos: 2
                size: 5
              next:
                type: u1
            """, [1, 2, 3]);

        Assert.True(Node(root, "bad").IsIncomplete);
        Assert.Contains("needed 5 byte(s) at position 2", Node(root, "bad").Error);
        Assert.Equal((1L, "2"), (Node(root, "next").Offset, Node(root, "next").Description));
        Assert.DoesNotContain(root.Children, c => c.Name == "未解析");
    }

    [Fact]
    public void Parse_ParseInstanceInTheMiddleOfBitsRestoresTheBitState()
    {
        var root = Parse("""
            meta:
              id: x
            seq:
              - id: hi
                type: b4
              - id: lo
                type: b4
                if: peek == 0xAB
            instances:
              peek:
                pos: 0
                type: u1
            """, [0xAB, 0xCD]);

        Assert.Equal(("10", "11"), (Node(root, "hi").Description, Node(root, "lo").Description));
    }

    [Fact]
    public void Parse_CyclicInstancesAreMarkedWithoutStoppingTheParse()
    {
        var root = Parse("""
            meta:
              id: x
            seq:
              - id: a
                type: u1
            instances:
              p:
                value: q
              q:
                value: p
              fine:
                value: a
            """, [7]);

        Assert.Contains("Cyclic reference to instance 'p'", Node(root, "q").Error);
        Assert.True(Node(root, "p").IsIncomplete);
        Assert.Equal("7", Node(root, "fine").Description);
        Assert.DoesNotContain(root.Children, c => c.Name == "未解析");
    }

    [Fact]
    public void Parse_NullFromASkippedFieldCanBePassedButNotUsed()
    {
        var root = Parse("""
            meta:
              id: x
            seq:
              - id: opt
                type: u1
                if: false
            instances:
              passed:
                value: opt
              used:
                value: opt + 1
              chosen:
                value: "true ? 5 : opt"
            """, [7]);

        Assert.Equal(("", null), (Node(root, "passed").Description, Node(root, "passed").Error));
        Assert.Contains("Value is null", Node(root, "used").Error);
        Assert.Equal("5", Node(root, "chosen").Description);
    }

    [Fact]
    public void Parse_InstanceReadFromTheFileInsideConvertedDataHasARange()
    {
        var root = Parse("""
            meta:
              id: x
            seq:
              - id: body
                type: inner
                size: 2
                process: xor(0xFF)
            types:
              inner:
                seq:
                  - id: v
                    type: u1
                instances:
                  raw:
                    io: _root._io
                    pos: 1
                    type: u1
            """, [0x00, 0x0F]);

        Assert.False(Node(root, "body/v").HasRange);
        var raw = Node(root, "body/raw");
        Assert.Equal((true, 1L, 1L, "15"), (raw.HasRange, raw.Offset, raw.Length, raw.Description));
    }

    [Fact]
    public void Parse_FalseInstanceConditionCreatesNoNode()
    {
        var root = Parse("meta:\n  id: x\ninstances:\n  maybe:\n    value: 1\n    if: false\n", []);

        Assert.Equal("", Names(root));
    }

    [Fact]
    public void Parse_RepeatedParseInstanceStartsAtItsPosition()
    {
        var root = Parse("meta:\n  id: x\ninstances:\n  items:\n    pos: 1\n    type: u1\n    repeat: expr\n    repeat-expr: 2\n", [9, 8, 7]);

        var items = Node(root, "items");
        Assert.Equal((1L, 2L, "items[0],items[1]"), (items.Offset, items.Length, Names(items)));
    }

    [Fact]
    public void Parse_FatalErrorSkipsTheEvaluationForDisplay()
    {
        var root = Parse("""
            meta:
              id: x
            seq:
              - id: a
                type: u4be
            instances:
              never:
                value: 1
            """, [1]);

        Assert.Equal("a,未解析", Names(root));
    }

    [Fact]
    public void Parse_RandomInputNeverThrows()
    {
        const string yaml = """
            meta:
              id: x
              endian: le
              encoding: UTF-8
            seq:
              - id: count
                type: u1
              - id: entries
                type: entry
                repeat: expr
                repeat-expr: count
              - id: tail
                size-eos: true
                type: blocks
            types:
              entry:
                seq:
                  - id: kind
                    type: u1
                  - id: len
                    type: u2
                  - id: body
                    size: len
                    type:
                      switch-on: kind
                      cases:
                        1: text
                        2: nested
                  - id: flags
                    type: b3
                instances:
                  peek:
                    pos: len
                    type: u4
                  ratio:
                    value: len / (kind - 3)
              text:
                seq:
                  - id: s
                    type: strz
                    repeat: until
                    repeat-until: _.length == 0 or _io.eof
              nested:
                seq:
                  - id: inner
                    type: entry
                    repeat: eos
              blocks:
                seq:
                  - id: items
                    type: u2
                    repeat: eos
                    valid:
                      max: 1000
            """;
        var random = new Random(1234);

        for (var i = 0; i < 300; i++)
        {
            var data = new byte[random.Next(0, 64)];
            random.NextBytes(data);

            var root = Parse(yaml, data, new KsyParseOptions { NodeBudget = 2_000 });

            Assert.NotNull(root);
        }
    }
}
