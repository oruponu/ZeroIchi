using System.Text;
using Avalonia;
using Avalonia.Controls.Primitives;
using Avalonia.Headless;
using Avalonia.Threading;
using ZeroIchi.Views;
using static ZeroIchi.Tests.ViewModelTestFactory;

namespace ZeroIchi.Tests.Controls;

[Collection(HeadlessTestApp.Collection)]
public sealed class HexViewControlHoverTests : IDisposable
{
    static HexViewControlHoverTests() => Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);

    private readonly string _tempDir = Path.Combine(Path.GetTempPath(), "ZeroIchiTests_" + Guid.NewGuid().ToString("N"));

    public HexViewControlHoverTests() => Directory.CreateDirectory(_tempDir);

    public void Dispose() => Directory.Delete(_tempDir, recursive: true);

    [Fact]
    public Task Scrolling_MovesTheHoverToTheCellUnderThePointer() => HeadlessTestApp.RunAsync(async () =>
    {
        var path = Path.Combine(_tempDir, Guid.NewGuid().ToString("N") + ".bin");
        File.WriteAllBytes(path, new byte[0x10000]);
        var vm = CreateViewModel();
        await vm.OpenFileAsync(path);
        var window = new MainWindow { DataContext = vm, Width = 900, Height = 400 };
        window.Show();
        Dispatcher.UIThread.RunJobs();
        try
        {
            var hexView = window.HexView;
            window.MouseMove(hexView.TranslatePoint(new Point(150, 60), window)!.Value);
            Dispatcher.UIThread.RunJobs();
            var hovered = hexView.HoveredByteIndex;
            Assert.True(hovered >= 0);

            var rowHeight = ((ILogicalScrollable)hexView).ScrollSize.Height;
            ((IScrollable)hexView).Offset = new Vector(0, rowHeight * 3);
            Dispatcher.UIThread.RunJobs();

            Assert.Equal(hovered + 3 * 16, hexView.HoveredByteIndex);
        }
        finally
        {
            await vm.DisposeDocumentAsync();
            window.Close();
        }
    });
}
