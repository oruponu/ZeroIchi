using System.Text;
using ZeroIchi.Services;
using ZeroIchi.ViewModels;

namespace ZeroIchi.Tests;

public sealed class MainWindowViewModelClipboardTests : IDisposable
{
    static MainWindowViewModelClipboardTests() => Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);

    private readonly string _tempDir = Path.Combine(Path.GetTempPath(), "ZeroIchiTests_" + Guid.NewGuid().ToString("N"));

    public MainWindowViewModelClipboardTests() => Directory.CreateDirectory(_tempDir);

    public void Dispose() => Directory.Delete(_tempDir, recursive: true);

    private async Task<(MainWindowViewModel Vm, FakeClipboardService Clipboard)> OpenAsync(byte[] content)
    {
        var path = Path.Combine(_tempDir, Guid.NewGuid().ToString("N") + ".bin");
        File.WriteAllBytes(path, content);

        var clipboard = new FakeClipboardService();
        var vm = new MainWindowViewModel(new StubDialogService(), clipboard, new StubWindowService());
        await vm.OpenFileAsync(path);
        return (vm, clipboard);
    }

    private static byte[] ReadAll(MainWindowViewModel vm)
    {
        var buffer = vm.Document!.Buffer;
        var bytes = new byte[buffer.Length];
        buffer.ReadBytes(0, bytes, 0, bytes.Length);
        return bytes;
    }

    private sealed class FakeClipboardService : IClipboardService
    {
        public byte[]? Bytes { get; private set; }
        public string? Text { get; private set; }

        public void SetTextFromOtherApp(string text)
        {
            Bytes = null;
            Text = text;
        }

        public TaskCompletionSource? PendingWrite { get; set; }

        public async Task SetBytesAsync(byte[] bytes, string? text)
        {
            if (PendingWrite is not null)
                await PendingWrite.Task;
            Bytes = bytes;
            Text = text;
        }

        public Task<byte[]?> TryGetBytesAsync() => Task.FromResult(Bytes);
        public Task<string?> GetTextAsync() => Task.FromResult(Text);
    }

    private sealed class StubDialogService : IDialogService
    {
        public Task<string?> PickOpenFileAsync(string title) => Task.FromResult<string?>(null);
        public Task<string?> PickSaveFileAsync(string title, string? suggestedFileName) => Task.FromResult<string?>(null);
        public Task<SaveChangesResult> ShowSaveChangesDialogAsync() => Task.FromResult(SaveChangesResult.Discard);
        public Task ShowErrorAsync(string message, string detail) => Task.CompletedTask;
    }

    private sealed class StubWindowService : IWindowService
    {
        public void Close() { }
    }

    [Fact]
    public async Task Copy_WritesSelectedBytesAndHexText()
    {
        var (vm, clipboard) = await OpenAsync([0x00, 0x10, 0x20, 0xFF]);
        using var _ = vm.Document;
        vm.SelectionStart = 1;
        vm.SelectionLength = 3;

        await vm.CopyCommand.ExecuteAsync(null);

        Assert.Equal([0x10, 0x20, 0xFF], clipboard.Bytes);
        Assert.Equal("10 20 FF", clipboard.Text);
    }

    [Fact]
    public async Task Cut_WritesBytesAndRemovesSelection()
    {
        var (vm, clipboard) = await OpenAsync([0x00, 0x10, 0x20, 0xFF]);
        using var _ = vm.Document;
        vm.SelectionStart = 1;
        vm.SelectionLength = 2;

        await vm.CutCommand.ExecuteAsync(null);

        Assert.Equal([0x10, 0x20], clipboard.Bytes);
        Assert.Equal([0x00, 0xFF], ReadAll(vm));
    }

    [Fact]
    public async Task Cut_DeletesCopiedRange_WhenSelectionChangesDuringClipboardWrite()
    {
        var (vm, clipboard) = await OpenAsync([0x00, 0x10, 0x20, 0xFF]);
        using var _ = vm.Document;
        vm.SelectionStart = 1;
        vm.SelectionLength = 2;
        clipboard.PendingWrite = new TaskCompletionSource();

        var cut = vm.CutCommand.ExecuteAsync(null);
        vm.SelectionStart = 3;
        vm.SelectionLength = 1;
        clipboard.PendingWrite.SetResult();
        await cut;

        Assert.Equal([0x10, 0x20], clipboard.Bytes);
        Assert.Equal([0x00, 0xFF], ReadAll(vm));
    }

    [Fact]
    public async Task Cut_KeepsData_WhenDataChangesDuringClipboardWrite()
    {
        var (vm, clipboard) = await OpenAsync([0x00, 0x10, 0x20, 0xFF]);
        using var _ = vm.Document;
        vm.SelectionStart = 1;
        vm.SelectionLength = 2;
        clipboard.PendingWrite = new TaskCompletionSource();

        var cut = vm.CutCommand.ExecuteAsync(null);
        vm.OnByteModified(3, 0xEE, cursorPositionAfter: 4);
        clipboard.PendingWrite.SetResult();
        await cut;

        Assert.Equal([0x10, 0x20], clipboard.Bytes);
        Assert.Equal([0x00, 0x10, 0x20, 0xEE], ReadAll(vm));
    }

    [Fact]
    public async Task Cut_KeepsNewDocument_WhenDocumentIsReplacedDuringClipboardWrite()
    {
        var (vm, clipboard) = await OpenAsync([0x00, 0x10, 0x20, 0xFF]);
        var original = vm.Document;
        vm.SelectionStart = 1;
        vm.SelectionLength = 2;
        clipboard.PendingWrite = new TaskCompletionSource();

        var cut = vm.CutCommand.ExecuteAsync(null);
        var otherPath = Path.Combine(_tempDir, "other.bin");
        File.WriteAllBytes(otherPath, [0xA0, 0xA1, 0xA2, 0xA3]);
        await vm.OpenFileAsync(otherPath);
        using var _ = vm.Document;
        clipboard.PendingWrite.SetResult();
        await cut;

        Assert.NotSame(original, vm.Document);
        Assert.Equal([0xA0, 0xA1, 0xA2, 0xA3], ReadAll(vm));
    }

    [Fact]
    public void ToClipboardText_ReturnsHexText_UpToLimit()
    {
        Assert.Equal("01 02", MainWindowViewModel.ToClipboardText([0x01, 0x02], maxBytes: 2));
    }

    [Fact]
    public void ToClipboardText_ReturnsNull_WhenOverLimit()
    {
        Assert.Null(MainWindowViewModel.ToClipboardText([0x01, 0x02, 0x03], maxBytes: 2));
    }

    [Fact]
    public async Task Paste_PrefersBytesOverText()
    {
        var (vm, clipboard) = await OpenAsync([0xAA]);
        using var _ = vm.Document;
        await clipboard.SetBytesAsync([0x01, 0x02], "FF");

        await vm.PasteCommand.ExecuteAsync(null);

        Assert.Equal([0x01, 0x02, 0xAA], ReadAll(vm));
    }

    [Fact]
    public async Task Paste_FallsBackToHexText_WhenNoBytes()
    {
        var (vm, clipboard) = await OpenAsync([0xAA]);
        using var _ = vm.Document;
        clipboard.SetTextFromOtherApp("10 20");

        await vm.PasteCommand.ExecuteAsync(null);

        Assert.Equal([0x10, 0x20, 0xAA], ReadAll(vm));
    }

    [Fact]
    public async Task Paste_DoesNothing_WhenTextIsNotHex()
    {
        var (vm, clipboard) = await OpenAsync([0xAA]);
        using var _ = vm.Document;
        clipboard.SetTextFromOtherApp("hello");

        await vm.PasteCommand.ExecuteAsync(null);

        Assert.Equal([0xAA], ReadAll(vm));
    }

    [Fact]
    public async Task CopyThenPaste_InsertsCopiedBytes_EvenIfOtherAppLeftHexText()
    {
        var (vm, clipboard) = await OpenAsync([0x01, 0x02, 0x03]);
        using var _ = vm.Document;
        clipboard.SetTextFromOtherApp("ff");
        vm.SelectionStart = 1;
        vm.SelectionLength = 2;
        await vm.CopyCommand.ExecuteAsync(null);
        vm.CursorPosition = 0;

        await vm.PasteCommand.ExecuteAsync(null);

        Assert.Equal([0x02, 0x03, 0x01, 0x02, 0x03], ReadAll(vm));
    }
}
