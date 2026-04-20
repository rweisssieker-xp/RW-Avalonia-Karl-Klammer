using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media;

namespace CarolusNexus_WinUI;

/// <summary>Fluent / WinUI 3 theme brushes and backdrop helpers so the shell matches system Mica + light/dark resources.</summary>
internal static class WinUiFluentChrome
{
    public static Brush? TryThemeBrush(string key)
    {
        if (Application.Current?.Resources.TryGetValue(key, out var o) == true && o is Brush b)
            return b;
        return null;
    }

    /// <summary>Sets Mica on the window when the OS / App SDK supports it (Windows 11 + current WASDK).</summary>
    public static void ApplyMicaBackdrop(Window window)
    {
        try
        {
            window.SystemBackdrop = new MicaBackdrop();
        }
        catch
        {
            // Older Windows or restricted environments — solid chrome still works.
        }
    }

    public static Brush LayerChromeBackground =>
        TryThemeBrush("LayerOnMicaBaseAltFillColorDefaultBrush")
        ?? TryThemeBrush("LayerFillColorDefaultBrush")
        ?? new SolidColorBrush(ColorHelper.FromArgb(255, 32, 32, 32));

    public static Brush CardSurfaceBackground =>
        TryThemeBrush("ControlFillColorSecondaryBrush")
        ?? TryThemeBrush("LayerFillColorDefaultBrush")
        ?? new SolidColorBrush(ColorHelper.FromArgb(255, 40, 40, 43));

    public static Brush CardBorderBrush =>
        TryThemeBrush("ControlStrokeColorDefaultBrush")
        ?? TryThemeBrush("CardStrokeColorDefaultBrush")
        ?? new SolidColorBrush(ColorHelper.FromArgb(255, 72, 72, 78));

    public static Brush BadgeBackground =>
        TryThemeBrush("SubtleFillColorSecondaryBrush")
        ?? TryThemeBrush("ControlFillColorDefaultBrush")
        ?? new SolidColorBrush(ColorHelper.FromArgb(255, 48, 48, 52));

    public static Brush SeparatorBrush =>
        TryThemeBrush("DividerStrokeColorDefaultBrush")
        ?? TryThemeBrush("ControlStrokeColorDefaultBrush")
        ?? new SolidColorBrush(ColorHelper.FromArgb(255, 60, 60, 66));
}
