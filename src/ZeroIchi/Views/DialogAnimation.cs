using Avalonia;
using Avalonia.Animation;
using Avalonia.Animation.Easings;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Styling;
using System;
using System.Threading.Tasks;

namespace ZeroIchi.Views;

internal static class DialogAnimation
{
    private const double HiddenScale = 1.05;

    private static readonly TimeSpan FasterDuration = TimeSpan.FromMilliseconds(83);
    private static readonly TimeSpan FastDuration = TimeSpan.FromMilliseconds(167);
    private static readonly TimeSpan NormalDuration = TimeSpan.FromMilliseconds(250);
    private static readonly Easing FastOutSlowIn = new SplineEasing(0, 0, 0, 1);

    public static void Attach(Window dialog)
    {
        var surface = (Control)dialog.Content!;
        var scale = new ScaleTransform(HiddenScale, HiddenScale);
        surface.Opacity = 0;
        surface.RenderTransform = scale;

        dialog.Opened += async (_, _) => await Task.WhenAll(
            Animate(surface, Visual.OpacityProperty, 0.0, 1.0, FasterDuration, new LinearEasing()),
            Animate(surface, ScaleTransform.ScaleXProperty, HiddenScale, 1.0, NormalDuration, FastOutSlowIn),
            Animate(surface, ScaleTransform.ScaleYProperty, HiddenScale, 1.0, NormalDuration, FastOutSlowIn));

        var closing = false;
        dialog.Closing += async (_, e) =>
        {
            if (closing || e.CloseReason is not (WindowCloseReason.WindowClosing or WindowCloseReason.Undefined))
                return;

            closing = true;
            e.Cancel = true;
            surface.IsHitTestVisible = false;
            (dialog.Owner as IShellWindow)?.SetModalOverlayVisible(false);
            await Task.WhenAll(
                Animate(surface, Visual.OpacityProperty, 1.0, 0.0, FasterDuration, new LinearEasing()),
                Animate(surface, ScaleTransform.ScaleXProperty, 1.0, HiddenScale, FastDuration, FastOutSlowIn),
                Animate(surface, ScaleTransform.ScaleYProperty, 1.0, HiddenScale, FastDuration, FastOutSlowIn));
            dialog.Close();
        };
    }

    private static Task Animate(Control target, AvaloniaProperty property, double from, double to, TimeSpan duration, Easing easing)
    {
        var animation = new Animation
        {
            Duration = duration,
            Easing = easing,
            FillMode = FillMode.Forward,
            Children =
            {
                new KeyFrame { Cue = new Cue(0), Setters = { new Setter(property, from) } },
                new KeyFrame { Cue = new Cue(1), Setters = { new Setter(property, to) } },
            },
        };
        return animation.RunAsync(target);
    }
}
