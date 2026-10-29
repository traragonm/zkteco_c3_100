using System.Globalization;
using ZKTecoC3Manager.Models;

namespace ZKTecoC3Manager.Services;

/// <summary>
/// Phân tích chuỗi trả về từ GetRTLog. Mỗi dòng có dạng:
///   time,pin,cardno,doorid,eventtype,inoutstate,verifytype
/// Dòng có eventtype = 255 là bản ghi trạng thái:
///   time,doorsensor,alarm,?,255,?,?
/// Đầu đọc QR (vd. QR500/QR600 qua Wiegand hoặc RS485) gửi nội dung mã lên
/// controller như một số thẻ, nên mã QR nằm ở trường cardno.
/// </summary>
public static class RtLogParser
{
    public static void Parse(string raw, List<AccessEvent> events, List<DoorStatus> statuses)
    {
        foreach (var line in raw.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries))
        {
            var f = line.Split(',');
            if (f.Length < 7) continue;
            if (!DateTime.TryParseExact(f[0].Trim(), "yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture,
                    DateTimeStyles.None, out var time))
                time = DateTime.Now;

            var eventType = ToInt(f[4]);
            if (eventType == 255)
            {
                statuses.Add(new DoorStatus { Time = time, Door1Sensor = ToInt(f[1]) & 0xFF, Alarm = ToInt(f[2]) });
                continue;
            }

            events.Add(new AccessEvent
            {
                Time = time,
                Pin = f[1].Trim(),
                Code = f[2].Trim(),
                Door = ToInt(f[3]),
                EventType = eventType,
                EventName = EventName(eventType),
                Direction = ToInt(f[5]) switch { 0 => "Vào", 1 => "Ra", _ => "—" },
                VerifyType = ToInt(f[6]),
                Granted = IsGranted(eventType),
                Raw = line,
            });
        }
    }

    /// <summary>Sự kiện có ý nghĩa với việc quét QR/thẻ (bỏ qua các sự kiện cửa đóng/mở, aux input...).</summary>
    public static bool IsScanEvent(AccessEvent e) =>
        !string.IsNullOrEmpty(e.Code) && e.Code != "0";

    private static bool IsGranted(int t) => t is 0 or 1 or 3 or 14 or 15 or 16 or 17 or 18 or 19;

    public static string EventName(int t) => t switch
    {
        0 => "Quét thẻ/QR hợp lệ",
        1 => "Hợp lệ (khung giờ mở thường trực)",
        2 => "Thẻ đầu tiên mở cửa",
        3 => "Đa thẻ mở cửa",
        4 => "Mật khẩu khẩn cấp",
        5 => "Mở trong khung giờ thường trực",
        6 => "Kích hoạt liên động",
        7 => "Huỷ báo động",
        8 => "Mở cửa từ xa",
        9 => "Đóng cửa từ xa",
        20 => "Quét quá nhanh",
        21 => "Cửa ngoài khung giờ hoạt động",
        22 => "Ngoài khung giờ",
        23 => "Từ chối truy cập",
        24 => "Anti-passback",
        25 => "Interlock",
        26 => "Chờ xác thực đa thẻ",
        27 => "Thẻ chưa đăng ký",
        28 => "Cửa mở quá thời gian",
        29 => "Thẻ hết hạn",
        30 => "Sai mật khẩu",
        36 => "Sai chế độ xác thực",
        101 => "Báo động cưỡng ép",
        102 => "Cửa bị mở cưỡng bức",
        200 => "Cửa đã mở",
        201 => "Cửa đã đóng",
        202 => "Nút Exit mở cửa",
        204 => "Kết thúc khung giờ thường trực",
        205 => "Mở thường trực từ xa",
        206 => "Thiết bị khởi động",
        220 => "Aux input ngắt",
        221 => "Aux input nối",
        _ => $"Sự kiện {t}",
    };

    private static int ToInt(string s) =>
        int.TryParse(s.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var v) ? v : 0;
}
