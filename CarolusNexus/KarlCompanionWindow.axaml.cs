using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Shapes;
using Avalonia.Media;
using Avalonia.Threading;

namespace CarolusNexus;

public partial class KarlCompanionWindow : Window
{
    private readonly DispatcherTimer _followTimer;
    private readonly DispatcherTimer _thinkingAnimTimer;
    private double _thinkPhase;
    private const double AuraBaseOpacity = 0.42;
    private const int OffsetX = 16;
    private const int OffsetY = 18;

    public KarlCompanionWindow()
    {
        InitializeComponent();
        _followTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(33) };
        _followTimer.Tick += (_, _) => FollowCursor();

        _thinkingAnimTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(48) };
        _thinkingAnimTimer.Tick += OnThinkingAnimTick;

        Opened += OnOpened;
        CompanionHub.JumpToTargetScreenRect += OnJumpToTargetScreenRect;
    }

    private void OnJumpToTargetScreenRect(int left, int top, int width, int height)
    {
        Dispatcher.UIThread.Post(() =>
        {
            var cx = left + Math.Max(8, width / 2);
            var cy = top + Math.Max(8, height / 2);
            Position = new PixelPoint(cx + OffsetX, cy + OffsetY);
        });
    }

    /// <summary>KI-/Operator-Zustand am Cursor (USP: Sichtbarkeit des Systemzustands).</summary>
    public void ApplyVisualState(CompanionVisualState state)
    {
        var (hex, label) = state switch
        {
            CompanionVisualState.Ready => ("#888888", "ready"),
            CompanionVisualState.Listening => ("#2ecc71", "listen"),
            CompanionVisualState.Transcribing => ("#f39c12", "STT"),
            CompanionVisualState.Thinking => ("#3498db", "think"),
            CompanionVisualState.Speaking => ("#9b59b6", "speak"),
            CompanionVisualState.Error => ("#e74c3c", "err"),
            _ => ("#888888", "?")
        };

        if (Aura is Ellipse aura)
        {
            aura.Fill = new SolidColorBrush(Color.Parse(hex));
            aura.Opacity = AuraBaseOpacity;
        }

        if (StateTag is TextBlock tag)
            tag.Text = label;

        if (state == CompanionVisualState.Thinking)
        {
            _thinkPhase = 0;
            _thinkingAnimTimer.Start();
        }
        else
        {
            _thinkingAnimTimer.Stop();
            ResetPupilTransforms();
        }
    }

    private void ResetPupilTransforms()
    {
        if (LeftPupil?.RenderTransform is TranslateTransform lt)
        {
            lt.X = 0;
            lt.Y = 0;
        }

        if (RightPupil?.RenderTransform is TranslateTransform rt)
        {
            rt.X = 0;
            rt.Y = 0;
        }
    }

    private void OnThinkingAnimTick(object? sender, EventArgs e)
    {
        _thinkPhase += 0.22;
        const double r = 3.4;
        var dxL = r * Math.Cos(_thinkPhase);
        var dyL = r * Math.Sin(_thinkPhase);
        var dxR = r * Math.Cos(_thinkPhase + 0.5);
        var dyR = r * Math.Sin(_thinkPhase + 0.5);

        if (LeftPupil?.RenderTransform is TranslateTransform lt)
        {
            lt.X = dxL;
            lt.Y = dyL;
        }

        if (RightPupil?.RenderTransform is TranslateTransform rt)
        {
            rt.X = dxR;
            rt.Y = dyR;
        }

        // Leichtes Pulsieren der Aura (Farbe bleibt „think“-Blau aus ApplyVisualState).
        if (Aura is Ellipse aura)
            aura.Opacity = AuraBaseOpacity + 0.14 * Math.Sin(_thinkPhase * 1.4);
    }

    private void OnOpened(object? sender, EventArgs e)
    {
        if (OperatingSystem.IsWindows())
            Win32ClickThrough.EnableForWindow(this);
        FollowCursor();
        _followTimer.Start();
        ApplyVisualState(CompanionVisualState.Ready);
    }

    private void FollowCursor()
    {
        if (!CursorTracking.GetCursorPos(out var p))
            return;

        Position = new PixelPoint(p.X + OffsetX, p.Y + OffsetY);
    }

    protected override void OnClosing(WindowClosingEventArgs e)
    {
        CompanionHub.JumpToTargetScreenRect -= OnJumpToTargetScreenRect;
        _followTimer.Stop();
        _thinkingAnimTimer.Stop();
        base.OnClosing(e);
    }
}
