using System.Diagnostics;
using System.Globalization;
using System.Text;
using ZKTecoC3Manager.Models;
using ZKTecoC3Manager.Sdk;

namespace ZKTecoC3Manager.Services;

public enum ConnectionState { Disconnected, Connecting, Connected, Reconnecting }

public sealed record ConnectionSettings(string Ip, int Port, int TimeoutMs, string Password, int PollIntervalMs, bool AutoReconnect)
{
    public string ToConnectString() =>
        $"protocol=TCP,ipaddress={Ip},port={Port},timeout={TimeoutMs},passwd={Password}";

    public string ToSafeString() =>
        $"protocol=TCP,ipaddress={Ip},port={Port},timeout={TimeoutMs},passwd={(Password.Length > 0 ? "***" : "")}";
}

public sealed record DeviceInfo(string Serial, string Firmware, int LockCount, DateTime? DeviceTime);

/// <summary>
/// Quản lý kết nối tới C3-100 qua PullSDK: kết nối, polling GetRTLog,
/// tự kết nối lại, điều khiển cửa. Mọi lời gọi SDK đi qua một cổng duy nhất
/// (<see cref="_gate"/>) vì plcommpro.dll không thread-safe.
/// </summary>
public sealed class C3Controller : IDisposable
{
    private const int MaxPollFailures = 3;
    private const int ReconnectDelayMs = 3000;

    private readonly AppLogger _log;
    private readonly SemaphoreSlim _gate = new(1, 1);
    private readonly byte[] _rtBuffer = new byte[256 * 1024];

    private IntPtr _handle = IntPtr.Zero;
    private CancellationTokenSource? _pollCts;
    private Task? _pollTask;
    private ConnectionSettings? _settings;
    private ConnectionState _state = ConnectionState.Disconnected;

    public event Action<ConnectionState>? StateChanged;
    public event Action<IReadOnlyList<AccessEvent>>? EventsReceived;
    public event Action<DoorStatus>? StatusReceived;
    public event Action<DeviceInfo>? DeviceInfoReceived;

    public C3Controller(AppLogger log) => _log = log;

    public ConnectionState State => _state;
    public bool IsConnected => _state == ConnectionState.Connected;
    public ConnectionSettings? Settings => _settings;

    public async Task<bool> ConnectAsync(ConnectionSettings settings)
    {
        if (_state != ConnectionState.Disconnected) return IsConnected;

        _settings = settings;
        SetState(ConnectionState.Connecting);
        _log.Info($"Kết nối: {settings.ToSafeString()}");

        if (!await TryOpenAsync(settings))
        {
            SetState(ConnectionState.Disconnected);
            return false;
        }

        SetState(ConnectionState.Connected);
        await RefreshDeviceInfoAsync();
        StartPolling();
        return true;
    }

    public async Task DisconnectAsync()
    {
        if (_state == ConnectionState.Disconnected) return;

        await StopPollingAsync();
        await CloseHandleAsync();
        _log.Info("Đã ngắt kết nối");
        SetState(ConnectionState.Disconnected);
    }

    public async Task<DeviceInfo?> RefreshDeviceInfoAsync()
    {
        const string items = "~SerialNumber,FirmVer,LockCount,DateTime";
        _log.Debug($"GetDeviceParam {items}");

        var buffer = new byte[4096];
        var ret = await CallAsync(h => PullSdk.GetDeviceParam(h, buffer, buffer.Length, items));
        if (ret < 0)
        {
            _log.Warn($"GetDeviceParam lỗi {ret} (PullLastError={await LastErrorAsync()})");
            return null;
        }

        var values = ParseKeyValues(DecodeBuffer(buffer));
        var info = new DeviceInfo(
            values.GetValueOrDefault("~SerialNumber", "?"),
            values.GetValueOrDefault("FirmVer", "?"),
            int.TryParse(values.GetValueOrDefault("LockCount"), out var lc) ? lc : 0,
            long.TryParse(values.GetValueOrDefault("DateTime"), out var dt) ? DecodeDeviceTime(dt) : null);

        _log.Info($"Thiết bị: SN={info.Serial}, FW={info.Firmware}, LockCount={info.LockCount}, Giờ={info.DeviceTime:yyyy-MM-dd HH:mm:ss}");
        DeviceInfoReceived?.Invoke(info);
        return info;
    }

