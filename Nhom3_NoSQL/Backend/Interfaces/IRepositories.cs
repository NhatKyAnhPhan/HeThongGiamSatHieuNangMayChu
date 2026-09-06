using System;
using System.Collections.Generic;
using Nhom3_NoSQL.Backend.Models;
using System.Threading.Tasks;
namespace Nhom3_NoSQL.Backend.Interfaces
{
    //Lưu trữ lâu dài metric (MongoDB - time series)
    public interface IMetricRepository
    {
        Task InsertAsync(MetricSnapshot snapshot);
        Task<List<MetricSnapshot>> GetHistoryAsync(string serverId, DateTime from, DateTime to);
    }

    //Lưu trữ AlertRule + AlertEvent (MongoDB)
    public interface IAlertRepository
    {
        Task<List<AlertRule>> GetActiveRulesAsync(string serverId);
        Task InsertAlertEventAsync(AlertEvent alertEvent);
        Task<List<AlertEvent>> GetRecentAlertsAsync(int take = 50);
    }

    //Cache dữ liệu real-time + Pub/Sub (Redis)
    public interface ICacheService
    {
        Task SetLatestMetricAsync(string serverId, MetricSnapshot snapshot);
        Task<MetricSnapshot> GetLatestMetricAsync(string serverId);
        Task PublishAsync(string channel, string payloadJson);
        Task SubscribeAsync(string channel, Action<string> onMessage);
    }

    //Index & tìm kiếm log/alert (Elasticsearch)
    public interface ISearchService
    {
        Task IndexAlertAsync(AlertEvent alertEvent);
        Task<List<AlertEvent>> SearchAlertsAsync(string keyword, DateTime? from = null, DateTime? to = null);
    }

    //Gửi thông báo ra ngoài (email, popup Windows,...)
    public interface INotificationService
    {
        Task NotifyAsync(AlertEvent alertEvent);
    }
}
