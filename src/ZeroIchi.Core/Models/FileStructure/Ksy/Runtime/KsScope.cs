using System;
using System.Collections.Generic;
using ZeroIchi.Models.FileStructure.Ksy.Expressions;

namespace ZeroIchi.Models.FileStructure.Ksy.Runtime;

internal sealed class KsScope(IKsEvalScope inner) : IKsEvalScope
{
    public Int128? Index { get; set; }
    public KsValue? Underscore { get; set; }

    public bool TryResolveName(string name, out KsValue value)
    {
        switch (name)
        {
            case "_index" when Index is { } index:
                value = KsValue.FromInt(index);
                return true;
            case "_" when Underscore is { } underscore:
                value = underscore;
                return true;
            default:
                return inner.TryResolveName(name, out value);
        }
    }

    public bool TryResolveFieldSize(string name, out long size) => inner.TryResolveFieldSize(name, out size);

    public IKsEnumType? ResolveEnum(IReadOnlyList<string> path) => inner.ResolveEnum(path);

    public IKsTypeInfo? ResolveType(IReadOnlyList<string> path) => inner.ResolveType(path);
}
