using System;
using System.Net.Http;
using CarolusNexus.Models;

namespace CarolusNexus.Services;

/// <summary>Kanal <c>api</c>: einfache GET/POST-Aufrufe (kein UI).</summary>
public static class ApiHookRunner
{
    private static readonly HttpClient Http = new() { Timeout = TimeSpan.FromMinutes(2) };

    public static string TryRun(RecipeStep step, NexusSettings settings)
    {
        if (!string.Equals(settings.Safety.Profile, "power-user", StringComparison.OrdinalIgnoreCase))
            return "[SKIP] api channel requires safety profile power-user";

        var raw = (step.ActionArgument ?? "").Trim();
        if (raw.Length == 0)
            return "[SKIP] api: empty";

        if (!PlanGuard.IsAllowed(settings, raw))
            return "[BLOCKED] Safety-Policy";

        try
        {
            if (raw.StartsWith("api.get:", StringComparison.OrdinalIgnoreCase))
            {
                var url = raw["api.get:".Length..].Trim();
                var s = Http.GetStringAsync(url).GetAwaiter().GetResult();
                return "[OK] api len=" + s.Length;
            }

            if (raw.StartsWith("api.post:", StringComparison.OrdinalIgnoreCase))
            {
                var rest = raw["api.post:".Length..].Trim();
                var pipe = rest.IndexOf('|');
                if (pipe < 0)
                    return "[SKIP] api.post: expected url|body";
                var url = rest[..pipe].Trim();
                var body = rest[(pipe + 1)..];
                using var c = new StringContent(body, System.Text.Encoding.UTF8, "application/json");
                var s = Http.PostAsync(url, c).GetAwaiter().GetResult().Content.ReadAsStringAsync().GetAwaiter()
                    .GetResult();
                return "[OK] api len=" + s.Length;
            }

            return "[SKIP] api: use api.get:URL or api.post:URL|body";
        }
        catch (Exception ex)
        {
            return "[ERR] api: " + ex.Message;
        }
    }
}
