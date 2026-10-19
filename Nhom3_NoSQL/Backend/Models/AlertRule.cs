using System;
using System.Collections.Generic;
using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace Nhom3_NoSQL.Backend.Models
{
    public enum MetricType { Cpu, Ram, Disk, NetworkIn, NetworkOut, Heartbeat }
    public enum ComparisonOperator { GreaterThan, LessThan }

    //Ngưỡng cảnh báo. ServerId = null -> áp dụng toàn hệ thống; có ServerId -> riêng host (ưu tiên hơn)
    [BsonIgnoreExtraElements]
    public class AlertRule
    {
        [BsonId]
        [BsonRepresentation(BsonType.ObjectId)]
        public string Id { get; set; }

        public string ServerId { get; set; }
        public string ServerName { get; set; }
        public MetricType Metric { get; set; }
        public string Unit { get; set; } = "%";
        public ComparisonOperator Operator { get; set; } = ComparisonOperator.GreaterThan;
        public double ThresholdValue { get; set; }      // ngưỡng WARNING
        public double CriticalValue { get; set; }       // ngưỡng CRITICAL
        public int DurationSeconds { get; set; } = 0;   // vượt ngưỡng liên tục bao lâu mới báo (chống báo giả)
        public bool IsEnabled { get; set; } = true;
    }
}
