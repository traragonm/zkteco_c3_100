using System.Drawing.Drawing2D;

namespace ZKTecoC3Manager.UI;

/// <summary>
/// Card trắng bo góc, có tiêu đề + icon tuỳ chọn, chứa đúng một control nội dung.
/// AutoHeight = true: chiều cao tự co theo nội dung (dùng cho cột trái).
/// </summary>
public class CardPanel : Panel
{
    private const int TitleBlock = 30;
    private Control? _content;
    private bool _inLayout;

    public string? Title { get; set; }
    public string? TitleGlyph { get; set; }
    public int Radius { get; set; } = 10;
    public Color FillColor { get; set; } = Theme.Card;
    public Color BorderColor { get; set; } = Theme.Border;
    public bool AutoHeight { get; set; } = true;

    /// <summary>Cắt nội dung theo góc bo (cho nội dung sát viền như lưới dữ liệu).</summary>
    public bool ClipContent { get; set; }

    public CardPanel()
    {
        SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint |
                 ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
        Padding = new Padding(16, 14, 16, 16);
        BackColor = Theme.Bg;
    }

    private int ContentTop => Padding.Top + (Title is null ? 0 : TitleBlock);

    public void SetContent(Control content)
    {
        if (_content is not null) Controls.Remove(_content);
        _content = content;
        content.BackColor = FillColor;
        Controls.Add(content);
        PerformLayout();
    }

    protected override void OnLayout(LayoutEventArgs levent)
    {
        base.OnLayout(levent);
        if (_content is null || _inLayout) return;
        _inLayout = true;
        try
        {
            var w = Math.Max(0, ClientSize.Width - Padding.Horizontal);
            int h;
            if (AutoHeight)
            {
                h = _content.GetPreferredSize(new Size(w, 0)).Height;
                var target = ContentTop + h + Padding.Bottom;
                if (Height != target) Height = target;
            }
            else h = Math.Max(0, ClientSize.Height - ContentTop - Padding.Bottom);

            _content.SetBounds(Padding.Left, ContentTop, w, h);
            if (ClipContent && w > 0 && h > 0)
            {
                using var path = Theme.RoundRect(new RectangleF(0, 0, w, h), Math.Max(0, Radius - 1));
                _content.Region = new Region(path);
            }
        }
        finally { _inLayout = false; }
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        g.Clear(Theme.BackOf(this));
        g.SmoothingMode = SmoothingMode.AntiAlias;

        using (var path = Theme.RoundRect(new RectangleF(0.5f, 0.5f, Width - 1.5f, Height - 1.5f), Radius))
        using (var fill = new SolidBrush(FillColor))
        using (var pen = new Pen(BorderColor))
        {
            g.FillPath(fill, path);
            g.DrawPath(pen, path);
        }

        if (Title is null) return;
        var x = Padding.Left;
        if (TitleGlyph is not null)
        {
            TextRenderer.DrawText(g, TitleGlyph, Theme.Icon(11), new Point(x, Padding.Top + 3), Theme.Primary,
                TextFormatFlags.NoPadding);
            x += 24;
        }
        TextRenderer.DrawText(g, Title, Theme.CardTitle, new Point(x, Padding.Top), Theme.Text, TextFormatFlags.NoPadding);
    }
}
