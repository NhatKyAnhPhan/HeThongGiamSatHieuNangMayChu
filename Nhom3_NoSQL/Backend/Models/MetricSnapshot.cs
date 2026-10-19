using System;
using System.Collections.Generic;
using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace Nhom3_NoSQL.Backend.Models
{
    //Một lần đo hiệu năng tại 1 thời điểm
    [BsonIgnoreExtraElements]
    public class MetricSnapshot
    {
        [BsonId]
        [BsonRepresentation(BsonType.ObjectId)]
        public string Id { get; set; }

        public string ServerId { get; set; }
        public DateTime Timestamp { get; set; } = DateTime.UtcNow;

        public double CpuUsagePercent { get; set; }
        public double RamUsagePercent { get; set; }
        public double DiskUsagePercent { get; set; }
        public double NetworkInKbps { get; set; }
        public double NetworkOutKbps { get; set; }
    }
}
