using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Windows.Forms.DataVisualization.Charting;
using Nhom3_NoSQL.Backend.Api;
using Nhom3_NoSQL.Frontend.UI;

namespace Nhom3_NoSQL.Frontend.Pages
{
    //Màn hình Chi tiết máy chủ: thông tin host, 4 chỉ số hiện tại, 4 biểu đồ lịch sử (1h/6h/24h/7d)
    public class HostDetailPage : PageBase
    {
        readonly string _id;
        string _range = "6h";
        HostView _view;

        public HostDetailPage(string id) => _id = id;

        public override string Title => "Chi tiết máy chủ";
        public override string Crumb => "Host / " + (_view?.Host.Name ?? "…");
        public override string NavKey => "Máy chủ";

        Label _name, _desc, _ip, _os, _hostId, _seen, _heartbeat;
        HeadPill _pill;
        MetricCard _cpu, _ram, _disk, _net;
        readonly Dictionary<string, (Chart chart, Label now, Label sub)> _charts = new Dictionary<string, (Chart, Label, Label)>();

        public override IEnumerable<Control> Actions()
        {
            var back = new RoundButton("Máy chủ", RoundButton.Kind.Normal, Theme.Icon.Back);
            back.Click += (s, e) => Main.Navigate(new HostsPage());
            yield return back;
            if (!Api.IsAdmin) yield break;
            var edit = new RoundButton("Chỉnh sửa", RoundButton.Kind.Normal, Theme.Icon.Edit);
            edit.Click += async (s, e) =>
            {
                if (_view == null) return;
                using (var dlg = new HostDialog(Api, _view.Host))
                    if (dlg.ShowDialog(Main) == DialogResult.OK) { Toast("Đã cập nhật máy chủ"); await RefreshAsync(); }
            };
            yield return edit;
        }

