using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Threading;

namespace WinNUT.App;

/// <summary>Knight-Rider style sweeping red scanner bar.</summary>
public sealed class KittScanner : Control
{
    public static readonly StyledProperty<bool> ActiveProperty = AvaloniaProperty.Register<KittScanner, bool>(nameof(Active));
    public static readonly StyledProperty<double> SpeedProperty = AvaloniaProperty.Register<KittScanner, double>(nameof(Speed), 1.0);
    public static readonly StyledProperty<bool> WarnProperty = AvaloniaProperty.Register<KittScanner, bool>(nameof(Warn));

    public bool Active { get => GetValue(ActiveProperty); set => SetValue(ActiveProperty, value); }
    public double Speed { get => GetValue(SpeedProperty); set => SetValue(SpeedProperty, value); }
    public bool Warn { get => GetValue(WarnProperty); set => SetValue(WarnProperty, value); }

    private const int Cells = 32;
    private double _pos; private int _dir = 1;
    private readonly DispatcherTimer _t = new() { Interval = TimeSpan.FromMilliseconds(30) };

    public KittScanner() { _t.Tick += (_, _) => Step(); Height = 18; }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e) { base.OnAttachedToVisualTree(e); _t.Start(); }
    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e) { _t.Stop(); base.OnDetachedFromVisualTree(e); }

    private void Step()
    {
        if (!Active) { InvalidateVisual(); return; }
        _pos += _dir * 0.45 * Math.Max(0.2, Speed);
        if (_pos >= Cells - 1) { _pos = Cells - 1; _dir = -1; }
        else if (_pos <= 0) { _pos = 0; _dir = 1; }
        InvalidateVisual();
    }

    public override void Render(DrawingContext ctx)
    {
        var col = Warn ? Kitt.Amber : Kitt.Red;
        double cw = Bounds.Width / Cells, h = Bounds.Height;
        for (int i = 0; i < Cells; i++)
        {
            double d = (i - _pos) * _dir;            // positive = behind the head
            double br = !Active ? 0.10 : d < 0 ? Math.Max(0.10, 1 - Math.Abs(d) * 1.2) : Math.Max(0.10, 1 - d / 7.0);
            br = Math.Max(0.10, br);
            var rect = new Rect(i * cw + 1.5, 3, Math.Max(2, cw - 3), h - 6);
            if (br > 0.2) ctx.DrawRectangle(Kitt.Glow(col, br * 0.25), null, rect.Inflate(2.5), 3, 3);
            ctx.DrawRectangle(Kitt.Glow(col, br), null, rect, 2, 2);
        }
    }
}
