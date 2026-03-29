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
