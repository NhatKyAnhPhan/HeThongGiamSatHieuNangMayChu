using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;
using MongoDB.Driver;
using Nhom3_NoSQL.Backend.Api;
using Nhom3_NoSQL.Backend.Interfaces;
using Nhom3_NoSQL.Backend.Models;
using Nhom3_NoSQL.Backend.Services;
using Nhom3_NoSQL.Database;
using Nhom3_NoSQL.Frontend;

namespace Nhom3_NoSQL
{
    internal static class Program
    {
        public static MongoDbContext MongoContext { get; private set; }
        public static ICacheService CacheService { get; private set; }
        public static ISearchService SearchService { get; private set; }
        public static IServerRepository ServerRepo { get; private set; }
        public static IMetricRepository MetricRepo { get; private set; }
        public static IAlertRepository AlertRepo { get; private set; }
        public static IEventRepository EventRepo { get; private set; }
        public static IUserRepository UserRepo { get; private set; }
        public static INotificationService NotificationService { get; private set; }
        public static AlertEngineService AlertEngine { get; private set; }
        public static MetricCollectorService MetricCollector { get; private set; }
        public static AuthService Auth { get; private set; }
        public static ApiServer Api { get; private set; }
        public static DemoDataSeeder Seeder { get; private set; }

        public const string ApiUrl = "http://localhost:5080/";

        [STAThread]
        static void Main(string[] args)
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            // Lỗi chưa bắt khi đang chạy (vd trong handler async của nút) -> ghi error.log và báo lỗi, không bị "nuốt" im lặng
            Application.SetUnhandledExceptionMode(UnhandledExceptionMode.CatchException);
            Application.ThreadException += (s, e) => ReportError(e.Exception);
            AppDomain.CurrentDomain.UnhandledException += (s, e) => ReportError(e.ExceptionObject as Exception);
            try
            {
                // Đã có ServerPulse khác chạy (cổng 5080 đang dùng) -> chỉ mở giao diện, kết nối vào server đó
                if (!args.Contains("--headless") && !args.Contains("--screenshots") && ApiAlreadyRunning())
                {
                    RunUi(new ApiClient(ApiUrl));
                    return;
                }
                Run(args);
            }
            catch (Exception ex)
            {
                // Ghi lỗi khởi động ra file cạnh .exe để dễ chẩn đoán
                var log = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "startup-error.log");
                File.WriteAllText(log, DateTime.Now + Environment.NewLine + ex);
                MessageBox.Show(ex.Message + "\n\nChi tiết: " + log, "ServerPulse - lỗi khởi động", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        static bool ApiAlreadyRunning()
        {
            try
            {
                using (var tcp = new System.Net.Sockets.TcpClient())
                    return tcp.ConnectAsync("localhost", 5080).Wait(500) && tcp.Connected;
            }
            catch { return false; }
        }

        static void ReportError(Exception ex)
        {
            if (ex == null) return;
            var log = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "error.log");
            try { File.AppendAllText(log, DateTime.Now + Environment.NewLine + ex + Environment.NewLine + Environment.NewLine); } catch { }
            MessageBox.Show(ex.Message + "\n\nChi tiết: " + log, "ServerPulse - lỗi", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }

        static void Run(string[] args)
        {
            //Có thể thay thế chuỗi kết nối trực tiếp hoặc đọc từ file cấu hình App.config
            string mongoConn = "mongodb://localhost:27017/?serverSelectionTimeoutMS=5000";
            string mongoDbName = "Nhom3_NoSQL_Db";
            string redisConn = "localhost:6379,abortConnect=false,connectTimeout=2000";
            string elasticUri = "http://localhost:9200";

            MongoContext = new MongoDbContext(mongoConn, mongoDbName);
            try { MongoContext.EnsureIndexes(); }
            catch (TimeoutException ex)
            {
                throw new Exception("Không kết nối được MongoDB tại localhost:27017. Hãy khởi động MongoDB rồi chạy lại.", ex);
            }

            // Redis và Elasticsearch là tùy chọn: không chạy thì hệ thống vẫn hoạt động với MongoDB
            try
            {
                var redis = new RedisCacheService(redisConn);
                CacheService = redis.IsConnected ? redis : null;
            }
            catch { CacheService = null; }
            SearchService = new ElasticSearchService(elasticUri);

            ServerRepo = new MongoServerRepository(MongoContext);
            MetricRepo = new MongoMetricRepository(MongoContext);
            AlertRepo = new MongoAlertRepository(MongoContext);
            EventRepo = new MongoEventRepository(MongoContext);
            UserRepo = new MongoUserRepository(MongoContext);
            NotificationService = new NotificationService();

            AlertEngine = new AlertEngineService(AlertRepo, MetricRepo, EventRepo, SearchService, NotificationService, CacheService);
            MetricCollector = new MetricCollectorService(CacheService, MetricRepo, AlertEngine);
            Auth = new AuthService(UserRepo);

            // Dữ liệu mẫu + mô phỏng Agent cho các host demo
            Seeder = new DemoDataSeeder(MongoContext, AlertEngine);
            Seeder.SeedAsync().GetAwaiter().GetResult();
            Seeder.StartSimulation();

            // Agent thật cho chính máy đang chạy app (chu kỳ 5 giây)
            var localName = Environment.MachineName.ToLowerInvariant();
            var local = MongoContext.Servers.Find(s => s.Name == localName).FirstOrDefault();
            if (local != null) MetricCollector.Start(local.Id, local.Name, TimeSpan.FromSeconds(5));

            // REST API - giao diện WinForms và Agent đều giao tiếp qua đây
            Api = new ApiServer(ApiUrl, MongoContext, ServerRepo, MetricRepo, AlertRepo, EventRepo, Auth, AlertEngine);
            Api.Start();

            var shot = Array.IndexOf(args, "--screenshots");
            if (args.Contains("--headless"))
                Application.Run(); // chỉ chạy API server + Collector, không mở giao diện (dừng bằng Task Manager)
            else if (shot >= 0)
                ScreenshotRunner.Run(args.Length > shot + 1 ? args[shot + 1] : "screenshots"); // tự chụp ảnh các màn hình cho báo cáo
            else
                RunUi(new ApiClient(ApiUrl));
            Api.Stop();
            MetricCollector.Dispose();
        }

        //Đăng nhập -> màn hình chính; đăng xuất thì quay lại màn hình đăng nhập
        static void RunUi(ApiClient client)
        {
            while (true)
            {
                using (var login = new FormLogin(client))
                    if (login.ShowDialog() != DialogResult.OK) return;
                var main = new FormMain(client);
                Application.Run(main);
                if (!main.LoggedOut) return;
            }
        }
    }
}
