using ZeroIchi.Infrastructure;
using ZeroIchi.Models;

namespace ZeroIchi.Tests.Infrastructure;

public sealed class JsonSettingsStoreTests : IDisposable
{
    private readonly string _tempDir = Path.Combine(Path.GetTempPath(), "ZeroIchiTests_" + Guid.NewGuid().ToString("N"));

    private string SettingsPath => Path.Combine(_tempDir, "settings.json");

    public JsonSettingsStoreTests() => Directory.CreateDirectory(_tempDir);

    public void Dispose() => Directory.Delete(_tempDir, recursive: true);

    private AppSettings LoadFrom(string json)
    {
        File.WriteAllText(SettingsPath, json);
        return new JsonSettingsStore(SettingsPath).Load();
    }

    [Fact]
    public void Save_ThenLoad_RoundTrips()
    {
        new JsonSettingsStore(SettingsPath).Save(new AppSettings { Language = "en" });

        Assert.Equal(new AppSettings { Language = "en" }, new JsonSettingsStore(SettingsPath).Load());
    }

    [Fact]
    public void Save_WritesCamelCaseJson()
    {
        new JsonSettingsStore(SettingsPath).Save(new AppSettings { Language = "en" });

        Assert.Contains("\"language\": \"en\"", File.ReadAllText(SettingsPath));
    }

    [Fact]
    public void Save_CreatesTheFolder()
    {
        var path = Path.Combine(_tempDir, "ZeroIchi", "settings.json");

        new JsonSettingsStore(path).Save(new AppSettings { Language = "ja" });

        Assert.Equal("ja", new JsonSettingsStore(path).Load().Language);
    }

    [Fact]
    public void Load_MissingFile_ReturnsDefault()
    {
        Assert.Equal(AppSettings.Default, new JsonSettingsStore(SettingsPath).Load());
    }

    [Fact]
    public void Load_CorruptJson_ReturnsDefaultAndKeepsTheFile()
    {
        var settings = LoadFrom("{ not json");

        Assert.Equal(AppSettings.Default, settings);
        Assert.Equal("{ not json", File.ReadAllText(SettingsPath));
    }

    [Fact]
    public void Load_JsonNull_ReturnsDefault()
    {
        Assert.Equal(AppSettings.Default, LoadFrom("null"));
    }

    [Fact]
    public void Load_LanguageOfTheWrongType_ReturnsDefault()
    {
        Assert.Equal(AppSettings.Default, LoadFrom("{ \"language\": 1 }"));
    }

    [Fact]
    public void Load_UnknownProperty_IsIgnored()
    {
        Assert.Equal("ja", LoadFrom("{ \"language\": \"ja\", \"theme\": \"dark\" }").Language);
    }

    [Fact]
    public void Save_WhenThePathIsADirectory_ThrowsAndLeavesNoTempFile()
    {
        Directory.CreateDirectory(SettingsPath);
        var store = new JsonSettingsStore(SettingsPath);

        var ex = Record.Exception(() => store.Save(new AppSettings { Language = "en" }));

        Assert.True(ex is IOException or UnauthorizedAccessException, $"Unexpected exception: {ex}");
        Assert.Empty(Directory.EnumerateFiles(_tempDir, "*.tmp"));
    }
}
