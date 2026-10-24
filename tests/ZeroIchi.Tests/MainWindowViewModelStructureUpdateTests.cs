using System.Text;
using ZeroIchi.Models.FileStructure.Ksy;
using ZeroIchi.Models.FileStructure.Ksy.Expressions;
using ZeroIchi.Models.FileStructure.Ksy.Model;
using ZeroIchi.Models.FileStructure.Ksy.Runtime;
using ZeroIchi.Services;
using ZeroIchi.ViewModels;

namespace ZeroIchi.Tests;

[Collection(HeadlessTestApp.Collection)]
public sealed class MainWindowViewModelStructureUpdateTests : IDisposable
{
    static MainWindowViewModelStructureUpdateTests() => Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);

    internal sealed class GateNative(bool honorsCancellation = true) : IKsyNativeType
    {
        private int _calls;

        public string Name => "gate";
        public ManualResetEventSlim Release { get; } = new(initialState: false);
        public ManualResetEventSlim Entered { get; } = new(initialState: false);
        public int Calls => Volatile.Read(ref _calls);

        public KsyNativeResult Read(KsStream stream, IReadOnlyList<KsValue> arguments)
        {
            Interlocked.Increment(ref _calls);
            Entered.Set();
            var start = stream.Position;
            if (honorsCancellation)
            {
                while (!Release.Wait(5))
                {
                    stream.ReadInteger(1, false, true);
                    stream.Seek(start);
                }
            }
            else
            {
                Release.Wait();
            }
            stream.ReadInteger(1, false, true);
            stream.Seek(start);
            return new KsyNativeResult(0, []);
        }
    }

    internal const string GateYaml = """
        meta:
          id: zi
          ks-opaque-types: true
        seq:
          - id: magic
            contents: ZI
          - id: wait
            type: gate
          - id: len
            type: u1
        """;

    private const string NestedYaml = """
        meta:
          id: zi
        seq:
          - id: magic
            contents: ZI
          - id: hdr
            type: hdr
        types:
          hdr:
            seq:
              - id: len
                type: u1
        """;

    private const string OtherYaml = """
        meta:
          id: zj
        seq:
          - id: magic
            contents: ZJ
          - id: other
            type: u1
        """;

    private readonly string _tempDir = Path.Combine(Path.GetTempPath(), "ZeroIchiTests_" + Guid.NewGuid().ToString("N"));

    public MainWindowViewModelStructureUpdateTests() => Directory.CreateDirectory(_tempDir);

    public void Dispose() => Directory.Delete(_tempDir, recursive: true);

    internal static KsyFormatRegistry Registry(GateNative? gate, params string[] yamls)
    {
        var natives = new KsyNativeTypeRegistry();
        if (gate is not null) natives.Register(gate);
        var registry = new KsyFormatRegistry();
        foreach (var yaml in yamls)
            registry.Register(KsyModelBuilder.Parse(yaml, "main", null, natives).Format!);
        return registry;
    }

    internal string Write(string name, byte[] content)
    {
        var path = Path.Combine(_tempDir, name);
        File.WriteAllBytes(path, content);
        return path;
    }

    internal static MainWindowViewModel Create(KsyFormatRegistry registry, TimeSpan? debounce = null,
        StubDialogService? dialog = null) =>
        new(dialog ?? new StubDialogService(), new StubClipboardService(), new StubWindowService())
        {
            KsyFormats = registry,
            StructureDebounce = debounce ?? TimeSpan.FromMilliseconds(250),
        };

    internal static string Names(MainWindowViewModel vm) => string.Join(",", vm.StructureTreeItems.Select(i => i.Name));

    internal static string Len(MainWindowViewModel vm) => vm.StructureTreeItems.Single(i => i.Name == "len").Description;

    internal static async Task WaitUntil(Func<bool> condition)
    {
        var deadline = DateTime.UtcNow.AddSeconds(5);
        while (!condition())
        {
            Assert.True(DateTime.UtcNow < deadline, "Timed out");
            await Task.Delay(5);
        }
    }

    [Fact]
    public Task Edits_InQuickSuccession_ParseOnlyOnce() => HeadlessTestApp.RunAsync(async () =>
    {
        var gate = new GateNative();
        gate.Release.Set();
        var vm = Create(Registry(gate, GateYaml), TimeSpan.FromMilliseconds(200));
        await vm.OpenFileAsync(Write("a.bin", "ZI\u0005"u8.ToArray()));
        await vm.StructureUpdate;
        try
        {
            vm.OnByteModified(2, 6, cursorPositionAfter: 2);
            vm.OnByteModified(2, 7, cursorPositionAfter: 2);
            vm.OnByteModified(2, 8, cursorPositionAfter: 2);
            await vm.StructureUpdate;

            Assert.Equal(2, gate.Calls);
            Assert.Equal("8", Len(vm));
        }
        finally
        {
            await vm.DisposeDocumentAsync();
        }
    });

    [Fact]
    public Task Edit_WhileParsing_DoesNotApplyTheStaleResult() => HeadlessTestApp.RunAsync(async () =>
    {
        var gate = new GateNative();
        var vm = Create(Registry(gate, GateYaml), TimeSpan.Zero);
        await vm.OpenFileAsync(Write("a.bin", "ZI\u0005"u8.ToArray()));
        try
        {
            await WaitUntil(() => gate.Entered.IsSet);
            Assert.Empty(vm.StructureTreeItems);

            vm.OnByteModified(2, 9, cursorPositionAfter: 2);
            gate.Release.Set();
            await vm.StructureUpdate;

            Assert.Equal("9", Len(vm));
        }
        finally
        {
            await vm.DisposeDocumentAsync();
        }
    });

    [Fact]
    public Task OpenAnotherFile_WhileParsing_ShowsOnlyTheNewFile() => HeadlessTestApp.RunAsync(async () =>
    {
        var gate = new GateNative();
        var vm = Create(Registry(gate, GateYaml, OtherYaml));
        await vm.OpenFileAsync(Write("a.bin", "ZI\u0005"u8.ToArray()));
        try
        {
            await WaitUntil(() => gate.Entered.IsSet);

            await vm.OpenFileAsync(Write("b.bin", "ZJ\u0001"u8.ToArray()));
            await vm.StructureUpdate;

            Assert.Equal("magic,other", Names(vm));
            Assert.False(gate.Release.IsSet);
        }
        finally
        {
            await vm.DisposeDocumentAsync();
        }
    });

    [Fact]
    public Task Reparse_ResyncsTheSelectionToTheCursor() => HeadlessTestApp.RunAsync(async () =>
    {
        var gate = new GateNative();
        gate.Release.Set();
        var vm = Create(Registry(gate, GateYaml), TimeSpan.Zero);
        await vm.OpenFileAsync(Write("a.bin", "ZI\u0005"u8.ToArray()));
        await vm.StructureUpdate;
        try
        {
            vm.CursorPosition = 2;
            vm.OnByteModified(2, 9, cursorPositionAfter: 2);
            await vm.StructureUpdate;

            Assert.Equal("len", vm.SelectedStructureItem?.Name);
            Assert.Contains(vm.SelectedStructureItem!, vm.StructureTreeItems);
        }
        finally
        {
            await vm.DisposeDocumentAsync();
        }
    });

    [Fact]
    public Task SelectingAStaleNodePastTheEnd_ClampsTheSelection() => HeadlessTestApp.RunAsync(async () =>
    {
        var gate = new GateNative();
        gate.Release.Set();
        var vm = Create(Registry(gate, GateYaml), TimeSpan.FromSeconds(30));
        await vm.OpenFileAsync(Write("a.bin", "ZI\u0005"u8.ToArray()));
        await vm.StructureUpdate;
        try
        {
            vm.OnBytesDeleted(0, 2, cursorPositionAfter: 0);

            vm.SelectedStructureItem = vm.StructureTreeItems.Single(i => i.Name == "len");

            Assert.Equal((1L, 1L, 0L), (vm.CursorPosition, vm.SelectionStart, vm.SelectionLength));
        }
        finally
        {
            await vm.DisposeDocumentAsync();
        }
    });

    [Fact]
    public Task DeletingEverything_ClearsTheTreeWithoutWaiting() => HeadlessTestApp.RunAsync(async () =>
    {
        var gate = new GateNative();
        gate.Release.Set();
        var vm = Create(Registry(gate, GateYaml), TimeSpan.FromSeconds(30));
        await vm.OpenFileAsync(Write("a.bin", "ZI\u0005"u8.ToArray()));
        await vm.StructureUpdate;
        try
        {
            vm.OnBytesDeleted(0, 3, cursorPositionAfter: 0);

            Assert.Empty(vm.StructureTreeItems);
            Assert.Null(vm.StructureColors);
        }
        finally
        {
            await vm.DisposeDocumentAsync();
        }
    });

    [Fact]
    public Task Reparse_KeepsExpandedNodes() => HeadlessTestApp.RunAsync(async () =>
    {
        var vm = Create(Registry(null, NestedYaml), TimeSpan.Zero);
        await vm.OpenFileAsync(Write("a.bin", "ZI\u0005"u8.ToArray()));
        await vm.StructureUpdate;
        try
        {
            vm.ToggleExpandCommand.Execute(vm.StructureTreeItems.Single(i => i.Name == "hdr"));
            vm.OnByteModified(2, 9, cursorPositionAfter: 2);
            await vm.StructureUpdate;
            Assert.Equal("magic,hdr,len", Names(vm));

            await vm.OpenFileAsync(Write("b.bin", "ZI\u0007"u8.ToArray()));
            await vm.StructureUpdate;
            Assert.Equal("magic,hdr,len", Names(vm));
        }
        finally
        {
            await vm.DisposeDocumentAsync();
        }
    });

    [Fact]
    public Task UnexpectedFailure_ClearsTheTreeWithoutThrowing() => HeadlessTestApp.RunAsync(async () =>
    {
        var vm = new MainWindowViewModel(new StubDialogService(), new StubClipboardService(), new StubWindowService())
        {
            KsyFormats = new KsyFormatRegistry(() => throw new InvalidOperationException("broken")),
        };
        await vm.OpenFileAsync(Write("a.bin", [1, 2, 3]));
        try
        {
            await vm.StructureUpdate;

            Assert.Empty(vm.StructureTreeItems);
            Assert.Null(vm.StructureColors);
        }
        finally
        {
            await vm.DisposeDocumentAsync();
        }
    });

    [Fact]
    public Task SlowParse_ShowsTheBusyIndicatorUntilItEnds() => HeadlessTestApp.RunAsync(async () =>
    {
        var gate = new GateNative();
        var vm = new MainWindowViewModel(new StubDialogService(), new StubClipboardService(), new StubWindowService())
        {
            KsyFormats = Registry(gate, GateYaml),
            StructureBusyDelay = TimeSpan.Zero,
        };
        await vm.OpenFileAsync(Write("a.bin", "ZI\u0005"u8.ToArray()));
        try
        {
            await WaitUntil(() => vm.IsStructureParsing);

            gate.Release.Set();
            await vm.StructureUpdate;

            Assert.False(vm.IsStructureParsing);
        }
        finally
        {
            gate.Release.Set();
            await vm.DisposeDocumentAsync();
        }
    });

    [Fact]
    public Task FastParse_NeverShowsTheBusyIndicator() => HeadlessTestApp.RunAsync(async () =>
    {
        var gate = new GateNative();
        gate.Release.Set();
        var vm = Create(Registry(gate, GateYaml), TimeSpan.Zero);
        var shown = false;
        vm.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(MainWindowViewModel.IsStructureParsing) && vm.IsStructureParsing) shown = true;
        };
        await vm.OpenFileAsync(Write("a.bin", "ZI\u0005"u8.ToArray()));
        try
        {
            await vm.StructureUpdate;
            vm.OnByteModified(2, 9, cursorPositionAfter: 2);
            await vm.StructureUpdate;

            Assert.False(shown);
        }
        finally
        {
            await vm.DisposeDocumentAsync();
        }
    });

    internal sealed class StubDialogService : IDialogService
    {
        public string? SavePath { get; set; }
        public int ErrorCount { get; private set; }
        public Task<string?> PickOpenFileAsync(string title) => Task.FromResult<string?>(null);
        public Task<string?> PickSaveFileAsync(string title, string? suggestedFileName) => Task.FromResult(SavePath);
        public Task<SaveChangesResult> ShowSaveChangesDialogAsync() => Task.FromResult(SaveChangesResult.Discard);
        public Task ShowErrorAsync(string message, string detail) { ErrorCount++; return Task.CompletedTask; }
        public Task ShowAboutAsync() => Task.CompletedTask;
    }

    private sealed class StubClipboardService : IClipboardService
    {
        public Task SetBytesAsync(byte[] bytes, string? text) => Task.CompletedTask;
        public Task<byte[]?> TryGetBytesAsync() => Task.FromResult<byte[]?>(null);
        public Task<string?> GetTextAsync() => Task.FromResult<string?>(null);
    }

    private sealed class StubWindowService : IWindowService
    {
        public void Close() { }
    }
}
