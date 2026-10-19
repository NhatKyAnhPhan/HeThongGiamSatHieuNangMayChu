using System;
using System.Drawing;
using System.Globalization;
using System.Windows.Forms;
using System.Windows.Forms.DataVisualization.Charting;

namespace Nhom3_NoSQL.Frontend.UI
{
    //Bảng màu, font, icon và hàm định dạng dùng chung cho toàn bộ giao diện (tông tối theo bản thiết kế)
    public static class Theme
    {
        public static readonly Color Bg = Hex("#0a131a");
        public static readonly Color Panel = Hex("#0f1c25");
        public static readonly Color Card = Hex("#111f29");
        public static readonly Color Card2 = Hex("#142430");
        public static readonly Color Line = Hex("#1d2e3a");
        public static readonly Color Line2 = Hex("#263946");
        public static readonly Color Text = Hex("#e6edf3");
        public static readonly Color Text2 = Hex("#c3ced8");
        public static readonly Color Muted = Hex("#7d8b99");
        public static readonly Color Dim = Hex("#55636f");
        public static readonly Color Cyan = Hex("#22d3ee");
        public static readonly Color CyanInk = Hex("#06202a");
        public static readonly Color Purple = Hex("#a78bfa");
        public static readonly Color Amber = Hex("#fbbf24");
        public static readonly Color Green = Hex("#34d399");
        public static readonly Color Red = Hex("#f87171");
        public static readonly Color Gray = Hex("#8b98a5");
        public static readonly Color SelBg = Hex("#123441");
        public static readonly Color SelLine = Hex("#1f5566");
        public static readonly Color InputBg = Hex("#0d1820");

        public static Color Hex(string hex) => ColorTranslator.FromHtml(hex);
        public static Color Alpha(Color c, int a) => Color.FromArgb(a, c);
        //Pha màu c lên nền bg với tỉ lệ (dùng cho nền nhạt của nhãn trạng thái, tránh alpha khi vẽ trên control)
        public static Color Mix(Color c, Color bg, double ratio) => Color.FromArgb(
            (int)(c.R * ratio + bg.R * (1 - ratio)), (int)(c.G * ratio + bg.G * (1 - ratio)), (int)(c.B * ratio + bg.B * (1 - ratio)));

        // ---------------- Font ----------------
        // Font được cache: các hàm vẽ gọi liên tục mà tạo Font mới sẽ rò rỉ handle GDI
        static readonly System.Collections.Generic.Dictionary<string, Font> FontCache = new System.Collections.Generic.Dictionary<string, Font>();
        static Font F(string family, float size, FontStyle style)
        {
            var key = family + "|" + size + "|" + style;
            lock (FontCache)
            {
                if (!FontCache.TryGetValue(key, out var f)) FontCache[key] = f = new Font(family, size, style);
                return f;
            }
        }
        public static Font Sans(float size, FontStyle style = FontStyle.Regular) => F("Segoe UI", size, style);
        public static Font Semi(float size) => F("Segoe UI Semibold", size, FontStyle.Regular);
        public static Font Mono(float size, FontStyle style = FontStyle.Regular) => F("Consolas", size, style);
        public static Font IconFont(float size) => F("Segoe MDL2 Assets", size, FontStyle.Regular);

        //Mã ký tự icon trong font Segoe MDL2 Assets (có sẵn trên Windows 10/11)
        public static class Icon
        {
            public const string Pulse = "", Grid = "", Server = "", Sliders = "", Siren = "",
                List = "", Bell = "", Refresh = "", Add = "", Search = "", Mail = "",
                Lock = "", Eye = "", EyeOff = "", Shield = "", Check = "", CheckCircle = "",
                Warning = "", Offline = "", Edit = "", Delete = "", Copy = "", Download = "",
                Info = "", Globe = "", Layers = "", Cpu = "", Ram = "", Disk = "",
                Network = "", Clock = "", Hand = "", Bulb = "", History = "", Logout = "",
                Antenna = "", Back = "", Arrow = "";
        }

        public static Color StatusColor(string status)
        {
            switch (status)
            {
                case "ONLINE": case "RESOLVED": return Green;
                case "WARNING": case "ACKNOWLEDGED": return Amber;
                case "CRITICAL": case "OPENING": return Red;
                case "INFO": return Cyan;
                default: return Gray;
            }
        }

        public static Color LevelColor(double value, double warn, double crit) =>
            value >= crit ? Red : value >= warn ? Amber : Text;

