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
                { "id": "chunkId", "name": "Chunk ID", "type": "ascii", "size": 4 },
                { "id": "fileSize", "name": "File Size", "type": "uint32" },
                { "id": "format", "name": "Format", "type": "ascii", "size": 4 }
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
                { "id": "chunkId", "name": "Chunk ID", "type": "ascii", "size": 4 },
                { "id": "chunkSize", "name": "Chunk Size", "type": "uint32" },
                { "id": "format", "name": "Format", "type": "ascii", "size": 4 }
              ]
            },
            {
              "id": "subChunks",
              "name": "Sub-Chunks",
              "type": "repeat",
              "until": "eof",
              "nameTemplate": "${subChunkId}",
              "fields": [
                { "id": "subChunkId", "name": "Sub-Chunk ID", "type": "ascii", "size": 4 },
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
