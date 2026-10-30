using ZeroIchi.Models.Buffers;
using ZeroIchi.Models.PieceTables;

namespace ZeroIchi.Tests.Buffers;

public sealed class ByteBufferReadRangeTests : IDisposable
{
    private static readonly byte[] Content = [1, 2, 3, 4, 5, 6, 7, 8];

    private readonly string _tempDir = Path.Combine(Path.GetTempPath(), "ZeroIchiTests_" + Guid.NewGuid().ToString("N"));

    public ByteBufferReadRangeTests() => Directory.CreateDirectory(_tempDir);

    public void Dispose() => Directory.Delete(_tempDir, recursive: true);

    public static TheoryData<string> Kinds => ["array", "mapped", "file", "piece-table", "snapshot"];

    private ByteBuffer Create(string kind)
    {
        string WriteFile()
        {
            var path = Path.Combine(_tempDir, Guid.NewGuid().ToString("N") + ".bin");
            File.WriteAllBytes(path, Content);
            return path;
        }

        return kind switch
        {
            "array" => new ArrayByteBuffer(Content),
            "mapped" => new MappedByteBuffer(WriteFile()),
            "file" => new FileByteBuffer(WriteFile()),
            "piece-table" => new PieceTableByteBuffer(new PieceTable(new ArrayByteBuffer(Content))),
            "snapshot" => new PieceTable(new ArrayByteBuffer(Content)).CreateSnapshot(),
            _ => throw new ArgumentOutOfRangeException(nameof(kind)),
        };
    }

    private static byte[] Destination() => [0xEE, 0xEE, 0xEE, 0xEE];

    [Theory]
    [MemberData(nameof(Kinds))]
    public void ReadBytes_FromTheEnd_ReadsNothing(string kind)
    {
        using var buffer = Create(kind);
        var destination = Destination();

        buffer.ReadBytes(Content.Length, destination, 0, 4);

        Assert.Equal(Destination(), destination);
    }

    [Theory]
    [MemberData(nameof(Kinds))]
    public void ReadBytes_PastTheEnd_ReadsNothing(string kind)
    {
        using var buffer = Create(kind);
        var destination = Destination();

        buffer.ReadBytes(Content.Length + 3, destination, 0, 4);

        Assert.Equal(Destination(), destination);
    }

    [Theory]
    [MemberData(nameof(Kinds))]
    public void ReadBytes_AcrossTheEnd_CopiesOnlyTheRest(string kind)
    {
        using var buffer = Create(kind);
        var destination = Destination();

        buffer.ReadBytes(6, destination, 0, 4);

        Assert.Equal([7, 8, 0xEE, 0xEE], destination);
    }

    [Theory]
    [MemberData(nameof(Kinds))]
    public void ReadBytes_FromANegativeOffset_Throws(string kind)
    {
        using var buffer = Create(kind);

        Assert.Throws<ArgumentOutOfRangeException>(() => buffer.ReadBytes(-1, Destination(), 0, 4));
    }
}
