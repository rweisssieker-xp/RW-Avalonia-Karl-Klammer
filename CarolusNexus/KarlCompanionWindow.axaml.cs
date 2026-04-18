using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Threading;

namespace CarolusNexus;

public partial class KarlCompanionWindow : Window
{
    private readonly DispatcherTimer _followTimer;
    private const int OffsetX = 16;
    private const int OffsetY = 18;

    public KarlCompanionWindow()
    {
        InitializeComponent();
        _followTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(33) };
        _followTimer.Tick += (_, _) => FollowCursor();

        Opened += OnOpened;
    }

    private void OnOpened(object? sender, EventArgs e)
    {
        if (OperatingSystem.IsWindows())
            Win32ClickThrough.EnableForWindow(this);
        FollowCursor();
        _followTimer.Start();
    }

    private void FollowCursor()
    {
        if (!CursorTracking.GetCursorPos(out var p))
            return;

        Position = new PixelPoint(p.X + OffsetX, p.Y + OffsetY);
    }

    protected override void OnClosing(WindowClosingEventArgs e)
    {
        _followTimer.Stop();
        base.OnClosing(e);
    }
}
