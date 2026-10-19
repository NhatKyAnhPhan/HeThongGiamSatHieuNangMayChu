using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Linq;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace Nhom3_NoSQL.Frontend.UI
{
    //Các control tự vẽ để có giao diện bo góc, tông tối giống bản thiết kế
    public static class Draw
    {
        public static GraphicsPath Round(Rectangle r, int radius)
        {
            var p = new GraphicsPath();
            int d = radius * 2;
            if (radius <= 0 || r.Width < d || r.Height < d) { p.AddRectangle(r); return p; }
            p.AddArc(r.X, r.Y, d, d, 180, 90);
            p.AddArc(r.Right - d, r.Y, d, d, 270, 90);
            p.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90);
            p.AddArc(r.X, r.Bottom - d, d, d, 90, 90);
            p.CloseFigure();
            return p;
        }

        public static void FillRound(Graphics g, Rectangle r, int radius, Color fill, Color? border = null)
        {
            g.SmoothingMode = SmoothingMode.AntiAlias;
            using (var path = Round(r, radius))
            {
                using (var b = new SolidBrush(fill)) g.FillPath(b, path);
                if (border.HasValue) using (var pen = new Pen(border.Value)) g.DrawPath(pen, path);
            }
        }

        //Nhãn trạng thái dạng viên thuốc: ● ONLINE. Trả về chiều rộng đã vẽ
        public static int Pill(Graphics g, Point at, string text, Color color, Color bg, bool dot = true)
        {
            var f = Theme.Mono(8, FontStyle.Bold); // font dùng chung trong cache - không được Dispose
            var size = TextRenderer.MeasureText(g, text, f, Size.Empty, TextFormatFlags.NoPadding);
            int w = size.Width + (dot ? 26 : 16), h = 22;
            var r = new Rectangle(at.X, at.Y, w, h);
            FillRound(g, r, 5, Theme.Mix(color, bg, 0.12), Theme.Mix(color, bg, 0.45));
            int x = r.X + 8;
            if (dot)
            {
                using (var b = new SolidBrush(color)) g.FillEllipse(b, x, r.Y + 8, 6, 6);
                x += 11;
            }
            TextRenderer.DrawText(g, text, f, new Point(x, r.Y + (h - size.Height) / 2), color, TextFormatFlags.NoPadding);
            return w;
        }

        public static void Dot(Graphics g, int x, int y, Color c, int size = 8)
        {
            g.SmoothingMode = SmoothingMode.AntiAlias;
            using (var b = new SolidBrush(c)) g.FillEllipse(b, x, y, size, size);
        }
    }

    //Panel bo góc có viền (thẻ card)
    public class RoundPanel : Panel
    {
        public int Radius { get; set; } = 12;
        public Color FillColor { get; set; } = Theme.Card;
        public Color BorderColor { get; set; } = Theme.Line;

        public RoundPanel()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw | ControlStyles.UserPaint, true);
            BackColor = Theme.Bg;
            Padding = new Padding(16);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            e.Graphics.Clear(Parent?.BackColor ?? Theme.Bg);
            Draw.FillRound(e.Graphics, new Rectangle(0, 0, Width - 1, Height - 1), Radius, FillColor, BorderColor);
        }

        protected override void OnControlAdded(ControlEventArgs e)
        {
            base.OnControlAdded(e);
            // control con nằm trên nền card
            if (e.Control.BackColor == Color.Transparent || e.Control is Label) e.Control.BackColor = FillColor;
        }
    }

    //Thẻ thống kê: tiêu đề, số lớn, dòng phụ, icon góc phải
    public class StatCard : RoundPanel
    {
        readonly Label _title, _value, _foot, _icon;
        public StatCard(string title, string icon, Color accent)
        {
            Padding = new Padding(16, 12, 16, 12);
            _title = Theme.Label(title, Theme.Sans(10.5f), Theme.Text2);
            _value = Theme.Label("–", Theme.Mono(22), Theme.Text);
            _foot = Theme.Label("", Theme.Sans(9), accent);
            _icon = icon == "●" ? Theme.Label("●", Theme.Sans(10), accent) : Theme.IconLabel(icon ?? "", 13, accent);
            _title.Location = new Point(16, 12);
            _value.Location = new Point(13, 36);
            _foot.Location = new Point(16, 76);
            Controls.AddRange(new Control[] { _title, _value, _foot, _icon });
            Resize += (s, e) => _icon.Location = new Point(Width - _icon.Width - 16, 14);
            Height = 108;
        }

        public string Value { set { _value.Text = value; } }
        public Color ValueColor { set { _value.ForeColor = value; } }
        public string Foot { set { _foot.Text = value; } }
        public Color FootColor { set { _foot.ForeColor = value; } }
        public bool Selected { set { BorderColor = value ? Theme.Cyan : Theme.Line; Invalidate(); } }

        public void MakeClickable(EventHandler onClick)
        {
            Cursor = Cursors.Hand;
            Click += onClick;
            foreach (Control c in Controls) { c.Click += onClick; c.Cursor = Cursors.Hand; }
        }
    }

    //Nút bo góc phẳng
    public class RoundButton : Button
    {
        public enum Kind { Normal, Primary, Amber, Success, Danger }
        Kind _kind;
        string _icon;
        bool _hover;

        public RoundButton(string text, Kind kind = Kind.Normal, string icon = null)
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.UserPaint, true);
            Text = text;
            _kind = kind;
            _icon = icon;
            Font = Theme.Semi(10);
            Height = 36;
            Cursor = Cursors.Hand;
            FlatStyle = FlatStyle.Flat;
            FlatAppearance.BorderSize = 0;
            Margin = new Padding(0, 0, 8, 0);
            using (var g = CreateGraphics())
                Width = TextRenderer.MeasureText(g, text, Font).Width + (icon != null ? 52 : 32);
        }

        protected override void OnMouseEnter(EventArgs e) { _hover = true; Invalidate(); base.OnMouseEnter(e); }
        protected override void OnMouseLeave(EventArgs e) { _hover = false; Invalidate(); base.OnMouseLeave(e); }
        protected override void OnEnabledChanged(EventArgs e) { Invalidate(); base.OnEnabledChanged(e); }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.Clear(Parent?.BackColor ?? Theme.Bg);
            Color fill, fore, border;
            switch (_kind)
            {
                case Kind.Primary: fill = Theme.Cyan; fore = Theme.CyanInk; border = Theme.Cyan; break;
                case Kind.Amber: fill = Theme.Amber; fore = Theme.Hex("#2a1d00"); border = Theme.Amber; break;
                case Kind.Success: fill = Theme.Mix(Theme.Green, Theme.Card, 0.1); fore = Theme.Green; border = Theme.Mix(Theme.Green, Theme.Card, 0.6); break;
                case Kind.Danger: fill = Theme.Mix(Theme.Red, Theme.Card, 0.12); fore = Theme.Red; border = Theme.Mix(Theme.Red, Theme.Card, 0.5); break;
                default: fill = Theme.Card2; fore = Theme.Text; border = _hover ? Theme.Hex("#3a5262") : Theme.Line2; break;
            }
            if (_hover && (_kind == Kind.Primary || _kind == Kind.Amber)) fill = ControlPaint.Light(fill, 0.15f);
            if (!Enabled) { fill = Theme.Mix(fill, Theme.Card, 0.45); fore = Theme.Mix(fore, Theme.Card, 0.45); border = Theme.Mix(border, Theme.Card, 0.45); }
            Draw.FillRound(g, new Rectangle(0, 0, Width - 1, Height - 1), 8, fill, border);

            var textSize = TextRenderer.MeasureText(g, Text, Font);
            int iconW = _icon != null ? 22 : 0;
            int x = (Width - textSize.Width - iconW) / 2;
            if (_icon != null)
                TextRenderer.DrawText(g, _icon, Theme.IconFont(10), new Rectangle(x, 0, 20, Height), fore, TextFormatFlags.VerticalCenter | TextFormatFlags.Left);
            TextRenderer.DrawText(g, Text, Font, new Rectangle(x + iconW, 0, textSize.Width, Height), fore, TextFormatFlags.VerticalCenter | TextFormatFlags.Left);
        }
    }

    //Ô nhập có icon và chữ gợi ý (cue banner)
    public class InputBox : RoundPanel
    {
        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        static extern IntPtr SendMessage(IntPtr hWnd, int msg, IntPtr wParam, string lParam);
        const int EM_SETCUEBANNER = 0x1501;

        public TextBox Box { get; }
        readonly Label _icon;
        public Label Trailing { get; }

        public InputBox(string icon, string placeholder, bool password = false)
        {
            Radius = 8;
            FillColor = Theme.InputBg;
            BorderColor = Theme.Line2;
            Height = 40;
            Width = 280;
            Padding = new Padding(0);
            _icon = Theme.IconLabel(icon ?? "", 11, Theme.Muted);
            Box = new TextBox
            {
                BorderStyle = BorderStyle.None, BackColor = Theme.InputBg, ForeColor = Theme.Text, Font = Theme.Sans(10.5f),
                UseSystemPasswordChar = password
            };
            Trailing = Theme.Label("", Theme.IconFont(11), Theme.Muted);
            Controls.AddRange(new Control[] { _icon, Box, Trailing });
            Box.HandleCreated += (s, e) => SendMessage(Box.Handle, EM_SETCUEBANNER, (IntPtr)1, placeholder);
            // TextBox của .NET Framework không có sẵn Ctrl+A (chọn tất cả)
            Box.KeyDown += (s, e) => { if (e.Control && e.KeyCode == Keys.A) { Box.SelectAll(); e.SuppressKeyPress = true; } };
            Box.GotFocus += (s, e) => { BorderColor = Theme.Cyan; Invalidate(); };
            Box.LostFocus += (s, e) => { BorderColor = Theme.Line2; Invalidate(); };
            Click += (s, e) => Box.Focus();
            Resize += (s, e) => LayoutChildren();
            Trailing.SizeChanged += (s, e) => LayoutChildren();
            LayoutChildren();
        }

        void LayoutChildren()
        {
            int left = string.IsNullOrEmpty(_icon.Text) ? 12 : 36;
            _icon.Location = new Point(11, (Height - _icon.Height) / 2);
            Trailing.Location = new Point(Width - Trailing.Width - 10, (Height - Trailing.Height) / 2);
            Box.Location = new Point(left, (Height - Box.Height) / 2);
            Box.Width = Width - left - (string.IsNullOrEmpty(Trailing.Text) ? 12 : Trailing.Width + 18);
        }

        public override string Text { get => Box.Text; set => Box.Text = value; }
    }

    //ComboBox tông tối (tự vẽ các mục, vì ComboBox chuẩn bỏ qua BackColor ở dạng DropDownList)
    public class DarkCombo : ComboBox
    {
        public DarkCombo()
        {
            DrawMode = DrawMode.OwnerDrawFixed;
            DropDownStyle = ComboBoxStyle.DropDownList;
            FlatStyle = FlatStyle.Flat;
            BackColor = Theme.InputBg;
            ForeColor = Theme.Text;
            Font = Theme.Sans(10);
            ItemHeight = 28;
            Theme.DarkScroll(this);
        }

        protected override void OnDrawItem(DrawItemEventArgs e)
        {
            if (e.Index < 0) return;
            bool edit = (e.State & DrawItemState.ComboBoxEdit) != 0;
            bool sel = !edit && (e.State & DrawItemState.Selected) != 0;
            using (var b = new SolidBrush(sel ? Theme.SelBg : Theme.InputBg)) e.Graphics.FillRectangle(b, e.Bounds);
            TextRenderer.DrawText(e.Graphics, Convert.ToString(Items[e.Index]), Font,
                new Rectangle(e.Bounds.X + 8, e.Bounds.Y, e.Bounds.Width - 8, e.Bounds.Height), sel ? Theme.Cyan : Theme.Text,
                TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);
        }
    }

    //Một dòng tài nguyên: tên, giá trị, thanh tiến trình
    public class BarRow : Control
    {
        public string Caption { get; set; }
        public string ValueText { get; set; }
        public string Note { get; set; }
        public double Percent { get; set; }
        public Color BarColor { get; set; }

        public BarRow()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.UserPaint | ControlStyles.ResizeRedraw, true);
            Height = 46;
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.Clear(Theme.Card);
            TextRenderer.DrawText(g, Caption, Theme.Sans(10), new Point(0, 2), Theme.Text);
            var vf = Theme.Mono(10, FontStyle.Bold); // font dùng chung trong cache - không được Dispose
            var vw = TextRenderer.MeasureText(g, ValueText, vf).Width;
            TextRenderer.DrawText(g, ValueText, vf, new Point(Width - vw, 2), BarColor);
            if (!string.IsNullOrEmpty(Note))
            {
                var nw = TextRenderer.MeasureText(g, Note, Theme.Mono(8.5f)).Width;
                TextRenderer.DrawText(g, Note, Theme.Mono(8.5f), new Point(Width - vw - nw - 4, 4), Theme.Muted);
            }
            var track = new Rectangle(0, 28, Width - 1, 6);
            Draw.FillRound(g, track, 3, Theme.Line);
            var w = (int)(track.Width * Math.Max(0, Math.Min(100, Percent)) / 100);
            if (w > 6) Draw.FillRound(g, new Rectangle(0, 28, w, 6), 3, BarColor);
        }
    }

    //Đồ thị mini (sparkline) cho danh sách host
    public class Sparkline : Control
    {
        public double[] Values { get; set; }
        public Color LineColor { get; set; } = Theme.Cyan;

        public Sparkline()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.UserPaint, true);
            Size = new Size(90, 28);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            e.Graphics.Clear(Parent?.BackColor ?? Theme.Card2);
            if (Values == null || Values.Length < 2) return;
            double min = Values.Min(), max = Values.Max(), span = Math.Max(1e-6, max - min);
            var pts = Values.Select((v, i) => new PointF(i * (Width - 2f) / (Values.Length - 1) + 1, (float)(Height - 3 - (v - min) / span * (Height - 6)))).ToArray();
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            using (var pen = new Pen(LineColor, 1.6f)) e.Graphics.DrawCurve(pen, pts, 0.4f);
        }
    }

    //Nhóm nút chọn (1h / 6h / 24h / 7d)
    public class Segmented : FlowLayoutPanel
    {
        public event Action<string> Changed;
        public string Selected { get; private set; }

        public Segmented(string selected, params string[] items)
        {
            AutoSize = true;
            WrapContents = false;
            BackColor = Theme.Card;
            Padding = new Padding(3);
            Margin = new Padding(0);
            Selected = selected;
            foreach (var it in items)
            {
                var b = new Label
                {
                    Text = it, AutoSize = false, Size = new Size(54, 28), TextAlign = ContentAlignment.MiddleCenter, Font = Theme.Mono(9.5f),
                    Cursor = Cursors.Hand, Margin = new Padding(1), Tag = it
                };
                b.Click += (s, e) => { Selected = (string)((Control)s).Tag; Paint2(); Changed?.Invoke(Selected); };
                Controls.Add(b);
            }
            Paint2();
        }

        void Paint2()
        {
            foreach (Control c in Controls)
            {
                bool on = (string)c.Tag == Selected;
                c.BackColor = on ? Theme.SelBg : Theme.Card;
                c.ForeColor = on ? Theme.Cyan : Theme.Muted;
            }
        }
    }

    //Thông báo nổi góc dưới phải, tự ẩn sau 3,5 giây
    public class Toast : Label
    {
        readonly Timer _timer = new Timer { Interval = 3500 };
        public Toast()
        {
            AutoSize = true;
            Padding = new Padding(14, 10, 14, 10);
            Font = Theme.Sans(10);
            Visible = false;
            BorderStyle = BorderStyle.FixedSingle;
            _timer.Tick += (s, e) => { _timer.Stop(); Visible = false; };
        }

        public void Show(string message, bool error = false)
        {
            Text = message;
            ForeColor = error ? Theme.Red : Theme.Green;
            BackColor = Theme.Card2;
            Visible = true;
            BringToFront();
            if (Parent != null) Location = new Point(Parent.ClientSize.Width - Width - 24, Parent.ClientSize.Height - Height - 24);
            _timer.Stop();
            _timer.Start();
        }
    }
}
