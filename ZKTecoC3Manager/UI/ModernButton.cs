using System.Drawing.Drawing2D;

namespace ZKTecoC3Manager.UI;

public enum ButtonVariant { Primary, Secondary, Success, DangerOutline, Ghost }

/// <summary>Nút phẳng bo góc, có icon tuỳ chọn, các biến thể màu theo mockup.</summary>
public class ModernButton : Button
{
    private ButtonVariant _variant = ButtonVariant.Secondary;
    private string? _glyph;
    private bool _hover, _down;

    public ModernButton()
    {
        SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer |
                 ControlStyles.ResizeRedraw | ControlStyles.SupportsTransparentBackColor, true);
        FlatStyle = FlatStyle.Flat;
        FlatAppearance.BorderSize = 0;
        UseVisualStyleBackColor = false;
        Cursor = Cursors.Hand;
        Font = Theme.UiSemibold;
        Height = 36;
    }

    public ButtonVariant Variant
    {
        get => _variant;
        set { _variant = value; Invalidate(); }
    }

    public string? Glyph
    {
        get => _glyph;
        set { _glyph = value; Invalidate(); }
    }

    public int Radius { get; set; } = 8;

    /// <summary>Chiều cao luôn là Height đã đặt; chiều rộng vừa icon + chữ.</summary>
    public override Size GetPreferredSize(Size proposedSize)
    {
        const TextFormatFlags flags = TextFormatFlags.NoPadding | TextFormatFlags.SingleLine;
        var w = string.IsNullOrEmpty(Text) ? 0 : TextRenderer.MeasureText(Text, Font, Size.Empty, flags).Width;
        if (_glyph is not null) w += TextRenderer.MeasureText(_glyph, Theme.Icon(Font.Size), Size.Empty, flags).Width + (w > 0 ? 8 : 0);
        return new Size(w + 28, Height);
    }

    protected override void OnMouseEnter(EventArgs e) { _hover = true; Invalidate(); base.OnMouseEnter(e); }
    protected override void OnMouseLeave(EventArgs e) { _hover = _down = false; Invalidate(); base.OnMouseLeave(e); }
    protected override void OnMouseDown(MouseEventArgs mevent) { _down = true; Invalidate(); base.OnMouseDown(mevent); }
    protected override void OnMouseUp(MouseEventArgs mevent) { _down = false; Invalidate(); base.OnMouseUp(mevent); }
    protected override void OnEnabledChanged(EventArgs e) { _hover = _down = false; Invalidate(); base.OnEnabledChanged(e); }

    protected override void OnPaint(PaintEventArgs pevent)
    {
        var g = pevent.Graphics;
        var back = Theme.BackOf(this);
        g.Clear(back);
        g.SmoothingMode = SmoothingMode.AntiAlias;

        var (fill, border, fore) = Colors(back);
        if (!Enabled)
        {
            fill = Theme.Blend(fill, back, 0.45f);
            border = Theme.Blend(border, back, 0.45f);
            fore = Theme.Blend(fore, fill, 0.5f);
        }

        var rect = new RectangleF(0.5f, 0.5f, Width - 1.5f, Height - 1.5f);
        using (var path = Theme.RoundRect(rect, Radius))
        {
            using (var b = new SolidBrush(fill)) g.FillPath(b, path);
            if (border != fill)
                using (var p = new Pen(border)) g.DrawPath(p, path);
        }

        if (Focused && ShowFocusCues)
        {
            using var ring = Theme.RoundRect(new RectangleF(2.5f, 2.5f, Width - 5.5f, Height - 5.5f), Math.Max(2, Radius - 2));
            using var pen = new Pen(Color.FromArgb(110, Theme.Primary), 1.5f);
            g.DrawPath(pen, ring);
        }

        DrawContent(g, fore);
    }

    private void DrawContent(Graphics g, Color fore)
    {
        const TextFormatFlags flags = TextFormatFlags.NoPadding | TextFormatFlags.SingleLine;
        var iconFont = Theme.Icon(Font.Size);
        var hasText = !string.IsNullOrEmpty(Text);
        var glyphSize = _glyph is null ? Size.Empty : TextRenderer.MeasureText(g, _glyph, iconFont, Size.Empty, flags);
        var textSize = hasText ? TextRenderer.MeasureText(g, Text, Font, Size.Empty, flags) : Size.Empty;
        var gap = _glyph is not null && hasText ? 8 : 0;
        var x = (Width - (glyphSize.Width + gap + textSize.Width)) / 2;

        if (_glyph is not null)
        {
            TextRenderer.DrawText(g, _glyph, iconFont, new Point(x, (Height - glyphSize.Height) / 2 + 1), fore, flags);
            x += glyphSize.Width + gap;
        }
        if (hasText)
            TextRenderer.DrawText(g, Text, Font, new Point(x, (Height - textSize.Height) / 2), fore, flags);
    }

    private (Color fill, Color border, Color fore) Colors(Color back)
    {
        switch (_variant)
        {
            case ButtonVariant.Primary:
                var p = _down ? Theme.PrimaryPressed : _hover ? Theme.PrimaryHover : Theme.Primary;
                return (p, p, Color.White);
            case ButtonVariant.Success:
                var s = _down ? Theme.SuccessPressed : _hover ? Theme.SuccessHover : Theme.Success;
                return (s, s, Color.White);
            case ButtonVariant.DangerOutline:
                return (_down ? Theme.DangerBorder : _hover ? Theme.DangerTint : Color.White, Theme.DangerBorder, Theme.Danger);
            case ButtonVariant.Ghost:
                var gh = _down ? Theme.NavyPressed : _hover ? Theme.NavyHover : back;
                return (gh, gh, _hover ? Color.White : Theme.NavyText);
            default:
                return (_down ? Hex(0xE2E8F0) : _hover ? Theme.Divider : Color.White, Theme.InputBorder, Theme.Text);
        }
    }

    private static Color Hex(int rgb) => Color.FromArgb((rgb >> 16) & 0xFF, (rgb >> 8) & 0xFF, rgb & 0xFF);
}
