using System.Drawing.Drawing2D;

namespace ZKTecoC3Manager.UI;

/// <summary>Thanh tab dạng segmented (nền xám, tab chọn nổi trắng), có badge số đếm.</summary>
public class SegmentedTabs : Control
{
    private const int Inset = 3, SegPadX = 14, BadgeGap = 8;
    private readonly List<string> _items = new();
    private readonly List<string?> _badges = new();
    private readonly List<Rectangle> _rects = new();
    private int _selected, _hover = -1;

    public event EventHandler? SelectedIndexChanged;

    public SegmentedTabs(params string[] items)
    {
        SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer |
                 ControlStyles.ResizeRedraw | ControlStyles.Selectable, true);
        TabStop = true;
        Height = 36;
        Cursor = Cursors.Hand;
        _items.AddRange(items);
        _badges.AddRange(items.Select(_ => (string?)null));
        Recalc();
    }

    public int SelectedIndex
    {
        get => _selected;
        set
        {
            if (value < 0 || value >= _items.Count || value == _selected) return;
            _selected = value;
            Invalidate();
            SelectedIndexChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    public void SetBadge(int index, string? text)
    {
        if (_badges[index] == text) return;
        _badges[index] = text;
        Recalc();
        Invalidate();
    }

    private void Recalc()
    {
        _rects.Clear();
        var x = Inset;
        for (var i = 0; i < _items.Count; i++)
        {
            var w = TextRenderer.MeasureText(_items[i], Theme.UiSemibold, Size.Empty, TextFormatFlags.NoPadding).Width + SegPadX * 2;
            if (_badges[i] is { } b) w += BadgeWidth(b) + BadgeGap;
            _rects.Add(new Rectangle(x, Inset, w, Height - Inset * 2));
            x += w + 2;
        }
        Width = x + Inset - 2;
        AccessibleName = string.Join(", ", _items);
    }

    private static int BadgeWidth(string b) =>
        TextRenderer.MeasureText(b, Theme.SmallBold, Size.Empty, TextFormatFlags.NoPadding).Width + 14;

    protected override void OnMouseMove(MouseEventArgs e)
    {
        var h = _rects.FindIndex(r => r.Contains(e.Location));
        if (h != _hover) { _hover = h; Invalidate(); }
        base.OnMouseMove(e);
    }

    protected override void OnMouseLeave(EventArgs e) { _hover = -1; Invalidate(); base.OnMouseLeave(e); }

    protected override void OnMouseDown(MouseEventArgs e)
    {
        Focus();
        var i = _rects.FindIndex(r => r.Contains(e.Location));
        if (i >= 0) SelectedIndex = i;
        base.OnMouseDown(e);
    }

    protected override bool IsInputKey(Keys keyData) => keyData is Keys.Left or Keys.Right || base.IsInputKey(keyData);

    protected override void OnKeyDown(KeyEventArgs e)
    {
        if (e.KeyCode == Keys.Left) SelectedIndex = Math.Max(0, _selected - 1);
        else if (e.KeyCode == Keys.Right) SelectedIndex = Math.Min(_items.Count - 1, _selected + 1);
        base.OnKeyDown(e);
    }

    protected override void OnGotFocus(EventArgs e) { Invalidate(); base.OnGotFocus(e); }
    protected override void OnLostFocus(EventArgs e) { Invalidate(); base.OnLostFocus(e); }

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        g.Clear(Theme.BackOf(this));
        g.SmoothingMode = SmoothingMode.AntiAlias;

        using (var track = Theme.RoundRect(new RectangleF(0, 0, Width - 0.5f, Height - 0.5f), 8))
        using (var b = new SolidBrush(Theme.Track))
            g.FillPath(b, track);

        for (var i = 0; i < _items.Count; i++)
        {
            var r = _rects[i];
            var selected = i == _selected;
            if (selected)
            {
                using var shadow = Theme.RoundRect(new RectangleF(r.X, r.Y + 1, r.Width, r.Height), 6);
                using var sb = new SolidBrush(Color.FromArgb(28, 15, 23, 42));
                g.FillPath(sb, shadow);
                using var seg = Theme.RoundRect(r, 6);
                g.FillPath(Brushes.White, seg);
                if (Focused && ShowFocusCues)
                {
                    using var pen = new Pen(Color.FromArgb(110, Theme.Primary), 1.5f);
                    g.DrawPath(pen, seg);
                }
            }

            var fore = selected ? Theme.Text : i == _hover ? Theme.TextSoft : Theme.Muted;
            var textW = TextRenderer.MeasureText(g, _items[i], Theme.UiSemibold, Size.Empty, TextFormatFlags.NoPadding).Width;
            var x = r.X + SegPadX;
            TextRenderer.DrawText(g, _items[i], Theme.UiSemibold, new Rectangle(x, r.Y, textW + 2, r.Height), fore,
                TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding | TextFormatFlags.SingleLine);

            if (_badges[i] is not { } badge) continue;
            var bw = BadgeWidth(badge);
            var br = new Rectangle(x + textW + BadgeGap, r.Y + (r.Height - 18) / 2, bw, 18);
            using (var bp = Theme.RoundRect(br, 9))
            using (var bb = new SolidBrush(Color.FromArgb(0xE2, 0xE8, 0xF0)))
                g.FillPath(bb, bp);
            TextRenderer.DrawText(g, badge, Theme.SmallBold, br, Theme.TextSoft,
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding);
        }
    }
}
