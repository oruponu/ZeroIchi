using ZeroIchi.Models.Buffers;
using ZeroIchi.Models.FileStructure;
using ZeroIchi.Models.FileStructure.Ksy.Model;
using ZeroIchi.Models.FileStructure.Ksy.Runtime;
using ZeroIchi.Tests.Ksy.Model;

namespace ZeroIchi.Tests.Ksy.Runtime;

internal static class KsyTestRunner
{
    public static FileStructureNode Parse(string yaml, byte[] data, KsyParseOptions? options = null,
        KsyNativeTypeRegistry? natives = null) =>
        Parse(yaml, new ArrayByteBuffer(data), options, natives);

    public static FileStructureNode Parse(string yaml, ByteBuffer buffer, KsyParseOptions? options = null,
        KsyNativeTypeRegistry? natives = null) =>
        KsyInterpreter.Parse(KsyTestLoader.Load(yaml, natives: natives), buffer, options);

    public static FileStructureNode Node(FileStructureNode root, string path)
    {
        var node = root;
        foreach (var name in path.Split('/'))
        {
            var next = node.Children.FirstOrDefault(c => c.Name == name);
            Assert.True(next is not null, $"'{name}' not found under '{node.Name}' ({string.Join(", ", node.Children.Select(c => c.Name))})");
            node = next;
        }
        return node;
    }

    public static string Names(FileStructureNode node) => string.Join(",", node.Children.Select(c => c.Name));
}
