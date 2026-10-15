using System.IO.Compression;
using ZeroIchi.Models.FileStructure;
using ZeroIchi.Models.FileStructure.Ksy.Runtime;
using static ZeroIchi.Tests.Ksy.Runtime.KsyTestRunner;

namespace ZeroIchi.Tests.Ksy.Runtime;

public sealed class KsyInterpreterUserTypeTests
{
    [Fact]
    public void Parse_NestedTypeBecomesANodeWithItsFields()
    {
        var root = Parse("""
            meta:
              id: x
              endian: le
            seq:
              - id: header
                type: header
              - id: tail
                type: u1
            types:
              header:
                seq:
                  - id: a
                    type: u2
                  - id: b
                    type: u1
            """, [0x01, 0x00, 0x02, 0x03]);

        var header = Node(root, "header");
        Assert.Equal((0L, 3L, "", ValueKind.None), (header.Offset, header.Length, header.Description, header.ValueKind));
        Assert.Equal("a,b", Names(header));
        Assert.Equal((0L, "1"), (Node(root, "header/a").Offset, Node(root, "header/a").Description));
        Assert.Equal((2L, "2"), (Node(root, "header/b").Offset, Node(root, "header/b").Description));
        Assert.Equal((3L, "3"), (Node(root, "tail").Offset, Node(root, "tail").Description));
    }

    [Fact]
    public void Parse_SizedTypeReadsInsideItsWindowAndSkipsTheRest()
    {
        var root = Parse("""
            meta:
              id: x
            seq:
              - id: chunk
                type: chunk
                size: 3
              - id: after
                type: u1
            types:
              chunk:
                seq:
                  - id: first
                    type: u1
                  - id: rest_size
                    type: u1
                    if: _io.size == 3
            """, [0x0A, 0x0B, 0x0C, 0x0D]);

        Assert.Equal((0L, 3L), (Node(root, "chunk").Offset, Node(root, "chunk").Length));
        Assert.Equal("first,rest_size", Names(Node(root, "chunk")));
        Assert.Equal((3L, "13"), (Node(root, "after").Offset, Node(root, "after").Description));
    }

    [Fact]
    public void Parse_FailureInsideASizedTypeKeepsTheWholeRange()
    {
        var root = Parse("""
            meta:
              id: x
            seq:
              - id: a
                type: u1
              - id: body
                type: inner
                size: 4
            types:
              inner:
                seq:
                  - id: big
                    type: u8le
            """, [1, 2, 3, 4, 5]);

        var body = Node(root, "body");
        Assert.True(body.IsIncomplete);
        Assert.Equal((1L, 4L), (body.Offset, body.Length));
        var big = Node(root, "body/big");
        Assert.Equal((1L, 0L, true), (big.Offset, big.Length, big.IsIncomplete));
        Assert.Contains("needed 8 byte(s) at position 0, but only 4 remain", big.Error);
        Assert.Null(body.Error);
        Assert.Equal((1L, 4L), (Node(root, "未解析").Offset, Node(root, "未解析").Length));
    }

    [Fact]
    public void Parse_ProcessedTypeIsReadFromTheConvertedData()
    {
        var plain = new byte[] { 0x01, 0x02, 0x03, 0x04 };
        var compressed = Compress(plain);
        var data = new[] { (byte)compressed.Length }.Concat(compressed).ToArray();

        var root = Parse("""
            meta:
              id: x
              endian: be
            seq:
              - id: len
                type: u1
              - id: body
                type: inner
                size: len
                process: zlib
            types:
              inner:
                seq:
                  - id: value
                    type: u4
            """, data);

        var body = Node(root, "body");
        Assert.Equal((1L, (long)compressed.Length, true), (body.Offset, body.Length, body.HasRange));
        var value = Node(root, "body/value");
        Assert.Equal(("16909060", false, 0L, 0L, ValueKind.None), (value.Description, value.HasRange, value.Offset, value.Length, value.ValueKind));
    }

    [Fact]
    public void Parse_ArgumentsAreEvaluatedInTheCallerAndConvertedToEnums()
    {
        var root = Parse("""
            meta:
              id: x
            seq:
              - id: len
                type: u1
              - id: body
                type: inner(len, 1)
            types:
              inner:
                params:
                  - id: n
                    type: u1
                  - id: k
                    type: u1
                    enum: kind
                seq:
                  - id: data
                    size: n
                  - id: check
                    type: u1
                    if: k == kind::one
            enums:
              kind:
                1: one
            """, [2, 0xAA, 0xBB, 0x07]);

        Assert.Equal("AA BB", Node(root, "body/data").Description);
        Assert.Equal("7", Node(root, "body/check").Description);
    }

    [Fact]
    public void Parse_ParentAndRootReferences()
    {
        var root = Parse("""
            meta:
              id: x
            seq:
              - id: magic
                type: u1
              - id: len
                type: u1
              - id: outer
                type: outer
            types:
              outer:
                seq:
                  - id: inner
                    type: inner
              inner:
                seq:
                  - id: data
                    size: _parent._parent.len
                  - id: flag
                    type: u1
                    if: _root.magic == 0x7F
            """, [0x7F, 1, 0xEE, 0x05]);

        Assert.Equal("EE", Node(root, "outer/inner/data").Description);
        Assert.Equal("5", Node(root, "outer/inner/flag").Description);
    }

