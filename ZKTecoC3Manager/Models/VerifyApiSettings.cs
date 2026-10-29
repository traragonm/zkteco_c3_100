namespace ZKTecoC3Manager.Models;

/// <summary>
/// Cấu hình gọi API xác thực QR/Card. URL, Headers và Body là template:
/// {code} {pin} {door} {direction} {eventType} {eventName} {time} {deviceSerial}.
/// </summary>
public sealed class VerifyApiSettings
{
    public bool Enabled { get; set; }
    public string Method { get; set; } = "POST";
    public string Url { get; set; } = "";

    /// <summary>Mỗi dòng một header dạng "Tên: Giá trị".</summary>
    public string Headers { get; set; } = "Content-Type: application/json";

    public string Body { get; set; } =
        "{\n  \"code\": \"{code}\",\n  \"pin\": \"{pin}\",\n  \"door\": {door},\n  \"direction\": \"{direction}\",\n" +
        "  \"eventType\": {eventType},\n  \"time\": \"{time}\",\n  \"deviceSerial\": \"{deviceSerial}\"\n}";

    public int TimeoutMs { get; set; } = 3000;

    /// <summary>Đường dẫn tới trường kết quả trong JSON trả về, hỗ trợ dạng lồng "data.allow".</summary>
    public string ResultField { get; set; } = "allow";

    /// <summary>true = API lỗi thì hỏi người vận hành; false = từ chối.</summary>
    public bool PromptOnError { get; set; } = true;

    /// <summary>Bỏ qua lượt quét controller đã cho phép (thẻ/QR đã đăng ký trên thiết bị).</summary>
    public bool SkipGranted { get; set; } = true;

    public int OpenSeconds { get; set; } = 5;
    public int DuplicateWindowSeconds { get; set; } = 3;
    public int PromptTimeoutSeconds { get; set; } = 20;

    public bool IsConfigured =>
        Enabled && Uri.TryCreate(Url.Trim(), UriKind.Absolute, out var u) && (u.Scheme == "http" || u.Scheme == "https");

    public VerifyApiSettings Clone() => (VerifyApiSettings)MemberwiseClone();
}
