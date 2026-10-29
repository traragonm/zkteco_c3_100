using System.Runtime.InteropServices;

namespace ZKTecoC3Manager.Sdk;

/// <summary>
/// P/Invoke cho ZKTeco PullSDK (plcommpro.dll) dùng với dòng C3/inBio.
/// Các hàm trong DLL KHÔNG thread-safe: mọi lời gọi phải được tuần tự hoá.
/// </summary>
internal static class PullSdk
{
    private const string Dll = "plcommpro.dll";

    [DllImport(Dll, EntryPoint = "Connect", CharSet = CharSet.Ansi)]
    public static extern IntPtr Connect(string parameters);

    [DllImport(Dll, EntryPoint = "Disconnect")]
    public static extern void Disconnect(IntPtr handle);

    [DllImport(Dll, EntryPoint = "GetDeviceParam", CharSet = CharSet.Ansi)]
    public static extern int GetDeviceParam(IntPtr handle, byte[] buffer, int bufferSize, string items);

    [DllImport(Dll, EntryPoint = "SetDeviceParam", CharSet = CharSet.Ansi)]
    public static extern int SetDeviceParam(IntPtr handle, string itemValues);

    /// <param name="operationId">1 = điều khiển output, 2 = huỷ báo động, 3 = khởi động lại.</param>
    /// <param name="param1">Số cửa (hoặc số aux output).</param>
    /// <param name="param2">1 = khoá cửa, 2 = aux output.</param>
    /// <param name="param3">0 = đóng, 1..254 = mở N giây, 255 = mở thường trực.</param>
    [DllImport(Dll, EntryPoint = "ControlDevice", CharSet = CharSet.Ansi)]
    public static extern int ControlDevice(IntPtr handle, int operationId, int param1, int param2, int param3, int param4, string options);

    [DllImport(Dll, EntryPoint = "GetRTLog")]
    public static extern int GetRTLog(IntPtr handle, byte[] buffer, int bufferSize);

    [DllImport(Dll, EntryPoint = "PullLastError")]
    public static extern int PullLastError();
}
