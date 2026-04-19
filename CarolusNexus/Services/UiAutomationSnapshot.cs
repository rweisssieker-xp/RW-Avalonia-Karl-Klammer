using System;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows.Automation;

namespace CarolusNexus.Services;

/// <summary>Gekürzter Control-View-Baum des Vordergrundfensters für LLM-Kontext (nur Windows).</summary>
public static class UiAutomationSnapshot
{
    public static string TryBuildForForeground(int maxDepth = 4, int maxNodes = 80, int maxChars = 6000)
    {
        if (!OperatingSystem.IsWindows())
            return "";

        try
        {
            var d = ForegroundWindowInfo.TryReadDetail();
            if (d == null)
                return "";

            var root = AutomationElement.FromHandle(GetForegroundWindow());
            if (root == null)
                return "";

            var sb = new StringBuilder();
            sb.AppendLine("(UIA-Snapshot, gekürzt)");
            sb.AppendLine($"Fenster: {d.Value.Title} · {d.Value.ProcessName}");
            var count = 0;
            Walk(root, sb, 0, maxDepth, maxNodes, ref count);
            sb.AppendLine($"… Knoten ausgegeben: {count}");
            var s = sb.ToString();
            return s.Length <= maxChars ? s : s[..maxChars] + "\n…(gekürzt)";
        }
        catch (Exception ex)
        {
            return "(UIA: " + ex.Message + ")";
        }
    }

    private static void Walk(AutomationElement? el, StringBuilder sb, int depth, int maxDepth, int maxNodes, ref int count)
    {
        if (el == null || depth > maxDepth || count >= maxNodes)
            return;

        try
        {
            var name = el.Current.Name ?? "";
            var ctype = el.Current.ControlType.ProgrammaticName ?? "";
            var cid = el.Current.AutomationId ?? "";
            var cls = el.Current.ClassName ?? "";
            var indent = new string(' ', depth * 2);
            var line = $"{indent}- {ctype}";
            if (!string.IsNullOrEmpty(name))
                line += $" name=\"{Truncate(name, 80)}\"";
            if (!string.IsNullOrEmpty(cid))
                line += $" id=\"{Truncate(cid, 40)}\"";
            if (!string.IsNullOrEmpty(cls) && cls.Length < 60)
                line += $" class=\"{cls}\"";
            sb.AppendLine(line);
            count++;

            if (depth >= maxDepth)
                return;

            foreach (AutomationElement child in el.FindAll(TreeScope.Children, Condition.TrueCondition))
            {
                Walk(child, sb, depth + 1, maxDepth, maxNodes, ref count);
                if (count >= maxNodes)
                    break;
            }
        }
        catch
        {
            /* Element kann verschwinden */
        }
    }

    private static string Truncate(string s, int n) => s.Length <= n ? s : s[..n] + "…";

    [DllImport("user32.dll")]
    private static extern IntPtr GetForegroundWindow();
}
