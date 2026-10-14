using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using ZeroIchi.Models.FileStructure.Ksy.Expressions;
using ZeroIchi.Models.FileStructure.Ksy.Yaml;

namespace ZeroIchi.Models.FileStructure.Ksy.Model;

public static partial class KsyModelBuilder
{
    private static readonly HashSet<string> BaseAttributeKeys =
        ["id", "doc", "doc-ref", "type", "if", "terminator", "consume", "include", "eos-error", "valid", "repeat"];

    private static readonly HashSet<string> BytesAttributeKeys =
        ["contents", "size", "size-eos", "pad-right", "parent", "process"];

    private static readonly HashSet<string> StrAttributeKeys = ["size", "size-eos", "pad-right", "encoding"];

    private static readonly HashSet<string> ValueInstanceKeys = ["value", "doc", "doc-ref", "enum", "if"];

    private static readonly HashSet<string> SwitchKeys = ["switch-on", "cases"];

    [GeneratedRegex("^([us])([1248])(le|be)?$")]
    private static partial Regex IntTypePattern();

    [GeneratedRegex("^f([48])(le|be)?$")]
    private static partial Regex FloatTypePattern();

    [GeneratedRegex("^b([0-9]+)(le|be)?$")]
    private static partial Regex BitTypePattern();

    [GeneratedRegex("^(xor|rol|ror)\\((.*)\\)$", RegexOptions.Singleline)]
    private static partial Regex ProcessPattern();

    [GeneratedRegex("^(-?[0-9]+|0x[0-9a-fA-F]+)$")]
    private static partial Regex NumericContentPattern();

    private sealed partial class FileBuilder
    {
        private void ParseMembers(KsyType type, YamlMapping map)
        {
            var ids = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (var param in type.Params)
                ids[param.Id] = Child(type.Path, "params");

            var seqPath = Child(type.Path, "seq");
            var seq = new List<KsyAttribute>();
            if (AsSequence(map.Get("seq"), seqPath) is { } items)
            {
                for (var i = 0; i < items.Items.Count; i++)
                {
                    var itemPath = Child(seqPath, i.ToString(CultureInfo.InvariantCulture));
                    if (AsItemMapping(items.Items[i], itemPath) is not { } item) continue;

                    var id = GetString(item, "id", itemPath);
                    if (id is not null) Declare(type, id);
                    if (id is null)
                        id = $"_unnamed{i}";
                    else if (!CheckIdentifier(item.Get("id")!, Child(itemPath, "id"), id, "attribute"))
                        continue;
                    else if (!ids.TryAdd(id, itemPath))
                        Error(item.Get("id")!, Child(itemPath, "id"), $"Duplicate attribute id '{id}'");

                    if (ParseAttribute(item, itemPath, type, KsyAttributeKind.Seq, id) is { } attribute)
                        seq.Add(attribute);
                }
            }
            type.Seq = seq;

            var instancesPath = Child(type.Path, "instances");
            var instances = new List<KsyAttribute>();
            if (AsMapping(map.Get("instances"), instancesPath) is { } instanceMap)
            {
                foreach (var (key, value) in instanceMap.Entries)
                {
                    var itemPath = Child(instancesPath, key.Text);
                    Declare(type, key.Text);
                    if (!CheckIdentifier(key, itemPath, key.Text, "instance")) continue;
                    if (!ids.TryAdd(key.Text, itemPath))
                        Error(key, itemPath, $"Duplicate attribute id '{key.Text}'");
                    if (AsItemMapping(value, itemPath) is not { } item) continue;

                    var kind = item.Get("value") is null ? KsyAttributeKind.ParseInstance : KsyAttributeKind.ValueInstance;
                    if (ParseAttribute(item, itemPath, type, kind, key.Text) is { } attribute)
                        instances.Add(attribute);
                }
            }
            type.Instances = instances;

            type.ToStringExpr = GetExpression(map, "to-string", type.Path, type);
        }

