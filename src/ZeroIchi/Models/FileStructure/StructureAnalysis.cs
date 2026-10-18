using System.Threading;
using ZeroIchi.Models.Buffers;
using ZeroIchi.Models.FileStructure.Ksy;
using ZeroIchi.Models.FileStructure.Ksy.Runtime;

namespace ZeroIchi.Models.FileStructure;

public sealed record StructureAnalysis(FileStructureNode Root, StructureColorMap Colors)
{
    public static StructureAnalysis? Analyze(
        KsyFormatRegistry formats, ByteBuffer buffer, string? filePath, CancellationToken cancellationToken)
    {
        if (formats.Detect(buffer, filePath) is not { } format) return null;

        var root = KsyInterpreter.Parse(format, buffer, cancellationToken: cancellationToken);
        cancellationToken.ThrowIfCancellationRequested();
        var colors = StructureColorMap.Build(root);
        cancellationToken.ThrowIfCancellationRequested();
        return new StructureAnalysis(root, colors);
    }
}
