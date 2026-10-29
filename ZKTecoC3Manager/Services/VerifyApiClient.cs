using System.Diagnostics;
using System.Globalization;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using ZKTecoC3Manager.Models;

namespace ZKTecoC3Manager.Services;

public sealed record VerifyResult(bool Success, bool Allow, string? Message, int? OpenSeconds, string? Error,
    int StatusCode, long ElapsedMs, string ResponseText);

/// <summary>Gọi Verify API theo cấu hình URL / Headers / Body template.</summary>
public static partial class VerifyApiClient
{
    private static readonly HttpClient Http = new() { Timeout = Timeout.InfiniteTimeSpan };

    [GeneratedRegex(@"\{(\w+)\}")]
    private static partial Regex Placeholder();

    public static async Task<VerifyResult> VerifyAsync(VerifyApiSettings s, AccessEvent e, string deviceSerial,
        CancellationToken ct = default)
    {
        var vars = Variables(e, deviceSerial);
        var sw = Stopwatch.StartNew();
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        cts.CancelAfter(Math.Max(500, s.TimeoutMs));

        try
        {
            using var req = BuildRequest(s, vars);
            using var resp = await Http.SendAsync(req, cts.Token);
            var text = await resp.Content.ReadAsStringAsync(cts.Token);
            var status = (int)resp.StatusCode;

            if (!resp.IsSuccessStatusCode)
                return Fail($"HTTP {status} {resp.ReasonPhrase}", status, sw, text);

            return ParseResponse(s.ResultField, text, status, sw);
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
        {
            return Fail($"Timeout sau {s.TimeoutMs} ms", 0, sw, "");
        }
        catch (Exception ex) when (ex is HttpRequestException or UriFormatException or FormatException or InvalidOperationException)
        {
            return Fail(ex.Message, 0, sw, "");
        }
    }

    private static HttpRequestMessage BuildRequest(VerifyApiSettings s, IReadOnlyDictionary<string, string> vars)
    {
        var url = Fill(s.Url.Trim(), vars, Uri.EscapeDataString);
        var method = new HttpMethod(string.IsNullOrWhiteSpace(s.Method) ? "POST" : s.Method.Trim().ToUpperInvariant());
        var req = new HttpRequestMessage(method, url);

        string? contentType = null;
        foreach (var (name, value) in ParseHeaders(s.Headers))
        {
            var v = Fill(value, vars, x => x);
            if (name.Equals("Content-Type", StringComparison.OrdinalIgnoreCase)) contentType = v;
            else req.Headers.TryAddWithoutValidation(name, v);
        }

        if (method != HttpMethod.Get && !string.IsNullOrWhiteSpace(s.Body))
        {
            var isJson = contentType?.Contains("json", StringComparison.OrdinalIgnoreCase)
                         ?? s.Body.TrimStart().StartsWith('{') || s.Body.TrimStart().StartsWith('[');
            // Trong body JSON, giá trị được escape để mã QR chứa dấu " hoặc \ không làm hỏng JSON.
            var body = Fill(s.Body, vars, isJson ? JsonEscape : x => x);
            req.Content = new StringContent(body, Encoding.UTF8);
            req.Content.Headers.ContentType = MediaTypeHeaderValue.Parse(contentType ?? (isJson ? "application/json" : "text/plain"));
            req.Content.Headers.ContentType.CharSet ??= "utf-8";
        }
        return req;
    }

    private static VerifyResult ParseResponse(string field, string text, int status, Stopwatch sw)
    {
        JsonDocument doc;
        try { doc = JsonDocument.Parse(text); }
        catch (JsonException) { return Fail("Response không phải JSON", status, sw, text); }

        using (doc)
        {
            if (!TryGetPath(doc.RootElement, field, out var value))
                return Fail($"Không có trường \"{field}\" trong response", status, sw, text);
            if (!TryGetBool(value, out var allow))
                return Fail($"Trường \"{field}\" không phải true/false", status, sw, text);

            string? message = TryGetPath(doc.RootElement, "message", out var m) && m.ValueKind == JsonValueKind.String
                ? m.GetString() : null;
            int? openSeconds = TryGetPath(doc.RootElement, "openSeconds", out var o) && o.TryGetInt32(out var os)
                ? os : null;

            return new VerifyResult(true, allow, message, openSeconds, null, status, sw.ElapsedMilliseconds, text);
        }
    }

    private static VerifyResult Fail(string error, int status, Stopwatch sw, string text) =>
        new(false, false, null, null, error, status, sw.ElapsedMilliseconds, text);

    private static Dictionary<string, string> Variables(AccessEvent e, string deviceSerial) => new(StringComparer.OrdinalIgnoreCase)
    {
        ["code"] = e.Code,
        ["pin"] = e.Pin,
        ["door"] = e.Door.ToString(CultureInfo.InvariantCulture),
        ["direction"] = e.Direction == "Ra" ? "out" : "in",
        ["eventType"] = e.EventType.ToString(CultureInfo.InvariantCulture),
        ["eventName"] = e.EventName,
        ["time"] = e.Time.ToString("yyyy-MM-ddTHH:mm:ss", CultureInfo.InvariantCulture),
        ["deviceSerial"] = deviceSerial,
    };

    /// <summary>Thay {biến}; biến không biết giữ nguyên để không phá JSON có dấu ngoặc nhọn.</summary>
    private static string Fill(string template, IReadOnlyDictionary<string, string> vars, Func<string, string> encode) =>
        Placeholder().Replace(template, m => vars.TryGetValue(m.Groups[1].Value, out var v) ? encode(v) : m.Value);

    public static IEnumerable<(string Name, string Value)> ParseHeaders(string text)
    {
        foreach (var raw in text.Split('\n'))
        {
            var line = raw.Trim();
            var i = line.IndexOf(':');
            if (i > 0) yield return (line[..i].Trim(), line[(i + 1)..].Trim());
        }
    }

    private static string JsonEscape(string s)
    {
        var quoted = JsonSerializer.Serialize(s);
        return quoted[1..^1];
    }

    private static bool TryGetPath(JsonElement root, string path, out JsonElement value)
    {
        value = root;
        foreach (var part in path.Split('.', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            if (value.ValueKind != JsonValueKind.Object || !value.TryGetProperty(part, out value)) return false;
        }
        return true;
    }

    private static bool TryGetBool(JsonElement v, out bool result)
    {
        switch (v.ValueKind)
        {
            case JsonValueKind.True: result = true; return true;
            case JsonValueKind.False: result = false; return true;
            case JsonValueKind.Number when v.TryGetInt32(out var n): result = n != 0; return true;
            case JsonValueKind.String:
                var s = v.GetString()?.Trim().ToLowerInvariant();
                if (s is "true" or "1" or "yes" or "allow" or "ok") { result = true; return true; }
                if (s is "false" or "0" or "no" or "deny") { result = false; return true; }
                break;
        }
        result = false;
        return false;
    }
}
