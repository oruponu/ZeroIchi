using System.Collections.Concurrent;
using System.Text;
using ZeroIchi.Models.Buffers;
using ZeroIchi.Models.FileStructure;
using ZeroIchi.Models.FileStructure.Ksy;
using ZeroIchi.Models.FileStructure.Ksy.Model;
using ZeroIchi.Models.FileStructure.Ksy.Runtime;
using static ZeroIchi.Tests.Ksy.Runtime.KsyTestRunner;

namespace ZeroIchi.Tests.Formats;

internal static class BuiltinFormat
{
    private static readonly ConcurrentDictionary<string, KsyFormat> Formats = new();

    static BuiltinFormat() => Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);

    public static FileStructureNode Parse(string root, byte[] data) =>
        KsyInterpreter.Parse(Formats.GetOrAdd(root, Load), new ArrayByteBuffer(data));

    public static string? Detect(byte[] data, string? fileName = null) =>
        KsyFormatRegistry.Default.Detect(new ArrayByteBuffer(data), fileName)?.Id;

    public static string Errors(FileStructureNode root)
    {
        var errors = new List<string>();
        Collect(root, "", errors);
        return string.Join("; ", errors);
    }

    private static void Collect(FileStructureNode node, string path, List<string> errors)
    {
        foreach (var child in node.Children)
        {
            var childPath = path.Length == 0 ? child.Name : $"{path}/{child.Name}";
            if (child.Error is { } error) errors.Add($"{childPath}: {Summary(error)} {error.Message}");
            Collect(child, childPath, errors);
        }
    }

    private static KsyFormat Load(string root)
    {
        var result = KsyModelBuilder.Load(root, KsyBuiltinFormats.CreateSource(), KsyBuiltinFormats.CreateNativeTypes());
        Assert.Empty(result.Diagnostics);
        return result.Format!;
    }
}
