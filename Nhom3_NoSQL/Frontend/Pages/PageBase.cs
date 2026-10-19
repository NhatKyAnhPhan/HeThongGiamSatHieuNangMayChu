using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;
using Nhom3_NoSQL.Backend.Api;
using Nhom3_NoSQL.Frontend.UI;

namespace Nhom3_NoSQL.Frontend.Pages
{
    //Lớp cơ sở cho các trang: Build() dựng khung 1 lần, RefreshAsync() nạp dữ liệu (gọi lại mỗi 5 giây)
    public abstract class PageBase : UserControl
    {
        public FormMain Main { get; set; }
        protected ApiClient Api => Main.Api;

        public abstract string Title { get; }
        public virtual string Crumb => "";
        public virtual string NavKey => Title;
        public virtual IEnumerable<Control> Actions() { yield break; }
        public abstract void Build();
        public abstract Task RefreshAsync();

        protected PageBase()
        {
            BackColor = Theme.Bg;
            ForeColor = Theme.Text;
            Font = Theme.Sans(10);
            DoubleBuffered = true;
        }

        // ---------------- Bố cục ----------------
        //Bảng chia hàng (Absolute = px, Percent = %)
        protected static TableLayoutPanel Rows(params float[] heights) => Grid(true, heights);
        protected static TableLayoutPanel Cols(params float[] widths) => Grid(false, widths);

        //Số dương = px cố định, số âm = phần trăm (vd -65 = 65%)
        static TableLayoutPanel Grid(bool rows, float[] sizes)
        {
            var t = new TableLayoutPanel { Dock = DockStyle.Fill, BackColor = Theme.Bg, Margin = new Padding(0), Padding = new Padding(0) };
            if (rows) { t.ColumnCount = 1; t.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100)); t.RowCount = sizes.Length; }
            else { t.RowCount = 1; t.RowStyles.Add(new RowStyle(SizeType.Percent, 100)); t.ColumnCount = sizes.Length; }
            foreach (var s in sizes)
            {
                var type = s < 0 ? SizeType.Percent : SizeType.Absolute;
                if (rows) t.RowStyles.Add(new RowStyle(type, System.Math.Abs(s)));
                else t.ColumnStyles.Add(new ColumnStyle(type, System.Math.Abs(s)));
            }
            return t;
        }

        //Đặt control vào ô, chừa khoảng cách giữa các thẻ
        protected static T Cell<T>(T c, int right = 14, int bottom = 14) where T : Control
        {
            c.Dock = DockStyle.Fill;
            c.Margin = new Padding(0, 0, right, bottom);
            return c;
        }

        //Thẻ có tiêu đề + dòng phụ + vùng nội dung (body)
        protected static RoundPanel CardWithHeader(string title, string sub, out Panel body, out Label subLabel, Control right = null)
        {
            var card = new RoundPanel();
            var head = new Panel { Dock = DockStyle.Top, Height = 50, BackColor = Theme.Card };
            var t = Theme.Label(title, Theme.Semi(11.5f), Theme.Text); t.Location = new Point(0, 0);
            subLabel = Theme.Label(sub, Theme.Sans(8.5f), Theme.Muted); subLabel.Location = new Point(1, 24);
            head.Controls.AddRange(new Control[] { t, subLabel });
            if (right != null)
            {
                right.Anchor = AnchorStyles.Top | AnchorStyles.Right;
                head.Controls.Add(right);
                head.Layout += (s, e) => right.Location = new Point(head.Width - right.Width, 0);
            }
            body = new Panel { Dock = DockStyle.Fill, BackColor = Theme.Card };
            card.Controls.Add(head);
            card.Controls.Add(body);
            body.BringToFront();
            return card;
        }

        protected static RoundPanel CardWithHeader(string title, string sub, out Panel body, Control right = null) =>
            CardWithHeader(title, sub, out body, out _, right);

        //Thanh công cụ (tìm kiếm, bộ lọc)
        protected static FlowLayoutPanel Toolbar(params Control[] items)
        {
            var f = new FlowLayoutPanel { Dock = DockStyle.Fill, BackColor = Theme.Bg, WrapContents = false, Margin = new Padding(0), Padding = new Padding(0, 6, 0, 0) };
            foreach (var c in items) { c.Margin = new Padding(0, c is ComboBox ? 4 : 0, 10, 0); f.Controls.Add(c); }
            return f;
        }

        protected void Toast(string msg, bool error = false) => Main.ShowToast(msg, error);

        //Chạy thao tác API, báo lỗi bằng toast thay vì để exception làm sập form
        protected async Task Try(System.Func<Task> action, string okMessage = null)
        {
            try
            {
                await action();
                if (okMessage != null) Toast(okMessage);
            }
            catch (ApiClientException ex) { Toast(ex.Message, true); }
        }
    }
}
