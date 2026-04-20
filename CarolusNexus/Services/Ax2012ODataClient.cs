using System;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using CarolusNexus.Models;

namespace CarolusNexus.Services;

/// <summary>
/// Minimaler OData-/HTTP-Lesezugriff auf AX 2012 (Testmandant, kein Secret im JSON — Basic über .env).
/// </summary>
public static class Ax2012ODataClient
{
    /// <summary>GET relativ zu <see cref="NexusSettings.AxODataBaseUrl"/>.</summary>
    public static async Task<string> GetAsync(NexusSettings settings, string relativePathAndQuery, CancellationToken ct)
    {
        var baseUrl = (settings.AxODataBaseUrl ?? "").Trim();
        if (string.IsNullOrEmpty(baseUrl))
            return "[SKIP] AxODataBaseUrl is empty — set in Setup / settings.json";

        if (!Uri.TryCreate(baseUrl, UriKind.Absolute, out var baseUri))
            return "[ERR] AxODataBaseUrl is not a valid absolute URL";

        var rel = (relativePathAndQuery ?? "").Trim().TrimStart('/');
        Uri requestUri;
        try
        {
            requestUri = rel.Length == 0 ? baseUri : new Uri(baseUri, rel);
        }
        catch (Exception ex)
        {
            return "[ERR] invalid OData path: " + ex.Message;
        }

        using var handler = new HttpClientHandler();
        if (settings.AxODataUseDefaultCredentials)
        {
            handler.UseDefaultCredentials = true;
        }
        else
        {
            var u = DotEnvStore.Get("AX_HTTP_USER");
            var p = DotEnvStore.Get("AX_HTTP_PASSWORD");
            if (string.IsNullOrWhiteSpace(u))
                return "[SKIP] AxODataUseDefaultCredentials=false but AX_HTTP_USER missing in .env";

            handler.Credentials = new NetworkCredential(u, p ?? "");
        }

        using var client = new HttpClient(handler) { Timeout = TimeSpan.FromSeconds(60) };
        using var resp = await client.GetAsync(requestUri, ct).ConfigureAwait(false);
        var body = await resp.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
        var code = (int)resp.StatusCode;
        var snippet = Truncate(body, 8000);
        return $"[OData HTTP {code}] {requestUri}\n{snippet}";
    }

    /// <summary>Für synchrone Plan-Pipeline (kein UI-Deadlock: <see cref="Task.Run"/>).</summary>
    public static string GetStringSync(NexusSettings settings, string relativePathAndQuery)
    {
        try
        {
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(90));
            return Task.Run(async () => await GetAsync(settings, relativePathAndQuery, cts.Token).ConfigureAwait(false))
                .GetAwaiter()
                .GetResult();
        }
        catch (Exception ex)
        {
            return "[ERR] OData: " + ex.Message;
        }
    }

    /// <summary>Beliebige GET-URL mit derselben Credential-Logik wie OData (AIF-Health / Metadaten-Ping).</summary>
    public static string GetAbsoluteSync(NexusSettings settings, string absoluteUrl)
    {
        var url = (absoluteUrl ?? "").Trim();
        if (string.IsNullOrEmpty(url) || !Uri.TryCreate(url, UriKind.Absolute, out var uri))
            return "[SKIP] invalid or empty URL";

        try
        {
            using var handler = new HttpClientHandler();
            if (settings.AxODataUseDefaultCredentials)
                handler.UseDefaultCredentials = true;
            else
            {
                var u = DotEnvStore.Get("AX_HTTP_USER");
                var p = DotEnvStore.Get("AX_HTTP_PASSWORD");
                if (string.IsNullOrWhiteSpace(u))
                    return "[SKIP] AX_HTTP_USER missing for Basic auth";
                handler.Credentials = new NetworkCredential(u, p ?? "");
            }

            using var client = new HttpClient(handler) { Timeout = TimeSpan.FromSeconds(60) };
            var resp = client.GetAsync(uri).GetAwaiter().GetResult();
            var body = resp.Content.ReadAsStringAsync().GetAwaiter().GetResult();
            return $"[HTTP {(int)resp.StatusCode}] {uri}\n{Truncate(body, 4000)}";
        }
        catch (Exception ex)
        {
            return "[ERR] HTTP GET: " + ex.Message;
        }
    }

    private static string Truncate(string s, int max)
    {
        if (s.Length <= max)
            return s;
        return s[..max] + "\n… (truncated)";
    }
}
