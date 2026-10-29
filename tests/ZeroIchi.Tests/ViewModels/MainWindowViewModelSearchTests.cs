using System.Text;
using ZeroIchi.Models.Buffers;
using ZeroIchi.ViewModels;
using static ZeroIchi.Tests.ViewModelTestFactory;

namespace ZeroIchi.Tests.ViewModels;

[Collection(HeadlessTestApp.Collection)]
public sealed class MainWindowViewModelSearchTests : IDisposable
{
    static MainWindowViewModelSearchTests() => Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);

    private const int ChunkSize = 65536;
    private const int FileSize = ChunkSize * 4;

    private readonly string _tempDir = Path.Combine(Path.GetTempPath(), "ZeroIchiTests_" + Guid.NewGuid().ToString("N"));
    private readonly List<GatedByteBuffer> _gates = [];

    public MainWindowViewModelSearchTests() => Directory.CreateDirectory(_tempDir);

    public void Dispose() => Directory.Delete(_tempDir, recursive: true);

    private string CreateTestFile()
    {
        var bytes = new byte[100];
        bytes[95] = 0xAB;
        var path = Path.Combine(_tempDir, Guid.NewGuid().ToString("N") + ".bin");
        File.WriteAllBytes(path, bytes);
        return path;
    }

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

    private static MainWindowViewModel CreateFast() =>
        new(new StubDialogService(), new StubClipboardService(), new StubWindowService())
        {
            SearchFlushInterval = TimeSpan.FromMilliseconds(10),
        };

    private async Task CleanUpAsync(MainWindowViewModel vm)
    {
        foreach (var gate in _gates) gate.OpenAll();
        await vm.SearchUpdate;
        await vm.DisposeDocumentAsync();
    }

    [Fact]
    public Task Edit_AfterSearch_ClearsSearchResults() => HeadlessTestApp.RunAsync(async () =>
    {
        var vm = CreateViewModel();
        await vm.OpenFileAsync(CreateTestFile());
        try
        {
            vm.SearchText = "AB";
            await vm.SearchUpdate;
            Assert.Equal([95L], vm.SearchMatchOffsets);

            vm.OnBytesDeleted(0, 10, cursorPositionAfter: 0);

            Assert.Empty(vm.SearchMatchOffsets);
            Assert.Equal(-1, vm.CurrentSearchMatchIndex);
            Assert.Equal("", vm.SearchStatusText);
        }
        finally
        {
            await vm.DisposeDocumentAsync();
        }
    });

    [Fact]
    public Task FindNext_AfterShrinkingEdit_MovesToCurrentMatch() => HeadlessTestApp.RunAsync(async () =>
    {
        var vm = CreateViewModel();
        await vm.OpenFileAsync(CreateTestFile());
        try
        {
            vm.SearchText = "AB";
            await vm.SearchUpdate;
            vm.OnBytesDeleted(0, 10, cursorPositionAfter: 0);

            vm.FindNextCommand.Execute(null);
            await vm.SearchUpdate;

            Assert.Equal(85, vm.CursorPosition);
            Assert.Equal(85, vm.SelectionStart);
            Assert.Equal(1, vm.SelectionLength);
        }
        finally
        {
            await vm.DisposeDocumentAsync();
        }
    });

    [Fact]
    public Task Search_JumpsToTheFirstMatchAfterTheCursor() => HeadlessTestApp.RunAsync(async () =>
    {
        var vm = CreateFast();
        await vm.OpenFileAsync(WriteFile(Ab(10), Ab(300), Ab(70000)));
        try
        {
            vm.CursorPosition = 200;
            vm.SearchText = "AB";
            await vm.SearchUpdate;

            Assert.Equal([10L, 300L, 70000L], vm.SearchMatchOffsets);
            Assert.Equal(300, vm.CursorPosition);
            Assert.Equal(300, vm.SelectionStart);
            Assert.Equal(1, vm.SelectionLength);
            Assert.Equal(300, vm.CurrentSearchMatchOffset);
            Assert.Equal(1, vm.CurrentSearchMatchIndex);
            Assert.Equal("2/3", vm.SearchStatusText);
        }
        finally { await CleanUpAsync(vm); }
    });

    [Fact]
    public Task Search_WithNoMatchAfterTheCursor_WrapsToTheFirstMatch() => HeadlessTestApp.RunAsync(async () =>
    {
        var vm = CreateFast();
        await vm.OpenFileAsync(WriteFile(Ab(10)));
        try
        {
            vm.CursorPosition = 200;
            vm.SearchText = "AB";
            await vm.SearchUpdate;

            Assert.Equal(10, vm.CursorPosition);
            Assert.Equal("1/1", vm.SearchStatusText);
        }
        finally { await CleanUpAsync(vm); }
    });

    [Fact]
    public Task Search_WithTheCursorAtTheEnd_WrapsToTheFirstMatch() => HeadlessTestApp.RunAsync(async () =>
    {
        var vm = CreateFast();
        await vm.OpenFileAsync(WriteFile(Ab(10)));
        try
        {
            vm.CursorPosition = FileSize;
            vm.SearchText = "AB";
            await vm.SearchUpdate;

            Assert.Equal(10, vm.CursorPosition);
            Assert.Equal("1/1", vm.SearchStatusText);
        }
        finally { await CleanUpAsync(vm); }
    });

    [Fact]
    public Task Search_WithNoMatches_ShowsZero() => HeadlessTestApp.RunAsync(async () =>
    {
        var vm = CreateFast();
        await vm.OpenFileAsync(WriteFile());
        try
        {
            vm.SearchText = "AB";
            await vm.SearchUpdate;

            Assert.Empty(vm.SearchMatchOffsets);
            Assert.Equal("0/0", vm.SearchStatusText);
            Assert.Equal(-1, vm.CurrentSearchMatchOffset);
        }
        finally { await CleanUpAsync(vm); }
    });

    [Fact]
    public Task Search_ShowsMatchesBeforeTheScanFinishes() => HeadlessTestApp.RunAsync(async () =>
    {
        var vm = CreateGated();
        await vm.OpenFileAsync(WriteFile(Ab(10), Ab(70000)));
        try
        {
            vm.SearchText = "AB";
            _gates[0].Allow(1);
            await HeadlessTestApp.WaitUntilAsync(() => vm.CursorPosition == 10);

            Assert.Equal([10L], vm.SearchMatchOffsets);
            Assert.Equal("1/1", vm.SearchStatusText);
            Assert.False(vm.SearchUpdate.IsCompleted);

            _gates[0].OpenAll();
            await vm.SearchUpdate;

            Assert.Equal([10L, 70000L], vm.SearchMatchOffsets);
            Assert.Equal("1/2", vm.SearchStatusText);
            Assert.Equal(10, vm.CursorPosition);
        }
        finally { await CleanUpAsync(vm); }
    });

    [Fact]
    public Task TypingAgain_KeepsTheOldResultsUntilTheNewSearchReports() => HeadlessTestApp.RunAsync(async () =>
    {
        var vm = CreateGated();
        await vm.OpenFileAsync(WriteFile(Ab(10), (300, [0xAB, 0xCD])));
        try
        {
            vm.SearchText = "AB";
            _gates[0].OpenAll();
            await vm.SearchUpdate;
            Assert.Equal([10L, 300L], vm.SearchMatchOffsets);
            Assert.Equal(10, vm.CursorPosition);

            vm.SearchText = "AB CD";
            await Task.Delay(50);

            Assert.Equal([10L, 300L], vm.SearchMatchOffsets);
            Assert.Equal(1, vm.SearchMatchLength);
            Assert.Equal("1/2", vm.SearchStatusText);

            _gates[1].OpenAll();
            await vm.SearchUpdate;

            Assert.Equal([300L], vm.SearchMatchOffsets);
            Assert.Equal(2, vm.SearchMatchLength);
            Assert.Equal(300, vm.CursorPosition);
            Assert.Equal(2, vm.SelectionLength);
            Assert.Equal("1/1", vm.SearchStatusText);
        }
        finally { await CleanUpAsync(vm); }
    });

    [Fact]
    public Task Edit_WhileSearching_CancelsAndClearsTheResults() => HeadlessTestApp.RunAsync(async () =>
    {
        var vm = CreateGated();
        await vm.OpenFileAsync(WriteFile(Ab(10), Ab(70000)));
        try
        {
            vm.SearchText = "AB";
            _gates[0].Allow(1);
            await HeadlessTestApp.WaitUntilAsync(() => vm.SearchMatchOffsets.Length == 1);

            vm.OnByteModified(5, 0x11, cursorPositionAfter: 5);

            Assert.Empty(vm.SearchMatchOffsets);
            Assert.Equal("", vm.SearchStatusText);

            _gates[0].OpenAll();
            await vm.SearchUpdate;
            await Task.Delay(50);

            Assert.Empty(vm.SearchMatchOffsets);
            Assert.Equal("", vm.SearchStatusText);
        }
        finally { await CleanUpAsync(vm); }
    });

    [Fact]
    public Task CloseSearch_WhileSearching_ClearsTheResults() => HeadlessTestApp.RunAsync(async () =>
    {
        var vm = CreateGated();
        await vm.OpenFileAsync(WriteFile(Ab(10), Ab(70000)));
        try
        {
            vm.SearchText = "AB";
            _gates[0].Allow(1);
            await HeadlessTestApp.WaitUntilAsync(() => vm.SearchMatchOffsets.Length == 1);

            vm.CloseSearchCommand.Execute(null);

            Assert.Empty(vm.SearchMatchOffsets);
            Assert.Equal("", vm.SearchStatusText);

            _gates[0].OpenAll();
            await vm.SearchUpdate;
            await Task.Delay(50);

            Assert.Empty(vm.SearchMatchOffsets);
            Assert.Equal("", vm.SearchStatusText);
        }
        finally { await CleanUpAsync(vm); }
    });

    [Fact]
    public Task FindNext_WhileTheNextMatchIsUnscanned_JumpsWhenItIsFound() => HeadlessTestApp.RunAsync(async () =>
    {
        var vm = CreateGated();
        await vm.OpenFileAsync(WriteFile(Ab(10), Ab(70000)));
        try
        {
            vm.SearchText = "AB";
            _gates[0].Allow(1);
            await HeadlessTestApp.WaitUntilAsync(() => vm.CursorPosition == 10);

            vm.FindNextCommand.Execute(null);
            await Task.Delay(50);
            Assert.Equal(10, vm.CursorPosition);

            _gates[0].Allow(1);
            await HeadlessTestApp.WaitUntilAsync(() => vm.CursorPosition == 70000);
            Assert.Equal("2/2", vm.SearchStatusText);
        }
        finally { await CleanUpAsync(vm); }
    });

    [Fact]
    public Task FindPrevious_ToStartASearch_JumpsToTheNearestMatchBeforeTheCursor() => HeadlessTestApp.RunAsync(async () =>
    {
        var vm = CreateGated();
        await vm.OpenFileAsync(WriteFile(Ab(10), Ab(100), Ab(70000)));
        try
        {
            vm.SearchText = "AB";
            _gates[0].OpenAll();
            await vm.SearchUpdate;
            vm.OnByteModified(200, 0x11, cursorPositionAfter: 200);
            vm.CursorPosition = 150;

            vm.FindPreviousCommand.Execute(null);
            _gates[1].Allow(4);
            await HeadlessTestApp.WaitUntilAsync(() => vm.SearchMatchOffsets.Length == 1);

            Assert.Equal([70000L], vm.SearchMatchOffsets);
            Assert.Equal(150, vm.CursorPosition);
            Assert.Equal("-/1", vm.SearchStatusText);

            _gates[1].OpenAll();
            await vm.SearchUpdate;

            Assert.Equal(100, vm.CursorPosition);
            Assert.Equal("2/3", vm.SearchStatusText);
        }
        finally { await CleanUpAsync(vm); }
    });

    [Fact]
    public Task FindPrevious_ToStartASearch_WithNoMatchBeforeTheCursor_WrapsToTheLastMatch() => HeadlessTestApp.RunAsync(async () =>
    {
        var vm = CreateFast();
        await vm.OpenFileAsync(WriteFile(Ab(70000), Ab(140000)));
        try
        {
            vm.SearchText = "AB";
            await vm.SearchUpdate;
            vm.OnByteModified(5, 0x11, cursorPositionAfter: 5);
            vm.CursorPosition = 10;

            vm.FindPreviousCommand.Execute(null);
            await vm.SearchUpdate;

            Assert.Equal(140000, vm.CursorPosition);
            Assert.Equal("2/2", vm.SearchStatusText);
        }
        finally { await CleanUpAsync(vm); }
    });

    [Fact]
    public Task PressingAgainWhileWaiting_KeepsOnlyTheLastDirection() => HeadlessTestApp.RunAsync(async () =>
    {
        var vm = CreateGated();
        await vm.OpenFileAsync(WriteFile(Ab(10), Ab(70000), Ab(140000)));
        try
        {
            vm.SearchText = "AB";
            _gates[0].Allow(1);
            await HeadlessTestApp.WaitUntilAsync(() => vm.CursorPosition == 10);

            vm.FindNextCommand.Execute(null);
            vm.FindPreviousCommand.Execute(null);
            _gates[0].Allow(1);
            await HeadlessTestApp.WaitUntilAsync(() => vm.SearchMatchOffsets.Length == 2);

            Assert.Equal(10, vm.CursorPosition);

            _gates[0].OpenAll();
            await vm.SearchUpdate;

            Assert.Equal(140000, vm.CursorPosition);
        }
        finally { await CleanUpAsync(vm); }
    });

    [Fact]
    public Task ClosingTheSearch_DropsThePendingMove() => HeadlessTestApp.RunAsync(async () =>
    {
        var vm = CreateGated();
        await vm.OpenFileAsync(WriteFile(Ab(10), Ab(70000)));
        try
        {
            vm.SearchText = "AB";
            _gates[0].Allow(1);
            await HeadlessTestApp.WaitUntilAsync(() => vm.CursorPosition == 10);

            vm.FindNextCommand.Execute(null);
            vm.CloseSearchCommand.Execute(null);
            _gates[0].OpenAll();
            await vm.SearchUpdate;
            await Task.Delay(50);

            Assert.Equal(10, vm.CursorPosition);
        }
        finally { await CleanUpAsync(vm); }
    });

    [Fact]
    public Task FindNext_AfterTheScanCompletes_WrapsAround() => HeadlessTestApp.RunAsync(async () =>
    {
        var vm = CreateFast();
        await vm.OpenFileAsync(WriteFile(Ab(10), Ab(70000)));
        try
        {
            vm.SearchText = "AB";
            await vm.SearchUpdate;

            vm.FindNextCommand.Execute(null);
            Assert.Equal(70000, vm.CursorPosition);
            Assert.Equal("2/2", vm.SearchStatusText);

            vm.FindNextCommand.Execute(null);
            Assert.Equal(10, vm.CursorPosition);
            Assert.Equal("1/2", vm.SearchStatusText);

            vm.FindPreviousCommand.Execute(null);
            Assert.Equal(70000, vm.CursorPosition);
        }
        finally { await CleanUpAsync(vm); }
    });

    [Fact]
    public Task IsSearching_TurnsOnAfterTheBusyDelayAndOffWhenDone() => HeadlessTestApp.RunAsync(async () =>
    {
        var vm = CreateGated();
        await vm.OpenFileAsync(WriteFile(Ab(10), Ab(70000)));
        try
        {
            vm.SearchText = "AB";
            await HeadlessTestApp.WaitUntilAsync(() => vm.IsSearching);

            _gates[0].OpenAll();
            await vm.SearchUpdate;

            Assert.False(vm.IsSearching);
        }
        finally { await CleanUpAsync(vm); }
    });

    [Fact]
    public Task IsSearching_StaysOffForAQuickSearch() => HeadlessTestApp.RunAsync(async () =>
    {
        var vm = CreateFast();
        await vm.OpenFileAsync(WriteFile(Ab(10), Ab(70000)));
        try
        {
            var values = new List<bool>();
            vm.PropertyChanged += (_, e) =>
            {
                if (e.PropertyName == nameof(MainWindowViewModel.IsSearching)) values.Add(vm.IsSearching);
            };

            vm.SearchText = "AB";
            await vm.SearchUpdate;

            Assert.DoesNotContain(true, values);
        }
        finally { await CleanUpAsync(vm); }
    });

    [Fact]
    public Task IsSearching_StaysOnWhenANewSearchReplacesARunningOne() => HeadlessTestApp.RunAsync(async () =>
    {
        var vm = CreateGated();
        await vm.OpenFileAsync(WriteFile(Ab(10), (300, [0xAB, 0xCD])));
        try
        {
            vm.SearchText = "AB";
            await HeadlessTestApp.WaitUntilAsync(() => vm.IsSearching);
            var values = new List<bool>();
            vm.PropertyChanged += (_, e) =>
            {
                if (e.PropertyName == nameof(MainWindowViewModel.IsSearching)) values.Add(vm.IsSearching);
            };

            vm.SearchText = "AB CD";
            await Task.Delay(50);

            Assert.DoesNotContain(false, values);
            Assert.True(vm.IsSearching);

            foreach (var gate in _gates) gate.OpenAll();
            await vm.SearchUpdate;

            Assert.False(vm.IsSearching);
        }
        finally { await CleanUpAsync(vm); }
    });

    [Fact]
    public Task CloseSearch_WhileSearching_TurnsOffIsSearching() => HeadlessTestApp.RunAsync(async () =>
    {
        var vm = CreateGated();
        await vm.OpenFileAsync(WriteFile(Ab(10), Ab(70000)));
        try
        {
            vm.SearchText = "AB";
            await HeadlessTestApp.WaitUntilAsync(() => vm.IsSearching);

            vm.CloseSearchCommand.Execute(null);

            Assert.False(vm.IsSearching);
        }
        finally { await CleanUpAsync(vm); }
    });

    [Fact]
    public Task Search_WhenReadingFails_ShowsAnError() => HeadlessTestApp.RunAsync(async () =>
    {
        var vm = new MainWindowViewModel(new StubDialogService(), new StubClipboardService(), new StubWindowService())
        {
            SearchFlushInterval = TimeSpan.FromMilliseconds(10),
            SearchSource = s => new FailingByteBuffer(s.Length),
        };
        await vm.OpenFileAsync(WriteFile(Ab(10)));
        try
        {
            vm.SearchText = "AB";
            await vm.SearchUpdate;

            Assert.Empty(vm.SearchMatchOffsets);
            Assert.Equal("検索できませんでした", vm.SearchStatusText);
        }
        finally { await CleanUpAsync(vm); }
    });

    private sealed class FailingByteBuffer(long length) : ByteBuffer
    {
        public override long Length => length;

        public override byte ReadByte(long index) => throw new IOException("Read failed");

        public override void ReadBytes(long offset, byte[] buffer, int bufferOffset, int count) =>
            throw new IOException("Read failed");
    }
}
