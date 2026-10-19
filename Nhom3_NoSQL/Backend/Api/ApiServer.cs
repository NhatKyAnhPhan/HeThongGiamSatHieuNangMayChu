using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using MongoDB.Bson;
using MongoDB.Driver;
using Nhom3_NoSQL.Backend.Interfaces;
using Nhom3_NoSQL.Backend.Models;
using Nhom3_NoSQL.Backend.Services;
using Nhom3_NoSQL.Database;

namespace Nhom3_NoSQL.Backend.Api
{
    //REST API (HttpListener có sẵn trong .NET Framework). Giao diện WinForms và Agent đều giao tiếp qua API này
    public class ApiServer
    {
        class ApiException : Exception
        {
            public int Status { get; }
            public ApiException(int status, string message) : base(message) => Status = status;
        }

        class Req
        {
            public HttpListenerContext Ctx;
            public string Method, Path;
            public string[] Parts;          // phần sau /api/v1/
            public UserSession User;
            public string Query(string key) => Ctx.Request.QueryString[key];
        }

        private readonly HttpListener _listener = new HttpListener();
        private readonly MongoDbContext _ctx;
        private readonly IServerRepository _servers;
        private readonly IMetricRepository _metrics;
        private readonly IAlertRepository _alerts;
        private readonly IEventRepository _events;
        private readonly AuthService _auth;
        private readonly AlertEngineService _engine;

        public string Url { get; }

        public ApiServer(string url, MongoDbContext ctx, IServerRepository servers, IMetricRepository metrics,
            IAlertRepository alerts, IEventRepository events, AuthService auth, AlertEngineService engine)
        {
            Url = url;
            _ctx = ctx;
            _servers = servers;
            _metrics = metrics;
            _alerts = alerts;
            _events = events;
            _auth = auth;
            _engine = engine;
            _listener.Prefixes.Add(url);
        }

        public void Start()
        {
            _listener.Start();
            Task.Run(async () =>
            {
                while (_listener.IsListening)
                {
                    HttpListenerContext c;
                    try { c = await _listener.GetContextAsync(); }
                    catch { break; }
                    _ = Task.Run(() => HandleAsync(c));
                }
            });
        }

        public void Stop() => _listener.Close();

        async Task HandleAsync(HttpListenerContext c)
        {
            try
            {
                var path = c.Request.Url.AbsolutePath;
                if (!path.StartsWith("/api/v1/", StringComparison.OrdinalIgnoreCase))
                {
                    WriteJson(c, 404, new ErrorDto { Error = "Chỉ hỗ trợ /api/v1/..." });
                    return;
                }
                var req = new Req
                {
                    Ctx = c,
                    Method = c.Request.HttpMethod.ToUpperInvariant(),
                    Path = path,
                    Parts = (path.Length > 8 ? path.Substring(8) : "")
                        .Trim('/').Split(new[] { '/' }, StringSplitOptions.RemoveEmptyEntries)
                };
                var result = await RouteAsync(req);
                WriteJson(c, 200, result);
            }
            catch (ApiException ex) { WriteJson(c, ex.Status, new ErrorDto { Error = ex.Message }); }
            catch (FormatException ex) { WriteJson(c, 400, new ErrorDto { Error = ex.Message }); }
            catch (JsonException ex) { WriteJson(c, 400, new ErrorDto { Error = "JSON không hợp lệ: " + ex.Message }); }
            catch (TimeoutException) { WriteJson(c, 503, new ErrorDto { Error = "Không kết nối được MongoDB" }); }
            catch (Exception ex) { WriteJson(c, 500, new ErrorDto { Error = ex.Message }); }
        }

