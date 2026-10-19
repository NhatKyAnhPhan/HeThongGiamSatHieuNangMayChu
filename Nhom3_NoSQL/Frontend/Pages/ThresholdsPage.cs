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
    //Màn hình Cấu hình ngưỡng cảnh báo: bảng quy tắc Warning/Critical/duration, bật/tắt, tạo/sửa/nhân bản
    public class ThresholdsPage : PageBase
    {
        public override string Title => "Cấu hình ngưỡng cảnh báo";
        public override string Crumb => "Alerting / Threshold rules";

        public static readonly Dictionary<MetricType, string> MetricNames = new Dictionary<MetricType, string>
        {
            [MetricType.Cpu] = "CPU utilization", [MetricType.Ram] = "Memory utilization", [MetricType.Disk] = "Disk utilization",
            [MetricType.NetworkIn] = "Network in", [MetricType.NetworkOut] = "Network out", [MetricType.Heartbeat] = "Heartbeat missed",
        };

        List<AlertRule> _rules = new List<AlertRule>();
        List<HostView> _hosts = new List<HostView>();
        StatCard _on, _global, _hostRules;
        InputBox _search;
        ComboBox _scope, _metric, _enabled;
        DataGridView _grid;

        public override IEnumerable<Control> Actions()
        {
            if (!Api.IsAdmin) yield break;
            var add = new RoundButton("Tạo quy tắc", RoundButton.Kind.Primary, Theme.Icon.Add);
            add.Click += async (s, e) => await Edit(null);
            yield return add;
        }

        public override void Build()
        {
            var root = Rows(122, 86, 56, -100);
            Controls.Add(root);

            var stats = Cols(-33.3f, -33.3f, -33.4f);
            _on = new StatCard("Quy tắc đang bật", Theme.Icon.Shield, Theme.Green);
            _global = new StatCard("Phạm vi toàn hệ thống", Theme.Icon.Globe, Theme.Cyan) { };
            _hostRules = new StatCard("Ngoại lệ theo host", Theme.Icon.Layers, Theme.Purple);
            _global.FootColor = _hostRules.FootColor = Theme.Muted;
            stats.Controls.Add(Cell(_on), 0, 0); stats.Controls.Add(Cell(_global), 1, 0); stats.Controls.Add(Cell(_hostRules, right: 0), 2, 0);
            root.Controls.Add(stats, 0, 0);

            var banner = new RoundPanel();
            var bi = new Panel { Location = new Point(16, 18), Size = new Size(38, 38), BackColor = Theme.Card };
            bi.Paint += (s, e) =>
            {
                UI.Draw.FillRound(e.Graphics, new Rectangle(0, 0, 37, 37), 8, Theme.Mix(Theme.Cyan, Theme.Card, 0.15));
                TextRenderer.DrawText(e.Graphics, Theme.Icon.Info, Theme.IconFont(12), new Rectangle(0, 0, 38, 38), Theme.Cyan, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
            };
            var bt = Theme.Label("Cách ServerPulse kích hoạt cảnh báo", Theme.Semi(10.5f), Theme.Text); bt.Location = new Point(68, 14);
            var bs = Theme.Label("Chỉ số phải vượt ngưỡng liên tục trong duration_seconds (cửa sổ trượt) trước khi tạo sự cố. Quy tắc theo host luôn ưu tiên hơn cấu hình toàn hệ thống.",
                Theme.Sans(9), Theme.Muted); bs.Location = new Point(68, 38);
            var flow = new FlowPills { Anchor = AnchorStyles.Top | AnchorStyles.Right };
            banner.Controls.AddRange(new Control[] { bi, bt, bs, flow });
            banner.Layout += (s, e) => { flow.Location = new Point(banner.Width - flow.Width - 16, 26); bs.MaximumSize = new Size(Math.Max(200, flow.Left - 80), 0); };
            root.Controls.Add(Cell(banner, right: 0), 0, 1);

            _search = new InputBox(Theme.Icon.Search, "Tìm host hoặc metric...") { Width = 260 };
            _scope = Theme.Combo("Mọi phạm vi", "Toàn hệ thống", "Riêng host");
            _metric = Theme.Combo(new[] { "Mọi tài nguyên" }.Concat(MetricNames.Values).ToArray());
            _enabled = Theme.Combo("Bật & tắt", "Đang bật", "Đang tắt");
            var note = Theme.Label("Thay đổi có hiệu lực ở lần thu thập kế tiếp", Theme.Sans(9), Theme.Muted);
            note.Margin = new Padding(10, 10, 0, 0);
            root.Controls.Add(Toolbar(_search, _scope, _metric, _enabled, note), 0, 2);
            _search.Box.TextChanged += (s, e) => Draw();
            foreach (var c in new[] { _scope, _metric, _enabled }) c.SelectedIndexChanged += (s, e) => Draw();

            var card = new RoundPanel { Padding = new Padding(14) };
            _grid = new DataGridView { Dock = DockStyle.Fill };
            Theme.StyleGrid(_grid);
            foreach (var (name, header, w) in new[] { ("scope", "PHẠM VI ÁP DỤNG", 170), ("metric", "METRIC", 170), ("warn", "WARNING THRESHOLD", 150),
                         ("crit", "CRITICAL THRESHOLD", 150), ("dur", "DURATION_SECONDS", 120), ("impact", "ẢNH HƯỞNG", 100), ("on", "BẬT / TẮT", 80), ("act", "", 70) })
                _grid.Columns.Add(new DataGridViewTextBoxColumn { Name = name, HeaderText = header, FillWeight = w, SortMode = DataGridViewColumnSortMode.NotSortable });
            _grid.CellPainting += Grid_CellPainting;
            _grid.CellMouseClick += Grid_CellMouseClick;
            card.Controls.Add(_grid);
            root.Controls.Add(Cell(card, right: 0, bottom: 0), 0, 3);
        }

        public override async Task RefreshAsync()
        {
            var rTask = Api.GetThresholdsAsync();
            var hTask = Api.GetHostsAsync();
            var rules = await rTask;
            var hosts = await hTask;
            if (IsDisposed) return;
            _rules = rules;
            _hosts = hosts;
            Draw();
        }

        void Draw()
        {
            int on = _rules.Count(r => r.IsEnabled);
            var hostRules = _rules.Where(r => r.ServerId != null).ToList();
            _on.Value = $"{on} / {_rules.Count}";
            _on.Foot = (_rules.Count == 0 ? 0 : on * 100.0 / _rules.Count).ToString("0.0", Theme.Vi) + "% chính sách đang được áp dụng";
            _global.Value = _rules.Count(r => r.ServerId == null) + " quy tắc";
            _global.Foot = $"Áp dụng mặc định cho {_hosts.Count} host";
            _hostRules.Value = hostRules.Count + " quy tắc";
            _hostRules.Foot = $"Trên {hostRules.Select(r => r.ServerId).Distinct().Count()} host quan trọng";

            var q = _search.Text.Trim().ToLowerInvariant();
            var metric = _metric.SelectedIndex > 0 ? MetricNames.Keys.ElementAt(_metric.SelectedIndex - 1) : (MetricType?)null;
            var rows = _rules.Where(r =>
                    (q == "" || $"{r.ServerName ?? "toàn hệ thống"} {MetricNames[r.Metric]}".ToLowerInvariant().Contains(q)) &&
                    (_scope.SelectedIndex == 0 || (_scope.SelectedIndex == 1) == (r.ServerId == null)) &&
                    (metric == null || r.Metric == metric) &&
                    (_enabled.SelectedIndex == 0 || (_enabled.SelectedIndex == 1) == r.IsEnabled))
                .OrderBy(r => r.ServerId != null).ThenBy(r => r.ServerName).ToList();

            _grid.Rows.Clear();
            foreach (var r in rows) _grid.Rows[_grid.Rows.Add()].Tag = r;
        }

        void Grid_CellPainting(object sender, DataGridViewCellPaintingEventArgs e)
        {
            if (e.RowIndex < 0 || e.ColumnIndex < 0) return;
            var r = (AlertRule)_grid.Rows[e.RowIndex].Tag;
            var g = e.Graphics;
            var b = e.CellBounds;
            e.PaintBackground(b, true);
            int mid = b.Y + b.Height / 2;
            Color Fade(Color c) => r.IsEnabled ? c : Theme.Mix(c, Theme.Card, 0.45);
            switch (_grid.Columns[e.ColumnIndex].Name)
            {
                case "scope":
                    TextRenderer.DrawText(g, r.ServerId == null ? Theme.Icon.Globe : Theme.Icon.Server, Theme.IconFont(10), new Point(b.X + 10, mid - 8), Fade(r.ServerId == null ? Theme.Cyan : Theme.Purple));
                    TextRenderer.DrawText(g, r.ServerName ?? "Toàn hệ thống", r.ServerId == null ? Theme.Semi(10) : Theme.Mono(10, FontStyle.Bold),
                        new Point(b.X + 32, mid - 9), Fade(Theme.Text));
                    break;
                case "metric":
                    TextRenderer.DrawText(g, MetricNames[r.Metric], Theme.Sans(10), new Point(b.X + 10, b.Y + 8), Fade(Theme.Text));
                    TextRenderer.DrawText(g, "Đơn vị: " + r.Unit, Theme.Sans(8.5f), new Point(b.X + 10, b.Y + 28), Fade(Theme.Muted));
                    break;
                case "warn": Box(g, b, r.ThresholdValue, r.Unit, Fade(Theme.Amber)); break;
                case "crit": Box(g, b, r.CriticalValue, r.Unit, Fade(Theme.Red)); break;
                case "dur":
                    TextRenderer.DrawText(g, Theme.Icon.Clock + " ", Theme.IconFont(9), new Point(b.X + 10, mid - 7), Fade(Theme.Muted));
                    TextRenderer.DrawText(g, r.DurationSeconds + "s", Theme.Mono(10), new Point(b.X + 28, mid - 8), Fade(Theme.Muted));
                    break;
                case "impact":
                    TextRenderer.DrawText(g, r.ServerId == null ? _hosts.Count + " host" : "Riêng host", Theme.Sans(9.5f), new Point(b.X + 10, mid - 9), Fade(Theme.Muted));
                    break;
                case "on":
                    var track = new Rectangle(b.X + 12, mid - 10, 38, 20);
                    var trackColor = r.IsEnabled ? Theme.Cyan : Theme.Hex("#2a3a46");
                    if (!Api.IsAdmin) trackColor = Theme.Mix(trackColor, Theme.Card, 0.55);
                    UI.Draw.FillRound(g, track, 10, trackColor);
                    UI.Draw.Dot(g, r.IsEnabled ? track.Right - 18 : track.X + 2, track.Y + 2, Color.White, 16);
                    break;
                case "act":
                    if (Api.IsAdmin)
                    {
                        TextRenderer.DrawText(g, Theme.Icon.Edit, Theme.IconFont(10.5f), new Point(b.X + 10, mid - 8), Theme.Muted);
                        TextRenderer.DrawText(g, Theme.Icon.Copy, Theme.IconFont(10.5f), new Point(b.X + 40, mid - 8), Theme.Muted);
                    }
                    break;
            }
            e.Handled = true;
        }

        static void Box(Graphics g, Rectangle b, double value, string unit, Color c)
        {
            var r = new Rectangle(b.X + 10, b.Y + b.Height / 2 - 15, Math.Min(150, b.Width - 20), 30);
            UI.Draw.FillRound(g, r, 6, Theme.Mix(c, Theme.Card, 0.14), Theme.Mix(c, Theme.Card, 0.4));
            TextRenderer.DrawText(g, value.ToString("#,0.##", Theme.Vi), Theme.Mono(10, FontStyle.Bold), new Rectangle(r.X + 10, r.Y, r.Width, r.Height), ControlPaint.Light(c, 0.3f), TextFormatFlags.VerticalCenter);
            TextRenderer.DrawText(g, unit, Theme.Mono(8), new Rectangle(r.X, r.Y, r.Width - 10, r.Height), c, TextFormatFlags.VerticalCenter | TextFormatFlags.Right);
        }

        async void Grid_CellMouseClick(object sender, DataGridViewCellMouseEventArgs e)
        {
            if (e.RowIndex < 0 || !Api.IsAdmin) return;
            var r = (AlertRule)_grid.Rows[e.RowIndex].Tag;
            var col = _grid.Columns[e.ColumnIndex].Name;
            if (col == "on")
            {
                var copy = Clone(r);
                copy.IsEnabled = !r.IsEnabled;
                await Try(async () => { await Api.SaveThresholdAsync(copy); await RefreshAsync(); },
                    $"{(copy.IsEnabled ? "Bật" : "Tắt")} quy tắc {MetricNames[r.Metric]}");
            }
            else if (col == "act")
            {
                if (e.X < 34) await Edit(r);
                else if (e.X < 64) { var dup = Clone(r); dup.Id = null; await Edit(dup); }
            }
        }

        static AlertRule Clone(AlertRule r) => new AlertRule
        {
            Id = r.Id, ServerId = r.ServerId, ServerName = r.ServerName, Metric = r.Metric, Unit = r.Unit, Operator = r.Operator,
            ThresholdValue = r.ThresholdValue, CriticalValue = r.CriticalValue, DurationSeconds = r.DurationSeconds, IsEnabled = r.IsEnabled
        };

        async Task Edit(AlertRule rule)
        {
            using (var dlg = new RuleDialog(Api, rule, _hosts))
                if (dlg.ShowDialog(Main) == DialogResult.OK)
                {
                    Toast("Đã lưu quy tắc");
                    await RefreshAsync();
                }
        }

        //WARNING → CRITICAL → OPENING
        class FlowPills : Control
        {
            public FlowPills() { SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.UserPaint, true); Size = new Size(330, 24); }
            protected override void OnPaint(PaintEventArgs e)
            {
                var g = e.Graphics;
                g.Clear(Theme.Card);
                int x = 0;
                x += UI.Draw.Pill(g, new Point(x, 1), "WARNING", Theme.Amber, Theme.Card) + 8;
                TextRenderer.DrawText(g, "→", Theme.Sans(10), new Point(x, 2), Theme.Muted); x += 22;
                x += UI.Draw.Pill(g, new Point(x, 1), "CRITICAL", Theme.Red, Theme.Card) + 8;
                TextRenderer.DrawText(g, "→", Theme.Sans(10), new Point(x, 2), Theme.Muted); x += 22;
                UI.Draw.Pill(g, new Point(x, 1), "OPENING", Theme.Cyan, Theme.Card);
            }
        }
    }
}
