using System;
using System.Runtime.InteropServices;
using CarolusNexus;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media;
using Windows.UI;
using WinRT.Interop;

namespace CarolusNexus_WinUI;

/// <summary>Topmost WinUI window that follows the cursor (layout parity with Avalonia KarlCompanionWindow).</summary>
public sealed partial class KarlCompanionWinUiWindow
{
    private const int OffsetX = 16;
    private const int OffsetY = 18;

    private readonly DispatcherQueueTimer _timer;
    private readonly DispatcherQueueTimer _thinkingAnimTimer;
    private double _thinkPhase;
    private const double AuraBaseOpacity = 0.42;

    public KarlCompanionWinUiWindow()
    {
        InitializeComponent();

        Title = string.Empty;
        ExtendsContentIntoTitleBar = true;

        CompanionHub.StateChanged += OnHubState;
        CompanionHub.JumpToTargetScreenRect += OnJumpToTargetScreenRect;

        var dq = DispatcherQueue.GetForCurrentThread();
        _timer = dq.CreateTimer();
        _timer.Interval = TimeSpan.FromMilliseconds(33);
        _timer.Tick += (_, _) => FollowCursor();

        _thinkingAnimTimer = dq.CreateTimer();
        _thinkingAnimTimer.Interval = TimeSpan.FromMilliseconds(48);
        _thinkingAnimTimer.Tick += OnThinkingAnimTick;

        Closed += (_, _) =>
        {
            CompanionHub.StateChanged -= OnHubState;
            CompanionHub.JumpToTargetScreenRect -= OnJumpToTargetScreenRect;
            _timer.Stop();
            _thinkingAnimTimer.Stop();
        };

        Activated += OnFirstActivated;
    }

    private bool _chromeApplied;

    private void OnFirstActivated(object sender, WindowActivatedEventArgs e)
    {
        if (_chromeApplied)
            return;
        _chromeApplied = true;
        try
        {
            var hwnd = WindowNative.GetWindowHandle(this);
            var id = Microsoft.UI.Win32Interop.GetWindowIdFromWindow(hwnd);
            var aw = AppWindow.GetFromWindowId(id);
            if (aw?.Presenter is OverlappedPresenter ov)
            {
                ov.SetBorderAndTitleBar(false, false);
                ov.IsResizable = false;
                ov.IsMaximizable = false;
                ov.IsMinimizable = false;
            }

            aw?.Resize(new Windows.Graphics.SizeInt32 { Width = 64, Height = 96 });
            WinUiCompanionClickThrough.Enable(hwnd);
            TrySetTopMost(hwnd);
        }
        catch
        {
            // ignore
        }

        ApplyVisualState(CompanionVisualState.Ready);
        Activated -= OnFirstActivated;
    }

    public void StartFollow()
    {
        ShowSelf(true);
        _timer.Start();
    }

    public void StopFollow()
    {
        _timer.Stop();
        ShowSelf(false);
    }

    private void OnJumpToTargetScreenRect(int left, int top, int width, int height)
    {
        var cx = left + Math.Max(8, width / 2);
        var cy = top + Math.Max(8, height / 2);
        var dq = DispatcherQueue.GetForCurrentThread();
        dq?.TryEnqueue(() => MoveToClientPoint(cx + OffsetX, cy + OffsetY));
    }

    private void OnHubState(CompanionVisualState s) => ApplyVisualState(s);

    private void ApplyVisualState(CompanionVisualState state)
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

        AuraEllipse.Fill = new SolidColorBrush(ParseHexColor(hex));
        AuraEllipse.Opacity = AuraBaseOpacity;
        StateTag.Text = label;

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
        if (LeftPupil.RenderTransform is TranslateTransform lt)
        {
            lt.X = 0;
            lt.Y = 0;
        }

        if (RightPupil.RenderTransform is TranslateTransform rt)
        {
            rt.X = 0;
            rt.Y = 0;
        }
    }

    private void OnThinkingAnimTick(DispatcherQueueTimer sender, object args)
    {
        _thinkPhase += 0.22;
        const double r = 3.4;
        var dxL = r * Math.Cos(_thinkPhase);
        var dyL = r * Math.Sin(_thinkPhase);
        var dxR = r * Math.Cos(_thinkPhase + 0.5);
        var dyR = r * Math.Sin(_thinkPhase + 0.5);

        if (LeftPupil.RenderTransform is TranslateTransform lt)
        {
            lt.X = dxL;
            lt.Y = dyL;
        }

        if (RightPupil.RenderTransform is TranslateTransform rt)
        {
            rt.X = dxR;
            rt.Y = dyR;
        }

        AuraEllipse.Opacity = AuraBaseOpacity + 0.14 * Math.Sin(_thinkPhase * 1.4);
    }

    private static Color ParseHexColor(string hex)
    {
        hex = hex.TrimStart('#');
        if (hex.Length == 6 &&
            byte.TryParse(hex[..2], System.Globalization.NumberStyles.HexNumber, null, out var r) &&
            byte.TryParse(hex.Substring(2, 2), System.Globalization.NumberStyles.HexNumber, null, out var g) &&
            byte.TryParse(hex.Substring(4, 2), System.Globalization.NumberStyles.HexNumber, null, out var b))
            return Color.FromArgb(255, r, g, b);
        return Color.FromArgb(255, 128, 128, 128);
    }

    private static void TrySetTopMost(IntPtr hwnd)
    {
        try
        {
            SetWindowPos(hwnd, HwndTopmost, 0, 0, 0, 0, SwpNomove | SwpNosize);
        }
        catch
        {
            // ignore
        }
    }

    private void ShowSelf(bool visible)
    {
        try
        {
            var hwnd = WindowNative.GetWindowHandle(this);
            ShowWindow(hwnd, visible ? SwShow : SwHide);
        }
        catch
        {
            // ignore
        }
    }

    private void MoveToClientPoint(int x, int y)
    {
        try
        {
            var hwnd = WindowNative.GetWindowHandle(this);
            var id = Microsoft.UI.Win32Interop.GetWindowIdFromWindow(hwnd);
            var aw = AppWindow.GetFromWindowId(id);
            aw?.Move(new Windows.Graphics.PointInt32 { X = x, Y = y });
        }
        catch
        {
            // ignore
        }
    }

    private void FollowCursor()
    {
        if (!TryGetCursorPos(out var p))
            return;
        MoveToClientPoint(p.X + OffsetX, p.Y + OffsetY);
    }

    private const int SwHide = 0;
    private const int SwShow = 5;

    [DllImport("user32.dll")]
    private static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

    [DllImport("user32.dll")]
    private static extern bool GetCursorPos(out Point lpPoint);

    [DllImport("user32.dll", EntryPoint = "SetWindowPos")]
    private static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter, int x, int y, int cx, int cy, uint uFlags);

    private const int SwpNomove = 0x0002;
    private const int SwpNosize = 0x0001;
    private static readonly IntPtr HwndTopmost = new(-1);

    private static bool TryGetCursorPos(out Point p) => GetCursorPos(out p);

    [StructLayout(LayoutKind.Sequential)]
    private struct Point
    {
        public int X;
        public int Y;
    }
}
