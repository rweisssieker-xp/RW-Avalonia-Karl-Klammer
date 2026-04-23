using System;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using CarolusNexus.Models;

namespace CarolusNexus.Services;

public static class AiPrivacyFirewallService
{
    private static readonly Regex EmailRegex = new(@"[A-Z0-9._%+-]+@[A-Z0-9.-]+\.[A-Z]{2,}", RegexOptions.IgnoreCase | RegexOptions.Compiled);
    private static readonly Regex IbanRegex = new(@"\b[A-Z]{2}\d{2}[A-Z0-9]{11,30}\b", RegexOptions.IgnoreCase | RegexOptions.Compiled);
    private static readonly Regex MoneyRegex = new(@"\b\d{1,3}(?:[.,]\d{3})*(?:[.,]\d{2})\s?(?:EUR|€|USD|\$)\b", RegexOptions.IgnoreCase | RegexOptions.Compiled);
    private static readonly Regex SecretRegex = new(@"\b(?:api[_-]?key|token|secret|password|pwd)\s*[:=]\s*\S+", RegexOptions.IgnoreCase | RegexOptions.Compiled);

    public static string BuildFirewallReport(NexusSettings settings, string? text)
    {
        var input = text ?? "";
        var email = EmailRegex.Matches(input).Count;
        var iban = IbanRegex.Matches(input).Count;
        var money = MoneyRegex.Matches(input).Count;
        var secret = SecretRegex.Matches(input).Count;
        var total = email + iban + money + secret;
        var block = secret > 0 || (settings.Safety.Profile.Equals("strict", StringComparison.OrdinalIgnoreCase) && total > 0);
        var sb = new StringBuilder();
        sb.AppendLine("Local AI Privacy Firewall");
        sb.AppendLine($"Generated: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
        sb.AppendLine();
        sb.AppendLine($"Decision: {(block ? "BLOCK / require local redaction" : total > 0 ? "REDACT before model call" : "ALLOW")}");
        sb.AppendLine($"Safety profile: {settings.Safety.Profile}");
        sb.AppendLine();
        sb.AppendLine($"- Emails: {email}");
        sb.AppendLine($"- IBAN-like values: {iban}");
        sb.AppendLine($"- Money amounts: {money}");
        sb.AppendLine($"- Secrets/tokens: {secret}");
        sb.AppendLine();
        sb.AppendLine("Redaction preview");
        sb.AppendLine(Redact(input.Length == 0 ? "(no prompt/context supplied)" : input));
        return sb.ToString().TrimEnd();
    }

    public static string Redact(string text)
    {
        var value = EmailRegex.Replace(text, "[email]");
        value = IbanRegex.Replace(value, "[iban]");
        value = MoneyRegex.Replace(value, "[amount]");
        value = SecretRegex.Replace(value, "[secret]");
        return value;
    }

    public static string ExportFirewallPack(NexusSettings settings, string? text)
    {
        Directory.CreateDirectory(AppPaths.DataDir);
        var path = Path.Combine(AppPaths.DataDir, $"privacy-firewall-{DateTime.Now:yyyyMMdd-HHmmss}.md");
        File.WriteAllText(path, BuildFirewallReport(settings, text) + Environment.NewLine);
        return path;
    }
}
