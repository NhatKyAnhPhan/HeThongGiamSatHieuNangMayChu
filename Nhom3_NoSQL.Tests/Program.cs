using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Windows.Forms.DataVisualization.Charting;
using Nhom3_NoSQL.Backend.Api;
using Nhom3_NoSQL.Backend.Models;
using Nhom3_NoSQL.Database;
using Nhom3_NoSQL.Frontend;
using Nhom3_NoSQL.Frontend.Pages;
using Nhom3_NoSQL.Frontend.UI;

namespace Nhom3_NoSQL.Tests
{
    //Bộ kiểm thử tự động ServerPulse.
    //Cách chạy: mở app (hoặc Nhom3_NoSQL.exe --headless) rồi chạy project này:  Nhom3_NoSQL.Tests.exe [http://localhost:5080]
    //Mỗi test tương ứng 1 mã test case trong Docs/TestCases.md
    internal static class Program
    {
        class Case { public string Group, Code, Name; public Func<Task> Run; public string Result, Detail; }

        static readonly List<Case> Cases = new List<Case>();
        static string BaseUrl = "http://localhost:5080";
        static ApiClient Admin, Viewer, Anon;
        static readonly Dictionary<string, string> Ctx = new Dictionary<string, string>();
        // exception phát sinh trong vòng lặp giao diện (vd khi vẽ control) - test nào gây ra sẽ bị FAIL
        static readonly List<Exception> UiErrors = new List<Exception>();

        static void T(string group, string code, string name, Func<Task> run) => Cases.Add(new Case { Group = group, Code = code, Name = name, Run = run });

        [STAThread]
        static int Main(string[] args)
        {
            Console.OutputEncoding = Encoding.UTF8;
            Application.EnableVisualStyles();
            Application.SetUnhandledExceptionMode(UnhandledExceptionMode.CatchException);
            // --thu-cong: chạy 6 test giao diện thủ công (TC-MAN-07..12) bằng chuột/bàn phím thật của hệ điều hành
            if (args.Contains("--thu-cong")) return ManualUi.Run();
            if (args.Length > 0) BaseUrl = args[0].TrimEnd('/');
            Application.ThreadException += (s, e) => UiErrors.Add(e.Exception);
            Register();

            // Chạy trong vòng lặp thông điệp WinForms để kiểm thử được cả giao diện
            int exit = 1;
            var ctx = new ApplicationContext();
            var start = new Timer { Interval = 50 };
            start.Tick += async (s, e) =>
            {
                start.Stop();
                try { exit = await RunAll(); }
                catch (Exception ex) { Console.WriteLine("Lỗi khởi tạo: " + ex.Message); }
                ctx.ExitThread();
            };
            start.Start();
            Application.Run(ctx);
            return exit;
        }

        static async Task<int> RunAll()
        {
            Console.WriteLine($"ServerPulse – kiểm thử tự động · API: {BaseUrl}\n");
            Admin = new ApiClient(BaseUrl);
            Viewer = new ApiClient(BaseUrl);
            Anon = new ApiClient(BaseUrl);

            // chờ API server khởi động (tối đa 90 giây) - cho phép bật app và chạy test cùng lúc
            for (int i = 0; ; i++)
            {
                try { await Anon.GetMeAsync(); }
                catch (ApiClientException ex) when (ex.StatusCode == 401) { break; } // API đã chạy (chưa đăng nhập nên 401)
                catch (ApiClientException) when (i < 90)
                {
                    if (i == 0) Console.WriteLine("Đang chờ API server khởi động...");
                    await Task.Delay(1000);
                    continue;
                }
                break;
            }
            await Admin.LoginAsync(DemoDataSeeder.AdminEmail, DemoDataSeeder.AdminPassword);
            await Viewer.LoginAsync(DemoDataSeeder.ViewerEmail, DemoDataSeeder.ViewerPassword);

            // dọn host test còn sót từ lần chạy trước
            foreach (var v in await Admin.GetHostsAsync("test-api-host")) await Admin.DeleteHostAsync(v.Host.Id);

            foreach (var c in Cases)
            {
                var sw = Stopwatch.StartNew();
                UiErrors.Clear();
                try
                {
                    await c.Run();
                    Application.DoEvents(); // xử lý nốt các lần vẽ còn chờ
                    if (UiErrors.Count > 0) throw new AssertException("lỗi giao diện: " + UiErrors[0].GetType().Name + ": " + UiErrors[0].Message);
                    c.Result = "PASS";
                }
                catch (AssertException ex) { c.Result = "FAIL"; c.Detail = ex.Message; }
                catch (Exception ex) { c.Result = "ERROR"; c.Detail = ex.GetType().Name + ": " + ex.Message + " @ " + string.Join(" <- ", (ex.StackTrace ?? "").Split('\n').Take(4).Select(l => l.Trim())); }
                Console.ForegroundColor = c.Result == "PASS" ? ConsoleColor.Green : ConsoleColor.Red;
                Console.Write($"{c.Result,-5} ");
                Console.ResetColor();
                Console.WriteLine($"{c.Code,-16} {c.Name} ({sw.ElapsedMilliseconds} ms) {c.Detail}");
            }

            var sb = new StringBuilder($"Kết quả kiểm thử ServerPulse – {DateTime.Now:dd/MM/yyyy HH:mm:ss} – API {BaseUrl}\r\n\r\n");
            foreach (var g in Cases.GroupBy(c => c.Group))
            {
                sb.Append($"== {g.Key}: {g.Count(c => c.Result == "PASS")}/{g.Count()} PASS\r\n");
                foreach (var c in g) sb.Append($"{c.Code,-16} {c.Result,-5} {c.Name} {c.Detail}\r\n");
                sb.Append("\r\n");
            }
            int failed = Cases.Count(c => c.Result != "PASS");
            var summary = $"Tổng: {Cases.Count} | PASS: {Cases.Count - failed} | FAIL/ERROR: {failed}";
            sb.Append(summary);
            File.WriteAllText("ket_qua_kiem_thu.txt", sb.ToString(), new UTF8Encoding(true));
            Console.WriteLine("\n" + summary + "\nĐã lưu: " + Path.GetFullPath("ket_qua_kiem_thu.txt"));
            return failed == 0 ? 0 : 1;
        }

