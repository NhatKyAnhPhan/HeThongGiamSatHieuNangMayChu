using System;
using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace Nhom3_NoSQL.Backend.Models
{
    //Nhật ký sự kiện hệ thống: host online/offline, tạo/tiếp nhận/xử lý alert, đổi cấu hình...
    [BsonIgnoreExtraElements]
    public class EventLog
    {
        [BsonId]
        [BsonRepresentation(BsonType.ObjectId)]
        public string Id { get; set; }

        public string ServerId { get; set; }
        public string ServerName { get; set; }
        public string Type { get; set; }   // HOST, ALERT, THRESHOLD, AUTH
        public string Level { get; set; }  // INFO, WARNING, CRITICAL
        public string Message { get; set; }
        public string Actor { get; set; }  // "system" hoặc email người thao tác
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }
}
