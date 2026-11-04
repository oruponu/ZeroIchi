using System.Text;
using Avalonia.Data;
using Avalonia.Input;
using ZeroIchi.Controls;
using ZeroIchi.ViewModels;
using static ZeroIchi.Tests.ViewModelTestFactory;

namespace ZeroIchi.Tests.Controls;

[Collection(HeadlessTestApp.Collection)]
public sealed class HexViewControlEditingTests : IDisposable
{
    static HexViewControlEditingTests() => Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);

    private readonly string _tempDir = Path.Combine(Path.GetTempPath(), "ZeroIchiTests_" + Guid.NewGuid().ToString("N"));

    public HexViewControlEditingTests() => Directory.CreateDirectory(_tempDir);

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
        control.Bind(HexViewControl.SelectionStartProperty,
            new ReflectionBinding(nameof(vm.SelectionStart)) { Source = vm, Mode = BindingMode.TwoWay });
        control.Bind(HexViewControl.SelectionLengthProperty,
            new ReflectionBinding(nameof(vm.SelectionLength)) { Source = vm, Mode = BindingMode.TwoWay });
        control.AddHandler(HexViewControl.ByteModifiedEvent,
            (_, e) => vm.OnByteModified(e.Index, e.Value, e.CursorPositionAfter, part: e.Part));
        control.AddHandler(HexViewControl.BytesDeletedEvent,
            (_, e) => vm.OnBytesDeleted(e.Index, e.Count, e.CursorPositionAfter));
        return (vm, control);
    }

    private static byte[] ReadAll(MainWindowViewModel vm) =>
        vm.Document!.Buffer.SliceToArray(0, (int)vm.Document.Buffer.Length);

    private static void Type(HexViewControl control, string text)
        => control.RaiseEvent(new TextInputEventArgs { RoutedEvent = InputElement.TextInputEvent, Text = text });

    [Fact]
    public Task Overwrite_TwoNibbles_SingleUndoRestoresByte() => HeadlessTestApp.RunAsync(async () =>
    {
        var (vm, control) = await OpenAsync([0x11, 0x22]);
        try
        {
            Type(control, "a");
            Type(control, "b");

            Assert.Equal([0xAB, 0x22], ReadAll(vm));
            Assert.Equal(1, vm.CursorPosition);

            vm.UndoCommand.Execute(null);

            Assert.Equal([0x11, 0x22], ReadAll(vm));
            Assert.False(vm.CanUndo);
        }
        finally
        {
            await vm.DisposeDocumentAsync();
        }
    });

    [Fact]
    public Task AtEnd_TwoNibbles_SingleUndoRemovesByte() => HeadlessTestApp.RunAsync(async () =>
    {
        var (vm, control) = await OpenAsync([0x11, 0x22]);
        try
        {
            vm.CursorPosition = 2;
            Type(control, "a");
            Type(control, "b");

            Assert.Equal([0x11, 0x22, 0xAB], ReadAll(vm));

            vm.UndoCommand.Execute(null);

            Assert.Equal([0x11, 0x22], ReadAll(vm));
        }
        finally
        {
            await vm.DisposeDocumentAsync();
        }
    });

    [Fact]
    public Task TwoDigitsInOneTextInput_WriteOneByte() => HeadlessTestApp.RunAsync(async () =>
    {
        var (vm, control) = await OpenAsync([0x11, 0x22]);
        try
        {
            Type(control, "ab");

            Assert.Equal([0xAB, 0x22], ReadAll(vm));
            Assert.Equal(1, vm.CursorPosition);

            vm.UndoCommand.Execute(null);

            Assert.Equal(0x11, vm.Document!.Buffer.ReadByte(0));
        }
        finally
        {
            await vm.DisposeDocumentAsync();
        }
    });
}
