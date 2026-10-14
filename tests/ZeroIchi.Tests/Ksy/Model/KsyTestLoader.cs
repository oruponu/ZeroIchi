using ZeroIchi.Models.FileStructure.Ksy.Model;

namespace ZeroIchi.Tests.Ksy.Model;

internal static class KsyTestLoader
{
    public static KsyFormat Load(string yaml, IKsyImportSource? imports = null, KsyNativeTypeRegistry? natives = null)
    {
        var result = KsyModelBuilder.Parse(yaml, "main", imports, natives);
        Assert.True(result.Format is not null, string.Join("\n", result.Diagnostics));
        Assert.DoesNotContain(result.Diagnostics, d => d.Severity == KsySeverity.Error);
        return result.Format!;
    }

    public static KsyDiagnostic SingleError(string yaml, IKsyImportSource? imports = null, KsyNativeTypeRegistry? natives = null)
    {
        var result = KsyModelBuilder.Parse(yaml, "main", imports, natives);
        Assert.Null(result.Format);
        return Assert.Single(result.Diagnostics, d => d.Severity == KsySeverity.Error);
    }

    public static KsyAttribute Attribute(KsyFormat format, string id) =>
        format.Root.Seq.Concat(format.Root.Instances).Single(a => a.Id == id);
}
