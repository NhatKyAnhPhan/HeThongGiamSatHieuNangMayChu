using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Windows.Forms.DataVisualization.Charting;
using Nhom3_NoSQL.Backend.Api;
using Nhom3_NoSQL.Backend.Models;
using Nhom3_NoSQL.Frontend.UI;

namespace Nhom3_NoSQL.Frontend.Pages
{
    //Màn hình Tổng quan hạ tầng (Dashboard)
    public class OverviewPage : PageBase
    {
        public override string Title => "Tổng quan hạ tầng";
        public override string Crumb => "Overview / Production";

        StatCard _total, _online, _warn, _off;
        Chart _chart;
        Label _health;
        BarRow _cpu, _ram, _disk, _net;
        FlowLayoutPanel _attention, _recent;
        Label _openPill, _mtta;

        public override IEnumerable<Control> Actions()
        {
            var b = new RoundButton("Làm mới", RoundButton.Kind.Normal, Theme.Icon.Refresh);
            b.Click += async (s, e) => { await Main.RefreshPageAsync(silent: false); Toast("Đã cập nhật dữ liệu"); };
            yield return b;
        }

        public override void Build()
        {
            var root = Rows(122, 330, -100);
            Controls.Add(root);

            var stats = Cols(-25, -25, -25, -25);
            _total = new StatCard("Tổng số host", Theme.Icon.Server, Theme.Cyan);
            _online = new StatCard("Đang online", Theme.Icon.CheckCircle, Theme.Green);
            _warn = new StatCard("Warning / Critical", Theme.Icon.Warning, Theme.Amber);
            _off = new StatCard("Offline", Theme.Icon.Offline, Theme.Red);
            stats.Controls.Add(Cell(_total), 0, 0);
            stats.Controls.Add(Cell(_online), 1, 0);
            stats.Controls.Add(Cell(_warn), 2, 0);
            stats.Controls.Add(Cell(_off, right: 0), 3, 0);
            root.Controls.Add(stats, 0, 0);

            // --- Sức khỏe hạ tầng + Tài nguyên trung bình
            var mid = Cols(-65, -35);
            _health = Theme.Label("–", Theme.Mono(14, FontStyle.Bold), Theme.Green);
            var healthCard = CardWithHeader("Sức khỏe hạ tầng", "CPU (xanh) và RAM (tím) trung bình toàn hệ thống · 6 giờ gần nhất · Aggregation Pipeline", out var hb, _health);
            _chart = Theme.LineChart();
            Theme.LineSeries(_chart, "CPU", Theme.Cyan, false);
            Theme.LineSeries(_chart, "RAM", Theme.Purple, false);
            hb.Controls.Add(_chart);
            mid.Controls.Add(Cell(healthCard), 0, 0);

            var resCard = CardWithHeader("Tài nguyên trung bình", "Toàn bộ host đang online", out var rb);
            _cpu = new BarRow { Caption = "CPU", BarColor = Theme.Cyan, Dock = DockStyle.Top };
            _ram = new BarRow { Caption = "RAM", BarColor = Theme.Purple, Dock = DockStyle.Top };
            _disk = new BarRow { Caption = "Disk", BarColor = Theme.Amber, Dock = DockStyle.Top };
            _net = new BarRow { Caption = "Network", BarColor = Theme.Green, Dock = DockStyle.Top, Note = "tổng in + out" };
            rb.Controls.AddRange(new Control[] { _net, Spacer(), _disk, Spacer(), _ram, Spacer(), _cpu }); // Dock Top: thêm sau nằm trên
            mid.Controls.Add(Cell(resCard, right: 0), 1, 0);
            root.Controls.Add(mid, 0, 1);

            // --- Host cần chú ý + Cảnh báo gần đây
            var bottom = Cols(-50, -50);
            var link = Theme.Label("Xem tất cả →", Theme.Sans(9.5f), Theme.Cyan);
            link.Cursor = Cursors.Hand;
            link.Click += (s, e) => Main.Navigate(new HostsPage());
            var attCard = CardWithHeader("Host cần chú ý", "Sắp xếp theo mức độ ảnh hưởng", out var ab, link);
            _attention = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.TopDown, WrapContents = false, AutoScroll = true, BackColor = Theme.Card };
            ab.Controls.Add(_attention);
            bottom.Controls.Add(Cell(attCard, bottom: 0), 0, 0);

