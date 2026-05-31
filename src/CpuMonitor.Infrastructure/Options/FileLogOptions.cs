namespace CpuMonitor.Infrastructure.Options;

public sealed class FileLogOptions
{
    public const string SectionName = "FileLog";

    public bool Enabled { get; set; } = true;

    public string FilePath { get; set; } = "logs/monitor.log";
}
