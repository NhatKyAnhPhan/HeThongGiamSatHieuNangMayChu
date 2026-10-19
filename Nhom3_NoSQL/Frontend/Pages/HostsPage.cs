using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using Nhom3_NoSQL.Backend.Api;
using Nhom3_NoSQL.Frontend.UI;

namespace Nhom3_NoSQL.Frontend.Pages
{
    //Màn hình Quản lý máy chủ: thống kê theo trạng thái, tìm kiếm, lọc, phân trang, xuất CSV, thêm/sửa/xóa
    public class HostsPage : PageBase
    {
        public override string Title => "Quản lý máy chủ";
        public override string Crumb => "Host inventory";

        const int PerPage = 9;
        static readonly string[] Statuses = { "", "ONLINE", "WARNING", "CRITICAL", "OFFLINE" };

        List<HostView> _data = new List<HostView>();
        readonly Dictionary<string, StatCard> _cards = new Dictionary<string, StatCard>();
        InputBox _search;
        ComboBox _status, _os, _region;
        DataGridView _grid;
        Label _pagerText;
        FlowLayoutPanel _pages;
        int _page = 1;
        bool _filling;

        public override IEnumerable<Control> Actions()
        {
            if (!Api.IsAdmin) yield break;
            var add = new RoundButton("Thêm máy chủ", RoundButton.Kind.Primary, Theme.Icon.Add);
            add.Click += async (s, e) => await EditHost(null);
            yield return add;
        }

        public override void Build()
        {
            var root = Rows(110, 56, -100);
            Controls.Add(root);

            var stats = Cols(-20, -20, -20, -20, -20);
            var defs = new[] { ("", "Tất cả host", Theme.Cyan), ("ONLINE", "Online", Theme.Green), ("WARNING", "Warning", Theme.Amber), ("CRITICAL", "Critical", Theme.Red), ("OFFLINE", "Offline", Theme.Gray) };
            for (int i = 0; i < defs.Length; i++)
            {
                var (key, title, color) = defs[i];
                var card = new StatCard(title, "●", color) { Height = 96 };
                card.ValueColor = color;
                card.MakeClickable((s, e) => { _status.SelectedIndex = Array.IndexOf(Statuses, key); });
                _cards[key] = card;
                stats.Controls.Add(Cell(card, right: i == 4 ? 0 : 14), i, 0);
            }
            root.Controls.Add(stats, 0, 0);

            _search = new InputBox(Theme.Icon.Search, "Tìm hostname, IP, OS... (Ctrl+K)") { Width = 300 };
            _status = Theme.Combo("Mọi trạng thái", "ONLINE", "WARNING", "CRITICAL", "OFFLINE");
            _os = Theme.Combo("Tất cả hệ điều hành"); _os.Width = 210;
            _region = Theme.Combo("Khu vực");
            var csv = new RoundButton("Xuất CSV", RoundButton.Kind.Normal, Theme.Icon.Download);
            csv.Click += (s, e) => ExportCsv();
            var bar = Toolbar(_search, _status, _os, _region);
            var barWrap = new Panel { Dock = DockStyle.Fill, BackColor = Theme.Bg };
            barWrap.Controls.Add(bar);
            barWrap.Controls.Add(csv);
            csv.BringToFront(); // nằm trên thanh công cụ (Dock Fill)
            csv.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            barWrap.Layout += (s, e) => csv.Location = new Point(barWrap.Width - csv.Width, 8);
            root.Controls.Add(barWrap, 0, 1);

            _search.Box.TextChanged += (s, e) => { _page = 1; Draw(); };
            foreach (var c in new[] { _status, _os, _region }) c.SelectedIndexChanged += (s, e) => { if (!_filling) { _page = 1; Draw(); } };

            var card2 = new RoundPanel { Padding = new Padding(14) };
            _grid = new DataGridView { Dock = DockStyle.Fill };
            Theme.StyleGrid(_grid);
            AddCol("name", "HOSTNAME / IP", 190);
            AddCol("status", "TRẠNG THÁI", 130);
            AddCol("os", "HỆ ĐIỀU HÀNH", 170);
            AddCol("cpu", "CPU", 70);
            AddCol("ram", "RAM", 70);
            AddCol("disk", "DISK", 70);
            AddCol("net", "NETWORK", 100);
            AddCol("seen", "LAST SEEN", 120);
            AddCol("act", "", 80);
            _grid.CellPainting += Grid_CellPainting;
            _grid.CellMouseClick += Grid_CellMouseClick;
            _grid.CellMouseMove += (s, e) => _grid.Cursor = e.RowIndex >= 0 ? Cursors.Hand : Cursors.Default;

            var pager = new Panel { Dock = DockStyle.Bottom, Height = 44, BackColor = Theme.Card };
            _pagerText = Theme.Label("", Theme.Sans(9), Theme.Muted); _pagerText.Location = new Point(4, 16);
            _pages = new FlowLayoutPanel { AutoSize = true, WrapContents = false, BackColor = Theme.Card, Anchor = AnchorStyles.Right | AnchorStyles.Top };
            pager.Controls.AddRange(new Control[] { _pagerText, _pages });
            pager.Layout += (s, e) => _pages.Location = new Point(pager.Width - _pages.Width, 10);
            card2.Controls.Add(pager);
            card2.Controls.Add(_grid);
            _grid.BringToFront();
            root.Controls.Add(Cell(card2, right: 0, bottom: 0), 0, 2);

            Main.KeyDown += Main_KeyDown;
            Disposed += (s, e) => Main.KeyDown -= Main_KeyDown;
        }

