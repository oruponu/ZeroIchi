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
        control.Bind(HexViewControl.IsInsertModeProperty, new ReflectionBinding(nameof(vm.IsInsertMode)) { Source = vm });
        control.AddHandler(HexViewControl.ByteModifiedEvent,
            (_, e) => vm.OnByteModified(e.Index, e.Value, e.CursorPositionAfter, e.IsInsert, e.Part));
        control.AddHandler(HexViewControl.BytesDeletedEvent,
            (_, e) => vm.OnBytesDeleted(e.Index, e.Count, e.CursorPositionAfter));
        return (vm, control);
    }

    private static byte[] ReadAll(MainWindowViewModel vm) =>
        vm.Document!.Buffer.SliceToArray(0, (int)vm.Document.Buffer.Length);

    private static void Type(HexViewControl control, string text)
        => control.RaiseEvent(new TextInputEventArgs { RoutedEvent = InputElement.TextInputEvent, Text = text });

    private static void PressKey(HexViewControl control, Key key, KeyModifiers modifiers = KeyModifiers.None)
        => control.RaiseEvent(new KeyEventArgs { RoutedEvent = InputElement.KeyDownEvent, Key = key, KeyModifiers = modifiers });

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
    public Task InsertMode_TwoNibbles_InsertsOneByte() => HeadlessTestApp.RunAsync(async () =>
    {
        var (vm, control) = await OpenAsync([0x11, 0x22]);
        try
        {
            vm.IsInsertMode = true;
            vm.CursorPosition = 1;

            Type(control, "a");

            Assert.Equal([0x11, 0xA0, 0x22], ReadAll(vm));
            Assert.Equal(1, vm.CursorPosition);

            Type(control, "b");

            Assert.Equal([0x11, 0xAB, 0x22], ReadAll(vm));
            Assert.Equal(2, vm.CursorPosition);

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
    public Task InsertMode_AtEnd_Appends() => HeadlessTestApp.RunAsync(async () =>
    {
        var (vm, control) = await OpenAsync([0x11, 0x22]);
        try
        {
            vm.IsInsertMode = true;
            vm.CursorPosition = 2;
            Type(control, "ab");

            Assert.Equal([0x11, 0x22, 0xAB], ReadAll(vm));
        }
        finally
        {
            await vm.DisposeDocumentAsync();
        }
    });

    [Fact]
    public Task InsertMode_TypeThenBackspace_RemovesTypedByte() => HeadlessTestApp.RunAsync(async () =>
    {
        var (vm, control) = await OpenAsync([0x11, 0x22]);
        try
        {
            vm.IsInsertMode = true;
            vm.CursorPosition = 1;
            Type(control, "ab");

            Assert.Equal([0x11, 0xAB, 0x22], ReadAll(vm));
            Assert.Equal(2, vm.CursorPosition);

            PressKey(control, Key.Back);

            Assert.Equal([0x11, 0x22], ReadAll(vm));
            Assert.Equal(1, vm.CursorPosition);
        }
        finally
        {
            await vm.DisposeDocumentAsync();
        }
    });

    [Fact]
    public Task InsertMode_HighNibbleThenBackspace_RemovesHalfTypedByte() => HeadlessTestApp.RunAsync(async () =>
    {
        var (vm, control) = await OpenAsync([0x11, 0x22, 0x33]);
        try
        {
            vm.IsInsertMode = true;
            vm.CursorPosition = 1;
            Type(control, "4");
            Assert.Equal([0x11, 0x40, 0x22, 0x33], ReadAll(vm));

            PressKey(control, Key.Back);

            Assert.Equal([0x11, 0x22, 0x33], ReadAll(vm));
            Assert.Equal(1, vm.CursorPosition);
        }
        finally
        {
            await vm.DisposeDocumentAsync();
        }
    });

    [Fact]
    public Task AtEnd_HighNibbleThenBackspace_RemovesHalfTypedByte() => HeadlessTestApp.RunAsync(async () =>
    {
        var (vm, control) = await OpenAsync([0x11, 0x22]);
        try
        {
            vm.CursorPosition = 2;
            Type(control, "a");
            Assert.Equal([0x11, 0x22, 0xA0], ReadAll(vm));

            PressKey(control, Key.Back);

            Assert.Equal([0x11, 0x22], ReadAll(vm));
            Assert.Equal(2, vm.CursorPosition);
        }
        finally
        {
            await vm.DisposeDocumentAsync();
        }
    });

    [Fact]
    public Task Overwrite_HighNibbleThenBackspace_DeletesPreviousByte() => HeadlessTestApp.RunAsync(async () =>
    {
        var (vm, control) = await OpenAsync([0x11, 0x22, 0x33]);
        try
        {
            vm.CursorPosition = 1;
            Type(control, "4");
            Assert.Equal([0x11, 0x42, 0x33], ReadAll(vm));

            PressKey(control, Key.Back);

            Assert.Equal([0x42, 0x33], ReadAll(vm));
            Assert.Equal(0, vm.CursorPosition);
        }
        finally
        {
            await vm.DisposeDocumentAsync();
        }
    });

    [Fact]
    public Task SwitchingToInsertAfterHighNibble_StartsNewByte() => HeadlessTestApp.RunAsync(async () =>
    {
        var (vm, control) = await OpenAsync([0x11, 0x22]);
        try
        {
            Type(control, "a");
            Assert.Equal([0xA1, 0x22], ReadAll(vm));

            vm.IsInsertMode = true;
            Type(control, "b");

            Assert.Equal([0xB0, 0xA1, 0x22], ReadAll(vm));

            vm.UndoCommand.Execute(null);
            Assert.Equal([0xA1, 0x22], ReadAll(vm));

            vm.UndoCommand.Execute(null);
            Assert.Equal([0x11, 0x22], ReadAll(vm));
        }
        finally
        {
            await vm.DisposeDocumentAsync();
        }
    });

    [Fact]
    public Task SwitchingToOverwriteAfterHighNibble_StartsNewByte() => HeadlessTestApp.RunAsync(async () =>
    {
        var (vm, control) = await OpenAsync([0x11, 0x22]);
        try
        {
            vm.IsInsertMode = true;
            Type(control, "a");
            Assert.Equal([0xA0, 0x11, 0x22], ReadAll(vm));

            vm.IsInsertMode = false;
            Type(control, "b");

            Assert.Equal([0xB0, 0x11, 0x22], ReadAll(vm));

            vm.UndoCommand.Execute(null);
            Assert.Equal([0xA0, 0x11, 0x22], ReadAll(vm));
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

    [Fact]
    public Task AsciiPane_TypesPrintableChars_OneUndoStepEach() => HeadlessTestApp.RunAsync(async () =>
    {
        var (vm, control) = await OpenAsync([0x11, 0x22, 0x33]);
        try
        {
            PressKey(control, Key.Tab);
            Type(control, "Hi");

            Assert.Equal([0x48, 0x69, 0x33], ReadAll(vm));
            Assert.Equal(2, vm.CursorPosition);

            vm.UndoCommand.Execute(null);

            Assert.Equal([0x48, 0x22, 0x33], ReadAll(vm));
        }
        finally
        {
            await vm.DisposeDocumentAsync();
        }
    });

    [Fact]
    public Task AsciiPane_IgnoresNonAsciiAndControlChars() => HeadlessTestApp.RunAsync(async () =>
    {
        var (vm, control) = await OpenAsync([0x11, 0x22, 0x33]);
        try
        {
            PressKey(control, Key.Tab);
            Type(control, "日");
            Type(control, "\u007F");
            Type(control, "\u0001");

            Assert.Equal([0x11, 0x22, 0x33], ReadAll(vm));
            Assert.False(vm.Document!.IsModified);
            Assert.Equal(0, vm.CursorPosition);
        }
        finally
        {
            await vm.DisposeDocumentAsync();
        }
    });

    [Fact]
    public Task AsciiPane_MixedText_SkipsOnlyUnsupportedChars() => HeadlessTestApp.RunAsync(async () =>
    {
        var (vm, control) = await OpenAsync([0x11, 0x22, 0x33]);
        try
        {
            PressKey(control, Key.Tab);
            Type(control, "a日b");

            Assert.Equal([0x61, 0x62, 0x33], ReadAll(vm));
            Assert.Equal(2, vm.CursorPosition);
        }
        finally
        {
            await vm.DisposeDocumentAsync();
        }
    });

    [Fact]
    public Task AsciiPane_InsertMode_InsertsByte() => HeadlessTestApp.RunAsync(async () =>
    {
        var (vm, control) = await OpenAsync([0x11, 0x22, 0x33]);
        try
        {
            vm.IsInsertMode = true;
            vm.CursorPosition = 1;
            PressKey(control, Key.Tab);
            Type(control, "A");

            Assert.Equal([0x11, 0x41, 0x22, 0x33], ReadAll(vm));
            Assert.Equal(2, vm.CursorPosition);
        }
        finally
        {
            await vm.DisposeDocumentAsync();
        }
    });

    [Fact]
    public Task AsciiPane_AtEnd_Appends() => HeadlessTestApp.RunAsync(async () =>
    {
        var (vm, control) = await OpenAsync([0x11, 0x22, 0x33]);
        try
        {
            vm.CursorPosition = 3;
            PressKey(control, Key.Tab);
            Type(control, "z");

            Assert.Equal([0x11, 0x22, 0x33, 0x7A], ReadAll(vm));
            Assert.Equal(4, vm.CursorPosition);
        }
        finally
        {
            await vm.DisposeDocumentAsync();
        }
    });

    [Fact]
    public Task AsciiPane_ReadOnly_IgnoresInputButSwitchesPane() => HeadlessTestApp.RunAsync(async () =>
    {
        var (vm, control) = await OpenAsync([0x11, 0x22, 0x33]);
        try
        {
            control.IsReadOnly = true;
            PressKey(control, Key.Tab);

            Assert.Equal(HexViewPane.Ascii, control.ActivePane);

            Type(control, "a");

            Assert.Equal([0x11, 0x22, 0x33], ReadAll(vm));
        }
        finally
        {
            await vm.DisposeDocumentAsync();
        }
    });

    [Fact]
    public Task Tab_DropsHalfTypedByte() => HeadlessTestApp.RunAsync(async () =>
    {
        var (vm, control) = await OpenAsync([0x11, 0x22]);
        try
        {
            Type(control, "a");
            Assert.Equal(0xA1, vm.Document!.Buffer.ReadByte(0));

            PressKey(control, Key.Tab);
            PressKey(control, Key.Tab);
            Type(control, "b");

            Assert.Equal(0xB1, vm.Document.Buffer.ReadByte(0));
        }
        finally
        {
            await vm.DisposeDocumentAsync();
        }
    });

    [Fact]
    public Task ShiftTab_TogglesPane() => HeadlessTestApp.RunAsync(async () =>
    {
        var (vm, control) = await OpenAsync([0x11, 0x22]);
        try
        {
            PressKey(control, Key.Tab, KeyModifiers.Shift);
            Assert.Equal(HexViewPane.Ascii, control.ActivePane);

            PressKey(control, Key.Tab, KeyModifiers.Shift);
            Assert.Equal(HexViewPane.Hex, control.ActivePane);
        }
        finally
        {
            await vm.DisposeDocumentAsync();
        }
    });
}
