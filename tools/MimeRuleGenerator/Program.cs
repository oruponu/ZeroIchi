using System.Globalization;
using System.Text;
using System.Xml.Linq;
using ZeroIchi.Models.FileTypes;

const string TikaVersion = "4.1.0";
const string TikaCommit = "c5348700b3f58e5559a50c8e1156d7e2806f42f9";
const int TypesPerMethod = 100;

XNamespace tika = "https://tika.apache.org/";
var knownAttributes = new Dictionary<XName, string[]>
{
    ["mime-info"] = [],
    ["mime-type"] = ["type", "interpreted"],
    ["glob"] = ["pattern", "isregex"],
    ["magic"] = ["priority"],
    ["match"] = ["type", "offset", "value", "mask", "minShouldMatch"],
    ["sub-class-of"] = ["type"],
    ["alias"] = ["type"],
    ["acronym"] = [],
    ["_comment"] = [],
    ["comment"] = [],
    ["root-XML"] = ["localName", "namespaceURI"],
    [tika + "link"] = [],
    [tika + "uti"] = [],
    ["url"] = [],
};

if (args.Length != 2)
{
    Console.Error.WriteLine("Usage: MimeRuleGenerator <tika-mimetypes.xml> <output .cs>");
    return 1;
}

CultureInfo.CurrentCulture = CultureInfo.InvariantCulture;
CultureInfo.CurrentUICulture = CultureInfo.InvariantCulture;

var document = XDocument.Load(args[0]);
foreach (var element in document.Descendants())
{
    if (!knownAttributes.TryGetValue(element.Name, out var attributes))
        throw new InvalidDataException($"Unknown element: {element.Name}");
    foreach (var attribute in element.Attributes())
    {
        if (attribute.IsNamespaceDeclaration) continue;
        if (!attributes.Contains(attribute.Name.LocalName) || attribute.Name.Namespace != XNamespace.None)
            throw new InvalidDataException($"Unknown attribute: {element.Name}@{attribute.Name}");
    }
}

var mimeTypes = document.Root!.Elements("mime-type").ToArray();
var aliases = new Dictionary<string, string>();
foreach (var type in mimeTypes)
{
    foreach (var alias in type.Elements("alias"))
        aliases.TryAdd(Attr(alias, "type"), Attr(type, "type"));
}

var unsupported = new List<string>();
var entries = new List<MimeTypeEntry>();
foreach (var definitions in mimeTypes.GroupBy(type => Attr(type, "type")))
{
    var name = definitions.Key;
    var parents = definitions.Elements("sub-class-of").Select(e => Attr(e, "type")).Distinct().ToArray();
    if (parents.Length > 1) throw new InvalidDataException($"{name} has more than one parent");
    var parent = parents.Length == 0 ? null : aliases.GetValueOrDefault(parents[0], parents[0]);

    var extensions = definitions.Descendants("glob")
        .Where(glob => !string.Equals((string?)glob.Attribute("isregex"), "true", StringComparison.OrdinalIgnoreCase))
        .Select(glob => Attr(glob, "pattern"))
        .Where(pattern => pattern.StartsWith("*.", StringComparison.Ordinal) && pattern.Length > 2
                          && pattern.AsSpan(2).IndexOfAny("*?[/") < 0)
        .Select(pattern => pattern[2..].ToLowerInvariant())
        .Distinct()
        .ToArray();

    var magics = new List<MimeMagic>();
    foreach (var magic in definitions.Elements("magic"))
    {
        var priorityText = (string?)magic.Attribute("priority");
        var priority = string.IsNullOrEmpty(priorityText) ? 50 : int.Parse(priorityText, CultureInfo.InvariantCulture);
        foreach (var match in magic.Elements("match"))
        {
            var clause = BuildMatch(name, match);
            magics.Add(new MimeMagic(priority, clause));
            if (clause.Kind != MagicMatchKind.Unsupported && !CanMatch(clause))
                unsupported.Add($"{name}: a magic can never match because all of its alternatives are unsupported");
        }
    }

    if (magics.Count == 0 && definitions.Elements("root-XML").Any())
        unsupported.Add($"{name}: detected only by the XML root element, which is not supported");

    entries.Add(new MimeTypeEntry(name, parent, extensions, magics.ToArray()));
}

var names = entries.Select(e => e.Name).ToHashSet();
foreach (var entry in entries)
{
    if (entry.Parent is not null && !names.Contains(entry.Parent))
        throw new InvalidDataException($"{entry.Name} has an unknown parent: {entry.Parent}");
}

Console.WriteLine($"Unsupported rules ({unsupported.Count}):");
foreach (var line in unsupported) Console.WriteLine($"- {line}");

File.WriteAllText(args[1], Generate(entries, unsupported), new UTF8Encoding(false));
Console.WriteLine($"Wrote {entries.Count} types to {args[1]}");
return 0;

