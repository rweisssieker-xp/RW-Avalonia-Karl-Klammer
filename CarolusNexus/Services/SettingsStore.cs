using System.IO;
using System.Text.Json;
using CarolusNexus.Models;

namespace CarolusNexus.Services;

public sealed class SettingsStore
{
    private static readonly JsonSerializerOptions JsonOpts = new() { WriteIndented = true };

    public NexusSettings LoadOrDefault()
    {
        try
        {
            if (!File.Exists(AppPaths.SettingsFile))
                return new NexusSettings();
            var json = File.ReadAllText(AppPaths.SettingsFile);
            return JsonSerializer.Deserialize<NexusSettings>(json, JsonOpts) ?? new NexusSettings();
        }
        catch
        {
            return new NexusSettings();
        }
    }

    public void Save(NexusSettings s)
    {
        var json = JsonSerializer.Serialize(s, JsonOpts);
        File.WriteAllText(AppPaths.SettingsFile, json);
    }
}
