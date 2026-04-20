using System;
using System.IO;
using CarolusNexus;
using CarolusNexus.Services;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;

namespace CarolusNexus_WinUI;

public partial class App
{
    private Window? _window;

    public App()
    {
        InitializeComponent();
        UnhandledException += (_, e) =>
        {
            try
            {
                var log = Path.Combine(Path.GetTempPath(), "CarolusNexus_WinUI_crash.txt");
                File.AppendAllText(log, $"{DateTime.UtcNow:O}\n{e.Message}\n{e.Exception}\n---\n");
            }
            catch
            {
                // ignore logging failure
            }
        };
    }

    protected override void OnLaunched(LaunchActivatedEventArgs args)
    {
        AppPaths.DiscoverRepoRoot();
        AppPaths.EnsureDataTree();
        WinUiShellState.Settings = WinUiShellState.SettingsStore.LoadOrDefault();
        WinUiThemeApplier.Apply(WinUiShellState.Settings.UiTheme);
        var dq = DispatcherQueue.GetForCurrentThread();
        if (dq != null)
            WinUiShellState.ApplyNexusContext(dq);

        NexusShell.Log($"WinUI start — repo: {AppPaths.RepoRoot}");
        _window = new MainWindow();
        _window.Activate();
    }
}