        public override void Build()
        {
            var root = Rows(88, 118, 56, -100);
            Controls.Add(root);

            // --- Thẻ thông tin host
            var head = new RoundPanel { Padding = new Padding(16) };
            var icon = new Panel { Location = new Point(18, 18), Size = new Size(50, 50), BackColor = Theme.Card };
            icon.Paint += (s, e) =>
            {
                UI.Draw.FillRound(e.Graphics, new Rectangle(0, 0, 49, 49), 10, Theme.SelBg);
                TextRenderer.DrawText(e.Graphics, Theme.Icon.Server, Theme.IconFont(18), new Rectangle(0, 0, 50, 50), Theme.Cyan, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
            };
            _name = Theme.Label("", Theme.Mono(16, FontStyle.Bold), Theme.Text); _name.Location = new Point(80, 14);
            _pill = new HeadPill { Location = new Point(300, 20) };
            _desc = Theme.Label("", Theme.Sans(9.5f), Theme.Muted); _desc.Location = new Point(82, 48);
            head.Controls.AddRange(new Control[] { icon, _name, _pill, _desc });
            var kv = new FlowLayoutPanel { AutoSize = true, WrapContents = false, BackColor = Theme.Card, Anchor = AnchorStyles.Top | AnchorStyles.Right };
            _ip = KV(kv, "IP ADDRESS"); _os = KV(kv, "HỆ ĐIỀU HÀNH"); _hostId = KV(kv, "HOST ID"); _seen = KV(kv, "LAST SEEN"); _heartbeat = KV(kv, "HEARTBEAT");
            _hostId.Font = Theme.Mono(8.5f);
            head.Controls.Add(kv);
            head.Layout += (s, e) => { kv.Location = new Point(head.Width - kv.Width - 16, 20); _pill.Left = _name.Right + 10; };
            root.Controls.Add(Cell(head, right: 0), 0, 0);

            // --- 4 chỉ số hiện tại
            var cards = Cols(-25, -25, -25, -25);
            _cpu = new MetricCard("CPU sử dụng", Theme.Icon.Cpu, Theme.Cyan);
            _ram = new MetricCard("RAM sử dụng", Theme.Icon.Ram, Theme.Purple);
            _disk = new MetricCard("Disk sử dụng", Theme.Icon.Disk, Theme.Amber);
            _net = new MetricCard("Network I/O", Theme.Icon.Network, Theme.Green);
            cards.Controls.Add(Cell(_cpu), 0, 0); cards.Controls.Add(Cell(_ram), 1, 0);
            cards.Controls.Add(Cell(_disk), 2, 0); cards.Controls.Add(Cell(_net, right: 0), 3, 0);
            root.Controls.Add(cards, 0, 1);

            // --- Tiêu đề lịch sử + chọn khoảng thời gian
            var sec = new Panel { Dock = DockStyle.Fill, BackColor = Theme.Bg };
            var st = Theme.Label("Lịch sử hiệu năng", Theme.Semi(12), Theme.Text); st.Location = new Point(0, 4);
            var ss = Theme.Label("Gom nhóm trung bình theo khoảng thời gian · giờ UTC+7 · đường xám nét đứt = trung bình cả kỳ", Theme.Sans(8.5f), Theme.Muted); ss.Location = new Point(1, 28);
            var tabs = new Segmented(_range, "1h", "6h", "24h", "7d") { Anchor = AnchorStyles.Top | AnchorStyles.Right };
            tabs.Changed += async r => { _range = r; await Try(RefreshAsync); };
            sec.Controls.AddRange(new Control[] { st, ss, tabs });
            sec.Layout += (s, e) => tabs.Location = new Point(sec.Width - tabs.Width, 6);
            root.Controls.Add(sec, 0, 2);

            // --- 4 biểu đồ
            var grid = Rows(-50, -50);
            var r1 = Cols(-50, -50);
            var r2 = Cols(-50, -50);
            r1.Controls.Add(Cell(ChartCard("cpu", "CPU utilization", Theme.Cyan, 100)), 0, 0);
            r1.Controls.Add(Cell(ChartCard("ram", "Memory utilization", Theme.Purple, 100), right: 0), 1, 0);
            r2.Controls.Add(Cell(ChartCard("disk", "Disk usage", Theme.Amber, 100), bottom: 0), 0, 0);
            r2.Controls.Add(Cell(ChartCard("net", "Network throughput (Mbps)", Theme.Green, null), right: 0, bottom: 0), 1, 0);
            grid.Controls.Add(r1, 0, 0);
            grid.Controls.Add(r2, 0, 1);
            root.Controls.Add(grid, 0, 3);
        }

        static Label KV(FlowLayoutPanel kv, string caption)
        {
            var p = new Panel { Size = new Size(150, 46), BackColor = Theme.Card, Margin = new Padding(16, 0, 0, 0) };
            var c = Theme.Label(caption, Theme.Mono(7.5f, FontStyle.Bold), Theme.Muted); c.Location = new Point(0, 0);
            var v = Theme.Label("", Theme.Mono(10.5f, FontStyle.Bold), Theme.Text); v.Location = new Point(0, 18);
            p.Controls.AddRange(new Control[] { c, v });
            kv.Controls.Add(p);
            return v;
        }

        RoundPanel ChartCard(string key, string title, Color color, double? max)
        {
            var now = Theme.Label("", Theme.Mono(13, FontStyle.Bold), color);
            var card = CardWithHeader(title, "", out var body, out var sub, now);
            var chart = Theme.LineChart(max);
            Theme.LineSeries(chart, title, color, true);
            Theme.LineSeries(chart, "Trung bình", Theme.Gray, false, 1, ChartDashStyle.Dash);
            body.Controls.Add(chart);
            _charts[key] = (chart, now, sub);
            return card;
        }

        public override async Task RefreshAsync()
        {
            var vTask = Api.GetHostAsync(_id);
            var hTask = Api.GetHistoryAsync(_id, _range);
            var v = await vTask;
            var hist = await hTask;
            if (IsDisposed) return;
            _view = v;
            Main.SetCrumb(Crumb);

            var h = v.Host;
            var l = v.Latest;
            bool live = l != null && v.Status != "OFFLINE";
            _name.Text = h.Name;
            _pill.Status = v.Status;
            _desc.Text = $"{h.Description} · {h.Region}";
            _ip.Text = h.IpAddress;
            _os.Text = h.OperatingSystem;
            _hostId.Text = h.Id;
            _seen.Text = Theme.Ago(v.LastSeen);
            _heartbeat.Text = v.Status == "OFFLINE" ? "LOST" : "OK";
            _heartbeat.ForeColor = v.Status == "OFFLINE" ? Theme.Red : Theme.Green;
            PerformLayout();

            var pts = hist.Points;
            string Delta(Func<HistoryPoint, double> f)
            {
                if (pts.Count < 2) return "";
                double a = f(pts[pts.Count - 1]), b = f(pts[Math.Max(0, pts.Count - 6)]);
                double d = b == 0 ? 0 : (a - b) / b * 100;
                return (d >= 0 ? "↑ " : "↓ ") + Theme.N1(Math.Abs(d)) + "%";
            }
            _cpu.Set(live ? Theme.N1(l.CpuUsagePercent) + "%" : "–", h.CpuCores > 0 ? h.CpuCores + " vCPU" : "",
                live ? (l.CpuUsagePercent >= 80 ? "Vượt ngưỡng warning" : "Trong ngưỡng vận hành") : "Không có dữ liệu", live && l.CpuUsagePercent >= 80, Delta(p => p.Cpu));
            _ram.Set(live ? Theme.N1(l.RamUsagePercent) + "%" : "–", h.RamTotalGb > 0 && live ? $"{Theme.N1(h.RamTotalGb * l.RamUsagePercent / 100)} / {h.RamTotalGb} GB" : "",
                live ? (l.RamUsagePercent >= 82 ? "Vượt ngưỡng warning" : "Trong ngưỡng vận hành") : "Không có dữ liệu", live && l.RamUsagePercent >= 82, Delta(p => p.Ram));
            _disk.Set(live ? Theme.N1(l.DiskUsagePercent) + "%" : "–", h.DiskTotalGb > 0 && live ? $"{Math.Round(h.DiskTotalGb * l.DiskUsagePercent / 100)} / {h.DiskTotalGb} GB" : "",
                live ? (l.DiskUsagePercent >= 85 ? "Vượt ngưỡng warning" : "Trong ngưỡng vận hành") : "Không có dữ liệu", live && l.DiskUsagePercent >= 85, Delta(p => p.Disk));
            _net.Set(live ? Theme.Mbps(l.NetworkInKbps + l.NetworkOutKbps) : "–", live ? $"↓{Math.Round(l.NetworkInKbps / 1000)} ↑{Math.Round(l.NetworkOutKbps / 1000)}" : "",
                live ? "Mbps vào / ra" : "Không có dữ liệu", false, "");

            var s = hist.Summary;
            Fill("cpu", pts, p => p.Cpu, "%", s == null ? "Chưa có dữ liệu" : $"Peak {Theme.N1(s.CpuPeak)}% · Avg {Theme.N1(s.CpuAvg)}%");
            Fill("ram", pts, p => p.Ram, "%", s == null ? "" : $"Peak {Theme.N1(s.RamPeak)}% · Avg {Theme.N1(s.RamAvg)}%");
            Fill("disk", pts, p => p.Disk, "%", s == null ? "" : $"Peak {Theme.N1(s.DiskPeak)}% · Avg {Theme.N1(s.DiskAvg)}%");
            Fill("net", pts, p => Math.Round((p.NetIn + p.NetOut) / 1000), " Mbps", s == null ? "" : $"Peak {Theme.Mbps(s.NetPeak)} · Avg {Theme.Mbps(s.NetAvg)}");
        }

        //Vẽ 1 biểu đồ: đường chính (vùng tô) + đường trung bình cả kỳ (nét đứt)
        void Fill(string key, List<HistoryPoint> pts, Func<HistoryPoint, double> value, string unit, string sub)
        {
            var (chart, now, subLabel) = _charts[key];
            var main = chart.Series[0];
            var avg = chart.Series[1];
            main.Points.Clear();
            avg.Points.Clear();
            double mean = pts.Count == 0 ? 0 : Math.Round(pts.Average(value), 1);
            foreach (var p in pts)
            {
                var t = p.T.ToLocalTime();
                main.Points.AddXY(t, value(p));
                avg.Points.AddXY(t, mean);
            }
            chart.ChartAreas[0].AxisX.LabelStyle.Format = _range == "7d" ? "dd/MM HH:mm" : "HH:mm";
            now.Text = pts.Count == 0 ? "" : value(pts[pts.Count - 1]).ToString("0.#", Theme.Vi) + unit;
            subLabel.Text = sub;
        }

        //Nhãn trạng thái cạnh tên host
        class HeadPill : Control
        {
            string _status = "";
            public HeadPill() { SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.UserPaint, true); Size = new Size(120, 24); }
            public string Status { set { _status = value; Invalidate(); } }
            protected override void OnPaint(PaintEventArgs e)
            {
                e.Graphics.Clear(Theme.Card);
                if (_status != "") UI.Draw.Pill(e.Graphics, new Point(0, 1), _status, Theme.StatusColor(_status), Theme.Card);
            }
        }

