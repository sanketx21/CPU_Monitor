namespace CpuMonitor.Application.Options;

public sealed class MonitorOptions
{
    public const string SectionName = "Monitor";

    public int IntervalSeconds { get; set; } = 5;

    public string? DiskRootPath { get; set; }
}
