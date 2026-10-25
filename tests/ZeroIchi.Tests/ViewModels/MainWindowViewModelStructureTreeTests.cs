using System.Text;
using ZeroIchi.Models.FileStructure.Ksy;
using ZeroIchi.Models.FileStructure.Ksy.Model;
using ZeroIchi.ViewModels;
using static ZeroIchi.Tests.ViewModelTestFactory;

namespace ZeroIchi.Tests.ViewModels;

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
        await vm.StructureUpdate;
        return vm;
    }

    private static string Names(MainWindowViewModel vm) => string.Join(",", vm.StructureTreeItems.Select(i => i.Name));

    [Fact]
    public async Task OpenFile_MatchingKsyDefinition_BuildsTheTreeWithTheInterpreter()
    {
        var vm = await OpenAsync("a.bin", "ZI\u0005"u8.ToArray(), MagicYaml);
        try
        {
            Assert.Equal("magic,len,doubled", Names(vm));
            Assert.NotNull(vm.StructureColors);
        }
        finally
        {
            await vm.DisposeDocumentAsync();
        }
    }

    [Fact]
    public async Task OpenFile_KsyDefinitionDetectedByTheFileExtension()
    {
        const string yaml = "meta:\n  id: ext\n  file-extension: zz\nseq:\n  - id: first\n    type: u1\n";

        var vm = await OpenAsync("a.zz", [7, 8], yaml);
        try
        {
            Assert.Equal("first", Names(vm));
        }
        finally
        {
            await vm.DisposeDocumentAsync();
        }
    }

    [Fact]
    public async Task OpenFile_NoKsyMatch_LeavesTheTreeEmpty()
    {
        byte[] png = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A];

        var vm = await OpenAsync("a.png", png, MagicYaml);
        try
        {
            Assert.Empty(vm.StructureTreeItems);
            Assert.Null(vm.StructureColors);
        }
        finally
        {
            await vm.DisposeDocumentAsync();
        }
    }

    [Fact]
    public async Task OpenFile_PngUsesTheBuiltinDefinitionByDefault()
    {
        byte[] png =
        [
            0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A,
            0, 0, 0, 13, .. "IHDR"u8, 0, 0, 0, 1, 0, 0, 0, 1, 8, 2, 0, 0, 0, 0, 0, 0, 0,
            0, 0, 0, 0, .. "IEND"u8, 0, 0, 0, 0,
        ];
        var path = Path.Combine(_tempDir, "a.png");
        File.WriteAllBytes(path, png);
        var vm = CreateViewModel();

        await vm.OpenFileAsync(path);
        await vm.StructureUpdate;
        try
        {
            Assert.Equal("magic,ihdr_len,ihdr_type,ihdr,ihdr_crc,chunks", Names(vm));
        }
        finally
        {
            await vm.DisposeDocumentAsync();
        }
    }

    [Fact]
    public async Task SelectingANodeWithoutRange_KeepsTheCursorAndSelection()
    {
        var vm = await OpenAsync("a.bin", "ZI\u0005"u8.ToArray(), MagicYaml);
        try
        {
            vm.SelectedStructureItem = vm.StructureTreeItems.Single(i => i.Name == "len");

            vm.SelectedStructureItem = vm.StructureTreeItems.Single(i => i.Name == "doubled");

            Assert.Equal((2L, 2L, 1L), (vm.CursorPosition, vm.SelectionStart, vm.SelectionLength));
        }
        finally
        {
            await vm.DisposeDocumentAsync();
        }
    }
}
