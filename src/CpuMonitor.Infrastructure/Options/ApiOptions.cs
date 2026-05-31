namespace CpuMonitor.Infrastructure.Options;

public sealed class ApiOptions
{
    public const string SectionName = "Api";

    public bool Enabled { get; set; } = true;

    public string? EndpointUrl { get; set; }

    public int TimeoutSeconds { get; set; } = 10;
}
