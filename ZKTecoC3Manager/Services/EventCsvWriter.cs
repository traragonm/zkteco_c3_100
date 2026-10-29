using System.Text;
using ZKTecoC3Manager.Models;

namespace ZKTecoC3Manager.Services;

/// <summary>Lưu mỗi sự kiện quét QR vào file CSV theo ngày.</summary>
public sealed class EventCsvWriter
{
    private const string Header = "Time,Door,Direction,Code,Pin,EventType,EventName,VerifyType,Verify,Result";
    private readonly object _sync = new();
    private readonly string _dir;

    public EventCsvWriter(string dir)
    {
        _dir = dir;
        Directory.CreateDirectory(_dir);
    }

    public string CurrentFile => Path.Combine(_dir, $"events_{DateTime.Now:yyyy-MM-dd}.csv");

    public void Append(AccessEvent e)
    {
        lock (_sync)
        {
            var file = CurrentFile;
            var isNew = !File.Exists(file);
            using var w = new StreamWriter(file, append: true, new UTF8Encoding(encoderShouldEmitUTF8Identifier: isNew));
            if (isNew) w.WriteLine(Header);
            w.WriteLine(ToCsv(e));
        }
    }

    public static void Export(string path, IEnumerable<AccessEvent> events)
    {
        using var w = new StreamWriter(path, append: false, new UTF8Encoding(true));
        w.WriteLine(Header);
        foreach (var e in events) w.WriteLine(ToCsv(e));
    }

    private static string ToCsv(AccessEvent e) => string.Join(",",
        e.Time.ToString("yyyy-MM-dd HH:mm:ss"), e.Door, Quote(e.Direction), Quote(e.Code), Quote(e.Pin),
        e.EventType, Quote(e.EventName), e.VerifyType, Quote(e.VerifyNote), Quote(e.Result));

    private static string Quote(string s) =>
        s.IndexOfAny([',', '"', '\n', '\r']) >= 0 ? "\"" + s.Replace("\"", "\"\"") + "\"" : s;
}
