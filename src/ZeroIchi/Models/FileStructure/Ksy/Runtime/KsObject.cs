using System;
using System.Collections.Generic;
using ZeroIchi.Models.FileStructure.Ksy.Expressions;
using ZeroIchi.Models.FileStructure.Ksy.Model;

namespace ZeroIchi.Models.FileStructure.Ksy.Runtime;

internal sealed class KsObject : IKsyObject, IKsEvalScope
{
    private readonly KsyRun _run;
    private readonly KsyTypeLayout _layout;
    private readonly KsValue[] _fields;
    private readonly bool[] _read;
    private readonly KsValue[] _params;
    private readonly InstanceState[] _instances;

    internal KsObject(KsyRun run, KsyType type, IKsStruct? parent, KsObject? root, KsStream io, KsValue[] args,
        KsyNode node)
    {
        _run = run;
        Type = type;
        Parent = parent;
        RootObject = root ?? this;
        Stream = io;
        Node = node;
        _params = args;
        _layout = run.Layout(type);
        _fields = new KsValue[type.Seq.Count];
        _read = new bool[type.Seq.Count];
        _instances = new InstanceState[type.Instances.Count];
    }

    public KsyType Type { get; }
    public string TypeName => Type.Name;
    public IKsStruct? Parent { get; }
    public IKsStruct Root => RootObject;
    public IKsStream Io => Stream;
    public long? StaticSize => Type.SizeInBits is { } bits && bits % 8 == 0 ? bits / 8 : null;

    internal KsObject RootObject { get; }
    internal KsStream Stream { get; }
    internal KsyNode Node { get; }
    internal KsyByteOrder? Endian { get; set; }
    internal int Depth { get; init; }

    internal void SetField(int index, KsValue value)
    {
        _fields[index] = value;
        _read[index] = true;
    }

    internal ref InstanceState Instance(int index) => ref _instances[index];

    internal struct InstanceState
    {
        public KsyInstanceStatus Status;
        public KsValue Value;
        public string? Error;
    }

    public bool TryGetMember(string name, out KsValue value)
    {
        value = KsValue.Null;
        if (!_layout.TryGet(name, out var member)) return false;

        switch (member.Kind)
        {
            case KsyMemberKind.Param:
                value = _params[member.Index];
                return true;
            case KsyMemberKind.Instance:
                value = _run.GetInstance(this, member.Index);
                return true;
            default:
                if (!_read[member.Index]) throw new KsExpressionException($"'{name}' has not been read yet", -1, 0);
                value = _fields[member.Index];
                return true;
        }
    }

    public bool TryResolveName(string name, out KsValue value)
    {
        switch (name)
        {
            case "_root":
                value = KsValue.FromStruct(RootObject);
                return true;
            case "_parent":
                value = Parent is { } parent ? KsValue.FromStruct(parent) : KsValue.Null;
                return true;
            case "_io":
                value = KsValue.FromStream(Stream);
                return true;
            case "_sizeof":
                value = StaticSize is { } size
                    ? KsValue.FromInt(size)
                    : throw new KsExpressionException($"Type '{Type.Name}' is not fixed-size", -1, 0);
                return true;
            default:
                return TryGetMember(name, out value);
        }
    }

    public IKsEnumType? ResolveEnum(IReadOnlyList<string> path) => Type.FindEnum(path);

    public IKsTypeInfo? ResolveType(IReadOnlyList<string> path) => Type.FindType(path);
}

internal enum KsyMemberKind
{
    Param,
    Seq,
    Instance,
}

internal enum KsyInstanceStatus
{
    Unevaluated,
    Evaluating,
    Done,
    Failed,
}

internal sealed class KsyTypeLayout
{
    private readonly Dictionary<string, (KsyMemberKind Kind, int Index)> _members = new(StringComparer.Ordinal);

    public KsyTypeLayout(KsyType type)
    {
        for (var i = 0; i < type.Params.Count; i++) _members.TryAdd(type.Params[i].Id, (KsyMemberKind.Param, i));
        for (var i = 0; i < type.Seq.Count; i++) _members.TryAdd(type.Seq[i].Id, (KsyMemberKind.Seq, i));
        for (var i = 0; i < type.Instances.Count; i++)
            _members.TryAdd(type.Instances[i].Id, (KsyMemberKind.Instance, i));
    }

    public bool TryGet(string name, out (KsyMemberKind Kind, int Index) member) => _members.TryGetValue(name, out member);
}
