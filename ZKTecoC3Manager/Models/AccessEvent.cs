namespace ZKTecoC3Manager.Models;

/// <summary>Một sự kiện quét QR/thẻ đọc được từ GetRTLog.</summary>
public sealed class AccessEvent
{
    public DateTime Time { get; init; }
    public int Door { get; init; }
    public string Direction { get; init; } = "";
    public string Code { get; init; } = "";
    public string Pin { get; init; } = "";
    public int EventType { get; init; }
    public string EventName { get; init; } = "";
    public int VerifyType { get; init; }
    /// <summary>Controller đã cho phép (thẻ/QR đăng ký trên thiết bị).</summary>
    public bool Granted { get; init; }
    public string Raw { get; init; } = "";

    // Kết quả cuối cùng sau khi xác thực bằng API / người vận hành.
    public bool Pending { get; set; }
    public bool DoorOpened { get; set; }
    public string VerifyNote { get; set; } = "";

    public string DoorText => $"{Door} · {Direction}";
    public string Result =>Pending ? "Chờ xác thực" : DoorOpened ? "Mở cửa" : "Từ chối";
}

/// <summary>Bản ghi trạng thái (eventtype = 255) mà C3 trả về định kỳ.</summary>
public sealed class DoorStatus
{
    public DateTime Time { get; init; }
    /// <summary>Trạng thái sensor cửa 1: 0 = không có sensor, 1 = đóng, 2 = mở.</summary>
    public int Door1Sensor { get; init; }
    public int Alarm { get; init; }
}
