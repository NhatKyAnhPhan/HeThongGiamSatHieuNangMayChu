using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MongoDB.Driver;
using Nhom3_NoSQL.Backend.Models;
using Nhom3_NoSQL.Backend.Services;

namespace Nhom3_NoSQL.Database
{
    //Nạp dữ liệu mẫu khi CSDL trống + mô phỏng Agent gửi metric cho các host demo (mỗi 5 giây)
    public class DemoDataSeeder
    {
        // Tài khoản demo (chỉ dùng cho môi trường đồ án)
        public const string AdminEmail = "admin@serverpulse.vn";
        public const string AdminPassword = "Admin@123";
        public const string ViewerEmail = "viewer@serverpulse.vn";
        public const string ViewerPassword = "Viewer@123";

        // Host không có trong bảng này sẽ không được mô phỏng (cần Agent thật gửi về)
        class Profile { public double Cpu, Ram, Disk, NetMbps; public bool Offline; }
        static readonly Dictionary<string, Profile> Profiles = new Dictionary<string, Profile>
        {
            ["web-prod-01"] = new Profile { Cpu = 34, Ram = 61, Disk = 48, NetMbps = 412 },
            ["web-prod-02"] = new Profile { Cpu = 41, Ram = 58, Disk = 52, NetMbps = 387 },
            ["api-gateway-03"] = new Profile { Cpu = 72, Ram = 87, Disk = 46, NetMbps = 681 },
            ["db-prod-01"] = new Profile { Cpu = 55, Ram = 74, Disk = 63, NetMbps = 224 },
            ["db-prod-02"] = new Profile { Cpu = 94, Ram = 82, Disk = 68, NetMbps = 198 },
            ["worker-hcm-07"] = new Profile { Cpu = 40, Ram = 50, Disk = 40, NetMbps = 80, Offline = true },
            ["storage-s3-01"] = new Profile { Cpu = 38, Ram = 69, Disk = 89, NetMbps = 1200 },
            ["proxy-edge-02"] = new Profile { Cpu = 22, Ram = 31, Disk = 28, NetMbps = 2400 },
            ["backup-hn-03"] = new Profile { Cpu = 18, Ram = 46, Disk = 72, NetMbps = 94 },
        };

        private readonly MongoDbContext _ctx;
        private readonly AlertEngineService _engine;
        private readonly Random _rnd = new Random();
        private Timer _simTimer, _heartbeatTimer;

        public DemoDataSeeder(MongoDbContext ctx, AlertEngineService engine)
        {
            _ctx = ctx;
            _engine = engine;
        }

