using System;
using System.Collections.Generic;
using MongoDB.Driver;
using Nhom3_NoSQL.Backend.Models;

namespace Nhom3_NoSQL.Database
{
    //Điểm truy cập tập trung tới các collection MongoDB
    public class MongoDbContext
    {
        private readonly IMongoDatabase _database;

        public MongoDbContext(string connectionString, string databaseName)
        {
            var client = new MongoClient(connectionString);
            _database = client.GetDatabase(databaseName);
        }

        public IMongoCollection<ServerInfo> Servers => _database.GetCollection<ServerInfo>("servers");
        public IMongoCollection<MetricSnapshot> Metrics => _database.GetCollection<MetricSnapshot>("metrics");
        public IMongoCollection<AlertRule> AlertRules => _database.GetCollection<AlertRule>("alert_rules");
        public IMongoCollection<AlertEvent> AlertEvents => _database.GetCollection<AlertEvent>("alert_events");
        public IMongoCollection<EventLog> EventLogs => _database.GetCollection<EventLog>("event_logs");
        public IMongoCollection<AppUser> Users => _database.GetCollection<AppUser>("users");

        //Index tối ưu truy vấn lịch sử (ServerId, Timestamp) + TTL 30 ngày cho metric
        public void EnsureIndexes()
        {
            Metrics.Indexes.CreateOne(new CreateIndexModel<MetricSnapshot>(
                Builders<MetricSnapshot>.IndexKeys.Ascending(m => m.ServerId).Descending(m => m.Timestamp)));
            Metrics.Indexes.CreateOne(new CreateIndexModel<MetricSnapshot>(
                Builders<MetricSnapshot>.IndexKeys.Ascending(m => m.Timestamp),
                new CreateIndexOptions { ExpireAfter = TimeSpan.FromDays(30) }));
            AlertEvents.Indexes.CreateOne(new CreateIndexModel<AlertEvent>(
                Builders<AlertEvent>.IndexKeys.Descending(a => a.TriggeredAt)));
            EventLogs.Indexes.CreateOne(new CreateIndexModel<EventLog>(
                Builders<EventLog>.IndexKeys.Descending(e => e.CreatedAt)));
            Users.Indexes.CreateOne(new CreateIndexModel<AppUser>(
                Builders<AppUser>.IndexKeys.Ascending(u => u.Email), new CreateIndexOptions { Unique = true }));
        }
    }
}
