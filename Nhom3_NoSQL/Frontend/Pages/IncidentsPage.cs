using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;
using Nhom3_NoSQL.Backend.Api;
using Nhom3_NoSQL.Backend.Models;
using Nhom3_NoSQL.Frontend.UI;

namespace Nhom3_NoSQL.Frontend.Pages
{
    //Màn hình Cảnh báo & xử lý sự cố: danh sách sự cố + chi tiết, timeline, Acknowledge / Resolve
    public class IncidentsPage : PageBase
    {
        public override string Title => "Cảnh báo & xử lý sự cố";
        public override string Crumb => "Incident response / Live";
        public override string NavKey => "Sự cố";

        static readonly string[] StatusValues = { "ACTIVE", "", AlertStatus.Opening, AlertStatus.Acknowledged, AlertStatus.Resolved };
        static readonly int[] HourValues = { 24, 1, 168 };

        static readonly Dictionary<MetricType, string> Hints = new Dictionary<MetricType, string>
        {
            [MetricType.Cpu] = "Gợi ý: kiểm tra tiến trình tiêu thụ CPU (top / Task Manager), slow query hoặc vòng lặp bất thường.",
            [MetricType.Ram] = "Gợi ý: kiểm tra rò rỉ bộ nhớ, cache quá lớn hoặc tăng RAM / giới hạn heap.",
            [MetricType.Disk] = "Gợi ý: dọn log cũ, xoay vòng log (logrotate) hoặc mở rộng phân vùng /data.",
            [MetricType.Heartbeat] = "Gợi ý: kiểm tra Agent còn chạy không, kết nối mạng và tường lửa tới API server.",
            [MetricType.NetworkIn] = "Gợi ý: kiểm tra lưu lượng bất thường, DDoS hoặc job đồng bộ dữ liệu lớn.",
            [MetricType.NetworkOut] = "Gợi ý: kiểm tra tác vụ backup / upload lớn đang chạy.",
        };

        StatCard _open, _ack, _res, _mt;
        InputBox _search;
        ComboBox _sev, _status, _hours;
        FlowLayoutPanel _list;
        Label _count;
        Panel _detail;
        List<AlertEvent> _items = new List<AlertEvent>();
        string _selected;

        public override void Build()
        {
            var root = Rows(112, 54, -100);
            Controls.Add(root);

            var stats = Cols(-25, -25, -25, -25);
            _open = new StatCard("Đang mở", "●", Theme.Red) { ValueColor = Theme.Red };
            _ack = new StatCard("Đã tiếp nhận", "●", Theme.Amber) { ValueColor = Theme.Amber };
            _res = new StatCard("Đã xử lý hôm nay", "●", Theme.Green) { ValueColor = Theme.Green };
            _mt = new StatCard("MTTA / MTTR", "●", Theme.Cyan) { ValueColor = Theme.Cyan };
            foreach (var c in new[] { _open, _ack, _res, _mt }) c.FootColor = Theme.Muted;
            _ack.Foot = "Đang xử lý"; _res.Foot = "Resolved trong ngày"; _mt.Foot = "Thời gian tiếp nhận / khắc phục TB";
            stats.Controls.Add(Cell(_open), 0, 0); stats.Controls.Add(Cell(_ack), 1, 0);
            stats.Controls.Add(Cell(_res), 2, 0); stats.Controls.Add(Cell(_mt, right: 0), 3, 0);
            root.Controls.Add(stats, 0, 0);

            _search = new InputBox(Theme.Icon.Search, "Tìm mã, host, nội dung...") { Width = 260 };
            _sev = Theme.Combo("Mọi mức độ", "Critical", "Warning");
            _status = Theme.Combo("OPENING + ACK", "Tất cả trạng thái", "OPENING", "ACKNOWLEDGED", "RESOLVED");
            _hours = Theme.Combo("24 giờ qua", "1 giờ qua", "7 ngày qua");
            var history = new RoundButton("Lịch sử xử lý", RoundButton.Kind.Normal, Theme.Icon.History);
            history.Click += (s, e) => Main.Navigate(new EventsPage("ALERT"));
            var barWrap = new Panel { Dock = DockStyle.Fill, BackColor = Theme.Bg };
            barWrap.Controls.Add(Toolbar(_search, _sev, _status, _hours));
            barWrap.Controls.Add(history);
            history.BringToFront(); // nằm trên thanh công cụ (Dock Fill)
            barWrap.Layout += (s, e) => history.Location = new Point(barWrap.Width - history.Width, 8);
            root.Controls.Add(barWrap, 0, 1);
            var debounce = new Timer { Interval = 300 };
            debounce.Tick += async (s, e) => { debounce.Stop(); await Try(RefreshAsync); };
            _search.Box.TextChanged += (s, e) => { debounce.Stop(); debounce.Start(); };
            Disposed += (s, e) => debounce.Dispose();
            foreach (var c in new[] { _sev, _status, _hours }) c.SelectedIndexChanged += async (s, e) => await Try(RefreshAsync);

            var split = Cols(-55, -45);
            _count = Theme.Label("", Theme.Mono(8.5f), Theme.Muted);
            var listCard = CardWithHeader("Sự cố gần đây", "", out var lb, _count);
            _list = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.TopDown, WrapContents = false, AutoScroll = true, BackColor = Theme.Card };
            Theme.FitRows(_list);
            lb.Controls.Add(_list);
            split.Controls.Add(Cell(listCard, bottom: 0), 0, 0);

