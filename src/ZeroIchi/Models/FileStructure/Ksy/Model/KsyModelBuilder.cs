using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using ZeroIchi.Models.FileStructure.Ksy.Expressions;
using ZeroIchi.Models.FileStructure.Ksy.Yaml;

namespace ZeroIchi.Models.FileStructure.Ksy.Model;

public static partial class KsyModelBuilder
{
    public static KsyLoadResult Parse(string text, string path = "main", IKsyImportSource? imports = null,
        KsyNativeTypeRegistry? natives = null)
    {
        var session = new Session(imports ?? new KsyMemoryImportSource(), natives ?? KsyNativeTypeRegistry.Empty);
        var root = session.Begin(path, text);
        session.Complete();
        var failed = root is null || session.Diagnostics.Exists(d => d.Severity == KsySeverity.Error);
        return new KsyLoadResult(failed ? null : root, session.Diagnostics);
    }

    public static KsyLoadResult Load(string path, IKsyImportSource source, KsyNativeTypeRegistry? natives = null)
    {
        if (!source.TryRead(path, out var text))
        {
            var missing = new KsyDiagnostic(KsySeverity.Error, path, 1, 1, "", $"File '{path}' not found");
            return new KsyLoadResult(null, [missing]);
        }
        return Parse(text, path, source, natives);
    }

    private sealed class Session(IKsyImportSource imports, KsyNativeTypeRegistry natives)
    {
        private readonly Dictionary<string, FileBuilder> _files = new(StringComparer.Ordinal);
        private readonly List<FileBuilder> _order = [];

        public IKsyImportSource Imports { get; } = imports;
        public KsyNativeTypeRegistry Natives { get; } = natives;
        public List<KsyDiagnostic> Diagnostics { get; } = [];

        public bool TryGetFile(string path, out FileBuilder? file) => _files.TryGetValue(path, out file);

        public KsyFormat? Begin(string path, string text)
        {
            var file = new FileBuilder(this, path);
            _files.Add(path, file);
            if (!file.Begin(text)) return null;
            _order.Add(file);
            return file.Format;
        }

        public void Complete()
        {
            foreach (var file in _order) file.ParseAllParams();
            foreach (var file in _order) file.ParseAllMembers();
            var sizes = new KsyStaticSize();
            foreach (var file in _order) file.ComputeSizes(sizes);
            foreach (var file in _order) file.Finish();
        }
    }

    [GeneratedRegex("^[a-z][a-z0-9_]*$")]
    private static partial Regex IdentifierPattern();

    private sealed partial class FileBuilder(Session session, string file)
    {
        private readonly KsyFormat _format = new(file);
        private readonly List<(KsyType Type, YamlMapping Map)> _types = [];
        private readonly List<ExpressionUse> _expressions = [];
        private YamlMapping? _root;

        private sealed record ExpressionUse(KsExpr Expr, KsyType Context, YamlScalar Node, string Path);

        public KsyFormat Format => _format;

        public bool Begin(string text)
        {
            var yaml = YamlLoader.Load(text);
            foreach (var error in yaml.Errors)
                session.Diagnostics.Add(new KsyDiagnostic(KsySeverity.Error, file, error.Position.Line,
                    error.Position.Column, "", error.Message));
            if (yaml.Root is null) return false;

            if (yaml.Root is not YamlMapping root)
            {
                Error(yaml.Root, "", "Top level must be a mapping");
                return false;
            }

            _root = root;
            _format.Root = CreateType(root, null, null, "");
            LoadImports(root);
            return true;
        }

        public void ParseAllParams()
        {
            foreach (var (type, map) in _types) ParseParams(type, map);
        }

        public void ParseAllMembers()
        {
            foreach (var (type, map) in _types) ParseMembers(type, map);
        }

        public void ComputeSizes(KsyStaticSize sizes)
        {
            foreach (var (type, _) in _types) type.SizeInBits = sizes.Compute(type);
        }

        public void Finish()
        {
            CheckExpressions();
            ReadDetection(_root!);
        }

        private void Error(YamlNode node, string path, string message) =>
            Report(KsySeverity.Error, node.Start, path, message);

        private void Warning(YamlNode node, string path, string message) =>
            Report(KsySeverity.Warning, node.Start, path, message);

