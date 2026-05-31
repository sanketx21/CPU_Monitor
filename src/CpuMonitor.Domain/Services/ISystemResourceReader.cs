using CpuMonitor.Domain.Models;

namespace CpuMonitor.Domain.Services;

public interface ISystemResourceReader
{
    Task<ResourceSnapshot> ReadAsync(CancellationToken cancellationToken);
}