        // ---------------- Định dạng (theo văn hóa vi-VN: 34,2%) ----------------
        public static readonly CultureInfo Vi = new CultureInfo("vi-VN");
        public static string N1(double v) => v.ToString("0.0", Vi);
        public static string Pct(double v) => Math.Round(v).ToString("0", Vi) + "%";
        public static string Mbps(double kbps)
        {
            var m = kbps / 1000;
            return m >= 1000 ? (m / 1000).ToString("0.0", Vi) + " Gbps" : Math.Round(m).ToString("0", Vi) + " Mbps";
        }
        public static string Ago(DateTime? utc)
        {
            if (utc == null) return "chưa có dữ liệu";
            var s = Math.Max(0, (int)(DateTime.UtcNow - utc.Value.ToUniversalTime()).TotalSeconds);
            if (s < 60) return s + " giây trước";
            if (s < 3600) return s / 60 + " phút trước";
            if (s < 86400) return s / 3600 + " giờ trước";
            return s / 86400 + " ngày trước";
        }
        public static string Dur(double sec) => sec >= 60 ? $"{(int)(sec / 60)}m{(int)(sec % 60):00}s" : $"{(int)sec}s";
        public static string HHmm(DateTime utc) => utc.ToLocalTime().ToString("HH:mm");
        public static string HHmmss(DateTime utc) => utc.ToLocalTime().ToString("HH:mm:ss");
        public static string Full(DateTime utc) => utc.ToLocalTime().ToString("HH:mm:ss dd/MM/yyyy");

        // ---------------- Control dựng sẵn ----------------
        public static Label Label(string text, Font font, Color color, bool autoSize = true) => new Label
        {
            Text = text, Font = font, ForeColor = color, AutoSize = autoSize, BackColor = Color.Transparent, UseMnemonic = false
        };

        public static Label IconLabel(string glyph, float size, Color color) => new Label
        {
            Text = glyph, Font = IconFont(size), ForeColor = color, AutoSize = true, BackColor = Color.Transparent
        };

        public static ComboBox Combo(params string[] items)
        {
            var c = new DarkCombo { Width = 180, Margin = new Padding(0, 0, 10, 0) };
            c.Items.AddRange(items);
            if (items.Length > 0) c.SelectedIndex = 0;
            return c;
        }

        // ---------------- Tông tối cho phần do Windows vẽ (thanh tiêu đề, thanh cuộn) ----------------
        [System.Runtime.InteropServices.DllImport("dwmapi.dll")]
        static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int value, int size);
        [System.Runtime.InteropServices.DllImport("uxtheme.dll", CharSet = System.Runtime.InteropServices.CharSet.Unicode)]
        static extern int SetWindowTheme(IntPtr hwnd, string appName, string idList);

        //Thanh tiêu đề tối (Windows 10 1809+ / Windows 11); máy cũ hơn bỏ qua
        public static void DarkTitleBar(Form f)
        {
            void Apply() { try { int on = 1; DwmSetWindowAttribute(f.Handle, 20, ref on, 4); } catch { } }
            if (f.IsHandleCreated) Apply(); else f.HandleCreated += (s, e) => Apply();
        }

        //Thanh cuộn tối cho control và các thanh cuộn con (DataGridView)
        public static void DarkScroll(Control c)
        {
            void Apply()
            {
                try
                {
                    SetWindowTheme(c.Handle, "DarkMode_Explorer", null);
                    foreach (Control child in c.Controls) if (child is ScrollBar) SetWindowTheme(child.Handle, "DarkMode_Explorer", null);
                }
                catch { }
            }
            if (c.IsHandleCreated) Apply(); else c.HandleCreated += (s, e) => Apply();
        }

        //Danh sách dọc: các dòng luôn rộng bằng vùng hiển thị, không có thanh cuộn ngang
        public static void FitRows(FlowLayoutPanel f)
        {
            DarkScroll(f);
            int RowWidth(Control c) => Math.Max(10, f.Width - SystemInformation.VerticalScrollBarWidth - c.Margin.Horizontal - 2);
            f.SizeChanged += (s, e) => { foreach (Control c in f.Controls) c.Width = RowWidth(c); };
            f.ControlAdded += (s, e) => e.Control.Width = RowWidth(e.Control);
        }

