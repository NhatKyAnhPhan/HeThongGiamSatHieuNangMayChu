using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;
using Nhom3_NoSQL.Backend.Api;
using Nhom3_NoSQL.Frontend.Pages;
using Nhom3_NoSQL.Frontend.UI;

namespace Nhom3_NoSQL.Frontend
{
    //Khung ứng dụng: sidebar + thanh trên + vùng nội dung; tự làm mới trang hiện tại mỗi 5 giây
    public class FormMain : Form
    {
        public ApiClient Api { get; }
        public bool LoggedOut { get; private set; }

        readonly Panel _content = new Panel { Dock = DockStyle.Fill, BackColor = Theme.Bg, Padding = new Padding(20, 16, 20, 20) };
        readonly Label _title = Theme.Label("", Theme.Semi(15), Theme.Text);
        readonly Label _crumb = Theme.Label("", Theme.Mono(8.5f), Theme.Muted);
        readonly FlowLayoutPanel _actions = new FlowLayoutPanel { AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink, WrapContents = false, BackColor = Theme.Bg, Margin = new Padding(0), Padding = new Padding(0) };
        readonly Label _clock = Theme.Label("", Theme.Mono(9), Theme.Muted);
        readonly Toast _toast = new Toast();
        readonly Timer _refreshTimer = new Timer { Interval = 5000 };
        readonly Timer _clockTimer = new Timer { Interval = 1000 };
        readonly List<NavItem> _nav = new List<NavItem>();
        PageBase _page;
        bool _refreshing;

        public FormMain(ApiClient api)
        {
            Api = api;
            Text = "ServerPulse – Giám sát hạ tầng";
            BackColor = Theme.Bg;
            ForeColor = Theme.Text;
            Font = Theme.Sans(10);
            StartPosition = FormStartPosition.CenterScreen;
            // vừa vùng làm việc của màn hình (laptop thường chỉ cao ~800px sau khi trừ taskbar)
            var work = Screen.PrimaryScreen.WorkingArea;
            Size = new Size(Math.Min(1440, work.Width), Math.Min(900, work.Height));
            if (work.Width < 1440 || work.Height < 900) WindowState = FormWindowState.Maximized;
            MinimumSize = new Size(1180, 720);
            DoubleBuffered = true;
            KeyPreview = true;
            Theme.DarkTitleBar(this);

            Controls.Add(_content);
            Controls.Add(BuildTopbar());
            Controls.Add(BuildSidebar());
            Controls.Add(_toast);

            _refreshTimer.Tick += async (s, e) => await RefreshPageAsync(silent: true);
            _clockTimer.Tick += (s, e) => _clock.Text = "LIVE · " + DateTime.Now.ToString("HH:mm:ss");
            Api.Unauthorized += () => BeginInvoke((Action)(() => { if (!LoggedOut) { MessageBox.Show(this, "Phiên đăng nhập đã hết hạn, vui lòng đăng nhập lại.", "ServerPulse"); Logout(); } }));
            Shown += (s, e) => { _clockTimer.Start(); _refreshTimer.Start(); Navigate(new OverviewPage()); };
            FormClosed += (s, e) => { _refreshTimer.Stop(); _clockTimer.Stop(); };
            Disposed += (s, e) => { _refreshTimer.Dispose(); _clockTimer.Dispose(); };
        }

