using System;
using System.Collections.Generic;
using System.Linq;
using ZeroIchi.Models.FileStructure.Ksy.Expressions;
using ZeroIchi.Models.FileStructure.Ksy.Yaml;

namespace ZeroIchi.Models.FileStructure.Ksy.Model;

public static partial class KsyModelBuilder
{
    private static readonly HashSet<string> TypeKeys =
        ["meta", "doc", "doc-ref", "to-string", "params", "seq", "types", "instances", "enums"];

    private static readonly HashSet<string> MetaKeys =
    [
        "id", "imports", "endian", "bit-endian", "encoding", "title", "ks-version", "ks-debug", "ks-opaque-types",
        "ks-zero-copy-substream", "license", "file-extension", "xref", "tags", "application",
    ];

    private static readonly HashSet<string> ParamKeys = ["id", "type", "enum", "doc", "doc-ref"];

    private static readonly HashSet<string> EnumValueKeys = ["id", "doc", "doc-ref"];

    private static readonly HashSet<string> EndianSwitchKeys = ["switch-on", "cases"];

    private static readonly HashSet<string> ParamPrimitiveTypes =
        ["bytes", "u1", "u2", "u4", "u8", "s1", "s2", "s4", "s8", "f4", "f8", "str", "bool", "struct", "io", "any"];

    private sealed partial class FileBuilder
    {
        private KsyType CreateType(YamlMapping map, string? name, KsyType? outer, string path)
        {
            CheckKeys(map, path, TypeKeys, "a type");
            var meta = AsMapping(map.Get("meta"), Child(path, "meta"));
            if (meta is not null) CheckKeys(meta, Child(path, "meta"), MetaKeys, "meta");

            if (outer is null)
            {
                var id = meta is null ? null : GetString(meta, "id", Child(path, "meta"));
                if (id is null)
                    Error(map, Child(path, "meta"), "meta/id is required at the top level");
                else if (CheckIdentifier(meta!.Get("id")!, Child(path, "meta/id"), id, "meta/id"))
                    _format.Id = id;
                name = _format.Id;
            }

            var type = new KsyType(name!, _format, outer, path);
            _types.Add((type, map));
            if (meta is not null) ReadMeta(type, meta, Child(path, "meta"));

            type.Doc = GetString(map, "doc", path);
            if (map.Get("-webide-representation") is YamlScalar representation)
                type.WebideRepresentation = representation.Text;

            if (AsMapping(map.Get("enums"), Child(path, "enums")) is { } enums)
            {
                foreach (var (key, value) in enums.Entries)
                {
                    var enumPath = Child(Child(path, "enums"), key.Text);
                    if (!CheckIdentifier(key, enumPath, key.Text, "enum")) continue;
                    var parsed = new KsyEnum(key.Text, type);
                    ReadEnumValues(parsed, value, enumPath);
                    type.AddEnum(parsed);
                }
            }

            if (AsMapping(map.Get("types"), Child(path, "types")) is { } types)
            {
                foreach (var (key, value) in types.Entries)
                {
                    var typePath = Child(Child(path, "types"), key.Text);
                    if (!CheckIdentifier(key, typePath, key.Text, "type")) continue;
                    if (AsItemMapping(value, typePath) is { } typeMap)
                        type.AddType(CreateType(typeMap, key.Text, type, typePath));
                }
            }

            return type;
        }

        private void ReadMeta(KsyType type, YamlMapping meta, string path)
        {
            if (meta.Get("endian") is { } endian) type.Endian = ReadEndian(endian, Child(path, "endian"), type);

            if (GetString(meta, "bit-endian", path) is { } bitEndian)
            {
                type.BitEndian = ParseByteOrder(bitEndian);
                if (type.BitEndian is null)
                    Error(meta.Get("bit-endian")!, Child(path, "bit-endian"), "bit-endian must be 'le' or 'be'");
            }

            if (GetString(meta, "encoding", path) is { } encoding)
                type.Encoding = CanonicalEncoding(meta.Get("encoding")!, Child(path, "encoding"), encoding);

            if (type.Outer is not null)
            {
                if (meta.Get("imports") is { } nestedImports)
                    Error(nestedImports, Child(path, "imports"), "imports are only supported in the top-level meta");
                return;
            }

            _format.OpaqueTypes = GetBool(meta, "ks-opaque-types", path, false);
            _format.FileExtensions = ReadStringList(meta.Get("file-extension"), Child(path, "file-extension"));
        }

        private KsyEndian? ReadEndian(YamlNode node, string path, KsyType type)
        {
            if (node is YamlScalar scalar)
            {
                if (ParseByteOrder(scalar.Text) is { } order) return new KsyFixedEndian(order);
                Error(node, path, "endian must be 'le', 'be' or a switch-on mapping");
                return null;
            }

            if (AsMapping(node, path) is not { } map) return null;
            CheckKeys(map, path, EndianSwitchKeys, "an endian switch");
            if (map.Get("switch-on") is null)
            {
                Error(map, path, "switch-on is required");
                return null;
            }

            var on = GetExpression(map, "switch-on", path, type);
            var cases = new List<KsyEndianCase>();
            if (AsMapping(map.Get("cases"), Child(path, "cases")) is { } caseMap)
            {
                foreach (var (key, value) in caseMap.Entries)
                {
                    var casePath = Child(Child(path, "cases"), key.Text);
                    var match = ParseExpression(key, casePath, type);
                    var order = value is YamlScalar s ? ParseByteOrder(s.Text) : null;
                    if (order is null) Error(value, casePath, "endian must be 'le' or 'be'");
                    if (match is not null && order is not null) cases.Add(new KsyEndianCase(match, order.Value));
                }
            }
            return on is null ? null : new KsySwitchEndian(on, cases);
        }

