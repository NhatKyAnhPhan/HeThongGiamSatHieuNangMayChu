using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Windows.Forms.DataVisualization.Charting;
using Nhom3_NoSQL.Backend.Api;
using Nhom3_NoSQL.Backend.Models;
using Nhom3_NoSQL.Frontend;
using Nhom3_NoSQL.Frontend.Pages;
using Nhom3_NoSQL.Frontend.UI;
using Nhom3_NoSQL.Database;

// Lưu ý: tên gõ bằng phím tránh các phím dấu Telex (s f r x j w, nguyên âm đôi) vì máy có thể bật UniKey
// Chạy các test thủ công TC-MAN-07..12 bằng chuột/bàn phím thật của hệ điều hành trên các form thật của app
namespace Nhom3_NoSQL.Tests
{
//Chạy: Nhom3_NoSQL.Tests.exe --thu-cong  (cần app/API đang chạy). Ảnh + kết quả lưu vào Nhom3_NoSQL/Docs/screenshots/kiem_thu_thu_cong
static class ManualUi
{
    [DllImport("user32.dll")] static extern bool SetCursorPos(int x, int y);
    [DllImport("user32.dll")] static extern void mouse_event(uint f, uint x, uint y, uint d, UIntPtr e);
    [DllImport("user32.dll")] static extern bool SetProcessDPIAware();

    static readonly string Shots = Path.GetFullPath(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, @"..\..\..\..\Nhom3_NoSQL\Docs\screenshots\kiem_thu_thu_cong"));
    static readonly List<string> Log = new List<string>();
    static readonly List<Exception> UiErrors = new List<Exception>();
    static ApiClient Api;

    static IEnumerable<Control> All(Control r) { foreach (Control c in r.Controls) { yield return c; foreach (var d in All(c)) yield return d; } }

    static async Task Click(Control c, Point? offset = null, int wait = 600)
    {
        var p = c.PointToScreen(offset ?? new Point(c.Width / 2, c.Height / 2));
        SetCursorPos(p.X, p.Y);
        await Task.Delay(80);
        mouse_event(0x2, 0, 0, 0, UIntPtr.Zero);
        await Task.Delay(60);
        mouse_event(0x4, 0, 0, 0, UIntPtr.Zero);
        await Task.Delay(wait);
    }

    static async Task Type(Control box, string text)
    {
        await Click(box, null, 200);
        SendKeys.SendWait("^a");
        SendKeys.SendWait("{BACKSPACE}");
        SendKeys.SendWait(text);
        await Task.Delay(300);
    }

    static void Shot(Form f, string name)
    {
        using (var bmp = new Bitmap(f.Width, f.Height))
        {
            using (var g = Graphics.FromImage(bmp)) g.CopyFromScreen(f.Location, Point.Empty, f.Size);
            bmp.Save(Path.Combine(Shots, name + ".png"), ImageFormat.Png);
        }
    }

    static T Dlg<T>() where T : Form => Application.OpenForms.OfType<T>().FirstOrDefault();

    //Bấm nút để mở hộp thoại; cửa sổ vừa hiện đôi khi chưa nhận lần bấm đầu -> thử lại tối đa 3 lần
    static async Task<T> OpenDialog<T>(Form owner, string button) where T : Form
    {
        for (int i = 0; i < 3 && Dlg<T>() == null; i++)
        {
            owner.Activate();
            await Click(Btn(owner, button), wait: 1000);
        }
        return Dlg<T>();
    }
    static Button Btn(Control root, string text) => All(root).OfType<Button>().FirstOrDefault(b => b.Text == text);
    static List<InputBox> Inputs(Control root) => All(root).OfType<InputBox>().ToList();
    static string ErrorText(Form f) => string.Join(" ", All(f).OfType<Label>().Where(l => l.ForeColor == Theme.Red && l.Text != "").Select(l => l.Text));

    static async Task<bool> WaitFor(Func<bool> cond, int ms = 6000)
    {
        var end = DateTime.Now.AddMilliseconds(ms);
        while (DateTime.Now < end) { if (cond()) return true; await Task.Delay(150); }
        return cond();
    }

    static void Result(string code, bool ok, string detail)
    {
        if (UiErrors.Count > 0) { ok = false; detail += " | lỗi giao diện: " + UiErrors[0].Message; UiErrors.Clear(); }
        Log.Add($"{code}  {(ok ? "PASS" : "FAIL")}  {detail}");
    }