        private KsyAttribute? ParseAttribute(YamlMapping map, string path, KsyType owner, KsyAttributeKind kind, string id)
        {
            if (kind == KsyAttributeKind.ValueInstance)
            {
                CheckKeys(map, path, ValueInstanceKeys, "a value instance");
                return new KsyAttribute
                {
                    Id = id,
                    Kind = kind,
                    Path = path,
                    Value = GetExpression(map, "value", path, owner),
                    If = GetExpression(map, "if", path, owner),
                    Enum = ReadEnumReference(map, path, owner),
                    Doc = GetString(map, "doc", path),
                };
            }

            var encodingName = map.Get("encoding") is { } encodingNode && GetString(map, "encoding", path) is { } name
                ? CanonicalEncoding(encodingNode, Child(path, "encoding"), name)
                : null;
            if (map.Get("encoding") is not null && encodingName is null) return null;
            var hasEnum = map.Get("enum") is not null;
            var dataType = ReadDataType(map, path, owner, encodingName, hasEnum);
            if (dataType is null) return null;

            var legal = new HashSet<string>(BaseAttributeKeys);
            legal.UnionWith(dataType switch
            {
                KsyStrType => StrAttributeKeys,
                KsyIntType or KsyBitType => ["enum"],
                KsyFloatType => [],
                _ => BytesAttributeKeys,
            });
            var (repeat, repeatExpr, repeatKey) = ReadRepeat(map, path, owner);
            if (repeatKey is not null) legal.Add(repeatKey);
            if (kind == KsyAttributeKind.ParseInstance) legal.UnionWith(["pos", "io"]);
            CheckKeys(map, path, legal, kind == KsyAttributeKind.Seq ? "an attribute" : "a parse instance");

            var contents = map.Get("contents") is { } contentsNode ? ReadContents(contentsNode, Child(path, "contents")) : null;
            var size = GetExpression(map, "size", path, owner);
            var sizeEos = GetBool(map, "size-eos", path, false);
            var terminator = GetByte(map, "terminator", path) is { } t ? new[] { t } : null;
            if (dataType is KsyStrType { IsZeroTerminated: true } strz && terminator is null)
                terminator = DefaultZeroTerminator(strz.EncodingName);

            if (size is not null && sizeEos)
                Error(map, path, "size and size-eos cannot be used together");

            var hasSize = map.Get("size") is not null || map.Get("size-eos") is not null || terminator is not null
                || map.Get("terminator") is not null;
            switch (dataType)
            {
                case KsyBytesType when map.Get("contents") is not null:
                    if (hasSize) Warning(map, path, "size, size-eos and terminator are ignored when contents is given");
                    break;
                case KsyBytesType or KsyStrType when !hasSize:
                    Error(map, path, "size, size-eos or terminator is required");
                    break;
            }

            var process = map.Get("process") is { } processNode ? ReadProcess(processNode, Child(path, "process"), owner) : null;
            if (process is not null && !hasSize && map.Get("contents") is null)
                Error(map, path, "process requires size, size-eos or terminator");

            var enumType = legal.Contains("enum") ? ReadEnumReference(map, path, owner) : null;
            var valid = map.Get("valid") is { } validNode ? ReadValidation(validNode, Child(path, "valid"), owner) : null;
            if (valid is not null && contents is not null)
                Error(map, path, "contents and valid cannot be used together");
            if (valid is KsyValidInEnum && enumType is null)
                Error(map.Get("valid")!, Child(path, "valid"), "valid/in-enum requires enum");

            return new KsyAttribute
            {
                Id = id,
                Kind = kind,
                Path = path,
                DataType = dataType,
                Enum = enumType,
                If = GetExpression(map, "if", path, owner),
                Size = size,
                SizeEos = sizeEos,
                Terminator = terminator,
                Consume = GetBool(map, "consume", path, true),
                Include = GetBool(map, "include", path, false),
                EosError = GetBool(map, "eos-error", path, true),
                PadRight = GetByte(map, "pad-right", path),
                Contents = contents,
                Repeat = repeat,
                RepeatExpr = repeatExpr,
                Process = process,
                Valid = valid,
                Parent = GetExpression(map, "parent", path, owner),
                Pos = GetExpression(map, "pos", path, owner),
                Io = GetExpression(map, "io", path, owner),
                Doc = GetString(map, "doc", path),
            };
        }

