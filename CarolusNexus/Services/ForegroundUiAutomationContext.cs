using System;
using System.Collections.Generic;
using System.Text;
using System.Windows.Automation;
using CarolusNexus.Models;

namespace CarolusNexus.Services;

/// <summary>
/// Bündelt lesende UIA-Informationen zum Vordergrundfenster (Form-/Kontext-Summary, §15/§2 Handbuch).
/// </summary>
public static class ForegroundUiAutomationContext
{
    private const int DefaultMaxChars = 12_000;

    /// <summary>
    /// Kompakte Liste interessanter Steuerelemente (Edit, Button, …) bis <paramref name="maxItems"/> Einträge.
    /// </summary>
    public static string BuildFormSummary(NexusSettings settings, int maxItems = 48, int maxDepth = 14)
    {
        if (!OperatingSystem.IsWindows())
            return "(not Windows)";

        if (!string.Equals(settings.Safety.Profile, "power-user", StringComparison.OrdinalIgnoreCase))
            return "(UIA form summary: requires safety profile “power-user”)";

        try
        {
            var root = AutomationElement.FromHandle(UiAutomationSnapshotHelpers.GetForegroundWindow());
            if (root == null)
                return "(no foreground automation root)";

            var lines = new List<string>(maxItems + 8);
            lines.Add($"Window: “{root.Current.Name}” · class hint · depth≤{maxDepth}");

            var acc = new List<string>(maxItems);
            Walk(root, 0, maxDepth, maxItems, acc);
            foreach (var line in acc)
                lines.Add(line);

            var sb = new StringBuilder();
            foreach (var line in lines)
            {
                if (sb.Length + line.Length + 1 > DefaultMaxChars)
                {
                    sb.AppendLine("… (truncated)");
                    break;
                }

                sb.AppendLine(line);
            }

            return sb.ToString().TrimEnd();
        }
        catch (Exception ex)
        {
            return "(UIA summary error: " + ex.Message + ")";
        }
    }

    /// <summary>
    /// Kurze Zeile zu Grid-/Listen-Selektion, falls <see cref="SelectionPattern"/> oder <see cref="TablePattern"/> greift.
    /// </summary>
    public static string? TryReadSelectionHint(NexusSettings settings)
    {
        if (!OperatingSystem.IsWindows())
            return null;
        if (!string.Equals(settings.Safety.Profile, "power-user", StringComparison.OrdinalIgnoreCase))
            return null;

        try
        {
            var root = AutomationElement.FromHandle(UiAutomationSnapshotHelpers.GetForegroundWindow());
            if (root == null)
                return null;

            var selHost = root.FindFirst(TreeScope.Descendants,
                new PropertyCondition(AutomationElement.IsSelectionPatternAvailableProperty, true));
            if (selHost != null
                && selHost.TryGetCurrentPattern(SelectionPattern.Pattern, out var spObj)
                && spObj is SelectionPattern sp)
            {
                var sel = sp.Current.GetSelection();
                if (sel == null || sel.Length == 0)
                    return "Selection: (empty)";

                var names = new List<string>(Math.Min(sel.Length, 8));
                foreach (AutomationElement a in sel)
                {
                    try
                    {
                        names.Add(a.Current.Name ?? "(unnamed)");
                    }
                    catch
                    {
                        names.Add("(?)");
                    }

                    if (names.Count >= 8)
                        break;
                }

                return "Selection: " + string.Join(" · ", names);
            }

            return null;
        }
        catch
        {
            return null;
        }
    }

    private static void Walk(AutomationElement el, int depth, int maxDepth, int maxItems, List<string> acc)
    {
        if (acc.Count >= maxItems || depth > maxDepth)
            return;

        try
        {
            var ct = el.Current.ControlType;
            if (IsInteresting(ct))
            {
                var name = el.Current.Name ?? "";
                var aid = el.Current.AutomationId ?? "";
                var line = $"{DepthIndent(depth)}{ControlLabel(ct)} · id={aid} · name={Truncate(name, 120)}";
                acc.Add(line);
            }
        }
        catch
        {
            /* transient UIA */
        }

        AutomationElement? child;
        try
        {
            child = TreeWalker.RawViewWalker.GetFirstChild(el);
        }
        catch
        {
            return;
        }

        while (child != null && acc.Count < maxItems)
        {
            Walk(child, depth + 1, maxDepth, maxItems, acc);
            try
            {
                child = TreeWalker.RawViewWalker.GetNextSibling(child);
            }
            catch
            {
                break;
            }
        }
    }

    private static string DepthIndent(int d)
    {
        if (d <= 0)
            return "";
        return new string(' ', Math.Min(d * 2, 24));
    }

    private static bool IsInteresting(ControlType ct)
    {
        return ct == ControlType.Edit
               || ct == ControlType.Button
               || ct == ControlType.CheckBox
               || ct == ControlType.RadioButton
               || ct == ControlType.ComboBox
               || ct == ControlType.List
               || ct == ControlType.ListItem
               || ct == ControlType.DataItem
               || ct == ControlType.TabItem
               || ct == ControlType.MenuItem
               || ct == ControlType.Hyperlink
               || ct == ControlType.Text;
    }

    private static string ControlLabel(ControlType ct)
    {
        try
        {
            return ct.LocalizedControlType;
        }
        catch
        {
            return "control";
        }
    }

    private static string Truncate(string s, int max)
    {
        if (s.Length <= max)
            return s;
        return s[..max] + "…";
    }
}
