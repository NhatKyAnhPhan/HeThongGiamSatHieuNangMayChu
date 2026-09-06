using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Nhom3_NoSQL.Backend.Interfaces;
using Nhom3_NoSQL.Backend.Models;

namespace Nhom3_NoSQL.Backend.Services
{
    //Chạy nền, định kỳ đo hiệu năng và đẩy vào Redis + Mongo
    public class MetricCollectorService : IDisposable
    {
        private readonly ICacheService _cache;
        private readonly IMetricRepository _metricRepo;
        private readonly AlertEngineService _alertEngine;
        private readonly PerformanceCounter _cpuCounter;
        private System.Threading.Timer _timer;

        public MetricCollectorService(ICacheService cache, IMetricRepository metricRepo, AlertEngineService alertEngine)
        {
            _cache = cache;
            _metricRepo = metricRepo;
            _alertEngine = alertEngine;
            _cpuCounter = new PerformanceCounter("Processor", "% Processor Time", "_Total");
        }

        public void Start(string serverId, TimeSpan interval)
        {
            _timer = new System.Threading.Timer(async _ => await CollectOnceAsync(serverId), null, TimeSpan.Zero, interval);
        }

        private async Task CollectOnceAsync(string serverId)
        {
            var snapshot = new MetricSnapshot
            {
                ServerId = serverId,
                CpuUsagePercent = _cpuCounter.NextValue(),
                RamUsagePercent = GetRamUsagePercent(),
                DiskUsagePercent = GetDiskUsagePercent(),
                // NetworkInKbps / NetworkOutKbps: đọc qua PerformanceCounter("Network Interface", ...)
            };

            // 1) Cache real-time
            await _cache.SetLatestMetricAsync(serverId, snapshot);
            await _cache.PublishAsync($"metrics:{serverId}", JsonSerializer.Serialize(snapshot));

            // 2) Lưu lâu dài (có thể throttle: chỉ lưu Mongo mỗi phút thay vì mỗi lần)
            await _metricRepo.InsertAsync(snapshot);

            // 3) Kiểm tra ngưỡng cảnh báo
            await _alertEngine.EvaluateAsync(snapshot);
        }

        private static double GetRamUsagePercent()
        {
            // TODO: dùng System.Management (WMI) - Win32_OperatingSystem
            // FreePhysicalMemory / TotalVisibleMemorySize
            return 0;
        }

        private static double GetDiskUsagePercent()
        {
            var drive = new DriveInfo("C");
            var used = drive.TotalSize - drive.AvailableFreeSpace;
            return (double)used / drive.TotalSize * 100;
        }

        public void Dispose() => _timer?.Dispose();
    }
}