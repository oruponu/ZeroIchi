using System.Text;
using Avalonia.Data;
using Avalonia.Input;
using ZeroIchi.Controls;
using ZeroIchi.ViewModels;
using static ZeroIchi.Tests.ViewModelTestFactory;

namespace ZeroIchi.Tests.Controls;

[Collection(HeadlessTestApp.Collection)]
public sealed class HexViewControlNibbleStateTests : IDisposable
{
    static HexViewControlNibbleStateTests() => Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);

    private readonly string _tempDir = Path.Combine(Path.GetTempPath(), "ZeroIchiTests_" + Guid.NewGuid().ToString("N"));

    public HexViewControlNibbleStateTests() => Directory.CreateDirectory(_tempDir);

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
        return (vm, control);
    }

    private static void Type(HexViewControl control, string text)
        => control.RaiseEvent(new TextInputEventArgs { RoutedEvent = InputElement.TextInputEvent, Text = text });

    [Fact]
    public Task HexInput_WritesHighThenLowNibble() => HeadlessTestApp.RunAsync(async () =>
    {
        var (vm, control) = await OpenAsync([0x11, 0x22]);
        try
        {
            Type(control, "a");
            Type(control, "b");

            Assert.Equal(0xAB, vm.Document!.Buffer.ReadByte(0));
            Assert.Equal(1, vm.CursorPosition);
        }
        finally
        {
            await vm.DisposeDocumentAsync();
        }
    });

    [Fact]
    public Task HexInput_AfterCursorMovedExternally_StartsFromHighNibble() => HeadlessTestApp.RunAsync(async () =>
    {
        var (vm, control) = await OpenAsync([0x11, 0x22, 0x33]);
        try
        {
            Type(control, "a");
            vm.CursorPosition = 2;
            Type(control, "b");

            Assert.Equal(0xA1, vm.Document!.Buffer.ReadByte(0));
            Assert.Equal(0xB3, vm.Document.Buffer.ReadByte(2));
        }
        finally
        {
            await vm.DisposeDocumentAsync();
        }
    });

    [Fact]
    public Task HexInput_AfterUndo_StartsFromHighNibble() => HeadlessTestApp.RunAsync(async () =>
    {
        var (vm, control) = await OpenAsync([0x11, 0x22]);
        try
        {
            Type(control, "a");
            vm.UndoCommand.Execute(null);
            Type(control, "b");

            Assert.Equal(0xB1, vm.Document!.Buffer.ReadByte(0));
        }
        finally
        {
            await vm.DisposeDocumentAsync();
        }
    });
}