            var detailCard = new RoundPanel { Padding = new Padding(18, 16, 18, 16) };
            _detail = new Panel { Dock = DockStyle.Fill, BackColor = Theme.Card, AutoScroll = true };
            Theme.DarkScroll(_detail);
            detailCard.Controls.Add(_detail);
            split.Controls.Add(Cell(detailCard, right: 0, bottom: 0), 1, 0);
            root.Controls.Add(split, 0, 2);
        }

        public override async Task RefreshAsync()
        {
            var res = await Api.GetAlertsAsync(StatusValues[_status.SelectedIndex], _sev.SelectedIndex == 0 ? null : (string)_sev.SelectedItem,
                _search.Text.Trim(), HourValues[_hours.SelectedIndex]);
            if (IsDisposed) return;
            var st = res.Stats;
            _open.Value = st.Opening.ToString(); _open.Foot = st.OpeningCritical + " critical";
            _ack.Value = st.Acknowledged.ToString();
            _res.Value = st.ResolvedToday.ToString();
            _mt.Value = Theme.Dur(st.MttaSeconds) + " / " + Theme.Dur(st.MttrSeconds);
            _count.Text = res.Items.Count + " KẾT QUẢ";

            _items = res.Items;
            if (_items.All(a => a.Id != _selected)) _selected = _items.FirstOrDefault()?.Id;

            _list.SuspendLayout();
            _list.Controls.Clear();
            if (_items.Count == 0) _list.Controls.Add(Theme.Label("Không có sự cố phù hợp bộ lọc", Theme.Sans(10), Theme.Muted));
            foreach (var a in _items)
            {
                var card = new IncidentCard(a) { Selected = a.Id == _selected };
                card.Click += (s, e) => { _selected = a.Id; foreach (IncidentCard c in _list.Controls.OfType<IncidentCard>()) c.Selected = c.Alert.Id == _selected; DrawDetail(); };
                _list.Controls.Add(card);
            }
            _list.ResumeLayout();
            DrawDetail();
        }

