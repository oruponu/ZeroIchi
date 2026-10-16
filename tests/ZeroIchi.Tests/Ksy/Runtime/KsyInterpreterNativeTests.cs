using ZeroIchi.Models.FileStructure;
using ZeroIchi.Models.FileStructure.Ksy.Model;
using ZeroIchi.Models.FileStructure.Ksy.Runtime;
using static ZeroIchi.Tests.Ksy.Runtime.KsyTestRunner;

namespace ZeroIchi.Tests.Ksy.Runtime;

public sealed class KsyInterpreterNativeTests
{
    private sealed class ScanToMarker : IKsyNativeType
    {
        public string Name => "scan_to_marker";

        public KsyNativeResult Read(KsStream stream)
        {
            var start = stream.Position;
            while (stream.ReadInteger(1, false, true) != 0xFF) { }
            var length = stream.Position - start - 1;
            return new KsyNativeResult(length + 1,
            [
                new KsyNativeNode("data", start, length, $"{length} bytes", ValueKind.Bytes),
                new KsyNativeNode("marker", start + length, 1, "FF", ValueKind.Bytes),
            ]);
        }
    }

    private sealed class Faulty(long consumed, Exception? error = null) : IKsyNativeType
    {
        public string Name => "faulty";

        public KsyNativeResult Read(KsStream stream) => error is null ? new KsyNativeResult(consumed, []) : throw error;
    }

    private sealed class Events(long childLength = 2) : IKsyNativeType
    {
        public string Name => "events";

        public KsyNativeResult Read(KsStream stream)
        {
            var start = stream.Position;
            const long consumed = 3;
            return new KsyNativeResult(consumed,
            [
                new KsyNativeNode("event[0]", start, consumed, "", ValueKind.None)
                {
                    Children =
                    [
                        new KsyNativeNode("v_time", start, 1, "0", ValueKind.Numeric),
                        new KsyNativeNode("status", start + 1, childLength, "90 3C", ValueKind.Bytes),
                    ],
                },
            ]);
        }
    }

    private static KsyNativeTypeRegistry Registry(params IKsyNativeType[] types)
    {
        var registry = new KsyNativeTypeRegistry();
        foreach (var type in types) registry.Register(type);
        return registry;
    }

    private const string Yaml = """
        meta:
          id: x
          ks-opaque-types: true
        seq:
          - id: head
            type: u1
          - id: blob
            type: scan_to_marker
          - id: after
            type: u1
        """;

    [Fact]
    public void Parse_NativeTypeAddsItsNodesAndConsumesItsBytes()
    {
        var root = Parse(Yaml, [7, 1, 2, 0xFF, 9], natives: Registry(new ScanToMarker()));

        var blob = Node(root, "blob");
        Assert.Equal((1L, 3L, "", ValueKind.None, "data,marker"), (blob.Offset, blob.Length, blob.Description, blob.ValueKind, Names(blob)));
        var data = Node(root, "blob/data");
        Assert.Equal((1L, 2L, "2 bytes", ValueKind.Bytes), (data.Offset, data.Length, data.Description, data.ValueKind));
        Assert.Equal((3L, 1L), (Node(root, "blob/marker").Offset, Node(root, "blob/marker").Length));
        Assert.Equal((4L, "9"), (Node(root, "after").Offset, Node(root, "after").Description));
    }

    [Fact]
    public void Parse_NativeNodesCanHaveChildren()
    {
        var root = Parse(Yaml.Replace("scan_to_marker", "events"), [7, 0, 0x90, 0x3C, 9], natives: Registry(new Events()));

        Assert.Equal("event[0]", Names(Node(root, "blob")));
        var item = Node(root, "blob/event[0]");
        Assert.Equal((1L, 3L, "v_time,status"), (item.Offset, item.Length, Names(item)));
        var status = Node(root, "blob/event[0]/status");
        Assert.Equal((2L, 2L, "90 3C", ValueKind.Bytes), (status.Offset, status.Length, status.Description, status.ValueKind));
        Assert.Equal((4L, "9"), (Node(root, "after").Offset, Node(root, "after").Description));
    }

    [Fact]
    public void Parse_NodeBudgetInsideNativeNodesKeepsTheConsumedRange()
    {
        var root = Parse(Yaml.Replace("scan_to_marker", "events"), [7, 0, 0x90, 0x3C, 9],
            new KsyParseOptions { NodeBudget = 4 }, Registry(new Events()));

        var blob = Node(root, "blob");
        Assert.Equal((1L, 3L, true), (blob.Offset, blob.Length, blob.IsIncomplete));
        Assert.Equal("v_time", Names(Node(root, "blob/event[0]")));
        Assert.Equal("head,blob,未解析", Names(root));
        Assert.Equal((4L, 1L), (Node(root, "未解析").Offset, Node(root, "未解析").Length));
    }

    [Fact]
    public void Parse_NestedNativeNodeOutsideTheStreamIsAnError()
    {
        var root = Parse(Yaml.Replace("scan_to_marker", "events"), [7, 0, 0x90, 0x3C, 9],
            natives: Registry(new Events(childLength: 100)));

        Assert.Equal("Native(events, returned a node outside the stream)", Summary(Node(root, "blob").Error));
    }

    [Fact]
    public void Parse_SizedNativeTypeReadsInsideItsWindow()
    {
        var root = Parse(Yaml.Replace("type: scan_to_marker", "type: scan_to_marker\n    size: 4"),
            [7, 1, 0xFF, 0, 0, 9], natives: Registry(new ScanToMarker()));

        Assert.Equal((1L, 4L), (Node(root, "blob").Offset, Node(root, "blob").Length));
        Assert.Equal((2L, 1L), (Node(root, "blob/marker").Offset, Node(root, "blob/marker").Length));
        Assert.Equal((5L, "9"), (Node(root, "after").Offset, Node(root, "after").Description));
    }

    [Fact]
    public void Parse_NativeTypeFailureStopsWithItsMessage()
    {
        var root = Parse(Yaml.Replace("scan_to_marker", "faulty"), [7, 1, 2],
            natives: Registry(new Faulty(0, new InvalidOperationException("broken"))));

        Assert.Equal("/seq/1: Native type 'faulty' failed: broken", Node(root, "blob").Error?.Message);
        Assert.Equal("Native(faulty, broken)", Summary(Node(root, "blob").Error));
        Assert.Equal("head,blob,未解析", Names(root));
    }

    [Fact]
    public void Parse_NativeTypeCannotConsumeMoreThanTheStream()
    {
        var root = Parse(Yaml.Replace("scan_to_marker", "faulty"), [7, 1, 2], natives: Registry(new Faulty(5)));

        Assert.Equal("/seq/1: Native type 'faulty' consumed 5 byte(s) but 2 remain", Node(root, "blob").Error?.Message);
        Assert.Equal("Native(faulty, consumed 5 byte(s) but 2 remain)", Summary(Node(root, "blob").Error));
    }

    [Fact]
    public void Parse_NativeTypeEndOfStreamIsAParseError()
    {
        var root = Parse(Yaml, [7, 1, 2], natives: Registry(new ScanToMarker()));

        Assert.Contains("Unexpected end of stream", Node(root, "blob").Error?.Message);
        Assert.Equal((3L, 0L), (Node(root, "未解析").Offset, Node(root, "未解析").Length));
    }
}