        // ---------------- Sidebar ----------------
        Control BuildSidebar()
        {
            var side = new Panel { Dock = DockStyle.Left, Width = 240, BackColor = Theme.Panel, Padding = new Padding(12, 16, 12, 12) };
            side.Paint += (s, e) => { using (var p = new Pen(Theme.Line)) e.Graphics.DrawLine(p, side.Width - 1, 0, side.Width - 1, side.Height); };

            var logo = new Panel { Location = new Point(14, 14), Size = new Size(36, 36), BackColor = Theme.Panel };
            logo.Paint += (s, e) =>
            {
                Draw.FillRound(e.Graphics, new Rectangle(0, 0, 35, 35), 8, Theme.Hex("#0f3a45"));
                TextRenderer.DrawText(e.Graphics, Theme.Icon.Pulse, Theme.IconFont(14), new Rectangle(0, 0, 36, 36), Theme.Cyan, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
            };
            // "Server" + "Pulse" vẽ liền nhau (2 Label riêng sẽ bị hở do padding)
            var brand = new Panel { Location = new Point(58, 14), Size = new Size(170, 36), BackColor = Theme.Panel };
            brand.Paint += (s, e) =>
            {
                const TextFormatFlags flags = TextFormatFlags.NoPadding;
                var w = TextRenderer.MeasureText(e.Graphics, "Server", Theme.Sans(15), Size.Empty, flags).Width;
                TextRenderer.DrawText(e.Graphics, "Server", Theme.Sans(15), new Point(0, 5), Theme.Text, flags);
                TextRenderer.DrawText(e.Graphics, "Pulse", Theme.Sans(15), new Point(w, 5), Theme.Cyan, flags);
            };

            var lbl = Theme.Label("CỤM VẬN HÀNH", Theme.Mono(8, FontStyle.Bold), Theme.Muted);
            lbl.Location = new Point(16, 72);
            var cluster = new RoundPanel { Location = new Point(12, 96), Size = new Size(216, 58), FillColor = Theme.Hex("#123528"), BorderColor = Theme.Hex("#1d5a42"), Radius = 8 };
            cluster.BackColor = Theme.Panel;
            var c1 = Theme.Label("Production · SGN", Theme.Semi(10), Theme.Text); c1.Location = new Point(30, 9);
            var c2 = Theme.Label("ĐANG ĐỒNG BỘ", Theme.Mono(7.5f, FontStyle.Bold), Theme.Green); c2.Location = new Point(31, 32);
            var dot = new Panel { Location = new Point(12, 16), Size = new Size(10, 10) };
            dot.Paint += (s, e) => { e.Graphics.Clear(cluster.FillColor); Draw.Dot(e.Graphics, 0, 0, Theme.Green, 9); };
            cluster.Controls.AddRange(new Control[] { dot, c1, c2 });

            side.Controls.AddRange(new Control[] { logo, brand, lbl, cluster });

            var items = new (string text, string icon, Func<PageBase> page)[]
            {
                ("Tổng quan", Theme.Icon.Grid, () => new OverviewPage()),
                ("Máy chủ", Theme.Icon.Server, () => new HostsPage()),
                ("Ngưỡng cảnh báo", Theme.Icon.Sliders, () => new ThresholdsPage()),
                ("Sự cố", Theme.Icon.Siren, () => new IncidentsPage()),
                ("Nhật ký sự kiện", Theme.Icon.List, () => new EventsPage()),
            };
            int y = 170;
            foreach (var it in items)
            {
                var factory = it.page;
                var n = new NavItem(it.text, it.icon) { Location = new Point(12, y), Width = 216 };
                n.Click += (s, e) => Navigate(factory());
                side.Controls.Add(n);
                _nav.Add(n);
                y += 48;
            }

            var collect = new RoundPanel { Size = new Size(216, 56), FillColor = Theme.Card, Radius = 8, Anchor = AnchorStyles.Left | AnchorStyles.Bottom };
            collect.BackColor = Theme.Panel;
            var ci = Theme.IconLabel(Theme.Icon.Antenna, 14, Theme.Cyan); ci.Location = new Point(12, 16);
            var ct1 = Theme.Label("Chu kỳ thu thập", Theme.Sans(8.5f), Theme.Muted); ct1.Location = new Point(44, 8);
            var ct2 = Theme.Label("3–5 giây", Theme.Mono(10, FontStyle.Bold), Theme.Text); ct2.Location = new Point(44, 28);
            collect.Controls.AddRange(new Control[] { ci, ct1, ct2 });
            var ver = Theme.Label("v2.8.4", Theme.Mono(8), Theme.Dim); ver.Anchor = AnchorStyles.Left | AnchorStyles.Bottom;
            var tz = Theme.Label("UTC+7", Theme.Mono(8), Theme.Dim); tz.Anchor = AnchorStyles.Left | AnchorStyles.Bottom;
            side.Controls.AddRange(new Control[] { collect, ver, tz });
            side.Layout += (s, e) =>
            {
                collect.Location = new Point(12, side.Height - 100);
                ver.Location = new Point(14, side.Height - 32);
                tz.Location = new Point(228 - tz.Width, side.Height - 32);
            };
            return side;
        }

        // ---------------- Topbar ----------------
        Control BuildTopbar()
        {
            var top = new Panel { Dock = DockStyle.Top, Height = 64, BackColor = Theme.Bg };
            top.Paint += (s, e) => { using (var p = new Pen(Theme.Line)) e.Graphics.DrawLine(p, 0, top.Height - 1, top.Width, top.Height - 1); };
            _title.Location = new Point(20, 18);
            _title.BackColor = _crumb.BackColor = _clock.BackColor = Theme.Bg;
            top.Controls.Add(_title);
            top.Controls.Add(_crumb);

            var liveDot = new Panel { Size = new Size(9, 9), BackColor = Theme.Bg };
            liveDot.Paint += (s, e) => { e.Graphics.Clear(Theme.Bg); Draw.Dot(e.Graphics, 0, 0, Theme.Green, 8); };
            var bell = Theme.IconLabel(Theme.Icon.Bell, 13, Theme.Muted);
            bell.BackColor = Theme.Bg;
            bell.Cursor = Cursors.Hand;
            bell.Click += (s, e) => Navigate(new IncidentsPage());

            var user = Api.User;
            var initials = string.Concat((user?.FullName ?? "U").Split(' ').Where(w => w.Length > 0).Select(w => w[0])).ToUpper();
            if (initials.Length > 2) initials = initials.Substring(0, 2);
            var avatar = new Panel { Size = new Size(34, 34), BackColor = Theme.Bg, Cursor = Cursors.Hand };
            avatar.Paint += (s, e) =>
            {
                e.Graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
                using (var b = new SolidBrush(Theme.Hex("#3b2a63"))) e.Graphics.FillEllipse(b, 0, 0, 33, 33);
                TextRenderer.DrawText(e.Graphics, initials, Theme.Semi(9), new Rectangle(0, 0, 34, 34), Theme.Hex("#d6c8ff"), TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
            };
            var uname = Theme.Label(user?.FullName ?? "", Theme.Semi(10), Theme.Text);
            var urole = Theme.Label(Api.IsAdmin ? "Quản trị viên" : "Người xem", Theme.Sans(8.5f), Theme.Muted);
            uname.BackColor = urole.BackColor = Theme.Bg;
            var menu = new ContextMenuStrip { BackColor = Theme.Card, ForeColor = Theme.Text, ShowImageMargin = false, Font = Theme.Sans(10) };
            menu.Items.Add(new ToolStripLabel(user?.Email ?? "") { ForeColor = Theme.Muted });
            menu.Items.Add("Đăng xuất", null, (s, e) => Logout());
            foreach (var c in new Control[] { avatar, uname, urole }) { c.Cursor = Cursors.Hand; c.Click += (s, e) => menu.Show(avatar, new Point(0, 38)); }

            top.Controls.AddRange(new Control[] { _actions, liveDot, _clock, bell, avatar, uname, urole });
            top.Layout += (s, e) =>
            {
                _crumb.Location = new Point(_title.Right + 12, 26);
                int right = top.Width - 20;
                int nameW = Math.Max(uname.Width, urole.Width);
                uname.Location = new Point(right - nameW, 13);
                urole.Location = new Point(right - nameW, 34);
                avatar.Location = new Point(right - nameW - 44, 15);
                bell.Location = new Point(avatar.Left - 40, 21);
                _clock.Location = new Point(bell.Left - 24 - Math.Max(_clock.Width, 120), 24);
                liveDot.Location = new Point(_clock.Left - 14, 28);
                _actions.Location = new Point(liveDot.Left - 16 - _actions.Width, 14);
            };
            _actions.SizeChanged += (s, e) => top.PerformLayout();
            _title.SizeChanged += (s, e) => top.PerformLayout();
            return top;
        }

        // ---------------- Điều hướng ----------------
        public void Navigate(PageBase page)
        {
            _page?.Dispose();
            _content.Controls.Clear();
            _page = page;
            page.Main = this;
            page.Dock = DockStyle.Fill;
            _content.Controls.Add(page);
            page.Build();

            _title.Text = page.Title;
            _crumb.Text = page.Crumb.ToUpper();
            _actions.Controls.Clear();
            _actions.Controls.AddRange(page.Actions().ToArray());
            foreach (var n in _nav) n.Active = n.Text == page.NavKey;
            _ = RefreshPageAsync(silent: false);
        }

        public async Task RefreshPageAsync(bool silent)
        {
            if (_refreshing)
            {
                if (silent) return;
                while (_refreshing) await Task.Delay(50); // lần làm mới chủ động: chờ lượt đang chạy xong rồi nạp lại
            }
            if (_page == null || _page.IsDisposed) return;
            if (silent && (OwnedForms.Length > 0 || WindowState == FormWindowState.Minimized)) return; // đang mở hộp thoại / thu nhỏ
            var page = _page;
            _refreshing = true;
            try
            {
                await page.RefreshAsync();
                var alerts = await Api.GetAlertsAsync("ACTIVE");
                _nav.First(n => n.Text == "Sự cố").Badge = alerts.Stats.Opening + alerts.Stats.Acknowledged;
            }
            catch (ApiClientException ex) when (ex.StatusCode == 401) { }
            catch (Exception ex)
            {
                if (page == _page && !page.IsDisposed) ShowToast(ex.Message, true);
            }
            finally { _refreshing = false; }
        }

        public void ShowToast(string message, bool error = false) => _toast.Show(message, error);
        public void SetCrumb(string crumb) { _crumb.Text = crumb.ToUpper(); }

        async void Logout()
        {
            LoggedOut = true;
            try { await Api.LogoutAsync(); } catch { }
            Close();
        }
    }

    //Mục menu sidebar (có badge số sự cố)
    public class NavItem : Control
    {
        readonly string _icon;
        bool _active, _hover;
        int _badge;

        public NavItem(string text, string icon)
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.UserPaint | ControlStyles.ResizeRedraw, true);
            Text = text;
            _icon = icon;
            Height = 42;
            Cursor = Cursors.Hand;
        }

