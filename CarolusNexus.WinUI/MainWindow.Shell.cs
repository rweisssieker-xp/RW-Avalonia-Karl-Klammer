using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using CarolusNexus;
using CarolusNexus.Services;
using CarolusNexus_WinUI.Pages;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using WinUiVisibility = Microsoft.UI.Xaml.Visibility;
using WinRT.Interop;

namespace CarolusNexus_WinUI;

public sealed partial class MainWindow
{
    private readonly Dictionary<Type, Page> _pageCache = new();
    private KarlCompanionWinUiWindow? _companionWin;
    private WinUiTrayHelper? _tray;
    private PushToTalkHotkeyWindow? _hotkeyWindow;
    private DispatcherQueueTimer? _releasePollTimer;
    private DispatcherQueueTimer? _pollFallbackTimer;
    private DispatcherQueueTimer? _dashTimer;
    private DispatcherQueueTimer? _statusActivityTimer;
    private bool _pollFallbackDown;
    private bool _exitRequested;
    private int _pttVk = PushToTalkKey.DefaultVirtualKey;
    private AppWindow? _appW;

    private void OnMainShellLoaded(object sender, RoutedEventArgs e)
    {
        WinUiShellState.MainWindowRef = this;
        NexusShell.SetGlobalStatusLine = t => _statusLine.Text = t;
        NexusShell.SetGlobalBusyIndicator = v =>
            _globalStatusBusyBar.Visibility = v ? WinUiVisibility.Visible : WinUiVisibility.Collapsed;
        WinUiShellState.SetStatusLine = NexusShell.SetGlobalStatus;
        ActivityStatusHub.RefreshFromStores();

        try
        {
            var hwnd = WindowNative.GetWindowHandle(this);
            var wid = Microsoft.UI.Win32Interop.GetWindowIdFromWindow(hwnd);
            _appW = AppWindow.GetFromWindowId(wid);
            _appW.Closing += OnAppWindowClosing;
        }
        catch
        {
            // ignore
        }

        _tray = new WinUiTrayHelper(
            () =>
            {
                try
                {
                    var h = WindowNative.GetWindowHandle(this);
                    ShowWindow(h, SwShow);
                }
                catch
                {
                    // ignore
                }
            },
            () =>
            {
                _exitRequested = true;
                _tray?.Dispose();
                _tray = null;
                Application.Current.Exit();
            });

        _companionToggle.Toggled += OnCompanionToggleToggled;
        if (OperatingSystem.IsWindows() && _companionToggle.IsOn)
            EnsureCompanion();

        var dq = DispatcherQueue.GetForCurrentThread();
        WinUiShellState.UiDispatcher = dq;
        _releasePollTimer = dq.CreateTimer();
        _releasePollTimer.Interval = TimeSpan.FromMilliseconds(60);
        _releasePollTimer.Tick += OnReleasePollTick;

        _pollFallbackTimer = dq.CreateTimer();
        _pollFallbackTimer.Interval = TimeSpan.FromMilliseconds(120);
        _pollFallbackTimer.Tick += OnPollFallbackTick;

        _dashTimer = dq.CreateTimer();
        _dashTimer.Interval = TimeSpan.FromSeconds(4);
        _dashTimer.Tick += OnDashboardTick;

        _statusActivityTimer = dq.CreateTimer();
        _statusActivityTimer.Interval = TimeSpan.FromSeconds(1);
        _statusActivityTimer.Tick += (_, _) => ActivityStatusHub.RefreshFromStores();

        SetupPushToTalk();
        _dashTimer.Start();
        _statusActivityTimer.Start();
        NexusShell.Log("WinUI: tray · companion · PTT · cached pages · dashboard timer.");
    }

    private void OnAppWindowClosing(AppWindow sender, AppWindowClosingEventArgs args)
    {
        if (_exitRequested)
            return;
        args.Cancel = true;
        try
        {
            var h = WindowNative.GetWindowHandle(this);
            ShowWindow(h, SwHide);
        }
        catch
        {
            // ignore
        }

        WinUiShellState.SetStatus("Minimized to tray");
    }

    private void OnHeaderCloseClick(object sender, RoutedEventArgs e)
    {
        try
        {
            var h = WindowNative.GetWindowHandle(this);
            ShowWindow(h, SwHide);
            WinUiShellState.SetStatus("Minimized to tray");
        }
        catch
        {
            // ignore
        }
    }

    private void OnCompanionToggleToggled(object sender, RoutedEventArgs e)
    {
        if (!OperatingSystem.IsWindows())
            return;
        if (sender is not ToggleSwitch ts)
            return;
        if (ts.IsOn)
            EnsureCompanion();
        else
        {
            _companionWin?.StopFollow();
            _companionWin?.Close();
            _companionWin = null;
        }
    }

    private void EnsureCompanion()
    {
        _companionWin ??= new KarlCompanionWinUiWindow();
        _companionWin.StartFollow();
    }

    private void SetupPushToTalk()
    {
        _releasePollTimer?.Stop();
        _pollFallbackTimer?.Stop();
        if (!OperatingSystem.IsWindows())
            return;

        RefreshPushToTalkKey();
        try
        {
            _hotkeyWindow?.Dispose();
            _hotkeyWindow = new PushToTalkHotkeyWindow(() =>
            {
                DispatcherQueue.GetForCurrentThread().TryEnqueue(() =>
                {
                    WinUiShellState.RaisePttPressed();
                    if (WinUiShellState.PttAwaitsHotkeyRelease?.Invoke() == true)
                        _releasePollTimer.Start();
                });
            });

            if (_hotkeyWindow.TryRegister(_pttVk))
            {
                NexusShell.Log($"PTT: global hotkey VK 0x{_pttVk:X}");
                return;
            }
        }
        catch (Exception ex)
        {
            NexusShell.Log("PTT: " + ex.Message);
        }

        _hotkeyWindow?.Dispose();
        _hotkeyWindow = null;
        _pollFallbackTimer.Start();
        NexusShell.Log("PTT: fallback polling");
    }

    private void RefreshPushToTalkKey() =>
        _pttVk = PushToTalkKey.ResolveVirtualKey(DotEnvStore.Get("PUSH_TO_TALK_KEY"));

    private void OnReleasePollTick(DispatcherQueueTimer sender, object args)
    {
        if (!OperatingSystem.IsWindows())
            return;
        if (WinUiShellState.PttAwaitsHotkeyRelease?.Invoke() != true)
        {
            _releasePollTimer.Stop();
            return;
        }

        if ((Win32AsyncKey.GetAsyncKeyState(_pttVk) & 0x8000) == 0)
        {
            _releasePollTimer.Stop();
            _ = WinUiShellState.OnPttReleasedAsync?.Invoke();
        }
    }

    private void OnPollFallbackTick(DispatcherQueueTimer sender, object args)
    {
        if (!OperatingSystem.IsWindows())
            return;
        var down = (Win32AsyncKey.GetAsyncKeyState(_pttVk) & 0x8000) != 0;
        if (down && !_pollFallbackDown)
            WinUiShellState.RaisePttPressed();

        if (!down && _pollFallbackDown)
            _ = WinUiShellState.OnPttReleasedAsync?.Invoke();
        _pollFallbackDown = down;
    }

    private void OnDashboardTick(DispatcherQueueTimer sender, object args)
    {
        if (_pageCache.TryGetValue(typeof(DashboardShellPage), out var p) && p is DashboardShellPage d)
            d.RefreshFull();
    }

    private const int SwHide = 0;
    private const int SwShow = 5;

    [System.Runtime.InteropServices.DllImport("user32.dll")]
    private static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);
}
