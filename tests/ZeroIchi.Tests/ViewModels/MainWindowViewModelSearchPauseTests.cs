using System.Text;
using ZeroIchi.ViewModels;

namespace ZeroIchi.Tests.ViewModels;

[Collection(HeadlessTestApp.Collection)]
public sealed class MainWindowViewModelSearchPauseTests : IDisposable
{
    static MainWindowViewModelSearchPauseTests() => Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);

    private const int ChunkSize = 65536;
    private const int FileSize = ChunkSize * 4;

    private readonly string _tempDir = Path.Combine(Path.GetTempPath(), "ZeroIchiTests_" + Guid.NewGuid().ToString("N"));
    private readonly List<GatedByteBuffer> _gates = [];

    public MainWindowViewModelSearchPauseTests() => Directory.CreateDirectory(_tempDir);

    public void Dispose() => Directory.Delete(_tempDir, recursive: true);

    private string WriteFile(params (long Offset, byte[] Bytes)[] parts)
    {
        var bytes = new byte[FileSize];
        foreach (var (offset, content) in parts)
            content.CopyTo(bytes, offset);
        var path = Path.Combine(_tempDir, Guid.NewGuid().ToString("N") + ".bin");
        File.WriteAllBytes(path, bytes);
        return path;
    }

    private static (long, byte[]) Ab(long offset) => (offset, [0xAB]);

    private MainWindowViewModel CreateGated() =>
        new(new StubDialogService(), new StubClipboardService(), new StubWindowService())
        {
            SearchFlushInterval = TimeSpan.FromMilliseconds(10),
            SearchBusyDelay = TimeSpan.FromMilliseconds(50),
            SearchSource = snapshot =>
            {
                var gate = new GatedByteBuffer(snapshot);
                _gates.Add(gate);
                return gate;
            },
        };

    private async Task CleanUpAsync(MainWindowViewModel vm)
    {
        foreach (var gate in _gates) gate.OpenAll();
        await vm.SearchUpdate;
        await vm.DisposeDocumentAsync();
    }

    private async Task<(MainWindowViewModel Vm, string Path)> OpenSearchingAsync()
    {
        var vm = CreateGated();
        var path = WriteFile(Ab(10), Ab(70000), (300, [0xAB, 0xCD]), (500, "AB"u8.ToArray()));
        await vm.OpenFileAsync(path);
        vm.OnByteModified(5, 0x11, cursorPositionAfter: 5);
        vm.OpenSearchCommand.Execute(null);
        vm.SearchText = "AB";
        _gates[0].Allow(1);
        await HeadlessTestApp.WaitUntilAsync(() => vm.CursorPosition == 10);
        return (vm, path);
    }

    [Fact]
    public Task Save_WhileSearching_WaitsForTheScanAndClearsTheResults() => HeadlessTestApp.RunAsync(async () =>
    {
        var (vm, path) = await OpenSearchingAsync();
        try
        {
            var save = vm.SaveFileCommand.ExecuteAsync(null);
            await Task.Delay(50);
            Assert.False(save.IsCompleted);
            Assert.Equal(0x00, File.ReadAllBytes(path)[5]);

            _gates[0].OpenAll();
            await save;

            Assert.Equal(0x11, File.ReadAllBytes(path)[5]);
            Assert.Empty(vm.SearchMatchOffsets);
            Assert.Equal("", vm.SearchStatusText);
            Assert.Single(_gates);
        }
        finally { await CleanUpAsync(vm); }
    });

    [Fact]
    public Task TypingDuringTheSaveWait_SearchesAfterTheSave() => HeadlessTestApp.RunAsync(async () =>
    {
        var (vm, _) = await OpenSearchingAsync();
        try
        {
            var save = vm.SaveFileCommand.ExecuteAsync(null);
            vm.SearchText = "AB CD";
            Assert.Single(_gates);

            _gates[0].OpenAll();
            await save;
            Assert.Equal(2, _gates.Count);

            _gates[1].OpenAll();
            await vm.SearchUpdate;

            Assert.Equal([300L], vm.SearchMatchOffsets);
        }
        finally { await CleanUpAsync(vm); }
    });

    [Fact]
    public Task SwitchingTheModeDuringTheSaveWait_SearchesWithTheNewMode() => HeadlessTestApp.RunAsync(async () =>
    {
        var (vm, _) = await OpenSearchingAsync();
        try
        {
            var save = vm.SaveFileCommand.ExecuteAsync(null);
            vm.IsHexSearch = false;
            Assert.Single(_gates);

            _gates[0].OpenAll();
            await save;
            Assert.Equal(2, _gates.Count);

            _gates[1].OpenAll();
            await vm.SearchUpdate;

            Assert.Equal([500L], vm.SearchMatchOffsets);
        }
        finally { await CleanUpAsync(vm); }
    });

    [Fact]
    public Task ClosingTheSearchDuringTheSaveWait_DoesNotSearchAfterTheSave() => HeadlessTestApp.RunAsync(async () =>
    {
        var (vm, _) = await OpenSearchingAsync();
        try
        {
            var save = vm.SaveFileCommand.ExecuteAsync(null);
            vm.SearchText = "AB CD";
            vm.CloseSearchCommand.Execute(null);

            _gates[0].OpenAll();
            await save;
            await Task.Delay(50);

            Assert.Single(_gates);
            Assert.Empty(vm.SearchMatchOffsets);
        }
        finally { await CleanUpAsync(vm); }
    });

    [Fact]
    public Task OpeningAnotherFile_WhileSearching_ShowsNoOldResults() => HeadlessTestApp.RunAsync(async () =>
    {
        var (vm, _) = await OpenSearchingAsync();
        var other = WriteFile(Ab(20));
        try
        {
            var open = vm.OpenFileAsync(other);
            await Task.Delay(50);
            Assert.False(open.IsCompleted);

            _gates[0].OpenAll();
            await open;
            await Task.Delay(50);

            Assert.Empty(vm.SearchMatchOffsets);
            Assert.Equal("", vm.SearchStatusText);
            Assert.Single(_gates);
        }
        finally { await CleanUpAsync(vm); }
    });
}
