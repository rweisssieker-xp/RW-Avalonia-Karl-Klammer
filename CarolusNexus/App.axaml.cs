using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Avalonia.Threading;

namespace CarolusNexus;

public partial class App : Application
{
    public static MainWindow? Shell { get; private set; }

    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            desktop.ShutdownMode = ShutdownMode.OnExplicitShutdown;
            desktop.MainWindow = new MainWindow();
            Shell = desktop.MainWindow as MainWindow;
        }

        base.OnFrameworkInitializationCompleted();
    }

    private void OnTrayOpen(object? sender, EventArgs e)
    {
        if (Shell == null)
            return;
        Shell.Show();
        Shell.WindowState = WindowState.Normal;
        Shell.Activate();
    }

    private void OnTrayQuit(object? sender, EventArgs e)
    {
        AppLifecycle.UserRequestedExit = true;
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime d)
            d.Shutdown();
    }

    private async void OnTrayAskClipboard(object? sender, EventArgs e)
    {
        if (Shell is null)
            return;
        await Dispatcher.UIThread.InvokeAsync(() => Shell.AskFromClipboardAsync());
    }
}
