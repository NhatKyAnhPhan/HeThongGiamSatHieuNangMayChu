using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;
using Nhom3_NoSQL.Backend.Api;
using Nhom3_NoSQL.Database;
using Nhom3_NoSQL.Frontend.Pages;

namespace Nhom3_NoSQL.Frontend
{
    //Chế độ --screenshots <thư mục>: mở lần lượt các màn hình và lưu ảnh PNG để đưa vào báo cáo
    public static class ScreenshotRunner
    {
        public static void Run(string dir)
        {
            Directory.CreateDirectory(dir);
            var ctx = new ApplicationContext();
            var start = new Timer { Interval = 200 };
            start.Tick += async (s, e) =>
            {
                start.Stop();
                try { await CaptureAll(dir); }
                catch (Exception ex) { File.WriteAllText(Path.Combine(dir, "screenshot-error.log"), ex.ToString()); }
                ctx.ExitThread();
            };
            start.Start();
            Application.Run(ctx);
        }

        static async Task CaptureAll(string dir)
        {
            var api = new ApiClient(Program.ApiUrl);
            await Task.Delay(8000); // chờ bộ mô phỏng + Alert Engine chạy vài chu kỳ

            // Đăng nhập
            using (var login = new FormLogin(api))
            {
                Place(login);
                login.Show();
                await Settle();
                Save(login, dir, "01_dang_nhap");
                login.Prefill(DemoDataSeeder.AdminEmail, "sai-mat-khau");
                await login.DoLogin();
                await Settle();
                Save(login, dir, "01b_dang_nhap_sai");
                login.Prefill(DemoDataSeeder.AdminEmail, DemoDataSeeder.AdminPassword);
                await login.DoLogin();
            }

            using (var main = new FormMain(api))
            {
                Place(main);
                main.Show();
                await Settle(2500);
                Save(main, dir, "02_tong_quan");

                await Page(main, new HostsPage(), dir, "03_may_chu");
                using (var dlg = new HostDialog(api, null)) { await ShowDialogShot(main, dlg, dir, "03c_them_may_chu"); }

                var web = (await api.GetHostsAsync("web-prod-01")).First().Host;
                await Page(main, new HostDetailPage(web.Id), dir, "04_chi_tiet_may_chu");

                await Page(main, new ThresholdsPage(), dir, "05_nguong_canh_bao");
                var rule = (await api.GetThresholdsAsync()).First();
                var hosts = await api.GetHostsAsync();
                using (var dlg = new RuleDialog(api, rule, hosts)) { await ShowDialogShot(main, dlg, dir, "05b_sua_nguong"); }

                await Page(main, new IncidentsPage(), dir, "06_su_co");
                await Page(main, new EventsPage(), dir, "07_nhat_ky_su_kien");
                main.Close();
            }

            // Phân quyền: tài khoản Viewer
            await api.LogoutAsync();
            await api.LoginAsync(DemoDataSeeder.ViewerEmail, DemoDataSeeder.ViewerPassword);
            using (var main = new FormMain(api))
            {
                Place(main);
                main.Show();
                await Settle(1500);
                await Page(main, new IncidentsPage(), dir, "08_phan_quyen_viewer");
                main.Close();
            }
            await api.LogoutAsync();
        }

        //Đặt cửa sổ ngoài vùng màn hình để không làm phiền người dùng khi chụp
        static void Place(Form f)
        {
            f.StartPosition = FormStartPosition.Manual;
            f.Location = new Point(-4000, 0);
            f.ShowInTaskbar = false;
        }

        static async Task Page(FormMain main, PageBase page, string dir, string name)
        {
            main.Navigate(page);
            await main.RefreshPageAsync(silent: false);
            await Settle(1200);
            Save(main, dir, name);
        }

        static async Task ShowDialogShot(Form owner, Form dlg, string dir, string name)
        {
            dlg.StartPosition = FormStartPosition.Manual;
            dlg.Location = new Point(-4000, 0);
            dlg.Show(owner);
            await Settle(600);
            Save(dlg, dir, name);
            dlg.Close();
        }

        static async Task Settle(int ms = 800)
        {
            await Task.Delay(ms);
            Application.DoEvents();
        }

        static void Save(Form f, string dir, string name)
        {
            using (var bmp = new Bitmap(f.Width, f.Height))
            {
                f.DrawToBitmap(bmp, new Rectangle(0, 0, f.Width, f.Height));
                bmp.Save(Path.Combine(dir, name + ".png"), ImageFormat.Png);
            }
        }
    }
}