        public static void StyleGrid(DataGridView g)
        {
            g.BackgroundColor = Card;
            g.BorderStyle = BorderStyle.None;
            g.CellBorderStyle = DataGridViewCellBorderStyle.SingleHorizontal;
            g.GridColor = Line;
            g.EnableHeadersVisualStyles = false;
            g.ColumnHeadersBorderStyle = DataGridViewHeaderBorderStyle.None;
            g.ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.DisableResizing;
            g.ColumnHeadersHeight = 40;
            g.ColumnHeadersDefaultCellStyle = new DataGridViewCellStyle
            {
                BackColor = Card2, ForeColor = Muted, Font = Mono(8.5f, FontStyle.Bold), Padding = new Padding(10, 0, 0, 0),
                SelectionBackColor = Card2, SelectionForeColor = Muted
            };
            g.DefaultCellStyle = new DataGridViewCellStyle
            {
                BackColor = Card, ForeColor = Text, Font = Sans(10), Padding = new Padding(10, 0, 4, 0),
                SelectionBackColor = Mix(Cyan, Card, 0.08), SelectionForeColor = Text
            };
            g.RowHeadersVisible = false;
            g.RowTemplate.Height = 48;
            g.AllowUserToAddRows = false;
            g.AllowUserToDeleteRows = false;
            g.AllowUserToResizeRows = false;
            g.AllowUserToResizeColumns = false;
            g.ReadOnly = true;
            g.MultiSelect = false;
            g.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
            g.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;
            g.ScrollBars = ScrollBars.Vertical;
            typeof(DataGridView).GetProperty("DoubleBuffered", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
                .SetValue(g, true, null);
            DarkScroll(g);
            // tự vẽ hàng tiêu đề: font mono nhỏ, chữ in hoa, nền card
            g.CellPainting += (s, e) =>
            {
                if (e.RowIndex != -1 || e.ColumnIndex < 0) return;
                using (var b = new SolidBrush(Card2)) e.Graphics.FillRectangle(b, e.CellBounds);
                TextRenderer.DrawText(e.Graphics, Convert.ToString(e.Value), Mono(8, FontStyle.Bold),
                    new Rectangle(e.CellBounds.X + 10, e.CellBounds.Y, e.CellBounds.Width - 12, e.CellBounds.Height), Muted,
                    TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);
                e.Handled = true;
            };
        }

        //Chart gốc ném ArgumentException khi bị co về 0px (thu nhỏ cửa sổ, layout tạm thời) -> bỏ qua resize/vẽ lúc đó
        public class SafeChart : Chart
        {
            protected override void OnResize(EventArgs e) { if (Width > 0 && Height > 0) base.OnResize(e); }
            protected override void OnPaint(PaintEventArgs e) { if (Width > 0 && Height > 0) base.OnPaint(e); }
        }

        //Biểu đồ đường tông tối: trục X giờ, trục Y 0-100% (hoặc tự co giãn khi max = null)
        public static Chart LineChart(double? max = 100)
        {
            var chart = new SafeChart { Dock = DockStyle.Fill, BackColor = Card, AntiAliasing = AntiAliasingStyles.All, TextAntiAliasingQuality = TextAntiAliasingQuality.High };
            var area = new ChartArea("main") { BackColor = Card };
            area.Position = new ElementPosition(0, 2, 100, 96);
            area.AxisX.LineColor = Line;
            area.AxisX.MajorGrid.Enabled = false;
            area.AxisX.MajorTickMark.Enabled = false;
            area.AxisX.LabelStyle.ForeColor = Muted;
            area.AxisX.LabelStyle.Font = Mono(8);
            area.AxisX.IntervalAutoMode = IntervalAutoMode.VariableCount;
            area.AxisX.IsMarginVisible = false;
            area.AxisY.LineColor = Color.Transparent;
            area.AxisY.MajorGrid.LineColor = Line;
            area.AxisY.MajorTickMark.Enabled = false;
            area.AxisY.LabelStyle.ForeColor = Muted;
            area.AxisY.LabelStyle.Font = Mono(8);
            area.AxisY.Minimum = 0;
            if (max.HasValue) { area.AxisY.Maximum = max.Value; area.AxisY.Interval = max.Value / 2; }
            chart.ChartAreas.Add(area);
            return chart;
        }

        public static Series LineSeries(Chart chart, string name, Color color, bool fill, int width = 2, ChartDashStyle dash = ChartDashStyle.Solid)
        {
            var s = new Series(name)
            {
                ChartType = fill ? SeriesChartType.SplineArea : SeriesChartType.Spline,
                Color = fill ? Color.FromArgb(40, color) : color,
                BorderColor = color, BorderWidth = width, BorderDashStyle = dash,
                XValueType = ChartValueType.DateTime, ToolTip = name + ": #VALY{0.#}\n#VALX{HH:mm}",
                IsXValueIndexed = false
            };
            chart.Series.Add(s);
            return s;
        }
    }
}