    public static int Run()
    {
        SetProcessDPIAware();
        Application.ThreadException += (s, e) => UiErrors.Add(e.Exception);
        Directory.CreateDirectory(Shots);
        var ctx = new ApplicationContext();
        var t = new Timer { Interval = 100 };
        t.Tick += async (s, e) =>
        {
            t.Stop();
            try { await RunAll(); }
            catch (Exception ex) { Log.Add("ERROR " + ex); }
            File.WriteAllLines(Path.Combine(Shots, "ket_qua_thu_cong.txt"), Log);
            foreach (var line in Log) Console.WriteLine(line);
            Console.WriteLine("Đã lưu ảnh và kết quả vào: " + Shots);
            ctx.ExitThread();
        };
        t.Start();
        Application.Run(ctx);
        return Log.Any(l => !l.Contains("  PASS  ")) ? 1 : 0;
    }

    static async Task RunAll()
    {
        Api = new ApiClient("http://localhost:5080");
        await Api.LoginAsync(DemoDataSeeder.AdminEmail, DemoDataSeeder.AdminPassword);
        foreach (var v in await Api.GetHostsAsync("man-qa-node")) await Api.DeleteHostAsync(v.Host.Id);

        var main = new FormMain(Api) { StartPosition = FormStartPosition.Manual, Location = new Point(0, 0), TopMost = true };
        main.Show();
        main.Activate();
        await Task.Delay(4000);

        // ---------------- TC-MAN-07: thêm / sửa / xóa host ----------------
        {
            main.Navigate(new HostsPage());
            await Task.Delay(1500);
            var dlg = await OpenDialog<HostDialog>(main, "Thêm máy chủ");
            bool opened = dlg != null;
            string err1 = "", err2 = "";
            bool added = false, edited = false, deleted = false;
            if (opened)
            {
                var f = Inputs(dlg);
                await Type(f[0].Box, "man-qa-node");
                await Type(f[1].Box, "300.1");
                await Type(f[2].Box, "Ubuntu");
                Shot(dlg, "man07a_ip_sai");
                await Click(Btn(dlg, "Lưu"));
                err1 = ErrorText(dlg);
                await Type(f[1].Box, "10.0.0.99");
                await Click(Btn(dlg, "Lưu"), wait: 1500);
                added = Dlg<HostDialog>() == null && (await Api.GetHostsAsync("man-qa-node")).Count == 1;
            }
            // sửa: lọc bảng theo tên rồi bấm biểu tượng bút
            var page = (HostsPage)All(main).OfType<HostsPage>().First();
            await Type(All(page).OfType<InputBox>().First().Box, "man-qa");
            await Task.Delay(800);
            var grid = All(page).OfType<DataGridView>().First();
            var actCol = grid.Columns["act"].Index;
            if (grid.Rows.Count == 1)
            {
                var cell = grid.GetCellDisplayRectangle(actCol, 0, false);
                await Click(grid, new Point(cell.X + 18, cell.Y + cell.Height / 2));
                var edit = Dlg<HostDialog>();
                if (edit != null)
                {
                    await Type(Inputs(edit)[4].Box, "QA edit");
                    await Click(Btn(edit, "Lưu"), wait: 1500);
                    var h = (await Api.GetHostsAsync("man-qa-node")).FirstOrDefault();
                    edited = h != null && h.Host.Description == "QA edit";
                }
                Shot(main, "man07b_sau_khi_sua");
                // xóa: bấm thùng rác -> hộp xác nhận -> Enter (Yes)
                cell = grid.GetCellDisplayRectangle(actCol, 0, false);
                var del = Task.Run(async () => { await Task.Delay(1200); SendKeys.SendWait("{ENTER}"); });
                await Click(grid, new Point(cell.X + 48, cell.Y + cell.Height / 2), wait: 2500);
                await del;
                deleted = (await Api.GetHostsAsync("man-qa-node")).Count == 0;
            }
            var ev = await Api.GetEventsAsync("man-qa-node", "HOST");
            Result("TC-MAN-07", opened && err1 == "Địa chỉ IP không hợp lệ" && added && edited && deleted && ev.Count >= 3,
                $"mở hộp thoại={opened}, IP sai báo '{err1}', thêm={added}, sửa={edited}, xóa={deleted}, Event Log HOST={ev.Count} dòng");
        }

        // ---------------- TC-MAN-08: đổi khoảng thời gian biểu đồ ----------------
        {
            var web = (await Api.GetHostsAsync("web-prod-01")).First().Host.Id;
            main.Navigate(new HostDetailPage(web));
            await Task.Delay(2000);
            var chart = All(main).OfType<Chart>().First();
            var seg = All(main).OfType<Segmented>().First();
            var res = new List<string>();
            bool ok = true;
            foreach (var r in new[] { "1h", "24h", "7d" })
            {
                await Click(seg.Controls.Cast<Control>().First(c => (string)c.Tag == r), wait: 1800);
                var pts = chart.Series[0].Points;
                var fmt = chart.ChartAreas[0].AxisX.LabelStyle.Format;
                var span = pts.Count > 1 ? DateTime.FromOADate(pts[pts.Count - 1].XValue) - DateTime.FromOADate(pts[0].XValue) : TimeSpan.Zero;
                res.Add($"{r}: {pts.Count} điểm, {span.TotalHours:0.#} giờ, định dạng {fmt}");
                ok &= seg.Selected == r && pts.Count > 10;
                if (r == "7d") { ok &= fmt == "dd/MM HH:mm" && span.TotalDays > 6; Shot(main, "man08_bieu_do_7_ngay"); }
                if (r == "1h") ok &= span.TotalMinutes <= 61;
            }
            Result("TC-MAN-08", ok, string.Join("; ", res));
        }

        // ---------------- TC-MAN-09: bật/tắt quy tắc bằng công tắc ----------------
        {
            main.Navigate(new ThresholdsPage());
            await Task.Delay(2000);
            var grid = All(main).OfType<DataGridView>().First();
            int row = grid.Rows.Cast<DataGridViewRow>().First(r => ((AlertRule)r.Tag).ServerName == "backup-hn-03").Index;
            var rule = (AlertRule)grid.Rows[row].Tag;
            bool before = rule.IsEnabled;
            var cell = grid.GetCellDisplayRectangle(grid.Columns["on"].Index, row, false);
            await Click(grid, new Point(cell.X + 30, cell.Y + cell.Height / 2), wait: 1500);
            bool after1 = (await Api.GetThresholdsAsync()).First(r => r.Id == rule.Id).IsEnabled;
            var onText = All(main).OfType<Label>().Select(l => l.Text).FirstOrDefault(x => x.Contains(" / "));
            Shot(main, "man09_bat_tat_quy_tac");
            row = grid.Rows.Cast<DataGridViewRow>().First(r => ((AlertRule)r.Tag).Id == rule.Id).Index;
            cell = grid.GetCellDisplayRectangle(grid.Columns["on"].Index, row, false);
            await Click(grid, new Point(cell.X + 30, cell.Y + cell.Height / 2), wait: 1500);
            bool after2 = (await Api.GetThresholdsAsync()).First(r => r.Id == rule.Id).IsEnabled;
            Result("TC-MAN-09", after1 == !before && after2 == before,
                $"backup-hn-03 Disk: {before} → {after1} → {after2} (đã trả về như cũ), thẻ 'Quy tắc đang bật' lúc bật = {onText}");
        }

        // ---------------- TC-MAN-10: tạo quy tắc sai (Critical < Warning) ----------------
        {
            int countBefore = (await Api.GetThresholdsAsync()).Count;
            var dlg = await OpenDialog<RuleDialog>(main, "Tạo quy tắc");
            string err = "";
            if (dlg != null)
            {
                var f = Inputs(dlg); // unit, warning, critical, duration
                await Type(f[1].Box, "90");
                await Type(f[2].Box, "80");
                await Click(Btn(dlg, "Lưu"));
                err = ErrorText(dlg);
                Shot(dlg, "man10_nguong_sai");
                await Click(Btn(dlg, "Hủy"));
            }
            int countAfter = (await Api.GetThresholdsAsync()).Count;
            Result("TC-MAN-10", dlg != null && err == "Ngưỡng Critical phải ≥ ngưỡng Warning" && countAfter == countBefore && Dlg<RuleDialog>() == null,
                $"mở hộp thoại={dlg != null}, báo '{err}', số quy tắc {countBefore} → {countAfter}");
        }

        // ---------------- TC-MAN-11: thu nhỏ / khôi phục / phóng to ----------------
        {
            main.Navigate(new OverviewPage());
            await Task.Delay(1500);
            main.WindowState = FormWindowState.Minimized; await Task.Delay(1500);
            main.WindowState = FormWindowState.Normal; await Task.Delay(1500);
            int normalW = All(main).OfType<Chart>().First().Width;
            main.WindowState = FormWindowState.Maximized; await Task.Delay(2000);
            int maxW = All(main).OfType<Chart>().First().Width;
            Shot(main, "man11_phong_to");
            main.WindowState = FormWindowState.Normal; await Task.Delay(1000);
            Result("TC-MAN-11", maxW > normalW, $"biểu đồ rộng {normalW}px (thường) → {maxW}px (phóng to), không lỗi khi thu nhỏ/khôi phục");
        }

        // ---------------- TC-MAN-12: đăng xuất ----------------
        {
            var avatar = All(main).OfType<Panel>().First(p => p.Size == new Size(34, 34));
            await Click(avatar, wait: 800);
            SendKeys.SendWait("{DOWN}");
            SendKeys.SendWait("{ENTER}");
            bool closed = await WaitFor(() => main.IsDisposed || !main.Visible, 5000);
            Result("TC-MAN-12", closed && main.LoggedOut && Api.Token == null, $"form đóng={closed}, LoggedOut={main.LoggedOut}, token đã xóa={Api.Token == null}");
        }
        if (!main.IsDisposed) main.Close();
    }
}
}
