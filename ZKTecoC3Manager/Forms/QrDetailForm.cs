using QRCoder;
using ZKTecoC3Manager.Models;
using ZKTecoC3Manager.UI;

namespace ZKTecoC3Manager.Forms;

/// <summary>
/// Popup xem lại một lượt quét: vẽ lại mã QR từ nội dung đã ghi, kèm chi tiết sự kiện,
/// sao chép mã và lưu ảnh PNG.
/// </summary>
public sealed class QrDetailForm : Form
{
    private const int QrBox = 300;

    private readonly AccessEvent _event;
    private readonly byte[] _png;
    private readonly Image _qrImage;

    public QrDetailForm(AccessEvent e)
    {
        _event = e;
        _png = RenderQrPng(e.Code, QrBox);
        using (var ms = new MemoryStream(_png)) _qrImage = new Bitmap(Image.FromStream(ms));

        Text = $"Mã QR — {e.Code}";
        Font = Theme.Ui;
        ForeColor = Theme.Text;
        BackColor = Theme.Bg;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = MinimizeBox = false;
        StartPosition = FormStartPosition.CenterParent;
        ShowInTaskbar = false;
        ClientSize = new Size(720, 432);
        KeyPreview = true;

        // Bên trái: ảnh QR trong card trắng.
        var qr = new PictureBox
        {
            Image = _qrImage, SizeMode = PictureBoxSizeMode.CenterImage, Size = new Size(QrBox, QrBox),
            BackColor = Color.White, Margin = Padding.Empty, AccessibleName = $"Mã QR của {e.Code}",
        };
        var qrCard = new CardPanel { Width = QrBox + 32, Margin = new Padding(0, 0, 16, 0), Padding = new Padding(16) };
        qrCard.SetContent(qr);

        // Bên phải: nội dung mã + chi tiết lượt quét.
        var caption = new Label { Text = "NỘI DUNG MÃ", AutoSize = true, Font = Theme.GridHeader, ForeColor = Theme.Muted, Margin = new Padding(0, 0, 0, 6) };
        var code = new TextBox
        {
            Text = e.Code, ReadOnly = true, Multiline = true, WordWrap = true, Height = 54, Width = 300,
            Font = new Font("Consolas", 12F, FontStyle.Bold), ScrollBars = ScrollBars.None,
        };
        var codeBox = new FieldBox(code, 60) { Width = 320, Margin = new Padding(0, 0, 0, 12) };

        var result = new PillLabel { Text = e.Result, Margin = new Padding(0, 0, 0, 12) };
        (result.PillColor, result.ForeColor) = e.Pending ? (Theme.WarningTint, Theme.WarningText)
            : e.DoorOpened ? (Theme.SuccessTint, Theme.SuccessText)
            : (Theme.DangerTint, Theme.DangerText);

        var info = new TableLayoutPanel { ColumnCount = 2, AutoSize = true, Width = 320, Margin = Padding.Empty };
        info.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 100));
        info.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        AddRow(info, "Thời gian", e.Time.ToString("yyyy-MM-dd HH:mm:ss"), Theme.Mono);
        AddRow(info, "Cửa", $"{e.Door} ({e.Direction})");
        AddRow(info, "PIN", e.Pin);
        AddRow(info, "Controller", e.EventName);
        AddRow(info, "Xác thực", string.IsNullOrEmpty(e.VerifyNote) ? "—" : e.VerifyNote);

        var note = new Label
        {
            Text = "Ảnh QR được tạo lại từ nội dung mã đã ghi nhận, nên có thể khác hình dáng mã gốc nhưng chứa cùng nội dung.",
            AutoSize = true, MaximumSize = new Size(320, 0), Font = Theme.Small, ForeColor = Theme.Faint, Margin = new Padding(0, 10, 0, 0),
        };

        var detailStack = new FlowLayoutPanel { FlowDirection = FlowDirection.TopDown, WrapContents = false, AutoSize = true };
        detailStack.Controls.AddRange([caption, codeBox, result, info, note]);
        var detailCard = new CardPanel { Title = "Chi tiết lượt quét", TitleGlyph = Glyph.QrCode, Width = 352, Margin = Padding.Empty };
        detailCard.SetContent(detailStack);

        var body = new FlowLayoutPanel { Dock = DockStyle.Fill, WrapContents = false, Padding = new Padding(16, 16, 16, 0) };
        body.Controls.AddRange([qrCard, detailCard]);

        // Thanh nút.
        var btnCopy = new ModernButton { Text = "Sao chép mã", Glyph = Glyph.Copy, Width = 140, Font = Theme.Ui, Margin = new Padding(0, 0, 8, 0) };
        var btnSave = new ModernButton { Text = "Lưu ảnh PNG", Glyph = Glyph.Save, Width = 140, Font = Theme.Ui, Margin = Padding.Empty };
        var btnClose = new ModernButton { Text = "Đóng", Variant = ButtonVariant.Primary, Width = 100, DialogResult = DialogResult.Cancel, Margin = Padding.Empty };
        var status = new Label { AutoSize = true, Font = Theme.Small, ForeColor = Theme.SuccessText, Anchor = AnchorStyles.Left, Margin = new Padding(12, 0, 0, 0) };

        var bar = new TableLayoutPanel
        {
            Dock = DockStyle.Bottom, Height = 64, ColumnCount = 5, RowCount = 1, BackColor = Color.White, Padding = new Padding(16, 0, 16, 0),
        };
        bar.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        bar.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        bar.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        bar.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        bar.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        bar.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        bar.Paint += (_, pe) =>
        {
            using var pen = new Pen(Theme.Border);
            pe.Graphics.DrawLine(pen, 0, 0, bar.Width, 0);
        };
        foreach (var b in new Control[] { btnCopy, btnSave }) b.Anchor = AnchorStyles.Left;
        btnClose.Anchor = AnchorStyles.Right;
        bar.Controls.Add(btnCopy, 0, 0);
        bar.Controls.Add(btnSave, 1, 0);
        bar.Controls.Add(status, 2, 0);
        bar.Controls.Add(btnClose, 4, 0);

        Controls.Add(body);
        Controls.Add(bar);
        CancelButton = btnClose;
        Shown += (_, _) =>
        {
            code.SelectionLength = 0;
            btnClose.Focus();
        };

        btnCopy.Click += (_, _) =>
        {
            Clipboard.SetText(e.Code);
            status.Text = "Đã sao chép mã";
        };
        btnSave.Click += (_, _) =>
        {
            if (SavePng()) status.Text = "Đã lưu ảnh";
        };
    }

    /// <summary>Ctrl+C sao chép mã (trừ khi đang bôi đen chữ trong ô mã).</summary>
    protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
    {
        if (keyData == (Keys.Control | Keys.C) && ActiveControl is not TextBox { SelectionLength: > 0 })
        {
            Clipboard.SetText(_event.Code);
            return true;
        }
        return base.ProcessCmdKey(ref msg, keyData);
    }

    private bool SavePng()
    {
        var safe = string.Concat(_event.Code.Select(c => Path.GetInvalidFileNameChars().Contains(c) ? '_' : c));
        using var dlg = new SaveFileDialog
        {
            Filter = "Ảnh PNG (*.png)|*.png",
            FileName = $"qr_{safe}_{_event.Time:yyyyMMdd_HHmmss}.png",
        };
        if (dlg.ShowDialog(this) != DialogResult.OK) return false;
        try
        {
            File.WriteAllBytes(dlg.FileName, RenderQrPng(_event.Code, 800));
            return true;
        }
        catch (IOException ex)
        {
            MessageBox.Show(this, ex.Message, "Lưu ảnh", MessageBoxButtons.OK, MessageBoxIcon.Error);
            return false;
        }
    }

    /// <summary>Tạo PNG mã QR vừa khít khung <paramref name="maxSize"/> px, mỗi module là số nguyên pixel để ảnh sắc nét.</summary>
    private static byte[] RenderQrPng(string text, int maxSize)
    {
        using var gen = new QRCodeGenerator();
        using var data = gen.CreateQrCode(text, QRCodeGenerator.ECCLevel.M);
        var modules = data.ModuleMatrix.Count; // đã gồm vùng trắng quanh mã
        var pixelsPerModule = Math.Max(1, maxSize / modules);
        using var png = new PngByteQRCode(data);
        return png.GetGraphic(pixelsPerModule);
    }

    private static void AddRow(TableLayoutPanel t, string caption, string value, Font? font = null)
    {
        t.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        t.Controls.Add(new Label { Text = caption, AutoSize = true, Font = Theme.Small, ForeColor = Theme.Muted, Margin = new Padding(0, 5, 0, 5) });
        t.Controls.Add(new Label
        {
            Text = value, AutoSize = true, MaximumSize = new Size(215, 0), Font = font ?? Theme.Ui, ForeColor = Theme.Text,
            Margin = new Padding(0, 4, 0, 4),
        });
    }

    protected override void OnFormClosed(FormClosedEventArgs e)
    {
        _qrImage.Dispose();
        base.OnFormClosed(e);
    }
}