        // ===================== ROUTING =====================
        async Task<object> RouteAsync(Req r)
        {
            var p = r.Parts;
            string M(int i) => p.Length > i ? p[i] : null;

            // ---- Không cần đăng nhập ----
            if (r.Method == "POST" && M(0) == "auth" && M(1) == "login") return await LoginAsync(r);
            if (r.Method == "POST" && M(0) == "metrics" && M(1) == "collector") return await CollectAsync(r);

            // ---- Cần đăng nhập ----
            r.User = _auth.Validate(BearerToken(r));
            if (r.User == null) throw new ApiException(401, "Chưa đăng nhập hoặc phiên đã hết hạn");

            switch (M(0))
            {
                case "auth":
                    if (M(1) == "me") return r.User;
                    if (M(1) == "logout" && r.Method == "POST") { _auth.Logout(BearerToken(r)); return new OkDto { Ok = true }; }
                    break;
                case "overview":
                    return await OverviewAsync();
                case "hosts":
                    if (M(1) == null && r.Method == "GET") return await HostsAsync(r);
                    if (M(1) == null && r.Method == "POST") { RequireAdmin(r); return await CreateHostAsync(r); }
                    if (M(1) != null && M(2) == null && r.Method == "GET") return await HostDetailAsync(M(1));
                    if (M(1) != null && r.Method == "PUT") { RequireAdmin(r); return await UpdateHostAsync(r, M(1)); }
                    if (M(1) != null && r.Method == "DELETE") { RequireAdmin(r); return await DeleteHostAsync(r, M(1)); }
                    break;
                case "metrics":
                    if (M(1) == "history" && M(2) != null) return await HistoryAsync(M(2), r.Query("range"));
                    if (M(1) == "latest" && M(2) != null) return await _metrics.GetLatestAsync(CheckId(M(2)));
                    break;
                case "alerts":
                    if (M(1) == null && r.Method == "GET") return await AlertsAsync(r);
                    if (M(1) != null && M(2) == null && r.Method == "GET") return await _alerts.GetAlertAsync(CheckId(M(1))) ?? throw new ApiException(404, "Không tìm thấy sự cố");
                    if (M(1) != null && r.Method == "POST" && (M(2) == "acknowledge" || M(2) == "resolve"))
                    {
                        RequireAdmin(r);
                        return await ChangeAlertStatusAsync(r, M(1), M(2) == "resolve");
                    }
                    break;
                case "events":
                    return await _events.SearchAsync(r.Query("q"), r.Query("type"), ParseInt(r.Query("take"), 200));
                case "thresholds":
                    if (M(1) == null && r.Method == "GET") return await _alerts.GetAllRulesAsync();
                    if (M(1) == null && r.Method == "POST") { RequireAdmin(r); return await SaveRuleAsync(r, null); }
                    if (M(1) != null && r.Method == "PUT") { RequireAdmin(r); return await SaveRuleAsync(r, M(1)); }
                    break;
            }
            throw new ApiException(404, $"Không có API {r.Method} {r.Path}");
        }

        // ===================== AUTH =====================
        async Task<object> LoginAsync(Req r)
        {
            var body = ReadBody<LoginBody>(r);
            var (token, session) = await _auth.LoginAsync(body?.Email, body?.Password);
            if (token == null) throw new ApiException(401, "Email hoặc mật khẩu không đúng");
            await _events.InsertAsync(new EventLog { Type = "AUTH", Level = "INFO", Actor = session.Email, Message = $"{session.Email} đăng nhập" });
            return new LoginResponse { Token = token, User = session };
        }

        static string BearerToken(Req r)
        {
            var h = r.Ctx.Request.Headers["Authorization"];
            return h != null && h.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase) ? h.Substring(7).Trim() : null;
        }

        static void RequireAdmin(Req r)
        {
            if (r.User.Role != UserRole.Admin) throw new ApiException(403, "Chỉ Quản trị viên (Admin) được thực hiện thao tác này");
        }