        private static KsyByteOrder? ParseByteOrder(string text) => text switch
        {
            "le" => KsyByteOrder.Le,
            "be" => KsyByteOrder.Be,
            _ => null,
        };

        private string? CanonicalEncoding(YamlNode node, string path, string name)
        {
            if (KsEncodings.Canonicalize(name) is not { } canonical)
            {
                Error(node, path, $"Unknown encoding '{name}'");
                return null;
            }

            if (canonical != name) Warning(node, path, $"Encoding '{name}' is an alias of '{canonical}'");
            return canonical;
        }

        private IReadOnlyList<string> ReadStringList(YamlNode? node, string path)
        {
            switch (node)
            {
                case null:
                    return [];
                case YamlScalar { Value.Kind: not YamlScalarKind.Null } scalar:
                    return [scalar.Text];
                case YamlSequence sequence:
                    var items = new List<string>();
                    foreach (var item in sequence.Items)
                    {
                        if (item is YamlScalar s) items.Add(s.Text);
                        else Error(item, path, "Expected a string");
                    }
                    return items;
                default:
                    Error(node, path, "Expected a string or a sequence of strings");
                    return [];
            }
        }

        private void ReadEnumValues(KsyEnum target, YamlNode node, string path)
        {
            if (AsMapping(node, path) is not { } map) return;

            foreach (var (key, value) in map.Entries)
            {
                var valuePath = Child(path, key.Text);
                if (key.Value.Kind != YamlScalarKind.Int)
                {
                    Error(key, valuePath, "Enum keys must be integers");
                    continue;
                }

                string? name;
                YamlNode nameNode = value;
                switch (value)
                {
                    case YamlScalar { Value.Kind: YamlScalarKind.String or YamlScalarKind.Bool } scalar:
                        name = scalar.Value.Kind == YamlScalarKind.Bool ? scalar.Value.ToExpressionSource() : scalar.Text;
                        break;
                    case YamlMapping valueMap:
                        CheckKeys(valueMap, valuePath, EnumValueKeys, "an enum value");
                        name = GetString(valueMap, "id", valuePath);
                        nameNode = valueMap.Get("id") ?? valueMap;
                        if (name is null) Error(valueMap, valuePath, "id is required");
                        break;
                    default:
                        Error(value, valuePath, "Enum values must be a name or a mapping with id");
                        continue;
                }

                if (name is null || !CheckIdentifier(nameNode, valuePath, name, "enum value")) continue;
                if (!target.TryAdd(key.Value.Int, name))
                    Error(nameNode, valuePath, $"Duplicate enum value name '{name}'");
            }
        }

        private void ParseParams(KsyType type, YamlMapping map)
        {
            var path = Child(type.Path, "params");
            if (AsSequence(map.Get("params"), path) is not { } sequence) return;

            var result = new List<KsyParam>();
            var ids = new HashSet<string>(StringComparer.Ordinal);
            for (var i = 0; i < sequence.Items.Count; i++)
            {
                var itemPath = Child(path, i.ToString());
                if (AsItemMapping(sequence.Items[i], itemPath) is not { } item) continue;
                CheckKeys(item, itemPath, ParamKeys, "a parameter");

                var id = GetString(item, "id", itemPath);
                if (id is null)
                {
                    Error(item, itemPath, "id is required");
                    continue;
                }
                Declare(type, id);
                if (!CheckIdentifier(item.Get("id")!, Child(itemPath, "id"), id, "parameter")) continue;
                if (!ids.Add(id))
                {
                    Error(item.Get("id")!, Child(itemPath, "id"), $"Duplicate attribute id '{id}'");
                    continue;
                }

                var typeName = GetString(item, "type", itemPath);
                var isArray = typeName?.EndsWith("[]") == true;
                if (isArray) typeName = typeName![..^2];

                KsyType? userType = null;
                if (typeName is not null && !ParamPrimitiveTypes.Contains(typeName) && !IsBitTypeName(typeName))
                {
                    userType = type.FindType(typeName.Split("::"));
                    if (userType is null)
                        Error(item.Get("type")!, Child(itemPath, "type"), $"Type '{typeName}' not found");
                }

                KsyEnum? enumType = null;
                if (GetString(item, "enum", itemPath) is { } enumName)
                {
                    enumType = type.FindEnum(enumName.Split("::"));
                    if (enumType is null)
                        Error(item.Get("enum")!, Child(itemPath, "enum"), $"Enum '{enumName}' not found");
                }

                result.Add(new KsyParam(id, typeName, isArray, userType, enumType));
            }
            type.Params = result;
        }

        private static bool IsBitTypeName(string name) =>
            name.Length > 1 && name[0] == 'b' && name[1..].All(char.IsAsciiDigit);
    }
}