        // ======================= Hàm kiểm tra =======================
        class AssertException : Exception { public AssertException(string m) : base(m) { } }

        static void Assert(bool ok, string message) { if (!ok) throw new AssertException(message); }

        static async Task ExpectStatus(int expected, Func<Task> action)
        {
            try { await action(); }
            catch (ApiClientException ex)
            {
                Assert(ex.StatusCode == expected, $"mong đợi HTTP {expected}, nhận {ex.StatusCode} ({ex.Message})");
                return;
            }
            throw new AssertException($"mong đợi HTTP {expected}, nhưng gọi thành công");
        }

        static void ExpectInvalid(Action action, string message)
        {
            try { action(); }
            catch (ValidationException ex) { Assert(ex.Message == message, $"thông báo '{ex.Message}' khác '{message}'"); return; }
            throw new AssertException("mong đợi báo lỗi: " + message);
        }

        //Lấy toàn bộ chữ của các Label trong một control (để kiểm tra giao diện hiển thị đúng số liệu)
        static string AllText(Control root) =>
            string.Join("|", Descendants(root).OfType<Label>().Select(l => l.Text));

        static IEnumerable<Control> Descendants(Control root)
        {
            foreach (Control c in root.Controls)
            {
                yield return c;
                foreach (var d in Descendants(c)) yield return d;
            }
        }

        //Chờ điều kiện đúng (vẫn xử lý sự kiện giao diện trong lúc chờ), quá 6 giây thì FAIL
        static async Task WaitFor(Func<Task<bool>> condition, string failMessage)
        {
            var sw = Stopwatch.StartNew();
            while (sw.ElapsedMilliseconds < 6000)
            {
                Application.DoEvents();
                if (await condition()) return;
                await Task.Delay(100);
            }
            throw new AssertException(failMessage);
        }

        static async Task<FormMain> OpenMain(ApiClient api)
        {
            var main = new FormMain(api) { StartPosition = FormStartPosition.Manual, Location = new System.Drawing.Point(-4000, 0), ShowInTaskbar = false };
            main.Show();
            await Task.Delay(300);
            return main;
        }

        static async Task<T> ShowPage<T>(FormMain main, T page) where T : PageBase
        {
            main.Navigate(page);
            await main.RefreshPageAsync(silent: false);
            Application.DoEvents();
            return page;
        }

