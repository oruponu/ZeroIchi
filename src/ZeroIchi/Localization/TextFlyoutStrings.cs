using Avalonia.Controls;

namespace ZeroIchi.Localization;

internal static class TextFlyoutStrings
{
    public static void Attach(IResourceDictionary resources)
    {
        Apply(resources);
        Localizer.Instance.CultureChanged += (_, _) => Apply(resources);
    }

    private static void Apply(IResourceDictionary resources)
    {
        resources["StringTextFlyoutCutText"] = Strings.MenuEditCut;
        resources["StringTextFlyoutCopyText"] = Strings.MenuEditCopy;
        resources["StringTextFlyoutPasteText"] = Strings.MenuEditPaste;
    }
}
