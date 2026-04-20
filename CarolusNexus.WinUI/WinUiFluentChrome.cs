using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Controls;

namespace CarolusNexus_WinUI;

/// <summary>Fluent / WinUI 3 theme brushes and backdrop helpers so the shell matches system Mica + light/dark resources.</summary>
internal static class WinUiFluentChrome
{
    public const double CardCornerRadius = 10;
    public const double PillCornerRadius = 999;
    public const double ActionButtonCornerRadius = 8;

    public static Brush? TryThemeBrush(string key)
    {
        if (Application.Current?.Resources.TryGetValue(key, out var o) == true && o is Brush b)
            return b;
        return null;
    }

    public static Brush PrimaryTextBrush =>
        TryThemeBrush("TextFillColorPrimaryBrush")
        ?? new SolidColorBrush(Colors.White);

    public static Brush SecondaryTextBrush =>
        TryThemeBrush("TextFillColorSecondaryBrush")
        ?? new SolidColorBrush(ColorHelper.FromArgb(255, 200, 200, 200));

    public static Brush TertiaryTextBrush =>
        TryThemeBrush("TextFillColorTertiaryBrush")
        ?? SecondaryTextBrush;

    /// <summary>Acrylic when available — reads as a refined header over Mica.</summary>
    public static Brush HeaderChromeBackground =>
        TryThemeBrush("AcrylicBackgroundFillColorDefaultBrush")
        ?? TryThemeBrush("LayerOnMicaBaseAltFillColorDefaultBrush")
        ?? LayerChromeBackground;

    public static void ApplyTitleTextStyle(TextBlock tb)
    {
        if (TryStyle("TitleTextBlockStyle") is { } st)
            tb.Style = st;
    }

    public static void ApplySubtitleTextStyle(TextBlock tb)
    {
        if (TryStyle("SubtitleTextBlockStyle") is { } st)
            tb.Style = st;
    }

    public static void ApplyCaptionTextStyle(TextBlock tb)
    {
        if (TryStyle("CaptionTextBlockStyle") is { } st)
            tb.Style = st;
    }

    private static Style? TryStyle(string key) =>
        Application.Current?.Resources.TryGetValue(key, out var o) == true && o is Style s ? s : null;

    public static TextBlock PageTitle(string text)
    {
        var t = new TextBlock { Text = text, TextWrapping = TextWrapping.Wrap };
        ApplyTitleTextStyle(t);
        return t;
    }

    /// <summary>Section heading under the page title (subtitle typography).</summary>
    public static TextBlock SectionTitle(string text, Thickness? margin = null)
    {
        var t = new TextBlock
        {
            Text = text,
            TextWrapping = TextWrapping.Wrap,
            Foreground = PrimaryTextBrush,
            Margin = margin ?? new Thickness(0, 12, 0, 4)
        };
        ApplySubtitleTextStyle(t);
        return t;
    }

    /// <summary>Compact label above a column (Ask prompts, Knowledge preview, …).</summary>
    public static TextBlock ColumnCaption(string text) =>
        new()
        {
            Text = text,
            TextWrapping = TextWrapping.Wrap,
            Foreground = PrimaryTextBrush,
            FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
            FontSize = 13
        };

    public static void StyleActionButton(Button b, bool accent = false, bool compact = false)
    {
        b.Padding = compact
            ? new Thickness(12, 8, 12, 8)
            : new Thickness(16, 10, 16, 10);
        b.CornerRadius = new CornerRadius(ActionButtonCornerRadius);
        if (accent && Application.Current?.Resources.TryGetValue("AccentButtonStyle", out var o) == true && o is Style st)
            b.Style = st;
    }

    public static Border WrapCard(UIElement child, Thickness? padding = null)
    {
        var b = new Border
        {
            Padding = padding ?? new Thickness(16, 14, 16, 14),
            CornerRadius = new CornerRadius(CardCornerRadius),
            BorderThickness = new Thickness(1),
            BorderBrush = CardBorderBrush,
            Background = CardSurfaceBackground,
            Child = child
        };
        ApplyCardElevation(b, 2f);
        return b;
    }

    public static void ApplyCardElevation(Border border, float z = 6f)
    {
        try
        {
            border.Shadow = new ThemeShadow();
            border.Translation = new System.Numerics.Vector3(0, 0, z);
        }
        catch
        {
            // ThemeShadow unsupported in some hosts
        }
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
