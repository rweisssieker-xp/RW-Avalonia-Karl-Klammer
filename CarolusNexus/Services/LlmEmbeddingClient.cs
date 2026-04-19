using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace CarolusNexus.Services;

public static class LlmEmbeddingClient
{
    private static readonly HttpClient Http = new() { Timeout = TimeSpan.FromMinutes(10) };

    public static async Task<IReadOnlyList<float[]>> EmbedBatchAsync(
        IReadOnlyList<string> inputs,
        string model,
        string baseUrl,
        string apiKey,
        CancellationToken ct)
    {
        if (inputs.Count == 0)
            return Array.Empty<float[]>();

        var url = baseUrl.TrimEnd('/') + "/embeddings";
        var body = JsonSerializer.Serialize(new { model, input = inputs.ToArray() });
        using var req = new HttpRequestMessage(HttpMethod.Post, url);
        req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiKey.Trim());
        req.Content = new StringContent(body, Encoding.UTF8, "application/json");

        using var resp = await Http.SendAsync(req, ct).ConfigureAwait(false);
        var json = await resp.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
        if (!resp.IsSuccessStatusCode)
            throw new InvalidOperationException($"Embeddings HTTP {(int)resp.StatusCode}: {json}");

        using var doc = JsonDocument.Parse(json);
        if (!doc.RootElement.TryGetProperty("data", out var data) || data.ValueKind != JsonValueKind.Array)
            throw new InvalidOperationException("Embeddings: kein data-Array.");

        var list = new List<float[]>();
        foreach (var item in data.EnumerateArray())
        {
            if (!item.TryGetProperty("embedding", out var emb) || emb.ValueKind != JsonValueKind.Array)
                continue;
            var arr = new float[emb.GetArrayLength()];
            var i = 0;
            foreach (var x in emb.EnumerateArray())
                arr[i++] = (float)x.GetDouble();
            list.Add(arr);
        }

        if (list.Count != inputs.Count)
            throw new InvalidOperationException($"Embeddings: erwartet {inputs.Count} Vektoren, erhalten {list.Count}.");
        return list;
    }
}
