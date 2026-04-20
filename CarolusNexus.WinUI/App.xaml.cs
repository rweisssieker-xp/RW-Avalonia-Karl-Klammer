using CarolusNexus.Services;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;

namespace CarolusNexus_WinUI;

public partial class App : Application
{
    private Window? _window;

    public App()
    {
        InitializeComponent();
    }

    protected override void OnLaunched(LaunchActivatedEventArgs args)
    {
        WinUiShellState.Settings = WinUiShellState.SettingsStore.LoadOrDefault();
        var dq = DispatcherQueue.GetForCurrentThread();
        WinUiShellState.ApplyNexusContext(dq);

        _window = new MainWindow();
        _window.Activate();
    }
}