        // ===================== HOSTS =====================
        async Task<List<HostView>> BuildHostViewsAsync(IEnumerable<ServerInfo> hosts, bool spark = false)
        {
            var openAlerts = (await _alerts.GetRecentAlertsAsync(500)).Where(a => a.Status != AlertStatus.Resolved).ToList();
            var views = new List<HostView>();
            foreach (var h in hosts)
            {
                var latest = await _metrics.GetLatestAsync(h.Id);
                var hostAlerts = openAlerts.Where(a => a.ServerId == h.Id).ToList();
                views.Add(new HostView
                {
                    Host = h,
                    Latest = latest,
                    LastSeen = latest?.Timestamp,
                    Status = HostStatus(latest, hostAlerts),
                    // sparkline: CPU 2 giờ gần nhất, trung bình theo 24 nhóm 5 phút
                    Spark = spark
                        ? (await _metrics.GetHistoryAsync(h.Id, DateTime.UtcNow.AddHours(-2), DateTime.UtcNow))
                            .GroupBy(m => m.Timestamp.Ticks / TimeSpan.FromMinutes(5).Ticks)
                            .Select(g => Math.Round(g.Average(m => Math.Max(m.CpuUsagePercent, Math.Max(m.RamUsagePercent, m.DiskUsagePercent))), 1))
                            .ToArray()
                        : null
                });
            }
            return views;
        }

        public static string HostStatus(MetricSnapshot latest, List<AlertEvent> openAlerts)
        {
            if (latest == null || DateTime.UtcNow - latest.Timestamp > AlertEngineService.HeartbeatTimeout) return "OFFLINE";
            if (openAlerts.Any(a => a.Severity == AlertSeverity.Critical && a.Metric != MetricType.Heartbeat)) return "CRITICAL";
            if (openAlerts.Any(a => a.Metric != MetricType.Heartbeat)) return "WARNING";
            return "ONLINE";
        }

        async Task<object> HostsAsync(Req r)
        {
            var q = (r.Query("q") ?? "").Trim().ToLowerInvariant();
            var status = r.Query("status");
            var os = r.Query("os");
            var hosts = (await _servers.GetAllAsync()).Where(h =>
                (q == "" || (h.Name ?? "").ToLowerInvariant().Contains(q) || (h.IpAddress ?? "").Contains(q) || (h.OperatingSystem ?? "").ToLowerInvariant().Contains(q))
                && (string.IsNullOrEmpty(os) || h.OperatingSystem == os));
            var views = await BuildHostViewsAsync(hosts);
            if (!string.IsNullOrEmpty(status)) views = views.Where(v => v.Status == status).ToList();
            return views;
        }

        async Task<object> HostDetailAsync(string id)
        {
            var h = await _servers.GetByIdAsync(CheckId(id)) ?? throw new ApiException(404, "Không tìm thấy máy chủ");
            return (await BuildHostViewsAsync(new[] { h })).First();
        }

        static void ValidateHost(ServerInfo h)
        {
            if (h == null || string.IsNullOrWhiteSpace(h.Name)) throw new ApiException(400, "Hostname là bắt buộc");
            if (!IPAddress.TryParse(h.IpAddress ?? "", out _)) throw new ApiException(400, "Địa chỉ IP không hợp lệ");
            h.Name = h.Name.Trim();
        }

        async Task<object> CreateHostAsync(Req r)
        {
            var h = ReadBody<ServerInfo>(r);
            ValidateHost(h);
            if ((await _servers.GetAllAsync()).Any(x => x.Name == h.Name)) throw new ApiException(409, "Hostname đã tồn tại");
            h.Id = null;
            h.CreatedAt = DateTime.UtcNow;
            await _servers.InsertAsync(h);
            await LogAsync(r, h, "HOST", "INFO", $"Thêm máy chủ {h.Name} ({h.IpAddress})");
            return h;
        }