        public bool Active { get => _active; set { _active = value; Invalidate(); } }
        public int Badge { set { if (_badge != value) { _badge = value; Invalidate(); } } }

        protected override void OnMouseEnter(EventArgs e) { _hover = true; Invalidate(); base.OnMouseEnter(e); }
        protected override void OnMouseLeave(EventArgs e) { _hover = false; Invalidate(); base.OnMouseLeave(e); }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.Clear(Theme.Panel);
            if (_active) Draw.FillRound(g, new Rectangle(0, 0, Width - 1, Height - 1), 8, Theme.SelBg, Theme.SelLine);
            else if (_hover) Draw.FillRound(g, new Rectangle(0, 0, Width - 1, Height - 1), 8, Theme.Card);
            var fore = _active ? Theme.Text : Theme.Text2;
            TextRenderer.DrawText(g, _icon, Theme.IconFont(12), new Rectangle(12, 0, 24, Height), fore, TextFormatFlags.VerticalCenter);
            TextRenderer.DrawText(g, Text, Theme.Sans(10.5f), new Rectangle(44, 0, Width - 80, Height), fore, TextFormatFlags.VerticalCenter);
            if (_badge > 0)
            {
                var r = new Rectangle(Width - 36, (Height - 22) / 2, 24, 22);
                Draw.FillRound(g, r, 11, Theme.Mix(Theme.Red, Theme.Panel, 0.25));
                TextRenderer.DrawText(g, _badge.ToString(), Theme.Mono(8.5f, FontStyle.Bold), r, Theme.Red, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
            }
        }
    }
}
