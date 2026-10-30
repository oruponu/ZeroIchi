using System.Text;
using Avalonia.Data;
using Avalonia.Input;
using ZeroIchi.Controls;
using ZeroIchi.ViewModels;
using static ZeroIchi.Tests.ViewModelTestFactory;

namespace ZeroIchi.Tests.Controls;

[Collection(HeadlessTestApp.Collection)]
public sealed class HexViewControlReadOnlyTests : IDisposable
{
    static HexViewControlReadOnlyTests() => Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);

    private readonly string _tempDir = Path.Combine(Path.GetTempPath(), "ZeroIchiTests_" + Guid.NewGuid().ToString("N"));

    public HexViewControlReadOnlyTests() => Directory.CreateDirectory(_tempDir);

    public void Dispose() => Directory.Delete(_tempDir, recursive: true);

    private async Task<(MainWindowViewModel Vm, HexViewControl Control)> OpenAsync(byte[] content)
    {
        var path = Path.Combine(_tempDir, Guid.NewGuid().ToString("N") + ".bin");
        File.WriteAllBytes(path, content);

        var vm = CreateViewModel();
        await vm.OpenFileAsync(path);

        var control = new HexViewControl();
        control.Bind(HexViewControl.DocumentProperty, new ReflectionBinding(nameof(vm.Document)) { Source = vm });
        control.Bind(HexViewControl.DataVersionProperty, new ReflectionBinding(nameof(vm.DataVersion)) { Source = vm });
        control.Bind(HexViewControl.CursorPositionProperty,
            new ReflectionBinding(nameof(vm.CursorPosition)) { Source = vm, Mode = BindingMode.TwoWay });
        control.AddHandler(HexViewControl.ByteModifiedEvent,
            (_, e) => vm.OnByteModified(e.Index, e.Value, e.CursorPositionAfter));
        control.AddHandler(HexViewControl.BytesDeletedEvent,
            (_, e) => vm.OnBytesDeleted(e.Index, e.Count, e.CursorPositionAfter));
        return (vm, control);
    }

    private static void Type(HexViewControl control, string text)
        => control.RaiseEvent(new TextInputEventArgs { RoutedEvent = InputElement.TextInputEvent, Text = text });

    private static void PressKey(HexViewControl control, Key key)
        => control.RaiseEvent(new KeyEventArgs { RoutedEvent = InputElement.KeyDownEvent, Key = key });

    [Fact]
    public Task ReadOnly_IgnoresHexDigitsAndDeleteButMovesTheCursor() => HeadlessTestApp.RunAsync(async () =>
    {
        var (vm, control) = await OpenAsync([0x11, 0x22, 0x33]);
        try
        {
            control.IsReadOnly = true;
            Type(control, "a");
            PressKey(control, Key.Delete);
            PressKey(control, Key.Back);
            PressKey(control, Key.Right);

            Assert.Equal([0x11, 0x22, 0x33], vm.Document!.Buffer.SliceToArray(0, 3));
            Assert.False(vm.Document.IsModified);
            Assert.Equal(1, vm.CursorPosition);
        }
        finally
        {
            await vm.DisposeDocumentAsync();
        }
    });

    [Fact]
    public Task TurningReadOnlyOn_DropsTheHalfTypedByte() => HeadlessTestApp.RunAsync(async () =>
    {
        var (vm, control) = await OpenAsync([0x11, 0x22, 0x33]);
        try
        {
            Type(control, "a");
            Assert.Equal(0xA1, vm.Document!.Buffer.ReadByte(0));

            control.IsReadOnly = true;
            control.IsReadOnly = false;
            Type(control, "b");

            Assert.Equal(0xB1, vm.Document.Buffer.ReadByte(0));
            Assert.Equal(0, vm.CursorPosition);
        }
        finally
        {
            await vm.DisposeDocumentAsync();
        }
    });
}
