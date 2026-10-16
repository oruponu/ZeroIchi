using System.Globalization;
using ZeroIchi.Models.FileStructure;

namespace ZeroIchi.ViewModels;

internal static class FileStructureErrorText
{
    public static string Format(FileStructureError error)
    {
        var args = error.Arguments;
        return error.Kind switch
        {
            FileStructureErrorKind.EndOfData =>
                $"データ不足: {Position(error, args[0])} で {Number(args[1])} バイト必要 (残り {Number(args[2])} バイト)",
            FileStructureErrorKind.MissingTerminator =>
                $"終端 {args[0]} が見つからずデータの末尾に到達 ({Position(error, args[1])} から)",
            FileStructureErrorKind.ContentsMismatch => $"固定値の不一致: 期待 {args[0]}、実際 {args[1]}",
            FileStructureErrorKind.ValidEq => $"検証エラー: 期待 {args[0]}、実際 {args[1]}",
            FileStructureErrorKind.ValidMin => $"検証エラー: {args[0]} は最小値 {args[1]} 未満",
            FileStructureErrorKind.ValidMax => $"検証エラー: {args[0]} は最大値 {args[1]} を超過",
            FileStructureErrorKind.ValidAnyOf => $"検証エラー: 許可されていない値 {args[0]}",
            FileStructureErrorKind.ValidInEnum => $"検証エラー: 列挙型 {args[0]} に未定義の値 {args[1]}",
            FileStructureErrorKind.ValidExpr => $"検証エラー: 条件を満たさない値 {args[0]}",
            FileStructureErrorKind.EndianSwitch => $"エンディアンの決定に失敗: {args[0]} に一致する case なし",
            FileStructureErrorKind.Seek => $"{Position(error, args[0])} への移動に失敗 (データの末尾 0x{args[1]})",
            FileStructureErrorKind.Zlib => $"zlib の展開に失敗: {args[0]}",
            FileStructureErrorKind.Native => $"{args[0]} の解析に失敗: {args[1]}",
            FileStructureErrorKind.NodeBudget => $"ノード数の上限 ({Number(args[0])}) に到達",
            FileStructureErrorKind.NestingLimit => $"型の入れ子の上限 ({Number(args[0])}) に到達",
            FileStructureErrorKind.LoadLimit => $"メモリに読み込む量の上限 ({Number(args[0])} バイト) に到達",
            FileStructureErrorKind.BytesLoadLimit =>
                $"バイト列 ({Number(args[0])} バイト) が読み込みの上限 ({Number(args[1])} バイト) を超過",
            FileStructureErrorKind.DecompressLimit => $"展開後の大きさの上限 ({Number(args[0])} バイト) に到達",
            FileStructureErrorKind.StackLimit => "入れ子が深すぎてスタックが不足",
            _ => error.Message,
        };
    }

    private static string Position(FileStructureError error, string hex) =>
        error.InConvertedData ? $"変換後のデータの位置 0x{hex}" : $"位置 0x{hex}";

    private static string Number(string value) =>
        long.Parse(value, CultureInfo.InvariantCulture).ToString("N0", CultureInfo.InvariantCulture);
}