        private KsyDataType? ReadDataType(YamlMapping map, string path, KsyType owner, string? encoding, bool hasEnum)
        {
            switch (map.Get("type"))
            {
                case null:
                    return new KsyBytesType();
                case YamlMapping switchMap:
                    return ReadSwitch(switchMap, Child(path, "type"), owner, encoding, hasEnum);
                case YamlScalar { Value.Kind: YamlScalarKind.String } scalar:
                    return ReadTypeName(scalar, Child(path, "type"), owner, encoding, hasEnum);
                case var other:
                    Error(other, Child(path, "type"), "type must be a string or a switch mapping");
                    return null;
            }
        }

        private KsyDataType? ReadSwitch(YamlMapping map, string path, KsyType owner, string? encoding, bool hasEnum)
        {
            CheckKeys(map, path, SwitchKeys, "a switch type");
            if (map.Get("switch-on") is null || map.Get("cases") is null)
            {
                Error(map, path, "switch-on and cases are required");
                return null;
            }

            var on = GetExpression(map, "switch-on", path, owner);
            var cases = new List<KsySwitchCase>();
            if (AsMapping(map.Get("cases"), Child(path, "cases")) is { } caseMap)
            {
                foreach (var (key, value) in caseMap.Entries)
                {
                    var casePath = Child(Child(path, "cases"), key.Text);
                    var match = key is { Value.Kind: YamlScalarKind.String, Text: "_" }
                        ? null
                        : ParseExpression(key, casePath, owner);
                    if (value is not YamlScalar { Value.Kind: YamlScalarKind.String } typeName)
                    {
                        Error(value, casePath, "Case type must be a type name");
                        continue;
                    }

                    if (ReadTypeName(typeName, casePath, owner, encoding, hasEnum) is { } caseType)
                        cases.Add(new KsySwitchCase(match, caseType));
                }
            }
            return on is null ? null : new KsySwitchType(on, cases);
        }

        private KsyDataType? ReadTypeName(YamlScalar node, string path, KsyType owner, string? encoding, bool hasEnum)
        {
            var text = node.Text.Trim();

            if (IntTypePattern().Match(text) is { Success: true } intMatch)
            {
                var size = intMatch.Groups[2].Value[0] - '0';
                var order = ExplicitOrder(intMatch.Groups[3], size, node, path, owner, text);
                return new KsyIntType(size, intMatch.Groups[1].Value == "s", order);
            }

            if (FloatTypePattern().Match(text) is { Success: true } floatMatch)
            {
                var size = floatMatch.Groups[1].Value[0] - '0';
                return new KsyFloatType(size, ExplicitOrder(floatMatch.Groups[2], size, node, path, owner, text));
            }

            if (BitTypePattern().Match(text) is { Success: true } bitMatch)
            {
                if (!int.TryParse(bitMatch.Groups[1].Value, CultureInfo.InvariantCulture, out var width) || width is < 1 or > 64)
                {
                    Error(node, path, $"Bit type '{text}' must be 1 to 64 bits wide");
                    return null;
                }

                var bitOrder = ParseByteOrder(bitMatch.Groups[2].Value) ?? owner.EffectiveBitEndian;
                return new KsyBitType(width, bitOrder, width == 1 && !hasEnum);
            }

            if (text is "str" or "strz")
            {
                var name = encoding ?? owner.EffectiveEncoding;
                if (name is null || !KsEncodings.TryGet(name, out var resolved))
                {
                    if (!IsInChain(owner, _invalidEncoding))
                        Error(node, path, "A string type needs an encoding (encoding or meta/encoding)");
                    return null;
                }
                return new KsyStrType(name, resolved, text == "strz");
            }

            return ReadUserType(node, path, owner, text);
        }

        private KsyByteOrder? ExplicitOrder(Group suffix, int size, YamlNode node, string path, KsyType owner, string text)
        {
            if (suffix.Success) return ParseByteOrder(suffix.Value);
            if (size > 1 && owner.EffectiveEndian is null && !IsInChain(owner, _invalidEndian))
                Error(node, path, $"Type '{text}' needs an endianness (le/be suffix or meta/endian)");
            return null;
        }

        private static bool IsInChain(KsyType type, HashSet<KsyType> types)
        {
            for (var scope = type; scope is not null; scope = scope.Outer)
            {
                if (types.Contains(scope)) return true;
            }
            return false;
        }

