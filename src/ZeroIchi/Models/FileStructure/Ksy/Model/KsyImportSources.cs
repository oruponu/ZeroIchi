using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;

namespace ZeroIchi.Models.FileStructure.Ksy.Model;

public interface IKsyImportSource
{
    bool TryRead(string path, [NotNullWhen(true)] out string? text);
}

public sealed class KsyMemoryImportSource : IKsyImportSource
{
    private readonly Dictionary<string, string> _files = new(StringComparer.Ordinal);

    public KsyMemoryImportSource Add(string path, string text)
    {
        _files[path] = text;
        return this;
    }

    public bool TryRead(string path, [NotNullWhen(true)] out string? text) => _files.TryGetValue(path, out text);
}
