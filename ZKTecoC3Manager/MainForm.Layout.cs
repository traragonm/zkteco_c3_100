using ZKTecoC3Manager.UI;

namespace ZKTecoC3Manager;

// Dựng giao diện bằng code (không dùng Designer) theo mockup "Main window v2":
// header navy + pill trạng thái, cột trái gồm các card cấu hình, cột phải gồm
// 4 thẻ thống kê và card chứa tab "Sự kiện quét QR" / "Log kết nối".
partial class MainForm
{
    // Card hẹp hơn cột một khoảng đủ cho thanh cuộn dọc, để không bao giờ phát sinh thanh cuộn ngang.
    private const int LeftColumnWidth = 340;
    private const int LeftCardWidth = 304;

    private ModernButton btnLogDir = null!, btnHeaderSettings = null!, btnAbout = null!;
    private PillLabel pillConn = null!;

    private TextBox txtIp = null!, txtPassword = null!;
    private NumericUpDown numPort = null!, numTimeout = null!, numPoll = null!, numOpenSeconds = null!;
    private ToggleSwitch chkAutoReconnect = null!;
    private ModernButton btnConnect = null!, btnSyncTime = null!, btnOpenDoor = null!, btnCloseDoor = null!;
    private ComboBox cboDoor = null!;
    private Label lblSerial = null!, lblFirmware = null!, lblDoors = null!, lblDevTime = null!;
    private PillLabel lblVerifyMode = null!;
    private Label lblVerifyUrl = null!;
    private ModernButton btnVerifySettings = null!;

    private StatCard statStatus = null!, statScans = null!, statOkDeny = null!, statDoor = null!;
    private SegmentedTabs tabs = null!;
    private DataGridView gridEvents = null!;
    private Panel logHost = null!;
    private RichTextBox txtLog = null!;
    private ToggleSwitch chkShowDebug = null!;
    private ModernButton btnExport = null!, btnClear = null!;

    private Label stConn = null!, stPoll = null!, stLogFile = null!, stEventFile = null!;
    private readonly ToolTip tips = new();

