using System.Text;
using ZeroIchi.Models;

namespace ZeroIchi.Tests;

public sealed class DataInspectorTests
{
    static DataInspectorTests() => Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);

    private const long HugeLength = 3L * 1024 * 1024 * 1024;

    [Fact]
    public void Inspect_AtStartOfBufferLargerThanIntRange_ReturnsEntries()
    {
        var buffer = new SparseByteBuffer(HugeLength, (0, [1, 2, 3, 4, 5, 6, 7, 8]));

        var entries = DataInspector.Inspect(buffer, 0, bigEndian: false);

        Assert.Equal("1", entries.Single(e => e.Label == "UInt8").Value);
        Assert.Equal("513", entries.Single(e => e.Label == "UInt16").Value);
    }

    [Fact]
    public void Inspect_AtOffsetBeyondIntRange_ReadsPlantedBytes()
    {
        const long offset = 0x8000_0000L;
        var buffer = new SparseByteBuffer(HugeLength, (offset, [0x2A, 0, 0, 0, 0, 0, 0, 0]));

        var entries = DataInspector.Inspect(buffer, offset, bigEndian: false);

        Assert.Equal("42", entries.Single(e => e.Label == "UInt8").Value);
    }

    [Fact]
    public void Inspect_NearEndOfBuffer_OmitsEntriesWiderThanRemainingBytes()
    {
        var buffer = new SparseByteBuffer(HugeLength, (HugeLength - 2, [0xAA, 0xBB]));

        var entries = DataInspector.Inspect(buffer, HugeLength - 2, bigEndian: false);

        Assert.Contains(entries, e => e.Label == "UInt16");
        Assert.DoesNotContain(entries, e => e.Label == "UInt32");
    }
}
