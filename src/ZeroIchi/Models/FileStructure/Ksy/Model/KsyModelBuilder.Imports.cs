using System.Collections.Generic;
using ZeroIchi.Models.FileStructure.Ksy.Yaml;

namespace ZeroIchi.Models.FileStructure.Ksy.Model;

public static partial class KsyModelBuilder
{
    private sealed partial class FileBuilder
    {
        private void LoadImports(YamlMapping root)
        {
            if (root.Get("meta") is not YamlMapping meta || meta.Get("imports") is not { } node) return;

            var directory = file.Contains('/') ? file[..file.LastIndexOf('/')] : "";
            var paths = ReadStringList(node, "/meta/imports");
            var items = node is YamlSequence sequence ? sequence.Items : [node];
            for (var i = 0; i < paths.Count && i < items.Count; i++)
            {
                var resolved = ResolveImportPath(directory, paths[i]);
                var itemPath = $"/meta/imports/{i}";
                if (resolved is null)
                {
                    Error(items[i], itemPath, $"Invalid import path '{paths[i]}'");
                    continue;
                }

                if (!session.TryGetFile(resolved, out var imported))
                {
                    if (!session.Imports.TryRead(resolved, out var text))
                    {
                        Error(items[i], itemPath, $"Import '{paths[i]}' not found");
                        continue;
                    }
                    session.Begin(resolved, text);
                    session.TryGetFile(resolved, out imported);
                }

                if (imported!.Format.Root is not null) _format.AddImport(imported.Format);
            }
        }

        private static string? ResolveImportPath(string directory, string import)
        {
            var parts = new List<string>();
            var relative = !import.StartsWith('/');
            if (relative && directory.Length > 0) parts.AddRange(directory.Split('/'));

            foreach (var part in import.Split('/'))
            {
                switch (part)
                {
                    case "" or ".":
                        continue;
                    case "..":
                        if (parts.Count == 0) return null;
                        parts.RemoveAt(parts.Count - 1);
                        break;
                    default:
                        parts.Add(part);
                        break;
                }
            }
            return parts.Count == 0 ? null : string.Join('/', parts);
        }
    }
}
