using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;

namespace CarolusNexus.Services;

/// <summary>Lädt optionale <see cref="IOperatorAdapter"/>-Implementierungen aus <c>windows/plugins/*.dll</c>.</summary>
public static class PluginAdapterLoader
{
    public static IReadOnlyList<IOperatorAdapter> LoadAdapters()
    {
        var dir = Path.Combine(AppPaths.WindowsDir, "plugins");
        if (!Directory.Exists(dir))
            return Array.Empty<IOperatorAdapter>();

        var list = new List<IOperatorAdapter>();
        foreach (var dll in Directory.GetFiles(dir, "*.dll"))
        {
            try
            {
                var asm = Assembly.LoadFrom(dll);
                foreach (var t in asm.GetTypes())
                {
                    if (typeof(IOperatorAdapter).IsAssignableFrom(t) && t is { IsInterface: false, IsAbstract: false })
                    {
                        if (Activator.CreateInstance(t) is IOperatorAdapter a)
                        {
                            list.Add(a);
                            NexusShell.Log($"Plugin adapter: {t.FullName} from {Path.GetFileName(dll)}");
                        }
                    }
                }
            }
            catch (ReflectionTypeLoadException ex)
            {
                NexusShell.Log($"Plugin load types: {Path.GetFileName(dll)} — {ex.Message}");
            }
            catch (Exception ex)
            {
                NexusShell.Log($"Plugin load: {Path.GetFileName(dll)} — {ex.Message}");
            }
        }

        return list;
    }
}