        void DrawDetail()
        {
            if (IsDisposed) return;
            var a = _items.FirstOrDefault(x => x.Id == _selected);
            _detail.SuspendLayout();
            foreach (Control c in _detail.Controls.Cast<Control>().ToList()) c.Dispose();
            if (a == null)
            {
                _detail.Controls.Add(Theme.Label("Chọn một sự cố để xem chi tiết", Theme.Sans(10), Theme.Muted));
                _detail.ResumeLayout();
                return;
            }

            int w = _detail.ClientSize.Width - SystemInformation.VerticalScrollBarWidth - 4, y = 0;
            var sev = a.Severity == AlertSeverity.Critical ? "CRITICAL" : "WARNING";
            var head = new PillLabel(sev, Theme.StatusColor(sev)) { Location = new Point(0, y) };
            var code = Theme.Label(a.Code, Theme.Mono(10), Theme.Muted);
            code.Location = new Point(w - code.PreferredWidth, y + 2);
            _detail.Controls.AddRange(new Control[] { head, code });
            y += 34;

            var title = Theme.Label(a.Message, Theme.Sans(14), Theme.Text); title.MaximumSize = new Size(w, 0); title.Location = new Point(0, y);
            _detail.Controls.Add(title); y += title.PreferredHeight + 6;
            var sub = Theme.Label($"{ThresholdsPage.MetricNames[a.Metric]} trên {a.ServerName} vượt ngưỡng {sev.ToLower()} liên tục ≥ {a.DurationSeconds}s.", Theme.Sans(9.5f), Theme.Muted);
            sub.MaximumSize = new Size(w, 0); sub.Location = new Point(0, y);
            _detail.Controls.Add(sub); y += sub.PreferredHeight + 12;

            var unit = a.Metric == MetricType.Heartbeat ? "s" : a.Metric == MetricType.NetworkIn || a.Metric == MetricType.NetworkOut ? " Kbps" : "%";
            var kv = new KvTable(new[]
            {
                ("Host", a.ServerName, Theme.Cyan),
                ("Metric", a.Metric.ToString().ToLower(), Theme.Text),
                ("Giá trị tại thời điểm phát hiện", a.ActualValue.ToString("#,0.#", Theme.Vi) + unit, Theme.StatusColor(sev)),
                ("Ngưỡng", $"{(sev == "CRITICAL" ? "Critical" : "Warning")} ≥ {a.ThresholdValue.ToString("#,0.#", Theme.Vi)}{unit} / {a.DurationSeconds}s", Theme.Text),
                ("Bắt đầu", Theme.Full(a.TriggeredAt), Theme.Text),
            }) { Location = new Point(0, y), Width = w };
            kv.Cursor = Cursors.Hand;
            kv.Click += (s, e) => Main.Navigate(new HostDetailPage(a.ServerId));
            _detail.Controls.Add(kv); y += kv.Height + 14;

            var tlTitle = Theme.Label("Tiến trình xử lý", Theme.Semi(11), Theme.Text); tlTitle.Location = new Point(0, y);
            _detail.Controls.Add(tlTitle); y += 26;
            var tl = new Timeline(a) { Location = new Point(0, y), Width = w };
            _detail.Controls.Add(tl); y += tl.Height + 4;

            var hint = new RoundPanel { Location = new Point(0, y), Size = new Size(w, 48), FillColor = Theme.Mix(Theme.Amber, Theme.Card, 0.12), BorderColor = Theme.Mix(Theme.Amber, Theme.Card, 0.35), Radius = 8, Padding = new Padding(0) };
            hint.BackColor = Theme.Card;
            var hi = Theme.IconLabel(Theme.Icon.Bulb, 11, Theme.Amber); hi.Location = new Point(12, 15);
            var ht = Theme.Label(Hints[a.Metric], Theme.Sans(9.5f), Theme.Hex("#f6d58a")); ht.MaximumSize = new Size(w - 50, 0); ht.Location = new Point(38, 14);
            hint.Controls.AddRange(new Control[] { hi, ht });
            hint.Height = Math.Max(48, ht.PreferredHeight + 28);
            _detail.Controls.Add(hint); y += hint.Height + 14;

            if (a.Status == AlertStatus.Resolved)
            {
                var done = Theme.Label("✓ Sự cố đã được xử lý", Theme.Semi(10.5f), Theme.Green); done.Location = new Point(0, y);
                _detail.Controls.Add(done);
            }
            else
            {
                bool admin = Api.IsAdmin;
                var ack = new RoundButton("Acknowledge", RoundButton.Kind.Amber, Theme.Icon.Hand) { Location = new Point(0, y), Size = new Size((w - 12) / 2, 42), Enabled = admin && a.Status == AlertStatus.Opening };
                var res = new RoundButton("Resolve", RoundButton.Kind.Success, Theme.Icon.CheckCircle) { Location = new Point((w - 12) / 2 + 12, y), Size = new Size((w - 12) / 2, 42), Enabled = admin };
                ack.Click += async (s, e) => await Try(async () => { await Api.AcknowledgeAsync(a.Id); await Main.RefreshPageAsync(false); }, "Đã tiếp nhận sự cố " + a.Code);
                res.Click += async (s, e) => await Try(async () => { await Api.ResolveAsync(a.Id); await Main.RefreshPageAsync(false); }, "Đã đánh dấu RESOLVED " + a.Code);
                _detail.Controls.AddRange(new Control[] { ack, res });
                if (!admin)
                {
                    var note = Theme.Label("Tài khoản Viewer chỉ được xem, không được xử lý sự cố", Theme.Sans(9), Theme.Muted);
                    note.Location = new Point((w - note.PreferredWidth) / 2, y + 52);
                    _detail.Controls.Add(note);
                }
            }
            _detail.ResumeLayout();
        }

        // ---------------- Control vẽ riêng ----------------
        class PillLabel : Control
        {
            readonly Color _c;
            public PillLabel(string text, Color c) { SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.UserPaint, true); Text = text; _c = c; Size = new Size(110, 24); }
            protected override void OnPaint(PaintEventArgs e) { e.Graphics.Clear(Theme.Card); UI.Draw.Pill(e.Graphics, new Point(0, 1), Text, _c, Theme.Card); }
        }

