using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using System.Windows.Forms;
using Nhom3_NoSQL.Backend.Api;
using Nhom3_NoSQL.Backend.Models;
using Nhom3_NoSQL.Frontend.Pages;
using Nhom3_NoSQL.Frontend.UI;

namespace Nhom3_NoSQL.Frontend
{
    //Khung hộp thoại tông tối: tiêu đề, các ô nhập xếp 2 cột, dòng báo lỗi, nút Hủy / Lưu
    public abstract class DialogBase : Form
    {
        protected readonly ApiClient Api;
        readonly TableLayoutPanel _fields = new TableLayoutPanel { ColumnCount = 2, AutoSize = true, BackColor = Theme.Panel, Dock = DockStyle.Top };
        readonly Label _error = Theme.Label("", Theme.Sans(9.5f), Theme.Red);
        readonly RoundButton _save = new RoundButton("Lưu", RoundButton.Kind.Primary);
        readonly Panel _head;

        protected DialogBase(ApiClient api, string title, string subtitle)
        {
            Api = api;
            Text = title;
            BackColor = Theme.Panel;
            ForeColor = Theme.Text;
            Font = Theme.Sans(10);
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = MinimizeBox = false;
            ShowInTaskbar = false;
            StartPosition = FormStartPosition.CenterParent;
            Width = 560;
            Padding = new Padding(22, 18, 22, 18);
            Theme.DarkTitleBar(this);

            _fields.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
            _fields.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
            var head = new Panel { Dock = DockStyle.Top, Height = 58, BackColor = Theme.Panel };
            var t = Theme.Label(title, Theme.Semi(14), Theme.Text); t.Location = new Point(0, 0);
            var s = Theme.Label(subtitle, Theme.Sans(9), Theme.Muted); s.Location = new Point(1, 32); s.MaximumSize = new Size(500, 0);
            head.Controls.AddRange(new Control[] { t, s });
            head.Height = s.Top + s.PreferredHeight + 14; // dòng mô tả có thể xuống 2 dòng
            _head = head;

            var foot = new Panel { Dock = DockStyle.Bottom, Height = 56, BackColor = Theme.Panel };
            var cancel = new RoundButton("Hủy") { DialogResult = DialogResult.Cancel };
            foot.Controls.AddRange(new Control[] { _error, cancel, _save });
            foot.Layout += (o, e) =>
            {
                _save.Location = new Point(foot.Width - _save.Width, 16);
                cancel.Location = new Point(_save.Left - cancel.Width - 10, 16);
                _error.Location = new Point(0, 0);
                _error.MaximumSize = new Size(foot.Width, 0);
            };
            CancelButton = cancel;
            AcceptButton = _save;
            _save.Click += async (o, e) => await SaveClicked();

            Controls.Add(_fields);
            Controls.Add(head);
            Controls.Add(foot);
        }

        protected InputBox AddField(string label, string value, int columnSpan = 1)
        {
            var box = new InputBox(null, "") { Dock = DockStyle.Fill, Text = value ?? "" };
            AddControl(label, box, columnSpan);
            return box;
        }

        protected void AddControl(string label, Control c, int columnSpan = 1)
        {
            var p = new Panel { Height = 70, Dock = DockStyle.Fill, BackColor = Theme.Panel, Margin = new Padding(0, 0, 12, 6) };
            var l = Theme.Label(label, Theme.Sans(9), Theme.Text2); l.Location = new Point(0, 2);
            c.Location = new Point(0, 24);
            c.Width = 240;
            c.Anchor = AnchorStyles.Left | AnchorStyles.Right | AnchorStyles.Top;
            c.Dock = DockStyle.None;
            p.Controls.AddRange(new Control[] { l, c });
            p.Layout += (o, e) => c.Width = p.Width;
            _fields.Controls.Add(p);
            _fields.SetColumnSpan(p, columnSpan);
        }

        protected override void OnShown(EventArgs e)
        {
            base.OnShown(e);
            ClientSize = new Size(ClientSize.Width, _head.Height + _fields.Height + 56 + Padding.Vertical + 10);
        }

        async Task SaveClicked()
        {
            _error.Text = "";
            _save.Enabled = false;
            try
            {
                await SaveAsync();
                DialogResult = DialogResult.OK;
            }
            catch (ValidationException ex) { _error.Text = ex.Message; }
            catch (ApiClientException ex) { _error.Text = ex.Message; }
            finally { if (!IsDisposed) _save.Enabled = true; }
        }

        protected abstract Task SaveAsync();

