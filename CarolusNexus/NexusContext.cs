using System;
using CarolusNexus.Models;

namespace CarolusNexus;

public static class NexusContext
{
    public static Func<NexusSettings> GetSettings { get; set; } = () => new();
}
