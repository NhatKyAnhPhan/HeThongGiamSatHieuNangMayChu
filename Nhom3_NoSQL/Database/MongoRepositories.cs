using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using MongoDB.Bson;
using MongoDB.Driver;
using Nhom3_NoSQL.Backend.Interfaces;
using Nhom3_NoSQL.Backend.Models;

namespace Nhom3_NoSQL.Database
{
    public class MongoServerRepository : IServerRepository
    {
        private readonly MongoDbContext _ctx;
        public MongoServerRepository(MongoDbContext ctx) => _ctx = ctx;

        public Task<List<ServerInfo>> GetAllAsync() =>
            _ctx.Servers.Find(FilterDefinition<ServerInfo>.Empty).SortBy(s => s.Name).ToListAsync();

        public Task<ServerInfo> GetByIdAsync(string id) =>
            _ctx.Servers.Find(s => s.Id == id).FirstOrDefaultAsync();

        public Task InsertAsync(ServerInfo server) => _ctx.Servers.InsertOneAsync(server);

        public Task UpdateAsync(ServerInfo server) =>
            _ctx.Servers.ReplaceOneAsync(s => s.Id == server.Id, server);

        public Task DeleteAsync(string id) => _ctx.Servers.DeleteOneAsync(s => s.Id == id);
    }

    public class MongoMetricRepository : IMetricRepository
    {
        private readonly MongoDbContext _ctx;
        public MongoMetricRepository(MongoDbContext ctx) => _ctx = ctx;

        public Task InsertAsync(MetricSnapshot snapshot) =>
            _ctx.Metrics.InsertOneAsync(snapshot);

        public async Task<List<MetricSnapshot>> GetHistoryAsync(string serverId, DateTime from, DateTime to)
        {
            var filter = Builders<MetricSnapshot>.Filter.And(
                Builders<MetricSnapshot>.Filter.Eq(m => m.ServerId, serverId),
                Builders<MetricSnapshot>.Filter.Gte(m => m.Timestamp, from),
                Builders<MetricSnapshot>.Filter.Lte(m => m.Timestamp, to));

            return await _ctx.Metrics.Find(filter).SortBy(m => m.Timestamp).ToListAsync();
        }

        public Task<MetricSnapshot> GetLatestAsync(string serverId) =>
            _ctx.Metrics.Find(m => m.ServerId == serverId).SortByDescending(m => m.Timestamp).FirstOrDefaultAsync();
    }

    public class MongoAlertRepository : IAlertRepository
    {
        private readonly MongoDbContext _ctx;
        public MongoAlertRepository(MongoDbContext ctx) => _ctx = ctx;

        //Lấy quy tắc riêng của host + quy tắc toàn hệ thống; quy tắc riêng host ghi đè quy tắc chung cùng metric
        public async Task<List<AlertRule>> GetActiveRulesAsync(string serverId)
        {
            var filter = Builders<AlertRule>.Filter.And(
                Builders<AlertRule>.Filter.Or(
                    Builders<AlertRule>.Filter.Eq(r => r.ServerId, serverId),
                    Builders<AlertRule>.Filter.Eq(r => r.ServerId, null)),
                Builders<AlertRule>.Filter.Eq(r => r.IsEnabled, true));

            var rules = await _ctx.AlertRules.Find(filter).ToListAsync();
            return rules.GroupBy(r => r.Metric)
                .Select(g => g.FirstOrDefault(r => r.ServerId == serverId) ?? g.First())
                .ToList();
        }

        public Task<List<AlertRule>> GetAllRulesAsync() =>
            _ctx.AlertRules.Find(FilterDefinition<AlertRule>.Empty).ToListAsync();

        public Task<AlertRule> GetRuleAsync(string id) =>
            _ctx.AlertRules.Find(r => r.Id == id).FirstOrDefaultAsync();

        public Task InsertRuleAsync(AlertRule rule) => _ctx.AlertRules.InsertOneAsync(rule);

        public Task UpdateRuleAsync(AlertRule rule) =>
            _ctx.AlertRules.ReplaceOneAsync(r => r.Id == rule.Id, rule);

        public Task InsertAlertEventAsync(AlertEvent alertEvent) =>
            _ctx.AlertEvents.InsertOneAsync(alertEvent);

        public async Task<List<AlertEvent>> GetRecentAlertsAsync(int take = 50) =>
            await _ctx.AlertEvents.Find(FilterDefinition<AlertEvent>.Empty)
                .SortByDescending(a => a.TriggeredAt)
                .Limit(take)
                .ToListAsync();

        public Task<AlertEvent> GetAlertAsync(string id) =>
            _ctx.AlertEvents.Find(a => a.Id == id).FirstOrDefaultAsync();

        public Task UpdateAlertAsync(AlertEvent alertEvent) =>
            _ctx.AlertEvents.ReplaceOneAsync(a => a.Id == alertEvent.Id, alertEvent);

        public Task<bool> HasUnresolvedAlertAsync(string serverId, MetricType metric) =>
            _ctx.AlertEvents.Find(a => a.ServerId == serverId && a.Metric == metric && a.Status != AlertStatus.Resolved).AnyAsync();

        public Task<long> CountAlertsAsync() =>
            _ctx.AlertEvents.CountDocumentsAsync(FilterDefinition<AlertEvent>.Empty);
    }

    public class MongoEventRepository : IEventRepository
    {
        private readonly MongoDbContext _ctx;
        public MongoEventRepository(MongoDbContext ctx) => _ctx = ctx;

        public Task InsertAsync(EventLog log) => _ctx.EventLogs.InsertOneAsync(log);

        public Task<List<EventLog>> SearchAsync(string keyword, string type, int take = 200)
        {
            var f = Builders<EventLog>.Filter;
            var filter = f.Empty;
            if (!string.IsNullOrWhiteSpace(keyword))
            {
                var rx = new BsonRegularExpression(Regex.Escape(keyword), "i");
                filter &= f.Or(f.Regex(e => e.Message, rx), f.Regex(e => e.ServerName, rx));
            }
            if (!string.IsNullOrWhiteSpace(type)) filter &= f.Eq(e => e.Type, type);
            return _ctx.EventLogs.Find(filter).SortByDescending(e => e.CreatedAt).Limit(take).ToListAsync();
        }
    }

    public class MongoUserRepository : IUserRepository
    {
        private readonly MongoDbContext _ctx;
        public MongoUserRepository(MongoDbContext ctx) => _ctx = ctx;

        public Task<AppUser> GetByEmailAsync(string email)
        {
            var normalized = (email ?? "").Trim().ToLowerInvariant();
            return _ctx.Users.Find(u => u.Email == normalized).FirstOrDefaultAsync();
        }

        public Task InsertAsync(AppUser user) => _ctx.Users.InsertOneAsync(user);

        public Task<long> CountAsync() => _ctx.Users.CountDocumentsAsync(FilterDefinition<AppUser>.Empty);
    }
}
