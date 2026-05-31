using CpuMonitor.Application.Options;
using CpuMonitor.Domain.Plugins;
using CpuMonitor.Domain.Services;
using CpuMonitor.Infrastructure.Monitoring;
using CpuMonitor.Infrastructure.Options;
using CpuMonitor.Infrastructure.Plugins;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace CpuMonitor.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<MonitorOptions>(configuration.GetSection(MonitorOptions.SectionName));
        services.Configure<FileLogOptions>(configuration.GetSection(FileLogOptions.SectionName));
        services.Configure<ApiOptions>(configuration.GetSection(ApiOptions.SectionName));
        services.Configure<PluginDiscoveryOptions>(configuration.GetSection(PluginDiscoveryOptions.SectionName));

        services.AddSingleton<ISystemResourceReader, SystemResourceReader>();
        services.AddSingleton<ExternalPluginLoader>();

        var fileLogOptions = configuration.GetSection(FileLogOptions.SectionName).Get<FileLogOptions>() ?? new FileLogOptions();
        if (fileLogOptions.Enabled)
        {
            services.AddSingleton<IMonitorPlugin, FileLoggingMonitorPlugin>();
        }

        var apiOptions = configuration.GetSection(ApiOptions.SectionName).Get<ApiOptions>() ?? new ApiOptions();
        if (apiOptions.Enabled)
        {
            services.AddHttpClient<ApiPostMonitorPlugin>();
            services.AddSingleton<IMonitorPlugin>(sp => sp.GetRequiredService<ApiPostMonitorPlugin>());
        }

        services.AddSingleton<IMonitorPluginProvider, MonitorPluginProvider>();

        return services;
    }
}