    private void BuildLayout()
    {
        SuspendLayout();
        Text = "ZKTeco C3-100 Manager";
        Font = Theme.Ui;
        BackColor = Theme.Bg;
        ForeColor = Theme.Text;
        ClientSize = new Size(1280, 820);
        MinimumSize = new Size(1040, 680);
        StartPosition = FormStartPosition.CenterScreen;
        KeyPreview = true;

        var body = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 1, Padding = new Padding(16) };
        body.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, LeftColumnWidth));
        body.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        body.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        body.Controls.Add(BuildLeftColumn(), 0, 0);
        body.Controls.Add(BuildRightColumn(), 1, 0);

        Controls.Add(body);
        Controls.Add(BuildFooter());
        Controls.Add(BuildHeader());
        ResumeLayout(true);
    }

    // ---------------------------------------------------------------- header / footer

    private Control BuildHeader()
    {
        var header = new TableLayoutPanel
        {
            Dock = DockStyle.Top, Height = 62, BackColor = Theme.Navy, ColumnCount = 5, RowCount = 1,
            Padding = new Padding(16, 0, 12, 0),
        };
        header.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        header.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        header.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        header.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        header.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        header.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

        var logo = new IconBadge { Anchor = AnchorStyles.Left, Margin = new Padding(0, 0, 12, 0) };

        var titles = new FlowLayoutPanel
        {
            FlowDirection = FlowDirection.TopDown, AutoSize = true, WrapContents = false, Anchor = AnchorStyles.Left,
            Margin = Padding.Empty,
        };
        titles.Controls.Add(new Label { Text = "ZKTeco C3-100 Manager", AutoSize = true, Font = Theme.AppTitle, ForeColor = Color.White, Margin = Padding.Empty });
        titles.Controls.Add(new Label { Text = "Access control · PullSDK", AutoSize = true, Font = Theme.Small, ForeColor = Theme.NavyMuted, Margin = Padding.Empty });

        pillConn = new PillLabel { ShowDot = true, Anchor = AnchorStyles.Right, Margin = new Padding(0, 0, 10, 0), Padding = new Padding(12, 6, 12, 6) };

        var actions = new FlowLayoutPanel { AutoSize = true, WrapContents = false, Anchor = AnchorStyles.Right, Margin = Padding.Empty };
        btnLogDir = HeaderButton(Glyph.Folder, "Mở thư mục log");
        btnHeaderSettings = HeaderButton(Glyph.Settings, "Cài đặt xác thực QR / Card");
        btnAbout = HeaderButton(Glyph.Info, "Giới thiệu");
        actions.Controls.AddRange([btnLogDir, btnHeaderSettings, btnAbout]);

        header.Controls.Add(logo, 0, 0);
        header.Controls.Add(titles, 1, 0);
        header.Controls.Add(pillConn, 3, 0);
        header.Controls.Add(actions, 4, 0);
        return header;
    }

    private ModernButton HeaderButton(string glyph, string tip)
    {
        var b = new ModernButton
        {
            Variant = ButtonVariant.Ghost, Glyph = glyph, Size = new Size(38, 38), Font = new Font("Segoe UI", 11F),
            Margin = new Padding(2, 0, 2, 0), AccessibleName = tip,
        };
        tips.SetToolTip(b, tip);
        return b;
    }

    private Control BuildFooter()
    {
        var footer = new FlowLayoutPanel
        {
            Dock = DockStyle.Bottom, Height = 30, BackColor = Color.White, WrapContents = false,
            Padding = new Padding(16, 7, 16, 0),
        };
        footer.Paint += (_, e) =>
        {
            using var pen = new Pen(Theme.Border);
            e.Graphics.DrawLine(pen, 0, 0, footer.Width, 0);
        };
        stConn = FooterLabel();
        stPoll = FooterLabel();
        stLogFile = FooterLabel();
        stEventFile = FooterLabel();
        footer.Controls.AddRange([stConn, stPoll, stLogFile, stEventFile]);
        return footer;
    }

    private static Label FooterLabel() => new()
    {
        AutoSize = true, Font = Theme.Small, ForeColor = Theme.Muted, Margin = new Padding(0, 0, 22, 0),
    };

    // ---------------------------------------------------------------- cột trái

    private Control BuildLeftColumn()
    {
        var col = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill, FlowDirection = FlowDirection.TopDown, WrapContents = false,
            AutoScroll = true, Margin = new Padding(0, 0, 8, 0), Padding = Padding.Empty,
        };

        col.Controls.Add(Card("Kết nối", Glyph.Connect, BuildConnectionContent()));
        col.Controls.Add(Card("Thiết bị", Glyph.Devices, BuildDeviceContent()));
        col.Controls.Add(Card("Điều khiển cửa", Glyph.Lock, BuildDoorContent()));
        col.Controls.Add(Card("Xác thực QR / Card", Glyph.Shield, BuildVerifyContent()));
        return col;
    }

    private Control BuildConnectionContent()
    {
        txtIp = new TextBox();
        numPort = Num(1, 65535, 4370);
        numTimeout = Num(500, 60000, 4000, 500);
        txtPassword = new TextBox { UseSystemPasswordChar = true, PlaceholderText = "(trống)" };
        // Chu kỳ dài làm popup hiện chậm (lượt quét chờ tới lần đọc kế tiếp), nên giới hạn 2 giây.
        numPoll = Num(100, 2000, 500, 100);
        chkAutoReconnect = new ToggleSwitch { Text = "Tự kết nối lại khi mất kết nối", Checked = true, Margin = new Padding(0, 2, 0, 12) };
        btnConnect = new ModernButton
        {
            Text = "Kết nối", Glyph = Glyph.Connect, Variant = ButtonVariant.Primary, Height = 40, Dock = DockStyle.Fill,
            Margin = Padding.Empty,
        };

        var t = Grid2();
        AddFields(t, ("Địa chỉ IP", txtIp, 2));
        AddFields(t, ("Cổng", numPort, 1), ("Timeout (ms)", numTimeout, 1));
        AddFields(t, ("Mật khẩu", txtPassword, 1), ("Chu kỳ đọc (ms)", numPoll, 1));
        AddRow(t, chkAutoReconnect);
        AddRow(t, btnConnect);
        return t;
    }

    private Control BuildDeviceContent()
    {
        lblSerial = InfoValue(mono: true);
        lblFirmware = InfoValue(mono: true);
        lblDoors = InfoValue();
        lblDevTime = InfoValue(mono: true);
        btnSyncTime = new ModernButton
        {
            Text = "Đồng bộ giờ PC → thiết bị", Glyph = Glyph.Sync, Height = 34, Dock = DockStyle.Fill,
            Font = Theme.Ui, Margin = new Padding(0, 10, 0, 0),
        };

        var t = new TableLayoutPanel { ColumnCount = 2, AutoSize = false };
        t.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 96));
        t.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        foreach (var (caption, value) in new[] { ("Serial", lblSerial), ("Firmware", lblFirmware), ("Số cửa", lblDoors), ("Giờ thiết bị", lblDevTime) })
        {
            t.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            t.Controls.Add(new Label { Text = caption, AutoSize = true, Font = Theme.Small, ForeColor = Theme.Muted, Margin = new Padding(0, 4, 0, 4) });
            t.Controls.Add(value);
        }
        t.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        t.Controls.Add(btnSyncTime);
        t.SetColumnSpan(btnSyncTime, 2);
        return t;
    }

    private Control BuildDoorContent()
    {
        cboDoor = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList };
        cboDoor.Items.Add(1);
        cboDoor.SelectedIndex = 0;
        numOpenSeconds = Num(1, 254, 5);
        btnOpenDoor = new ModernButton
        {
            Text = "Mở cửa", Glyph = Glyph.Unlock, Variant = ButtonVariant.Success, Dock = DockStyle.Fill,
            Margin = new Padding(0, 0, 5, 0),
        };
        btnCloseDoor = new ModernButton
        {
            Text = "Đóng cửa", Glyph = Glyph.Lock, Dock = DockStyle.Fill, Font = Theme.Ui, Margin = new Padding(5, 0, 0, 0),
        };

        var t = Grid2();
        AddFields(t, ("Cửa", cboDoor, 1), ("Mở (giây)", numOpenSeconds, 1));
        t.RowStyles.Add(new RowStyle(SizeType.Absolute, 38));
        t.Controls.Add(btnOpenDoor);
        t.Controls.Add(btnCloseDoor);
        return t;
    }

    private Control BuildVerifyContent()
    {
        lblVerifyMode = new PillLabel { Anchor = AnchorStyles.Left, Margin = new Padding(0, 0, 8, 0) };
        lblVerifyUrl = new Label
        {
            Text = "—", AutoSize = false, AutoEllipsis = true, Height = 20, Anchor = AnchorStyles.Left | AnchorStyles.Right,
            Font = new Font("Consolas", 9F), ForeColor = Theme.Muted, Margin = Padding.Empty, TextAlign = ContentAlignment.MiddleLeft,
        };
        btnVerifySettings = new ModernButton
        {
            Text = "Cài đặt xác thực...", Glyph = Glyph.Settings, Height = 34, Dock = DockStyle.Fill, Font = Theme.Ui,
            Margin = new Padding(0, 12, 0, 0),
        };

        var t = new TableLayoutPanel { ColumnCount = 2 };
        t.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        t.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        t.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        t.Controls.Add(lblVerifyMode);
        t.Controls.Add(lblVerifyUrl);
        t.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        t.Controls.Add(btnVerifySettings);
        t.SetColumnSpan(btnVerifySettings, 2);
        return t;
    }

    // ---------------------------------------------------------------- cột phải

    private Control BuildRightColumn()
    {
        var right = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 2, Margin = Padding.Empty };
        right.RowStyles.Add(new RowStyle(SizeType.Absolute, 92));
        right.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

        var stats = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 4, RowCount = 1, Margin = Padding.Empty };
        for (var i = 0; i < 4; i++) stats.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 25));
        statStatus = Stat("Trạng thái", Glyph.Wifi, Theme.DangerTint, Theme.Danger, new Padding(0, 0, 7, 16));
        statScans = Stat("Lượt quét QR", Glyph.QrCode, Theme.PrimaryTint, Theme.Primary, new Padding(7, 0, 7, 16));
        statOkDeny = Stat("Hợp lệ / Từ chối", Glyph.Shield, Theme.SuccessTint, Theme.Success, new Padding(7, 0, 7, 16));
        statDoor = Stat("Cửa 1", Glyph.Lock, Theme.WarningTint, Theme.Warning, new Padding(7, 0, 0, 16));
        stats.Controls.AddRange([statStatus, statScans, statOkDeny, statDoor]);
        right.Controls.Add(stats, 0, 0);

        var card = new CardPanel
        {
            Dock = DockStyle.Fill, AutoHeight = false, ClipContent = true, Padding = new Padding(1), Margin = Padding.Empty,
        };
        card.SetContent(BuildEventsContent());
        right.Controls.Add(card, 0, 1);
        return right;
    }

    private Control BuildEventsContent()
    {
        var host = new Panel { BackColor = Color.White };

        // Thanh công cụ: tab segmented bên trái, nút thao tác bên phải.
        var toolbar = new TableLayoutPanel
        {
            Dock = DockStyle.Top, Height = 56, ColumnCount = 5, RowCount = 1, Padding = new Padding(12, 0, 12, 0),
        };
        toolbar.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        toolbar.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        toolbar.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        toolbar.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        toolbar.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        toolbar.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        toolbar.Paint += (_, e) =>
        {
            using var pen = new Pen(Theme.Border);
            e.Graphics.DrawLine(pen, 0, toolbar.Height - 1, toolbar.Width, toolbar.Height - 1);
        };

        tabs = new SegmentedTabs("Sự kiện quét QR", "Log kết nối") { Anchor = AnchorStyles.Left, Margin = Padding.Empty };
        chkShowDebug = new ToggleSwitch
        {
            Text = "Hiện DEBUG", Checked = true, Anchor = AnchorStyles.Right, Font = Theme.Small, Margin = new Padding(0, 0, 14, 0),
            Visible = false,
        };
        btnExport = new ModernButton
        {
            Text = "Xuất CSV", Glyph = Glyph.Download, Height = 32, Width = 112, Font = Theme.Ui, Anchor = AnchorStyles.Right,
            Margin = new Padding(0, 0, 8, 0), Radius = 6,
        };
        btnClear = new ModernButton
        {
            Text = "Xoá", Glyph = Glyph.Delete, Variant = ButtonVariant.DangerOutline, Height = 32, Width = 80, Font = Theme.Ui,
            Anchor = AnchorStyles.Right, Margin = Padding.Empty, Radius = 6,
        };
        toolbar.Controls.Add(tabs, 0, 0);
        toolbar.Controls.Add(chkShowDebug, 2, 0);
        toolbar.Controls.Add(btnExport, 3, 0);
        toolbar.Controls.Add(btnClear, 4, 0);

        gridEvents = BuildGrid();

        logHost = new Panel { Dock = DockStyle.Fill, BackColor = Theme.ConsoleBg, Padding = new Padding(14, 12, 6, 6), Visible = false };
        txtLog = new RichTextBox
        {
            Dock = DockStyle.Fill, ReadOnly = true, BorderStyle = BorderStyle.None, WordWrap = false, DetectUrls = false,
            BackColor = Theme.ConsoleBg, ForeColor = Theme.ConsoleText, Font = Theme.Mono,
        };
        logHost.Controls.Add(txtLog);

        host.Controls.Add(gridEvents);
        host.Controls.Add(logHost);
        host.Controls.Add(toolbar);
        return host;
    }

    private DataGridView BuildGrid()
    {
        var g = new DataGridView
        {
            Dock = DockStyle.Fill, ReadOnly = true, AllowUserToAddRows = false, AllowUserToDeleteRows = false,
            AllowUserToResizeRows = false, RowHeadersVisible = false, AutoGenerateColumns = false,
            SelectionMode = DataGridViewSelectionMode.FullRowSelect, BackgroundColor = Color.White,
            BorderStyle = BorderStyle.None, CellBorderStyle = DataGridViewCellBorderStyle.SingleHorizontal,
            ColumnHeadersBorderStyle = DataGridViewHeaderBorderStyle.None, GridColor = Theme.Divider,
            AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill, EnableHeadersVisualStyles = false,
            ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.DisableResizing,
            ColumnHeadersHeight = 40, Font = Theme.Ui,
        };
        g.RowTemplate.Height = 40;
        g.Paint += (_, e) =>
        {
            if (g.Rows.Count > 0) return;
            var area = new Rectangle(0, g.ColumnHeadersHeight, g.Width, g.Height - g.ColumnHeadersHeight);
            var icon = new Rectangle(area.X, area.Y + area.Height / 2 - 48, area.Width, 36);
            TextRenderer.DrawText(e.Graphics, Glyph.QrCode, Theme.Icon(22), icon, Theme.InputBorder,
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
            TextRenderer.DrawText(e.Graphics, "Chưa có lượt quét nào", Theme.UiSemibold,
                new Rectangle(area.X, icon.Bottom + 6, area.Width, 22), Theme.Muted, TextFormatFlags.HorizontalCenter);
            TextRenderer.DrawText(e.Graphics, "Kết nối thiết bị và quét mã QR / thẻ để xem sự kiện tại đây.", Theme.Small,
                new Rectangle(area.X, icon.Bottom + 30, area.Width, 20), Theme.Faint, TextFormatFlags.HorizontalCenter);
        };

        var header = g.ColumnHeadersDefaultCellStyle;
        header.BackColor = Theme.InputBg;
        header.ForeColor = Theme.Muted;
        header.Font = Theme.GridHeader;
        header.Padding = new Padding(10, 0, 10, 0);
        header.SelectionBackColor = Theme.InputBg;
        header.SelectionForeColor = Theme.Muted;

        var cell = g.DefaultCellStyle;
        cell.ForeColor = Theme.Text;
        cell.Padding = new Padding(10, 0, 10, 0);
        cell.SelectionBackColor = Theme.Selection;
        cell.SelectionForeColor = Theme.Text;

        g.Columns.AddRange(
            Col("Time", "THỜI GIAN", 150, "yyyy-MM-dd HH:mm:ss", Theme.Mono, Theme.TextSoft),
            Col("DoorText", "CỬA", 70),
            Col("Code", "MÃ QR / CARD", 160, font: Theme.MonoBold),
            Col("Pin", "PIN", 60, color: Theme.TextSoft),
            Col("EventName", "SỰ KIỆN", 160),
            Col("VerifyNote", "XÁC THỰC", 160, color: Theme.Muted),
            Col("Result", "KẾT QUẢ", 100));
        return g;
    }

    // ---------------------------------------------------------------- helpers

    private static CardPanel Card(string title, string glyph, Control content)
    {
        var c = new CardPanel { Title = title, TitleGlyph = glyph, Width = LeftCardWidth, Margin = new Padding(0, 0, 0, 14) };
        c.SetContent(content);
        return c;
    }

    private static StatCard Stat(string caption, string glyph, Color tint, Color color, Padding margin)
    {
        var s = new StatCard { Caption = caption, Dock = DockStyle.Fill, Margin = margin };
        s.SetIcon(glyph, tint, color);
        return s;
    }

    private static NumericUpDown Num(int min, int max, int value, int step = 1) =>
        new() { Minimum = min, Maximum = max, Value = value, Increment = step };

    private static Label InfoValue(bool mono = false) => new()
    {
        Text = "—", AutoSize = true, Margin = new Padding(0, 4, 0, 4), ForeColor = Theme.Text,
        Font = mono ? new Font("Consolas", 9.5F) : Theme.Ui,
    };

    private static TableLayoutPanel Grid2()
    {
        var t = new TableLayoutPanel { ColumnCount = 2 };
        t.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
        t.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
        return t;
    }

    /// <summary>Thêm một hàng nhãn và một hàng ô nhập; mỗi ô chiếm <c>span</c> cột.</summary>
    private static void AddFields(TableLayoutPanel t, params (string caption, Control input, int span)[] fields)
    {
        t.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        var row = t.RowStyles.Count - 1;
        var col = 0;
        foreach (var (caption, _, span) in fields)
        {
            var lbl = new Label { Text = caption, AutoSize = true, Font = Theme.Caption, ForeColor = Theme.Muted, Margin = new Padding(0, 0, 0, 4) };
            t.Controls.Add(lbl, col, row);
            t.SetColumnSpan(lbl, span);
            col += span;
        }

        t.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        row++;
        col = 0;
        foreach (var (_, input, span) in fields)
        {
            var box = new FieldBox(input)
            {
                Dock = DockStyle.Fill,
                Margin = new Padding(col == 0 ? 0 : 5, 0, col + span >= t.ColumnCount ? 0 : 5, 12),
            };
            t.Controls.Add(box, col, row);
            t.SetColumnSpan(box, span);
            col += span;
        }
    }

    private static void AddRow(TableLayoutPanel t, Control c)
    {
        t.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        t.Controls.Add(c, 0, t.RowStyles.Count - 1);
        t.SetColumnSpan(c, t.ColumnCount);
    }

    private static DataGridViewTextBoxColumn Col(string prop, string header, int weight, string? format = null,
        Font? font = null, Color? color = null)
    {
        var c = new DataGridViewTextBoxColumn
        {
            DataPropertyName = prop, HeaderText = header, FillWeight = weight, Name = "col" + prop,
            SortMode = DataGridViewColumnSortMode.NotSortable,
        };
        if (format is not null) c.DefaultCellStyle.Format = format;
        if (font is not null) c.DefaultCellStyle.Font = font;
        if (color is not null) c.DefaultCellStyle.ForeColor = color.Value;
        return c;
    }
}
