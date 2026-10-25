using System.IO.Compression;
using System.Text;
using ZeroIchi.Models.Commands;
using ZeroIchi.Services;
using ZeroIchi.ViewModels;

namespace ZeroIchi.Tests.ViewModels;

public sealed class MainWindowViewModelFileTypeTests : IDisposable
{
    static MainWindowViewModelFileTypeTests() => Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);

    private static readonly byte[] Png =
    [
        0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A,
        0x00, 0x00, 0x00, 0x0D, 0x49, 0x48, 0x44, 0x52,
        0x00, 0x00, 0x00, 0x01, 0x00, 0x00, 0x00, 0x01, 0x08, 0x06, 0x00, 0x00, 0x00,
        0x1F, 0x15, 0xC4, 0x89,
    ];

    private readonly string _tempDir = Path.Combine(Path.GetTempPath(), "ZeroIchiTests_" + Guid.NewGuid().ToString("N"));

    public MainWindowViewModelFileTypeTests() => Directory.CreateDirectory(_tempDir);

    public void Dispose() => Directory.Delete(_tempDir, recursive: true);

    private async Task<MainWindowViewModel> OpenAsync(string fileName, byte[] content)
    {
        var path = Path.Combine(_tempDir, fileName);
        File.WriteAllBytes(path, content);
        var vm = new MainWindowViewModel(new StubDialogService(), new StubClipboardService(), new StubWindowService());
        await vm.OpenFileAsync(path);
        await vm.StructureUpdate;
        return vm;
    }

    private async Task<string> FileTypeAfterOpeningAsync(string fileName, byte[] content)
    {
        var vm = await OpenAsync(fileName, content);
        try
        {
            return vm.StatusBarFileTypeText;
        }
        finally
        {
            await vm.DisposeDocumentAsync();
        }
    }

    private static byte[] Docx()
    {
        using var stream = new MemoryStream();
        using (var archive = new ZipArchive(stream, ZipArchiveMode.Create))
        {
            foreach (var (name, content) in new[]
            {
                ("[Content_Types].xml",
                    """<?xml version="1.0"?><Types><Override PartName="/word/document.xml" ContentType="application/vnd.openxmlformats-officedocument.wordprocessingml.document.main+xml"/></Types>"""),
                ("_rels/.rels", "<Relationships/>"),
                ("word/document.xml", "<w/>"),
            })
            {
                using var writer = new StreamWriter(archive.CreateEntry(name).Open());
                writer.Write(content);
            }
        }
        return stream.ToArray();
    }

    [Fact]
    public void WarmUpFileTypeDetection_DoesNotThrow()
    {
        var exception = Record.Exception(MainWindowViewModel.WarmUpFileTypeDetection);

        Assert.Null(exception);
    }

    [Fact]
    public async Task OpeningAPngWithoutExtension_ShowsPng()
    {
        Assert.Equal("png", await FileTypeAfterOpeningAsync("image", Png));
    }

    [Fact]
    public async Task OpeningADocxRenamedToTxt_ShowsDocx()
    {
        Assert.Equal("docx", await FileTypeAfterOpeningAsync("document.txt", Docx()));
    }

    [Fact]
    public async Task OpeningAnEpubRenamedToZip_ShowsEpub()
    {
        var epub = new ZipBuilder()
            .Add("mimetype", "application/epub+zip"u8.ToArray())
            .Add("META-INF/container.xml", "<container/>"u8.ToArray())
            .Build();

        Assert.Equal("epub", await FileTypeAfterOpeningAsync("book.zip", epub));
    }

    [Fact]
    public async Task OpeningAnEmptyFile_ShowsNothing()
    {
        Assert.Equal("", await FileTypeAfterOpeningAsync("empty.bin", []));
    }

    [Fact]
    public async Task EditingTheMagic_UpdatesTheFileType()
    {
        var vm = await OpenAsync("image.png", Png);
        var document = vm.Document!;
        try
        {
            Assert.Equal("png", vm.StatusBarFileTypeText);

            document.ExecuteCommand(new WriteByteCommand(document, 0, 0x00, cursorPositionBefore: 0, cursorPositionAfter: 0));
            await vm.StructureUpdate;

            Assert.NotEqual("png", vm.StatusBarFileTypeText);
        }
        finally
        {
            await vm.DisposeDocumentAsync();
        }
    }

    private sealed class StubDialogService : IDialogService
    {
        public Task<string?> PickOpenFileAsync(string title) => Task.FromResult<string?>(null);
        public Task<string?> PickSaveFileAsync(string title, string? suggestedFileName) => Task.FromResult<string?>(null);
        public Task<SaveChangesResult> ShowSaveChangesDialogAsync() => Task.FromResult(SaveChangesResult.Discard);
        public Task ShowErrorAsync(string message, string detail) => Task.CompletedTask;
        public Task ShowAboutAsync() => Task.CompletedTask;
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