        //Thẻ chỉ số hiện tại: icon + tên, ghi chú phải, giá trị lớn, dòng trạng thái + biến động
        class MetricCard : RoundPanel
        {
            readonly Label _value, _note, _foot, _delta;
            public MetricCard(string title, string icon, Color color)
            {
                var ic = Theme.IconLabel(icon, 11, color); ic.Location = new Point(14, 14);
                var t = Theme.Label(title, Theme.Sans(10), Theme.Text2); t.Location = new Point(38, 12);
                _note = Theme.Label("", Theme.Mono(8.5f), Theme.Muted);
                _value = Theme.Label("–", Theme.Mono(20, FontStyle.Bold), color); _value.Location = new Point(12, 38);
                _foot = Theme.Label("", Theme.Sans(8.5f), Theme.Muted); _foot.Location = new Point(15, 80);
                _delta = Theme.Label("", Theme.Mono(8.5f), Theme.Muted);
                Controls.AddRange(new Control[] { ic, t, _note, _value, _foot, _delta });
                Layout += (s, e) => { _note.Location = new Point(Width - _note.Width - 14, 14); _delta.Location = new Point(Width - _delta.Width - 14, 80); };
            }

            public void Set(string value, string note, string foot, bool warn, string delta)
            {
                _value.Text = value; _note.Text = note; _foot.Text = foot; _delta.Text = delta;
                _foot.ForeColor = warn ? Theme.Amber : Theme.Muted;
                PerformLayout();
            }
        }
    }
}