    [Fact]
    public void Parse_ParentKeyReplacesTheParent()
    {
        var root = Parse("""
            meta:
              id: x
            seq:
              - id: len
                type: u1
              - id: outer
                type: outer
            types:
              outer:
                seq:
                  - id: inner
                    type: inner
                    parent: _parent
              inner:
                seq:
                  - id: data
                    size: _parent.len
            """, [2, 0x01, 0x02]);

        Assert.Equal("01 02", Node(root, "outer/inner/data").Description);
    }

    [Fact]
    public void Parse_EndianIsInheritedOrOverriddenByNestedTypes()
    {
        var root = Parse("""
            meta:
              id: x
              endian: le
            seq:
              - id: a
                type: inner_le
              - id: b
                type: inner_be
            types:
              inner_le:
                seq:
                  - id: v
                    type: u2
              inner_be:
                meta:
                  endian: be
                seq:
                  - id: v
                    type: u2
            """, [0x01, 0x00, 0x00, 0x01]);

        Assert.Equal("1", Node(root, "a/v").Description);
        Assert.Equal("1", Node(root, "b/v").Description);
    }

    [Fact]
    public void Parse_SwitchedEndianIsDecidedPerObjectAndPassedToNestedTypes()
    {
        var root = Parse("""
            meta:
              id: x
            seq:
              - id: little
                type: block(0)
              - id: big
                type: block(1)
            types:
              block:
                meta:
                  endian:
                    switch-on: order
                    cases:
                      0: le
                      1: be
                params:
                  - id: order
                    type: u1
                seq:
                  - id: body
                    type: body
                types:
                  body:
                    seq:
                      - id: v
                        type: u2
            """, [0x01, 0x00, 0x00, 0x01]);

        Assert.Equal("1", Node(root, "little/body/v").Description);
        Assert.Equal("1", Node(root, "big/body/v").Description);
    }

    [Fact]
    public void Parse_SwitchedEndianWithoutAMatchIsAnError()
    {
        var root = Parse("""
            meta:
              id: x
            seq:
              - id: tag
                type: u1
              - id: block
                type: block
            types:
              block:
                meta:
                  endian:
                    switch-on: _parent.tag
                    cases:
                      0: le
                      1: be
                seq:
                  - id: v
                    type: u2
            """, [2, 0, 0]);

        Assert.Equal("/types/block/meta/endian: No case matches 2", Node(root, "block").Error);
        Assert.Equal("tag,block,未解析", Names(root));
    }

    [Fact]
    public void Parse_SwitchedEndianFallsBackToTheDefaultCase()
    {
        var root = Parse("""
            meta:
              id: x
            seq:
              - id: tag
                type: u1
              - id: block
                type: block
            types:
              block:
                meta:
                  endian:
                    switch-on: _parent.tag
                    cases:
                      1: be
                      _: le
                seq:
                  - id: v
                    type: u2
            """, [2, 0x01, 0x00]);

        Assert.Equal("1", Node(root, "block/v").Description);
    }

    [Fact]
    public void Parse_SizeOfAFixedTypeIsAvailableAtRuntime()
    {
        var root = Parse("""
            meta:
              id: x
            seq:
              - id: pair
                type: pair
              - id: copy
                size: pair._sizeof
            types:
              pair:
                seq:
                  - id: a
                    type: u1
                  - id: b
                    type: u1
            """, [1, 2, 3, 4]);

        Assert.Equal((2L, 2L), (Node(root, "copy").Offset, Node(root, "copy").Length));
    }

    [Fact]
    public void Parse_NestingDepthIsLimited()
    {
        var root = Parse("""
            meta:
              id: x
            seq:
              - id: next
                type: node
            types:
              node:
                seq:
                  - id: next
                    type: node
            """, new byte[16], new KsyParseOptions { MaxDepth = 5 });

        var deepest = Node(root, "next/next/next/next/next");
        Assert.Equal("Type nesting exceeds the limit (5)", deepest.Error);
        Assert.Equal("Type nesting exceeds the limit (5)", Node(root, "未解析").Description);
    }

    [Fact]
    public void Parse_DeepNestingOnASmallStackStopsWithoutCrashing()
    {
        FileStructureNode? root = null;

        var error = SmallStack.Run(() => root = Parse("""
            meta:
              id: x
            seq:
              - id: next
                type: node
            types:
              node:
                seq:
                  - id: v
                    type: u1
                  - id: next
                    type: node
            """, new byte[100_000], new KsyParseOptions { MaxDepth = 100_000 }));

        Assert.Null(error);
        Assert.NotNull(root);
        Assert.Equal("Nesting is too deep for the available stack", Node(root, "未解析").Description);
    }

    [Fact]
    public void Parse_DataLoadedIntoMemoryIsLimitedForTheWholeRun()
    {
        var compressed = Compress(new byte[1000]);
        var chunk = new[] { (byte)compressed.Length }.Concat(compressed).ToArray();
        var data = Enumerable.Repeat(chunk, 5).SelectMany(c => c).ToArray();

        var root = Parse("""
            meta:
              id: x
            seq:
              - id: chunks
                type: chunk
                repeat: eos
            types:
              chunk:
                seq:
                  - id: len
                    type: u1
                  - id: body
                    size: len
                    process: zlib
            """, data, new KsyParseOptions { MaxTotalLoad = 3000 });

        Assert.Equal("Data loaded into memory exceeds the limit (3000 bytes)", Node(root, "未解析").Description);
        Assert.Equal(3, Node(root, "chunks").Children.Count);
    }

    private static byte[] Compress(byte[] data)
    {
        using var output = new MemoryStream();
        using (var zlib = new ZLibStream(output, CompressionLevel.Optimal))
            zlib.Write(data);
        return output.ToArray();
    }
}
