using ZeroIchi.Models.FileStructure;
using ZeroIchi.ViewModels;

namespace ZeroIchi.Tests.ViewModels;

public sealed class FileStructureErrorTextTests
{
    private static FileStructureError Error(FileStructureErrorKind kind, params string[] arguments) =>
        new(kind, arguments, "english");

    public static TheoryData<FileStructureError, string> Cases => new()
    {
        { FileStructureError.EndOfData(0x1A, false, 4, 2, "e"), "データ不足: 位置 0x1A で 4 バイト必要 (残り 2 バイト)" },
        { FileStructureError.EndOfData(0x1A, true, 4, 2, "e"), "データ不足: 変換後のデータの位置 0x1A で 4 バイト必要 (残り 2 バイト)" },
        { FileStructureError.MissingTerminator("00", 0x10, false, "e"), "終端 00 が見つからずデータの末尾に到達 (位置 0x10 から)" },
        { Error(FileStructureErrorKind.ContentsMismatch, "89 50", "89 51"), "固定値の不一致: 期待 89 50、実際 89 51" },
        { Error(FileStructureErrorKind.ValidEq, "1", "2"), "検証エラー: 期待 1、実際 2" },
        { Error(FileStructureErrorKind.ValidMin, "1", "3"), "検証エラー: 1 は最小値 3 未満" },
        { Error(FileStructureErrorKind.ValidMax, "9", "4"), "検証エラー: 9 は最大値 4 を超過" },
        { Error(FileStructureErrorKind.ValidAnyOf, "3"), "検証エラー: 許可されていない値 3" },
        { Error(FileStructureErrorKind.ValidInEnum, "kind", "7"), "検証エラー: 列挙型 kind に未定義の値 7" },
        { Error(FileStructureErrorKind.ValidExpr, "3"), "検証エラー: 条件を満たさない値 3" },
        { Error(FileStructureErrorKind.EndianSwitch, "2"), "エンディアンの決定に失敗: 2 に一致する case なし" },
        { FileStructureError.Seek(0x20, 0x10, false, "e"), "位置 0x20 への移動に失敗 (データの末尾 0x10)" },
        { FileStructureError.Seek(0x20, 0x10, true, "e"), "変換後のデータの位置 0x20 への移動に失敗 (データの末尾 0x10)" },
        { Error(FileStructureErrorKind.Zlib, "bad header"), "zlib の展開に失敗: bad header" },
        { Error(FileStructureErrorKind.Native, "jpeg_entropy_data", "broken"), "jpeg_entropy_data の解析に失敗: broken" },
        { FileStructureError.Limit(FileStructureErrorKind.NodeBudget, 100000, "e"), "ノード数の上限 (100,000) に到達" },
        { FileStructureError.Limit(FileStructureErrorKind.NestingLimit, 256, "e"), "型の入れ子の上限 (256) に到達" },
        { FileStructureError.Limit(FileStructureErrorKind.LoadLimit, 3000, "e"), "メモリに読み込む量の上限 (3,000 バイト) に到達" },
        { FileStructureError.BytesLoadLimit(5000, 4096, "e"), "バイト列 (5,000 バイト) が読み込みの上限 (4,096 バイト) を超過" },
        { FileStructureError.Limit(FileStructureErrorKind.DecompressLimit, 1024, "e"), "展開後の大きさの上限 (1,024 バイト) に到達" },
        { Error(FileStructureErrorKind.StackLimit), "入れ子が深すぎてスタックが不足" },
        { FileStructureError.Other("/seq/0/size: 'b' has not been read yet"), "/seq/0/size: 'b' has not been read yet" },
    };

    [Theory]
    [MemberData(nameof(Cases))]
    public void Format_ShowsEachKindInJapanese(FileStructureError error, string expected)
    {
        Assert.Equal(expected, FileStructureErrorText.Format(error));
    }
}
