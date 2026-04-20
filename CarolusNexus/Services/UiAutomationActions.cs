using System;
using System.Linq;
using System.Text.RegularExpressions;
using System.Windows.Automation;
using CarolusNexus;
using CarolusNexus.Models;

namespace CarolusNexus.Services;

/// <summary>
/// Erste ausführbare UIA-Schritte unter power-user + <see cref="PlanGuard"/>.
/// Token: <c>uia.invoke:automationId=Save</c>, <c>uia.invoke:name=OK</c>,
/// <c>uia.setvalue:automationId=Edit|value=hello</c>
/// </summary>
public static class UiAutomationActions
{
    private static readonly Regex InvokeRx = new(
        @"^uia\.invoke:(.+)$",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly Regex SetValueRx = new(
        @"^uia\.setvalue:(.+)$",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    public static bool TryParseAndExecute(string argument, NexusSettings settings, out string message)
    {
        message = "";
        var arg = (argument ?? "").Trim();
        if (!arg.StartsWith("uia.", StringComparison.OrdinalIgnoreCase))
            return false;

        if (!OperatingSystem.IsWindows())
        {
            message = "[SKIP] UIA not Windows";
            return true;
        }

        if (!string.Equals(settings.Safety.Profile, "power-user", StringComparison.OrdinalIgnoreCase))
        {
            message = "[SKIP] UIA actions require safety profile power-user";
            return true;
        }

        if (!PlanGuard.IsAllowed(settings, arg))
        {
            message = "[BLOCKED] Safety-Policy";
            return true;
        }

        try
        {
            var root = AutomationElement.FromHandle(UiAutomationSnapshotHelpers.GetForegroundWindow());
            if (root == null)
            {
                message = "[ERR] no foreground window";
                return true;
            }

            var mInv = InvokeRx.Match(arg);
            if (mInv.Success)
            {
                var q = ParseQuery(mInv.Groups[1].Value);
                var el = FindElement(root, q);
                if (el == null)
                {
                    message = "[ERR] UIA element not found for invoke";
                    return true;
                }

                if (el.TryGetCurrentPattern(InvokePattern.Pattern, out var p) && p is InvokePattern inv)
                {
                    inv.Invoke();
                    TryPublishCompanionJump(el);
                    message = "[OK] uia.invoke";
                    return true;
                }

                message = "[ERR] InvokePattern not available";
                return true;
            }

            var mSet = SetValueRx.Match(arg);
            if (mSet.Success)
            {
                var parts = mSet.Groups[1].Value.Split('|', StringSplitOptions.TrimEntries);
                var queryPart = parts.FirstOrDefault(x => !x.StartsWith("value=", StringComparison.OrdinalIgnoreCase)) ?? "";
                var valuePart = parts.FirstOrDefault(x => x.StartsWith("value=", StringComparison.OrdinalIgnoreCase));
                if (valuePart == null)
                {
                    message = "[ERR] uia.setvalue needs |value=…";
                    return true;
                }

                var value = valuePart["value=".Length..].Trim();
                var q = ParseQuery(queryPart);
                var el = FindElement(root, q);
                if (el == null)
                {
                    message = "[ERR] UIA element not found for setvalue";
                    return true;
                }

                if (el.TryGetCurrentPattern(ValuePattern.Pattern, out var vp) && vp is ValuePattern val)
                {
                    val.SetValue(value);
                    message = "[OK] uia.setvalue";
                    return true;
                }

                message = "[ERR] ValuePattern not available";
                return true;
            }

            message = "[SKIP] UIA token not recognized";
            return true;
        }
        catch (Exception ex)
        {
            message = "[ERR] " + ex.Message;
            return true;
        }
    }

    private sealed class Query
    {
        public string? AutomationId { get; set; }
        public string? Name { get; set; }
    }

    private static Query ParseQuery(string raw)
    {
        var q = new Query();
        foreach (var piece in raw.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var eq = piece.IndexOf('=');
            if (eq <= 0)
                continue;
            var k = piece[..eq].Trim().ToLowerInvariant();
            var v = piece[(eq + 1)..].Trim();
            if (k is "automationid" or "id")
                q.AutomationId = v;
            else if (k is "name")
                q.Name = v;
        }

        return q;
    }

    private static AutomationElement? FindElement(AutomationElement root, Query q)
    {
        if (!string.IsNullOrEmpty(q.AutomationId))
        {
            var c = root.FindFirst(TreeScope.Descendants,
                new PropertyCondition(AutomationElement.AutomationIdProperty, q.AutomationId));
            if (c != null)
                return c;
        }

        if (!string.IsNullOrEmpty(q.Name))
        {
            return root.FindFirst(TreeScope.Descendants,
                new PropertyCondition(AutomationElement.NameProperty, q.Name));
        }

        return null;
    }

    private static void TryPublishCompanionJump(AutomationElement el)
    {
        try
        {
            var r = el.Current.BoundingRectangle;
            if (r.Width > 2 && r.Height > 2)
                CompanionHub.PublishJumpToTarget((int)r.Left, (int)r.Top, (int)r.Width, (int)r.Height);
        }
        catch
        {
            /* ignore */
        }
    }
}

/// <summary>Small Win32 hook for UIA root — kept separate to avoid changing public snapshot API.</summary>
internal static class UiAutomationSnapshotHelpers
{
    [System.Runtime.InteropServices.DllImport("user32.dll")]
    internal static extern nint GetForegroundWindow();
}
