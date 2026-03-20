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

    private static byte[] BuildMinimalPng()
    {
        var data = new List<byte>();

        // シグネチャ
        data.AddRange([0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A]);

        // IHDR チャンク
        data.AddRange([0x00, 0x00, 0x00, 0x0D]);
        data.AddRange("IHDR"u8.ToArray());
        data.AddRange(new byte[13]);
        data.AddRange([0x00, 0x00, 0x00, 0x00]);

        // IEND チャンク
        data.AddRange([0x00, 0x00, 0x00, 0x00]);
        data.AddRange("IEND"u8.ToArray());
        data.AddRange([0xAE, 0x42, 0x60, 0x82]);

        return [.. data];
    }

    private static FormatDefinition LoadPngDefinition()
    {
        const string json = """
        {
          "name": "PNG",
          "extensions": ["png"],
          "magic": "89 50 4E 47 0D 0A 1A 0A",
          "endian": "big",
          "fields": [
            {
              "id": "signature",
              "name": "Signature",
              "type": "bytes",
              "size": 8
            },
            {
              "id": "chunks",
              "name": "Chunks",
              "type": "repeat",
              "until": "eof",
              "nameTemplate": "${chunkType}",
              "fields": [
                { "id": "dataLength", "name": "Data Length", "type": "uint32" },
                { "id": "chunkType", "name": "Chunk Type", "type": "ascii", "size": 4 },
                { "id": "chunkData", "name": "Chunk Data", "type": "bytes", "size": "dataLength" },
                { "id": "crc", "name": "CRC", "type": "uint32" }
              ]
            }
          ]
        }
        """;
        return JsonSerializer.Deserialize<FormatDefinition>(json, JsonOptions)!;
    }

    [Fact]
    public void Parse_ValidPng_ReturnsCorrectTreeStructure()
    {
        var png = BuildMinimalPng();
        var buffer = new ArrayByteBuffer(png);
        var definition = LoadPngDefinition();

        var root = StructureParser.Parse(definition, buffer);

        Assert.Equal("PNG", root.Name);
        Assert.Equal(0, root.Offset);
        Assert.Equal(png.Length, root.Length);
        Assert.Equal(2, root.Children.Count); // シグネチャ + チャンク
    }

    [Fact]
    public void Parse_ValidPng_SignatureNode()
    {
        var png = BuildMinimalPng();
        var buffer = new ArrayByteBuffer(png);
        var definition = LoadPngDefinition();

        var root = StructureParser.Parse(definition, buffer);
        var sig = root.Children[0];

        Assert.Equal("Signature", sig.Name);
        Assert.Equal(0, sig.Offset);
        Assert.Equal(8, sig.Length);
        Assert.Contains("89", sig.Description);
    }

    [Fact]
    public void Parse_ValidPng_ChunksRepeatNode()
    {
        var png = BuildMinimalPng();
        var buffer = new ArrayByteBuffer(png);
        var definition = LoadPngDefinition();

        var root = StructureParser.Parse(definition, buffer);
        var chunks = root.Children[1];

        Assert.Equal("Chunks", chunks.Name);
        Assert.Equal(2, chunks.Children.Count); // IHDR + IEND
    }

    [Fact]
    public void Parse_ValidPng_ChunkNameTemplate()
    {
        var png = BuildMinimalPng();
        var buffer = new ArrayByteBuffer(png);
        var definition = LoadPngDefinition();

        var root = StructureParser.Parse(definition, buffer);
        var chunks = root.Children[1];

        Assert.Equal("IHDR", chunks.Children[0].Name);
        Assert.Equal("IEND", chunks.Children[1].Name);
    }

    [Fact]
    public void Parse_ValidPng_ChunkOffsets()
    {
        var png = BuildMinimalPng();
        var buffer = new ArrayByteBuffer(png);
        var definition = LoadPngDefinition();

        var root = StructureParser.Parse(definition, buffer);
        var chunks = root.Children[1];

        // IHDR はシグネチャ（8 バイト）の直後から開始
        Assert.Equal(8, chunks.Children[0].Offset);
        // IHDR チャンク: 4 (length) + 4 (type) + 13 (data) + 4 (crc) = 25
        Assert.Equal(25, chunks.Children[0].Length);

        // IEND は 8 + 25 = 33 から 開始
        Assert.Equal(33, chunks.Children[1].Offset);
        // IEND チャンク: 4 (length) + 4 (type) + 0 (data) + 4 (crc) = 12
        Assert.Equal(12, chunks.Children[1].Length);
    }

    [Fact]
    public void Parse_ValidPng_VariableSizeReference()
    {
        var png = BuildMinimalPng();
        var buffer = new ArrayByteBuffer(png);
        var definition = LoadPngDefinition();

        var root = StructureParser.Parse(definition, buffer);
        var ihdrChunk = root.Children[1].Children[0];

        var dataLengthNode = ihdrChunk.Children[0];
        Assert.Equal("Data Length", dataLengthNode.Name);
        Assert.Equal("13", dataLengthNode.Description);

        var chunkDataNode = ihdrChunk.Children[2];
        Assert.Equal("Chunk Data", chunkDataNode.Name);
        Assert.Equal(13, chunkDataNode.Length);
    }

    [Fact]
    public void Parse_IncompleteBuffer_ReturnsPartialResult()
    {
        var png = BuildMinimalPng();
        var truncated = png[..15]; // 8 (signature) + 4 (length) + 3 (type の途中)
        var buffer = new ArrayByteBuffer(truncated);
        var definition = LoadPngDefinition();

        var root = StructureParser.Parse(definition, buffer);

        Assert.Equal("PNG", root.Name);
        Assert.Equal(2, root.Children.Count);

        var sig = root.Children[0];
        Assert.Equal(8, sig.Length);

        var chunks = root.Children[1];
        Assert.True(chunks.Children.Count >= 1);
    }

    [Fact]
    public void DefinitionRegistry_TryMatch_MatchesPng()
    {
        var png = BuildMinimalPng();
        var buffer = new ArrayByteBuffer(png);

        var definition = DefinitionRegistry.TryMatch(buffer);

        Assert.NotNull(definition);
        Assert.Equal("PNG", definition.Name);
    }

    [Fact]
    public void DefinitionRegistry_TryMatch_NonPngReturnsNull()
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
