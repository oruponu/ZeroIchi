using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using ZeroIchi.Models.FileStructure.Ksy.Runtime;

namespace ZeroIchi.Models.FileStructure.Ksy.Model;

public interface IKsyNativeType
{
    string Name { get; }

    KsyNativeResult Read(KsStream stream);
}

public sealed record KsyNativeNode(string Name, long Position, long Length, string Description, ValueKind ValueKind)
{
    public IReadOnlyList<KsyNativeNode> Children { get; init; } = [];
}

public sealed record KsyNativeResult(long Consumed, IReadOnlyList<KsyNativeNode> Nodes);

public sealed class KsyNativeTypeRegistry
{
    private readonly Dictionary<string, IKsyNativeType> _types = new(StringComparer.Ordinal);

    public static KsyNativeTypeRegistry Empty => new();

    public void Register(IKsyNativeType type) => _types.Add(type.Name, type);

    public bool TryGet(string name, [NotNullWhen(true)] out IKsyNativeType? type) => _types.TryGetValue(name, out type);
}