        //Thẻ sự cố trong danh sách
        class IncidentCard : Control
        {
            public AlertEvent Alert { get; }
            bool _sel;
            public IncidentCard(AlertEvent a)
            {
                SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.UserPaint | ControlStyles.ResizeRedraw, true);
                Alert = a;
                Height = 96;
                Margin = new Padding(0, 0, 0, 10);
                Cursor = Cursors.Hand;
            }
            public bool Selected { set { _sel = value; Invalidate(); } }

            protected override void OnPaint(PaintEventArgs e)
            {
                var g = e.Graphics;
                var a = Alert;
                g.Clear(Theme.Card);
                var bg = _sel ? Theme.SelBg : Theme.Card2;
                UI.Draw.FillRound(g, new Rectangle(0, 0, Width - 1, Height - 1), 10, bg, _sel ? Theme.SelLine : Theme.Line);
                if (_sel) using (var b = new SolidBrush(Theme.Cyan)) g.FillRectangle(b, 0, 8, 3, Height - 16);
                var sev = a.Severity == AlertSeverity.Critical ? "CRITICAL" : "WARNING";
                int pw = UI.Draw.Pill(g, new Point(14, 12), sev, Theme.StatusColor(sev), bg);
                TextRenderer.DrawText(g, a.Code, Theme.Mono(8.5f), new Point(14 + pw + 8, 15), Theme.Muted);
                var when = Theme.HHmm(a.TriggeredAt) + " · " + Theme.Ago(a.TriggeredAt);
                var ww = TextRenderer.MeasureText(g, when, Theme.Mono(8.5f)).Width;
                TextRenderer.DrawText(g, when, Theme.Mono(8.5f), new Point(Width - ww - 14, 15), Theme.Muted);
                TextRenderer.DrawText(g, a.Message, Theme.Semi(10.5f), new Rectangle(14, 40, Width - 28, 22), Theme.Text, TextFormatFlags.EndEllipsis);
                TextRenderer.DrawText(g, Theme.Icon.Server, Theme.IconFont(8.5f), new Point(14, 70), Theme.Muted);
                TextRenderer.DrawText(g, $"{a.ServerName} · {OverviewPage.AlertMetric(a.Metric)}", Theme.Mono(9), new Point(32, 68), Theme.Muted);
                var val = a.Metric == MetricType.Heartbeat ? a.ActualValue + "s"
                    : a.Metric == MetricType.NetworkIn || a.Metric == MetricType.NetworkOut ? Theme.Mbps(a.ActualValue) : Theme.N1(a.ActualValue) + "%";
                var sw = TextRenderer.MeasureText(g, a.Status, Theme.Mono(8, FontStyle.Bold)).Width;
                TextRenderer.DrawText(g, a.Status, Theme.Mono(8, FontStyle.Bold), new Point(Width - sw - 14, 70), Theme.StatusColor(a.Status));
                var vw = TextRenderer.MeasureText(g, val, Theme.Mono(9.5f, FontStyle.Bold)).Width;
                TextRenderer.DrawText(g, val, Theme.Mono(9.5f, FontStyle.Bold), new Point(Width - sw - vw - 22, 68), Theme.StatusColor(sev));
            }
        }

        //Bảng 2 cột Host / Metric / Giá trị / Ngưỡng / Bắt đầu
        class KvTable : Control
        {
            readonly (string k, string v, Color c)[] _rows;
            public KvTable((string, string, Color)[] rows)
            {
                SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.UserPaint | ControlStyles.ResizeRedraw, true);
                _rows = rows;
                Height = rows.Length * 32 + 1;
            }
            protected override void OnPaint(PaintEventArgs e)
            {
                var g = e.Graphics;
                g.Clear(Theme.Card);
                UI.Draw.FillRound(g, new Rectangle(0, 0, Width - 1, Height - 1), 10, Theme.Card, Theme.Line);
                for (int i = 0; i < _rows.Length; i++)
                {
                    int y = i * 32;
                    if (i > 0) using (var p = new Pen(Theme.Line)) g.DrawLine(p, 1, y, Width - 2, y);
                    TextRenderer.DrawText(g, _rows[i].k, Theme.Sans(9.5f), new Rectangle(14, y, Width / 2, 32), Theme.Muted, TextFormatFlags.VerticalCenter);
                    TextRenderer.DrawText(g, _rows[i].v, Theme.Mono(10, FontStyle.Bold), new Rectangle(Width / 3, y, Width * 2 / 3 - 14, 32), _rows[i].c, TextFormatFlags.VerticalCenter | TextFormatFlags.Right);
                }
            }
        }

        //Timeline OPENING → ACKNOWLEDGED → RESOLVED
        class Timeline : Control
        {
            readonly AlertEvent _a;
            public Timeline(AlertEvent a)
            {
                SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.UserPaint | ControlStyles.ResizeRedraw, true);
                _a = a;
                Height = 132;
            }
            protected override void OnPaint(PaintEventArgs e)
            {
                var g = e.Graphics;
                g.Clear(Theme.Card);
                var steps = new[]
                {
                    (AlertStatus.Opening, (DateTime?)_a.TriggeredAt, "Cảnh báo được tạo tự động", (string)null),
                    (AlertStatus.Acknowledged, _a.AcknowledgedAt, _a.AcknowledgedAt.HasValue ? "Đã tiếp nhận" : "Chờ người vận hành tiếp nhận", _a.AcknowledgedBy),
                    (AlertStatus.Resolved, _a.ResolvedAt, _a.ResolvedAt.HasValue ? "Đã khắc phục" : "Chờ xử lý xong", _a.ResolvedBy),
                };
                using (var p = new Pen(Theme.Line2)) g.DrawLine(p, 7, 10, 7, 100);
                for (int i = 0; i < steps.Length; i++)
                {
                    var (st, at, label, by) = steps[i];
                    int y = i * 44;
                    var c = Theme.StatusColor(st);
                    g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
                    using (var bg = new SolidBrush(Theme.Card)) g.FillEllipse(bg, 1, y + 2, 13, 13);
                    if (at.HasValue) using (var b = new SolidBrush(c)) g.FillEllipse(b, 1, y + 2, 13, 13);
                    using (var pen = new Pen(at.HasValue ? c : Theme.Mix(c, Theme.Card, 0.8f), 2)) g.DrawEllipse(pen, 2, y + 3, 11, 11);
                    TextRenderer.DrawText(g, st, Theme.Mono(8.5f, FontStyle.Bold), new Point(26, y), c);
                    var text = at.HasValue ? $"{label} · {Theme.HHmmss(at.Value)}{(by != null ? " · " + by : "")}" : label;
                    TextRenderer.DrawText(g, text, Theme.Sans(9.5f), new Point(26, y + 18), Theme.Muted);
                }
            }
        }
    }
}
