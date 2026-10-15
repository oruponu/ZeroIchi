using System.Text;
using ZeroIchi.Models.FileStructure.Ksy;
using ZeroIchi.Models.FileStructure.Ksy.Model;
using ZeroIchi.Services;
using ZeroIchi.ViewModels;

namespace ZeroIchi.Tests;

public sealed class MainWindowViewModelStructureTreeTests : IDisposable
{
    static MainWindowViewModelStructureTreeTests() => Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);

    private const string MagicYaml = """
        meta:
          id: zi
        seq:
          - id: magic
            contents: ZI
          - id: len
            type: u1
        instances:
          doubled:
            value: len * 2
        """;

    private readonly string _tempDir = Path.Combine(Path.GetTempPath(), "ZeroIchiTests_" + Guid.NewGuid().ToString("N"));

    public MainWindowViewModelStructureTreeTests() => Directory.CreateDirectory(_tempDir);

    public void Dispose() => Directory.Delete(_tempDir, recursive: true);

    private async Task<MainWindowViewModel> OpenAsync(string fileName, byte[] content, params string[] yamls)
    {
        var registry = new KsyFormatRegistry();
        foreach (var yaml in yamls)
            registry.Register(KsyModelBuilder.Parse(yaml).Format!);

        var path = Path.Combine(_tempDir, fileName);
        File.WriteAllBytes(path, content);
        var vm = new MainWindowViewModel(new StubDialogService(), new StubClipboardService(), new StubWindowService())
        {
            KsyFormats = registry,
        };
        await vm.OpenFileAsync(path);
        return vm;
    }

    private static string Names(MainWindowViewModel vm) => string.Join(",", vm.StructureTreeItems.Select(i => i.Name));

    [Fact]
    public async Task OpenFile_MatchingKsyDefinition_BuildsTheTreeWithTheInterpreter()
    {
        var vm = await OpenAsync("a.bin", "ZI\u0005"u8.ToArray(), MagicYaml);
        using var _ = vm.Document;

        Assert.Equal("magic,len,doubled", Names(vm));
        Assert.NotNull(vm.StructureColors);
    }

    [Fact]
    public async Task OpenFile_KsyDefinitionDetectedByTheFileExtension()
    {
        const string yaml = "meta:\n  id: ext\n  file-extension: zz\nseq:\n  - id: first\n    type: u1\n";

        var vm = await OpenAsync("a.zz", [7, 8], yaml);
        using var _ = vm.Document;

        Assert.Equal("first", Names(vm));
    }

    [Fact]
    public async Task OpenFile_NoKsyMatch_FallsBackToTheJsonDefinitions()
    {
        byte[] png = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A];

        var vm = await OpenAsync("a.png", png, MagicYaml);
        using var _ = vm.Document;

        Assert.Equal("Signature", vm.StructureTreeItems[0].Name);
    }

    private sealed class StubDialogService : IDialogService
    {
        public Task<string?> PickOpenFileAsync(string title) => Task.FromResult<string?>(null);
        public Task<string?> PickSaveFileAsync(string title, string? suggestedFileName) => Task.FromResult<string?>(null);
        public Task<SaveChangesResult> ShowSaveChangesDialogAsync() => Task.FromResult(SaveChangesResult.Discard);
        public Task ShowErrorAsync(string message, string detail) => Task.CompletedTask;
    }

    private sealed class StubClipboardService : IClipboardService
    {
        public Task SetBytesAsync(byte[] bytes, string? text) => Task.CompletedTask;
        public Task<byte[]?> TryGetBytesAsync() => Task.FromResult<byte[]?>(null);
        public Task<string?> GetTextAsync() => Task.FromResult<string?>(null);
    }

    private sealed class StubWindowService : IWindowService
    {
        public void Close() { }
    }
}
