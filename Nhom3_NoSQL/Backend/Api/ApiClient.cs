using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Nhom3_NoSQL.Backend.Models;
using Nhom3_NoSQL.Backend.Services;

namespace Nhom3_NoSQL.Backend.Api
{
    //Lỗi API: giữ mã HTTP để giao diện / test phân biệt 400, 401, 403, 404, 409...
    public class ApiClientException : Exception
    {
        public int StatusCode { get; }
        public ApiClientException(int statusCode, string message) : base(message) => StatusCode = statusCode;
    }

    //Client gọi REST API /api/v1 - dùng cho giao diện WinForms và project kiểm thử
    public class ApiClient
    {
        private readonly HttpClient _http;

        public string Token { get; private set; }
        public UserSession User { get; private set; }
        public bool IsAdmin => User?.Role == UserRole.Admin;

        //Phát ra khi API trả 401 (token hết hạn) để giao diện quay về màn hình đăng nhập
        public event Action Unauthorized;

        public ApiClient(string baseUrl)
        {
            _http = new HttpClient { BaseAddress = new Uri(baseUrl.TrimEnd('/') + "/api/v1/"), Timeout = TimeSpan.FromSeconds(20) };
        }

        // ---------------- Xác thực ----------------
        public async Task<LoginResponse> LoginAsync(string email, string password)
        {
            var r = await SendAsync<LoginResponse>(HttpMethod.Post, "auth/login", new LoginBody { Email = email, Password = password });
            Token = r.Token;
            User = r.User;
            return r;
        }

        public async Task LogoutAsync()
        {
            try { if (Token != null) await SendAsync<OkDto>(HttpMethod.Post, "auth/logout"); }
            finally { Token = null; User = null; }
        }

        public Task<UserSession> GetMeAsync() => SendAsync<UserSession>(HttpMethod.Get, "auth/me");

        // ---------------- Host ----------------
        public Task<List<HostView>> GetHostsAsync(string q = null, string status = null) =>
            SendAsync<List<HostView>>(HttpMethod.Get, "hosts" + Query(("q", q), ("status", status)));

        public Task<HostView> GetHostAsync(string id) => SendAsync<HostView>(HttpMethod.Get, "hosts/" + id);

        public Task<ServerInfo> CreateHostAsync(ServerInfo host) => SendAsync<ServerInfo>(HttpMethod.Post, "hosts", host);

        public Task<ServerInfo> UpdateHostAsync(ServerInfo host) => SendAsync<ServerInfo>(HttpMethod.Put, "hosts/" + host.Id, host);

        public Task DeleteHostAsync(string id) => SendAsync<OkDto>(HttpMethod.Delete, "hosts/" + id);

        // ---------------- Metrics ----------------
        public Task<OverviewDto> GetOverviewAsync() => SendAsync<OverviewDto>(HttpMethod.Get, "overview");

        public Task<HistoryDto> GetHistoryAsync(string hostId, string range) =>
            SendAsync<HistoryDto>(HttpMethod.Get, $"metrics/history/{hostId}" + Query(("range", range)));

        public Task<OkDto> CollectAsync(CollectorBody body) => SendAsync<OkDto>(HttpMethod.Post, "metrics/collector", body);

        // ---------------- Alert ----------------
        public Task<AlertsDto> GetAlertsAsync(string status = null, string severity = null, string q = null, int hours = 24) =>
            SendAsync<AlertsDto>(HttpMethod.Get, "alerts" + Query(("status", status), ("severity", severity), ("q", q), ("hours", hours.ToString())));

        public Task<AlertEvent> GetAlertAsync(string id) => SendAsync<AlertEvent>(HttpMethod.Get, "alerts/" + id);

        public Task<AlertEvent> AcknowledgeAsync(string id) => SendAsync<AlertEvent>(HttpMethod.Post, $"alerts/{id}/acknowledge");

        public Task<AlertEvent> ResolveAsync(string id) => SendAsync<AlertEvent>(HttpMethod.Post, $"alerts/{id}/resolve");

        // ---------------- Event Log + Ngưỡng ----------------
        public Task<List<EventLog>> GetEventsAsync(string q = null, string type = null, int take = 300) =>
            SendAsync<List<EventLog>>(HttpMethod.Get, "events" + Query(("q", q), ("type", type), ("take", take.ToString())));

        public Task<List<AlertRule>> GetThresholdsAsync() => SendAsync<List<AlertRule>>(HttpMethod.Get, "thresholds");

        public Task<AlertRule> SaveThresholdAsync(AlertRule rule) => string.IsNullOrEmpty(rule.Id)
            ? SendAsync<AlertRule>(HttpMethod.Post, "thresholds", rule)
            : SendAsync<AlertRule>(HttpMethod.Put, "thresholds/" + rule.Id, rule);

        // ---------------- Hạ tầng ----------------
        static string Query(params (string key, string value)[] items)
        {
            var parts = items.Where(i => !string.IsNullOrEmpty(i.value)).Select(i => i.key + "=" + Uri.EscapeDataString(i.value)).ToList();
            return parts.Count == 0 ? "" : "?" + string.Join("&", parts);
        }

        async Task<T> SendAsync<T>(HttpMethod method, string path, object body = null)
        {
            using (var req = new HttpRequestMessage(method, path))
            {
                if (Token != null) req.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", Token);
                if (body != null) req.Content = new StringContent(JsonSerializer.Serialize(body, ApiJson.Options), Encoding.UTF8, "application/json");

                HttpResponseMessage res;
                try { res = await _http.SendAsync(req); }
                catch (HttpRequestException) { throw new ApiClientException(0, "Không kết nối được API server " + _http.BaseAddress); }
                catch (TaskCanceledException) { throw new ApiClientException(0, "API server không phản hồi (quá thời gian chờ)"); }

                using (res)
                {
                    var text = await res.Content.ReadAsStringAsync();
                    if (res.IsSuccessStatusCode) return JsonSerializer.Deserialize<T>(text, ApiJson.Options);

                    string message;
                    try { message = JsonSerializer.Deserialize<ErrorDto>(text, ApiJson.Options)?.Error; }
                    catch (JsonException) { message = null; }
                    if (res.StatusCode == HttpStatusCode.Unauthorized && path != "auth/login")
                    {
                        Token = null;
                        Unauthorized?.Invoke();
                    }
                    throw new ApiClientException((int)res.StatusCode, message ?? $"Lỗi {(int)res.StatusCode}");
                }
            }
        }
    }
}