        public async Task SeedAsync()
        {
            if (await _ctx.Users.CountDocumentsAsync(FilterDefinition<AppUser>.Empty) == 0)
            {
                await _ctx.Users.InsertManyAsync(new[]
                {
                    AuthService.CreateUser(AdminEmail, "Admin Ops", UserRole.Admin, AdminPassword),
                    AuthService.CreateUser(ViewerEmail, "Viewer Ops", UserRole.Viewer, ViewerPassword),
                });
            }

            if (await _ctx.Servers.CountDocumentsAsync(FilterDefinition<ServerInfo>.Empty) > 0)
            {
                await BackfillAsync();
                return;
            }

            var hosts = new List<ServerInfo>
            {
                Host("web-prod-01", "10.20.1.11", "Ubuntu 24.04 LTS", "Web frontend chính", "SGN-AZ1", 8, 16, 480),
                Host("web-prod-02", "10.20.1.12", "Ubuntu 24.04 LTS", "Web frontend dự phòng", "SGN-AZ1", 8, 16, 480),
                Host("api-gateway-03", "10.20.2.33", "Debian 12", "API Gateway", "SGN-AZ2", 4, 8, 240),
                Host("db-prod-01", "10.20.4.17", "RHEL 9.4", "PostgreSQL primary", "SGN-AZ1", 16, 64, 2000),
                Host("db-prod-02", "10.20.4.18", "RHEL 9.4", "PostgreSQL replica", "SGN-AZ2", 16, 64, 2000),
                Host("worker-hcm-07", "10.22.8.71", "Rocky Linux 9", "Background worker", "HCM-DC2", 8, 32, 500),
                Host("storage-s3-01", "10.20.7.12", "TrueNAS SCALE", "Object storage", "SGN-AZ1", 8, 32, 12000),
                Host("proxy-edge-02", "10.30.1.22", "Alpine 3.20", "Reverse proxy biên", "SGN-EDGE", 4, 4, 60),
                Host("backup-hn-03", "10.40.6.13", "Ubuntu 22.04 LTS", "Backup server", "HN-DC1", 4, 16, 8000),
                Host(Environment.MachineName.ToLowerInvariant(), "127.0.0.1", "Windows (máy chạy app)", "Máy hiện tại - Agent thật (PerformanceCounter)", "LOCAL",
                    Environment.ProcessorCount, 0, 0),
            };
            await _ctx.Servers.InsertManyAsync(hosts);
            var byName = hosts.ToDictionary(h => h.Name);

            // Lịch sử: 6 giờ gần nhất mỗi 1 phút, 7 ngày trước đó mỗi 15 phút
            var now = DateTime.UtcNow;
            var metrics = new List<MetricSnapshot>();
            foreach (var h in hosts.Where(h => Profiles.ContainsKey(h.Name)))
            {
                var p = Profiles[h.Name];
                var end = p.Offline ? now.AddMinutes(-4) : now;
                for (var t = now.AddDays(-7); t < now.AddHours(-6); t = t.AddMinutes(15)) metrics.Add(Sample(h.Id, p, t));
                for (var t = now.AddHours(-6); t <= end; t = t.AddMinutes(1)) metrics.Add(Sample(h.Id, p, t));
            }
            await _ctx.Metrics.InsertManyAsync(metrics);

            // Ngưỡng: 4 quy tắc toàn hệ thống + quy tắc riêng host
            await _ctx.AlertRules.InsertManyAsync(new[]
            {
                Rule(null, MetricType.Cpu, 80, 90, 300),
                Rule(null, MetricType.Ram, 82, 92, 300),
                Rule(null, MetricType.Disk, 85, 95, 600),
                Rule(null, MetricType.Heartbeat, 2, 4, 15, "lần"),
                Rule(byName["db-prod-02"], MetricType.Cpu, 75, 88, 180),
                Rule(byName["storage-s3-01"], MetricType.Disk, 80, 90, 900),
                Rule(byName["proxy-edge-02"], MetricType.NetworkIn, 2000000, 2500000, 120, "Kbps"),
                Rule(byName["backup-hn-03"], MetricType.Disk, 80, 90, 300, "%", false),
            });

            // Sự cố mẫu (giống màn hình thiết kế)
            var alerts = new[]
            {
                Alert(2001, byName["web-prod-02"], MetricType.Cpu, AlertSeverity.Warning, 83.6, 80, now.AddMinutes(-70), AlertStatus.Resolved, "CPU vượt 80% liên tục"),
                Alert(2002, byName["proxy-edge-02"], MetricType.NetworkIn, AlertSeverity.Warning, 2168000, 2000000, now.AddMinutes(-36), AlertStatus.Resolved, "Network in vượt ngưỡng 2 Gbps"),
                Alert(2003, byName["storage-s3-01"], MetricType.Disk, AlertSeverity.Warning, 89, 80, now.AddMinutes(-24), AlertStatus.Resolved, "Disk /data gần đầy"),
                Alert(2004, byName["worker-hcm-07"], MetricType.Heartbeat, AlertSeverity.Critical, 4, 30, now.AddMinutes(-4), AlertStatus.Opening, "Không nhận được heartbeat"),
                Alert(2005, byName["api-gateway-03"], MetricType.Ram, AlertSeverity.Warning, 87.1, 82, now.AddMinutes(-11), AlertStatus.Acknowledged, "RAM vượt ngưỡng warning"),
                Alert(2006, byName["db-prod-02"], MetricType.Cpu, AlertSeverity.Critical, 94.2, 88, now.AddMinutes(-8), AlertStatus.Opening, "CPU vượt 88% liên tục trên db-prod-02"),
            };
            await _ctx.AlertEvents.InsertManyAsync(alerts);

            await _ctx.EventLogs.InsertManyAsync(alerts.Select(a => new EventLog
            {
                ServerId = a.ServerId, ServerName = a.ServerName, Type = "ALERT",
                Level = a.Severity == AlertSeverity.Critical ? "CRITICAL" : "WARNING",
                Message = $"[{a.Code}] {a.Message}", Actor = "system", CreatedAt = a.TriggeredAt
            }).Concat(hosts.Select(h => new EventLog
            {
                ServerId = h.Id, ServerName = h.Name, Type = "HOST", Level = "INFO",
                Message = $"Thêm máy chủ {h.Name} ({h.IpAddress}) vào hệ thống", Actor = AdminEmail, CreatedAt = now.AddDays(-7)
            })));
        }

        //Agent mô phỏng chỉ chạy khi app chạy: lần mở sau, bù dữ liệu khoảng thời gian app tắt (tối đa 7 ngày)
        //để biểu đồ lịch sử của host demo không bị đứt quãng. Không áp dụng cho host thật / host đang mô phỏng offline.
        async Task BackfillAsync()
        {
            var now = DateTime.UtcNow;
            var hosts = await _ctx.Servers.Find(s => s.IsActive).ToListAsync();
            foreach (var h in hosts)
            {
                if (!Profiles.TryGetValue(h.Name, out var p) || p.Offline) continue;
                var since = now.AddDays(-7);
                var times = await _ctx.Metrics.Find(m => m.ServerId == h.Id && m.Timestamp >= since)
                    .SortBy(m => m.Timestamp).Project(m => m.Timestamp).ToListAsync();
                // lấp mọi khoảng trống: 6 giờ gần nhất mỗi 1 phút, trước đó mỗi 15 phút
                double Step(DateTime t) => t < now.AddHours(-6) ? 15 : 1;
                var batch = new List<MetricSnapshot>();
                var cursor = since;
                foreach (var existing in times.Concat(new[] { now }))
                {
                    while ((existing - cursor).TotalMinutes > 1.5 * Step(cursor))
                    {
                        cursor = cursor.AddMinutes(Step(cursor));
                        batch.Add(Sample(h.Id, p, cursor));
                    }
                    if (existing > cursor) cursor = existing;
                }
                if (batch.Count > 0) await _ctx.Metrics.InsertManyAsync(batch);
            }
        }

