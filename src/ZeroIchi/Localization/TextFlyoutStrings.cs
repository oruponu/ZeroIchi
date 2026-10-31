using Avalonia.Controls;

namespace ZeroIchi.Localization;

internal static class TextFlyoutStrings
{
    public static void Apply(IResourceDictionary resources)
    {
        resources["StringTextFlyoutCutText"] = Strings.MenuEditCut;
        resources["StringTextFlyoutCopyText"] = Strings.MenuEditCopy;
        resources["StringTextFlyoutPasteText"] = Strings.MenuEditPaste;
    }
}
