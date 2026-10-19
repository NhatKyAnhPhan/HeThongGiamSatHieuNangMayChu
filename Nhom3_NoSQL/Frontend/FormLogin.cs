using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Text.RegularExpressions;
using System.Windows.Forms;
using Nhom3_NoSQL.Backend.Api;
using Nhom3_NoSQL.Frontend.UI;

namespace Nhom3_NoSQL.Frontend
{
    //Màn hình đăng nhập quản trị: bên trái giới thiệu hệ thống, bên phải form đăng nhập
    public class FormLogin : Form
    {
        readonly ApiClient _api;
        readonly InputBox _email, _password;
        readonly Label _error;
        readonly RoundButton _login;

        public FormLogin(ApiClient api)
        {
            _api = api;
            Text = "ServerPulse – Đăng nhập";
            BackColor = Theme.Bg;
            ForeColor = Theme.Text;
            Font = Theme.Sans(10);
            StartPosition = FormStartPosition.CenterScreen;
            var work = Screen.PrimaryScreen.WorkingArea;
            Size = new Size(Math.Min(1280, work.Width), Math.Min(800, work.Height));
            MinimumSize = new Size(980, 640);
            DoubleBuffered = true;
            Theme.DarkTitleBar(this);

            var split = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, BackColor = Theme.Bg };
            split.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 54));
            split.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 46));
            Controls.Add(split);

            // ---------------- Bên trái: hero ----------------
            var hero = new HeroPanel { Dock = DockStyle.Fill, Margin = new Padding(0) };
            split.Controls.Add(hero, 0, 0);

            // ---------------- Bên phải: form ----------------
            var right = new Panel { Dock = DockStyle.Fill, BackColor = Theme.Bg, Margin = new Padding(0) };
            split.Controls.Add(right, 1, 0);
            var card = new RoundPanel { Size = new Size(400, 450), FillColor = Theme.Panel, Radius = 14, Padding = new Padding(24) };
            right.Controls.Add(card);
            var foot = Theme.Label("© 2026 ServerPulse Operations · Nhóm 3 – Đồ án NoSQL", Theme.Sans(8.5f), Theme.Dim);
            right.Controls.Add(foot);
            right.Layout += (s, e) =>
            {
                card.Location = new Point((right.Width - card.Width) / 2, (right.Height - card.Height) / 2 - 10);
                foot.Location = new Point((right.Width - foot.Width) / 2, right.Height - 40);
            };

            var eyebrow = Theme.Label("OPERATOR CONSOLE", Theme.Mono(8, FontStyle.Bold), Theme.Cyan); eyebrow.Location = new Point(24, 24);
            var title = Theme.Label("Đăng nhập quản trị", Theme.Sans(18), Theme.Text); title.Location = new Point(21, 44);
            var sub = Theme.Label("Sử dụng tài khoản nội bộ để truy cập cụm Production.", Theme.Sans(9.5f), Theme.Muted); sub.Location = new Point(24, 84);
            var le = Theme.Label("Email quản trị", Theme.Sans(9), Theme.Text2); le.Location = new Point(24, 118);
            _email = new InputBox(Theme.Icon.Mail, "admin@serverpulse.vn") { Location = new Point(24, 140), Width = 352 };
            var lp = Theme.Label("Mật khẩu", Theme.Sans(9), Theme.Text2); lp.Location = new Point(24, 192);
            var forgot = Theme.Label("Quên mật khẩu?", Theme.Semi(9), Theme.Cyan); forgot.Location = new Point(280, 192); forgot.Cursor = Cursors.Hand;
            forgot.Click += (s, e) => MessageBox.Show(this, "Liên hệ ops@serverpulse.vn để đặt lại mật khẩu.", "ServerPulse");
            _password = new InputBox(Theme.Icon.Lock, "••••••••", password: true) { Location = new Point(24, 214), Width = 352 };
            _password.Trailing.Text = Theme.Icon.EyeOff;
            _password.Trailing.Cursor = Cursors.Hand;
            // dùng MouseDown: 2 lần bấm nhanh liên tiếp bị Windows tính là double-click và lần 2 không phát Click
            _password.Trailing.MouseDown += (s, e) =>
            {
                if (e.Button != MouseButtons.Left) return;
                _password.Box.UseSystemPasswordChar = !_password.Box.UseSystemPasswordChar;
                _password.Trailing.Text = _password.Box.UseSystemPasswordChar ? Theme.Icon.EyeOff : Theme.Icon.Eye;
            };
            _password.PerformLayout();
            var session = Theme.Label("✓  Phiên đăng nhập có hiệu lực 8 giờ", Theme.Sans(9), Theme.Text2); session.Location = new Point(24, 266);
            _error = Theme.Label("", Theme.Sans(9.5f), Theme.Red); _error.Location = new Point(24, 292);
            _login = new RoundButton("Đăng nhập an toàn  →", RoundButton.Kind.Primary) { Location = new Point(24, 324), Size = new Size(352, 46) };
            _login.Click += async (s, e) => await DoLogin();
            var secure = Theme.Label("Mật khẩu băm PBKDF2 · Kết nối qua REST API nội bộ", Theme.Sans(8.5f), Theme.Muted); secure.Location = new Point(60, 390);
            card.Controls.AddRange(new Control[] { eyebrow, title, sub, le, _email, lp, forgot, _password, session, _error, _login, secure });
            AcceptButton = _login;
            Shown += (s, e) => _email.Box.Focus();
        }

        public void Prefill(string email, string password) { _email.Text = email; _password.Text = password; }

        //Kiểm tra dữ liệu nhập trước khi gọi API
        public static string ValidateInput(string email, string password)
        {
            if (!Regex.IsMatch(email ?? "", @"^[^@\s]+@[^@\s]+\.[^@\s]+$")) return "Email không hợp lệ";
            if (string.IsNullOrEmpty(password)) return "Vui lòng nhập mật khẩu";
            return null;
        }

        public async System.Threading.Tasks.Task DoLogin()
        {
            _error.Text = ValidateInput(_email.Text.Trim(), _password.Text) ?? "";
            if (_error.Text != "") return;
            _login.Enabled = false;
            try
            {
                await _api.LoginAsync(_email.Text.Trim(), _password.Text);
                DialogResult = DialogResult.OK;
            }
            catch (ApiClientException ex) { _error.Text = ex.Message; }
            finally { if (!IsDisposed) _login.Enabled = true; }
        }

        public string ErrorText => _error.Text;

        //Nền bên trái: lưới rack server phát sáng + thẻ giới thiệu
        class HeroPanel : Panel
        {
            public HeroPanel()
            {
                SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.UserPaint | ControlStyles.ResizeRedraw, true);
            }

            protected override void OnPaint(PaintEventArgs e)
            {
                var g = e.Graphics;
                var r = ClientRectangle;
                using (var bg = new LinearGradientBrush(new Rectangle(0, -2, r.Width, r.Height + 4), Theme.Hex("#12303a"), Theme.Bg, 90f)) g.FillRectangle(bg, r);
                // các tủ rack: cột dọc + đèn nhỏ
                var rnd = new Random(7);
                for (int x = 30; x < r.Width - 20; x += 48)
                {
                    using (var p = new Pen(Color.FromArgb(18, Theme.Cyan))) g.DrawLine(p, x, 0, x, r.Height);
                    for (int y = 20; y < r.Height * 0.75; y += 17)
                        if (rnd.Next(4) == 0)
                            using (var b = new SolidBrush(Color.FromArgb(rnd.Next(40, 120), rnd.Next(3) == 0 ? Theme.Green : Theme.Cyan)))
                                g.FillEllipse(b, x + 6 + rnd.Next(30), y, 3, 3);
                }
                // vùng brush lớn hơn vùng tô 2px để GDI+ không lặp màu cuối ở mép (tạo vạch ngang)
                using (var fade = new LinearGradientBrush(new Rectangle(0, r.Height / 3 - 2, r.Width, r.Height * 2 / 3 + 4), Color.FromArgb(0, Theme.Bg), Theme.Bg, 90f))
                    g.FillRectangle(fade, 0, r.Height / 3, r.Width, r.Height * 2 / 3 + 1);

                // logo
                Draw.FillRound(g, new Rectangle(32, 28, 190, 50), 10, Color.FromArgb(220, Theme.Panel), Theme.Line);
                Draw.FillRound(g, new Rectangle(42, 36, 34, 34), 8, Theme.Hex("#0f3a45"));
                TextRenderer.DrawText(g, Theme.Icon.Pulse, Theme.IconFont(13), new Rectangle(42, 36, 34, 34), Theme.Cyan, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
                TextRenderer.DrawText(g, "ServerPulse", Theme.Semi(14), new Point(84, 39), Theme.Text);

                // thẻ giới thiệu
                var card = new Rectangle(32, r.Height - 330, Math.Min(460, r.Width - 64), 250);
                Draw.FillRound(g, card, 12, Color.FromArgb(235, Theme.Hex("#0d1820")), Theme.Line);
                Draw.Pill(g, new Point(card.X + 20, card.Y + 20), "HỆ THỐNG ĐANG VẬN HÀNH", Theme.Green, Theme.Hex("#0d1820"));
                TextRenderer.DrawText(g, "Quan sát toàn bộ hạ tầng.\nPhản ứng trước sự cố.", Theme.Sans(22), new Rectangle(card.X + 18, card.Y + 56, card.Width - 30, 90), Theme.Text2, TextFormatFlags.WordBreak);
                TextRenderer.DrawText(g, "Giám sát CPU, RAM, Disk, Network và heartbeat theo thời gian thực. Dữ liệu được cập nhật mỗi 3–5 giây.",
                    Theme.Sans(10), new Rectangle(card.X + 20, card.Y + 146, card.Width - 40, 44), Theme.Muted, TextFormatFlags.WordBreak);
                var stats = new[] { ("MongoDB", "TIME-SERIES"), ("3–5s", "CHU KỲ THU THẬP"), ("24/7", "CẢNH BÁO TỰ ĐỘNG") };
                for (int i = 0; i < stats.Length; i++)
                {
                    TextRenderer.DrawText(g, stats[i].Item1, Theme.Mono(12, FontStyle.Bold), new Point(card.X + 20 + i * 130, card.Y + 196), Theme.Cyan);
                    TextRenderer.DrawText(g, stats[i].Item2, Theme.Mono(7), new Point(card.X + 21 + i * 130, card.Y + 222), Theme.Dim);
                }
            }
        }
    }
}
