using System.Text;
using ZeroIchi.Models;
using ZeroIchi.ViewModels;
using static ZeroIchi.Tests.ViewModelTestFactory;

namespace ZeroIchi.Tests.ViewModels;

public sealed class MainWindowViewModelNibbleMergeTests : IDisposable
{
    static MainWindowViewModelNibbleMergeTests() => Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);

    private readonly string _tempDir = Path.Combine(Path.GetTempPath(), "ZeroIchiTests_" + Guid.NewGuid().ToString("N"));

    public MainWindowViewModelNibbleMergeTests() => Directory.CreateDirectory(_tempDir);

    public void Dispose() => Directory.Delete(_tempDir, recursive: true);

    private async Task<MainWindowViewModel> OpenAsync(byte[] content)
    {
        var path = Path.Combine(_tempDir, Guid.NewGuid().ToString("N") + ".bin");
        File.WriteAllBytes(path, content);

        var vm = CreateViewModel();
        await vm.OpenFileAsync(path);
        return vm;
    }

    private static byte[] ReadAll(MainWindowViewModel vm) =>
        vm.Document!.Buffer.SliceToArray(0, (int)vm.Document.Buffer.Length);

    [Fact]
    public async Task LowNibble_AfterHighNibble_UndoesWholeByteAtOnce()
    {
        var vm = await OpenAsync([0x11, 0x22]);
        try
        {
            vm.OnByteModified(0, 0xA1, 0, ByteInputPart.HighNibble);
            vm.OnByteModified(0, 0xAB, 1, ByteInputPart.LowNibble);

            Assert.Equal([0xAB, 0x22], ReadAll(vm));

            vm.UndoCommand.Execute(null);

            Assert.Equal([0x11, 0x22], ReadAll(vm));
            Assert.False(vm.CanUndo);
            Assert.Equal(0, vm.CursorPosition);
        }
        finally
        {
            await vm.DisposeDocumentAsync();
        }
    }

    [Fact]
    public async Task AtEnd_TwoNibbles_UndoRemovesAppendedByte()
    {
        var vm = await OpenAsync([0x11, 0x22]);
        try
        {
            vm.CursorPosition = 2;
            vm.OnByteModified(2, 0xA0, 2, ByteInputPart.HighNibble);
            vm.OnByteModified(2, 0xAB, 3, ByteInputPart.LowNibble);

            Assert.Equal([0x11, 0x22, 0xAB], ReadAll(vm));

            vm.UndoCommand.Execute(null);

            Assert.Equal([0x11, 0x22], ReadAll(vm));
            Assert.False(vm.CanUndo);
        }
        finally
        {
            await vm.DisposeDocumentAsync();
        }
    }

    [Theory]
    [InlineData("undoRedo")]
    [InlineData("save")]
    [InlineData("cursor")]
    [InlineData("delete")]
    public async Task LowNibble_AfterInterruption_IsSeparateUndoStep(string interruption)
    {
        var vm = await OpenAsync([0x11, 0x22]);
        try
        {
            vm.OnByteModified(0, 0xA1, 0, ByteInputPart.HighNibble);

            switch (interruption)
            {
                case "undoRedo":
                    vm.UndoCommand.Execute(null);
                    vm.RedoCommand.Execute(null);
                    break;
                case "save":
                    await vm.SaveFileCommand.ExecuteAsync(null);
                    break;
                case "cursor":
                    vm.CursorPosition = 1;
                    vm.CursorPosition = 0;
                    break;
                case "delete":
                    vm.OnBytesDeleted(1, 1, 1);
                    break;
            }

            var lengthAfterInterruption = vm.Document!.Buffer.Length;
            vm.OnByteModified(0, 0xAB, 1, ByteInputPart.LowNibble);

            Assert.Equal(0xAB, vm.Document.Buffer.ReadByte(0));
            Assert.Equal(lengthAfterInterruption, vm.Document.Buffer.Length);

            vm.UndoCommand.Execute(null);

            Assert.Equal(0xA1, vm.Document.Buffer.ReadByte(0));
            Assert.Equal(lengthAfterInterruption, vm.Document.Buffer.Length);
        }
        finally
        {
            await vm.DisposeDocumentAsync();
        }
    }
}
