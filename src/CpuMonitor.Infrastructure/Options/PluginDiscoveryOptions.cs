namespace CpuMonitor.Infrastructure.Options;

public sealed class PluginDiscoveryOptions
{
    public const string SectionName = "Plugins";

    public bool LoadExternalPlugins { get; set; } = true;

    public string Directory { get; set; } = "Plugins";
}
