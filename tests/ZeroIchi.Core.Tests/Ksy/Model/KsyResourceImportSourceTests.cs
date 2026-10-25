using System.Reflection;
using ZeroIchi.Models.FileStructure.Ksy.Model;

namespace ZeroIchi.Tests.Ksy.Model;

public sealed class KsyResourceImportSourceTests
{
    [Fact]
    public void TryRead_ReadsEmbeddedKsy()
    {
        var source = new KsyResourceImportSource(Assembly.GetExecutingAssembly(), "ZeroIchi.Tests.Ksy.Model.Resources.");

        Assert.True(source.TryRead("common/sample", out var text));
        Assert.Contains("id: sample", text);
        Assert.False(source.TryRead("common/nope", out _));
    }
}