            _openPill = Theme.Label("", Theme.Mono(8.5f, FontStyle.Bold), Theme.Red);
            var recCard = CardWithHeader("Cảnh báo gần đây", "5 sự kiện mới nhất", out var recb, _openPill);
            _recent = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.TopDown, WrapContents = false, BackColor = Theme.Card };
            var foot = new Panel { Dock = DockStyle.Bottom, Height = 30, BackColor = Theme.Card };
            var mttaLbl = Theme.Label("MTTA hôm nay", Theme.Sans(9.5f), Theme.Muted); mttaLbl.Location = new Point(0, 8);
            _mtta = Theme.Label("–", Theme.Mono(10, FontStyle.Bold), Theme.Text);
            foot.Controls.AddRange(new Control[] { mttaLbl, _mtta });
            foot.Layout += (s, e) => _mtta.Location = new Point(foot.Width - _mtta.Width, 8);
            recb.Controls.Add(foot);
            recb.Controls.Add(_recent);
            _recent.BringToFront();
            bottom.Controls.Add(Cell(recCard, right: 0, bottom: 0), 1, 0);
            root.Controls.Add(bottom, 0, 2);

            // các dòng trong danh sách co giãn theo chiều rộng thẻ
            Theme.FitRows(_attention);
            Theme.FitRows(_recent);
        }

        static Control Spacer() => new Panel { Dock = DockStyle.Top, Height = 10, BackColor = Theme.Card };

        public override async Task RefreshAsync()
        {
            var ovTask = Api.GetOverviewAsync();
            var alTask = Api.GetAlertsAsync(hours: 24);
            var o = await ovTask;
            var al = await alTask;
            if (IsDisposed) return;

            _total.Value = o.Total.ToString(); _total.Foot = "Đang được giám sát";
            _online.Value = o.Online.ToString(); _online.Foot = (o.Total == 0 ? 0 : o.Online * 100.0 / o.Total).ToString("0.0", Theme.Vi) + "% toàn hệ thống";
            _warn.Value = $"{o.Warning} / {o.Critical}"; _warn.ValueColor = o.Critical > 0 ? Theme.Red : o.Warning > 0 ? Theme.Amber : Theme.Text;
            _warn.Foot = al.Stats.Opening + " sự cố đang mở";
            _off.Value = o.Offline.ToString(); _off.Foot = o.Offline + " host mất kết nối";
            _health.Text = Theme.N1(o.HealthScore) + " / 100";

            _chart.Series["CPU"].Points.Clear();
            _chart.Series["RAM"].Points.Clear();
            foreach (var p in o.Series)
            {
                var t = p.T.ToLocalTime();
                _chart.Series["CPU"].Points.AddXY(t, p.Cpu);
                _chart.Series["RAM"].Points.AddXY(t, p.Ram);
            }
            _chart.ChartAreas[0].AxisX.LabelStyle.Format = "HH:mm";

            SetBar(_cpu, o.AvgCpu, Theme.N1(o.AvgCpu) + "%");
            SetBar(_ram, o.AvgRam, Theme.N1(o.AvgRam) + "%");
            SetBar(_disk, o.AvgDisk, Theme.N1(o.AvgDisk) + "%");
            SetBar(_net, Math.Min(100, o.TotalNetMbps / 100), Theme.Mbps(o.TotalNetMbps * 1000));

            _attention.SuspendLayout();
            _attention.Controls.Clear();
            if (o.Attention.Count == 0) _attention.Controls.Add(Theme.Label("Tất cả host đều ổn định ✓", Theme.Sans(10), Theme.Muted));
            foreach (var v in o.Attention)
            {
                var row = new HostRow(v);
                var id = v.Host.Id;
                row.Click += (s, e) => Main.Navigate(new HostDetailPage(id));
                _attention.Controls.Add(row);
            }
            _attention.ResumeLayout();

            _openPill.Text = "● " + al.Stats.Opening + " OPEN";
            _recent.SuspendLayout();
            _recent.Controls.Clear();
            foreach (var a in o.RecentAlerts) _recent.Controls.Add(new AlertRow(a));
            _recent.ResumeLayout();
            _mtta.Text = al.Stats.MttaSeconds > 0 ? Theme.Dur(al.Stats.MttaSeconds) : "–";
        }

        static void SetBar(BarRow b, double pct, string text)
        {
            b.Percent = pct;
            b.ValueText = text;
            b.Invalidate();
        }

        //Một dòng "Host cần chú ý": icon, tên + IP, mô tả, sparkline, giá trị
        class HostRow : Control
        {
            readonly HostView _v;
            public HostRow(HostView v)
            {
                SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.UserPaint | ControlStyles.ResizeRedraw, true);
                _v = v;
                Height = 54;
                Margin = new Padding(0, 0, 0, 6);
                Cursor = Cursors.Hand;
            }

            protected override void OnPaint(PaintEventArgs e)
            {
                var g = e.Graphics;
                g.Clear(Theme.Card);
                Draw.FillRound(g, new Rectangle(0, 0, Width - 1, Height - 1), 8, Theme.Card2);
                var st = _v.Status;
                var color = st == "CRITICAL" ? Theme.Red : st == "WARNING" ? Theme.Amber : Theme.Gray;
                Draw.FillRound(g, new Rectangle(12, 11, 32, 32), 8, Theme.Mix(color, Theme.Card2, 0.18));
                TextRenderer.DrawText(g, st == "OFFLINE" ? Theme.Icon.Offline : Theme.Icon.Server, Theme.IconFont(11), new Rectangle(12, 11, 32, 32), color,
                    TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
                var nameW = TextRenderer.MeasureText(g, _v.Host.Name, Theme.Mono(10.5f, FontStyle.Bold)).Width;
                TextRenderer.DrawText(g, _v.Host.Name, Theme.Mono(10.5f, FontStyle.Bold), new Point(56, 8), Theme.Text);
                TextRenderer.DrawText(g, _v.Host.IpAddress, Theme.Mono(8.5f), new Point(56 + nameW, 10), Theme.Dim);
                TextRenderer.DrawText(g, st == "OFFLINE" ? "Mất heartbeat" : (_v.Host.Description ?? _v.Host.OperatingSystem), Theme.Sans(8.5f), new Point(56, 29), Theme.Muted);

                var l = _v.Latest;
                var val = st == "OFFLINE" ? Theme.Ago(_v.LastSeen)
                    : Theme.Pct(Math.Max(l?.CpuUsagePercent ?? 0, Math.Max(l?.RamUsagePercent ?? 0, l?.DiskUsagePercent ?? 0)));
                var vf = Theme.Mono(st == "OFFLINE" ? 9 : 10.5f, FontStyle.Bold);
                var vw = TextRenderer.MeasureText(g, val, vf).Width;
                TextRenderer.DrawText(g, val, vf, new Point(Width - vw - 14, 17), color);

                var spark = _v.Spark;
                if (spark != null && spark.Length > 1)
                {
                    var box = new Rectangle(Width - vw - 130, 13, 100, 28);
                    double min = spark.Min(), max = spark.Max(), span = Math.Max(1e-6, max - min);
                    var pts = spark.Select((x, i) => new PointF(box.X + i * (box.Width - 1f) / (spark.Length - 1), (float)(box.Bottom - (x - min) / span * box.Height))).ToArray();
                    g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
                    using (var pen = new Pen(color, 1.6f)) g.DrawCurve(pen, pts, 0.4f);
                }
            }
        }

        //Một dòng "Cảnh báo gần đây"
        class AlertRow : Control
        {
            readonly AlertEvent _a;
            public AlertRow(AlertEvent a)
            {
                SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.UserPaint | ControlStyles.ResizeRedraw, true);
                _a = a;
                Height = 44;
                Margin = new Padding(0);
            }

            protected override void OnPaint(PaintEventArgs e)
            {
                var g = e.Graphics;
                g.Clear(Theme.Card);
                using (var p = new Pen(Theme.Line)) g.DrawLine(p, 0, Height - 1, Width, Height - 1);
                TextRenderer.DrawText(g, Theme.HHmmss(_a.TriggeredAt), Theme.Mono(9), new Point(4, 14), Theme.Muted);
                Draw.Dot(g, 80, 18, _a.Severity == AlertSeverity.Critical ? Theme.Red : Theme.Amber, 7);
                var text = $"{_a.Severity} {AlertMetric(_a.Metric)} · ";
                TextRenderer.DrawText(g, text, Theme.Sans(10), new Point(96, 12), Theme.Text);
                var tw = TextRenderer.MeasureText(g, text, Theme.Sans(10)).Width;
                TextRenderer.DrawText(g, _a.ServerName, Theme.Mono(10), new Point(92 + tw, 13), Theme.Text);
                var sw = TextRenderer.MeasureText(g, _a.Status, Theme.Mono(8, FontStyle.Bold)).Width;
                TextRenderer.DrawText(g, _a.Status, Theme.Mono(8, FontStyle.Bold), new Point(Width - sw - 4, 15), Theme.StatusColor(_a.Status));
            }
        }

        public static string AlertMetric(MetricType m) => Backend.Services.AlertEngineService.MetricLabel(m);
    }
}
