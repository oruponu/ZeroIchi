using System.Text;
using Avalonia.Controls.Primitives;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.Threading;
using ZeroIchi.Services;
using ZeroIchi.ViewModels;
using ZeroIchi.Views;
using static ZeroIchi.Tests.ViewModelTestFactory;

namespace ZeroIchi.Tests.Controls;

[Collection(HeadlessTestApp.Collection)]
public sealed class HexViewControlCursorVisibilityTests : IDisposable
{
    static HexViewControlCursorVisibilityTests() => Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);

    private readonly string _tempDir = Path.Combine(Path.GetTempPath(), "ZeroIchiTests_" + Guid.NewGuid().ToString("N"));

    public HexViewControlCursorVisibilityTests() => Directory.CreateDirectory(_tempDir);

    public void Dispose() => Directory.Delete(_tempDir, recursive: true);

    private sealed class FixedClipboardService(byte[] bytes) : IClipboardService
    {
        public Task SetBytesAsync(byte[] value, string? text) => Task.CompletedTask;
        public Task<byte[]?> TryGetBytesAsync() => Task.FromResult<byte[]?>(bytes);
        public Task<string?> GetTextAsync() => Task.FromResult<string?>(null);
    }

    [Fact]
    public Task PasteAtTheEnd_ScrollsToTheMovedCursor() => HeadlessTestApp.RunAsync(async () =>
    {
        var path = Path.Combine(_tempDir, Guid.NewGuid().ToString("N") + ".bin");
        File.WriteAllBytes(path, new byte[0x1000]);
        var vm = CreateViewModel(clipboard: new FixedClipboardService(new byte[0x100]));
        await vm.OpenFileAsync(path);
        var window = new MainWindow { DataContext = vm, Width = 900, Height = 400 };
        window.Show();
        Dispatcher.UIThread.RunJobs();
        try
        {
            window.KeyPress(Key.End, RawInputModifiers.Control, PhysicalKey.None, null);
            Dispatcher.UIThread.RunJobs();

            await vm.PasteCommand.ExecuteAsync(null);
            Dispatcher.UIThread.RunJobs();

            var scrollable = (IScrollable)window.HexView;
            Assert.Equal(0x1100, vm.CursorPosition);
            Assert.Equal(scrollable.Extent.Height - scrollable.Viewport.Height, scrollable.Offset.Y);
        }
        finally
        {
            await vm.DisposeDocumentAsync();
            window.Close();
        }
    });
}
