using ZKTecoC3Manager.Models;
using ZKTecoC3Manager.Services;
using ZKTecoC3Manager.UI;

namespace ZKTecoC3Manager.Forms;

/// <summary>Cài đặt Verify API QR/Card: URL, Headers, Body data và quy tắc áp dụng.</summary>
public sealed class VerifySettingsForm : Form
{
    private readonly ToggleSwitch chkEnabled;
    private readonly ComboBox cboMethod;
    private readonly TextBox txtUrl, txtHeaders, txtBody, txtResultField;
    private readonly NumericUpDown numTimeout, numOpenSeconds, numDuplicate, numPromptTimeout;
    private readonly ComboBox cboOnError;
    private readonly ToggleSwitch chkSkipGranted;
    private readonly ModernButton btnTest, btnSave, btnCancel;
    private readonly Label lblTest;
    private readonly TableLayoutPanel apiGrid;
    private readonly Func<string> _deviceSerial;

    public VerifyApiSettings Settings { get; private set; }

    public VerifySettingsForm(VerifyApiSettings settings, Func<string> deviceSerial)
    {
        Settings = settings.Clone();
        _deviceSerial = deviceSerial;

        Text = "Cài đặt xác thực QR / Card";
        Font = Theme.Ui;
        ForeColor = Theme.Text;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = MinimizeBox = false;
        StartPosition = FormStartPosition.CenterParent;
        ClientSize = new Size(744, 760);
        BackColor = Theme.Bg;

        var mono = new Font("Consolas", 9.5F);

        chkEnabled = new ToggleSwitch
        {
            Text = "Bật xác thực qua API (tắt hoặc để trống URL → hỏi người vận hành bằng popup)",
            Margin = new Padding(0, 0, 0, 10),
        };
        cboMethod = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Width = 110 };
        cboMethod.Items.AddRange(["POST", "PUT", "GET"]);
        txtUrl = new TextBox { Dock = DockStyle.Fill, PlaceholderText = "https://api.example.com/access/verify" };
        txtHeaders = new TextBox
        {
            Dock = DockStyle.Fill, Multiline = true, Height = 66, ScrollBars = ScrollBars.Vertical, Font = mono,
            AcceptsReturn = true, WordWrap = false,
        };
        txtBody = new TextBox
        {
            Dock = DockStyle.Fill, Multiline = true, Height = 170, ScrollBars = ScrollBars.Both, Font = mono,
            AcceptsReturn = true, AcceptsTab = true, WordWrap = false,
        };
        var lblVars = new Label
        {
            AutoSize = true, ForeColor = Theme.Muted, Font = Theme.Small, MaximumSize = new Size(500, 0),
            Text = "Biến dùng được trong URL, Headers, Body: {code} {pin} {door} {direction} {eventType} {eventName} {time} {deviceSerial}",
        };
        numTimeout = Num(500, 60000, 500);
        txtResultField = new TextBox { Width = 160, Font = mono };
        cboOnError = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Width = 220 };
        cboOnError.Items.AddRange(["Hỏi người vận hành (popup)", "Từ chối"]);
        var lblResp = new Label
        {
            AutoSize = true, ForeColor = Theme.Muted, Font = Theme.Small, MaximumSize = new Size(500, 0),
            Text = "Response mong đợi: { \"allow\": true, \"message\": \"...\", \"openSeconds\": 5 } — trường kết quả hỗ trợ dạng lồng như data.allow",
        };

        apiGrid = Grid(
            ("URL", txtUrl), ("Phương thức", cboMethod),
            ("Headers\r\n(mỗi dòng Tên: Giá trị)", txtHeaders),
            ("Body data\r\n(bỏ trống nếu GET)", txtBody),
            ("", lblVars), ("Timeout (ms)", numTimeout), ("Trường kết quả JSON", txtResultField),
            ("Khi API lỗi", cboOnError), ("", lblResp));

        chkSkipGranted = new ToggleSwitch
        {
            Text = "Bỏ qua lượt quét controller đã cho phép (chỉ xác thực thẻ/QR bị từ chối)",
            Margin = new Padding(0, 0, 0, 10),
        };
        numOpenSeconds = Num(1, 254, 1);
        numDuplicate = Num(0, 60, 1);
        numPromptTimeout = Num(5, 300, 5);
        var applyGrid = Grid(("Mở cửa (giây)", numOpenSeconds), ("Bỏ quét trùng (giây)", numDuplicate),
            ("Popup tự đóng (giây)", numPromptTimeout));

        btnTest = new ModernButton { Text = "Kiểm tra API", Glyph = Glyph.Code, Width = 140, Font = Theme.Ui, Anchor = AnchorStyles.Left };
        lblTest = new Label { AutoSize = true, Font = Theme.Small, Margin = new Padding(10, 0, 3, 0), MaximumSize = new Size(360, 0), Anchor = AnchorStyles.Left };
        btnSave = new ModernButton { Text = "Lưu", Glyph = Glyph.Accept, Variant = ButtonVariant.Primary, Width = 100, Anchor = AnchorStyles.Right, Margin = new Padding(0, 0, 8, 0) };
        btnCancel = new ModernButton { Text = "Huỷ", Width = 90, Font = Theme.Ui, DialogResult = DialogResult.Cancel, Anchor = AnchorStyles.Right, Margin = Padding.Empty };

        var bottom = new TableLayoutPanel { Dock = DockStyle.Bottom, Height = 60, ColumnCount = 4, RowCount = 1, Padding = new Padding(16, 0, 16, 0), BackColor = Color.White };
        bottom.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        bottom.Paint += (_, e) =>
        {
            using var pen = new Pen(Theme.Border);
            e.Graphics.DrawLine(pen, 0, 0, bottom.Width, 0);
        };
        bottom.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        bottom.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        bottom.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        bottom.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        bottom.Controls.AddRange([btnTest, lblTest, btnSave, btnCancel]);

        var content = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill, FlowDirection = FlowDirection.TopDown, WrapContents = false, AutoScroll = true,
            Padding = new Padding(16, 16, 16, 4),
        };
        content.Controls.Add(Group("Verify API", chkEnabled, apiGrid));
        content.Controls.Add(Group("Áp dụng", chkSkipGranted, applyGrid));

        Controls.Add(content);
        Controls.Add(bottom);
        AcceptButton = btnSave;
        CancelButton = btnCancel;

        chkEnabled.CheckedChanged += (_, _) => apiGrid.Enabled = chkEnabled.Checked;
        btnTest.Click += async (_, _) => await TestAsync();
        btnSave.Click += (_, _) => Save();

        LoadValues();
    }

    private void LoadValues()
    {
        var s = Settings;
        chkEnabled.Checked = s.Enabled;
        apiGrid.Enabled = s.Enabled;
        cboMethod.SelectedItem = cboMethod.Items.Contains(s.Method.ToUpperInvariant()) ? s.Method.ToUpperInvariant() : "POST";
        txtUrl.Text = s.Url;
        txtHeaders.Text = Normalize(s.Headers);
        txtBody.Text = Normalize(s.Body);
        numTimeout.Value = Math.Clamp(s.TimeoutMs, (int)numTimeout.Minimum, (int)numTimeout.Maximum);
        txtResultField.Text = s.ResultField;
        cboOnError.SelectedIndex = s.PromptOnError ? 0 : 1;
        chkSkipGranted.Checked = s.SkipGranted;
        numOpenSeconds.Value = Math.Clamp(s.OpenSeconds, 1, 254);
        numDuplicate.Value = Math.Clamp(s.DuplicateWindowSeconds, 0, 60);
        numPromptTimeout.Value = Math.Clamp(s.PromptTimeoutSeconds, 5, 300);
    }

    private VerifyApiSettings ReadValues() => new()
    {
        Enabled = chkEnabled.Checked,
        Method = cboMethod.SelectedItem as string ?? "POST",
        Url = txtUrl.Text.Trim(),
        Headers = txtHeaders.Text.Replace("\r\n", "\n"),
        Body = txtBody.Text.Replace("\r\n", "\n"),
        TimeoutMs = (int)numTimeout.Value,
        ResultField = string.IsNullOrWhiteSpace(txtResultField.Text) ? "allow" : txtResultField.Text.Trim(),
        PromptOnError = cboOnError.SelectedIndex == 0,
        SkipGranted = chkSkipGranted.Checked,
        OpenSeconds = (int)numOpenSeconds.Value,
        DuplicateWindowSeconds = (int)numDuplicate.Value,
        PromptTimeoutSeconds = (int)numPromptTimeout.Value,
    };

    private void Save()
    {
        var s = ReadValues();
        if (s.Enabled && !s.IsConfigured)
        {
            MessageBox.Show(this, "URL không hợp lệ (cần http:// hoặc https://).", Text, MessageBoxButtons.OK, MessageBoxIcon.Warning);
            txtUrl.Focus();
            return;
        }
        Settings = s;
        DialogResult = DialogResult.OK;
        Close();
    }

    private async Task TestAsync()
    {
        var s = ReadValues();
        if (!Uri.TryCreate(s.Url, UriKind.Absolute, out _))
        {
            ShowTest(false, "Nhập URL hợp lệ trước.");
            return;
        }

        var sample = new AccessEvent
        {
            Time = DateTime.Now, Door = 1, Direction = "Vào", Code = "TEST-QR-0001", Pin = "0",
            EventType = 27, EventName = RtLogParser.EventName(27),
        };

        btnTest.Enabled = false;
        ShowTest(null, "Đang gọi API...");
        try
        {
            var r = await VerifyApiClient.VerifyAsync(s, sample, _deviceSerial());
            ShowTest(r.Success, r.Success
                ? $"✓ {r.StatusCode} OK · {s.ResultField}={r.Allow.ToString().ToLowerInvariant()} · {r.ElapsedMs} ms" +
                  (r.Message is null ? "" : $" · {r.Message}")
                : $"✗ {r.Error} ({r.ElapsedMs} ms)");
        }
        finally { btnTest.Enabled = true; }
    }

    private void ShowTest(bool? ok, string text)
    {
        lblTest.Text = text;
        lblTest.ForeColor = ok switch
        {
            true => Theme.SuccessText,
            false => Theme.DangerText,
            _ => Theme.Muted,
        };
    }

    private static string Normalize(string s) => s.Replace("\r\n", "\n").Replace("\n", "\r\n");

    private static NumericUpDown Num(int min, int max, int step) =>
        new() { Minimum = min, Maximum = max, Increment = step, Width = 110 };

    /// <summary>Lưới nhãn | ô nhập; ô nhập được bọc trong <see cref="FieldBox"/> bo góc.</summary>
    private static TableLayoutPanel Grid(params (string caption, Control control)[] rows)
    {
        var t = new TableLayoutPanel { ColumnCount = 2, AutoSize = true, Width = 680 };
        t.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 160));
        t.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        foreach (var (caption, control) in rows)
        {
            t.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            t.Controls.Add(new Label
            {
                Text = caption, AutoSize = true, Font = Theme.Small, ForeColor = Theme.TextSoft, Margin = new Padding(0, 8, 8, 0),
            });

            Control cell = control;
            if (control is TextBox or NumericUpDown or ComboBox)
            {
                var fill = control.Dock == DockStyle.Fill;
                var height = control is TextBox { Multiline: true } ? control.Height + 12 : 32;
                var width = control.Width + 22;
                cell = new FieldBox(control, height);
                if (fill) cell.Dock = DockStyle.Fill;
                else cell.Width = width;
            }
            cell.Margin = new Padding(0, 0, 0, 10);
            t.Controls.Add(cell);
        }
        return t;
    }

    private static CardPanel Group(string title, params Control[] children)
    {
        var stack = new FlowLayoutPanel { FlowDirection = FlowDirection.TopDown, WrapContents = false, AutoSize = true };
        stack.Controls.AddRange(children);
        var card = new CardPanel { Title = title, TitleGlyph = Glyph.Settings, Width = 712, Margin = new Padding(0, 0, 0, 14) };
        card.SetContent(stack);
        return card;
    }
}
