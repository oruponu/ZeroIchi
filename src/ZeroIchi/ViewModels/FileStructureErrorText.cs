using System.Globalization;
using ZeroIchi.Localization;
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
                Text(Strings.FileStructureErrorEndOfData, Position(error, args[0]), Number(args[1]), Number(args[2])),
            FileStructureErrorKind.MissingTerminator =>
                Text(Strings.FileStructureErrorMissingTerminator, args[0], Position(error, args[1])),
            FileStructureErrorKind.ContentsMismatch => Text(Strings.FileStructureErrorContentsMismatch, args[0], args[1]),
            FileStructureErrorKind.ValidEq => Text(Strings.FileStructureErrorValidEq, args[0], args[1]),
            FileStructureErrorKind.ValidMin => Text(Strings.FileStructureErrorValidMin, args[0], args[1]),
            FileStructureErrorKind.ValidMax => Text(Strings.FileStructureErrorValidMax, args[0], args[1]),
            FileStructureErrorKind.ValidAnyOf => Text(Strings.FileStructureErrorValidAnyOf, args[0]),
            FileStructureErrorKind.ValidInEnum => Text(Strings.FileStructureErrorValidInEnum, args[0], args[1]),
            FileStructureErrorKind.ValidExpr => Text(Strings.FileStructureErrorValidExpr, args[0]),
            FileStructureErrorKind.EndianSwitch => Text(Strings.FileStructureErrorEndianSwitch, args[0]),
            FileStructureErrorKind.Seek => Text(Strings.FileStructureErrorSeek, Position(error, args[0]), args[1]),
            FileStructureErrorKind.Zlib => Text(Strings.FileStructureErrorZlib, args[0]),
            FileStructureErrorKind.Native => Text(Strings.FileStructureErrorNative, args[0], args[1]),
            FileStructureErrorKind.NodeBudget => Text(Strings.FileStructureErrorNodeBudget, Number(args[0])),
            FileStructureErrorKind.NestingLimit => Text(Strings.FileStructureErrorNestingLimit, Number(args[0])),
            FileStructureErrorKind.LoadLimit => Text(Strings.FileStructureErrorLoadLimit, Number(args[0])),
            FileStructureErrorKind.BytesLoadLimit =>
                Text(Strings.FileStructureErrorBytesLoadLimit, Number(args[0]), Number(args[1])),
            FileStructureErrorKind.DecompressLimit => Text(Strings.FileStructureErrorDecompressLimit, Number(args[0])),
            FileStructureErrorKind.StackLimit => Strings.FileStructureErrorStackLimit,
            _ => error.Message,
        };
    }

    private static string Text(string format, params object[] args) =>
        string.Format(CultureInfo.CurrentCulture, format, args);

    private static string Position(FileStructureError error, string hex) =>
        Text(error.InConvertedData ? Strings.FileStructureConvertedPosition : Strings.FileStructurePosition, hex);

    private static string Number(string value) =>
        long.Parse(value, CultureInfo.InvariantCulture).ToString("N0", CultureInfo.InvariantCulture);
}
