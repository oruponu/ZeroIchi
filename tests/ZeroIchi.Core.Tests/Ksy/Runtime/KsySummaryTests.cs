using System.Diagnostics;
using ZeroIchi.Models.FileStructure;
using static ZeroIchi.Tests.Ksy.Runtime.KsyTestRunner;

namespace ZeroIchi.Tests.Ksy.Runtime;

public sealed class KsySummaryTests
{
    [Fact]
    public void Parse_RepresentationFormatsFieldsByKind()
    {
        var root = Parse("""
            meta:
              id: x
              encoding: ASCII
            seq:
              - id: chunk
                type: chunk
            types:
              chunk:
                -webide-representation: "{name} len={len:dec} raw={len} kind={kind} magic={magic:str} bytes={magic} neg={neg}"
                seq:
                  - id: name
                    type: str
                    size: 2
                  - id: len
                    type: u1
                  - id: kind
                    type: u1
                    enum: kinds
                  - id: magic
                    size: 2
                instances:
                  neg:
                    value: -len
            enums:
              kinds:
                1: data
            """, [(byte)'h', (byte)'i', 31, 1, (byte)'P', 0x01]);

        var chunk = Node(root, "chunk");
        Assert.Equal(("hi len=31 raw=0x1F kind=data magic=\"P.\" bytes=50 01 neg=-0x1F", ValueKind.None), (chunk.Description, chunk.ValueKind));
    }

    [Fact]
    public void Parse_RepresentationRecursesIntoObjectsAndArrays()
    {
        var root = Parse("""
            meta:
              id: x
            seq:
              - id: list
                type: list
            types:
              list:
                -webide-representation: "[{items:sep=|}] first={items.0} parent={_parent.list.count:dec}"
                seq:
                  - id: count
                    type: u1
                  - id: items
                    type: item
                    repeat: expr
                    repeat-expr: count
              item:
                -webide-representation: "<{v:dec}>"
                seq:
                  - id: v
                    type: u1
            """, [2, 5, 6]);

        Assert.Equal("[<5>|<6>] first= parent=2", Node(root, "list").Description);
        Assert.Equal("<6>", Node(root, "list/items/items[1]").Description);
    }

    [Fact]
    public void Parse_LongArraysAreCutAfterSixteenItems()
    {
        var data = new byte[] { 20 }.Concat(Enumerable.Range(0, 20).Select(i => (byte)i)).ToArray();

        var root = Parse("""
            meta:
              id: x
            seq:
              - id: holder
                type: holder
            types:
              holder:
                -webide-representation: "{items:dec}"
                seq:
                  - id: count
                    type: u1
                  - id: items
                    type: u1
                    repeat: expr
                    repeat-expr: count
            """, data);

        Assert.Equal("0, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15, …", Node(root, "holder").Description);
    }

    [Fact]
    public void Parse_MissingValuesAndErrorsBecomeEmpty()
    {
        var root = Parse("""
            meta:
              id: x
            seq:
              - id: holder
                type: holder
            types:
              holder:
                -webide-representation: "a={missing} b={opt} c={broken} d={v:dec}"
                seq:
                  - id: v
                    type: u1
                  - id: opt
                    type: u1
                    if: false
                instances:
                  broken:
                    value: opt + 1
            """, [4]);

        Assert.Equal("a= b= c= d=4", Node(root, "holder").Description);
    }

    [Fact]
    public void Parse_CyclesAndDeepNestingAreOmitted()
    {
        var root = Parse("""
            meta:
              id: x
            seq:
              - id: a
                type: level
            types:
              level:
                -webide-representation: "({next}{_parent})"
                seq:
                  - id: v
                    type: u1
                  - id: next
                    type: level
                    if: v != 0
            """, [1, 1, 1, 1, 1, 1, 0]);

        Assert.Equal("(((((……)…)…)…))", Node(root, "a").Description);
    }

    [Fact]
    public void Parse_LongSummariesAreCut()
    {
        var text = new string('a', 64);
        var data = System.Text.Encoding.ASCII.GetBytes(string.Concat(Enumerable.Repeat(text, 5)));

        var root = Parse("""
            meta:
              id: x
              encoding: ASCII
            seq:
              - id: holder
                type: holder
            types:
              holder:
                -webide-representation: "{parts:sep=}"
                seq:
                  - id: parts
                    type: str
                    size: 64
                    repeat: expr
                    repeat-expr: 5
            """, data);

        Assert.Equal(new string('a', 256) + " ...", Node(root, "holder").Description);
    }

    [Fact]
    public void Parse_SelfReferencingRepresentationFinishesQuickly()
    {
        var stopwatch = Stopwatch.StartNew();

        var root = Parse("""
            meta:
              id: x
            seq:
              - id: items
                type: item
                repeat: eos
            types:
              item:
                -webide-representation: '{_parent.items}'
                seq:
                  - id: v
                    type: u1
            """, new byte[100]);

        Assert.True(stopwatch.Elapsed < TimeSpan.FromSeconds(5), $"Took {stopwatch.Elapsed}");
        Assert.StartsWith("…, …", Node(root, "items/items[0]").Description);
    }

    [Fact]
    public void Parse_ToStringIsUsedWithoutARepresentation()
    {
        var root = Parse("""
            meta:
              id: x
            seq:
              - id: point
                type: point
            types:
              point:
                to-string: x.to_s + "," + y.to_s
                seq:
                  - id: x
                    type: u1
                  - id: y
                    type: u1
            """, [3, 4]);

        Assert.Equal("3,4", Node(root, "point").Description);
    }

    [Fact]
    public void Parse_UnevaluatedInstancesAreNotReadForTheSummary()
    {
        var root = Parse("""
            meta:
              id: x
            seq:
              - id: holder
                type: holder
              - id: tail
                type: u4be
            types:
              holder:
                -webide-representation: "v={v:dec} later={later:dec}"
                seq:
                  - id: v
                    type: u1
                instances:
                  later:
                    pos: 0
                    type: u1
            """, [9]);

        Assert.Equal("v=9 later=", Node(root, "holder").Description);
        Assert.Equal("holder,tail,未解析", Names(root));
        Assert.Equal("v", Names(Node(root, "holder")));
    }
}
