using System;
using System.Collections.Generic;
using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace Nhom3_NoSQL.Backend.Models
{
    //Thông tin cấu hình 1 máy chủ được giám sát
    [BsonIgnoreExtraElements]
    public class ServerInfo
    {
        [BsonId]
        [BsonRepresentation(BsonType.ObjectId)]
        public string Id { get; set; }

        public string Name { get; set; }
        public string IpAddress { get; set; }
        public string OperatingSystem { get; set; }
        public string Description { get; set; }
        public string Region { get; set; }
        public int CpuCores { get; set; }
        public double RamTotalGb { get; set; }
        public double DiskTotalGb { get; set; }
        public bool IsActive { get; set; } = true;
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }
}
