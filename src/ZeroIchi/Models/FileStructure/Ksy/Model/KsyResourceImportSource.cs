using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Reflection;

namespace ZeroIchi.Models.FileStructure.Ksy.Model;

public sealed class KsyResourceImportSource(Assembly assembly, string prefix) : IKsyImportSource
{
    public bool TryRead(string path, [NotNullWhen(true)] out string? text)
    {
        text = null;
        using var stream = assembly.GetManifestResourceStream(prefix + path.Replace('/', '.') + ".ksy");
        if (stream is null) return false;

        using var reader = new StreamReader(stream);
        text = reader.ReadToEnd();
        return true;
    }
}
