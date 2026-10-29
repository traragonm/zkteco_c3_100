using ZKTecoC3Manager.Models;
using ZKTecoC3Manager.UI;

namespace ZKTecoC3Manager.Forms;

/// <summary>
/// Popup hỏi người vận hành có mở cửa hay không (khi chưa cấu hình Verify API hoặc API lỗi).
/// Hết thời gian chờ thì tự động từ chối. DialogResult.Yes = mở cửa.
/// </summary>
public sealed class OpenDoorPromptForm : Form
{
    private readonly System.Windows.Forms.Timer _timer = new() { Interval = 1000 };
    private readonly Label lblCountdown;
    private int _remaining;

    public bool TimedOut { get; private set; }

    public OpenDoorPromptForm(AccessEvent e, string reason, int timeoutSeconds, int waitingCount)
    {
        _remaining = timeoutSeconds;

        Text = "Xác thực thủ công";
        Font = Theme.Ui;
        ForeColor = Theme.Text;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = MinimizeBox = false;
        ShowInTaskbar = true;
        TopMost = true;
        StartPosition = FormStartPosition.CenterParent;
        ClientSize = new Size(460, 370);
        BackColor = Color.White;

        var icon = new IconBadge
        {
            BadgeGlyph = Glyph.Help, Tint = Theme.PrimaryTint, GlyphColor = Theme.Primary, Size = new Size(44, 44),
            GlyphSize = 15, Margin = new Padding(0, 0, 14, 0),
        };
        var title = new Label { Text = "Mở cửa cho lượt quét này?", AutoSize = true, Font = new Font("Segoe UI Semibold", 13F), Margin = Padding.Empty };
        var sub = new Label { Text = reason, AutoSize = true, ForeColor = Theme.Muted, MaximumSize = new Size(350, 0), Margin = new Padding(0, 2, 0, 0) };
        var titles = new FlowLayoutPanel { FlowDirection = FlowDirection.TopDown, AutoSize = true, WrapContents = false, Margin = Padding.Empty };
        titles.Controls.AddRange([title, sub]);
        var head = new FlowLayoutPanel { AutoSize = true, WrapContents = false, Margin = new Padding(0, 0, 0, 14) };
        head.Controls.AddRange([icon, titles]);

        var info = new TableLayoutPanel
        {
            ColumnCount = 2, AutoSize = true, Width = 420, BackColor = Theme.InputBg,
            Padding = new Padding(12, 8, 12, 8), CellBorderStyle = TableLayoutPanelCellBorderStyle.None,
        };
        info.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 120));
        info.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        info.Paint += (_, pe) =>
        {
            using var pen = new Pen(Theme.Border);
            pe.Graphics.DrawRectangle(pen, 0, 0, info.Width - 1, info.Height - 1);
        };
        AddRow(info, "Mã QR / Card", e.Code, new Font("Consolas", 11F, FontStyle.Bold));
        AddRow(info, "Cửa", $"{e.Door} ({e.Direction})");
        AddRow(info, "PIN", e.Pin);
        AddRow(info, "Thời gian", e.Time.ToString("yyyy-MM-dd HH:mm:ss"), Theme.Mono);
        AddRow(info, "Controller", e.EventName, color: e.Granted ? Theme.SuccessText : Theme.DangerText);

        lblCountdown = new Label { AutoSize = true, Font = Theme.Small, ForeColor = Theme.Muted, Margin = new Padding(0, 12, 0, 0) };
        var waiting = waitingCount > 0 ? $" · còn {waitingCount} lượt quét đang chờ" : "";
        UpdateCountdown(waiting);

        var btnOpen = new ModernButton
        {
            Text = "Mở cửa", Glyph = Glyph.Unlock, Variant = ButtonVariant.Primary, Width = 124, Height = 38,
            DialogResult = DialogResult.Yes, Margin = new Padding(8, 0, 0, 0),
        };
        var btnDeny = new ModernButton
        {
            Text = "Không mở", Glyph = Glyph.Cancel, Width = 124, Height = 38, Font = Theme.Ui,
            DialogResult = DialogResult.No, Margin = Padding.Empty,
        };
        var buttons = new FlowLayoutPanel
        {
            Dock = DockStyle.Bottom, Height = 62, FlowDirection = FlowDirection.RightToLeft, Padding = new Padding(18, 12, 18, 12),
        };
        buttons.Controls.AddRange([btnOpen, btnDeny]);

        var body = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.TopDown, WrapContents = false, Padding = new Padding(20, 20, 20, 0) };
        body.Controls.AddRange([head, info, lblCountdown]);

        Controls.Add(body);
        Controls.Add(buttons);
        AcceptButton = btnOpen;
        CancelButton = btnDeny;

        _timer.Tick += (_, _) =>
        {
            _remaining--;
            UpdateCountdown(waiting);
            if (_remaining > 0) return;
            _timer.Stop();
            TimedOut = true;
            DialogResult = DialogResult.No;
        };
    }

    protected override void OnShown(EventArgs e)
    {
        base.OnShown(e);
        Activate();
        System.Media.SystemSounds.Exclamation.Play();
        _timer.Start();
    }

    protected override void OnFormClosed(FormClosedEventArgs e)
    {
        _timer.Dispose();
        base.OnFormClosed(e);
    }

    private void UpdateCountdown(string waiting) =>
        lblCountdown.Text = $"Tự động từ chối sau {_remaining} giây{waiting}";

    private static void AddRow(TableLayoutPanel t, string caption, string value, Font? font = null, Color? color = null)
    {
        t.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        t.Controls.Add(new Label { Text = caption, AutoSize = true, Font = Theme.Small, ForeColor = Theme.Muted, Margin = new Padding(0, 5, 0, 5) });
        var v = new Label { Text = value, AutoSize = true, ForeColor = Theme.Text, Margin = new Padding(0, 4, 0, 4) };
        if (font is not null) v.Font = font;
        if (color is not null) v.ForeColor = color.Value;
        t.Controls.Add(v);
    }
}
