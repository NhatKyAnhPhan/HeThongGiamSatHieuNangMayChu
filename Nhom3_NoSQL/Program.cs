using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;
using Nhom3_NoSQL.Backend.Interfaces;
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
        public static IMetricRepository MetricRepo { get; private set; }
        public static IAlertRepository AlertRepo { get; private set; }
        public static INotificationService NotificationService { get; private set; }
        public static AlertEngineService AlertEngine { get; private set; }
        public static MetricCollectorService MetricCollector { get; private set; }

        [STAThread]
        static void Main()
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);

            //Có thể thay thế chuỗi kết nối trực tiếp hoặc đọc từ file cấu hình App.config
            string mongoConn = "mongodb://localhost:27017";
            string mongoDbName = "Nhom3_NoSQL_Db";
            string redisConn = "localhost:6379";
            string elasticUri = "http://localhost:9200";

            MongoContext = new MongoDbContext(mongoConn, mongoDbName);
            CacheService = new RedisCacheService(redisConn);
            SearchService = new ElasticSearchService(elasticUri);

            MetricRepo = new MongoMetricRepository(MongoContext);
            AlertRepo = new MongoAlertRepository(MongoContext);
            NotificationService = new NotificationService();

            AlertEngine = new AlertEngineService(AlertRepo, SearchService, NotificationService, CacheService);
            MetricCollector = new MetricCollectorService(CacheService, MetricRepo, AlertEngine);

            // Khởi chạy giao diện Dashboard chính
            Application.Run(new FormDashboard());
        }
    }
}