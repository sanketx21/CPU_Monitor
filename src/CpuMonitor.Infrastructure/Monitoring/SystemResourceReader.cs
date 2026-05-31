using System.Diagnostics;
using System.Runtime.InteropServices;
using CpuMonitor.Application.Options;
using CpuMonitor.Domain.Models;
using CpuMonitor.Domain.Services;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace CpuMonitor.Infrastructure.Monitoring;

public sealed class SystemResourceReader : ISystemResourceReader
{
    private const double BytesPerMegabyte = 1024d * 1024d;

    private readonly MonitorOptions _options;
    private readonly ILogger<SystemResourceReader> _logger;
    private CpuSample? _previousCpuSample;

    public SystemResourceReader(IOptions<MonitorOptions> options, ILogger<SystemResourceReader> logger)
    {
        _options = options.Value;
        _logger = logger;
    }

    public Task<ResourceSnapshot> ReadAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var cpuPercent = ReadCpuPercent();
        var memory = ReadMemoryUsage();
        var disk = ReadDiskUsage();

        return Task.FromResult(new ResourceSnapshot(
            DateTimeOffset.Now,
            cpuPercent,
            memory.UsedMegabytes,
            memory.TotalMegabytes,
            disk));
    }

    private double ReadCpuPercent()
    {
        var currentSample = new CpuSample(DateTimeOffset.UtcNow, ReadTotalProcessCpuTime());
        var previousSample = _previousCpuSample;
        _previousCpuSample = currentSample;

        if (previousSample is null)
        {
            return 0;
        }

        var cpuDelta = currentSample.TotalCpuTime - previousSample.TotalCpuTime;
        var elapsed = currentSample.Timestamp - previousSample.Timestamp;
        if (elapsed <= TimeSpan.Zero || cpuDelta < TimeSpan.Zero)
        {
            return 0;
        }

        var usage = cpuDelta.TotalMilliseconds / (elapsed.TotalMilliseconds * Environment.ProcessorCount) * 100;
        return Math.Clamp(usage, 0, 100);
    }

    private TimeSpan ReadTotalProcessCpuTime()
    {
        var total = TimeSpan.Zero;

        foreach (var process in Process.GetProcesses())
        {
            try
            {
                total += process.TotalProcessorTime;
            }
            catch (Exception ex) when (ex is InvalidOperationException or System.ComponentModel.Win32Exception or NotSupportedException)
            {
                _logger.LogDebug(ex, "Skipped process while reading CPU time.");
            }
            finally
            {
                process.Dispose();
            }
        }

        return total;
    }

    private static MemoryUsage ReadMemoryUsage()
    {
        if (OperatingSystem.IsWindows())
        {
            return ReadWindowsMemoryUsage();
        }

        if (OperatingSystem.IsLinux())
        {
            return ReadLinuxMemoryUsage();
        }

        var gcInfo = GC.GetGCMemoryInfo();
        var totalMb = gcInfo.TotalAvailableMemoryBytes / BytesPerMegabyte;
        var usedMb = Math.Max(0, totalMb - gcInfo.MemoryLoadBytes / BytesPerMegabyte);
        return new MemoryUsage(usedMb, totalMb);
    }

    private static MemoryUsage ReadWindowsMemoryUsage()
    {
        var status = new MemoryStatusEx();
        if (!GlobalMemoryStatusEx(status))
        {
            throw new InvalidOperationException("Unable to read Windows memory status.");
        }

        var total = status.TotalPhys / BytesPerMegabyte;
        var available = status.AvailPhys / BytesPerMegabyte;
        return new MemoryUsage(total - available, total);
    }

    private static MemoryUsage ReadLinuxMemoryUsage()
    {
        var values = File.ReadLines("/proc/meminfo")
            .Select(line => line.Split(':', 2, StringSplitOptions.TrimEntries))
            .Where(parts => parts.Length == 2)
            .ToDictionary(parts => parts[0], parts => ParseKilobytes(parts[1]), StringComparer.OrdinalIgnoreCase);

        var total = values.GetValueOrDefault("MemTotal") / 1024d;
        var available = values.GetValueOrDefault("MemAvailable") / 1024d;
        return new MemoryUsage(total - available, total);
    }

    private DiskUsage ReadDiskUsage()
    {
        var rootPath = ResolveDiskRootPath();
        var drive = new DriveInfo(rootPath);
        var used = (drive.TotalSize - drive.AvailableFreeSpace) / BytesPerMegabyte;
        var total = drive.TotalSize / BytesPerMegabyte;

        return new DiskUsage(drive.Name, used, total);
    }

    private string ResolveDiskRootPath()
    {
        if (!string.IsNullOrWhiteSpace(_options.DiskRootPath))
        {
            return _options.DiskRootPath;
        }

        return Path.GetPathRoot(AppContext.BaseDirectory)
            ?? DriveInfo.GetDrives().First(drive => drive.IsReady).Name;
    }

    private static double ParseKilobytes(string value)
    {
        var number = value.Split(' ', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault();
        return double.TryParse(number, out var result) ? result : 0;
    }

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool GlobalMemoryStatusEx([In, Out] MemoryStatusEx lpBuffer);

    private sealed record CpuSample(DateTimeOffset Timestamp, TimeSpan TotalCpuTime);

    private sealed record MemoryUsage(double UsedMegabytes, double TotalMegabytes);

    [StructLayout(LayoutKind.Sequential)]
    private sealed class MemoryStatusEx
    {
        public uint Length;
        public uint MemoryLoad;
        public ulong TotalPhys;
        public ulong AvailPhys;
        public ulong TotalPageFile;
        public ulong AvailPageFile;
        public ulong TotalVirtual;
        public ulong AvailVirtual;
        public ulong AvailExtendedVirtual;

        public MemoryStatusEx()
        {
            Length = (uint)Marshal.SizeOf<MemoryStatusEx>();
        }
    }
}
