using System.Text.Json;
using ZeroIchi.Models.Buffers;
using ZeroIchi.Models.FileStructure;

namespace ZeroIchi.Tests;

public sealed class StructureParserTests
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    private static byte[] BuildMinimalWav()
    {
        var data = new List<byte>();

        // RIFF header
        data.AddRange("RIFF"u8.ToArray());
        data.AddRange(BitConverter.GetBytes(40u));
        data.AddRange("WAVE"u8.ToArray());

        // fmt chunk
        data.AddRange("fmt "u8.ToArray());
        data.AddRange(BitConverter.GetBytes(16u));
        data.AddRange(BitConverter.GetBytes((ushort)1));
        data.AddRange(BitConverter.GetBytes((ushort)1));
        data.AddRange(BitConverter.GetBytes(44100u));
        data.AddRange(BitConverter.GetBytes(88200u));
        data.AddRange(BitConverter.GetBytes((ushort)2));
        data.AddRange(BitConverter.GetBytes((ushort)16));

        // data chunk
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

    private static byte[] BuildMinimalJpeg()
    {
        var data = new List<byte>();

        // SOI
        data.AddRange([0xFF, 0xD8]);

        // APP0 (JFIF)
        data.AddRange([0xFF, 0xE0, 0x00, 0x10]);
        data.AddRange("JFIF\0"u8.ToArray());
        data.AddRange([0x01, 0x01, 0x00, 0x00, 0x01, 0x00, 0x01, 0x00, 0x00]);

        // DQT
        data.AddRange([0xFF, 0xDB, 0x00, 0x43, 0x00]);
        data.AddRange(new byte[64]);

        // SOF0
        data.AddRange([0xFF, 0xC0, 0x00, 0x11, 0x08]);
        data.AddRange([0x00, 0x40]);
        data.AddRange([0x00, 0x60]);
        data.Add(0x03);
        data.AddRange(new byte[9]);

        // DHT
        data.AddRange([0xFF, 0xC4, 0x00, 0x1F, 0x00]);
        data.AddRange([0x00, 0x01, 0x05, 0x01, 0x01, 0x01, 0x01, 0x01, 0x01, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00]);
        data.AddRange([0x00, 0x01, 0x02, 0x03, 0x04, 0x05, 0x06, 0x07, 0x08, 0x09, 0x0A, 0x0B]);

        // SOS
        data.AddRange([0xFF, 0xDA, 0x00, 0x0C, 0x03]);
        data.AddRange(new byte[6]);
        data.AddRange([0x00, 0x3F, 0x00]);

        // Scan data
        data.AddRange([0x11, 0x22, 0xFF, 0x00, 0x33]);

        // EOI
        data.AddRange([0xFF, 0xD9]);

        return [.. data];
    }

    private static byte[] BuildJpegWithRestartMarker()
    {
        var data = new List<byte>();

        data.AddRange([0xFF, 0xD8]);
        data.AddRange([0xFF, 0xDA, 0x00, 0x0C, 0x03]);
        data.AddRange(new byte[6]);
        data.AddRange([0x00, 0x3F, 0x00]);
        data.AddRange([0xAA, 0xBB]);
        data.AddRange([0xFF, 0xD0]);
        data.Add(0xCC);
        data.AddRange([0xFF, 0xD9]);

        return [.. data];
    }

    private static byte[] BuildJpegWithFillBytes(int fillByteCount)
    {
        var data = new List<byte>();

        data.AddRange([0xFF, 0xD8]);

        data.AddRange([0xFF, 0xE0, 0x00, 0x10]);
        data.AddRange("JFIF\0"u8.ToArray());
        data.AddRange([0x01, 0x01, 0x00, 0x00, 0x01, 0x00, 0x01, 0x00, 0x00]);

        for (var i = 0; i < fillByteCount; i++)
            data.Add(0xFF);

        data.AddRange([0xFF, 0xD9]);

        return [.. data];
    }

    private static byte[] BuildJpegWithShortApp0()
    {
        var data = new List<byte>();

        data.AddRange([0xFF, 0xD8]);

        // APP0 shorter than the fixed JFIF fields
        data.AddRange([0xFF, 0xE0, 0x00, 0x0D]);
        data.AddRange("JFIF\0"u8.ToArray());
        data.AddRange([0x01, 0x01, 0x00, 0x00, 0x01, 0x00]);

        data.AddRange([0xFF, 0xD9]);

        return [.. data];
    }

    private static byte[] BuildJpegWithReservedMarker()
    {
        var data = new List<byte>();

        data.AddRange([0xFF, 0xD8]);

        data.AddRange([0xFF, 0xE0, 0x00, 0x10]);
        data.AddRange("JFIF\0"u8.ToArray());
        data.AddRange([0x01, 0x01, 0x00, 0x00, 0x01, 0x00, 0x01, 0x00, 0x00]);

        data.AddRange([0xFF, 0x50]);

        data.AddRange([0xFF, 0xD9]);

        return [.. data];
    }

    private static byte[] BuildJpegWithTrailingSegment()
    {
        var data = new List<byte>(BuildMinimalJpeg());

        data.AddRange([0xFF, 0xFE, 0x00, 0x04]);
        data.AddRange("AB"u8.ToArray());

        return [.. data];
    }

    private static FormatDefinition LoadJpegDefinition()
    {
        var buffer = new ArrayByteBuffer(BuildMinimalJpeg());
        return DefinitionRegistry.TryMatch(buffer)!;
    }

    private static FileStructureNode SegmentBody(FileStructureNode segment) => segment.Children[^1];

    [Fact]
    public void Parse_IncompleteBuffer_ReturnsPartialResult()
    {
        var wav = BuildMinimalWav();
        var truncated = wav[..14]; // 12 (RIFF header) + 2 (partway into the subchunk ID)
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
        // Starts right after the RIFF header (12 bytes)
        Assert.Equal(12, data.Offset);
        // All remaining bytes
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
        Assert.Equal(2, root.Children.Count); // RIFF header + subchunk
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

    private static FormatDefinition LoadBoundedGroupDefinition()
    {
        const string json = """
        {
          "name": "Bounded",
          "extensions": ["bin"],
          "magic": "00",
          "endian": "big",
          "fields": [
            { "id": "length", "name": "Length", "type": "uint8" },
            {
              "id": "body",
              "name": "Body",
              "type": "group",
              "size": "length",
              "fields": [
                { "id": "first", "name": "First", "type": "uint16" },
                { "id": "second", "name": "Second", "type": "uint16" }
              ]
            },
            { "id": "trailer", "name": "Trailer", "type": "uint8" }
          ]
        }
        """;
        return JsonSerializer.Deserialize<FormatDefinition>(json, JsonOptions)!;
    }

    [Fact]
    public void Parse_BoundedGroup_StopsChildrenAtRegionEnd()
    {
        var buffer = new ArrayByteBuffer([0x03, 0x11, 0x22, 0x33, 0x99]);

        var root = StructureParser.Parse(LoadBoundedGroupDefinition(), buffer);
        var body = root.Children[1];

        Assert.Equal(1, body.Offset);
        Assert.Equal(3, body.Length);
        Assert.Equal(2, body.Children[0].Length);
        Assert.Equal(1, body.Children[1].Length);

        var trailer = root.Children[2];
        Assert.Equal(4, trailer.Offset);
        Assert.Equal("153", trailer.Description);
    }

    [Fact]
    public void Parse_BoundedGroup_ConsumesTrailingPadding()
    {
        var buffer = new ArrayByteBuffer([0x06, 0x11, 0x22, 0x33, 0x44, 0x00, 0x00, 0x99]);

        var root = StructureParser.Parse(LoadBoundedGroupDefinition(), buffer);
        var body = root.Children[1];

        Assert.Equal(6, body.Length);
        Assert.Equal(2, body.Children.Count);

        var trailer = root.Children[2];
        Assert.Equal(7, trailer.Offset);
        Assert.Equal("153", trailer.Description);
    }

    [Fact]
    public void Parse_BoundedGroupBeyondBufferEnd_StopsAtBufferEnd()
    {
        var buffer = new ArrayByteBuffer([0x08, 0x11, 0x22, 0x33]);

        var root = StructureParser.Parse(LoadBoundedGroupDefinition(), buffer);
        var body = root.Children[1];

        Assert.Equal(1, body.Offset);
        Assert.Equal(3, body.Length);
        Assert.Equal(2, root.Children.Count);
    }

    [Fact]
    public void Parse_RestInsideBoundedGroup_ResolvesToRegionRemainder()
    {
        const string json = """
        {
          "name": "BoundedRest",
          "extensions": ["bin"],
          "magic": "00",
          "endian": "big",
          "fields": [
            { "id": "length", "name": "Length", "type": "uint8" },
            {
              "id": "body",
              "name": "Body",
              "type": "group",
              "size": "length",
              "fields": [
                { "id": "head", "name": "Head", "type": "uint8" },
                { "id": "filler", "name": "Filler", "type": "bytes", "size": "rest" }
              ]
            },
            { "id": "trailer", "name": "Trailer", "type": "uint8" }
          ]
        }
        """;
        var definition = JsonSerializer.Deserialize<FormatDefinition>(json, JsonOptions)!;
        var buffer = new ArrayByteBuffer([0x04, 0x11, 0x22, 0x33, 0x44, 0x99]);

        var root = StructureParser.Parse(definition, buffer);
        var filler = root.Children[1].Children[1];

        Assert.Equal(2, filler.Offset);
        Assert.Equal(3, filler.Length);
        Assert.Equal(5, root.Children[2].Offset);
    }

    [Fact]
    public void Parse_RestWithAdjustmentInsideBoundedGroup_LeavesRoomForLaterFields()
    {
        const string json = """
        {
          "name": "BoundedRestAdjusted",
          "extensions": ["bin"],
          "magic": "00",
          "endian": "big",
          "fields": [
            { "id": "length", "name": "Length", "type": "uint8" },
            {
              "id": "body",
              "name": "Body",
              "type": "group",
              "size": "length",
              "fields": [
                { "id": "head", "name": "Head", "type": "uint8" },
                { "id": "filler", "name": "Filler", "type": "bytes", "size": "rest-2" },
                { "id": "tailA", "name": "Tail A", "type": "uint8" },
                { "id": "tailB", "name": "Tail B", "type": "uint8" }
              ]
            }
          ]
        }
        """;
        var definition = JsonSerializer.Deserialize<FormatDefinition>(json, JsonOptions)!;
        var buffer = new ArrayByteBuffer([0x05, 0x11, 0x22, 0x33, 0xAA, 0xBB]);

        var root = StructureParser.Parse(definition, buffer);
        var body = root.Children[1];

        Assert.Equal(2, body.Children[1].Length);
        Assert.Equal("170", body.Children[2].Description);
        Assert.Equal("187", body.Children[3].Description);
    }

    [Fact]
    public void Parse_NestedGroupInsideBoundedGroup_StopsAtRegionEnd()
    {
        const string json = """
        {
          "name": "NestedGroup",
          "extensions": ["bin"],
          "magic": "00",
          "endian": "big",
          "fields": [
            { "id": "length", "name": "Length", "type": "uint8" },
            {
              "id": "body",
              "name": "Body",
              "type": "group",
              "size": "length",
              "fields": [
                {
                  "id": "inner",
                  "name": "Inner",
                  "type": "group",
                  "fields": [
                    { "id": "a", "name": "A", "type": "uint16" },
                    { "id": "b", "name": "B", "type": "uint16" },
                    { "id": "c", "name": "C", "type": "uint16" }
                  ]
                }
              ]
            },
            { "id": "trailer", "name": "Trailer", "type": "uint8" }
          ]
        }
        """;
        var definition = JsonSerializer.Deserialize<FormatDefinition>(json, JsonOptions)!;
        var buffer = new ArrayByteBuffer([0x04, 0x11, 0x11, 0x22, 0x22, 0x99]);

        var root = StructureParser.Parse(definition, buffer);
        var inner = root.Children[1].Children[0];

        Assert.Equal(4, inner.Length);
        Assert.Equal(5, root.Children[2].Offset);
        Assert.Equal("153", root.Children[2].Description);
    }

    [Fact]
    public void Parse_RepeatInsideBoundedGroup_StopsAtRegionEnd()
    {
        const string json = """
        {
          "name": "NestedRepeat",
          "extensions": ["bin"],
          "magic": "00",
          "endian": "big",
          "fields": [
            { "id": "length", "name": "Length", "type": "uint8" },
            {
              "id": "body",
              "name": "Body",
              "type": "group",
              "size": "length",
              "fields": [
                {
                  "id": "items",
                  "name": "Items",
                  "type": "repeat",
                  "until": "eof",
                  "fields": [
                    { "id": "value", "name": "Value", "type": "uint16" }
                  ]
                }
              ]
            },
            { "id": "trailer", "name": "Trailer", "type": "uint8" }
          ]
        }
        """;
        var definition = JsonSerializer.Deserialize<FormatDefinition>(json, JsonOptions)!;
        var buffer = new ArrayByteBuffer([0x04, 0x11, 0x11, 0x22, 0x22, 0x99, 0x88]);

        var root = StructureParser.Parse(definition, buffer);
        var items = root.Children[1].Children[0];

        Assert.Equal(2, items.Children.Count);
        Assert.Equal(4, items.Length);
        Assert.Equal(5, root.Children[2].Offset);
        Assert.Equal("153", root.Children[2].Description);
    }

    [Fact]
    public void Parse_MatchInsideBoundedGroup_StopsAtRegionEnd()
    {
        const string json = """
        {
          "name": "NestedMatch",
          "extensions": ["bin"],
          "magic": "00",
          "endian": "big",
          "fields": [
            { "id": "length", "name": "Length", "type": "uint8" },
            {
              "id": "body",
              "name": "Body",
              "type": "group",
              "size": "length",
              "fields": [
                { "id": "tag", "name": "Tag", "type": "uint8" },
                {
                  "id": "content",
                  "name": "Content",
                  "type": "match",
                  "on": "tag",
                  "cases": [
                    {
                      "name": "Big",
                      "range": [1, 1],
                      "fields": [
                        { "id": "data", "name": "Data", "type": "bytes", "size": 4 }
                      ]
                    }
                  ]
                }
              ]
            },
            { "id": "trailer", "name": "Trailer", "type": "uint8" }
          ]
        }
        """;
        var definition = JsonSerializer.Deserialize<FormatDefinition>(json, JsonOptions)!;
        var buffer = new ArrayByteBuffer([0x03, 0x01, 0xAA, 0xBB, 0x99]);

        var root = StructureParser.Parse(definition, buffer);
        var data = root.Children[1].Children[1].Children[0];

        Assert.Equal(2, data.Offset);
        Assert.Equal(2, data.Length);
        Assert.Equal(4, root.Children[2].Offset);
        Assert.Equal("153", root.Children[2].Description);
    }

    [Fact]
    public void Parse_ScanInsideBoundedGroup_StopsAtRegionEnd()
    {
        const string json = """
        {
          "name": "NestedScan",
          "extensions": ["bin"],
          "magic": "00",
          "endian": "big",
          "fields": [
            { "id": "length", "name": "Length", "type": "uint8" },
            {
              "id": "body",
              "name": "Body",
              "type": "group",
              "size": "length",
              "fields": [
                { "id": "data", "name": "Data", "type": "scan" }
              ]
            },
            { "id": "trailer", "name": "Trailer", "type": "uint8" }
          ]
        }
        """;
        var definition = JsonSerializer.Deserialize<FormatDefinition>(json, JsonOptions)!;
        var buffer = new ArrayByteBuffer([0x03, 0x11, 0x22, 0x33, 0x44, 0xFF, 0xD9]);

        var root = StructureParser.Parse(definition, buffer);
        var data = root.Children[1].Children[0];

        Assert.Equal(1, data.Offset);
        Assert.Equal(3, data.Length);
        Assert.Equal(4, root.Children[2].Offset);
        Assert.Equal("68", root.Children[2].Description);
    }

    [Fact]
    public void Parse_GroupWithoutSize_ReadsWithoutLimit()
    {
        const string json = """
        {
          "name": "Unbounded",
          "extensions": ["bin"],
          "magic": "00",
          "endian": "big",
          "fields": [
            {
              "id": "body",
              "name": "Body",
              "type": "group",
              "fields": [
                { "id": "first", "name": "First", "type": "uint16" },
                { "id": "second", "name": "Second", "type": "uint16" }
              ]
            }
          ]
        }
        """;
        var definition = JsonSerializer.Deserialize<FormatDefinition>(json, JsonOptions)!;
        var buffer = new ArrayByteBuffer([0x00, 0x11, 0x22, 0x33]);

        var root = StructureParser.Parse(definition, buffer);

        Assert.Equal(4, root.Children[0].Length);
    }

    [Fact]
    public void Parse_MinimalJpeg_ListsSegmentsInOrder()
    {
        var buffer = new ArrayByteBuffer(BuildMinimalJpeg());

        var root = StructureParser.Parse(LoadJpegDefinition(), buffer);
        var segments = root.Children[1];

        Assert.Equal("SOI", root.Children[0].Name);
        Assert.Equal(
            ["APP0 (JFIF)", "DQT", "SOF0 (Baseline DCT)", "DHT", "SOS", "EOI"],
            segments.Children.Select(c => c.Name));
    }

    [Fact]
    public void Parse_MinimalJpeg_SegmentLengthExcludesNextMarker()
    {
        var buffer = new ArrayByteBuffer(BuildMinimalJpeg());

        var root = StructureParser.Parse(LoadJpegDefinition(), buffer);
        var segments = root.Children[1];

        var app0 = segments.Children[0];
        Assert.Equal(2, app0.Offset);
        Assert.Equal(18, app0.Length);

        var dqt = segments.Children[1];
        Assert.Equal(20, dqt.Offset);
        Assert.Equal(69, dqt.Length);
    }

    [Fact]
    public void Parse_MinimalJpeg_DecodesFrameHeader()
    {
        var buffer = new ArrayByteBuffer(BuildMinimalJpeg());

        var root = StructureParser.Parse(LoadJpegDefinition(), buffer);
        var header = SegmentBody(root.Children[1].Children[2]).Children[1];

        Assert.Equal("Frame Header", header.Name);
        Assert.Equal("8", header.Children[0].Description);
        Assert.Equal("64", header.Children[1].Description);
        Assert.Equal("96", header.Children[2].Description);
        Assert.Equal("3", header.Children[3].Description);
        Assert.Equal(9, header.Children[4].Length);
    }

    [Fact]
    public void Parse_MinimalJpeg_DecodesJfifIdentifier()
    {
        var buffer = new ArrayByteBuffer(BuildMinimalJpeg());

        var root = StructureParser.Parse(LoadJpegDefinition(), buffer);
        var header = SegmentBody(root.Children[1].Children[0]).Children[1];

        Assert.Equal("JFIF Header", header.Name);
        Assert.StartsWith("JFIF", header.Children[0].Description);
    }

    [Fact]
    public void Parse_MinimalJpeg_ScanDataStopsBeforeEndMarker()
    {
        var buffer = new ArrayByteBuffer(BuildMinimalJpeg());

        var root = StructureParser.Parse(LoadJpegDefinition(), buffer);
        var sos = SegmentBody(root.Children[1].Children[4]);
        var scanData = sos.Children[^1];

        Assert.Equal("Scan Data", scanData.Name);
        Assert.Equal(5, scanData.Length);
    }

    [Fact]
    public void Parse_JpegWithRestartMarker_SplitsScanAtRestart()
    {
        var buffer = new ArrayByteBuffer(BuildJpegWithRestartMarker());

        var root = StructureParser.Parse(LoadJpegDefinition(), buffer);
        var segments = root.Children[1];

        Assert.Equal(["SOS", "RSTn", "EOI"], segments.Children.Select(c => c.Name));
        Assert.Equal(2, SegmentBody(segments.Children[0]).Children[^1].Length);
        Assert.Equal(1, SegmentBody(segments.Children[1]).Children[0].Length);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    public void Parse_JpegWithFillBytesBeforeMarker_KeepsMarkerAligned(int fillByteCount)
    {
        var buffer = new ArrayByteBuffer(BuildJpegWithFillBytes(fillByteCount));

        var root = StructureParser.Parse(LoadJpegDefinition(), buffer);
        var segments = root.Children[1];

        Assert.Equal(["APP0 (JFIF)", "EOI"], segments.Children.Select(c => c.Name));

        var fill = segments.Children[1].Children[0];
        Assert.Equal("Fill Bytes", fill.Name);
        Assert.Equal(fillByteCount, fill.Length);
    }

    [Fact]
    public void Parse_JpegWithShortApp0_StopsAtDeclaredLength()
    {
        var buffer = new ArrayByteBuffer(BuildJpegWithShortApp0());

        var root = StructureParser.Parse(LoadJpegDefinition(), buffer);
        var segments = root.Children[1];

        Assert.Equal(["APP0 (JFIF)", "EOI"], segments.Children.Select(c => c.Name));
        Assert.Equal(2, segments.Children[0].Offset);
        Assert.Equal(15, segments.Children[0].Length);
        Assert.Equal(17, segments.Children[1].Offset);
    }

    [Fact]
    public void Parse_JpegWithReservedMarkerCode_SkipsToNextRealMarker()
    {
        var buffer = new ArrayByteBuffer(BuildJpegWithReservedMarker());

        var root = StructureParser.Parse(LoadJpegDefinition(), buffer);
        var segments = root.Children[1];

        Assert.Equal(["APP0 (JFIF)", "EOI"], segments.Children.Select(c => c.Name));

        var fill = segments.Children[1].Children[0];
        Assert.Equal("Fill Bytes", fill.Name);
        Assert.Equal(2, fill.Length);
    }

    [Fact]
    public void Parse_JpegWithDataAfterEndMarker_KeepsParsing()
    {
        var buffer = new ArrayByteBuffer(BuildJpegWithTrailingSegment());

        var root = StructureParser.Parse(LoadJpegDefinition(), buffer);
        var segments = root.Children[1];

        Assert.Equal(
            ["APP0 (JFIF)", "DQT", "SOF0 (Baseline DCT)", "DHT", "SOS", "EOI", "COM"],
            segments.Children.Select(c => c.Name));
        Assert.Equal("AB", SegmentBody(segments.Children[^1]).Children[1].Description);
    }

    [Fact]
    public void Parse_TruncatedJpeg_ReturnsPartialTree()
    {
        var jpeg = BuildMinimalJpeg();
        var buffer = new ArrayByteBuffer(jpeg[..144]); // 141 (through DHT) + 2 (SOS marker) + 1 (partway into the segment size)

        var root = StructureParser.Parse(LoadJpegDefinition(), buffer);
        var segments = root.Children[1];

        Assert.Equal("SOS", segments.Children[^1].Name);
    }

    private static FormatDefinition LoadByteRepeatDefinition()
    {
        const string json = """
        {
          "name": "Bytes",
          "extensions": ["bin"],
          "magic": "00",
          "fields": [
            {
              "id": "items",
              "name": "Items",
              "type": "repeat",
              "until": "eof",
              "fields": [
                { "id": "value", "name": "Value", "type": "uint8" }
              ]
            }
          ]
        }
        """;
        return JsonSerializer.Deserialize<FormatDefinition>(json, JsonOptions)!;
    }

    private static int CountNodes(FileStructureNode node)
    {
        var count = 1;
        foreach (var child in node.Children)
            count += CountNodes(child);
        return count;
    }

    [Fact]
    public void Parse_ExceedingNodeBudget_AppendsTruncationNodeCoveringRest()
    {
        var buffer = new ArrayByteBuffer(new byte[100]);

        var root = StructureParser.Parse(LoadByteRepeatDefinition(), buffer, nodeBudget: 10);
        var items = root.Children[0];
        var truncation = root.Children[^1];

        Assert.Equal(2, root.Children.Count);
        Assert.True(truncation.IsUnparsed);
        Assert.Equal(items.Offset + items.Length, truncation.Offset);
        Assert.Equal(100, truncation.Offset + truncation.Length);
        Assert.True(CountNodes(root) - 2 <= 10 + 2);
    }

    [Fact]
    public void Parse_WithinNodeBudget_DoesNotAppendTruncationNode()
    {
        var buffer = new ArrayByteBuffer([0x00, 0x01, 0x02]);

        var root = StructureParser.Parse(LoadByteRepeatDefinition(), buffer, nodeBudget: 100);

        Assert.Single(root.Children);
        Assert.Equal(3, root.Children[0].Children.Count);
    }

    [Fact]
    public void Parse_ExceedingNodeBudgetInNestedRepeat_StopsOuterRepeat()
    {
        const string json = """
        {
          "name": "Nested",
          "extensions": ["bin"],
          "magic": "00",
          "fields": [
            {
              "id": "outer",
              "name": "Outer",
              "type": "repeat",
              "until": "eof",
              "fields": [
                {
                  "id": "inner",
                  "name": "Inner",
                  "type": "repeat",
                  "size": 4,
                  "fields": [
                    { "id": "value", "name": "Value", "type": "uint8" }
                  ]
                }
              ]
            }
          ]
        }
        """;
        var definition = JsonSerializer.Deserialize<FormatDefinition>(json, JsonOptions)!;
        var buffer = new ArrayByteBuffer(new byte[400]);

        var root = StructureParser.Parse(definition, buffer, nodeBudget: 10);
        var outer = root.Children[0];
        var truncation = root.Children[^1];

        Assert.True(truncation.IsUnparsed);
        Assert.Equal(outer.Offset + outer.Length, truncation.Offset);
        Assert.True(CountNodes(root) - 2 <= 10 + 3);
    }

    [Fact]
    public void Parse_ExceedingNodeBudgetInSizedGroup_DoesNotSkipToGroupEnd()
    {
        const string json = """
        {
          "name": "Sized",
          "extensions": ["bin"],
          "magic": "00",
          "fields": [
            {
              "id": "body",
              "name": "Body",
              "type": "group",
              "size": 8,
              "fields": [
                {
                  "id": "items",
                  "name": "Items",
                  "type": "repeat",
                  "fields": [
                    { "id": "value", "name": "Value", "type": "uint8" }
                  ]
                }
              ]
            },
            { "id": "trailer", "name": "Trailer", "type": "uint8" }
          ]
        }
        """;
        var definition = JsonSerializer.Deserialize<FormatDefinition>(json, JsonOptions)!;
        var buffer = new ArrayByteBuffer(new byte[20]);

        var root = StructureParser.Parse(definition, buffer, nodeBudget: 4);
        var body = root.Children[0];
        var truncation = root.Children[^1];

        Assert.Equal(2, root.Children.Count);
        Assert.True(truncation.IsUnparsed);
        Assert.Equal(body.Offset + body.Length, truncation.Offset);
        Assert.True(truncation.Offset < 8);
    }

    [Fact]
    public void Parse_ExceedingNodeBudgetMidIteration_DoesNotSkipToSizedRepeatEnd()
    {
        const string json = """
        {
          "name": "Terminated",
          "extensions": ["bin"],
          "magic": "01",
          "fields": [
            {
              "id": "frames",
              "name": "Frames",
              "type": "repeat",
              "size": 10,
              "until": "peek:0x00",
              "fields": [
                { "id": "id", "name": "ID", "type": "uint8" },
                { "id": "size", "name": "Size", "type": "uint8" }
              ]
            }
          ]
        }
        """;
        var definition = JsonSerializer.Deserialize<FormatDefinition>(json, JsonOptions)!;
        var buffer = new ArrayByteBuffer([0x01, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00]);

        var root = StructureParser.Parse(definition, buffer, nodeBudget: 1);
        var frames = root.Children[0];
        var truncation = root.Children[^1];

        Assert.True(truncation.IsUnparsed);
        Assert.Equal(1, truncation.Offset);
        Assert.Equal(frames.Offset + frames.Length, truncation.Offset);
    }

    [Fact]
    public void Parse_DefaultNodeBudget_IsOneHundredThousand()
    {
        var buffer = new ArrayByteBuffer(new byte[1024 * 1024]);

        var root = StructureParser.Parse(LoadByteRepeatDefinition(), buffer);
        var truncation = root.Children[^1];

        Assert.True(truncation.IsUnparsed);
        Assert.Equal((FileStructureErrorKind.NodeBudget, "100000"), (truncation.Error?.Kind, truncation.Error?.Arguments[0]));
    }

    [Fact]
    public void Parse_WavDataChunkOver2GB_ParsesFollowingChunkAtItsOffset()
    {
        const long dataSize = 3_000_000_000;
        const long listOffset = 44 + dataSize;
        var header = new List<byte>();
        header.AddRange("RIFF"u8.ToArray());
        header.AddRange(BitConverter.GetBytes(0u));
        header.AddRange("WAVE"u8.ToArray());
        header.AddRange("fmt "u8.ToArray());
        header.AddRange(BitConverter.GetBytes(16u));
        header.AddRange(new byte[16]);
        header.AddRange("data"u8.ToArray());
        header.AddRange(BitConverter.GetBytes((uint)dataSize));
        var list = new List<byte>();
        list.AddRange("LIST"u8.ToArray());
        list.AddRange(BitConverter.GetBytes(4u));
        list.AddRange([0x01, 0x02, 0x03, 0x04]);
        var buffer = new SparseByteBuffer(listOffset + list.Count, (0, [.. header]), (listOffset, [.. list]));

        var root = StructureParser.Parse(LoadWavDefinition(), buffer);
        var subChunks = root.Children[1];
        var data = subChunks.Children[1];

        Assert.Equal(dataSize, data.Children[2].Length);
        Assert.Equal(buffer.Length - 12, subChunks.Length);
        Assert.Equal(3, subChunks.Children.Count);
        Assert.Equal("LIST", subChunks.Children[2].Name);
        Assert.Equal(listOffset, subChunks.Children[2].Offset);
    }

    private static FormatDefinition LoadRestPayloadDefinition()
    {
        const string json = """
        {
          "name": "Payload",
          "extensions": ["bin"],
          "magic": "00",
          "fields": [
            { "id": "header", "name": "Header", "type": "uint8" },
            { "id": "payload", "name": "Payload", "type": "bytes", "size": "rest" }
          ]
        }
        """;
        return JsonSerializer.Deserialize<FormatDefinition>(json, JsonOptions)!;
    }

    [Theory]
    [InlineData("group")]
    [InlineData("repeat")]
    public void Parse_ContainerWithMaximumSizeAfterOffset_StopsAtBufferEnd(string type)
    {
        var json = $$"""
        {
          "name": "HugeContainer",
          "extensions": ["bin"],
          "magic": "7F",
          "endian": "big",
          "fields": [
            { "id": "length", "name": "Length", "type": "uint64" },
            {
              "id": "body",
              "name": "Body",
              "type": "{{type}}",
              "size": "length",
              "fields": [
                { "id": "value", "name": "Value", "type": "uint8" }
              ]
            }
          ]
        }
        """;
        var definition = JsonSerializer.Deserialize<FormatDefinition>(json, JsonOptions)!;
        var buffer = new ArrayByteBuffer(
            [0x7F, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0x11, 0x22]);

        var root = StructureParser.Parse(definition, buffer);
        var body = root.Children[1];

        Assert.Equal(8, body.Offset);
        Assert.Equal(2, body.Length);
    }

    [Fact]
    public void Parse_RestSizeOver2GB_CoversRemainingBytes()
    {
        var buffer = new SparseByteBuffer(3_000_000_000);

        var root = StructureParser.Parse(LoadRestPayloadDefinition(), buffer);

        Assert.Equal(buffer.Length, root.Length);
        Assert.Equal(buffer.Length - 1, root.Children[1].Length);
    }

    [Fact]
    public void StructureColorMap_LeafOver2GB_ReturnsKindBeyond2GB()
    {
        var buffer = new SparseByteBuffer(3_000_000_000);
        var root = StructureParser.Parse(LoadRestPayloadDefinition(), buffer);

        var map = StructureColorMap.Build(root);

        Assert.Equal(ValueKind.Bytes, map.GetValueKind(2_999_999_999));
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

    [Fact]
    public void DefinitionRegistry_TryMatch_MatchesJpeg()
    {
        var buffer = new ArrayByteBuffer(BuildMinimalJpeg());

        var definition = DefinitionRegistry.TryMatch(buffer);

        Assert.NotNull(definition);
        Assert.Equal("JPEG", definition.Name);
    }
}
