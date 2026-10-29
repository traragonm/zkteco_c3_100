using System.Text;
using ZKTecoC3Manager.Models;

namespace ZKTecoC3Manager.Services;

/// <summary>Ghi log kết nối ra file theo ngày và phát sự kiện cho UI.</summary>
public sealed class AppLogger
{
    private readonly object _sync = new();
    private readonly string _dir;

    public event Action<LogEntry>? Logged;

    public AppLogger(string dir)
    {
        _dir = dir;
        Directory.CreateDirectory(_dir);
    }

    public string CurrentFile => Path.Combine(_dir, $"connection_{DateTime.Now:yyyy-MM-dd}.log");

    public void Debug(string msg) => Write(LogLevel.Debug, msg);
    public void Info(string msg) => Write(LogLevel.Info, msg);
    public void Warn(string msg) => Write(LogLevel.Warn, msg);
    public void Error(string msg) => Write(LogLevel.Error, msg);

    public void Write(LogLevel level, string msg)
    {
        var entry = new LogEntry(DateTime.Now, level, msg);
        lock (_sync)
        {
            try { File.AppendAllText(CurrentFile, entry + Environment.NewLine, Encoding.UTF8); }
            catch (IOException) { /* không để lỗi ghi file làm hỏng luồng kết nối */ }
        }
        Logged?.Invoke(entry);
    }
}
