using System;
using System.Collections.Generic;
using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace Nhom3_NoSQL.Backend.Models
{
    public enum AlertSeverity { Warning, Critical }

    //Trạng thái xử lý sự cố: OPENING -> ACKNOWLEDGED -> RESOLVED
    public static class AlertStatus
    {
        public const string Opening = "OPENING";
        public const string Acknowledged = "ACKNOWLEDGED";
        public const string Resolved = "RESOLVED";
    }

    //Bản ghi 1 lần cảnh báo được kích hoạt
    [BsonIgnoreExtraElements]
    public class AlertEvent
    {
        [BsonId]
        [BsonRepresentation(BsonType.ObjectId)]
        public string Id { get; set; }

        public string Code { get; set; } // vd: INC-2048
        public string ServerId { get; set; }
        public string ServerName { get; set; }
        public MetricType Metric { get; set; }
        public double ActualValue { get; set; }
        public double ThresholdValue { get; set; }
        public int DurationSeconds { get; set; }
        public AlertSeverity Severity { get; set; }
        public string Message { get; set; }
        public DateTime TriggeredAt { get; set; } = DateTime.UtcNow;
        public string Status { get; set; } = AlertStatus.Opening;
        public DateTime? AcknowledgedAt { get; set; }
        public string AcknowledgedBy { get; set; }
        public DateTime? ResolvedAt { get; set; }
        public string ResolvedBy { get; set; }
        public bool IsResolved { get; set; } = false;
    }
}