        //Bắt đầu mô phỏng Agent cho các host demo + quét heartbeat định kỳ
        public void StartSimulation()
        {
            _simTimer = new Timer(async _ =>
            {
                try
                {
                    var hosts = await _ctx.Servers.Find(s => s.IsActive).ToListAsync();
                    foreach (var h in hosts)
                    {
                        if (!Profiles.TryGetValue(h.Name, out var p) || p.Offline) continue;
                        var s = Sample(h.Id, p, DateTime.UtcNow);
                        await _ctx.Metrics.InsertOneAsync(s);
                        await _engine.EvaluateAsync(s, h.Name);
                    }
                }
                catch (Exception ex) { System.Diagnostics.Debug.WriteLine("[Simulator] " + ex.Message); }
            }, null, TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(5));

            _heartbeatTimer = new Timer(async _ =>
            {
                try { await _engine.CheckHeartbeatsAsync(await _ctx.Servers.Find(s => s.IsActive).ToListAsync()); }
                catch (Exception ex) { System.Diagnostics.Debug.WriteLine("[Heartbeat] " + ex.Message); }
            }, null, TimeSpan.FromSeconds(15), TimeSpan.FromSeconds(10));
        }

        static ServerInfo Host(string name, string ip, string os, string desc, string region, int cores, double ramGb, double diskGb) =>
            new ServerInfo
            {
                Name = name, IpAddress = ip, OperatingSystem = os, Description = desc, Region = region,
                CpuCores = cores, RamTotalGb = ramGb, DiskTotalGb = diskGb, CreatedAt = DateTime.UtcNow.AddDays(-30)
            };

        static AlertRule Rule(ServerInfo host, MetricType m, double warn, double crit, int duration, string unit = "%", bool enabled = true) =>
            new AlertRule
            {
                ServerId = host?.Id, ServerName = host?.Name, Metric = m, ThresholdValue = warn, CriticalValue = crit,
                DurationSeconds = duration, Unit = unit, IsEnabled = enabled
            };

        static AlertEvent Alert(int code, ServerInfo h, MetricType m, AlertSeverity sev, double value, double threshold, DateTime at, string status, string msg) =>
            new AlertEvent
            {
                Code = "INC-" + code, ServerId = h.Id, ServerName = h.Name, Metric = m, Severity = sev,
                ActualValue = value, ThresholdValue = threshold, DurationSeconds = 300, TriggeredAt = at, Status = status, Message = msg,
                AcknowledgedAt = status != AlertStatus.Opening ? at.AddMinutes(2) : (DateTime?)null,
                AcknowledgedBy = status != AlertStatus.Opening ? AdminEmail : null,
                ResolvedAt = status == AlertStatus.Resolved ? at.AddMinutes(15) : (DateTime?)null,
                ResolvedBy = status == AlertStatus.Resolved ? AdminEmail : null,
                IsResolved = status == AlertStatus.Resolved
            };

        //Giá trị dao động quanh mức nền: sóng chậm (theo giờ) + nhiễu nhỏ
        MetricSnapshot Sample(string serverId, Profile p, DateTime t)
        {
            double phase = t.Ticks / (double)TimeSpan.TicksPerHour;
            double Wave(double baseVal, double amp, double shift)
            {
                double noise;
                lock (_rnd) noise = _rnd.NextDouble() - 0.5;
                return Math.Max(0, Math.Min(100, baseVal + amp * Math.Sin(phase * 1.3 + shift) + noise * 3));
            }
            double net = p.NetMbps * (1 + 0.15 * Math.Sin(phase + 2));
            return new MetricSnapshot
            {
                ServerId = serverId,
                Timestamp = t,
                CpuUsagePercent = Math.Round(Wave(p.Cpu, p.Cpu > 85 ? 2 : 8, 0), 1),
                RamUsagePercent = Math.Round(Wave(p.Ram, 3, 1), 1),
                DiskUsagePercent = Math.Round(Math.Max(0, Math.Min(100, p.Disk + 0.5 * Math.Sin(phase / 24))), 1),
                NetworkInKbps = Math.Round(net * 1000 * 0.6),
                NetworkOutKbps = Math.Round(net * 1000 * 0.4),
            };
        }
    }
}
