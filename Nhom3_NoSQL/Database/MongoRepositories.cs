using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using MongoDB.Driver;
using Nhom3_NoSQL.Backend.Interfaces;
using Nhom3_NoSQL.Backend.Models;

namespace Nhom3_NoSQL.Database
{
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
    }

    public class MongoAlertRepository : IAlertRepository
    {
        private readonly MongoDbContext _ctx;
        public MongoAlertRepository(MongoDbContext ctx) => _ctx = ctx;

        public async Task<List<AlertRule>> GetActiveRulesAsync(string serverId)
        {
            var filter = Builders<AlertRule>.Filter.And(
                Builders<AlertRule>.Filter.Eq(r => r.ServerId, serverId),
                Builders<AlertRule>.Filter.Eq(r => r.IsEnabled, true));

            return await _ctx.AlertRules.Find(filter).ToListAsync();
        }

        public Task InsertAlertEventAsync(AlertEvent alertEvent) =>
            _ctx.AlertEvents.InsertOneAsync(alertEvent);

        public async Task<List<AlertEvent>> GetRecentAlertsAsync(int take = 50) =>
            await _ctx.AlertEvents.Find(FilterDefinition<AlertEvent>.Empty)
                .SortByDescending(a => a.TriggeredAt)
                .Limit(take)
                .ToListAsync();
    }
}