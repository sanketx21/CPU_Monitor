namespace CpuMonitor.Domain.Models;

public sealed record DiskUsage(
    string RootPath,
    double UsedMegabytes,
    double TotalMegabytes)
{
    public double UsedPercent => TotalMegabytes <= 0 ? 0 : UsedMegabytes / TotalMegabytes * 100;
}
