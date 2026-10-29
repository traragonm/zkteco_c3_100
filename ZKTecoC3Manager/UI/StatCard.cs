using System.Drawing.Drawing2D;

namespace ZKTecoC3Manager.UI;

/// <summary>Thẻ thống kê: icon trong ô màu nhạt + nhãn + giá trị (có thể nhiều màu, vd. "4 / 3").</summary>
public class StatCard : Control
{
    private (string Text, Color Color)[] _parts = [("—", Theme.Text)];
    private string _glyph = Glyph.Info;
    private Color _tint = Theme.PrimaryTint, _glyphColor = Theme.Primary;

    public string Caption { get; set; } = "";

    public StatCard()
    {
        SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint |
                 ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
        Height = 76;
        TabStop = false;
    }

    public void SetIcon(string glyph, Color tint, Color glyphColor)
    {
        (_glyph, _tint, _glyphColor) = (glyph, tint, glyphColor);
        Invalidate();
    }

    public void SetValue(string text, Color? color = null) => SetParts((text, color ?? Theme.Text));

    public void SetParts(params (string Text, Color Color)[] parts)
    {
        _parts = parts;
        AccessibleName = Caption + ": " + string.Concat(parts.Select(p => p.Text));
        Invalidate();
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        g.Clear(Theme.BackOf(this));
        g.SmoothingMode = SmoothingMode.AntiAlias;

        using (var path = Theme.RoundRect(new RectangleF(0.5f, 0.5f, Width - 1.5f, Height - 1.5f), 10))
        using (var pen = new Pen(Theme.Border))
        {
            g.FillPath(Brushes.White, path);
            g.DrawPath(pen, path);
        }

        var badge = new Rectangle(14, (Height - 40) / 2, 40, 40);
        using (var path = Theme.RoundRect(badge, 10))
        using (var b = new SolidBrush(_tint))
            g.FillPath(b, path);
        TextRenderer.DrawText(g, _glyph, Theme.Icon(14), badge, _glyphColor,
            TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding);

        const TextFormatFlags flags = TextFormatFlags.NoPadding | TextFormatFlags.SingleLine | TextFormatFlags.EndEllipsis;
        var x = badge.Right + 12;
        var maxW = Width - x - 10;
        TextRenderer.DrawText(g, Caption, Theme.Small, new Rectangle(x, Height / 2 - 20, maxW, 18), Theme.Muted, flags);

        var y = Height / 2 - 1;
        foreach (var (text, color) in _parts)
        {
            var w = TextRenderer.MeasureText(g, text, Theme.Stat, Size.Empty, TextFormatFlags.NoPadding).Width;
            TextRenderer.DrawText(g, text, Theme.Stat, new Rectangle(x, y, Math.Max(0, Width - x - 10), 26), color, flags);
            x += w;
            if (x >= Width - 10) break;
        }
    }
}
