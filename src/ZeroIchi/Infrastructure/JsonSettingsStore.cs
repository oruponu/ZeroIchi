using System;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using ZeroIchi.Models;
using ZeroIchi.Services;

namespace ZeroIchi.Infrastructure;

public sealed class JsonSettingsStore(string path) : ISettingsStore
{
    public static string DefaultPath { get; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "ZeroIchi", "settings.json");

    public AppSettings Load()
    {
        try
        {
            var json = File.ReadAllText(path);
            return JsonSerializer.Deserialize(json, AppSettingsJsonContext.Default.AppSettings) ?? AppSettings.Default;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException or NotSupportedException)
        {
            return AppSettings.Default;
        }
    }

    public void Save(AppSettings settings)
    {
        var folder = Path.GetDirectoryName(Path.GetFullPath(path))!;
        Directory.CreateDirectory(folder);
        var temp = Path.Combine(folder, $"{Path.GetFileName(path)}.{Guid.NewGuid():N}.tmp");
        try
        {
            File.WriteAllText(temp, JsonSerializer.Serialize(settings, AppSettingsJsonContext.Default.AppSettings));
            File.Move(temp, path, overwrite: true);
        }
        catch
        {
            File.Delete(temp);
            throw;
        }
    }
}

[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase, WriteIndented = true)]
[JsonSerializable(typeof(AppSettings))]
internal sealed partial class AppSettingsJsonContext : JsonSerializerContext;
