using System;
using System.Collections.Generic;
using System.Globalization;
using ZeroIchi.Models.FileStructure.Ksy.Yaml;

namespace ZeroIchi.Models.FileStructure.Ksy.Model;

public static partial class KsyModelBuilder
{
    private sealed partial class FileBuilder
    {
        private void ReadDetection(YamlMapping root)
        {
            if (root.Get("meta") is YamlMapping meta && meta.Get("-zeroichi-magic") is { } magicNode)
            {
                _format.Magic = magicNode is YamlScalar { Value.Kind: YamlScalarKind.String } text
                    ? ParseMagic(text.Text)
                    : null;
                if (_format.Magic is null)
                    Error(magicNode, "/meta/-zeroichi-magic", "-zeroichi-magic must be hex bytes or ?? separated by spaces");
                return;
            }

            var magic = new List<byte?>();
            foreach (var attribute in _format.Root.Seq)
            {
                if (attribute.Contents is not { } contents || attribute.If is not null
                    || attribute.Repeat != KsyRepeatKind.None || attribute.Process is not null)
                    break;
                foreach (var b in contents) magic.Add(b);
            }
            _format.Magic = magic.Count > 0 ? [.. magic] : null;
        }

        private static byte?[]? ParseMagic(string text)
        {
            var parts = text.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length == 0) return null;

            var result = new byte?[parts.Length];
            for (var i = 0; i < parts.Length; i++)
            {
                if (parts[i] == "??") continue;
                if (parts[i].Length != 2 || !byte.TryParse(parts[i], NumberStyles.AllowHexSpecifier, CultureInfo.InvariantCulture, out var b))
                    return null;
                result[i] = b;
            }
            return result;
        }
    }
}
