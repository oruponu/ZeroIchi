using System.Diagnostics;
using ZeroIchi.Models.Buffers;
using ZeroIchi.Models.PieceTables;

namespace ZeroIchi.Tests;

public sealed class PieceTableLookupTests
{
    private readonly record struct ModelByte(byte Value, bool Modified);

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    public void RandomEditsAndUndoRedo_MatchModel(int seed)
    {
        var random = new Random(seed);
        var original = new byte[64];
        random.NextBytes(original);
        var table = new PieceTable(new ArrayByteBuffer(original));
        var model = original.Select(b => new ModelByte(b, false)).ToList();
        var undo = new Stack<(PieceTableEdit Edit, List<ModelByte> Before)>();
        var redo = new Stack<(PieceTableEdit Edit, List<ModelByte> After)>();

        for (var step = 0; step < 2000; step++)
        {
            var before = model.ToList();
            PieceTableEdit edit;
            switch (random.Next(6))
            {
                case 0 when model.Count > 0:
                {
                    var index = random.Next(model.Count);
                    var value = (byte)random.Next(256);
                    edit = table.WriteByte(index, value);
                    model[index] = new ModelByte(value, true);
                    break;
                }
                case 1:
                {
                    var index = random.Next(model.Count + 1);
                    var bytes = new byte[random.Next(1, 5)];
                    random.NextBytes(bytes);
                    edit = table.InsertBytes(index, bytes);
                    model.InsertRange(index, bytes.Select(b => new ModelByte(b, true)));
                    break;
                }
                case 2 when model.Count > 0:
                {
                    var index = random.Next(model.Count);
                    var count = random.Next(1, Math.Min(8, model.Count - index) + 1);
                    edit = table.DeleteBytes(index, count);
                    model.RemoveRange(index, count);
                    break;
                }
                case 3:
                {
                    var value = (byte)random.Next(256);
                    edit = table.AppendByte(value);
                    model.Add(new ModelByte(value, true));
                    break;
                }
                case 4 when undo.Count > 0:
                {
                    var (undone, restored) = undo.Pop();
                    table.UndoEdit(undone);
                    redo.Push((undone, model));
                    model = restored;
                    AssertMatches(table, model, random);
                    continue;
                }
                case 5 when redo.Count > 0:
                {
                    var (redone, restored) = redo.Pop();
                    table.RedoEdit(redone);
                    undo.Push((redone, model));
                    model = restored;
                    AssertMatches(table, model, random);
                    continue;
                }
                default:
                    continue;
            }

            undo.Push((edit, before));
            redo.Clear();
            AssertMatches(table, model, random);
        }
    }

    [Fact]
    public void LookupsBehindTheLastPosition_DoNotScanFromTheStart()
    {
        var table = new PieceTable(new ArrayByteBuffer([0]));
        for (var i = 0; i < 50_000; i++)
            table.InsertBytes(table.Length, [(byte)i]);

        var stopwatch = Stopwatch.StartNew();
        for (var index = table.Length - 1; index >= table.Length - 10_000; index--)
        {
            Assert.True(table.IsModified(index));
            Assert.Equal((byte)(index - 1), table.ReadByte(index));
        }

        Assert.True(stopwatch.Elapsed < TimeSpan.FromSeconds(1), $"Took {stopwatch.Elapsed}");
    }

    private static void AssertMatches(PieceTable table, List<ModelByte> model, Random random)
    {
        Assert.Equal(model.Count, table.Length);

        for (var index = model.Count - 1; index >= 0; index--)
        {
            Assert.Equal(model[index].Value, table.ReadByte(index));
            Assert.Equal(model[index].Modified, table.IsModified(index));
        }

        if (model.Count == 0) return;
        var start = random.Next(model.Count);
        var length = random.Next(model.Count - start + 1);
        var buffer = new byte[length];
        table.ReadBytes(start, buffer, 0, length);
        Assert.Equal(model.Skip(start).Take(length).Select(b => b.Value), buffer);
    }
}