        private KsyDataType? ReadUserType(YamlScalar node, string path, KsyType owner, string text)
        {
            var open = text.IndexOf('(');
            var name = (open < 0 ? text : text[..open]).Trim();
            IReadOnlyList<KsExpr> arguments = [];
            if (open >= 0)
            {
                if (!text.EndsWith(')'))
                {
                    Error(node, path, $"Invalid type '{text}'");
                    return null;
                }

                try
                {
                    if (KsExpressionParser.Parse("[" + text[(open + 1)..^1] + "]") is not KsArrayLiteral list)
                    {
                        Error(node, path, $"Invalid type arguments in '{text}'");
                        return null;
                    }
                    arguments = list.Elements;
                    foreach (var argument in arguments)
                        _expressions.Add(new ExpressionUse(argument, owner, node, path));
                }
                catch (KsExpressionException e)
                {
                    Report(KsySeverity.Error, node.Start, path, e.Message);
                    return null;
                }
            }

            var parts = name.Split("::");
            if (Array.Exists(parts, p => !IdentifierPattern().IsMatch(p)))
            {
                Error(node, path, $"Invalid type '{text}'");
                return null;
            }

            if (owner.FindType(parts) is { } type)
            {
                if (type.Params.Count != arguments.Count)
                {
                    Error(node, path,
                        $"Type '{name}' takes {type.Params.Count} argument(s) but {arguments.Count} were given");
                    return null;
                }
                return new KsyUserType(type, arguments);
            }

            if (_format.OpaqueTypes && parts.Length == 1 && session.Natives.TryGet(name, out var native))
            {
                if (arguments.Count == 0) return new KsyNativeType(native);
                Error(node, path, $"Native type '{name}' does not take arguments");
                return null;
            }

            Error(node, path, $"Type '{name}' not found");
            return null;
        }

        private (KsyRepeatKind, KsExpr?, string?) ReadRepeat(YamlMapping map, string path, KsyType owner)
        {
            switch (GetString(map, "repeat", path))
            {
                case null:
                    return (KsyRepeatKind.None, null, null);
                case "eos":
                    return (KsyRepeatKind.Eos, null, null);
                case "expr":
                    return (KsyRepeatKind.Expr, RequiredExpression(map, "repeat-expr", path, owner), "repeat-expr");
                case "until":
                    return (KsyRepeatKind.Until, RequiredExpression(map, "repeat-until", path, owner), "repeat-until");
                default:
                    Error(map.Get("repeat")!, Child(path, "repeat"), "repeat must be 'eos', 'expr' or 'until'");
                    return (KsyRepeatKind.None, null, null);
            }
        }

        private KsExpr? RequiredExpression(YamlMapping map, string key, string path, KsyType owner)
        {
            if (map.Get(key) is null)
            {
                Error(map, path, $"{key} is required");
                return null;
            }
            return GetExpression(map, key, path, owner);
        }

        private KsyEnum? ReadEnumReference(YamlMapping map, string path, KsyType owner)
        {
            if (GetString(map, "enum", path) is not { } name) return null;

            var found = owner.FindEnum(name.Split("::"));
            if (found is null) Error(map.Get("enum")!, Child(path, "enum"), $"Enum '{name}' not found");
            return found;
        }

        private byte[]? ReadContents(YamlNode node, string path)
        {
            switch (node)
            {
                case YamlScalar { Value.Kind: YamlScalarKind.String } text:
                    return Encoding.UTF8.GetBytes(text.Text);
                case YamlSequence sequence:
                {
                    var bytes = new List<byte>();
                    foreach (var item in sequence.Items)
                    {
                        switch (item)
                        {
                            case YamlScalar { Value.Kind: YamlScalarKind.Int } number when number.Value.Int >= -128 && number.Value.Int <= 255:
                                bytes.Add((byte)(int)number.Value.Int);
                                break;
                            case YamlScalar { Value.Kind: YamlScalarKind.String } s when NumericContentPattern().IsMatch(s.Text):
                                if (ParseContentNumber(s.Text) is { } b) bytes.Add(b);
                                else Error(item, path, $"Byte value '{s.Text}' is out of range");
                                break;
                            case YamlScalar { Value.Kind: YamlScalarKind.String } s:
                                bytes.AddRange(Encoding.UTF8.GetBytes(s.Text));
                                break;
                            default:
                                Error(item, path, "contents items must be bytes or strings");
                                break;
                        }
                    }
                    return [.. bytes];
                }
                default:
                    Error(node, path, "contents must be a string or a sequence");
                    return null;
            }
        }