    public async Task<bool> SyncTimeAsync()
    {
        var value = EncodeDeviceTime(DateTime.Now);
        var ret = await CallAsync(h => PullSdk.SetDeviceParam(h, $"DateTime={value}"));
        if (ret < 0)
        {
            _log.Error($"Đồng bộ giờ thất bại: {ret} (PullLastError={await LastErrorAsync()})");
            return false;
        }
        _log.Info($"Đã đồng bộ giờ thiết bị = {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
        await RefreshDeviceInfoAsync();
        return true;
    }

    /// <param name="seconds">1..254 giây, 255 = mở thường trực, 0 = đóng.</param>
    public async Task<bool> SetDoorAsync(int door, int seconds)
    {
        seconds = Math.Clamp(seconds, 0, 255);
        var ret = await CallAsync(h => PullSdk.ControlDevice(h, 1, door, 1, seconds, 0, ""));
        var action = seconds == 0 ? "Đóng" : seconds == 255 ? "Mở thường trực" : $"Mở {seconds}s";
        if (ret < 0)
        {
            _log.Error($"{action} cửa {door} thất bại: {ret} (PullLastError={await LastErrorAsync()})");
            return false;
        }
        _log.Info($"{action} cửa {door}: OK");
        return true;
    }

    // ---------------------------------------------------------------- polling

    private void StartPolling()
    {
        // Vòng polling cũ có thể đã tự kết thúc (mất kết nối, không bật tự kết nối lại).
        _pollCts?.Dispose();
        _pollCts = new CancellationTokenSource();
        var ct = _pollCts.Token;
        _pollTask = Task.Run(() => PollLoopAsync(ct), ct);
        _log.Info($"Bắt đầu polling GetRTLog mỗi {_settings!.PollIntervalMs} ms");
    }

    private async Task StopPollingAsync()
    {
        if (_pollCts is null) return;
        _pollCts.Cancel();
        try { if (_pollTask is not null) await _pollTask; }
        catch (OperationCanceledException) { }
        _pollCts.Dispose();
        _pollCts = null;
        _pollTask = null;
        _log.Info("Dừng polling");
    }

    private async Task PollLoopAsync(CancellationToken ct)
    {
        var failures = 0;
        var events = new List<AccessEvent>();
        var statuses = new List<DoorStatus>();

        while (!ct.IsCancellationRequested)
        {
            var ret = await CallAsync(h =>
            {
                Array.Clear(_rtBuffer);
                return PullSdk.GetRTLog(h, _rtBuffer, _rtBuffer.Length);
            }, ct);

            if (ret >= 0)
            {
                if (failures > 0) _log.Info("GetRTLog đã phục hồi");
                failures = 0;
                HandleRtLog(DecodeBuffer(_rtBuffer), events, statuses);
            }
            else
            {
                failures++;
                _log.Warn($"GetRTLog trả về {ret}, lỗi {failures}/{MaxPollFailures}");
                if (failures >= MaxPollFailures)
                {
                    _log.Error("Mất kết nối với thiết bị");
                    if (!_settings!.AutoReconnect)
                    {
                        await CloseHandleAsync();
                        SetState(ConnectionState.Disconnected);
                        return;
                    }
                    await ReconnectAsync(ct);
                    failures = 0;
                }
            }

            await Task.Delay(_settings!.PollIntervalMs, ct);
        }
    }

    private void HandleRtLog(string raw, List<AccessEvent> events, List<DoorStatus> statuses)
    {
        if (string.IsNullOrWhiteSpace(raw)) return;

        events.Clear();
        statuses.Clear();
        RtLogParser.Parse(raw, events, statuses);

        foreach (var e in events) _log.Debug($"RTLog: {e.Raw}");
        if (events.Count > 0) EventsReceived?.Invoke(events.ToArray());
        if (statuses.Count > 0) StatusReceived?.Invoke(statuses[^1]);
    }

    private async Task ReconnectAsync(CancellationToken ct)
    {
        SetState(ConnectionState.Reconnecting);
        await CloseHandleAsync();

        for (var attempt = 1; !ct.IsCancellationRequested; attempt++)
        {
            _log.Info($"Thử kết nối lại lần {attempt}...");
            if (await TryOpenAsync(_settings!))
            {
                SetState(ConnectionState.Connected);
                await RefreshDeviceInfoAsync();
                return;
            }
            await Task.Delay(ReconnectDelayMs, ct);
        }
    }

    // ---------------------------------------------------------------- SDK helpers

    private async Task<bool> TryOpenAsync(ConnectionSettings s)
    {
        var connectString = s.ToConnectString();
        var sw = Stopwatch.StartNew();
        IntPtr handle;
        int lastError = 0;

        await _gate.WaitAsync();
        try
        {
            handle = await Task.Run(() =>
            {
                var h = PullSdk.Connect(connectString);
                if (h == IntPtr.Zero) lastError = PullSdk.PullLastError();
                return h;
            });
            _handle = handle;
        }
        catch (DllNotFoundException ex)
        {
            _log.Error($"Không tìm thấy plcommpro.dll hoặc DLL phụ thuộc: {ex.Message}");
            return false;
        }
        catch (BadImageFormatException)
        {
            _log.Error("Sai kiến trúc DLL: PullSDK 32-bit cần build x86 (PlatformTarget).");
            return false;
        }
        finally { _gate.Release(); }

        if (handle == IntPtr.Zero)
        {
            _log.Error($"Connect thất bại sau {sw.ElapsedMilliseconds} ms, PullLastError={lastError} ({DescribeError(lastError)})");
            return false;
        }

        _log.Info($"Connect OK, handle=0x{handle.ToInt64():X8} ({sw.ElapsedMilliseconds} ms)");
        return true;
    }

    private async Task CloseHandleAsync()
    {
        await _gate.WaitAsync();
        try
        {
            if (_handle == IntPtr.Zero) return;
            var h = _handle;
            _handle = IntPtr.Zero;
            await Task.Run(() => PullSdk.Disconnect(h));
            _log.Info($"Disconnect handle=0x{h.ToInt64():X8}");
        }
        finally { _gate.Release(); }
    }

    /// <summary>Gọi một hàm SDK cần handle, tuần tự hoá qua <see cref="_gate"/>. Trả về -999 nếu chưa có handle.</summary>
    private async Task<int> CallAsync(Func<IntPtr, int> call, CancellationToken ct = default)
    {
        await _gate.WaitAsync(ct);
        try
        {
            if (_handle == IntPtr.Zero) return -999;
            var h = _handle;
            return await Task.Run(() => call(h), CancellationToken.None);
        }
        finally { _gate.Release(); }
    }

    private async Task<int> LastErrorAsync()
    {
        await _gate.WaitAsync();
        try { return PullSdk.PullLastError(); }
        finally { _gate.Release(); }
    }

    private static string DecodeBuffer(byte[] buffer)
    {
        var len = Array.IndexOf(buffer, (byte)0);
        return Encoding.UTF8.GetString(buffer, 0, len < 0 ? buffer.Length : len);
    }

    private static Dictionary<string, string> ParseKeyValues(string s)
    {
        var dict = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var part in s.Split(',', StringSplitOptions.RemoveEmptyEntries))
        {
            var i = part.IndexOf('=');
            if (i > 0) dict[part[..i].Trim()] = part[(i + 1)..].Trim();
        }
        return dict;
    }

