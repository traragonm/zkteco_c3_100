namespace ZKTecoC3Manager.Models;

public enum LogLevel { Debug, Info, Warn, Error }

public sealed record LogEntry(DateTime Time, LogLevel Level, string Message)
{
    public override string ToString() =>
        $"{Time:yyyy-MM-dd HH:mm:ss.fff} [{Level.ToString().ToUpperInvariant()}] {Message}";
}
