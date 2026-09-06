using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Nhom3_NoSQL.Backend.Interfaces;
using Nhom3_NoSQL.Backend.Models;

namespace Nhom3_NoSQL.Backend.Services
{
    //So khớp metric mới với AlertRule, sinh AlertEvent khi vi phạm
    public class AlertEngineService
    {
        private readonly IAlertRepository _alertRepo;
        private readonly ISearchService _searchService;
        private readonly INotificationService _notificationService;
        private readonly ICacheService _cache;

        public AlertEngineService(
            IAlertRepository alertRepo,
            ISearchService searchService,
            INotificationService notificationService,
            ICacheService cache)
        {
            _alertRepo = alertRepo;
            _searchService = searchService;
            _notificationService = notificationService;
            _cache = cache;
        }

        public async Task EvaluateAsync(MetricSnapshot snapshot)
        {
            var rules = await _alertRepo.GetActiveRulesAsync(snapshot.ServerId);

            foreach (var rule in rules)
            {
                double actual = 0;
                switch (rule.Metric)
                {
                    case MetricType.Cpu:
                        actual = snapshot.CpuUsagePercent;
                        break;
                    case MetricType.Ram:
                        actual = snapshot.RamUsagePercent;
                        break;
                    case MetricType.Disk:
                        actual = snapshot.DiskUsagePercent;
                        break;
                    case MetricType.NetworkIn:
                        actual = snapshot.NetworkInKbps;
                        break;
                    case MetricType.NetworkOut:
                        actual = snapshot.NetworkOutKbps;
                        break;
                    default:
                        actual = 0;
                        break;
                }

                bool violated = rule.Operator == ComparisonOperator.GreaterThan
                    ? actual > rule.ThresholdValue
                    : actual < rule.ThresholdValue;

                if (!violated) continue;

                var alertEvent = new AlertEvent
                {
                    ServerId = snapshot.ServerId,
                    Metric = rule.Metric,
                    ActualValue = actual,
                    ThresholdValue = rule.ThresholdValue,
                    Severity = actual > rule.ThresholdValue * 1.2 ? AlertSeverity.Critical : AlertSeverity.Warning,
                    Message = $"{rule.Metric} = {actual:F1} vượt ngưỡng {rule.ThresholdValue:F1}"
                };

                await _alertRepo.InsertAlertEventAsync(alertEvent);
                await _searchService.IndexAlertAsync(alertEvent);
                await _notificationService.NotifyAsync(alertEvent);
                await _cache.PublishAsync("alerts", System.Text.Json.JsonSerializer.Serialize(alertEvent));
            }
        }
    }
}