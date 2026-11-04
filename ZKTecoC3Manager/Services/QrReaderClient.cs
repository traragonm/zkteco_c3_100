using System.Net.Sockets;
using System.Text;

namespace ZKTecoC3Manager.Services;

public enum ReaderState { Disconnected, Connecting, Connected, Reconnecting }

public sealed record ReaderSettings(string Ip, int Port);

/// <summary>Một bản tin (một lượt quét) nhận từ đầu đọc.</summary>
public sealed record ReaderFrame(DateTime Time, byte[] Data)
{
    private static readonly Encoding StrictUtf8 = new UTF8Encoding(false, throwOnInvalidBytes: true);

    /// <summary>Nội dung mã: giải mã UTF-8, bỏ ký tự điều khiển (CR/LF, STX/ETX...) và khoảng trắng hai đầu.</summary>
    public string Text
    {
        get
        {
            var s = Encoding.UTF8.GetString(Data);
            var sb = new StringBuilder(s.Length);
            foreach (var c in s)
                if (!char.IsControl(c) && c != '�') sb.Append(c);
            return sb.ToString().Trim();
        }
    }

    public string Hex => Convert.ToHexString(Data);

    /// <summary>
    /// Tỉ lệ byte đọc được thấp → thường do sai baudrate trên Waveshare hoặc đảo dây A/B,
    /// dữ liệu nhận được là rác chứ không phải nội dung mã.
    /// </summary>
    public bool LooksGarbled
    {
        get
        {
            if (Data.Length == 0) return true;
            string s;
            try { s = StrictUtf8.GetString(Data); }
            catch (DecoderFallbackException) { return true; }
            var ok = s.Count(c => !char.IsControl(c));
            return ok / (double)s.Length < 0.7;
        }
    }
}

/// <summary>
/// Đọc mã QR / thẻ từ đầu đọc RS485 qua bộ chuyển Waveshare RS485 TO ETH
/// (chế độ Transparent, Waveshare làm TCP Server, mặc định cổng 4196).
/// Waveshare chuyển nguyên byte từ RS485 sang TCP nên một lượt quét có thể đến thành nhiều gói nhỏ:
/// gom lại thành một bản tin khi gặp CR/LF hoặc khi đường truyền im lặng <see cref="FrameGapMs"/>.
/// </summary>
public sealed class QrReaderClient : IDisposable
{
    private const int FrameGapMs = 150;
    private const int ConnectTimeoutMs = 5000;
    private const int ReconnectDelayMs = 3000;

    private readonly AppLogger _log;
    private CancellationTokenSource? _cts;
    private Task? _task;
    private ReaderState _state = ReaderState.Disconnected;

    public event Action<ReaderState>? StateChanged;
    public event Action<ReaderFrame>? FrameReceived;

    public QrReaderClient(AppLogger log) => _log = log;

    public ReaderState State => _state;
    public ReaderSettings? Settings { get; private set; }

    public void Start(ReaderSettings settings)
    {
        if (_state != ReaderState.Disconnected) return;
        Settings = settings;
        _cts?.Dispose();
        _cts = new CancellationTokenSource();
        var ct = _cts.Token;
        SetState(ReaderState.Connecting);
        _task = Task.Run(() => RunAsync(settings, ct), ct);
    }

    public async Task StopAsync()
    {
        if (_cts is null) return;
        _cts.Cancel();
        try { if (_task is not null) await _task; }
        catch (OperationCanceledException) { }
        _cts.Dispose();
        _cts = null;
        _task = null;
        if (_state != ReaderState.Disconnected)
        {
            _log.Info("Đầu đọc QR: đã ngắt kết nối");
            SetState(ReaderState.Disconnected);
        }
    }

    private async Task RunAsync(ReaderSettings s, CancellationToken ct)
    {
        var target = $"{s.Ip}:{s.Port}";
        var attempt = 0;
        while (!ct.IsCancellationRequested)
        {
            attempt++;
            try
            {
                using var client = new TcpClient { NoDelay = true };
                client.Client.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.KeepAlive, true);
                // Phát hiện Waveshare mất điện / rút cáp mạng sau khoảng 10 + 3×2 giây không phản hồi.
                client.Client.SetSocketOption(SocketOptionLevel.Tcp, SocketOptionName.TcpKeepAliveTime, 10);
                client.Client.SetSocketOption(SocketOptionLevel.Tcp, SocketOptionName.TcpKeepAliveInterval, 2);
                client.Client.SetSocketOption(SocketOptionLevel.Tcp, SocketOptionName.TcpKeepAliveRetryCount, 3);

                _log.Info($"Đầu đọc QR: kết nối {target}{(attempt > 1 ? $" (lần {attempt})" : "")}...");
                using (var connectCts = CancellationTokenSource.CreateLinkedTokenSource(ct))
                {
                    connectCts.CancelAfter(ConnectTimeoutMs);
                    try { await client.ConnectAsync(s.Ip, s.Port, connectCts.Token); }
                    catch (OperationCanceledException) when (!ct.IsCancellationRequested)
                    {
                        throw new TimeoutException($"không phản hồi sau {ConnectTimeoutMs} ms");
                    }
                }

                attempt = 0;
                _log.Info($"Đầu đọc QR: đã kết nối {target}, chờ quét mã...");
                SetState(ReaderState.Connected);
                await ReadLoopAsync(client.GetStream(), ct);
                _log.Warn("Đầu đọc QR: Waveshare đóng kết nối");
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested) { break; }
            catch (Exception ex) when (ex is SocketException or IOException or TimeoutException)
            {
                _log.Warn($"Đầu đọc QR: lỗi kết nối {target}: {ex.Message}");
            }

            if (ct.IsCancellationRequested) break;
            SetState(ReaderState.Reconnecting);
            try { await Task.Delay(ReconnectDelayMs, ct); }
            catch (OperationCanceledException) { break; }
        }
    }

    private async Task ReadLoopAsync(NetworkStream stream, CancellationToken ct)
    {
        var buf = new byte[4096];
        var acc = new List<byte>(256);
        Task<int>? pending = null;

        while (!ct.IsCancellationRequested)
        {
            pending ??= stream.ReadAsync(buf, 0, buf.Length, ct);

            if (acc.Count > 0)
            {
                // Đang có dữ liệu dở: im lặng quá FrameGapMs thì coi như hết một bản tin.
                var done = await Task.WhenAny(pending, Task.Delay(FrameGapMs, ct));
                if (done != pending)
                {
                    Emit(acc);
                    continue;
                }
            }

            var n = await pending;
            pending = null;
            if (n == 0) { Emit(acc); return; }

            _log.Debug($"Đầu đọc QR RX {n} byte: {Convert.ToHexString(buf, 0, n)}");
            for (var i = 0; i < n; i++)
            {
                var b = buf[i];
                if (b is (byte)'\r' or (byte)'\n') Emit(acc);
                else acc.Add(b);
            }
        }
    }

    private void Emit(List<byte> acc)
    {
        if (acc.Count == 0) return;
        var frame = new ReaderFrame(DateTime.Now, acc.ToArray());
        acc.Clear();
        FrameReceived?.Invoke(frame);
    }

    private void SetState(ReaderState s)
    {
        if (_state == s) return;
        _state = s;
        StateChanged?.Invoke(s);
    }

    public void Dispose()
    {
        _cts?.Cancel();
        try { _task?.Wait(2000); } catch (AggregateException) { }
        _cts?.Dispose();
    }
}
