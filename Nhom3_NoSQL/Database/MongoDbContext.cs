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
    }
}
