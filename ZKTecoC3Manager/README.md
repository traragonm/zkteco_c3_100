# ZKTeco C3-100 Manager

Ứng dụng WinForms (.NET 8) kết nối bộ điều khiển ZKTeco C3-100 qua PullSDK.

## Chuẩn bị

1. Chép toàn bộ DLL của PullSDK (`plcommpro.dll` và các DLL đi kèm) vào `ZKTecoSDK\`.
   Khi build, các DLL này được copy ra cạnh file exe.
2. PullSDK phổ biến là bản 32-bit, nên project build `x86` và cần cài
   **.NET 8 Desktop Runtime x86**. Nếu dùng PullSDK 64-bit, đổi `PlatformTarget` trong `.csproj` thành `x64`.
3. Đầu đọc QR (QR500/QR600...) nối với C3-100 qua Wiegand/RS485. Mã QR được controller
   gửi lên như số thẻ, nên nằm ở cột **Mã QR / Card**.

## Chạy

```
dotnet run --project ZKTecoC3Manager
```

## Xác thực QR / Card (menu Cài đặt › Verify API QR/Card...)

- Cấu hình gồm **URL**, **Headers** (mỗi dòng `Tên: Giá trị`) và **Body data**. Cả ba đều dùng được biến
  `{code} {pin} {door} {direction} {eventType} {eventName} {time} {deviceSerial}`.
  Trong URL giá trị được URL-encode, trong body JSON giá trị được escape theo JSON.
- API trả về JSON, ví dụ `{ "allow": true, "message": "...", "openSeconds": 5 }`. Tên trường kết quả
  cấu hình được, hỗ trợ dạng lồng như `data.allow`.
- Chưa bật API (hoặc URL trống/không hợp lệ) → hiện popup hỏi người vận hành có mở cửa không;
  hết thời gian chờ thì tự từ chối. Khi API lỗi có thể chọn hỏi popup hoặc từ chối.
- Để app quyết định mở cửa, mã QR **không** được đăng ký trên controller (controller báo
  "Thẻ chưa đăng ký" rồi app gọi `ControlDevice` để mở).
- Header (vd. token) được lưu dạng plain text trong `settings.json`.

## File log (thư mục `logs\` cạnh exe)

- `connection_yyyy-MM-dd.log` — log kết nối / lỗi SDK / dữ liệu RTLog thô.
- `events_yyyy-MM-dd.csv` — mỗi lượt quét QR một dòng.
