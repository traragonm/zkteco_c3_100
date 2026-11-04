using System.ComponentModel;
using System.Diagnostics;
using ZKTecoC3Manager.Forms;
using ZKTecoC3Manager.Models;
using ZKTecoC3Manager.Services;
using ZKTecoC3Manager.UI;
using LogLevel = ZKTecoC3Manager.Models.LogLevel;

namespace ZKTecoC3Manager;

public partial class MainForm : Form
{
    private const int MaxGridRows = 5000;
    private const int MaxLogLines = 3000;

    private readonly string _logDir = Path.Combine(AppContext.BaseDirectory, "logs");
    private readonly AppLogger _logger;
    private readonly EventCsvWriter _eventWriter;
    private readonly C3Controller _device;
    private readonly QrReaderClient _reader;
    private readonly AppSettings _settings = AppSettings.Load();
    private readonly BindingList<AccessEvent> _events = new();
    private readonly List<LogEntry> _logHistory = new();

    private readonly Dictionary<string, DateTime> _lastScan = new();
    private readonly SemaphoreSlim _promptGate = new(1, 1);
    private int _promptWaiting;
    private string _deviceSerial = "";

    private int _granted, _denied;
    private bool _closing;

    public MainForm()
    {
        BuildLayout();

        _logger = new AppLogger(_logDir);
        _eventWriter = new EventCsvWriter(_logDir);
        _device = new C3Controller(_logger);
        _reader = new QrReaderClient(_logger);

        _logger.Logged += e => Ui(() => AppendLog(e));
        _device.StateChanged += s => Ui(() => ApplyState(s));
        _device.EventsReceived += list => Ui(() => OnEvents(list));
        _device.StatusReceived += st => Ui(() => OnDoorStatus(st));
        _device.DeviceInfoReceived += info => Ui(() => ShowDeviceInfo(info));
        _reader.StateChanged += s => Ui(() => ApplyReaderState(s));
        _reader.FrameReceived += f => Ui(() => OnReaderFrame(f));

        gridEvents.DataSource = _events;
        gridEvents.CellPainting += GridEvents_CellPainting;
        gridEvents.CellDoubleClick += (_, e) =>
        {
            if (e.RowIndex >= 0) ShowQrDetail(e.RowIndex);
        };
        gridEvents.KeyDown += (_, e) =>
        {
            if (e.KeyCode != Keys.Enter || gridEvents.CurrentRow is null) return;
            e.SuppressKeyPress = true;
            ShowQrDetail(gridEvents.CurrentRow.Index);
        };
        gridEvents.CellToolTipTextNeeded += (_, e) =>
        {
            if (e.RowIndex >= 0) e.ToolTipText = "Nhấp đúp (hoặc Enter) để xem mã QR";
        };

        btnConnect.Click += async (_, _) =>
        {
            if (_device.State == ConnectionState.Disconnected) await ConnectAsync();
            else await _device.DisconnectAsync();
        };
        btnReaderConnect.Click += async (_, _) => await ToggleReaderAsync();
        btnSyncTime.Click += async (_, _) => await RunGuarded(btnSyncTime, _device.SyncTimeAsync);
        btnOpenDoor.Click += async (_, _) => await RunGuarded(btnOpenDoor, () => _device.SetDoorAsync(SelectedDoor, (int)numOpenSeconds.Value));
        btnCloseDoor.Click += async (_, _) => await RunGuarded(btnCloseDoor, () => _device.SetDoorAsync(SelectedDoor, 0));
        btnExport.Click += (_, _) => ExportCsv();
        btnClear.Click += (_, _) => ClearCurrentTab();
        chkShowDebug.CheckedChanged += (_, _) => RedrawLog();
        tabs.SelectedIndexChanged += (_, _) => ShowTab(tabs.SelectedIndex);
        btnVerifySettings.Click += (_, _) => OpenVerifySettings();
        btnHeaderSettings.Click += (_, _) => OpenVerifySettings();
        btnLogDir.Click += (_, _) => Process.Start(new ProcessStartInfo(_logDir) { UseShellExecute = true });
        btnAbout.Click += (_, _) => MessageBox.Show(this,
            "ZKTeco C3-100 Manager\nĐọc mã QR / thẻ từ đầu đọc RS485 qua Waveshare RS485 TO ETH (TCP),\n" +
            "xác thực rồi mở cửa trên C3-100 qua PullSDK (plcommpro.dll), ghi sự kiện và log kết nối.\n\n" +
            "Phím tắt: F6 kết nối/ngắt đầu đọc · F5 kết nối C3 · Shift+F5 ngắt C3 · Ctrl+S xuất CSV",
            "Giới thiệu", MessageBoxButtons.OK, MessageBoxIcon.Information);

        LoadSettings();
        ApplyState(ConnectionState.Disconnected);
        ApplyReaderState(ReaderState.Disconnected);
        UpdateCounters();
        UpdateVerifyInfo();
        stLogFile.Text = "Log: " + Path.GetRelativePath(AppContext.BaseDirectory, _logger.CurrentFile);
        stEventFile.Text = "Events: " + Path.GetRelativePath(AppContext.BaseDirectory, _eventWriter.CurrentFile);
    }