    /// <summary>C3 lưu giờ dạng số giây theo lịch 12 tháng × 31 ngày tính từ năm 2000.</summary>
    public static long EncodeDeviceTime(DateTime t) =>
        ((long)(t.Year - 2000) * 12 * 31 + (t.Month - 1) * 31 + (t.Day - 1)) * 86400
        + (t.Hour * 60 + t.Minute) * 60 + t.Second;

    public static DateTime? DecodeDeviceTime(long v)
    {
        try
        {
            var sec = (int)(v % 60); v /= 60;
            var min = (int)(v % 60); v /= 60;
            var hour = (int)(v % 24); v /= 24;
            var day = (int)(v % 31) + 1; v /= 31;
            var month = (int)(v % 12) + 1; v /= 12;
            var year = (int)v + 2000;
            return new DateTime(year, month, day, hour, min, sec);
        }
        catch (ArgumentOutOfRangeException) { return null; }
    }

    private static string DescribeError(int code) => code switch
    {
        -1 => "lệnh gửi lỗi",
        -2 => "timeout / không phản hồi",
        -3 => "bộ đệm không đủ",
        -4 => "giải nén lỗi",
        -5 => "độ dài dữ liệu sai",
        -6 => "lỗi giải nén dữ liệu",
        -7 => "lệnh lặp lại",
        -8 => "kết nối chưa được xác thực",
        -9 => "lỗi dữ liệu",
        -10 => "kiểm tra CRC sai",
        -11 => "dữ liệu không phù hợp",
        -12 => "sai mật khẩu kết nối (comm key)",
        -13 => "dữ liệu nhận được sai",
        -14 => "thiết bị bận",
        -15 => "tham số sai",
        -307 => "không kết nối được (sai IP/cổng hoặc mạng)",
        _ => "không xác định",
    };

    private void SetState(ConnectionState s)
    {
        if (_state == s) return;
        _state = s;
        StateChanged?.Invoke(s);
    }

    public void Dispose()
    {
        _pollCts?.Cancel();
        try { _pollTask?.Wait(2000); } catch (AggregateException) { }
        if (_handle != IntPtr.Zero)
        {
            try { PullSdk.Disconnect(_handle); } catch { /* đang thoát ứng dụng */ }
            _handle = IntPtr.Zero;
        }
        _gate.Dispose();
    }
}