        private static byte? ParseContentNumber(string text)
        {
            if (text.StartsWith("0x", StringComparison.Ordinal))
            {
                return ulong.TryParse(text.AsSpan(2), NumberStyles.AllowHexSpecifier, CultureInfo.InvariantCulture, out var hex)
                    && hex <= 255
                        ? (byte)hex
                        : null;
            }

            return long.TryParse(text, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out var value)
                && value is >= -128 and <= 255
                    ? (byte)value
                    : null;
        }

        private static byte[] DefaultZeroTerminator(string encoding) => encoding switch
        {
            "UTF-16LE" or "UTF-16BE" => [0, 0],
            "UTF-32LE" or "UTF-32BE" => [0, 0, 0, 0],
            _ => [0],
        };

        private KsyProcess? ReadProcess(YamlNode node, string path, KsyType owner)
        {
            if (node is not YamlScalar { Value.Kind: YamlScalarKind.String } scalar)
            {
                Error(node, path, "process must be a string");
                return null;
            }

            var text = scalar.Text.Trim();
            if (text == "zlib") return new KsyProcess(KsyProcessKind.Zlib, null);

            if (ProcessPattern().Match(text) is { Success: true } match)
            {
                var kind = match.Groups[1].Value switch
                {
                    "xor" => KsyProcessKind.Xor,
                    "rol" => KsyProcessKind.Rol,
                    _ => KsyProcessKind.Ror,
                };
                try
                {
                    var argument = KsExpressionParser.Parse(match.Groups[2].Value);
                    _expressions.Add(new ExpressionUse(argument, owner, scalar, path));
                    return new KsyProcess(kind, argument);
                }
                catch (KsExpressionException e)
                {
                    Report(KsySeverity.Error, scalar.Start, path, e.Message);
                    return null;
                }
            }

            Error(node, path, $"Unsupported process '{text}' (only zlib, xor, rol and ror are supported)");
            return null;
        }

        private KsyValidation? ReadValidation(YamlNode node, string path, KsyType owner)
        {
            if (node is YamlScalar scalar)
            {
                if (scalar.Value.Kind is YamlScalarKind.Float or YamlScalarKind.Null)
                {
                    Error(node, path, "valid must be an expression or a mapping");
                    return null;
                }
                return ParseExpression(scalar, path, owner) is { } eq ? new KsyValidEq(eq) : null;
            }

            if (AsMapping(node, path) is not { } map) return null;

            if (map.Get("eq") is not null)
            {
                CheckKeys(map, path, new HashSet<string> { "eq" }, "valid");
                return GetExpression(map, "eq", path, owner) is { } eq ? new KsyValidEq(eq) : null;
            }

            if (map.Get("min") is not null || map.Get("max") is not null)
            {
                CheckKeys(map, path, new HashSet<string> { "min", "max" }, "valid");
                return new KsyValidRange(GetExpression(map, "min", path, owner), GetExpression(map, "max", path, owner));
            }

            if (map.Get("any-of") is { } anyOf)
            {
                CheckKeys(map, path, new HashSet<string> { "any-of" }, "valid");
                var values = new List<KsExpr>();
                var items = anyOf is YamlSequence sequence ? sequence.Items : [anyOf];
                for (var i = 0; i < items.Count; i++)
                {
                    if (ParseExpression(items[i], Child(Child(path, "any-of"), i.ToString(CultureInfo.InvariantCulture)), owner) is { } value)
                        values.Add(value);
                }
                return new KsyValidAnyOf(values);
            }

            if (map.Get("in-enum") is not null)
            {
                CheckKeys(map, path, new HashSet<string> { "in-enum" }, "valid");
                if (!GetBool(map, "in-enum", path, false))
                {
                    Error(map, path, "in-enum must be true");
                    return null;
                }
                return new KsyValidInEnum();
            }

            if (map.Get("expr") is not null)
            {
                CheckKeys(map, path, new HashSet<string> { "expr" }, "valid");
                return GetExpression(map, "expr", path, owner) is { } condition ? new KsyValidExpr(condition) : null;
            }

            CheckKeys(map, path, new HashSet<string>(), "valid");
            if (map.Entries.Count == 0) Error(map, path, "valid needs eq, min, max, any-of, in-enum or expr");
            return null;
        }
    }
}
