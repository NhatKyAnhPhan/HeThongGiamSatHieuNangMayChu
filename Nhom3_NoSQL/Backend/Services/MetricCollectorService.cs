using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.NetworkInformation;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Nhom3_NoSQL.Backend.Interfaces;
using Nhom3_NoSQL.Backend.Models;

namespace Nhom3_NoSQL.Backend.Services
{
    //Chạy nền, định kỳ đo hiệu năng máy hiện tại và đẩy vào Redis + Mongo
    public class MetricCollectorService : IDisposable
    {
        private readonly ICacheService _cache; // có thể null nếu không chạy Redis
        private readonly IMetricRepository _metricRepo;
        private readonly AlertEngineService _alertEngine;
        private readonly PerformanceCounter _cpuCounter;
        private System.Threading.Timer _timer;
        private long _lastRx, _lastTx;
        private DateTime _lastNetAt;

        public MetricCollectorService(ICacheService cache, IMetricRepository metricRepo, AlertEngineService alertEngine)
        {
            _cache = cache;
            _metricRepo = metricRepo;
            _alertEngine = alertEngine;
            _cpuCounter = new PerformanceCounter("Processor", "% Processor Time", "_Total");
        }

        public void Start(string serverId, string serverName, TimeSpan interval)
        {
            _timer = new System.Threading.Timer(async _ =>
            {
                try { await CollectOnceAsync(serverId, serverName); }
                catch (Exception ex) { Debug.WriteLine("[Collector] " + ex.Message); }
            }, null, TimeSpan.Zero, interval);
        }

        private async Task CollectOnceAsync(string serverId, string serverName)
        {
            var (inKbps, outKbps) = GetNetworkKbps();
            var snapshot = new MetricSnapshot
            {
                ServerId = serverId,
                CpuUsagePercent = Math.Round(_cpuCounter.NextValue(), 1),
                RamUsagePercent = Math.Round(GetRamUsagePercent(), 1),
                DiskUsagePercent = Math.Round(GetDiskUsagePercent(), 1),
                NetworkInKbps = Math.Round(inKbps, 1),
                NetworkOutKbps = Math.Round(outKbps, 1),
            };

            // 1) Cache real-time
            if (_cache != null)
            {
                try
                {
                    await _cache.SetLatestMetricAsync(serverId, snapshot);
                    await _cache.PublishAsync($"metrics:{serverId}", JsonSerializer.Serialize(snapshot));
                }
                catch { /* Redis không bắt buộc */ }
            }

            // 2) Lưu lâu dài
            await _metricRepo.InsertAsync(snapshot);

            // 3) Kiểm tra ngưỡng cảnh báo
            await _alertEngine.EvaluateAsync(snapshot, serverName);
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct MEMORYSTATUSEX
        {
            public uint dwLength, dwMemoryLoad;
            public ulong ullTotalPhys, ullAvailPhys, ullTotalPageFile, ullAvailPageFile, ullTotalVirtual, ullAvailVirtual, ullAvailExtendedVirtual;
        }

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool GlobalMemoryStatusEx(ref MEMORYSTATUSEX lpBuffer);

        public static double GetRamUsagePercent()
        {
            var m = new MEMORYSTATUSEX { dwLength = (uint)Marshal.SizeOf(typeof(MEMORYSTATUSEX)) };
            if (!GlobalMemoryStatusEx(ref m) || m.ullTotalPhys == 0) return 0;
            return (double)(m.ullTotalPhys - m.ullAvailPhys) / m.ullTotalPhys * 100;
        }

        private static double GetDiskUsagePercent()
        {
            var drive = new DriveInfo("C");
            var used = drive.TotalSize - drive.AvailableFreeSpace;
            return (double)used / drive.TotalSize * 100;
        }

        //Tổng lưu lượng các card mạng đang hoạt động, quy đổi ra Kbps theo khoảng thời gian giữa 2 lần đo
        private (double inKbps, double outKbps) GetNetworkKbps()
        {
            long rx = 0, tx = 0;
            foreach (var nic in NetworkInterface.GetAllNetworkInterfaces()
                         .Where(n => n.OperationalStatus == OperationalStatus.Up && n.NetworkInterfaceType != NetworkInterfaceType.Loopback))
            {
                var s = nic.GetIPv4Statistics();
                rx += s.BytesReceived;
                tx += s.BytesSent;
            }
            var now = DateTime.UtcNow;
            double seconds = (now - _lastNetAt).TotalSeconds;
            var result = _lastNetAt == default || seconds <= 0
                ? (0d, 0d)
                : ((rx - _lastRx) * 8 / 1000d / seconds, (tx - _lastTx) * 8 / 1000d / seconds);
            _lastRx = rx; _lastTx = tx; _lastNetAt = now;
            return result;
        }

        public void Dispose() => _timer?.Dispose();
    }
}