        async Task<object> UpdateHostAsync(Req r, string id)
        {
            var old = await _servers.GetByIdAsync(CheckId(id)) ?? throw new ApiException(404, "Không tìm thấy máy chủ");
            var h = ReadBody<ServerInfo>(r);
            ValidateHost(h);
            h.Id = old.Id;
            h.CreatedAt = old.CreatedAt;
            await _servers.UpdateAsync(h);
            await LogAsync(r, h, "HOST", "INFO", $"Cập nhật máy chủ {h.Name}");
            return h;
        }

        async Task<object> DeleteHostAsync(Req r, string id)
        {
            var h = await _servers.GetByIdAsync(CheckId(id)) ?? throw new ApiException(404, "Không tìm thấy máy chủ");
            await _servers.DeleteAsync(h.Id);
            await _ctx.AlertRules.DeleteManyAsync(rule => rule.ServerId == h.Id); // bỏ quy tắc riêng của host đã xóa
            // đóng các sự cố còn mở của host đã xóa để không treo mãi ở OPENING
            await _ctx.AlertEvents.UpdateManyAsync(a => a.ServerId == h.Id && a.Status != AlertStatus.Resolved,
                Builders<AlertEvent>.Update.Set(a => a.Status, AlertStatus.Resolved).Set(a => a.IsResolved, true)
                    .Set(a => a.ResolvedAt, DateTime.UtcNow).Set(a => a.ResolvedBy, r.User.Email + " (xóa host)"));
            await LogAsync(r, h, "HOST", "WARNING", $"Xóa máy chủ {h.Name}");
            return new OkDto { Ok = true };
        }

        // ===================== METRICS =====================
        //POST /api/v1/metrics/collector - Agent gửi dữ liệu về, xác thực Host ID rồi ghi vào metrics + chạy Alert Engine
        async Task<object> CollectAsync(Req r)
        {
            var b = ReadBody<CollectorBody>(r) ?? throw new ApiException(400, "Thiếu dữ liệu");
            var host = await _servers.GetByIdAsync(CheckId(b.ServerId)) ?? throw new ApiException(404, "Host ID không tồn tại");
            foreach (var v in new[] { b.Cpu, b.Ram, b.Disk })
                if (v < 0 || v > 100) throw new ApiException(400, "CPU/RAM/Disk phải trong khoảng 0-100");
            var s = new MetricSnapshot
            {
                ServerId = host.Id,
                CpuUsagePercent = b.Cpu, RamUsagePercent = b.Ram, DiskUsagePercent = b.Disk,
                NetworkInKbps = Math.Max(0, b.NetworkInKbps), NetworkOutKbps = Math.Max(0, b.NetworkOutKbps)
            };
            await _metrics.InsertAsync(s);
            await _engine.EvaluateAsync(s, host.Name);
            return new OkDto { Ok = true, Id = s.Id };
        }

        static TimeSpan ParseRange(string range)
        {
            switch (range)
            {
                case "1h": return TimeSpan.FromHours(1);
                case "24h": return TimeSpan.FromHours(24);
                case "7d": return TimeSpan.FromDays(7);
                default: return TimeSpan.FromHours(6);
            }
        }

