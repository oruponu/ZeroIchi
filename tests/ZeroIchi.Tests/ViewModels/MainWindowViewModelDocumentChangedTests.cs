using System.Text;
using ZeroIchi.Models.Commands;
using ZeroIchi.ViewModels;
using static ZeroIchi.Tests.ViewModelTestFactory;

namespace ZeroIchi.Tests.ViewModels;

public sealed class MainWindowViewModelDocumentChangedTests : IDisposable
{
    static MainWindowViewModelDocumentChangedTests() => Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);

    private readonly string _tempDir = Path.Combine(Path.GetTempPath(), "ZeroIchiTests_" + Guid.NewGuid().ToString("N"));

    public MainWindowViewModelDocumentChangedTests() => Directory.CreateDirectory(_tempDir);

    public void Dispose() => Directory.Delete(_tempDir, recursive: true);

    private string CreateTestFile()
    {
        var path = Path.Combine(_tempDir, Guid.NewGuid().ToString("N") + ".bin");
        File.WriteAllBytes(path, [1, 2, 3]);
        return path;
    }

    [Fact]
    public async Task InitialDocument_EditedDirectly_UpdatesViewModelState()
    {
        var vm = CreateViewModel();
        var document = vm.Document!;
        try
        {
            var version = vm.DataVersion;

            document.ExecuteCommand(new AppendByteCommand(document, 0x42, cursorPositionBefore: 0, cursorPositionAfter: 1));

            Assert.Equal(version + 1, vm.DataVersion);
            Assert.True(vm.CanUndo);
            Assert.Equal("*Untitled - ZeroIchi", vm.Title);
        }
        finally
        {
            await vm.DisposeDocumentAsync();
        }
    }

    [Fact]
    public async Task OpenedDocument_EditedDirectly_UpdatesViewModelState()
    {
        var vm = CreateViewModel();
        await vm.OpenFileAsync(CreateTestFile());
        var document = vm.Document!;
        try
        {
            var version = vm.DataVersion;

            document.ExecuteCommand(new WriteByteCommand(document, 0, 0xFF, cursorPositionBefore: 0, cursorPositionAfter: 0));

            Assert.Equal(version + 1, vm.DataVersion);
            Assert.True(vm.CanUndo);
            Assert.StartsWith("*", vm.Title);
        }
        finally
        {
            await vm.DisposeDocumentAsync();
        }
    }

    [Fact]
    public async Task ReplacedDocument_EditedAfterReplace_DoesNotAffectViewModel()
    {
        var vm = CreateViewModel();
        var oldDocument = vm.Document!;
        await vm.OpenFileAsync(CreateTestFile());
        try
        {
            var version = vm.DataVersion;
            var title = vm.Title;

            oldDocument.ExecuteCommand(new AppendByteCommand(oldDocument, 0x42, cursorPositionBefore: 0, cursorPositionAfter: 1));

            Assert.Equal(version, vm.DataVersion);
            Assert.False(vm.CanUndo);
            Assert.Equal(title, vm.Title);
        }
        finally
        {
            await vm.DisposeDocumentAsync();
        }
    }

    [Fact]
    public async Task Save_IncrementsDataVersionAndClearsModifiedMark()
    {
        var vm = CreateViewModel();
        await vm.OpenFileAsync(CreateTestFile());
        try
        {
            vm.OnByteModified(0, 0xFF, cursorPositionAfter: 1);
            var version = vm.DataVersion;

            await vm.SaveFileCommand.ExecuteAsync(null);

            Assert.Equal(version + 1, vm.DataVersion);
            Assert.False(vm.Title.StartsWith('*'));
        }
        finally
        {
            await vm.DisposeDocumentAsync();
        }
    }
}
