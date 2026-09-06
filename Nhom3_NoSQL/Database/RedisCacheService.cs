using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading.Tasks;
using StackExchange.Redis;
using Nhom3_NoSQL.Backend.Interfaces;
using Nhom3_NoSQL.Backend.Models;

namespace Nhom3_NoSQL.Database
{
    public class RedisCacheService : ICacheService
    {
        private readonly ConnectionMultiplexer _redis;
        private readonly IDatabase _db;

        public RedisCacheService(string connectionString)
        {
            _redis = ConnectionMultiplexer.Connect(connectionString);
            _db = _redis.GetDatabase();
        }

        public Task SetLatestMetricAsync(string serverId, MetricSnapshot snapshot)
        {
            var json = JsonSerializer.Serialize(snapshot);
            // TTL 5 phút: nếu server ngừng gửi dữ liệu, key tự hết hạn -> UI biết server "mất kết nối"
            return _db.StringSetAsync($"server:{serverId}:latest", json, TimeSpan.FromMinutes(5));
        }

        public async Task<MetricSnapshot> GetLatestMetricAsync(string serverId)
        {
            var json = await _db.StringGetAsync($"server:{serverId}:latest");
            if (json.IsNullOrEmpty) return null;
            return JsonSerializer.Deserialize<MetricSnapshot>(json.ToString());
        }

        public Task PublishAsync(string channel, string payloadJson) =>
            _redis.GetSubscriber().PublishAsync(RedisChannel.Literal(channel), payloadJson);

        public Task SubscribeAsync(string channel, Action<string> onMessage) =>
            _redis.GetSubscriber().SubscribeAsync(RedisChannel.Literal(channel),
                (ch, value) => onMessage(value.ToString()));
    }
}