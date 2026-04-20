using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using System.Threading;
using System.Windows.Forms;
using CarolusNexus.Models;

namespace CarolusNexus.Services;

/// <summary>Experimentelle Ausführung einzelner Plan-Tokens (nur Windows, nur mit power-user + PlanGuard).</summary>
public static class Win32AutomationExecutor
{
    private static readonly Regex ActionRx = new(@"\[ACTION:(\w+)(?:\|([^\]]*))?\]", RegexOptions.IgnoreCase | RegexOptions.Compiled);
    private static readonly Regex BrowserOpen = new(@"browser\.open\s*:\s*(.+)", RegexOptions.IgnoreCase | RegexOptions.Compiled);
    private static readonly Regex ExplorerPath = new(@"explorer\.open_path\s*:\s*(.+)", RegexOptions.IgnoreCase | RegexOptions.Compiled);

    /// <summary>UI-Automation mit optionalem CV-Fallback <see cref="RecipeStep.FallbackCvTemplatePath"/>.</summary>
    public static string ExecuteWithCvFallback(RecipeStep step, NexusSettings settings)
    {
        var msg = Execute(step, settings);
        if (!ShouldTryCvFallback(msg, step))
            return msg;
        return TryCvSelfHeal(step, msg);
    }

    private static bool ShouldTryCvFallback(string msg, RecipeStep step)
    {
        if (!msg.StartsWith("[ERR]", StringComparison.Ordinal))
            return false;
        var p = (step.FallbackCvTemplatePath ?? "").Trim();
        return p.Length > 0 && File.Exists(p);
    }

    private static string TryCvSelfHeal(RecipeStep step, string primaryMsg)
    {
        try
        {
            var cv = CvTemplateMatchService.TryClickTemplate(step.FallbackCvTemplatePath!.Trim());
            return "[SELF-HEAL] " + primaryMsg + " → " + cv;
        }
        catch (Exception ex)
        {
            return primaryMsg + " (self-heal failed: " + ex.Message + ")";
        }
    }

    public static string Execute(RecipeStep step, NexusSettings settings)
    {
        if (!OperatingSystem.IsWindows())
            return "[SKIP] not Windows";

        var arg = step.ActionArgument?.Trim() ?? "";
        if (string.IsNullOrEmpty(arg))
            return "[SKIP] empty";

        if (!PlanGuard.IsAllowed(settings, arg))
            return "[BLOCKED] Safety-Policy";

        try
        {
            if (CvTemplateMatchService.TryParseAndExecute(arg, out var cvMsg))
                return cvMsg;

            if (UiAutomationActions.TryParseAndExecute(arg, settings, out var uiaMsg))
                return uiaMsg;

            if (AppFamilyLauncher.TryLaunch(arg, settings, out var appMsg))
                return appMsg;

            if (AxClientAutomationService.TryExecute(arg, settings, out var axMsg))
                return axMsg;

            var m = ActionRx.Match(arg);
            if (m.Success)
            {
                var kind = m.Groups[1].Value.ToLowerInvariant();
                var payload = m.Groups[2].Success ? m.Groups[2].Value : "";
                return kind switch
                {
                    "hotkey" => SendHotkey(payload),
                    "type" => SendType(payload),
                    "open" => OpenTarget(payload),
                    "click" => DoClick(),
                    "move" => DoMove(payload),
                    _ => $"[SKIP] ACTION:{kind}"
                };
            }

            if (BrowserOpen.IsMatch(arg))
            {
                var url = BrowserOpen.Match(arg).Groups[1].Value.Trim();
                Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
                return "[OK] browser.open";
            }

            if (ExplorerPath.IsMatch(arg))
            {
                var path = ExplorerPath.Match(arg).Groups[1].Value.Trim();
                Process.Start("explorer.exe", path);
                return "[OK] explorer.open_path";
            }

            if (arg.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
                arg.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
            {
                Process.Start(new ProcessStartInfo(arg) { UseShellExecute = true });
                return "[OK] URL";
            }

            return "[SKIP] Token not executable: " + arg;
        }
        catch (Exception ex)
        {
            return "[ERR] " + ex.Message;
        }
    }

    private static string SendHotkey(string combo)
    {
        if (string.IsNullOrWhiteSpace(combo))
            return "[SKIP] hotkey empty";
        var sk = ToSendKeys(combo);
        SendKeys.SendWait(sk);
        return "[OK] hotkey " + combo;
    }

    private static string ToSendKeys(string combo)
    {
        var parts = combo.Split('+', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        var prefix = "";
        string? key = null;
        foreach (var p in parts)
        {
            if (p.Equals("Ctrl", StringComparison.OrdinalIgnoreCase) ||
                p.Equals("Control", StringComparison.OrdinalIgnoreCase))
                prefix += "^";
            else if (p.Equals("Alt", StringComparison.OrdinalIgnoreCase))
                prefix += "%";
            else if (p.Equals("Shift", StringComparison.OrdinalIgnoreCase))
                prefix += "+";
            else
                key = p;
        }

        if (string.IsNullOrEmpty(key))
            return prefix;
        if (key.Length == 1)
            return prefix + key.ToLowerInvariant();
        return prefix + "{" + key + "}";
    }

    private static string SendType(string text)
    {
        if (string.IsNullOrEmpty(text))
            return "[SKIP] type empty";
        Thread.Sleep(30);
        string? prev = null;
        try
        {
            prev = Clipboard.ContainsText() ? Clipboard.GetText() : null;
        }
        catch
        {
            /* ignore */
        }

        UndoStackService.PushClipboardText(prev);
        Clipboard.SetText(text);
        Thread.Sleep(30);
        SendKeys.SendWait("^v");
        return "[OK] type (Clipboard)";
    }

    private static string OpenTarget(string target)
    {
        if (string.IsNullOrWhiteSpace(target))
            return "[SKIP] open empty";
        Process.Start(new ProcessStartInfo(target.Trim()) { UseShellExecute = true });
        return "[OK] open";
    }

    private static string DoClick()
    {
        mouse_event(MOUSEEVENTF_LEFTDOWN, 0, 0, 0, UIntPtr.Zero);
        mouse_event(MOUSEEVENTF_LEFTUP, 0, 0, 0, UIntPtr.Zero);
        return "[OK] click";
    }

    /// <summary>Payload: <c>x,y</c> (Komma, Semikolon oder Leerzeichen).</summary>
    private static string DoMove(string payload)
    {
        if (string.IsNullOrWhiteSpace(payload))
            return "[SKIP] move ohne Koordinate";

        var parts = payload.Split(new[] { ',', ';', ' ' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (parts.Length < 2
            || !int.TryParse(parts[0], System.Globalization.NumberStyles.Integer, null, out var x)
            || !int.TryParse(parts[1], System.Globalization.NumberStyles.Integer, null, out var y))
            return "[SKIP] move: expected two integers (x,y)";

        if (!SetCursorPos(x, y))
            return "[ERR] SetCursorPos";
        return "[OK] move";
    }

    private const uint MOUSEEVENTF_LEFTDOWN = 0x0002;
    private const uint MOUSEEVENTF_LEFTUP = 0x0004;

    [DllImport("user32.dll")]
    private static extern void mouse_event(uint dwFlags, uint dx, uint dy, uint dwData, UIntPtr dwExtraInfo);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool SetCursorPos(int X, int Y);
}
