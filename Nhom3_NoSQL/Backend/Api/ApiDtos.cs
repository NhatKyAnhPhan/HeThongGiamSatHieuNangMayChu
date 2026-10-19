using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Serialization;
using Nhom3_NoSQL.Backend.Models;
using Nhom3_NoSQL.Backend.Services;

namespace Nhom3_NoSQL.Backend.Api
{
    //Các kiểu dữ liệu vào/ra của REST API - dùng chung cho ApiServer (server) và ApiClient (giao diện, test)
    public static class ApiJson
    {
        public static readonly JsonSerializerOptions Options = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            PropertyNameCaseInsensitive = true,
            Converters = { new JsonStringEnumConverter() }
        };
    }

    public class ErrorDto { public string Error { get; set; } }
    public class OkDto { public bool Ok { get; set; } public string Id { get; set; } }

    public class LoginBody { public string Email { get; set; } public string Password { get; set; } }
    public class LoginResponse { public string Token { get; set; } public UserSession User { get; set; } }

    //Máy chủ kèm metric mới nhất và trạng thái tính từ heartbeat + sự cố đang mở
    public class HostView
    {
        public ServerInfo Host { get; set; }
        public MetricSnapshot Latest { get; set; }
        public string Status { get; set; }     // ONLINE, WARNING, CRITICAL, OFFLINE
        public DateTime? LastSeen { get; set; }
        public double[] Spark { get; set; }
    }

    //Body Agent gửi về POST /api/v1/metrics/collector
    public class CollectorBody
    {
        public string ServerId { get; set; }
        public double Cpu { get; set; }
        public double Ram { get; set; }
        public double Disk { get; set; }
        public double NetworkInKbps { get; set; }
        public double NetworkOutKbps { get; set; }
    }

    public class HistoryPoint
    {
        public DateTime T { get; set; }
        public double Cpu { get; set; }
        public double Ram { get; set; }
        public double Disk { get; set; }
        public double NetIn { get; set; }
        public double NetOut { get; set; }
    }

    public class HistorySummary
    {
        public double CpuPeak { get; set; }
        public double CpuAvg { get; set; }
        public double RamPeak { get; set; }
        public double RamAvg { get; set; }
        public double DiskPeak { get; set; }
        public double DiskAvg { get; set; }
        public double NetPeak { get; set; }
        public double NetAvg { get; set; }
    }

    public class HistoryDto
    {
        public string Range { get; set; }
        public List<HistoryPoint> Points { get; set; } = new List<HistoryPoint>();
        public HistorySummary Summary { get; set; }
    }

    public class SeriesPoint
    {
        public DateTime T { get; set; }
        public double Cpu { get; set; }
        public double Ram { get; set; }
    }

    public class OverviewDto
    {
        public int Total { get; set; }
        public int Online { get; set; }
        public int Warning { get; set; }
        public int Critical { get; set; }
        public int Offline { get; set; }
        public double AvgCpu { get; set; }
        public double AvgRam { get; set; }
        public double AvgDisk { get; set; }
        public double TotalNetMbps { get; set; }
        public double HealthScore { get; set; }
        public List<SeriesPoint> Series { get; set; } = new List<SeriesPoint>();
        public List<HostView> Attention { get; set; } = new List<HostView>();
        public List<AlertEvent> RecentAlerts { get; set; } = new List<AlertEvent>();
    }

    public class AlertStats
    {
        public int Opening { get; set; }
        public int OpeningCritical { get; set; }
        public int Acknowledged { get; set; }
        public int ResolvedToday { get; set; }
        public double MttaSeconds { get; set; }
        public double MttrSeconds { get; set; }
    }

    public class AlertsDto
    {
        public List<AlertEvent> Items { get; set; } = new List<AlertEvent>();
        public AlertStats Stats { get; set; } = new AlertStats();
    }
}
