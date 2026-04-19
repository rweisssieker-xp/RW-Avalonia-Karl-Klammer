using System;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace CarolusNexus.Services;

/// <summary>Win32 <c>RegisterHotKey</c> über ein verstecktes <see cref="NativeWindow"/> (kein Dauer-Polling bei Ruhe).</summary>
public sealed class PushToTalkHotkeyWindow : NativeWindow, IDisposable
{
    private const int WmHotkey = 0x0312;
    private const int HotkeyId = 0x4e78;
    /// <summary>MOD_NOREPEAT — verhindert wiederholte WM_HOTKEY beim Halten.</summary>
    private const uint ModNorepeat = 0x4000;

    private readonly Action _onPressed;
    private bool _registered;

    public PushToTalkHotkeyWindow(Action onPressed)
    {
        _onPressed = onPressed ?? throw new ArgumentNullException(nameof(onPressed));
        CreateHandle(new CreateParams());
    }

    public bool TryRegister(int virtualKey)
    {
        Unregister();
        if (virtualKey <= 0 || virtualKey > 255)
            return false;
        _registered = RegisterHotKey(Handle, HotkeyId, ModNorepeat, (uint)virtualKey);
        return _registered;
    }

    public void Unregister()
    {
        if (!_registered || Handle == IntPtr.Zero)
            return;
        try
        {
            UnregisterHotKey(Handle, HotkeyId);
        }
        catch
        {
            // ignore
        }

        _registered = false;
    }

    protected override void WndProc(ref Message m)
    {
        if (m.Msg == WmHotkey && m.WParam.ToInt32() == HotkeyId)
        {
            try
            {
                _onPressed();
            }
            catch
            {
                // ignore
            }
        }

        base.WndProc(ref m);
    }

    public void Dispose()
    {
        Unregister();
        if (Handle != IntPtr.Zero)
        {
            try
            {
                DestroyHandle();
            }
            catch
            {
                // ignore
            }
        }

        GC.SuppressFinalize(this);
    }

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool UnregisterHotKey(IntPtr hWnd, int id);
}