        //Lịch sử hiệu năng, gom nhóm trung bình theo bucket để biểu đồ không quá 120 điểm
        async Task<object> HistoryAsync(string id, string range)
        {
            var span = ParseRange(range);
            var to = DateTime.UtcNow;
            var raw = await _metrics.GetHistoryAsync(CheckId(id), to - span, to);
            var bucket = Math.Max(1, (long)Math.Ceiling(span.TotalSeconds / 120));
            var points = raw.GroupBy(m => m.Timestamp.Ticks / TimeSpan.TicksPerSecond / bucket)
                .Select(g => new HistoryPoint
                {
                    T = g.First().Timestamp,
                    Cpu = Math.Round(g.Average(x => x.CpuUsagePercent), 1),
                    Ram = Math.Round(g.Average(x => x.RamUsagePercent), 1),
                    Disk = Math.Round(g.Average(x => x.DiskUsagePercent), 1),
                    NetIn = Math.Round(g.Average(x => x.NetworkInKbps)),
                    NetOut = Math.Round(g.Average(x => x.NetworkOutKbps)),
                }).ToList();
            return new HistoryDto
            {
                Range = range ?? "6h",
                Points = points,
                Summary = raw.Count == 0 ? null : new HistorySummary
                {
                    CpuPeak = raw.Max(x => x.CpuUsagePercent), CpuAvg = Math.Round(raw.Average(x => x.CpuUsagePercent), 1),
                    RamPeak = raw.Max(x => x.RamUsagePercent), RamAvg = Math.Round(raw.Average(x => x.RamUsagePercent), 1),
                    DiskPeak = raw.Max(x => x.DiskUsagePercent), DiskAvg = Math.Round(raw.Average(x => x.DiskUsagePercent), 1),
                    NetPeak = raw.Max(x => x.NetworkInKbps + x.NetworkOutKbps), NetAvg = Math.Round(raw.Average(x => x.NetworkInKbps + x.NetworkOutKbps)),
                }
            };
        }

        // ===================== OVERVIEW (Aggregation Pipeline) =====================
        async Task<object> OverviewAsync()
        {
            var hosts = await _servers.GetAllAsync();
            var views = await BuildHostViewsAsync(hosts, spark: true);
            var online = views.Where(v => v.Status != "OFFLINE").ToList();

            // Trung bình CPU/RAM toàn hệ thống mỗi 10 phút trong 6 giờ: $match -> $group theo bucket thời gian -> $sort
            var from = DateTime.UtcNow.AddHours(-6);
            const long bucketMs = 10 * 60 * 1000;
            var pipeline = new[]
            {
                new BsonDocument("$match", new BsonDocument("Timestamp", new BsonDocument("$gte", from))),
                new BsonDocument("$group", new BsonDocument
                {
                    { "_id", new BsonDocument("$subtract", new BsonArray {
                        new BsonDocument("$toLong", "$Timestamp"),
                        new BsonDocument("$mod", new BsonArray { new BsonDocument("$toLong", "$Timestamp"), bucketMs }) }) },
                    { "cpu", new BsonDocument("$avg", "$CpuUsagePercent") },
                    { "ram", new BsonDocument("$avg", "$RamUsagePercent") },
                }),
                new BsonDocument("$sort", new BsonDocument("_id", 1)),
            };
            var series = (await _ctx.Metrics.Aggregate<BsonDocument>(pipeline).ToListAsync())
                .Select(d => new SeriesPoint
                {
                    T = DateTimeOffset.FromUnixTimeMilliseconds(d["_id"].ToInt64()).UtcDateTime,
                    Cpu = Math.Round(d["cpu"].ToDouble(), 1),
                    Ram = Math.Round(d["ram"].ToDouble(), 1)
                }).ToList();

            var alerts = await _alerts.GetRecentAlertsAsync(5);
            double Avg(Func<MetricSnapshot, double> f) => online.Count == 0 ? 0 : Math.Round(online.Average(v => f(v.Latest)), 1);
            int Count(string s) => views.Count(v => v.Status == s);
            int critical = Count("CRITICAL"), warning = Count("WARNING"), offline = Count("OFFLINE");
            double health = hosts.Count == 0 ? 100 : Math.Round(100 - (critical * 2.0 + warning + offline * 1.5) / hosts.Count * 10, 1);

            return new OverviewDto
            {
                Total = hosts.Count,
                Online = views.Count(v => v.Status != "OFFLINE"),
                Warning = warning, Critical = critical, Offline = offline,
                AvgCpu = Avg(m => m.CpuUsagePercent),
                AvgRam = Avg(m => m.RamUsagePercent),
                AvgDisk = Avg(m => m.DiskUsagePercent),
                TotalNetMbps = Math.Round(online.Sum(v => v.Latest.NetworkInKbps + v.Latest.NetworkOutKbps) / 1000, 1),
                HealthScore = Math.Max(0, health),
                Series = series,
                Attention = views.Where(v => v.Status != "ONLINE")
                    .OrderBy(v => v.Status == "CRITICAL" ? 0 : v.Status == "OFFLINE" ? 1 : 2).Take(5).ToList(),
                RecentAlerts = alerts
            };
        }

