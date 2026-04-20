using System;
using System.Drawing;
using System.IO;
using System.Windows.Forms;

namespace CarolusNexus_WinUI;

/// <summary>Notification-area icon: restore main window or exit (parity with Avalonia tray).</summary>
public sealed class WinUiTrayHelper : IDisposable
{
    private readonly NotifyIcon _icon;

    public WinUiTrayHelper(Action onOpen, Action onExit)
    {
        _icon = new NotifyIcon
        {
            Visible = true,
            Text = "Carolus Nexus (WinUI)"
        };

        try
        {
            var icoPath = Path.Combine(AppContext.BaseDirectory, "Assets", "AppIcon.ico");
            if (File.Exists(icoPath))
                _icon.Icon = new Icon(icoPath);
        }
        catch
        {
            _icon.Icon = SystemIcons.Application;
        }

        var menu = new ContextMenuStrip();
        menu.Items.Add("Open Carolus Nexus", null, (_, _) => onOpen());
        menu.Items.Add("Exit", null, (_, _) => onExit());
        _icon.ContextMenuStrip = menu;
        _icon.DoubleClick += (_, _) => onOpen();
    }

    public void Dispose()
    {
        _icon.Visible = false;
        _icon.Dispose();
    }
}
