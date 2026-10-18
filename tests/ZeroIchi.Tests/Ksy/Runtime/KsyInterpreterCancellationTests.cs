using ZeroIchi.Models.Buffers;
using ZeroIchi.Models.FileStructure.Ksy.Expressions;
using ZeroIchi.Models.FileStructure.Ksy.Model;
using ZeroIchi.Models.FileStructure.Ksy.Runtime;
using ZeroIchi.Tests.Ksy.Model;

namespace ZeroIchi.Tests.Ksy.Runtime;

public sealed class KsyInterpreterCancellationTests
{
    private sealed class CancelOnReadBuffer(byte[] data, long cancelAt, CancellationTokenSource cts) : ByteBuffer
    {
        public override long Length => data.Length;

        public override byte ReadByte(long index)
        {
            if (index >= cancelAt) cts.Cancel();
            return data[index];
        }

        public override void ReadBytes(long offset, byte[] buffer, int bufferOffset, int count)
        {
            if (offset + count > cancelAt) cts.Cancel();
            Array.Copy(data, offset, buffer, bufferOffset, count);
        }
    }

    private sealed class CancelThenRead(CancellationTokenSource cts) : IKsyNativeType
    {
        public string Name => "cancel_then_read";

        public KsyNativeResult Read(KsStream stream, IReadOnlyList<KsValue> arguments)
        {
            cts.Cancel();
            stream.ReadInteger(1, false, true);
            return new KsyNativeResult(1, []);
        }
    }

    private static KsyFormat Load(string yaml, CancellationTokenSource? cts = null)
    {
        var natives = new KsyNativeTypeRegistry();
        if (cts is not null) natives.Register(new CancelThenRead(cts));
        return KsyTestLoader.Load(yaml, natives: natives);
    }

    private const string Bytes = """
        meta:
          id: x
        seq:
          - id: items
            type: u1
            repeat: eos
        """;

    [Fact]
    public void Parse_AlreadyCancelled_Throws()
    {
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        Assert.ThrowsAny<OperationCanceledException>(() =>
            KsyInterpreter.Parse(Load(Bytes), new ArrayByteBuffer([1, 2, 3]), cancellationToken: cts.Token));
    }

    [Fact]
    public void Parse_CancelledWhileReadingTheSeq_Throws()
    {
        using var cts = new CancellationTokenSource();
        var buffer = new CancelOnReadBuffer(new byte[64], cancelAt: 10, cts);

        Assert.ThrowsAny<OperationCanceledException>(() =>
            KsyInterpreter.Parse(Load(Bytes), buffer, cancellationToken: cts.Token));
    }

    [Fact]
    public void Parse_CancelledInsideANativeType_IsNotReportedAsANativeError()
    {
        using var cts = new CancellationTokenSource();
        const string yaml = """
            meta:
              id: x
              ks-opaque-types: true
            seq:
              - id: blob
                type: cancel_then_read
            """;

        Assert.ThrowsAny<OperationCanceledException>(() =>
            KsyInterpreter.Parse(Load(yaml, cts), new ArrayByteBuffer([1, 2]), cancellationToken: cts.Token));
    }

    [Fact]
    public void Parse_CancelledInsideProcessedData_Throws()
    {
        using var cts = new CancellationTokenSource();
        const string yaml = """
            meta:
              id: x
              ks-opaque-types: true
            seq:
              - id: body
                size: 2
                process: xor(1)
                type: inner
            types:
              inner:
                seq:
                  - id: blob
                    type: cancel_then_read
            """;

        Assert.ThrowsAny<OperationCanceledException>(() =>
            KsyInterpreter.Parse(Load(yaml, cts), new ArrayByteBuffer([1, 2]), cancellationToken: cts.Token));
    }

    [Fact]
    public void Parse_CancelledWhileEvaluatingInstances_Throws()
    {
        using var cts = new CancellationTokenSource();
        const string yaml = """
            meta:
              id: x
            seq:
              - id: head
                type: u1
            instances:
              late:
                pos: 40
                type: u1
            """;
        var buffer = new CancelOnReadBuffer(new byte[64], cancelAt: 40, cts);

        Assert.ThrowsAny<OperationCanceledException>(() =>
            KsyInterpreter.Parse(Load(yaml), buffer, cancellationToken: cts.Token));
    }
}