        // ===================== ALERTS =====================
        async Task<object> AlertsAsync(Req r)
        {
            var status = r.Query("status");       // OPENING, ACKNOWLEDGED, RESOLVED hoặc "ACTIVE" = OPENING+ACK
            var severity = r.Query("severity");   // Warning, Critical
            var q = (r.Query("q") ?? "").Trim().ToLowerInvariant();
            var hours = ParseInt(r.Query("hours"), 24);
            var since = DateTime.UtcNow.AddHours(-hours);

            var all = await _alerts.GetRecentAlertsAsync(1000);
            var items = all.Where(a => a.TriggeredAt >= since || a.Status != AlertStatus.Resolved)
                .Where(a => string.IsNullOrEmpty(status) || (status == "ACTIVE" ? a.Status != AlertStatus.Resolved : a.Status == status))
                .Where(a => string.IsNullOrEmpty(severity) || a.Severity.ToString() == severity)
                .Where(a => q == "" || (a.Message ?? "").ToLowerInvariant().Contains(q) || (a.ServerName ?? "").ToLowerInvariant().Contains(q) || (a.Code ?? "").ToLowerInvariant().Contains(q))
                .ToList();

            var today = DateTime.UtcNow.Date;
            var acked = all.Where(a => a.AcknowledgedAt.HasValue).ToList();
            var resolved = all.Where(a => a.ResolvedAt.HasValue).ToList();
            return new AlertsDto
            {
                Items = items,
                Stats = new AlertStats
                {
                    Opening = all.Count(a => a.Status == AlertStatus.Opening),
                    OpeningCritical = all.Count(a => a.Status == AlertStatus.Opening && a.Severity == AlertSeverity.Critical),
                    Acknowledged = all.Count(a => a.Status == AlertStatus.Acknowledged),
                    ResolvedToday = resolved.Count(a => a.ResolvedAt.Value >= today),
                    MttaSeconds = acked.Count == 0 ? 0 : Math.Round(acked.Average(a => (a.AcknowledgedAt.Value - a.TriggeredAt).TotalSeconds)),
                    MttrSeconds = resolved.Count == 0 ? 0 : Math.Round(resolved.Average(a => (a.ResolvedAt.Value - a.TriggeredAt).TotalSeconds)),
                }
            };
        }

        async Task<object> ChangeAlertStatusAsync(Req r, string id, bool resolve)
        {
            var a = await _alerts.GetAlertAsync(CheckId(id)) ?? throw new ApiException(404, "Không tìm thấy sự cố");
            if (a.Status == AlertStatus.Resolved) throw new ApiException(409, "Sự cố đã được xử lý");
            if (!resolve && a.Status == AlertStatus.Acknowledged) throw new ApiException(409, "Sự cố đã được tiếp nhận");

            var now = DateTime.UtcNow;
            if (!a.AcknowledgedAt.HasValue) { a.AcknowledgedAt = now; a.AcknowledgedBy = r.User.Email; }
            if (resolve) { a.ResolvedAt = now; a.ResolvedBy = r.User.Email; a.IsResolved = true; a.Status = AlertStatus.Resolved; }
            else a.Status = AlertStatus.Acknowledged;
            await _alerts.UpdateAlertAsync(a);
            await _events.InsertAsync(new EventLog
            {
                ServerId = a.ServerId, ServerName = a.ServerName, Type = "ALERT", Level = "INFO", Actor = r.User.Email,
                Message = $"[{a.Code}] {(resolve ? "Đã xử lý (RESOLVED)" : "Đã tiếp nhận (ACKNOWLEDGED)")} bởi {r.User.Email}"
            });
            return a;
        }

