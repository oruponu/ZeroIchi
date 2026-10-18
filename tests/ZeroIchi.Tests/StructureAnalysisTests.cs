using ZeroIchi.Models.Buffers;
using ZeroIchi.Models.FileStructure;
using ZeroIchi.Models.FileStructure.Ksy;
using ZeroIchi.Tests.Ksy.Model;

namespace ZeroIchi.Tests;

public sealed class StructureAnalysisTests
{
    private const string Yaml = """
        meta:
          id: zi
        seq:
          - id: magic
            contents: ZI
          - id: len
            type: u1
        """;

    private static KsyFormatRegistry Registry()
    {
        var registry = new KsyFormatRegistry();
        registry.Register(KsyTestLoader.Load(Yaml));
        return registry;
    }

    [Fact]
    public void Analyze_MatchingFormat_ReturnsTheTreeAndTheColors()
    {
        var analysis = StructureAnalysis.Analyze(Registry(), new ArrayByteBuffer("ZI\u0005"u8.ToArray()), "a.bin", CancellationToken.None);

        Assert.NotNull(analysis);
        Assert.Equal("magic,len", string.Join(",", analysis.Root.Children.Select(c => c.Name)));
        Assert.NotNull(analysis.Colors);
    }

    [Fact]
    public void Analyze_NoMatchingFormat_ReturnsNull()
    {
        Assert.Null(StructureAnalysis.Analyze(Registry(), new ArrayByteBuffer([1, 2, 3]), "a.bin", CancellationToken.None));
    }

    [Fact]
    public void Analyze_Cancelled_Throws()
    {
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        Assert.ThrowsAny<OperationCanceledException>(() =>
            StructureAnalysis.Analyze(Registry(), new ArrayByteBuffer("ZI\u0005"u8.ToArray()), "a.bin", cts.Token));
    }
}
