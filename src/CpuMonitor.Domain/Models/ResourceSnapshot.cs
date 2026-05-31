namespace CpuMonitor.Domain.Models;

public sealed record ResourceSnapshot(
    DateTimeOffset Timestamp,
    double CpuPercent,
    double RamUsedMegabytes,
    double RamTotalMegabytes,
    DiskUsage Disk)
{
    public double RamUsedPercent => RamTotalMegabytes <= 0 ? 0 : RamUsedMegabytes / RamTotalMegabytes * 100;
}
