using ZeroIchi.ViewModels;
using static ZeroIchi.Tests.ViewModels.MainWindowViewModelStructureUpdateTests;

namespace ZeroIchi.Tests.ViewModels;

[Collection(HeadlessTestApp.Collection)]
public sealed class MainWindowViewModelStructureSaveTests : IDisposable
{
    private readonly string _tempDir = Path.Combine(Path.GetTempPath(), "ZeroIchiTests_" + Guid.NewGuid().ToString("N"));

    public MainWindowViewModelStructureSaveTests() => Directory.CreateDirectory(_tempDir);

    public void Dispose() => Directory.Delete(_tempDir, recursive: true);

    private string Write(string name, byte[] content)
    {
        var path = Path.Combine(_tempDir, name);
        File.WriteAllBytes(path, content);
        return path;
    }

    [Fact]
    public Task Save_WhileACancelledParseIsStillRunning_WaitsForIt() => HeadlessTestApp.RunAsync(async () =>
    {
        var gate = new GateNative(honorsCancellation: false);
        var vm = Create(Registry(gate, GateYaml), TimeSpan.Zero);
        var path = Write("a.bin", "ZI\u0005"u8.ToArray());
        await vm.OpenFileAsync(path);
        try
        {
            await WaitUntil(() => gate.Entered.IsSet);
            vm.OnByteModified(2, 9, cursorPositionAfter: 2);

            var save = vm.SaveFileCommand.ExecuteAsync(null);
            await Task.Delay(100);
            vm.OnByteModified(2, 10, cursorPositionAfter: 2);
            await Task.Delay(100);

            Assert.False(save.IsCompleted);
            Assert.Equal("ZI\u0005"u8.ToArray(), await File.ReadAllBytesAsync(path));
            Assert.Equal(1, gate.Calls);

            gate.Release.Set();
            await save;
            await vm.StructureUpdate;

            Assert.Equal("ZI\u000A"u8.ToArray(), await File.ReadAllBytesAsync(path));
            Assert.Equal("10", Len(vm));
            Assert.Equal(2, gate.Calls);
        }
        finally
        {
            gate.Release.Set();
            await vm.DisposeDocumentAsync();
        }
    });

    [Fact]
    public Task OpenAnotherFile_WhileSaveIsWaiting_DoesNotSaveTheDisposedDocument() => HeadlessTestApp.RunAsync(async () =>
    {
        var gate = new GateNative(honorsCancellation: false);
        var vm = Create(Registry(gate, GateYaml, OtherYamlForSave), TimeSpan.Zero);
        var path = Write("a.bin", "ZI\u0005"u8.ToArray());
        await vm.OpenFileAsync(path);
        try
        {
            await WaitUntil(() => gate.Entered.IsSet);
            vm.OnByteModified(2, 9, cursorPositionAfter: 2);

            var save = vm.SaveFileCommand.ExecuteAsync(null);
            var open = vm.OpenFileAsync(Write("b.bin", "ZJ\u0001"u8.ToArray()));
            gate.Release.Set();
            await Task.WhenAll(save, open);
            await vm.StructureUpdate;

            Assert.Equal("b.bin", vm.Document!.FileName);
            Assert.Equal("magic,other", Names(vm));
            var saved = await File.ReadAllBytesAsync(path);
            Assert.True(saved.SequenceEqual("ZI\u0005"u8.ToArray()) || saved.SequenceEqual("ZI\u0009"u8.ToArray()));
        }
        finally
        {
            gate.Release.Set();
            await vm.DisposeDocumentAsync();
        }
    });

    private const string OtherYamlForSave = """
        meta:
          id: zj
        seq:
          - id: magic
            contents: ZJ
          - id: other
            type: u1
        """;

    [Fact]
    public Task SaveFailure_ResumesTheUpdates() => HeadlessTestApp.RunAsync(async () =>
    {
        var gate = new GateNative();
        gate.Release.Set();
        var vm = Create(Registry(gate, GateYaml), TimeSpan.FromSeconds(30));
        var path = Write("a.bin", "ZI\u0005"u8.ToArray());
        await vm.OpenFileAsync(path);
        await vm.StructureUpdate;
        try
        {
            vm.OnByteModified(2, 9, cursorPositionAfter: 2);

            using (new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
            {
                await vm.SaveFileCommand.ExecuteAsync(null);
            }
            var update = vm.StructureUpdate;
            Assert.Same(update, await Task.WhenAny(update, Task.Delay(TimeSpan.FromSeconds(5))));

            Assert.True(vm.Document!.IsModified);
            Assert.Equal("9", Len(vm));
        }
        finally
        {
            await vm.DisposeDocumentAsync();
        }
    });

    [Fact]
    public Task NestedPauses_ResumeOnlyAfterTheLastOne() => HeadlessTestApp.RunAsync(async () =>
    {
        var gate = new GateNative();
        gate.Release.Set();
        var vm = Create(Registry(gate, GateYaml), TimeSpan.Zero);
        await vm.OpenFileAsync(Write("a.bin", "ZI\u0005"u8.ToArray()));
        await vm.StructureUpdate;
        try
        {
            await vm.PauseStructureUpdatesAsync();
            await vm.PauseStructureUpdatesAsync();
            vm.OnByteModified(2, 9, cursorPositionAfter: 2);

            vm.ResumeStructureUpdates();
            await Task.Delay(50);
            Assert.Equal((1, "5"), (gate.Calls, Len(vm)));

            vm.ResumeStructureUpdates();
            await vm.StructureUpdate;
            Assert.Equal((2, "9"), (gate.Calls, Len(vm)));
        }
        finally
        {
            await vm.DisposeDocumentAsync();
        }
    });

    [Fact]
    public Task SaveAs_WithANewExtension_RedetectsTheFormat() => HeadlessTestApp.RunAsync(async () =>
    {
        const string yaml = "meta:\n  id: ext\n  file-extension: zz\nseq:\n  - id: first\n    type: u1\n";
        var dialog = new MainWindowViewModelStructureUpdateTests.StubDialogService();
        var vm = Create(Registry(null, yaml), TimeSpan.Zero, dialog);
        await vm.OpenFileAsync(Write("a.bin", [7, 8]));
        await vm.StructureUpdate;
        try
        {
            Assert.Empty(vm.StructureTreeItems);
            dialog.SavePath = Path.Combine(_tempDir, "b.zz");

            await vm.SaveAsFileCommand.ExecuteAsync(null);
            await vm.StructureUpdate;

            Assert.Equal("first", Names(vm));
        }
        finally
        {
            await vm.DisposeDocumentAsync();
        }
    });
}
