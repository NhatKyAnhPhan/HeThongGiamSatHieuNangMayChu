using System;
using System.Collections.Generic;
using Nhom3_NoSQL.Backend.Models;
using System.Threading.Tasks;
namespace Nhom3_NoSQL.Backend.Interfaces
{
    //Danh mục máy chủ (MongoDB - servers)
    public interface IServerRepository
    {
        Task<List<ServerInfo>> GetAllAsync();
        Task<ServerInfo> GetByIdAsync(string id);
        Task InsertAsync(ServerInfo server);
        Task UpdateAsync(ServerInfo server);
        Task DeleteAsync(string id);
    }

    //Lưu trữ lâu dài metric (MongoDB - time series)
    public interface IMetricRepository
    {
        Task InsertAsync(MetricSnapshot snapshot);
        Task<List<MetricSnapshot>> GetHistoryAsync(string serverId, DateTime from, DateTime to);
        Task<MetricSnapshot> GetLatestAsync(string serverId);
    }

    //Lưu trữ AlertRule + AlertEvent (MongoDB)
    public interface IAlertRepository
    {
        Task<List<AlertRule>> GetActiveRulesAsync(string serverId);
        Task<List<AlertRule>> GetAllRulesAsync();
        Task<AlertRule> GetRuleAsync(string id);
        Task InsertRuleAsync(AlertRule rule);
        Task UpdateRuleAsync(AlertRule rule);
        Task InsertAlertEventAsync(AlertEvent alertEvent);
        Task<List<AlertEvent>> GetRecentAlertsAsync(int take = 50);
        Task<AlertEvent> GetAlertAsync(string id);
        Task UpdateAlertAsync(AlertEvent alertEvent);
        Task<bool> HasUnresolvedAlertAsync(string serverId, MetricType metric);
        Task<long> CountAlertsAsync();
    }

    //Nhật ký sự kiện (MongoDB - event_logs)
    public interface IEventRepository
    {
        Task InsertAsync(EventLog log);
        Task<List<EventLog>> SearchAsync(string keyword, string type, int take = 200);
    }

    //Tài khoản đăng nhập (MongoDB - users)
    public interface IUserRepository
    {
        Task<AppUser> GetByEmailAsync(string email);
        Task InsertAsync(AppUser user);
        Task<long> CountAsync();
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
