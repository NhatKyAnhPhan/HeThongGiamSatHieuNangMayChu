using System;
using System.Collections.Generic;
namespace Nhom3_NoSQL.Backend.Models
{
    //Một lần đo hiệu năng tại 1 thời điểm
    public class MetricSnapshot
    {
        public string ServerId { get; set; }
        public DateTime Timestamp { get; set; } = DateTime.UtcNow;

        public double CpuUsagePercent { get; set; }
        public double RamUsagePercent { get; set; }
        public double DiskUsagePercent { get; set; }
        public double NetworkInKbps { get; set; }
        public double NetworkOutKbps { get; set; }
    }
}
