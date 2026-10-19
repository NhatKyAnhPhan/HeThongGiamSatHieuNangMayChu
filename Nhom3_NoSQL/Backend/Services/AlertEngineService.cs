using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Nhom3_NoSQL.Backend.Interfaces;
using Nhom3_NoSQL.Backend.Models;

namespace Nhom3_NoSQL.Backend.Services
{
    //So khớp metric mới với AlertRule (cửa sổ trượt DurationSeconds), sinh AlertEvent khi vi phạm
    public class AlertEngineService
    {
        public static readonly TimeSpan HeartbeatTimeout = TimeSpan.FromSeconds(30);

        private readonly IAlertRepository _alertRepo;
        private readonly IMetricRepository _metricRepo;
        private readonly IEventRepository _eventRepo;
        private readonly ISearchService _searchService;   // có thể null nếu không chạy Elasticsearch
        private readonly INotificationService _notificationService;
        private readonly ICacheService _cache;            // có thể null nếu không chạy Redis

        public AlertEngineService(
            IAlertRepository alertRepo,
            IMetricRepository metricRepo,
            IEventRepository eventRepo,
            ISearchService searchService,
            INotificationService notificationService,
            ICacheService cache)
        {
            _alertRepo = alertRepo;
            _metricRepo = metricRepo;
            _eventRepo = eventRepo;
            _searchService = searchService;
            _notificationService = notificationService;
            _cache = cache;
        }

        public static double ValueOf(MetricSnapshot s, MetricType metric)
        {
            switch (metric)
            {
                case MetricType.Cpu: return s.CpuUsagePercent;
                case MetricType.Ram: return s.RamUsagePercent;
                case MetricType.Disk: return s.DiskUsagePercent;
                case MetricType.NetworkIn: return s.NetworkInKbps;
                case MetricType.NetworkOut: return s.NetworkOutKbps;
                default: return 0;
            }
        }

        static bool Violates(AlertRule rule, double value, double threshold) =>
            rule.Operator == ComparisonOperator.GreaterThan ? value >= threshold : value <= threshold;

        public async Task EvaluateAsync(MetricSnapshot snapshot, string serverName = null)
        {
            var rules = await _alertRepo.GetActiveRulesAsync(snapshot.ServerId);

            foreach (var rule in rules.Where(r => r.Metric != MetricType.Heartbeat))
            {
                double actual = ValueOf(snapshot, rule.Metric);
                if (!Violates(rule, actual, rule.ThresholdValue)) continue;

                // Cửa sổ trượt: mọi mẫu trong DurationSeconds gần nhất đều phải vượt ngưỡng
                var window = new List<MetricSnapshot> { snapshot };
                if (rule.DurationSeconds > 0)
                {
                    var from = snapshot.Timestamp.AddSeconds(-rule.DurationSeconds);
                    window = await _metricRepo.GetHistoryAsync(snapshot.ServerId, from, snapshot.Timestamp);
                    if (window.Count == 0) continue;
                    var covered = (snapshot.Timestamp - window.First().Timestamp).TotalSeconds;
                    if (covered < rule.DurationSeconds * 0.8) continue; // chưa đủ dữ liệu cho cả cửa sổ
                    if (!window.All(m => Violates(rule, ValueOf(m, rule.Metric), rule.ThresholdValue))) continue;
                }

                if (await _alertRepo.HasUnresolvedAlertAsync(snapshot.ServerId, rule.Metric)) continue;

                // Mức Critical khi giá trị "tốt nhất" trong cửa sổ vẫn vượt ngưỡng critical
                double worstCaseFloor = rule.Operator == ComparisonOperator.GreaterThan
                    ? window.Min(m => ValueOf(m, rule.Metric))
                    : window.Max(m => ValueOf(m, rule.Metric));
                bool critical = rule.CriticalValue > 0 && Violates(rule, worstCaseFloor, rule.CriticalValue);

                var alertEvent = new AlertEvent
                {
                    ServerId = snapshot.ServerId,
                    ServerName = serverName,
                    Metric = rule.Metric,
                    ActualValue = Math.Round(actual, 1),
                    ThresholdValue = critical ? rule.CriticalValue : rule.ThresholdValue,
                    DurationSeconds = rule.DurationSeconds,
                    Severity = critical ? AlertSeverity.Critical : AlertSeverity.Warning,
                    Message = $"{MetricLabel(rule.Metric)} vượt {(critical ? rule.CriticalValue : rule.ThresholdValue):0.#}{rule.Unit} liên tục {rule.DurationSeconds}s (hiện tại {actual:0.#}{rule.Unit})"
                };
                await RaiseAsync(alertEvent);
            }
        }

        //Quét last_seen của các host, quá HeartbeatTimeout -> phát cảnh báo mất kết nối
        public async Task CheckHeartbeatsAsync(IEnumerable<ServerInfo> servers)
        {
            foreach (var server in servers.Where(s => s.IsActive))
            {
                var latest = await _metricRepo.GetLatestAsync(server.Id);
                if (latest != null && DateTime.UtcNow - latest.Timestamp < HeartbeatTimeout) continue;
                if (await _alertRepo.HasUnresolvedAlertAsync(server.Id, MetricType.Heartbeat)) continue;

                await RaiseAsync(new AlertEvent
                {
                    ServerId = server.Id,
                    ServerName = server.Name,
                    Metric = MetricType.Heartbeat,
                    Severity = AlertSeverity.Critical,
                    ActualValue = latest == null ? 0 : Math.Round((DateTime.UtcNow - latest.Timestamp).TotalSeconds),
                    ThresholdValue = HeartbeatTimeout.TotalSeconds,
                    DurationSeconds = (int)HeartbeatTimeout.TotalSeconds,
                    Message = $"Không nhận được heartbeat từ {server.Name} quá {HeartbeatTimeout.TotalSeconds:0}s"
                });
            }
        }

        async Task RaiseAsync(AlertEvent alertEvent)
        {
            alertEvent.Code = "INC-" + (2000 + await _alertRepo.CountAlertsAsync() + 1);
            await _alertRepo.InsertAlertEventAsync(alertEvent);
            await _eventRepo.InsertAsync(new EventLog
            {
                ServerId = alertEvent.ServerId,
                ServerName = alertEvent.ServerName,
                Type = "ALERT",
                Level = alertEvent.Severity == AlertSeverity.Critical ? "CRITICAL" : "WARNING",
                Message = $"[{alertEvent.Code}] {alertEvent.Message}",
                Actor = "system"
            });

            // Các kênh phụ (Elasticsearch, Redis) lỗi không được làm hỏng luồng cảnh báo chính
            try { if (_searchService != null) await _searchService.IndexAlertAsync(alertEvent); } catch { }
            try { if (_cache != null) await _cache.PublishAsync("alerts", System.Text.Json.JsonSerializer.Serialize(alertEvent)); } catch { }
            await _notificationService.NotifyAsync(alertEvent);
        }

        public static string MetricLabel(MetricType m)
        {
            switch (m)
            {
                case MetricType.Cpu: return "CPU";
                case MetricType.Ram: return "RAM";
                case MetricType.Disk: return "Disk";
                case MetricType.NetworkIn: return "Network in";
                case MetricType.NetworkOut: return "Network out";
                default: return "Heartbeat";
            }
        }
    }
}
