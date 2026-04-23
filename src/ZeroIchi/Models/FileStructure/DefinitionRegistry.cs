using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;
using ZeroIchi.Models.Buffers;

namespace ZeroIchi.Models.FileStructure;

[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase)]
[JsonSerializable(typeof(FormatDefinition))]
internal sealed partial class FormatDefinitionJsonContext : JsonSerializerContext;

public static class DefinitionRegistry
{
    private static readonly List<FormatDefinition> Definitions = LoadDefinitions();

    private static List<FormatDefinition> LoadDefinitions()
    {
        var definitions = new List<FormatDefinition>();
        var assembly = Assembly.GetExecutingAssembly();

        foreach (var resourceName in assembly.GetManifestResourceNames())
        {
            if (!resourceName.EndsWith(".json", StringComparison.OrdinalIgnoreCase))
                continue;

            using var stream = assembly.GetManifestResourceStream(resourceName);
            if (stream is null) continue;

            var definition = JsonSerializer.Deserialize(stream, FormatDefinitionJsonContext.Default.FormatDefinition);
            if (definition is not null)
                definitions.Add(definition);
        }

        return definitions;
    }

    public static FormatDefinition? TryMatch(ByteBuffer buffer)
    {
        if (buffer.Length == 0) return null;

        foreach (var definition in Definitions)
        {
            var magic = definition.MagicBytes;
            if (buffer.Length < magic.Length) continue;

            var match = true;
            for (var i = 0; i < magic.Length; i++)
            {
                if (magic[i] is { } expected && buffer.ReadByte(i) != expected)
                {
                    match = false;
                    break;
                }
            }

            if (match) return definition;
        }

        return null;
    }
}
