using System.Drawing.Drawing2D;

namespace ZKTecoC3Manager.UI;

/// <summary>Khung bo góc bao quanh TextBox / NumericUpDown / ComboBox, viền xanh khi focus.</summary>
public class FieldBox : Panel
{
    public Control Inner { get; }

    public FieldBox(Control inner, int height = 32)
    {
        SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint |
                 ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
        Inner = inner;
        Height = height;
        Margin = new Padding(0, 0, 0, 10);

        inner.Font = inner.Font.FontFamily.Name == "Consolas" ? inner.Font : Theme.Ui;
        inner.BackColor = Theme.InputBg;
        inner.ForeColor = Theme.Text;

        switch (inner)
        {
            case TextBox { Multiline: true } tb:
                tb.BorderStyle = BorderStyle.None;
                Padding = new Padding(10, 7, 4, 6);
                break;
            case TextBox tb:
                tb.BorderStyle = BorderStyle.None;
                Padding = new Padding(10, Math.Max(0, (height - tb.PreferredHeight) / 2), 8, 0);
                break;
            case NumericUpDown n:
                n.BorderStyle = BorderStyle.None;
                Padding = new Padding(8, Math.Max(0, (height - n.PreferredHeight) / 2), 2, 0);
                break;
            case ComboBox c:
                c.FlatStyle = FlatStyle.Flat;
                Padding = new Padding(3, Math.Max(0, (height - c.Height) / 2), 3, 0);
                break;
        }

        inner.Dock = DockStyle.Fill;
        inner.GotFocus += (_, _) => Invalidate();
        inner.LostFocus += (_, _) => Invalidate();
        inner.EnabledChanged += (_, _) => Invalidate();
        Controls.Add(inner);
    }

    public override Size GetPreferredSize(Size proposedSize) =>
        new(proposedSize.Width > 0 ? proposedSize.Width : Width, Height);

    protected override void OnMouseDown(MouseEventArgs e)
    {
        Inner.Focus();
        base.OnMouseDown(e);
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        g.Clear(Theme.BackOf(this));
        g.SmoothingMode = SmoothingMode.AntiAlias;

        var focused = Inner.ContainsFocus;
        var fill = Inner.Enabled ? Theme.InputBg : Theme.Divider;
        using var path = Theme.RoundRect(new RectangleF(0.75f, 0.75f, Width - 2f, Height - 2f), 6);
        using var brush = new SolidBrush(fill);
        using var pen = new Pen(focused ? Theme.Primary : Theme.InputBorder, focused ? 1.5f : 1f);
        g.FillPath(brush, path);
        g.DrawPath(pen, path);
        if (Inner.BackColor != fill) Inner.BackColor = fill;
    }
}
