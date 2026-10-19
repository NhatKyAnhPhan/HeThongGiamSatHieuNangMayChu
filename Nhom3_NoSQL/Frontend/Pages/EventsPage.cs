using System;
using System.Collections.Generic;
using System.Drawing;
using System.Threading.Tasks;
using System.Windows.Forms;
using Nhom3_NoSQL.Backend.Models;
using Nhom3_NoSQL.Frontend.UI;

namespace Nhom3_NoSQL.Frontend.Pages
{
    //Màn hình Nhật ký sự kiện (Event Log): tìm kiếm theo nội dung/host, lọc theo loại
    public class EventsPage : PageBase
    {
        static readonly string[] Types = { "", "ALERT", "HOST", "THRESHOLD", "AUTH" };

        readonly string _initialType;
        InputBox _search;
        ComboBox _type;
        DataGridView _grid;

        public EventsPage(string type = null) => _initialType = type;

        public override string Title => "Nhật ký sự kiện";
        public override string Crumb => "Event log / Audit";

        public override void Build()
        {
            var root = Rows(56, -100);
            Controls.Add(root);

            _search = new InputBox(Theme.Icon.Search, "Tìm theo nội dung hoặc host...") { Width = 320 };
            _type = Theme.Combo("Mọi loại sự kiện", "ALERT – cảnh báo", "HOST – máy chủ", "THRESHOLD – ngưỡng", "AUTH – đăng nhập");
            _type.Width = 220;
            _type.SelectedIndex = Math.Max(0, Array.IndexOf(Types, _initialType ?? ""));
            root.Controls.Add(Toolbar(_search, _type), 0, 0);
            var debounce = new Timer { Interval = 300 };
            debounce.Tick += async (s, e) => { debounce.Stop(); await Try(RefreshAsync); };
            _search.Box.TextChanged += (s, e) => { debounce.Stop(); debounce.Start(); };
            Disposed += (s, e) => debounce.Dispose();
            _type.SelectedIndexChanged += async (s, e) => await Try(RefreshAsync);

            var card = new RoundPanel { Padding = new Padding(14) };
            _grid = new DataGridView { Dock = DockStyle.Fill };
            Theme.StyleGrid(_grid);
            _grid.RowTemplate.Height = 44;
            foreach (var (name, header, w) in new[] { ("time", "THỜI GIAN", 175), ("type", "LOẠI", 90), ("level", "MỨC ĐỘ", 110), ("host", "HOST", 150), ("msg", "NỘI DUNG", 400), ("actor", "NGƯỜI THỰC HIỆN", 190) })
                _grid.Columns.Add(new DataGridViewTextBoxColumn { Name = name, HeaderText = header, FillWeight = w, SortMode = DataGridViewColumnSortMode.NotSortable });
            _grid.CellPainting += Grid_CellPainting;
            _grid.CellMouseClick += (s, e) =>
            {
                if (e.RowIndex < 0 || _grid.Columns[e.ColumnIndex].Name != "host") return;
                var ev = (EventLog)_grid.Rows[e.RowIndex].Tag;
                if (ev.ServerId != null) Main.Navigate(new HostDetailPage(ev.ServerId));
            };
            card.Controls.Add(_grid);
            root.Controls.Add(Cell(card, right: 0, bottom: 0), 0, 1);
        }

        public override async Task RefreshAsync()
        {
            var items = await Api.GetEventsAsync(_search.Text.Trim(), Types[Math.Max(0, _type.SelectedIndex)], 300);
            if (IsDisposed) return;
            var first = _grid.FirstDisplayedScrollingRowIndex;
            _grid.Rows.Clear();
            foreach (var ev in items) _grid.Rows[_grid.Rows.Add()].Tag = ev;
            if (first > 0 && first < _grid.Rows.Count) _grid.FirstDisplayedScrollingRowIndex = first;
        }

        void Grid_CellPainting(object sender, DataGridViewCellPaintingEventArgs e)
        {
            if (e.RowIndex < 0 || e.ColumnIndex < 0) return;
            var ev = (EventLog)_grid.Rows[e.RowIndex].Tag;
            var g = e.Graphics;
            var b = e.CellBounds;
            e.PaintBackground(b, true);
            var flags = TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis;
            var r = new Rectangle(b.X + 10, b.Y, b.Width - 14, b.Height);
            switch (_grid.Columns[e.ColumnIndex].Name)
            {
                case "time": TextRenderer.DrawText(g, Theme.Full(ev.CreatedAt), Theme.Mono(9), r, Theme.Muted, flags); break;
                case "type": TextRenderer.DrawText(g, ev.Type, Theme.Mono(9.5f), r, Theme.Text, flags); break;
                case "level":
                    var lv = ev.Level == "CRITICAL" ? "CRITICAL" : ev.Level == "WARNING" ? "WARNING" : "INFO";
                    UI.Draw.Pill(g, new Point(b.X + 10, b.Y + b.Height / 2 - 11), ev.Level ?? "INFO", Theme.StatusColor(lv), Theme.Card, dot: false);
                    break;
                case "host": TextRenderer.DrawText(g, ev.ServerName ?? "–", Theme.Mono(9.5f), r, ev.ServerId != null ? Theme.Cyan : Theme.Text, flags); break;
                case "msg": TextRenderer.DrawText(g, ev.Message, Theme.Sans(9.5f), r, Theme.Text, flags); break;
                case "actor": TextRenderer.DrawText(g, ev.Actor, Theme.Sans(9), r, Theme.Muted, flags); break;
            }
            e.Handled = true;
        }
    }
}