    protected override void OnLoad(EventArgs e)
    {
        base.OnLoad(e);
        _logger.Info($"Khởi động ứng dụng ({(Environment.Is64BitProcess ? "x64" : "x86")}), thư mục log: {_logDir}");
        if (_settings.ReaderAutoConnect) _ = ToggleReaderAsync();
    }

    private int SelectedDoor => cboDoor.SelectedItem is int d ? d : 1;

    // ---------------------------------------------------------------- kết nối

    private async Task ConnectAsync()
    {
        if (_device.State != ConnectionState.Disconnected) return;

        var ip = txtIp.Text.Trim();
        if (!System.Net.IPAddress.TryParse(ip, out _))
        {
            MessageBox.Show(this, "Địa chỉ IP không hợp lệ.", "Kết nối", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            txtIp.Focus();
            return;
        }

        SaveSettings();
        var settings = new ConnectionSettings(ip, (int)numPort.Value, (int)numTimeout.Value, txtPassword.Text,
            (int)numPoll.Value, chkAutoReconnect.Checked);

        if (!await _device.ConnectAsync(settings))
            MessageBox.Show(this, "Không kết nối được tới thiết bị. Xem tab \"Log kết nối\" để biết chi tiết.",
                "Kết nối", MessageBoxButtons.OK, MessageBoxIcon.Error);
    }

    // ---------------------------------------------------------------- đầu đọc QR (Waveshare)

    private async Task ToggleReaderAsync()
    {
        if (_reader.State != ReaderState.Disconnected)
        {
            await _reader.StopAsync();
            return;
        }

        var ip = txtReaderIp.Text.Trim();
        if (!System.Net.IPAddress.TryParse(ip, out _))
        {
            MessageBox.Show(this, "Địa chỉ IP của Waveshare không hợp lệ.", "Đầu đọc QR", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            txtReaderIp.Focus();
            return;
        }
        SaveSettings();
        _reader.Start(new ReaderSettings(ip, (int)numReaderPort.Value));
    }

    private void ApplyReaderState(ReaderState s)
    {
        var idle = s == ReaderState.Disconnected;
        foreach (var c in new Control[] { txtReaderIp, numReaderPort }) c.Enabled = idle;

        (btnReaderConnect.Text, btnReaderConnect.Variant) = s switch
        {
            ReaderState.Connected => ("Ngắt đầu đọc", ButtonVariant.DangerOutline),
            ReaderState.Connecting => ("Đang kết nối... (huỷ)", ButtonVariant.DangerOutline),
            ReaderState.Reconnecting => ("Dừng kết nối lại", ButtonVariant.DangerOutline),
            _ => ("Kết nối đầu đọc", ButtonVariant.Primary),
        };

        var (statText, statColor, statTint) = s switch
        {
            ReaderState.Connected => ("Online", Theme.Success, Theme.SuccessTint),
            ReaderState.Connecting => ("Đang kết nối", Theme.Warning, Theme.WarningTint),
            ReaderState.Reconnecting => ("Kết nối lại", Theme.Warning, Theme.WarningTint),
            _ => ("Offline", Theme.Danger, Theme.DangerTint),
        };
        statStatus.SetIcon(Glyph.Wifi, statTint, statColor);
        statStatus.SetValue(statText, statColor);

        var target = _reader.Settings is { } rs ? $"{rs.Ip}:{rs.Port}" : "";
        stReader.Text = s switch
        {
            ReaderState.Connected => $"Đầu đọc: {target}",
            ReaderState.Connecting => $"Đầu đọc: đang kết nối {target}...",
            ReaderState.Reconnecting => $"Đầu đọc: mất kết nối {target}, đang thử lại...",
            _ => "Đầu đọc: chưa kết nối",
        };
    }

    private void OnReaderFrame(ReaderFrame f)
    {
        var code = f.Text;
        var e = new AccessEvent
        {
            Time = f.Time,
            Door = (int)numReaderDoor.Value,
            Direction = "Vào",
            Code = code,
            EventName = "Đầu đọc QR (RS485)",
            Raw = f.Hex,
        };
        _logger.Debug($"Đầu đọc QR: {f.Data.Length} byte HEX={f.Hex}");
        ProcessScan(e, f.LooksGarbled || code.Length == 0
            ? $"Dữ liệu đầu đọc không đọc được ({f.Data.Length} byte, HEX {Truncate(f.Hex, 48)}). Kiểm tra baudrate Waveshare / đảo dây A-B."
            : null);
    }

    private static string Truncate(string s, int max) => s.Length <= max ? s : s[..max] + "…";

    private void ApplyState(ConnectionState s)
    {
        var connected = s == ConnectionState.Connected;
        var idle = s == ConnectionState.Disconnected;

        btnSyncTime.Enabled = btnOpenDoor.Enabled = btnCloseDoor.Enabled = connected;
        foreach (var c in new Control[] { txtIp, numPort, numTimeout, txtPassword, numPoll, chkAutoReconnect })
            c.Enabled = idle;

        (btnConnect.Text, btnConnect.Variant, btnConnect.Enabled) = s switch
        {
            ConnectionState.Connected => ("Ngắt kết nối", ButtonVariant.DangerOutline, true),
            ConnectionState.Connecting => ("Đang kết nối...", ButtonVariant.Primary, false),
            ConnectionState.Reconnecting => ("Dừng kết nối lại", ButtonVariant.DangerOutline, true),
            _ => ("Kết nối", ButtonVariant.Primary, true),
        };

        var target = _device.Settings is { } cs ? $"{cs.Ip}:{cs.Port}" : "";
        (pillConn.Text, pillConn.PillColor, pillConn.ForeColor) = s switch
        {
            ConnectionState.Connected => ($"C3 đã kết nối · {target}", Theme.PillOnlineBg, Theme.PillOnlineFg),
            ConnectionState.Connecting => ($"Đang kết nối · {target}", Theme.PillBusyBg, Theme.PillBusyFg),
            ConnectionState.Reconnecting => ($"C3 mất kết nối · đang thử lại", Theme.PillBusyBg, Theme.PillBusyFg),
            _ => ("C3 chưa kết nối", Theme.PillOfflineBg, Theme.PillOfflineFg),
        };

        stConn.Text = s switch
        {
            ConnectionState.Connected => $"C3: {target}",
            ConnectionState.Connecting => $"C3: đang kết nối {target}...",
            ConnectionState.Reconnecting => $"C3: mất kết nối {target}, đang thử lại...",
            _ => "C3: chưa kết nối",
        };
        stPoll.Text = connected ? $"Polling: {numPoll.Value} ms" : "Polling: dừng";

        if (idle)
        {
            lblSerial.Text = lblFirmware.Text = lblDoors.Text = lblDevTime.Text = "—";
            statDoor.SetIcon(Glyph.Lock, Theme.WarningTint, Theme.Warning);
            statDoor.SetValue("—");
        }
    }

    private void ShowDeviceInfo(DeviceInfo info)
    {
        _deviceSerial = info.Serial;
        lblSerial.Text = info.Serial;
        lblFirmware.Text = info.Firmware;
        lblDoors.Text = info.LockCount.ToString();
        lblDevTime.Text = info.DeviceTime?.ToString("yyyy-MM-dd HH:mm:ss") ?? "?";

        cboDoor.Items.Clear();
        for (var i = 1; i <= Math.Max(1, info.LockCount); i++) cboDoor.Items.Add(i);
        cboDoor.SelectedIndex = 0;
    }

    // ---------------------------------------------------------------- sự kiện

    /// <summary>
    /// Sự kiện từ GetRTLog của C3. Mã QR giờ đọc từ đầu đọc qua Waveshare (<see cref="OnReaderFrame"/>),
    /// nên ở đây chỉ ghi log (cửa mở/đóng, nút exit, lượt quét trên đầu đọc nối thẳng vào C3...).
    /// </summary>
    private void OnEvents(IReadOnlyList<AccessEvent> list)
    {
        foreach (var e in list)
        {
            var code = string.IsNullOrEmpty(e.Code) || e.Code == "0" ? "" : $", mã {e.Code}";
            _logger.Info($"C3 cửa {e.Door}: {e.EventName}{code}");
        }
    }

    /// <summary>Đưa một lượt quét vào bảng rồi xác thực / mở cửa.</summary>
    /// <param name="unreadableReason">Khác null khi không lấy được mã: không gọi API, luôn hỏi người vận hành.</param>
    private void ProcessScan(AccessEvent e, string? unreadableReason)
    {
        _events.Insert(0, e);
        if (unreadableReason is not null)
            _logger.Warn($"Quét cửa {e.Door}: {unreadableReason}");
        else
            _logger.Info($"Quét QR cửa {e.Door}: {e.Code}");

        var v = _settings.Verify;
        if (unreadableReason is not null)
        {
            e.Pending = true;
            e.VerifyNote = "Đang chờ người vận hành...";
            _ = VerifyAndDecideAsync(e, unreadableReason);
        }
        else if (IsDuplicate(e, v.DuplicateWindowSeconds))
        {
            e.VerifyNote = "Bỏ qua (quét trùng)";
            Complete(e);
        }
        else
        {
            e.Pending = true;
            e.VerifyNote = "Đang xác thực...";
            _ = VerifyAndDecideAsync(e);
        }

        while (_events.Count > MaxGridRows) _events.RemoveAt(_events.Count - 1);
        UpdateCounters();
    }

    // ---------------------------------------------------------------- xác thực QR / Card

    /// <summary>
    /// Xác thực một lượt quét: có Verify API thì gọi API, chưa cấu hình (hoặc API lỗi và chọn hỏi)
    /// thì hiện popup cho người vận hành. Cho phép → ControlDevice mở cửa.
    /// </summary>
    private async Task VerifyAndDecideAsync(AccessEvent e, string? unreadableReason = null)
    {
        var v = _settings.Verify.Clone();
        var seconds = v.OpenSeconds;
        var open = false;
        var note = "";

        try
        {
            if (unreadableReason is not null)
            {
                (open, note) = await AskOperatorAsync(e, unreadableReason, v.PromptTimeoutSeconds);
                note = "Mã lỗi → " + note;
            }
            else if (v.IsConfigured)
            {
                var r = await VerifyApiClient.VerifyAsync(v, e, _deviceSerial);
                if (r.Success)
                {
                    open = r.Allow;
                    if (r.OpenSeconds is > 0 and < 255) seconds = r.OpenSeconds.Value;
                    var msg = r.Message is null ? "" : $" — {r.Message}";
                    note = $"API: {v.ResultField}={(r.Allow ? "true" : "false")}{msg}";
                    _logger.Info($"Verify API {e.Code}: HTTP {r.StatusCode}, allow={r.Allow}, {r.ElapsedMs} ms{msg}");
                }
                else
                {
                    _logger.Warn($"Verify API lỗi cho {e.Code}: {r.Error} ({r.ElapsedMs} ms)");
                    if (v.PromptOnError)
                    {
                        (open, note) = await AskOperatorAsync(e, $"Verify API lỗi: {r.Error}", v.PromptTimeoutSeconds);
                        note = "API lỗi → " + note;
                    }
                    else note = "API lỗi → từ chối";
                }
            }
            else
            {
                (open, note) = await AskOperatorAsync(e, "Chưa cấu hình API xác thực.", v.PromptTimeoutSeconds);
            }

            e.DoorOpened = e.Granted;
            if (open && !e.Granted)
            {
                if (!_device.IsConnected) note += " (chưa kết nối C3, không mở được)";
                else if (await _device.SetDoorAsync(e.Door > 0 ? e.Door : 1, seconds)) e.DoorOpened = true;
                else note += " (lệnh mở cửa lỗi)";
            }
            else if (!open && e.Granted)
            {
                note += " (controller đã mở)";
            }
        }
        catch (Exception ex)
        {
            _logger.Error($"Lỗi xác thực {e.Code}: {ex.Message}");
            note = "Lỗi: " + ex.Message;
        }
        finally
        {
            e.Pending = false;
            e.VerifyNote = note;
            Complete(e);
            _logger.Info($"Kết quả {e.Code}: {e.Result} ({note})");
            UpdateCounters();
        }
    }

    /// <summary>Hỏi người vận hành, mỗi lần một popup; các lượt quét khác xếp hàng chờ.</summary>
    private async Task<(bool open, string note)> AskOperatorAsync(AccessEvent e, string reason, int timeoutSeconds)
    {
        var queuedAt = DateTime.Now;
        _promptWaiting++;
        await _promptGate.WaitAsync();
        try
        {
            _promptWaiting--;
            if (_closing || IsDisposed) return (false, "Thủ công: huỷ");
            // Lượt quét đã chờ quá lâu trong hàng đợi thì không còn ý nghĩa mở cửa nữa.
            if ((DateTime.Now - queuedAt).TotalSeconds > timeoutSeconds) return (false, "Thủ công: hết hạn chờ");

            using var dlg = new OpenDoorPromptForm(e, reason, timeoutSeconds, _promptWaiting);
            var res = dlg.ShowDialog(this);
            if (dlg.TimedOut)
            {
                _logger.Warn($"Popup xác thực {e.Code} hết giờ → từ chối");
                return (false, "Thủ công: hết giờ → từ chối");
            }
            var open = res == DialogResult.Yes;
            _logger.Info($"Người vận hành {(open ? "MỞ" : "KHÔNG mở")} cửa {e.Door} cho {e.Code}");
            return (open, open ? "Thủ công: mở" : "Thủ công: không mở");
        }
        finally { _promptGate.Release(); }
    }

    private bool IsDuplicate(AccessEvent e, int windowSeconds)
    {
        if (windowSeconds <= 0) return false;
        var key = $"{e.Door}|{e.Code}";
        var now = DateTime.Now;
        if (_lastScan.TryGetValue(key, out var last) && (now - last).TotalSeconds < windowSeconds) return true;

        if (_lastScan.Count > 1000)
            foreach (var k in _lastScan.Where(kv => (now - kv.Value).TotalSeconds > 60).Select(kv => kv.Key).ToList())
                _lastScan.Remove(k);
        _lastScan[key] = now;
        return false;
    }

    /// <summary>Chốt kết quả cuối của lượt quét: cập nhật lưới, bộ đếm và ghi CSV.</summary>
    private void Complete(AccessEvent e)
    {
        if (e.DoorOpened) _granted++; else _denied++;
        var i = _events.IndexOf(e);
        if (i >= 0) _events.ResetItem(i);

        try { _eventWriter.Append(e); }
        catch (IOException ex) { _logger.Error($"Không ghi được file sự kiện: {ex.Message}"); }
    }

    private void OpenVerifySettings()
    {
        using var dlg = new VerifySettingsForm(_settings.Verify, () => _deviceSerial);
        if (dlg.ShowDialog(this) != DialogResult.OK) return;

        _settings.Verify = dlg.Settings;
        _settings.Save();
        UpdateVerifyInfo();
        _logger.Info(_settings.Verify.IsConfigured
            ? $"Đã lưu cấu hình Verify API: {_settings.Verify.Method} {_settings.Verify.Url}"
            : "Đã lưu cấu hình xác thực: hỏi thủ công (popup)");
    }

    private void UpdateVerifyInfo()
    {
        var v = _settings.Verify;
        if (v.IsConfigured)
        {
            lblVerifyMode.Text = "Verify API";
            (lblVerifyMode.PillColor, lblVerifyMode.ForeColor) = (Theme.PrimaryTint, Theme.PrimaryText);
            lblVerifyUrl.Text = $"{v.Method} {v.Url}";
        }
        else
        {
            lblVerifyMode.Text = "Hỏi thủ công";
            (lblVerifyMode.PillColor, lblVerifyMode.ForeColor) = (Theme.WarningTint, Theme.WarningText);
            lblVerifyUrl.Text = v.Enabled ? "URL chưa hợp lệ" : "Popup hỏi mở cửa";
        }
        tips.SetToolTip(lblVerifyUrl, lblVerifyUrl.Text);
    }

    private void OnDoorStatus(DoorStatus st)
    {
        var (text, glyph, tint, color) = st.Door1Sensor switch
        {
            1 => ("Đóng", Glyph.Lock, Theme.SuccessTint, Theme.Success),
            2 => ("Đang mở", Glyph.Unlock, Theme.WarningTint, Theme.Warning),
            _ => ("Không sensor", Glyph.Lock, Theme.Divider, Theme.Muted),
        };
        statDoor.SetIcon(glyph, tint, color);
        statDoor.SetValue(text, st.Door1Sensor == 0 ? Theme.Muted : Theme.Text);
    }

    private void UpdateCounters()
    {
        var total = _granted + _denied;
        statScans.SetValue(total.ToString());
        statOkDeny.SetParts((_granted.ToString(), Theme.Success), (" / ", Theme.Faint), (_denied.ToString(), Theme.Danger));
        tabs.SetBadge(0, _events.Count > 0 ? _events.Count.ToString() : null);
    }

    private void ShowQrDetail(int rowIndex)
    {
        if (gridEvents.Rows[rowIndex].DataBoundItem is not AccessEvent ev) return;
        try
        {
            using var dlg = new QrDetailForm(ev);
            dlg.ShowDialog(this);
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException)
        {
            // Nội dung quá dài so với dung lượng tối đa của QR, hoặc lỗi tạo ảnh.
            _logger.Warn($"Không tạo được mã QR cho {ev.Code}: {ex.Message}");
            MessageBox.Show(this, $"Không tạo được mã QR cho nội dung này.\n{ex.Message}", "Mã QR",
                MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
    }

    private void ShowTab(int index)
    {
        var log = index == 1;
        gridEvents.Visible = !log;
        logHost.Visible = log;
        chkShowDebug.Visible = log;
        if (log) txtLog.ScrollToCaret();
    }

    /// <summary>Vẽ cột "Kết quả" thành badge màu (xanh mở cửa, đỏ từ chối, cam chờ xác thực).</summary>
    private void GridEvents_CellPainting(object? sender, DataGridViewCellPaintingEventArgs e)
    {
        if (e.RowIndex < 0 || e.ColumnIndex < 0 || gridEvents.Columns[e.ColumnIndex].Name != "colResult") return;
        if (gridEvents.Rows[e.RowIndex].DataBoundItem is not AccessEvent ev || e.Graphics is null) return;

        e.Paint(e.ClipBounds, DataGridViewPaintParts.All & ~DataGridViewPaintParts.ContentForeground);

        var (bg, fg) = ev.Pending ? (Theme.WarningTint, Theme.WarningText)
            : ev.DoorOpened ? (Theme.SuccessTint, Theme.SuccessText)
            : (Theme.DangerTint, Theme.DangerText);
        var text = ev.Result;
        var size = TextRenderer.MeasureText(e.Graphics, text, Theme.SmallBold, Size.Empty, TextFormatFlags.NoPadding);
        var pill = new Rectangle(e.CellBounds.X + 10, e.CellBounds.Y + (e.CellBounds.Height - 22) / 2, size.Width + 20, 22);

        var g = e.Graphics;
        var old = g.SmoothingMode;
        g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
        using (var path = Theme.RoundRect(pill, 11))
        using (var b = new SolidBrush(bg))
            g.FillPath(b, path);
        g.SmoothingMode = old;
        TextRenderer.DrawText(g, text, Theme.SmallBold, pill, fg,
            TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding);
        e.Handled = true;
    }

    protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
    {
        switch (keyData)
        {
            case Keys.F6:
                _ = ToggleReaderAsync();
                return true;
            case Keys.F5:
                _ = ConnectAsync();
                return true;
            case Keys.Shift | Keys.F5:
                _ = _device.DisconnectAsync();
                return true;
            case Keys.Control | Keys.S:
                ExportCsv();
                return true;
        }
        return base.ProcessCmdKey(ref msg, keyData);
    }

    private void ExportCsv()
    {
        if (_events.Count == 0)
        {
            MessageBox.Show(this, "Chưa có sự kiện nào.", "Xuất CSV", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }
        using var dlg = new SaveFileDialog
        {
            Filter = "CSV (*.csv)|*.csv", FileName = $"qr_events_{DateTime.Now:yyyyMMdd_HHmmss}.csv",
        };
        if (dlg.ShowDialog(this) != DialogResult.OK) return;
        try
        {
            EventCsvWriter.Export(dlg.FileName, _events.Reverse());
            _logger.Info($"Đã xuất {_events.Count} sự kiện ra {dlg.FileName}");
        }
        catch (IOException ex)
        {
            MessageBox.Show(this, ex.Message, "Xuất CSV", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private void ClearCurrentTab()
    {
        if (tabs.SelectedIndex == 0) ClearEvents();
        else { _logHistory.Clear(); txtLog.Clear(); }
    }

    private void ClearEvents()
    {
        _events.Clear();
        _granted = _denied = 0;
        UpdateCounters();
        gridEvents.Invalidate();
    }

    // ---------------------------------------------------------------- log

    private void AppendLog(LogEntry e)
    {
        _logHistory.Add(e);
        if (_logHistory.Count > MaxLogLines)
        {
            _logHistory.RemoveRange(0, _logHistory.Count - MaxLogLines);
            RedrawLog();
            return;
        }
        WriteLogLine(e);
        txtLog.ScrollToCaret();
    }

    private void RedrawLog()
    {
        txtLog.SuspendLayout();
        txtLog.Clear();
        foreach (var e in _logHistory) WriteLogLine(e);
        txtLog.ResumeLayout();
        txtLog.ScrollToCaret();
    }

    private void WriteLogLine(LogEntry e)
    {
        if (e.Level == LogLevel.Debug && !chkShowDebug.Checked) return;

        AppendColored(e.Time.ToString("HH:mm:ss.fff") + "  ", Theme.ConsoleTime);
        AppendColored(e.Level.ToString().ToUpperInvariant().PadRight(7), e.Level switch
        {
            LogLevel.Info => Theme.LogInfo,
            LogLevel.Warn => Theme.LogWarn,
            LogLevel.Error => Theme.LogError,
            _ => Theme.LogDebug,
        });
        AppendColored(e.Message + Environment.NewLine, txtLog.ForeColor);
    }

    private void AppendColored(string text, Color color)
    {
        txtLog.SelectionStart = txtLog.TextLength;
        txtLog.SelectionLength = 0;
        txtLog.SelectionColor = color;
        txtLog.AppendText(text);
    }

    // ---------------------------------------------------------------- tiện ích

    private void Ui(Action action)
    {
        if (_closing || IsDisposed || !IsHandleCreated) return;
        if (InvokeRequired) BeginInvoke(action);
        else action();
    }

    private static async Task RunGuarded(Button button, Func<Task<bool>> action)
    {
        button.Enabled = false;
        try { await action(); }
        finally { button.Enabled = true; }
    }

    private void LoadSettings()
    {
        txtIp.Text = _settings.Ip;
        numPort.Value = Math.Clamp(_settings.Port, (int)numPort.Minimum, (int)numPort.Maximum);
        numTimeout.Value = Math.Clamp(_settings.TimeoutMs, (int)numTimeout.Minimum, (int)numTimeout.Maximum);
        // Giá trị cũ ngoài khoảng (vd. 6100 do gõ nhầm) thì về mặc định thay vì kẹp ở mức tối đa vẫn còn chậm.
        numPoll.Value = _settings.PollIntervalMs >= numPoll.Minimum && _settings.PollIntervalMs <= numPoll.Maximum
            ? _settings.PollIntervalMs : new AppSettings().PollIntervalMs;
        numOpenSeconds.Value = Math.Clamp(_settings.OpenSeconds, (int)numOpenSeconds.Minimum, (int)numOpenSeconds.Maximum);
        chkAutoReconnect.Checked = _settings.AutoReconnect;
        txtReaderIp.Text = _settings.ReaderIp;
        numReaderPort.Value = Math.Clamp(_settings.ReaderPort, (int)numReaderPort.Minimum, (int)numReaderPort.Maximum);
        numReaderDoor.Value = Math.Clamp(_settings.ReaderDoor, (int)numReaderDoor.Minimum, (int)numReaderDoor.Maximum);
        chkReaderAutoConnect.Checked = _settings.ReaderAutoConnect;
    }

    private void SaveSettings()
    {
        _settings.Ip = txtIp.Text.Trim();
        _settings.Port = (int)numPort.Value;
        _settings.TimeoutMs = (int)numTimeout.Value;
        _settings.PollIntervalMs = (int)numPoll.Value;
        _settings.OpenSeconds = (int)numOpenSeconds.Value;
        _settings.AutoReconnect = chkAutoReconnect.Checked;
        _settings.ReaderIp = txtReaderIp.Text.Trim();
        _settings.ReaderPort = (int)numReaderPort.Value;
        _settings.ReaderDoor = (int)numReaderDoor.Value;
        _settings.ReaderAutoConnect = chkReaderAutoConnect.Checked;
        _settings.Save();
    }

    protected override async void OnFormClosing(FormClosingEventArgs e)
    {
        if (_closing || (_device.State == ConnectionState.Disconnected && _reader.State == ReaderState.Disconnected))
        {
            SaveSettings();
            base.OnFormClosing(e);
            return;
        }

        // Ngắt kết nối gọn gàng trước khi đóng form.
        e.Cancel = true;
        Enabled = false;
        await _reader.StopAsync();
        await _device.DisconnectAsync();
        _closing = true;
        Close();
    }

    protected override void OnFormClosed(FormClosedEventArgs e)
    {
        _reader.Dispose();
        _device.Dispose();
        base.OnFormClosed(e);
    }
}
