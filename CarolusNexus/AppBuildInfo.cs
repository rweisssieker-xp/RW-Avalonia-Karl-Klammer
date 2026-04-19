using System.Reflection;

namespace CarolusNexus;

public static class AppBuildInfo
{
    public static string Version =>
        Assembly.GetExecutingAssembly().GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion
        ?? Assembly.GetExecutingAssembly().GetName().Version?.ToString()
        ?? "?";

    public static string Summary =>
        $"Carolus Nexus · {Version} · .NET {System.Environment.Version} · {System.Environment.OSVersion}";
}