        protected static double Number(InputBox box, string name)
        {
            var s = box.Text.Trim().Replace(" ", "");
            if (s == "") return 0;
            if (double.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out var v) ||
                double.TryParse(s, NumberStyles.Float, Theme.Vi, out v)) return v;
            throw new ValidationException(name + " phải là số");
        }
    }

    public class ValidationException : Exception { public ValidationException(string m) : base(m) { } }

    //Thêm / sửa máy chủ
    public class HostDialog : DialogBase
    {
        readonly ServerInfo _host;
        readonly InputBox _name, _ip, _os, _region, _desc, _cpu, _ram, _disk;
        public ServerInfo Result { get; private set; }

        public HostDialog(ApiClient api, ServerInfo host)
            : base(api, host == null ? "Thêm máy chủ" : "Chỉnh sửa máy chủ",
                "Sau khi thêm, cấu hình Agent gửi metric về POST /api/v1/metrics/collector kèm Host ID.")
        {
            _host = host;
            _name = AddField("Hostname *", host?.Name);
            _ip = AddField("Địa chỉ IP *", host?.IpAddress);
            _os = AddField("Hệ điều hành", host?.OperatingSystem);
            _region = AddField("Khu vực", host?.Region);
            _desc = AddField("Mô tả", host?.Description, 2);
            _cpu = AddField("Số vCPU", host?.CpuCores > 0 ? host.CpuCores.ToString() : "");
            _ram = AddField("RAM (GB)", host?.RamTotalGb > 0 ? host.RamTotalGb.ToString(CultureInfo.InvariantCulture) : "");
            _disk = AddField("Disk (GB)", host?.DiskTotalGb > 0 ? host.DiskTotalGb.ToString(CultureInfo.InvariantCulture) : "");
        }

        //Kiểm tra dữ liệu phía giao diện (API cũng kiểm tra lại)
        public static void Validate(string name, string ip)
        {
            if (string.IsNullOrWhiteSpace(name)) throw new ValidationException("Hostname là bắt buộc");
            if (!IPAddress.TryParse(ip ?? "", out var addr) || (addr.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork && ip.Split('.').Length != 4))
                throw new ValidationException("Địa chỉ IP không hợp lệ");
        }

        protected override async Task SaveAsync()
        {
            Validate(_name.Text, _ip.Text.Trim());
            var h = new ServerInfo
            {
                Id = _host?.Id, Name = _name.Text.Trim(), IpAddress = _ip.Text.Trim(), OperatingSystem = _os.Text.Trim(), Region = _region.Text.Trim(),
                Description = _desc.Text.Trim(), CpuCores = (int)Number(_cpu, "Số vCPU"), RamTotalGb = Number(_ram, "RAM"), DiskTotalGb = Number(_disk, "Disk"),
                IsActive = _host?.IsActive ?? true, CreatedAt = _host?.CreatedAt ?? DateTime.UtcNow
            };
            Result = _host == null ? await Api.CreateHostAsync(h) : await Api.UpdateHostAsync(h);
        }
    }

    //Tạo / sửa quy tắc ngưỡng
    public class RuleDialog : DialogBase
    {
        readonly AlertRule _rule;
        readonly List<HostView> _hosts;
        readonly ComboBox _scope, _metric;
        readonly InputBox _unit, _warn, _crit, _dur;
        readonly CheckBox _enabled;

        public RuleDialog(ApiClient api, AlertRule rule, List<HostView> hosts)
            : base(api, rule?.Id != null ? "Sửa quy tắc ngưỡng" : "Tạo quy tắc ngưỡng",
                "Cảnh báo được tạo khi chỉ số vượt ngưỡng liên tục trong duration_seconds.")
        {
            _rule = rule;
            _hosts = hosts;
            _scope = Theme.Combo(new[] { "Toàn hệ thống" }.Concat(hosts.Select(h => h.Host.Name)).ToArray());
            if (rule?.ServerId != null) _scope.SelectedIndex = 1 + hosts.FindIndex(h => h.Host.Id == rule.ServerId);
            AddControl("Phạm vi", _scope, 2);
            _metric = Theme.Combo(ThresholdsPage.MetricNames.Values.ToArray());
            if (rule != null) _metric.SelectedIndex = ThresholdsPage.MetricNames.Keys.ToList().IndexOf(rule.Metric);
            AddControl("Metric", _metric);
            _unit = AddField("Đơn vị", rule?.Unit ?? "%");
            _warn = AddField("Warning threshold", (rule?.ThresholdValue ?? 80).ToString(CultureInfo.InvariantCulture));
            _crit = AddField("Critical threshold", (rule?.CriticalValue ?? 90).ToString(CultureInfo.InvariantCulture));
            _dur = AddField("duration_seconds", (rule?.DurationSeconds ?? 300).ToString(), 2);
            _enabled = new CheckBox { Text = "Bật quy tắc", Checked = rule?.IsEnabled ?? true, ForeColor = Theme.Text, BackColor = Theme.Panel, AutoSize = true };
            AddControl("", _enabled, 2);
        }

        //Kiểm tra ngưỡng phía giao diện (API cũng kiểm tra lại)
        public static void Validate(double warn, double crit, string unit, int duration)
        {
            if (warn <= 0 || crit <= 0) throw new ValidationException("Ngưỡng phải lớn hơn 0");
            if (crit < warn) throw new ValidationException("Ngưỡng Critical phải ≥ ngưỡng Warning");
            if (unit == "%" && (warn > 100 || crit > 100)) throw new ValidationException("Ngưỡng % không được vượt 100");
            if (duration < 0) throw new ValidationException("duration_seconds không hợp lệ");
        }

        protected override async Task SaveAsync()
        {
            double warn = Number(_warn, "Warning threshold"), crit = Number(_crit, "Critical threshold");
            int dur = (int)Number(_dur, "duration_seconds");
            Validate(warn, crit, _unit.Text.Trim(), dur);
            var rule = new AlertRule
            {
                Id = _rule?.Id,
                ServerId = _scope.SelectedIndex == 0 ? null : _hosts[_scope.SelectedIndex - 1].Host.Id,
                Metric = ThresholdsPage.MetricNames.Keys.ElementAt(_metric.SelectedIndex),
                Unit = _unit.Text.Trim(), Operator = ComparisonOperator.GreaterThan,
                ThresholdValue = warn, CriticalValue = crit, DurationSeconds = dur, IsEnabled = _enabled.Checked
            };
            await Api.SaveThresholdAsync(rule);
        }
    }
}