        // ===================== THRESHOLDS =====================
        async Task<object> SaveRuleAsync(Req r, string id)
        {
            var rule = ReadBody<AlertRule>(r) ?? throw new ApiException(400, "Thiếu dữ liệu");
            if (rule.ThresholdValue <= 0 || rule.CriticalValue <= 0) throw new ApiException(400, "Ngưỡng phải lớn hơn 0");
            if (rule.Operator == ComparisonOperator.GreaterThan && rule.CriticalValue < rule.ThresholdValue)
                throw new ApiException(400, "Ngưỡng Critical phải lớn hơn hoặc bằng ngưỡng Warning");
            if (rule.Unit == "%" && (rule.ThresholdValue > 100 || rule.CriticalValue > 100)) throw new ApiException(400, "Ngưỡng % không được vượt 100");
            if (rule.DurationSeconds < 0) throw new ApiException(400, "duration_seconds không hợp lệ");

            if (!string.IsNullOrEmpty(rule.ServerId))
            {
                var host = await _servers.GetByIdAsync(CheckId(rule.ServerId)) ?? throw new ApiException(400, "Host không tồn tại");
                rule.ServerName = host.Name;
            }
            else { rule.ServerId = null; rule.ServerName = null; }

            if (id == null) { rule.Id = null; await _alerts.InsertRuleAsync(rule); }
            else
            {
                _ = await _alerts.GetRuleAsync(CheckId(id)) ?? throw new ApiException(404, "Không tìm thấy quy tắc");
                rule.Id = id;
                await _alerts.UpdateRuleAsync(rule);
            }
            await _events.InsertAsync(new EventLog
            {
                ServerId = rule.ServerId, ServerName = rule.ServerName ?? "Toàn hệ thống", Type = "THRESHOLD", Level = "INFO", Actor = r.User.Email,
                Message = $"{(id == null ? "Tạo" : "Cập nhật")} ngưỡng {AlertEngineService.MetricLabel(rule.Metric)}: warning {rule.ThresholdValue}{rule.Unit}, critical {rule.CriticalValue}{rule.Unit}, {rule.DurationSeconds}s, {(rule.IsEnabled ? "bật" : "tắt")}"
            });
            return rule;
        }

        // ===================== HELPERS =====================
        Task LogAsync(Req r, ServerInfo h, string type, string level, string msg) =>
            _events.InsertAsync(new EventLog { ServerId = h.Id, ServerName = h.Name, Type = type, Level = level, Message = msg, Actor = r.User?.Email ?? "system" });

        static string CheckId(string id)
        {
            if (!ObjectId.TryParse(id ?? "", out _)) throw new ApiException(400, "ID không hợp lệ");
            return id;
        }

        static int ParseInt(string s, int def) => int.TryParse(s, out var v) && v > 0 ? Math.Min(v, 1000) : def;

        static T ReadBody<T>(Req r)
        {
            using (var sr = new StreamReader(r.Ctx.Request.InputStream, Encoding.UTF8))
            {
                var s = sr.ReadToEnd();
                return string.IsNullOrWhiteSpace(s) ? default(T) : JsonSerializer.Deserialize<T>(s, ApiJson.Options);
            }
        }

        static void WriteJson(HttpListenerContext c, int status, object data)
        {
            try
            {
                var bytes = JsonSerializer.SerializeToUtf8Bytes(data, ApiJson.Options);
                c.Response.StatusCode = status;
                c.Response.ContentType = "application/json; charset=utf-8";
                c.Response.Headers["Cache-Control"] = "no-store";
                c.Response.OutputStream.Write(bytes, 0, bytes.Length);
            }
            finally { c.Response.Close(); }
        }

    }
}