        // ======================= Danh sách test case =======================
        static void Register()
        {
            const string AUTH = "Đăng nhập / phân quyền (API)", API = "API", ALERT = "Alert (API)", UILOGIC = "Logic giao diện", UI = "Giao diện WinForms";

            // ---------------- Đăng nhập / phân quyền ----------------
            T(AUTH, "TC-AUTH-01", "Đăng nhập đúng tài khoản Admin trả về token + role", async () =>
            {
                var c = new ApiClient(BaseUrl);
                var r = await c.LoginAsync(DemoDataSeeder.AdminEmail, DemoDataSeeder.AdminPassword);
                Assert(!string.IsNullOrEmpty(r.Token) && r.User.Role == UserRole.Admin, "không có token hoặc sai role");
            });
            T(AUTH, "TC-AUTH-02", "Sai mật khẩu -> 401", () => ExpectStatus(401, () => new ApiClient(BaseUrl).LoginAsync(DemoDataSeeder.AdminEmail, "sai-mat-khau")));
            T(AUTH, "TC-AUTH-03", "Email không tồn tại -> 401, cùng thông báo (không lộ tài khoản)", async () =>
            {
                try { await new ApiClient(BaseUrl).LoginAsync("khongco@x.vn", "x"); throw new AssertException("đăng nhập được"); }
                catch (ApiClientException ex) { Assert(ex.StatusCode == 401 && ex.Message == "Email hoặc mật khẩu không đúng", ex.Message); }
            });
            T(AUTH, "TC-AUTH-04", "Gọi API không có token -> 401", () => ExpectStatus(401, () => Anon.GetHostsAsync()));
            T(AUTH, "TC-AUTH-05", "Token giả -> 401", async () =>
            {
                using (var http = new HttpClient())
                {
                    http.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", "fake-token");
                    var res = await http.GetAsync(BaseUrl + "/api/v1/hosts");
                    Assert((int)res.StatusCode == 401, "HTTP " + (int)res.StatusCode);
                }
            });
            T(AUTH, "TC-AUTH-06", "Viewer xem được danh sách host", async () => Assert((await Viewer.GetHostsAsync()).Count > 0, "danh sách rỗng"));
            T(AUTH, "TC-AUTH-07", "Viewer không được thêm host -> 403", () => ExpectStatus(403, () => Viewer.CreateHostAsync(new ServerInfo { Name = "x", IpAddress = "1.1.1.1" })));
            T(AUTH, "TC-AUTH-08", "Viewer không được sửa ngưỡng -> 403", async () =>
            {
                var rule = (await Viewer.GetThresholdsAsync()).First();
                await ExpectStatus(403, () => Viewer.SaveThresholdAsync(rule));
            });
            T(AUTH, "TC-AUTH-09", "auth/me trả về thông tin phiên", async () => Assert((await Admin.GetMeAsync()).Email == DemoDataSeeder.AdminEmail, "sai email"));

            // ---------------- API Host / Metrics / Event ----------------
            T(API, "TC-API-01", "GET /hosts trả danh sách có status + latest metric", async () =>
            {
                var d = await Admin.GetHostsAsync();
                Assert(d.Count >= 9, "chỉ có " + d.Count + " host");
                Assert(d.All(v => new[] { "ONLINE", "WARNING", "CRITICAL", "OFFLINE" }.Contains(v.Status)), "status không hợp lệ");
            });
            T(API, "TC-API-02", "Lọc host theo trạng thái OFFLINE", async () =>
            {
                var d = await Admin.GetHostsAsync(status: "OFFLINE");
                Assert(d.Count > 0 && d.All(v => v.Status == "OFFLINE"), "lọc sai");
            });
            T(API, "TC-API-03", "Tìm host theo từ khóa 'db-prod'", async () => Assert((await Admin.GetHostsAsync("db-prod")).Count == 2, "không đúng 2 host"));
            T(API, "TC-API-04", "GET /hosts/{id} chi tiết host", async () =>
            {
                var id = (await Admin.GetHostsAsync()).First().Host.Id;
                Assert((await Admin.GetHostAsync(id)).Host.Id == id, "sai host");
            });
            T(API, "TC-API-05", "ID sai định dạng -> 400; ID không tồn tại -> 404", async () =>
            {
                await ExpectStatus(400, () => Admin.GetHostAsync("abc"));
                await ExpectStatus(404, () => Admin.GetHostAsync("000000000000000000000000"));
            });
            T(API, "TC-API-06", "Thêm / chống trùng / sửa host (Admin)", async () =>
            {
                var h = await Admin.CreateHostAsync(new ServerInfo { Name = "test-api-host", IpAddress = "10.99.0.1", OperatingSystem = "Ubuntu" });
                Assert(!string.IsNullOrEmpty(h.Id), "không có Id");
                await ExpectStatus(409, () => Admin.CreateHostAsync(new ServerInfo { Name = "test-api-host", IpAddress = "10.99.0.1" }));
                h.Description = "đã sửa";
                Assert((await Admin.UpdateHostAsync(h)).Description == "đã sửa", "sửa không lưu");
                Ctx["tmpHost"] = h.Id;
            });
            T(API, "TC-API-07", "Thêm host thiếu tên / IP sai -> 400", async () =>
            {
                await ExpectStatus(400, () => Admin.CreateHostAsync(new ServerInfo { Name = "", IpAddress = "10.0.0.1" }));
                await ExpectStatus(400, () => Admin.CreateHostAsync(new ServerInfo { Name = "abc", IpAddress = "999.1.1" }));
            });
            T(API, "TC-API-08", "POST /metrics/collector nhận dữ liệu Agent (không cần token)", async () =>
            {
                var r = await Anon.CollectAsync(new CollectorBody { ServerId = Ctx["tmpHost"], Cpu = 12.5, Ram = 40, Disk = 30, NetworkInKbps = 1000, NetworkOutKbps = 500 });
                Assert(r.Ok, "ok=false");
                var v = await Admin.GetHostAsync(Ctx["tmpHost"]);
                Assert(v.Status == "ONLINE" && v.Latest.CpuUsagePercent == 12.5, $"status={v.Status}, cpu={v.Latest?.CpuUsagePercent}");
            });
            T(API, "TC-API-09", "Collector: Host ID không tồn tại -> 404, giá trị % > 100 -> 400", async () =>
            {
                await ExpectStatus(404, () => Anon.CollectAsync(new CollectorBody { ServerId = "000000000000000000000000", Cpu = 1, Ram = 1, Disk = 1 }));
                await ExpectStatus(400, () => Anon.CollectAsync(new CollectorBody { ServerId = Ctx["tmpHost"], Cpu = 150, Ram = 1, Disk = 1 }));
            });
            T(API, "TC-API-10", "GET /metrics/history/{id} 1h/6h/24h/7d có points + summary", async () =>
            {
                var id = (await Admin.GetHostsAsync("web-prod-01")).First().Host.Id;
                foreach (var range in new[] { "1h", "6h", "24h", "7d" })
                {
                    var h = await Admin.GetHistoryAsync(id, range);
                    Assert(h.Points.Count > 0 && h.Points.Count <= 125, $"{range}: {h.Points.Count} điểm");
                    Assert(h.Summary.CpuPeak >= h.Summary.CpuAvg, range + ": peak < avg");
                }
            });
            T(API, "TC-API-11", "GET /overview trả thống kê + series 6 giờ (Aggregation)", async () =>
            {
                var o = await Admin.GetOverviewAsync();
                Assert(o.Total == o.Online + o.Offline, "total != online + offline");
                Assert(o.Series.Count >= 30 && o.AvgCpu >= 0 && o.AvgCpu <= 100, "series = " + o.Series.Count);
            });

            // ---------------- Alert ----------------
            T(ALERT, "TC-ALERT-01", "GET /alerts trả items + stats MTTA/MTTR", async () =>
            {
                var d = await Admin.GetAlertsAsync(hours: 24);
                Assert(d.Items.Count > 0 && d.Stats != null, "không có dữ liệu");
            });
            T(ALERT, "TC-ALERT-02", "Lọc alert theo severity=Critical, status=RESOLVED", async () =>
            {
                Assert((await Admin.GetAlertsAsync(severity: "Critical")).Items.All(a => a.Severity == AlertSeverity.Critical), "lọc mức độ sai");
                Assert((await Admin.GetAlertsAsync(status: "RESOLVED", hours: 168)).Items.All(a => a.Status == AlertStatus.Resolved), "lọc trạng thái sai");
            });
            T(ALERT, "TC-ALERT-03", "Alert Engine tự tạo cảnh báo khi Agent gửi CPU vượt ngưỡng, không tạo trùng", async () =>
            {
                await Admin.SaveThresholdAsync(new AlertRule { ServerId = Ctx["tmpHost"], Metric = MetricType.Cpu, Unit = "%", ThresholdValue = 50, CriticalValue = 90, DurationSeconds = 0, IsEnabled = true });
                await Anon.CollectAsync(new CollectorBody { ServerId = Ctx["tmpHost"], Cpu = 95, Ram = 10, Disk = 10 });
                await Anon.CollectAsync(new CollectorBody { ServerId = Ctx["tmpHost"], Cpu = 96, Ram = 10, Disk = 10 });
                var mine = (await Admin.GetAlertsAsync(q: "test-api-host")).Items.Where(a => a.ServerId == Ctx["tmpHost"] && a.Metric == MetricType.Cpu).ToList();
                Assert(mine.Count == 1, "số alert = " + mine.Count);
                Assert(mine[0].Severity == AlertSeverity.Critical && mine[0].Status == AlertStatus.Opening, $"{mine[0].Severity}/{mine[0].Status}");
                Ctx["tmpAlert"] = mine[0].Id;
            });
            T(ALERT, "TC-ALERT-04", "Viewer không được Acknowledge -> 403", () => ExpectStatus(403, () => Viewer.AcknowledgeAsync(Ctx["tmpAlert"])));
            T(ALERT, "TC-ALERT-05", "Quy trình OPENING -> ACKNOWLEDGED -> RESOLVED", async () =>
            {
                var a = await Admin.AcknowledgeAsync(Ctx["tmpAlert"]);
                Assert(a.Status == AlertStatus.Acknowledged && a.AcknowledgedBy == DemoDataSeeder.AdminEmail, a.Status);
                await ExpectStatus(409, () => Admin.AcknowledgeAsync(Ctx["tmpAlert"]));
                a = await Admin.ResolveAsync(Ctx["tmpAlert"]);
                Assert(a.Status == AlertStatus.Resolved && a.ResolvedAt.HasValue, a.Status);
                await ExpectStatus(409, () => Admin.ResolveAsync(Ctx["tmpAlert"]));
            });
            T(ALERT, "TC-ALERT-06", "Ngưỡng không hợp lệ (critical < warning, % > 100) -> 400", async () =>
            {
                await ExpectStatus(400, () => Admin.SaveThresholdAsync(new AlertRule { Metric = MetricType.Cpu, Unit = "%", ThresholdValue = 90, CriticalValue = 80 }));
                await ExpectStatus(400, () => Admin.SaveThresholdAsync(new AlertRule { Metric = MetricType.Cpu, Unit = "%", ThresholdValue = 90, CriticalValue = 120 }));
            });
            T(API, "TC-API-12", "GET /events tìm kiếm Event Log theo từ khóa và loại", async () =>
            {
                Assert((await Admin.GetEventsAsync("test-api-host")).Count > 0, "không có event của host test");
                Assert((await Admin.GetEventsAsync(type: "AUTH")).All(e => e.Type == "AUTH"), "lọc loại sai");
            });

            // ---------------- Logic giao diện ----------------
            T(UILOGIC, "TC-UI-LOGIN-01", "Form đăng nhập: email sai định dạng", () =>
            { Assert(FormLogin.ValidateInput("abc", "x") == "Email không hợp lệ", "không báo lỗi"); return Task.CompletedTask; });
            T(UILOGIC, "TC-UI-LOGIN-02", "Form đăng nhập: bỏ trống mật khẩu", () =>
            { Assert(FormLogin.ValidateInput("admin@serverpulse.vn", "") == "Vui lòng nhập mật khẩu", "không báo lỗi"); return Task.CompletedTask; });
            T(UILOGIC, "TC-UI-LOGIN-03", "Form đăng nhập: dữ liệu hợp lệ không báo lỗi", () =>
            { Assert(FormLogin.ValidateInput("admin@serverpulse.vn", "x") == null, "báo lỗi nhầm"); return Task.CompletedTask; });
            T(UILOGIC, "TC-UI-HOST-01", "Form máy chủ: thiếu hostname / IP sai", () =>
            {
                ExpectInvalid(() => HostDialog.Validate("", "10.0.0.1"), "Hostname là bắt buộc");
                ExpectInvalid(() => HostDialog.Validate("abc", "300.1"), "Địa chỉ IP không hợp lệ");
                ExpectInvalid(() => HostDialog.Validate("abc", "10.0.1"), "Địa chỉ IP không hợp lệ");
                HostDialog.Validate("abc", "10.0.0.1");
                return Task.CompletedTask;
            });
            T(UILOGIC, "TC-UI-RULE-01", "Form ngưỡng: critical < warning, % > 100, ngưỡng ≤ 0", () =>
            {
                ExpectInvalid(() => RuleDialog.Validate(90, 80, "%", 300), "Ngưỡng Critical phải ≥ ngưỡng Warning");
                ExpectInvalid(() => RuleDialog.Validate(90, 120, "%", 300), "Ngưỡng % không được vượt 100");
                ExpectInvalid(() => RuleDialog.Validate(0, 10, "%", 300), "Ngưỡng phải lớn hơn 0");
                RuleDialog.Validate(2000000, 2500000, "Kbps", 120);
                return Task.CompletedTask;
            });
            T(UILOGIC, "TC-UI-FMT-01", "Định dạng hiển thị: %, Mbps/Gbps, thời lượng, 'x giây trước'", () =>
            {
                Assert(Theme.Pct(34.4) == "34%" && Theme.N1(61.37) == "61,4", Theme.Pct(34.4) + " " + Theme.N1(61.37));
                Assert(Theme.Mbps(412000) == "412 Mbps" && Theme.Mbps(2400000) == "2,4 Gbps", Theme.Mbps(412000) + " " + Theme.Mbps(2400000));
                Assert(Theme.Dur(222) == "3m42s" && Theme.Dur(45) == "45s", Theme.Dur(222));
                Assert(Theme.Ago(DateTime.UtcNow.AddSeconds(-5)) == "5 giây trước" && Theme.Ago(null) == "chưa có dữ liệu", Theme.Ago(DateTime.UtcNow.AddSeconds(-5)));
                return Task.CompletedTask;
            });
            T(UILOGIC, "TC-UI-FMT-02", "Màu theo trạng thái và theo ngưỡng", () =>
            {
                Assert(Theme.StatusColor("ONLINE") == Theme.Green && Theme.StatusColor("CRITICAL") == Theme.Red && Theme.StatusColor("ACKNOWLEDGED") == Theme.Amber && Theme.StatusColor("OFFLINE") == Theme.Gray, "màu trạng thái sai");
                Assert(Theme.LevelColor(95, 80, 90) == Theme.Red && Theme.LevelColor(85, 80, 90) == Theme.Amber && Theme.LevelColor(50, 80, 90) == Theme.Text, "màu ngưỡng sai");
                return Task.CompletedTask;
            });
            T(UILOGIC, "TC-UI-FONT-01", "Vẽ nhãn trạng thái / thanh tài nguyên không làm hỏng font dùng chung", () =>
            {
                using (var bmp = new System.Drawing.Bitmap(300, 60))
                using (var g = System.Drawing.Graphics.FromImage(bmp))
                    Nhom3_NoSQL.Frontend.UI.Draw.Pill(g, new System.Drawing.Point(0, 0), "ONLINE", Theme.Green, Theme.Card);
                using (var bar = new BarRow { Width = 300, Height = 46, Caption = "CPU", ValueText = "50%", Percent = 50, BarColor = Theme.Cyan })
                using (var bmp = new System.Drawing.Bitmap(300, 46))
                    bar.DrawToBitmap(bmp, new System.Drawing.Rectangle(0, 0, 300, 46));
                // font đã bị Dispose thì GetHeight() ném ArgumentException
                Theme.Mono(8, System.Drawing.FontStyle.Bold).GetHeight();
                Theme.Mono(10, System.Drawing.FontStyle.Bold).GetHeight();
                return Task.CompletedTask;
            });
            T(UILOGIC, "TC-UI-CSV-01", "Xuất CSV: tiêu đề, số dòng, escape dấu ngoặc kép", async () =>
            {
                var hosts = await Admin.GetHostsAsync();
                var csv = HostsPage.BuildCsv(hosts.Concat(new[] { new HostView { Host = new ServerInfo { Name = "a\"b", IpAddress = "1.1.1.1" }, Status = "OFFLINE" } }));
                var lines = csv.TrimEnd().Split(new[] { "\r\n" }, StringSplitOptions.None);
                Assert(lines[0].StartsWith("hostname,ip,status"), "sai tiêu đề");
                Assert(lines.Length == hosts.Count + 2, "số dòng " + lines.Length);
                Assert(lines.Last().StartsWith("\"a\"\"b\""), "escape sai: " + lines.Last());
            });

            // ---------------- Giao diện WinForms (mở form thật, gọi API thật) ----------------
            T(UI, "TC-UI-LOGIN-04", "Màn hình đăng nhập: sai mật khẩu hiện thông báo lỗi", async () =>
            {
                using (var f = new FormLogin(new ApiClient(BaseUrl)) { StartPosition = FormStartPosition.Manual, Location = new System.Drawing.Point(-4000, 0), ShowInTaskbar = false })
                {
                    f.Show();
                    f.Prefill(DemoDataSeeder.AdminEmail, "sai");
                    await f.DoLogin();
                    Assert(f.ErrorText == "Email hoặc mật khẩu không đúng" && f.DialogResult != DialogResult.OK, "lỗi: " + f.ErrorText);
                }
            });
            T(UI, "TC-UI-LOGIN-05", "Màn hình đăng nhập: đúng tài khoản thì đóng form với OK", async () =>
            {
                var api = new ApiClient(BaseUrl);
                using (var f = new FormLogin(api) { StartPosition = FormStartPosition.Manual, Location = new System.Drawing.Point(-4000, 0), ShowInTaskbar = false })
                {
                    f.Show();
                    f.Prefill(DemoDataSeeder.AdminEmail, DemoDataSeeder.AdminPassword);
                    await f.DoLogin();
                    Assert(f.DialogResult == DialogResult.OK && api.IsAdmin, "không đăng nhập được: " + f.ErrorText);
                }
            });
            T(UI, "TC-UI-DASH-01", "Tổng quan: thẻ thống kê khớp API, biểu đồ có 2 series", async () =>
            {
                using (var main = await OpenMain(Admin))
                {
                    var page = await ShowPage(main, new OverviewPage());
                    var o = await Admin.GetOverviewAsync();
                    var text = AllText(page);
                    Assert(text.Contains("|" + o.Total + "|") && text.Contains(o.Offline + " host mất kết nối"), "thẻ không khớp: " + text.Substring(0, Math.Min(200, text.Length)));
                    var chart = Descendants(page).OfType<Chart>().Single();
                    Assert(chart.Series.Count == 2 && chart.Series.All(s => s.Points.Count >= 30), "series: " + string.Join(",", chart.Series.Select(s => s.Points.Count)));
                }
            });
            T(UI, "TC-UI-DASH-02", "Máy chủ: bảng hiển thị 9 dòng/trang, lọc OFFLINE đúng", async () =>
            {
                using (var main = await OpenMain(Admin))
                {
                    var page = await ShowPage(main, new HostsPage());
                    var grid = Descendants(page).OfType<DataGridView>().Single();
                    var total = (await Admin.GetHostsAsync()).Count;
                    Assert(grid.Rows.Count == Math.Min(9, total), "số dòng " + grid.Rows.Count);
                    var status = Descendants(page).OfType<ComboBox>().First(c => c.Items.Contains("OFFLINE"));
                    status.SelectedItem = "OFFLINE";
                    Application.DoEvents();
                    Assert(grid.Rows.Count > 0 && grid.Rows.Cast<DataGridViewRow>().All(r => ((HostView)r.Tag).Status == "OFFLINE"), "lọc sai");
                }
            });
            T(UI, "TC-UI-CHART-01", "Chi tiết host: 4 biểu đồ có dữ liệu, trục % tối đa 100", async () =>
            {
                var id = (await Admin.GetHostsAsync("web-prod-01")).First().Host.Id;
                using (var main = await OpenMain(Admin))
                {
                    var page = await ShowPage(main, new HostDetailPage(id));
                    var charts = Descendants(page).OfType<Chart>().ToList();
                    Assert(charts.Count == 4, "số biểu đồ " + charts.Count);
                    Assert(charts.All(c => c.Series[0].Points.Count > 10), "thiếu dữ liệu");
                    Assert(charts.Take(3).All(c => c.ChartAreas[0].AxisY.Maximum == 100), "trục Y sai");
                    Assert(AllText(page).Contains("web-prod-01"), "không hiện tên host");
                }
            });
            T(UI, "TC-UI-ALERT-01", "Sự cố: danh sách khớp API, Viewer bị khóa nút Acknowledge/Resolve", async () =>
            {
                using (var main = await OpenMain(Viewer))
                {
                    var page = await ShowPage(main, new IncidentsPage());
                    var api = await Viewer.GetAlertsAsync("ACTIVE");
                    var text = AllText(page);
                    Assert(text.Contains(api.Items.Count + " KẾT QUẢ"), "số kết quả không khớp");
                    var buttons = Descendants(page).OfType<RoundButton>().Where(b => b.Text == "Acknowledge" || b.Text == "Resolve").ToList();
                    Assert(buttons.Count == 2 && buttons.All(b => !b.Enabled), "Viewer vẫn bấm được");
                    Assert(text.Contains("Tài khoản Viewer chỉ được xem"), "thiếu ghi chú quyền");
                }
            });
            T(UI, "TC-UI-ROLE-01", "Viewer: không có nút Thêm máy chủ / Tạo quy tắc", async () =>
            {
                using (var main = await OpenMain(Viewer))
                {
                    await ShowPage(main, new HostsPage());
                    Assert(!Descendants(main).OfType<RoundButton>().Any(b => b.Text == "Thêm máy chủ"), "có nút Thêm máy chủ");
                    await ShowPage(main, new ThresholdsPage());
                    Assert(!Descendants(main).OfType<RoundButton>().Any(b => b.Text == "Tạo quy tắc"), "có nút Tạo quy tắc");
                }
            });
            T(UI, "TC-UI-NAV-01", "Chuyển qua tất cả các trang không lỗi", async () =>
            {
                using (var main = await OpenMain(Admin))
                {
                    await ShowPage(main, new OverviewPage());
                    await ShowPage(main, new HostsPage());
                    await ShowPage(main, new ThresholdsPage());
                    await ShowPage(main, new IncidentsPage());
                    var ev = await ShowPage(main, new EventsPage("ALERT"));
                    var grid = Descendants(ev).OfType<DataGridView>().Single();
                    Assert(grid.Rows.Count > 0 && grid.Rows.Cast<DataGridViewRow>().All(r => ((Nhom3_NoSQL.Backend.Models.EventLog)r.Tag).Type == "ALERT"), "Event Log lọc sai");
                }
            });

            T(UI, "TC-UI-ALERT-02", "Sự cố: bấm Acknowledge rồi Resolve trên giao diện, trạng thái lưu vào DB", async () =>
            {
                var tmp = Ctx["tmpHost"];
                await Admin.SaveThresholdAsync(new AlertRule { ServerId = tmp, Metric = MetricType.Ram, Unit = "%", ThresholdValue = 50, CriticalValue = 90, DurationSeconds = 0, IsEnabled = true });
                await Anon.CollectAsync(new CollectorBody { ServerId = tmp, Cpu = 10, Ram = 99, Disk = 10 });
                var alert = (await Admin.GetAlertsAsync(AlertStatus.Opening, q: "test-api-host")).Items.First(a => a.ServerId == tmp && a.Metric == MetricType.Ram);
                using (var main = await OpenMain(Admin))
                {
                    var page = await ShowPage(main, new IncidentsPage());
                    // bộ lọc mặc định "OPENING + ACK" sẽ ẩn sự cố sau khi Resolve -> chọn "Tất cả trạng thái" để theo dõi đến cuối
                    Descendants(page).OfType<ComboBox>().First(c => c.Items.Contains("Tất cả trạng thái")).SelectedItem = "Tất cả trạng thái";
                    Descendants(page).OfType<TextBox>().First().Text = "test-api-host"; // ô tìm kiếm
                    await WaitFor(async () => { await Task.Yield(); return AllText(page).Contains(alert.Code); }, "chi tiết không hiện " + alert.Code);

                    Button Btn(string text) => Descendants(page).OfType<RoundButton>().FirstOrDefault(b => b.Text == text && b.Enabled);
                    Assert(Btn("Acknowledge") != null, "nút Acknowledge bị khóa");
                    Btn("Acknowledge").PerformClick();
                    await WaitFor(async () => (await Admin.GetAlertAsync(alert.Id)).Status == AlertStatus.Acknowledged, "chưa chuyển ACKNOWLEDGED");
                    await WaitFor(async () => { await Task.Yield(); return Btn("Resolve") != null && Btn("Acknowledge") == null; }, "giao diện chưa cập nhật sau Acknowledge");
                    Btn("Resolve").PerformClick();
                    await WaitFor(async () => (await Admin.GetAlertAsync(alert.Id)).Status == AlertStatus.Resolved, "chưa chuyển RESOLVED");
                    await WaitFor(async () => { await Task.Yield(); return AllText(page).Contains("Sự cố đã được xử lý"); }, "không hiện 'Sự cố đã được xử lý'");
                }
            });

            // ---------------- Dọn dữ liệu ----------------
            T(API, "TC-API-13", "Xóa host test: host biến mất, sự cố của host được đóng", async () =>
            {
                await Admin.DeleteHostAsync(Ctx["tmpHost"]);
                await ExpectStatus(404, () => Admin.GetHostAsync(Ctx["tmpHost"]));
                Assert(!(await Admin.GetAlertsAsync("ACTIVE")).Items.Any(a => a.ServerId == Ctx["tmpHost"]), "còn sự cố mở");
            });
            T(AUTH, "TC-AUTH-10", "Đăng xuất làm token hết hiệu lực", async () =>
            {
                var c = new ApiClient(BaseUrl);
                await c.LoginAsync(DemoDataSeeder.AdminEmail, DemoDataSeeder.AdminPassword);
                var token = c.Token;
                await c.LogoutAsync();
                using (var http = new HttpClient())
                {
                    http.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
                    Assert((int)(await http.GetAsync(BaseUrl + "/api/v1/hosts")).StatusCode == 401, "token cũ vẫn dùng được");
                }
            });
        }
    }
}
