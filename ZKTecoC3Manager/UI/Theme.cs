using System.Drawing.Drawing2D;

namespace ZKTecoC3Manager.UI;

/// <summary>Bảng màu, font và icon dùng chung (khớp mockup "Main window v2").</summary>
public static class Theme
{
    public static readonly Color Navy = Hex(0x0F1B2D);
    public static readonly Color NavyHover = Hex(0x1E2A3D);
    public static readonly Color NavyPressed = Hex(0x26344A);
    public static readonly Color NavyText = Hex(0xCBD5E1);
    public static readonly Color NavyMuted = Hex(0x94A3B8);

    public static readonly Color Bg = Hex(0xF4F6FA);
    public static readonly Color Card = Color.White;
    public static readonly Color Border = Hex(0xE3E8EF);
    public static readonly Color Divider = Hex(0xF1F5F9);
    public static readonly Color InputBg = Hex(0xF8FAFC);
    public static readonly Color InputBorder = Hex(0xCBD5E1);
    public static readonly Color Track = Hex(0xF1F5F9);

    public static readonly Color Text = Hex(0x0F172A);
    public static readonly Color TextSoft = Hex(0x334155);
    public static readonly Color Muted = Hex(0x64748B);
    public static readonly Color Faint = Hex(0x94A3B8);

    public static readonly Color Primary = Hex(0x2563EB);
    public static readonly Color PrimaryHover = Hex(0x1D4ED8);
    public static readonly Color PrimaryPressed = Hex(0x1E40AF);
    public static readonly Color PrimaryTint = Hex(0xDBEAFE);
    public static readonly Color PrimaryText = Hex(0x1D4ED8);
    public static readonly Color Selection = Hex(0xEFF6FF);

    public static readonly Color Success = Hex(0x16A34A);
    public static readonly Color SuccessHover = Hex(0x15803D);
    public static readonly Color SuccessPressed = Hex(0x166534);
    public static readonly Color SuccessTint = Hex(0xDCFCE7);
    public static readonly Color SuccessText = Hex(0x15803D);

    public static readonly Color Danger = Hex(0xDC2626);
    public static readonly Color DangerBorder = Hex(0xFCA5A5);
    public static readonly Color DangerTint = Hex(0xFEE2E2);
    public static readonly Color DangerText = Hex(0xB91C1C);

    public static readonly Color Warning = Hex(0xD97706);
    public static readonly Color WarningTint = Hex(0xFEF3C7);
    public static readonly Color WarningText = Hex(0xB45309);

    public static readonly Color ConsoleBg = Hex(0x0B1220);
    public static readonly Color ConsoleText = Hex(0xCBD5E1);
    public static readonly Color ConsoleTime = Hex(0x64748B);
    public static readonly Color LogInfo = Hex(0x38BDF8);
    public static readonly Color LogWarn = Hex(0xFBBF24);
    public static readonly Color LogError = Hex(0xF87171);
    public static readonly Color LogDebug = Hex(0x64748B);

    // Pill trạng thái trên header navy (màu đã trộn sẵn với nền navy).
    public static readonly Color PillOnlineBg = Hex(0x123635);
    public static readonly Color PillOnlineFg = Hex(0x4ADE80);
    public static readonly Color PillOfflineBg = Hex(0x342938);
    public static readonly Color PillOfflineFg = Hex(0xF87171);
    public static readonly Color PillBusyBg = Hex(0x35352C);
    public static readonly Color PillBusyFg = Hex(0xFBBF24);

    public static readonly Font Ui = new("Segoe UI", 9.75F);
    public static readonly Font UiSemibold = new("Segoe UI Semibold", 9.75F);
    public static readonly Font Small = new("Segoe UI", 9F);
    public static readonly Font SmallBold = new("Segoe UI Semibold", 8.5F);
    public static readonly Font Caption = new("Segoe UI", 8.75F);
    public static readonly Font CardTitle = new("Segoe UI Semibold", 10F);
    public static readonly Font AppTitle = new("Segoe UI Semibold", 11.5F);
    public static readonly Font Stat = new("Segoe UI", 14F, FontStyle.Bold);
    public static readonly Font Mono = new("Consolas", 9.75F);
    public static readonly Font MonoBold = new("Consolas", 9.75F, FontStyle.Bold);
    public static readonly Font GridHeader = new("Segoe UI Semibold", 8.25F);

    private static readonly Dictionary<float, Font> IconFonts = new();

    /// <summary>Font icon Segoe MDL2 Assets (có sẵn trên Windows 10/11).</summary>
    public static Font Icon(float size)
    {
        if (!IconFonts.TryGetValue(size, out var f))
            IconFonts[size] = f = new Font("Segoe MDL2 Assets", size);
        return f;
    }

    public static Color Blend(Color a, Color b, float t) => Color.FromArgb(
        (int)(a.R + (b.R - a.R) * t), (int)(a.G + (b.G - a.G) * t), (int)(a.B + (b.B - a.B) * t));

    public static GraphicsPath RoundRect(RectangleF r, float radius)
    {
        var p = new GraphicsPath();
        var d = Math.Min(radius * 2, Math.Min(r.Width, r.Height));
        if (d <= 1)
        {
            p.AddRectangle(r);
            return p;
        }
        p.AddArc(r.X, r.Y, d, d, 180, 90);
        p.AddArc(r.Right - d, r.Y, d, d, 270, 90);
        p.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90);
        p.AddArc(r.X, r.Bottom - d, d, d, 90, 90);
        p.CloseFigure();
        return p;
    }

    /// <summary>Nền phía sau control tự vẽ (màu của control cha) để góc bo hoà vào nền.</summary>
    public static Color BackOf(Control c)
    {
        for (var p = c.Parent; p is not null; p = p.Parent)
            if (p.BackColor.A == 255) return p.BackColor;
        return Bg;
    }

    private static Color Hex(int rgb) => Color.FromArgb((rgb >> 16) & 0xFF, (rgb >> 8) & 0xFF, rgb & 0xFF);
}

/// <summary>Mã ký tự icon trong Segoe MDL2 Assets.</summary>
public static class Glyph
{
    public const string Connect = "";
    public const string Wifi = "";
    public const string Devices = "";
    public const string Lock = "";
    public const string Unlock = "";
    public const string Shield = "";
    public const string QrCode = "";
    public const string Settings = "";
    public const string Info = "";
    public const string Folder = "";
    public const string Sync = "";
    public const string Download = "";
    public const string Delete = "";
    public const string Accept = "";
    public const string Cancel = "";
    public const string Help = "";
    public const string Code = "";
    public const string Copy = "";
    public const string Save = "";
}
