using System;
using System.Collections.Generic;
using System.Linq;
using ZeroIchi.Models.FileStructure.Ksy.Expressions;

namespace ZeroIchi.Models.FileStructure.Ksy.Model;

public static partial class KsyModelBuilder
{
    private static readonly HashSet<string> ReservedNames =
        ["_root", "_parent", "_io", "_", "_index", "_on", "_is_le", "_sizeof", "_buf"];

    private sealed partial class FileBuilder
    {
        private void CheckExpressions()
        {
            foreach (var use in _expressions)
            {
                var pending = new Stack<KsExpr>();
                pending.Push(use.Expr);
                while (pending.Count > 0)
                {
                    var node = pending.Pop();
                    if (CheckNode(use, node) is { } message)
                        Report(KsySeverity.Error, ExpressionPosition(use.Node, node.Position), use.Path, message);
                    foreach (var child in Children(node)) pending.Push(child);
                }
            }
        }

        private void Declare(KsyType type, string name)
        {
            if (!_declared.TryGetValue(type, out var names)) _declared[type] = names = new HashSet<string>(StringComparer.Ordinal);
            names.Add(name);
        }

        private bool IsDeclared(KsyType type, string name) =>
            _declared.TryGetValue(type, out var names) && names.Contains(name);

        private string? CheckNode(ExpressionUse use, KsExpr node)
        {
            var context = use.Context;
            switch (node)
            {
                case KsName { Name: "_sizeof" }:
                    return context.SizeInBits is null ? $"_sizeof cannot be used on variable-size type '{context.Name}'" : null;
                case KsName name when ReservedNames.Contains(name.Name):
                    return null;
                case KsName name:
                    return IsDeclared(context, name.Name)
                        ? null
                        : $"Name '{name.Name}' not found in type '{context.Name}'";
                case KsMember { Target: KsName { Name: "_root" }, Name: var rootMember } when !rootMember.StartsWith('_'):
                {
                    var root = context.Format.Root;
                    return IsDeclared(root, rootMember)
                        ? null
                        : $"Name '{rootMember}' not found in type '{root.Name}'";
                }
                case KsMember { Target: KsName target } member when FindMember(context, target.Name) is { } attribute:
                    return attribute.DataType switch
                    {
                        KsyNativeType when attribute.Repeat == KsyRepeatKind.None =>
                            $"Members of native type '{target.Name}' cannot be referenced",
                        KsyUserType user when member.Name == "_sizeof" && attribute.Size is null && !attribute.SizeEos
                            && attribute.Terminator is null && user.Type.SizeInBits is null =>
                            $"_sizeof cannot be used on variable-size type '{user.Type.Name}'",
                        _ => null,
                    };
                case KsEnumRef enumRef:
                {
                    var found = context.FindEnum(enumRef.EnumPath);
                    if (found is null) return $"Enum '{string.Join("::", enumRef.EnumPath)}' not found";
                    return found.TryGetValue(enumRef.ValueName, out _)
                        ? null
                        : $"Enum '{found.Name}' has no value '{enumRef.ValueName}'";
                }
                case KsCast cast:
                    return KsPrimitiveTypes.KindOf(cast.Type) is null && context.FindType(cast.Type.Path) is null
                        ? $"Type '{cast.Type.FullName}' not found"
                        : null;
                case KsSizeOf sizeOf:
                {
                    if (KsPrimitiveTypes.SizeInBits(sizeOf.Type) is not null) return null;
                    var type = context.FindType(sizeOf.Type.Path);
                    if (type is null) return $"Type '{sizeOf.Type.FullName}' not found";
                    return type.SizeInBits is null ? $"sizeof cannot be used on variable-size type '{type.Name}'" : null;
                }
                default:
                    return null;
            }
        }

        private static KsyAttribute? FindMember(KsyType type, string name) =>
            type.Seq.FirstOrDefault(a => a.Id == name) ?? type.Instances.FirstOrDefault(a => a.Id == name);

        private static IEnumerable<KsExpr> Children(KsExpr node) => node switch
        {
            KsFString e => e.Parts,
            KsArrayLiteral e => e.Elements,
            KsUnary e => [e.Operand],
            KsBinary e => [e.Left, e.Right],
            KsTernary e => [e.Condition, e.Then, e.Else],
            KsMember e => [e.Target],
            KsCall e => [e.Target, .. e.Arguments],
            KsIndex e => [e.Target, e.Index],
            KsCast e => [e.Target],
            _ => [],
        };
    }
}
