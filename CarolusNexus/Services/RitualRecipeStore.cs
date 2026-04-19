using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using CarolusNexus.Models;

namespace CarolusNexus.Services;

public static class RitualRecipeStore
{
    public static event Action? Saved;

    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    public static List<AutomationRecipe> LoadAll()
    {
        if (!File.Exists(AppPaths.AutomationRecipes))
            return new List<AutomationRecipe>();

        try
        {
            var json = File.ReadAllText(AppPaths.AutomationRecipes);
            var list = JsonSerializer.Deserialize<List<AutomationRecipe>>(json, JsonOpts);
            return list ?? new List<AutomationRecipe>();
        }
        catch
        {
            return new List<AutomationRecipe>();
        }
    }

    public static void SaveAll(IReadOnlyList<AutomationRecipe> recipes)
    {
        Directory.CreateDirectory(AppPaths.DataDir);
        var json = JsonSerializer.Serialize(recipes.ToList(), JsonOpts);
        File.WriteAllText(AppPaths.AutomationRecipes, json);
        Saved?.Invoke();
    }

    public static void AppendRecipe(AutomationRecipe recipe)
    {
        if (string.IsNullOrEmpty(recipe.Id))
            recipe.Id = Guid.NewGuid().ToString("n");
        var all = LoadAll();
        all.Add(recipe);
        SaveAll(all);
    }

    public static void Upsert(AutomationRecipe recipe)
    {
        if (string.IsNullOrEmpty(recipe.Id))
            recipe.Id = Guid.NewGuid().ToString("n");
        var all = LoadAll();
        var i = all.FindIndex(r => string.Equals(r.Id, recipe.Id, StringComparison.Ordinal));
        if (i >= 0)
            all[i] = recipe;
        else
            all.Add(recipe);
        SaveAll(all);
    }

    public static void DeleteById(string id)
    {
        var all = LoadAll().Where(r => !string.Equals(r.Id, id, StringComparison.Ordinal)).ToList();
        SaveAll(all);
    }
}
