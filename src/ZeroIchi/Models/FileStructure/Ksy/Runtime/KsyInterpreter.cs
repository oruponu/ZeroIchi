using System.Threading;
using ZeroIchi.Models.Buffers;
using ZeroIchi.Models.FileStructure.Ksy.Model;

namespace ZeroIchi.Models.FileStructure.Ksy.Runtime;

public static class KsyInterpreter
{
    public static FileStructureNode Parse(KsyFormat format, ByteBuffer buffer, KsyParseOptions? options = null,
        CancellationToken cancellationToken = default) =>
        new KsyRun(format, buffer, options ?? KsyParseOptions.Default, cancellationToken).Run();
}