        private void Report(KsySeverity severity, YamlPosition position, string path, string message) =>
            session.Diagnostics.Add(new KsyDiagnostic(severity, file, position.Line, position.Column, path, message));

        private static string Child(string path, string key) => $"{path}/{key}";

        private void CheckKeys(YamlMapping map, string path, IReadOnlySet<string> legal, string where)
        {
            foreach (var (key, _) in map.Entries)
            {
                if (key.Value.Kind == YamlScalarKind.String && key.Text.StartsWith('-')) continue;
                if (key.Value.Kind != YamlScalarKind.String || !legal.Contains(key.Text))
                    Error(key, Child(path, key.Text), $"Unknown key '{key.Text}' in {where}");
            }
        }

        private bool CheckIdentifier(YamlNode node, string path, string name, string what)
        {
            if (IdentifierPattern().IsMatch(name)) return true;
            Error(node, path, $"Invalid {what} name '{name}' (must match [a-z][a-z0-9_]*)");
            return false;
        }

        private YamlMapping? AsMapping(YamlNode? node, string path)
        {
            if (node is null or YamlMapping) return (YamlMapping?)node;
            if (node is YamlScalar { Value.Kind: YamlScalarKind.Null }) return null;
            Error(node, path, "Expected a mapping");
            return null;
        }

        private YamlSequence? AsSequence(YamlNode? node, string path)
        {
            if (node is null or YamlSequence) return (YamlSequence?)node;
            Error(node, path, "Expected a sequence");
            return null;
        }

        private string? GetString(YamlMapping map, string key, string path)
        {
            var node = map.Get(key);
            if (node is null) return null;
            if (node is YamlScalar { Value.Kind: not YamlScalarKind.Null } scalar)
                return scalar.Value.Kind == YamlScalarKind.String ? scalar.Text : scalar.Value.ToExpressionSource();

            Error(node, Child(path, key), $"'{key}' must be a string");
            return null;
        }

        private bool GetBool(YamlMapping map, string key, string path, bool defaultValue)
        {
            var node = map.Get(key);
            if (node is null) return defaultValue;
            if (node is YamlScalar { Value.Kind: YamlScalarKind.Bool } scalar) return scalar.Value.Bool;

            Error(node, Child(path, key), $"'{key}' must be true or false");
            return defaultValue;
        }

        private byte? GetByte(YamlMapping map, string key, string path)
        {
            var node = map.Get(key);
            if (node is null) return null;
            if (node is YamlScalar { Value.Kind: YamlScalarKind.Int } scalar && scalar.Value.Int >= 0 && scalar.Value.Int <= 255)
                return (byte)scalar.Value.Int;

            Error(node, Child(path, key), $"'{key}' must be an integer from 0 to 255");
            return null;
        }

        private KsExpr? GetExpression(YamlMapping map, string key, string path, KsyType context) =>
            map.Get(key) is { } node ? ParseExpression(node, Child(path, key), context) : null;

        private KsExpr? ParseExpression(YamlNode node, string path, KsyType context)
        {
            if (node is not YamlScalar { Value.Kind: not YamlScalarKind.Null } scalar)
            {
                Error(node, path, "Expected an expression");
                return null;
            }

            try
            {
                var expr = KsExpressionParser.Parse(scalar.Value.ToExpressionSource());
                _expressions.Add(new ExpressionUse(expr, context, scalar, path));
                return expr;
            }
            catch (KsExpressionException e)
            {
                Report(KsySeverity.Error, ExpressionPosition(scalar, e.Position), path, e.Message);
                return null;
            }
        }

        private static YamlPosition ExpressionPosition(YamlScalar scalar, int offset)
        {
            var quoted = scalar.Style is YamlScalarStyle.SingleQuoted or YamlScalarStyle.DoubleQuoted;
            var verbatim = quoted || scalar.Value.Kind == YamlScalarKind.String;
            if (!verbatim || scalar.Style is YamlScalarStyle.Literal or YamlScalarStyle.Folded) return scalar.Start;

            var clamped = Math.Clamp(offset, 0, scalar.Text.Length);
            if (scalar.Text.AsSpan(0, clamped).Contains('\n')) return scalar.Start;
            return scalar.Start with { Column = scalar.Start.Column + clamped + (quoted ? 1 : 0) };
        }
    }
}
