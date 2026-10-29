using System.Drawing.Drawing2D;

namespace ZKTecoC3Manager.UI;

/// <summary>CheckBox dạng công tắc gạt.</summary>
public class ToggleSwitch : CheckBox
{
    private const int TrackW = 34, TrackH = 18, Gap = 10;

    public ToggleSwitch()
    {
        SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint |
                 ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
        AutoSize = false;
        Height = 26;
        Cursor = Cursors.Hand;
        Font = Theme.Ui;
        ForeColor = Theme.TextSoft;
    }

    protected override void OnTextChanged(EventArgs e) { base.OnTextChanged(e); FitWidth(); }
    protected override void OnFontChanged(EventArgs e) { base.OnFontChanged(e); FitWidth(); }
    protected override void OnCheckedChanged(EventArgs e) { base.OnCheckedChanged(e); Invalidate(); }

    public override Size GetPreferredSize(Size proposedSize) => new(Width, Height);

    private void FitWidth() =>
        Width = TrackW + Gap + TextRenderer.MeasureText(Text, Font, Size.Empty, TextFormatFlags.NoPadding).Width + 4;

    protected override void OnPaint(PaintEventArgs pevent)
    {
        var g = pevent.Graphics;
        var back = Theme.BackOf(this);
        g.Clear(back);
        g.SmoothingMode = SmoothingMode.AntiAlias;

        var on = Checked ? Theme.Primary : Theme.InputBorder;
        if (!Enabled) on = Theme.Blend(on, back, 0.5f);
        var top = (Height - TrackH) / 2f;
        using (var track = Theme.RoundRect(new RectangleF(0.5f, top, TrackW, TrackH), TrackH / 2f))
        using (var b = new SolidBrush(on))
            g.FillPath(b, track);

        var knobX = Checked ? TrackW - TrackH + 2.5f : 2.5f;
        g.FillEllipse(Brushes.White, knobX, top + 2, TrackH - 4, TrackH - 4);

        if (Focused && ShowFocusCues)
        {
            using var ring = Theme.RoundRect(new RectangleF(-1, top - 2, TrackW + 3, TrackH + 4), TrackH / 2f + 2);
            using var pen = new Pen(Color.FromArgb(110, Theme.Primary), 1.5f);
            g.DrawPath(pen, ring);
        }

        var fore = Enabled ? ForeColor : Theme.Faint;
        TextRenderer.DrawText(g, Text, Font, new Rectangle(TrackW + Gap, 0, Width - TrackW - Gap, Height), fore,
            TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding | TextFormatFlags.SingleLine);
    }
}

/// <summary>Nhãn dạng viên thuốc (badge), có chấm tròn tuỳ chọn.</summary>
public class PillLabel : Label
{
    private Color _pillColor = Theme.PrimaryTint;
    private bool _showDot;

    public PillLabel()
    {
        SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint |
                 ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
        AutoSize = true;
        Font = Theme.SmallBold;
        Padding = new Padding(10, 4, 10, 4);
        ForeColor = Theme.PrimaryText;
    }

    public Color PillColor
    {
        get => _pillColor;
        set { _pillColor = value; Invalidate(); }
    }

    public bool ShowDot
    {
        get => _showDot;
        set { _showDot = value; PerformLayout(); Invalidate(); }
    }

    public override Size GetPreferredSize(Size proposedSize)
    {
        var s = TextRenderer.MeasureText(Text, Font, Size.Empty, TextFormatFlags.NoPadding);
        return new Size(s.Width + Padding.Horizontal + (_showDot ? 14 : 0), s.Height + Padding.Vertical);
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        g.Clear(Theme.BackOf(this));
        g.SmoothingMode = SmoothingMode.AntiAlias;
        using (var path = Theme.RoundRect(new RectangleF(0, 0, Width - 0.5f, Height - 0.5f), Height / 2f))
        using (var b = new SolidBrush(_pillColor))
            g.FillPath(b, path);

        var x = Padding.Left;
        if (_showDot)
        {
            using var dot = new SolidBrush(ForeColor);
            g.FillEllipse(dot, x, (Height - 8) / 2f, 8, 8);
            x += 14;
        }
        TextRenderer.DrawText(g, Text, Font, new Rectangle(x, 0, Width - x, Height), ForeColor,
            TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding | TextFormatFlags.SingleLine);
    }
}

/// <summary>Ô vuông bo góc có icon ở giữa (logo trên header).</summary>
public class IconBadge : Control
{
    public string BadgeGlyph { get; set; } = Glyph.QrCode;
    public Color Tint { get; set; } = Theme.Primary;
    public Color GlyphColor { get; set; } = Color.White;
    public float GlyphSize { get; set; } = 13;

    public IconBadge()
    {
        SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint |
                 ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
        Size = new Size(36, 36);
        TabStop = false;
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        g.Clear(Theme.BackOf(this));
        g.SmoothingMode = SmoothingMode.AntiAlias;
        using (var path = Theme.RoundRect(new RectangleF(0, 0, Width - 0.5f, Height - 0.5f), 8))
        using (var b = new SolidBrush(Tint))
            g.FillPath(b, path);
        TextRenderer.DrawText(g, BadgeGlyph, Theme.Icon(GlyphSize), ClientRectangle, GlyphColor,
            TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding);
    }
}
