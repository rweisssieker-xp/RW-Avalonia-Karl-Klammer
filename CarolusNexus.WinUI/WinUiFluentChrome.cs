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

    public static void ApplyBodyTextStyle(TextBlock tb)
    {
        if (TryStyle("BodyTextBlockStyle") is { } st)
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

    public static void SetIconButton(Button b, string label, string glyph)
    {
        b.Content = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 8,
            Children =
            {
                new FontIcon
                {
                    Glyph = glyph,
                    FontFamily = new FontFamily("Segoe Fluent Icons"),
                    FontSize = 14
                },
                new TextBlock
                {
                    Text = label,
                    VerticalAlignment = VerticalAlignment.Center
                }
            }
        };
    }

    public static Button AppBarCommand(string label, string glyph, RoutedEventHandler handler)
    {
        var command = new Button();
        StyleActionButton(command);
        SetIconButton(command, label, glyph);
        command.Click += handler;
        return command;
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

    public static Border SectionCard(string title, string? subtitle, UIElement content)
    {
        var inner = new StackPanel { Spacing = 8 };
        inner.Children.Add(new TextBlock
        {
            Text = title,
            TextWrapping = TextWrapping.Wrap,
            Foreground = PrimaryTextBrush,
            FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
            FontSize = 15
        });
        if (!string.IsNullOrWhiteSpace(subtitle))
        {
            var sub = new TextBlock
            {
                Text = subtitle,
                TextWrapping = TextWrapping.Wrap,
                Foreground = SecondaryTextBrush
            };
            ApplyCaptionTextStyle(sub);
            inner.Children.Add(sub);
        }

        inner.Children.Add(content);
        return WrapCard(inner);
    }

    public static Border StatusTile(string title, string value, string? caption = null)
    {
        var inner = new StackPanel { Spacing = 4 };
        inner.Children.Add(new TextBlock
        {
            Text = title,
            TextWrapping = TextWrapping.Wrap,
            Foreground = SecondaryTextBrush,
            FontSize = 11
        });
        inner.Children.Add(new TextBlock
        {
            Text = value,
            TextWrapping = TextWrapping.Wrap,
            Foreground = PrimaryTextBrush,
            FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
            FontSize = 13
        });
        if (!string.IsNullOrWhiteSpace(caption))
        {
            var cap = new TextBlock
            {
                Text = caption,
                TextWrapping = TextWrapping.Wrap,
                Foreground = TertiaryTextBrush
            };
            ApplyCaptionTextStyle(cap);
            inner.Children.Add(cap);
        }

        return WrapCard(inner, new Thickness(12, 10, 12, 10));
    }

    public static Border ActionGroup(string title, params UIElement[] actions)
    {
        var row = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };
        foreach (var action in actions)
            row.Children.Add(action);

        return SectionCard(title, null, row);
    }

    public static Border CommandSurface(string title, params UIElement[] commands)
    {
        var row = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };
        foreach (var command in commands)
            row.Children.Add(command);

        return SectionCard(title, null, row);
    }

    public static Border EmptyState(string title, string message, string? hint = null)
    {
        var inner = new StackPanel { Spacing = 6 };
        inner.Children.Add(new TextBlock
        {
            Text = title,
            TextWrapping = TextWrapping.Wrap,
            Foreground = PrimaryTextBrush,
            FontWeight = Microsoft.UI.Text.FontWeights.SemiBold
        });
        inner.Children.Add(new TextBlock
        {
            Text = message,
            TextWrapping = TextWrapping.Wrap,
            Foreground = SecondaryTextBrush
        });
        if (!string.IsNullOrWhiteSpace(hint))
        {
            var h = new TextBlock
            {
                Text = hint,
                TextWrapping = TextWrapping.Wrap,
                Foreground = TertiaryTextBrush
            };
            ApplyCaptionTextStyle(h);
            inner.Children.Add(h);
        }

        return WrapCard(inner, new Thickness(14, 12, 14, 12));
    }

    public static InfoBar RiskGateCard(string title, string message, InfoBarSeverity severity)
    {
        return new InfoBar
        {
            IsOpen = true,
            Title = title,
            Message = message,
            Severity = severity
        };
    }

    public static Border NextBestActionBar(
        NextBestAction action,
        Button primary,
        Button secondary,
        Button dismiss)
    {
        primary.Content = action.PrimaryLabel;
        secondary.Content = action.SecondaryLabel;
        dismiss.Content = "Dismiss";
        StyleActionButton(primary, accent: action.Severity is "success" or "warning");
        StyleActionButton(secondary);
        StyleActionButton(dismiss, compact: true);

        var badge = new Border
        {
            CornerRadius = new CornerRadius(PillCornerRadius),
            Padding = new Thickness(10, 4, 10, 4),
            Background = BadgeBackground,
            Child = new TextBlock
            {
                Text = action.RequiresApproval ? "approval required" : action.Severity,
                TextWrapping = TextWrapping.NoWrap,
                Foreground = SecondaryTextBrush,
                FontSize = 11
            }
        };

        var titleRow = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };
        titleRow.Children.Add(new TextBlock
        {
            Text = action.Title,
            TextWrapping = TextWrapping.Wrap,
            Foreground = PrimaryTextBrush,
            FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
            FontSize = 15
        });
        titleRow.Children.Add(badge);

        var left = new StackPanel { Spacing = 6 };
        left.Children.Add(titleRow);
        left.Children.Add(new TextBlock
        {
            Text = action.Message,
            TextWrapping = TextWrapping.Wrap,
            Foreground = PrimaryTextBrush,
            FontSize = 14
        });
        var context = new TextBlock
        {
            Text = action.Context,
            TextWrapping = TextWrapping.Wrap,
            Foreground = SecondaryTextBrush
        };
        ApplyCaptionTextStyle(context);
        left.Children.Add(context);

        var buttons = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Right,
            VerticalAlignment = VerticalAlignment.Center,
            Spacing = 8
        };
        if (!string.IsNullOrWhiteSpace(action.PrimaryLabel))
            buttons.Children.Add(primary);
        if (!string.IsNullOrWhiteSpace(action.SecondaryLabel))
            buttons.Children.Add(secondary);
        buttons.Children.Add(dismiss);

        var grid = new Grid { ColumnSpacing = 16 };
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        grid.Children.Add(left);
        grid.Children.Add(buttons);
        Grid.SetColumn(buttons, 1);
        return WrapCard(grid, new Thickness(16, 14, 16, 14));
    }

    public static Border GhostOperatorCard(
        GhostOperatorSuggestion suggestion,
        Button primary,
        Button why,
        Button ignore,
        Button openPowerUser)
    {
        primary.Content = suggestion.ActionLabel;
        why.Content = "Why?";
        ignore.Content = "Ignore";
        openPowerUser.Content = suggestion.SecondaryLabel;
        StyleActionButton(primary, accent: !suggestion.RequiresApproval);
        StyleActionButton(why);
        StyleActionButton(openPowerUser);
        StyleActionButton(ignore, compact: true);

        var badge = new Border
        {
            CornerRadius = new CornerRadius(PillCornerRadius),
            Padding = new Thickness(10, 4, 10, 4),
            Background = BadgeBackground,
            Child = new TextBlock
            {
                Text = $"{suggestion.Risk} · {(int)(suggestion.Confidence * 100)}%",
                TextWrapping = TextWrapping.NoWrap,
                Foreground = SecondaryTextBrush,
                FontSize = 11
            }
        };

        var header = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };
        header.Children.Add(new FontIcon
        {
            Glyph = "\uE9F5",
            FontFamily = new FontFamily("Segoe Fluent Icons"),
            Foreground = PrimaryTextBrush
        });
        header.Children.Add(new TextBlock
        {
            Text = suggestion.Title,
            TextWrapping = TextWrapping.Wrap,
            Foreground = PrimaryTextBrush,
            FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
            FontSize = 16
        });
        header.Children.Add(badge);

        var copy = new StackPanel { Spacing = 6 };
        copy.Children.Add(header);
        copy.Children.Add(new TextBlock
        {
            Text = suggestion.Situation,
            TextWrapping = TextWrapping.Wrap,
            Foreground = PrimaryTextBrush,
            FontSize = 14
        });
        copy.Children.Add(new TextBlock
        {
            Text = suggestion.Why,
            TextWrapping = TextWrapping.Wrap,
            Foreground = SecondaryTextBrush
        });

        var buttons = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Right,
            VerticalAlignment = VerticalAlignment.Center,
            Spacing = 8
        };
        buttons.Children.Add(primary);
        buttons.Children.Add(openPowerUser);
        buttons.Children.Add(why);
        buttons.Children.Add(ignore);

        var grid = new Grid { ColumnSpacing = 16 };
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        grid.Children.Add(copy);
        grid.Children.Add(buttons);
        Grid.SetColumn(buttons, 1);
        return WrapCard(grid, new Thickness(16, 14, 16, 14));
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
