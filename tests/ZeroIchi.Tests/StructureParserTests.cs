using System.Text.Json;
using ZeroIchi.Models.Buffers;
using ZeroIchi.Models.FileStructure;

namespace ZeroIchi.Tests;

public class StructureParserTests
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    private static byte[] BuildMinimalWav()
    {
        var data = new List<byte>();

        // RIFF ヘッダー
        data.AddRange("RIFF"u8.ToArray());
        data.AddRange(BitConverter.GetBytes(40u));
        data.AddRange("WAVE"u8.ToArray());

        // fmt チャンク
        data.AddRange("fmt "u8.ToArray());
        data.AddRange(BitConverter.GetBytes(16u));
        data.AddRange(BitConverter.GetBytes((ushort)1));
        data.AddRange(BitConverter.GetBytes((ushort)1));
        data.AddRange(BitConverter.GetBytes(44100u));
        data.AddRange(BitConverter.GetBytes(88200u));
        data.AddRange(BitConverter.GetBytes((ushort)2));
        data.AddRange(BitConverter.GetBytes((ushort)16));

        // data チャンク
        data.AddRange("data"u8.ToArray());
        data.AddRange(BitConverter.GetBytes(4u));
        data.AddRange(new byte[4]);

        return [.. data];
    }

    private static FormatDefinition LoadWavDefinition()
    {
        const string json = """
        {
          "name": "WAV",
          "extensions": ["wav"],
          "magic": "52 49 46 46 ?? ?? ?? ?? 57 41 56 45",
          "endian": "little",
          "fields": [
            {
              "id": "riffHeader",
              "name": "RIFF Header",
              "type": "group",
              "fields": [
                { "id": "chunkId", "name": "Chunk ID", "type": "text", "size": 4 },
                { "id": "chunkSize", "name": "Chunk Size", "type": "uint32" },
                { "id": "format", "name": "Format", "type": "text", "size": 4 }
              ]
            },
            {
              "id": "subChunks",
              "name": "Sub-Chunks",
              "type": "repeat",
              "until": "eof",
              "nameTemplate": "${subChunkId}",
              "fields": [
                { "id": "subChunkId", "name": "Sub-Chunk ID", "type": "text", "size": 4 },
                { "id": "subChunkSize", "name": "Sub-Chunk Size", "type": "uint32" },
                { "id": "subChunkData", "name": "Sub-Chunk Data", "type": "bytes", "size": "subChunkSize" }
              ]
            }
          ]
        }
        """;
        return JsonSerializer.Deserialize<FormatDefinition>(json, JsonOptions)!;
    }

    [Fact]
    public void Parse_IncompleteBuffer_ReturnsPartialResult()
    {
        var wav = BuildMinimalWav();
        var truncated = wav[..14]; // 12 (RIFF ヘッダー) + 2 (サブチャンク ID の途中)
        var buffer = new ArrayByteBuffer(truncated);
        var definition = LoadWavDefinition();

        var root = StructureParser.Parse(definition, buffer);

        Assert.Equal("WAV", root.Name);
        Assert.Equal(2, root.Children.Count);

        var riffHeader = root.Children[0];
        Assert.Equal(12, riffHeader.Length);

        var subChunks = root.Children[1];
        Assert.True(subChunks.Children.Count >= 1);
    }

    [Fact]
    public void Parse_TruncatedNumericField_ReturnsNodeWithoutValue()
    {
        const string json = """
        {
          "name": "Truncated",
          "extensions": ["bin"],
          "magic": "12",
          "endian": "big",
          "fields": [
            { "id": "value", "name": "Value", "type": "uint16" }
          ]
        }
        """;
        var definition = JsonSerializer.Deserialize<FormatDefinition>(json, JsonOptions)!;
        var buffer = new ArrayByteBuffer([0x12]);

        var root = StructureParser.Parse(definition, buffer);

        var value = root.Children[0];
        Assert.Equal("Value", value.Name);
        Assert.Equal(1, value.Length);
        Assert.Equal("", value.Description);
    }

    [Fact]
    public void Parse_RestSize_ReadsRemainingBytes()
    {
        var wav = BuildMinimalWav();
        var buffer = new ArrayByteBuffer(wav);

        const string json = """
        {
          "name": "WAV",
          "extensions": ["wav"],
          "magic": "52 49 46 46 ?? ?? ?? ?? 57 41 56 45",
          "endian": "little",
          "fields": [
            {
              "id": "riffHeader",
              "name": "RIFF Header",
              "type": "group",
              "fields": [
                { "id": "chunkId", "name": "Chunk ID", "type": "text", "size": 4 },
                { "id": "fileSize", "name": "File Size", "type": "uint32" },
                { "id": "format", "name": "Format", "type": "text", "size": 4 }
              ]
            },
            {
              "id": "data",
              "name": "Data",
              "type": "bytes",
              "size": "rest"
            }
          ]
        }
        """;
        var definition = JsonSerializer.Deserialize<FormatDefinition>(json, JsonOptions)!;

        var root = StructureParser.Parse(definition, buffer);
        var data = root.Children[1];

        Assert.Equal("Data", data.Name);
        // RIFF ヘッダー (12 バイト) の直後から開始
        Assert.Equal(12, data.Offset);
        // 残りの全バイト
        Assert.Equal(wav.Length - 12, data.Length);
    }

    [Fact]
    public void Parse_ValidWav_ReturnsCorrectTreeStructure()
    {
        var wav = BuildMinimalWav();
        var buffer = new ArrayByteBuffer(wav);
        var definition = LoadWavDefinition();

        var root = StructureParser.Parse(definition, buffer);

        Assert.Equal("WAV", root.Name);
        Assert.Equal(0, root.Offset);
        Assert.Equal(wav.Length, root.Length);
        Assert.Equal(2, root.Children.Count); // RIFF ヘッダー + サブチャンク
    }

    [Fact]
    public void Parse_ValidWav_RiffHeader()
    {
        var wav = BuildMinimalWav();
        var buffer = new ArrayByteBuffer(wav);
        var definition = LoadWavDefinition();

        var root = StructureParser.Parse(definition, buffer);
        var riffHeader = root.Children[0];

        Assert.Equal("RIFF Header", riffHeader.Name);
        Assert.Equal(0, riffHeader.Offset);
        Assert.Equal(12, riffHeader.Length);

        Assert.Equal("RIFF", riffHeader.Children[0].Description);
        Assert.Equal("40", riffHeader.Children[1].Description);
        Assert.Equal("WAVE", riffHeader.Children[2].Description);
    }

    [Fact]
    public void Parse_ValidWav_SubChunkNameTemplate()
    {
        var wav = BuildMinimalWav();
        var buffer = new ArrayByteBuffer(wav);
        var definition = LoadWavDefinition();

        var root = StructureParser.Parse(definition, buffer);
        var subChunks = root.Children[1];

        Assert.Equal("Sub-Chunks", subChunks.Name);
        Assert.Equal(2, subChunks.Children.Count);
        Assert.Equal("fmt ", subChunks.Children[0].Name);
        Assert.Equal("data", subChunks.Children[1].Name);
    }

    [Fact]
    public void Parse_ValidWav_SubChunkData()
    {
        var wav = BuildMinimalWav();
        var buffer = new ArrayByteBuffer(wav);
        var definition = LoadWavDefinition();

        var root = StructureParser.Parse(definition, buffer);
        var fmtChunk = root.Children[1].Children[0];

        Assert.Equal(12, fmtChunk.Offset);
        Assert.Equal(24, fmtChunk.Length); // 4 (ID) + 4 (Size) + 16 (Data)

        var subChunkData = fmtChunk.Children[2];
        Assert.Equal("Sub-Chunk Data", subChunkData.Name);
        Assert.Equal(16, subChunkData.Length);
    }

    [Fact]
    public void Parse_SizeExpressionWithSubtraction_ExcludesLengthFieldItself()
    {
        const string json = """
        {
          "name": "SelfInclusiveLength",
          "extensions": ["bin"],
          "magic": "00",
          "endian": "big",
          "fields": [
            { "id": "length", "name": "Length", "type": "uint16" },
            { "id": "payload", "name": "Payload", "type": "bytes", "size": "length-2" },
            { "id": "trailer", "name": "Trailer", "type": "uint8" }
          ]
        }
        """;
        var definition = JsonSerializer.Deserialize<FormatDefinition>(json, JsonOptions)!;
        var buffer = new ArrayByteBuffer([0x00, 0x05, 0x11, 0x22, 0x33, 0x99]);

        var root = StructureParser.Parse(definition, buffer);

        var payload = root.Children[1];
        Assert.Equal("Payload", payload.Name);
        Assert.Equal(2, payload.Offset);
        Assert.Equal(3, payload.Length);

        var trailer = root.Children[2];
        Assert.Equal(5, trailer.Offset);
        Assert.Equal("153", trailer.Description);
    }

    [Fact]
    public void Parse_SizeExpressionWithNegativeResult_ClampsToZero()
    {
        const string json = """
        {
          "name": "ShortLength",
          "extensions": ["bin"],
          "magic": "00",
          "endian": "big",
          "fields": [
            { "id": "length", "name": "Length", "type": "uint16" },
            { "id": "payload", "name": "Payload", "type": "bytes", "size": "length-2" },
            { "id": "trailer", "name": "Trailer", "type": "uint8" }
          ]
        }
        """;
        var definition = JsonSerializer.Deserialize<FormatDefinition>(json, JsonOptions)!;
        var buffer = new ArrayByteBuffer([0x00, 0x01, 0x99]);

        var root = StructureParser.Parse(definition, buffer);

        Assert.Equal(2, root.Children.Count);
        Assert.Equal("Trailer", root.Children[1].Name);
        Assert.Equal(2, root.Children[1].Offset);
    }

    [Fact]
    public void Parse_SizeReferenceWithHyphen_TreatedAsIdentifier()
    {
        const string json = """
        {
          "name": "HyphenId",
          "extensions": ["bin"],
          "magic": "00",
          "endian": "big",
          "fields": [
            { "id": "data-size", "name": "Data Size", "type": "uint8" },
            { "id": "data", "name": "Data", "type": "bytes", "size": "data-size" }
          ]
        }
        """;
        var definition = JsonSerializer.Deserialize<FormatDefinition>(json, JsonOptions)!;
        var buffer = new ArrayByteBuffer([0x03, 0x11, 0x22, 0x33]);

        var root = StructureParser.Parse(definition, buffer);

        var data = root.Children[1];
        Assert.Equal(1, data.Offset);
        Assert.Equal(3, data.Length);
    }

    [Fact]
    public void Parse_SizeReferenceEndingInSignedInteger_TreatedAsArithmetic()
    {
        const string json = """
        {
          "name": "AmbiguousId",
          "extensions": ["bin"],
          "magic": "00",
          "endian": "big",
          "fields": [
            { "id": "part", "name": "Part", "type": "uint8" },
            { "id": "data", "name": "Data", "type": "bytes", "size": "part-2" }
          ]
        }
        """;
        var definition = JsonSerializer.Deserialize<FormatDefinition>(json, JsonOptions)!;
        var buffer = new ArrayByteBuffer([0x05, 0x11, 0x22, 0x33, 0x44]);

        var root = StructureParser.Parse(definition, buffer);

        var data = root.Children[1];
        Assert.Equal(1, data.Offset);
        Assert.Equal(3, data.Length);
    }

    [Fact]
    public void Parse_SizeExpressionWithAddition_ExtendsReferencedValue()
    {
        const string json = """
        {
          "name": "PaddedLength",
          "extensions": ["bin"],
          "magic": "00",
          "endian": "big",
          "fields": [
            { "id": "count", "name": "Count", "type": "uint8" },
            { "id": "payload", "name": "Payload", "type": "bytes", "size": "count+1" },
            { "id": "trailer", "name": "Trailer", "type": "uint8" }
          ]
        }
        """;
        var definition = JsonSerializer.Deserialize<FormatDefinition>(json, JsonOptions)!;
        var buffer = new ArrayByteBuffer([0x02, 0x11, 0x22, 0x33, 0x99]);

        var root = StructureParser.Parse(definition, buffer);

        var payload = root.Children[1];
        Assert.Equal(1, payload.Offset);
        Assert.Equal(3, payload.Length);

        var trailer = root.Children[2];
        Assert.Equal(4, trailer.Offset);
        Assert.Equal("153", trailer.Description);
    }

    [Fact]
    public void Parse_SizeExpressionWithExtremeValue_DoesNotOverflow()
    {
        const string json = """
        {
          "name": "HugeLength",
          "extensions": ["bin"],
          "magic": "7F",
          "endian": "big",
          "fields": [
            { "id": "length", "name": "Length", "type": "uint64" },
            { "id": "payload", "name": "Payload", "type": "bytes", "size": "length+1" }
          ]
        }
        """;
        var definition = JsonSerializer.Deserialize<FormatDefinition>(json, JsonOptions)!;
        var buffer = new ArrayByteBuffer(
            [0x7F, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0x11, 0x22]);

        var root = StructureParser.Parse(definition, buffer);

        var payload = root.Children[1];
        Assert.Equal(8, payload.Offset);
        Assert.Equal(2, payload.Length);
    }

    [Fact]
    public void Parse_MatchedCaseWithoutFields_KeepsNamedNode()
    {
        const string json = """
        {
          "name": "Tagged",
          "extensions": ["bin"],
          "magic": "00",
          "endian": "big",
          "fields": [
            {
              "id": "items",
              "name": "Items",
              "type": "repeat",
              "until": "eof",
              "nameTemplate": "${body}",
              "fields": [
                { "id": "tag", "name": "Tag", "type": "uint8" },
                {
                  "id": "body",
                  "name": "Body",
                  "type": "match",
                  "on": "tag",
                  "cases": [
                    { "name": "Empty", "range": [1, 1] },
                    {
                      "name": "Payload",
                      "range": [2, 2],
                      "fields": [
                        { "id": "data", "name": "Data", "type": "bytes", "size": 2 }
                      ]
                    }
                  ]
                }
              ]
            }
          ]
        }
        """;
        var definition = JsonSerializer.Deserialize<FormatDefinition>(json, JsonOptions)!;
        var buffer = new ArrayByteBuffer([0x01, 0x02, 0xAA, 0xBB, 0x01]);

        var root = StructureParser.Parse(definition, buffer);
        var items = root.Children[0];

        Assert.Equal(3, items.Children.Count);
        Assert.Equal("Empty", items.Children[0].Name);
        Assert.Equal(1, items.Children[0].Length);
        Assert.Equal("Payload", items.Children[1].Name);
        Assert.Equal("Empty", items.Children[2].Name);
    }

    [Fact]
    public void Parse_UnmatchedCase_DropsNode()
    {
        const string json = """
        {
          "name": "Tagged",
          "extensions": ["bin"],
          "magic": "00",
          "endian": "big",
          "fields": [
            { "id": "tag", "name": "Tag", "type": "uint8" },
            {
              "id": "body",
              "name": "Body",
              "type": "match",
              "on": "tag",
              "cases": [
                { "name": "Known", "range": [1, 1] }
              ]
            }
          ]
        }
        """;
        var definition = JsonSerializer.Deserialize<FormatDefinition>(json, JsonOptions)!;
        var buffer = new ArrayByteBuffer([0x09]);

        var root = StructureParser.Parse(definition, buffer);

        Assert.Single(root.Children);
        Assert.Equal("Tag", root.Children[0].Name);
    }

    [Fact]
    public void Parse_MatchedCaseWithUnreadableBody_KeepsNamedNode()
    {
        const string json = """
        {
          "name": "Tagged",
          "extensions": ["bin"],
          "magic": "00",
          "endian": "big",
          "fields": [
            { "id": "tag", "name": "Tag", "type": "uint8" },
            {
              "id": "body",
              "name": "Body",
              "type": "match",
              "on": "tag",
              "cases": [
                {
                  "name": "Payload",
                  "range": [2, 2],
                  "fields": [
                    { "id": "data", "name": "Data", "type": "bytes", "size": 2 }
                  ]
                }
              ]
            }
          ]
        }
        """;
        var definition = JsonSerializer.Deserialize<FormatDefinition>(json, JsonOptions)!;
        var buffer = new ArrayByteBuffer([0x02]);

        var root = StructureParser.Parse(definition, buffer);

        Assert.Equal(2, root.Children.Count);
        Assert.Equal("Payload", root.Children[1].Name);
        Assert.Equal(0, root.Children[1].Length);
    }

    private static FormatDefinition LoadScanDefinition()
    {
        const string json = """
        {
          "name": "Scanned",
          "extensions": ["bin"],
          "magic": "00",
          "endian": "big",
          "fields": [
            { "id": "header", "name": "Header", "type": "uint8" },
            { "id": "body", "name": "Body", "type": "scan" },
            { "id": "marker", "name": "Marker", "type": "uint16" }
          ]
        }
        """;
        return JsonSerializer.Deserialize<FormatDefinition>(json, JsonOptions)!;
    }

    [Fact]
    public void Parse_Scan_CrossesStuffedBytesAndStopsAtMarker()
    {
        var buffer = new ArrayByteBuffer([0x00, 0x11, 0xFF, 0x00, 0x22, 0xFF, 0xD9]);

        var root = StructureParser.Parse(LoadScanDefinition(), buffer);

        var body = root.Children[1];
        Assert.Equal("Body", body.Name);
        Assert.Equal(1, body.Offset);
        Assert.Equal(4, body.Length);
        Assert.Equal(ValueKind.Bytes, body.ValueKind);

        var marker = root.Children[2];
        Assert.Equal(5, marker.Offset);
        Assert.Equal("65497", marker.Description);
    }

    [Fact]
    public void Parse_Scan_StopsAtRestartMarker()
    {
        var buffer = new ArrayByteBuffer([0x00, 0x11, 0x22, 0xFF, 0xD0]);

        var root = StructureParser.Parse(LoadScanDefinition(), buffer);

        Assert.Equal(2, root.Children[1].Length);
    }

    [Fact]
    public void Parse_Scan_CrossesReservedMarkerCode()
    {
        var buffer = new ArrayByteBuffer([0x00, 0x11, 0xFF, 0x3A, 0x22, 0xFF, 0xD9]);

        var root = StructureParser.Parse(LoadScanDefinition(), buffer);

        Assert.Equal(4, root.Children[1].Length);

        var marker = root.Children[2];
        Assert.Equal(5, marker.Offset);
        Assert.Equal("65497", marker.Description);
    }

    [Fact]
    public void Parse_Scan_WithOneFillByte_LeavesLastByteForMarker()
    {
        var buffer = new ArrayByteBuffer([0x00, 0x11, 0xFF, 0xFF, 0xD9]);

        var root = StructureParser.Parse(LoadScanDefinition(), buffer);

        Assert.Equal(2, root.Children[1].Length);

        var marker = root.Children[2];
        Assert.Equal(3, marker.Offset);
        Assert.Equal("65497", marker.Description);
    }

    [Fact]
    public void Parse_Scan_WithTwoFillBytes_LeavesLastByteForMarker()
    {
        var buffer = new ArrayByteBuffer([0x00, 0x11, 0xFF, 0xFF, 0xFF, 0xD9]);

        var root = StructureParser.Parse(LoadScanDefinition(), buffer);

        Assert.Equal(3, root.Children[1].Length);

        var marker = root.Children[2];
        Assert.Equal(4, marker.Offset);
        Assert.Equal("65497", marker.Description);
    }

    [Fact]
    public void Parse_Scan_WithoutMarker_ConsumesRemainingBytes()
    {
        var buffer = new ArrayByteBuffer([0x00, 0x11, 0x22, 0x33, 0xFF]);

        var root = StructureParser.Parse(LoadScanDefinition(), buffer);

        var body = root.Children[1];
        Assert.Equal(1, body.Offset);
        Assert.Equal(4, body.Length);

        Assert.Equal(2, root.Children.Count);
    }

    [Fact]
    public void Parse_UnresolvedNameTemplate_FallsBackToDefaultName()
    {
        const string json = """
        {
          "name": "Tagged",
          "extensions": ["bin"],
          "magic": "00",
          "endian": "big",
          "fields": [
            {
              "id": "items",
              "name": "Items",
              "type": "repeat",
              "until": "eof",
              "nameTemplate": "${body}",
              "fields": [
                { "id": "tag", "name": "Tag", "type": "uint8" },
                {
                  "id": "body",
                  "name": "Body",
                  "type": "match",
                  "on": "tag",
                  "cases": [
                    { "name": "Known", "range": [1, 1] }
                  ]
                }
              ]
            }
          ]
        }
        """;
        var definition = JsonSerializer.Deserialize<FormatDefinition>(json, JsonOptions)!;
        var buffer = new ArrayByteBuffer([0x01, 0x09]);

        var root = StructureParser.Parse(definition, buffer);
        var items = root.Children[0];

        Assert.Equal("Known", items.Children[0].Name);
        Assert.Equal("Items[1]", items.Children[1].Name);
    }

    [Fact]
    public void DefinitionRegistry_TryMatch_MatchesWav()
    {
        var wav = new List<byte>();
        wav.AddRange("RIFF"u8.ToArray());
        wav.AddRange(BitConverter.GetBytes(40u));
        wav.AddRange("WAVE"u8.ToArray());
        wav.AddRange("fmt "u8.ToArray());
        wav.AddRange(BitConverter.GetBytes(16u));
        wav.AddRange(new byte[16]);
        var buffer = new ArrayByteBuffer([.. wav]);

        var definition = DefinitionRegistry.TryMatch(buffer);

        Assert.NotNull(definition);
        Assert.Equal("WAV", definition.Name);
    }

    [Fact]
    public void DefinitionRegistry_TryMatch_WebPNotMatchedAsWav()
    {
        var webp = new List<byte>();
        webp.AddRange("RIFF"u8.ToArray());
        webp.AddRange(BitConverter.GetBytes(24u));
        webp.AddRange("WEBP"u8.ToArray());
        webp.AddRange("VP8L"u8.ToArray());
        webp.AddRange(BitConverter.GetBytes(12u));
        webp.AddRange(new byte[12]);
        var buffer = new ArrayByteBuffer([.. webp]);

        var definition = DefinitionRegistry.TryMatch(buffer);

        Assert.NotNull(definition);
        Assert.Equal("WebP", definition.Name);
    }

    [Fact]
    public void DefinitionRegistry_TryMatch_NoMatchReturnsNull()
    {
        var data = new byte[] { 0x00, 0x01, 0x02, 0x03 };
        var buffer = new ArrayByteBuffer(data);

        var definition = DefinitionRegistry.TryMatch(buffer);

        Assert.Null(definition);
    }

    [Fact]
    public void DefinitionRegistry_TryMatch_EmptyBufferReturnsNull()
    {
        var buffer = new ArrayByteBuffer([]);

        var definition = DefinitionRegistry.TryMatch(buffer);

        Assert.Null(definition);
    }
}