MagicMatch BuildMatch(string typeName, XElement element)
{
    var children = element.Elements("match").Select(child => BuildMatch(typeName, child)).ToArray();
    var match = TikaMagicParser.Parse(
        (string?)element.Attribute("type"), (string?)element.Attribute("offset"), (string?)element.Attribute("value"),
        (string?)element.Attribute("mask"), (string?)element.Attribute("minShouldMatch"), children, out var reason);
    if (reason is not null) unsupported.Add($"{typeName}: {OneLine(reason)}");
    return match;
}

static bool CanMatch(MagicMatch match) => match.Kind switch
{
    MagicMatchKind.Unsupported => false,
    MagicMatchKind.MinShouldMatch => match.Children.Count(CanMatch) >= match.MinShouldMatch,
    _ => match.Children.Length == 0 || match.Children.Any(CanMatch),
};

static string Attr(XElement element, string name) =>
    (string?)element.Attribute(name) ?? throw new InvalidDataException($"{element.Name} has no {name} attribute");

static string OneLine(string text) => string.Join(" ", text.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries));

static string Generate(List<MimeTypeEntry> entries, List<string> unsupported)
{
    var code = new StringBuilder();
    void Line(string text = "") => code.Append(text).Append("\r\n");

    Line($"// This file was generated by tools/MimeRuleGenerator from tika-mimetypes.xml of Apache Tika {TikaVersion}");
    Line($"// (https://github.com/apache/tika, commit {TikaCommit}).");
    Line("// Modified: the magic rules were converted into C# data. Do not edit this file by hand.");
    Line("//");
    Line("// Licensed under the Apache License, Version 2.0 (http://www.apache.org/licenses/LICENSE-2.0).");
    Line("// See THIRD-PARTY-NOTICES.md.");
    Line("//");
    Line($"// Unsupported rules ({unsupported.Count}):");
    foreach (var line in unsupported) Line($"// - {line}");
    Line("// <auto-generated />");
    Line();
    Line("#nullable enable");
    Line();
    Line("namespace ZeroIchi.Models.FileTypes;");
    Line();
    Line("internal static class TikaMimeRules");
    Line("{");
    Line($"    public const string Version = \"{TikaVersion}\";");
    Line();
    Line("    public static MimeTypeEntry[] Create()");
    Line("    {");
    Line($"        var types = new MimeTypeEntry[{entries.Count}];");
    var methodCount = (entries.Count + TypesPerMethod - 1) / TypesPerMethod;
    for (var i = 0; i < methodCount; i++) Line($"        Create{i}(types);");
    Line("        return types;");
    Line("    }");

    for (var method = 0; method < methodCount; method++)
    {
        Line();
        Line($"    private static void Create{method}(MimeTypeEntry[] types)");
        Line("    {");
        for (var i = method * TypesPerMethod; i < Math.Min(entries.Count, (method + 1) * TypesPerMethod); i++)
        {
            var entry = entries[i];
            var extensions = string.Join(", ", entry.Extensions.Select(Literal));
            var magics = string.Join(", ", entry.Magics.Select(m => $"new({m.Priority}, {Match(m.Clause)})"));
            Line($"        types[{i}] = new({Literal(entry.Name)}, {(entry.Parent is null ? "null" : Literal(entry.Parent))}, [{extensions}], [{magics}]);");
        }
        Line("    }");
    }

    Line();
    Line("    private static MagicMatch M(MagicMatchKind kind, int start, int end, byte[] pattern, byte[]? mask,");
    Line("        string? regex, int minShouldMatch, MagicMatch[] children)");
    Line("    {");
    Line("        if (mask is null)");
    Line("        {");
    Line("            mask = new byte[pattern.Length];");
    Line("            System.Array.Fill(mask, (byte)0xFF);");
    Line("        }");
    Line("        return new MagicMatch(kind, start, end, pattern, mask, regex, minShouldMatch, children);");
    Line("    }");
    Line("}");
    return code.ToString();
}

static string Match(MagicMatch match)
{
    var mask = match.Mask.All(b => b == 0xFF) ? "null" : Bytes(match.Mask);
    var children = string.Join(", ", match.Children.Select(Match));
    return $"M(MagicMatchKind.{match.Kind}, {match.OffsetStart}, {match.OffsetEnd}, {Bytes(match.Pattern)}, {mask}, "
           + $"{(match.Regex is null ? "null" : Literal(match.Regex))}, {match.MinShouldMatch}, [{children}])";
}

static string Bytes(byte[] bytes) => "[" + string.Join(", ", bytes.Select(b => $"0x{b:X2}")) + "]";

static string Literal(string text)
{
    var literal = new StringBuilder("\"");
    foreach (var c in text)
    {
        literal.Append(c switch
        {
            '"' => "\\\"",
            '\\' => "\\\\",
            >= ' ' and <= '~' => c.ToString(),
            _ => $"\\u{(int)c:X4}",
        });
    }
    return literal.Append('"').ToString();
}
