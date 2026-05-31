# CPU Monitor

A cross-platform .NET 8 console application that samples CPU, memory, and disk usage on a configurable interval, prints the readings to the console, logs them to a local file through a plugin, and can publish readings to a configurable REST API endpoint.

## Features

- Real-time monitoring for CPU percentage, RAM used/total, and disk used/total.
- Clean Architecture style solution with Domain, Application, Infrastructure, and Console layers.
- Dependency injection and hosted background service orchestration.
- Plugin contract through `IMonitorPlugin`.
- Built-in file logging plugin.
- Built-in REST API plugin that posts JSON payloads.
- Optional external plugin discovery from a `Plugins` folder.
- Windows memory support using `GlobalMemoryStatusEx`; Linux memory support using `/proc/meminfo`; disk support using `DriveInfo`.

## Requirements

- .NET SDK 8.0 or later.

## Build

```powershell
dotnet restore CpuMonitor.sln --configfile NuGet.Config
dotnet build CpuMonitor.sln -m:1
```

The `-m:1` flag is not required on a normal machine, but it keeps builds reliable in low-space sandboxed environments.

## Run

```powershell
dotnet run --project src/CpuMonitor.Console
```

Stop the application with `Ctrl+C`.

## Configuration

Edit `src/CpuMonitor.Console/appsettings.json`:

```json
{
  "Monitor": {
    "IntervalSeconds": 5,
    "DiskRootPath": null
  },
  "FileLog": {
    "Enabled": true,
    "FilePath": "logs/monitor.log"
  },
  "Plugins": {
    "LoadExternalPlugins": true,
    "Directory": "Plugins"
  },
  "Api": {
    "Enabled": false,
    "EndpointUrl": "https://example.com/api/system-resources",
    "TimeoutSeconds": 10
  }
}
```

Replace `Api:EndpointUrl` with the real endpoint to publish monitoring data using HTTP POST. The API plugin is enabled by default so the assessment requirement is active through JSON configuration. The API payload is:

```json
{
  "cpu": 12.5,
  "ram_used": 8192,
  "disk_used": 102400
}
```

Environment variable overrides are supported with the `CPUMONITOR_` prefix. Example:

```powershell
$env:CPUMONITOR_Api__Enabled="true"
$env:CPUMONITOR_Api__EndpointUrl="https://your-api.example/monitor"
dotnet run --project src/CpuMonitor.Console
```

## Adding a Plugin

Create a class that implements `CpuMonitor.Domain.Plugins.IMonitorPlugin`.

```csharp
using CpuMonitor.Domain.Models;
using CpuMonitor.Domain.Plugins;

public sealed class ConsoleAlertPlugin : IMonitorPlugin
{
    public string Name => "Console Alert";

    public Task OnSnapshotAsync(ResourceSnapshot snapshot, CancellationToken cancellationToken)
    {
        if (snapshot.CpuPercent > 90)
        {
            Console.WriteLine("High CPU detected.");
        }

        return Task.CompletedTask;
    }
}
```

For a built-in plugin, register it in `CpuMonitor.Infrastructure.DependencyInjection`. For an external plugin, compile it into a DLL that references `CpuMonitor.Domain.dll` and place it in the configured `Plugins` directory. The loader creates plugin instances using the DI container, so constructor dependencies can be injected if they are already registered.

## Design Decisions

The application uses Clean Architecture because monitoring, orchestration, and infrastructure details change for different reasons. The Domain project owns the core resource snapshot model and plugin interfaces. The Application project owns the monitoring loop and plugin dispatching. The Infrastructure project owns platform-specific resource reading, HTTP publishing, file logging, and plugin discovery. The Console project is the composition root where configuration, logging, and dependency injection are assembled.

The primary implementation target is Windows, with Linux memory support included and a clear `ISystemResourceReader` abstraction for adding macOS or more precise OS-specific implementations later. CPU usage is calculated from total process CPU time between samples, which keeps the implementation cross-platform without adding external packages. The first CPU reading is `0` because a baseline sample is needed before a percentage can be computed.

## Known Limitations

- CPU usage is an approximation based on visible process CPU time. Some protected system processes may be skipped.
- macOS memory reporting falls back to managed runtime information and should be replaced with a dedicated macOS reader for production.
- The REST API plugin requires a reachable endpoint. For local-only demos, set `Api:Enabled` to `false`.
- External plugin loading is intentionally simple: DLLs are loaded from one folder and should be trusted code.

## Demo Checklist

1. Run `dotnet run --project src/CpuMonitor.Console`.
2. Confirm console output refreshes every configured interval.
3. Confirm `logs/monitor.log` is created and appended.
4. Enable `Api:Enabled`, set a test endpoint, and confirm HTTP POST payloads are received.
5. Explain the layer boundaries using `ARCHITECTURE.md`.
