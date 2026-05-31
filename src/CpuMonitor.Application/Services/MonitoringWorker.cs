using CpuMonitor.Application.Options;
using CpuMonitor.Domain.Models;
using CpuMonitor.Domain.Plugins;
using CpuMonitor.Domain.Services;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace CpuMonitor.Application.Services;

public sealed class MonitoringWorker : BackgroundService
{
    private readonly ISystemResourceReader _resourceReader;
    private readonly IMonitorPluginProvider _pluginProvider;
    private readonly IOptions<MonitorOptions> _options;
    private readonly ILogger<MonitoringWorker> _logger;

    public MonitoringWorker(
        ISystemResourceReader resourceReader,
        IMonitorPluginProvider pluginProvider,
        IOptions<MonitorOptions> options,
        ILogger<MonitoringWorker> logger)
    {
        _resourceReader = resourceReader;
        _pluginProvider = pluginProvider;
        _options = options;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var interval = TimeSpan.FromSeconds(Math.Max(1, _options.Value.IntervalSeconds));
        using var timer = new PeriodicTimer(interval);
        var plugins = _pluginProvider.GetPlugins();

        _logger.LogInformation("CPU Monitor started. Interval: {IntervalSeconds}s. Plugins: {PluginCount}.", interval.TotalSeconds, plugins.Count);

        await PublishSnapshotAsync(plugins, stoppingToken);

        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            await PublishSnapshotAsync(plugins, stoppingToken);
        }
    }

    private async Task PublishSnapshotAsync(IReadOnlyCollection<IMonitorPlugin> plugins, CancellationToken cancellationToken)
    {
        ResourceSnapshot snapshot;

        try
        {
            snapshot = await _resourceReader.ReadAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to read system resources.");
            return;
        }

        WriteToConsole(snapshot);

        foreach (var plugin in plugins)
        {
            try
            {
                await plugin.OnSnapshotAsync(snapshot, cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Plugin {PluginName} failed while processing a monitoring update.", plugin.Name);
            }
        }
    }

    private static void WriteToConsole(ResourceSnapshot snapshot)
    {
        Console.WriteLine(
            "[{0:yyyy-MM-dd HH:mm:ss zzz}] CPU: {1,6:N2}% | RAM: {2,8:N0}/{3:N0} MB ({4,6:N2}%) | Disk {5}: {6,8:N0}/{7:N0} MB ({8,6:N2}%)",
            snapshot.Timestamp,
            snapshot.CpuPercent,
            snapshot.RamUsedMegabytes,
            snapshot.RamTotalMegabytes,
            snapshot.RamUsedPercent,
            snapshot.Disk.RootPath,
            snapshot.Disk.UsedMegabytes,
            snapshot.Disk.TotalMegabytes,
            snapshot.Disk.UsedPercent);
    }
}