        void Main_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Control && e.KeyCode == Keys.K) { _search.Box.Focus(); e.SuppressKeyPress = true; }
        }

        void AddCol(string name, string header, int weight)
        {
            _grid.Columns.Add(new DataGridViewTextBoxColumn { Name = name, HeaderText = header, FillWeight = weight, SortMode = DataGridViewColumnSortMode.NotSortable });
        }

        public override async Task RefreshAsync()
        {
            var data = await Api.GetHostsAsync();
            if (IsDisposed) return;
            _data = data;
            _filling = true;
            Refill(_os, "Tất cả hệ điều hành", _data.Select(v => v.Host.OperatingSystem));
            Refill(_region, "Khu vực", _data.Select(v => v.Host.Region));
            _filling = false;
            Draw();
        }

        static void Refill(ComboBox c, string first, IEnumerable<string> values)
        {
            var cur = c.SelectedIndex > 0 ? (string)c.SelectedItem : null;
            var items = new[] { first }.Concat(values.Where(v => !string.IsNullOrEmpty(v)).Distinct().OrderBy(v => v)).ToArray();
            if (items.SequenceEqual(c.Items.Cast<string>())) return;
            c.Items.Clear();
            c.Items.AddRange(items);
            c.SelectedIndex = cur != null && items.Contains(cur) ? Array.IndexOf(items, cur) : 0;
        }

        List<HostView> Filtered()
        {
            var q = _search.Text.Trim().ToLowerInvariant();
            var status = Statuses[Math.Max(0, _status.SelectedIndex)];
            var os = _os.SelectedIndex > 0 ? (string)_os.SelectedItem : null;
            var region = _region.SelectedIndex > 0 ? (string)_region.SelectedItem : null;
            return _data.Where(v =>
                (q == "" || $"{v.Host.Name} {v.Host.IpAddress} {v.Host.OperatingSystem}".ToLowerInvariant().Contains(q)) &&
                (status == "" || v.Status == status) && (os == null || v.Host.OperatingSystem == os) && (region == null || v.Host.Region == region)).ToList();
        }

        void Draw()
        {
            var status = Statuses[Math.Max(0, _status.SelectedIndex)];
            foreach (var kv in _cards)
            {
                kv.Value.Value = (kv.Key == "" ? _data.Count : _data.Count(v => v.Status == kv.Key)).ToString();
                kv.Value.Selected = kv.Key != "" && kv.Key == status;
            }

            var rows = Filtered();
            int pages = Math.Max(1, (rows.Count + PerPage - 1) / PerPage);
            _page = Math.Min(_page, pages);
            var slice = rows.Skip((_page - 1) * PerPage).Take(PerPage).ToList();

            _grid.Rows.Clear();
            foreach (var v in slice)
            {
                var i = _grid.Rows.Add();
                _grid.Rows[i].Tag = v;
            }
            _pagerText.Text = rows.Count == 0 ? "Không có máy chủ phù hợp"
                : $"Hiển thị {(_page - 1) * PerPage + 1}–{(_page - 1) * PerPage + slice.Count} trong {rows.Count} máy chủ";

            _pages.Controls.Clear();
            AddPageButton("‹", _page - 1, _page > 1);
            for (int p = 1; p <= pages; p++) AddPageButton(p.ToString(), p, true, p == _page);
            AddPageButton("›", _page + 1, _page < pages);
        }

        void AddPageButton(string text, int page, bool enabled, bool on = false)
        {
            var b = new Label
            {
                Text = text, AutoSize = false, Size = new Size(30, 28), TextAlign = ContentAlignment.MiddleCenter, Font = Theme.Mono(9.5f),
                BackColor = on ? Theme.SelBg : Theme.Card, ForeColor = on ? Theme.Cyan : enabled ? Theme.Text2 : Theme.Dim,
                Cursor = enabled ? Cursors.Hand : Cursors.Default, Margin = new Padding(2, 0, 2, 0)
            };
            if (enabled) b.Click += (s, e) => { _page = page; Draw(); };
            _pages.Controls.Add(b);
        }

        // ---------------- Vẽ ô bảng ----------------
        void Grid_CellPainting(object sender, DataGridViewCellPaintingEventArgs e)
        {
            if (e.RowIndex < 0 || e.ColumnIndex < 0) return;
            var v = (HostView)_grid.Rows[e.RowIndex].Tag;
            var g = e.Graphics;
            var r = e.CellBounds;
            e.PaintBackground(r, true);
            var col = _grid.Columns[e.ColumnIndex].Name;
            bool live = v.Latest != null && v.Status != "OFFLINE";
            var mid = r.Y + r.Height / 2;
            switch (col)
            {
                case "name":
                    UI.Draw.Dot(g, r.X + 10, r.Y + 15, Theme.StatusColor(v.Status), 7);
                    TextRenderer.DrawText(g, v.Host.Name, Theme.Mono(10, FontStyle.Bold), new Point(r.X + 22, r.Y + 8), Theme.Text);
                    TextRenderer.DrawText(g, v.Host.IpAddress, Theme.Mono(8.5f), new Point(r.X + 22, r.Y + 28), Theme.Muted);
                    break;
                case "status":
                    UI.Draw.Pill(g, new Point(r.X + 10, mid - 11), v.Status, Theme.StatusColor(v.Status), _grid.DefaultCellStyle.BackColor);
                    break;
                case "os":
                    Txt(g, r, v.Host.OperatingSystem, Theme.Sans(10), Theme.Text);
                    break;
                case "cpu":
                    Txt(g, r, live ? Theme.Pct(v.Latest.CpuUsagePercent) : "–", Theme.Mono(10, FontStyle.Bold), live ? Theme.LevelColor(v.Latest.CpuUsagePercent, 80, 90) : Theme.Muted);
                    break;
                case "ram":
                    Txt(g, r, live ? Theme.Pct(v.Latest.RamUsagePercent) : "–", Theme.Mono(10, FontStyle.Bold), live ? Theme.LevelColor(v.Latest.RamUsagePercent, 82, 92) : Theme.Muted);
                    break;
                case "disk":
                    Txt(g, r, live ? Theme.Pct(v.Latest.DiskUsagePercent) : "–", Theme.Mono(10, FontStyle.Bold), live ? Theme.LevelColor(v.Latest.DiskUsagePercent, 85, 95) : Theme.Muted);
                    break;
                case "net":
                    Txt(g, r, live ? Theme.Mbps(v.Latest.NetworkInKbps + v.Latest.NetworkOutKbps) : "–", Theme.Mono(9), Theme.Text);
                    break;
                case "seen":
                    Txt(g, r, Theme.Ago(v.LastSeen), Theme.Sans(9), v.Status == "OFFLINE" ? Theme.Red : Theme.Muted);
                    break;
                case "act":
                    if (Api.IsAdmin)
                    {
                        TextRenderer.DrawText(g, Theme.Icon.Edit, Theme.IconFont(10.5f), new Point(r.X + 10, mid - 8), Theme.Muted);
                        TextRenderer.DrawText(g, Theme.Icon.Delete, Theme.IconFont(10.5f), new Point(r.X + 40, mid - 8), Theme.Red);
                    }
                    break;
            }
            e.Handled = true;
        }

        static void Txt(Graphics g, Rectangle r, string text, Font f, Color c) =>
            TextRenderer.DrawText(g, text, f, new Rectangle(r.X + 10, r.Y, r.Width - 12, r.Height), c, TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);

        async void Grid_CellMouseClick(object sender, DataGridViewCellMouseEventArgs e)
        {
            if (e.RowIndex < 0) return;
            var v = (HostView)_grid.Rows[e.RowIndex].Tag;
            if (_grid.Columns[e.ColumnIndex].Name == "act")
            {
                if (!Api.IsAdmin) return;
                if (e.X >= 6 && e.X < 32) await EditHost(v.Host);
                else if (e.X >= 36 && e.X < 62) await DeleteHost(v);
                return;
            }
            Main.Navigate(new HostDetailPage(v.Host.Id));
        }

        async Task EditHost(Backend.Models.ServerInfo host)
        {
            using (var dlg = new HostDialog(Api, host))
                if (dlg.ShowDialog(Main) == DialogResult.OK)
                {
                    Toast(host == null ? "Đã thêm máy chủ " + dlg.Result.Name : "Đã cập nhật máy chủ " + dlg.Result.Name);
                    await RefreshAsync();
                }
        }

        async Task DeleteHost(HostView v)
        {
            if (MessageBox.Show(Main, $"Xóa máy chủ {v.Host.Name}?\nQuy tắc ngưỡng riêng của host sẽ bị xóa và các sự cố đang mở được đóng lại.",
                    "Xác nhận xóa", MessageBoxButtons.YesNo, MessageBoxIcon.Warning) != DialogResult.Yes) return;
            await Try(async () => { await Api.DeleteHostAsync(v.Host.Id); await RefreshAsync(); }, "Đã xóa " + v.Host.Name);
        }

        void ExportCsv()
        {
            using (var dlg = new SaveFileDialog { Filter = "CSV (*.csv)|*.csv", FileName = $"hosts_{DateTime.Now:yyyyMMdd_HHmmss}.csv" })
            {
                if (dlg.ShowDialog(Main) != DialogResult.OK) return;
                File.WriteAllText(dlg.FileName, BuildCsv(Filtered()), new UTF8Encoding(true));
                Toast("Đã xuất " + Path.GetFileName(dlg.FileName));
            }
        }

        //Tạo nội dung CSV (tách riêng để kiểm thử được)
        public static string BuildCsv(IEnumerable<HostView> rows)
        {
            string Esc(object o) => "\"" + Convert.ToString(o, System.Globalization.CultureInfo.InvariantCulture).Replace("\"", "\"\"") + "\"";
            var sb = new StringBuilder("hostname,ip,status,os,cpu,ram,disk,network_kbps,last_seen\r\n");
            foreach (var v in rows)
                sb.Append(string.Join(",", new object[]
                {
                    v.Host.Name, v.Host.IpAddress, v.Status, v.Host.OperatingSystem, v.Latest?.CpuUsagePercent, v.Latest?.RamUsagePercent,
                    v.Latest?.DiskUsagePercent, v.Latest == null ? (double?)null : v.Latest.NetworkInKbps + v.Latest.NetworkOutKbps,
                    v.LastSeen?.ToString("o")
                }.Select(Esc))).Append("\r\n");
            return sb.ToString();
        }
    }
}
