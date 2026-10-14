using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;

namespace ZeroIchi.Models.FileStructure.Ksy.Model;

public interface IKsyNativeType
{
    string Name { get; }
}

public sealed class KsyNativeTypeRegistry
{
    private readonly Dictionary<string, IKsyNativeType> _types = new(StringComparer.Ordinal);

    public static KsyNativeTypeRegistry Empty => new();

    public void Register(IKsyNativeType type) => _types.Add(type.Name, type);

    public bool TryGet(string name, [NotNullWhen(true)] out IKsyNativeType? type) => _types.TryGetValue(name, out type);
}
