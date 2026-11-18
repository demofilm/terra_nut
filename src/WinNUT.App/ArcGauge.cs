using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;

namespace WinNUT.App;

/// <summary>240° gauge made of glowing LED ticks with a slanted 7-segment readout.</summary>
public sealed class ArcGauge : Control
{
    public static readonly StyledProperty<double> ValueProperty = AvaloniaProperty.Register<ArcGauge, double>(nameof(Value));
    public static readonly StyledProperty<double> MinimumProperty = AvaloniaProperty.Register<ArcGauge, double>(nameof(Minimum));
    public static readonly StyledProperty<double> MaximumProperty = AvaloniaProperty.Register<ArcGauge, double>(nameof(Maximum), 100);
    public static readonly StyledProperty<string> UnitProperty = AvaloniaProperty.Register<ArcGauge, string>(nameof(Unit), "");
    public static readonly StyledProperty<string> TitleProperty = AvaloniaProperty.Register<ArcGauge, string>(nameof(Title), "");
    public static readonly StyledProperty<bool> HasValueProperty = AvaloniaProperty.Register<ArcGauge, bool>(nameof(HasValue));
    public static readonly StyledProperty<bool> InvertColorsProperty = AvaloniaProperty.Register<ArcGauge, bool>(nameof(InvertColors));
    public static readonly StyledProperty<int> DecimalsProperty = AvaloniaProperty.Register<ArcGauge, int>(nameof(Decimals), 0);
    public static readonly StyledProperty<double> NominalProperty = AvaloniaProperty.Register<ArcGauge, double>(nameof(Nominal), double.NaN);

    static ArcGauge() => AffectsRender<ArcGauge>(ValueProperty, MinimumProperty, MaximumProperty, UnitProperty, TitleProperty,
        HasValueProperty, InvertColorsProperty, DecimalsProperty, NominalProperty);

    public double Value { get => GetValue(ValueProperty); set => SetValue(ValueProperty, value); }
    public double Minimum { get => GetValue(MinimumProperty); set => SetValue(MinimumProperty, value); }
    public double Maximum { get => GetValue(MaximumProperty); set => SetValue(MaximumProperty, value); }
    public string Unit { get => GetValue(UnitProperty); set => SetValue(UnitProperty, value); }
    public string Title { get => GetValue(TitleProperty); set => SetValue(TitleProperty, value); }
    public bool HasValue { get => GetValue(HasValueProperty); set => SetValue(HasValueProperty, value); }
    /// <summary>True when a high value is bad (load), false when high is good (battery).</summary>
    public bool InvertColors { get => GetValue(InvertColorsProperty); set => SetValue(InvertColorsProperty, value); }
    public int Decimals { get => GetValue(DecimalsProperty); set => SetValue(DecimalsProperty, value); }
    /// <summary>When set (not NaN) green is centred on this value and red is at both ends (e.g. mains voltage).</summary>
    public double Nominal { get => GetValue(NominalProperty); set => SetValue(NominalProperty, value); }

    /// <summary>0 (bad) .. 1 (good) for a position t in 0..1 along the gauge.</summary>
    private double GoodAt(double t)
    {
        if (double.IsNaN(Nominal) || Maximum <= Minimum) return InvertColors ? 1 - t : t;
        var tc = Math.Clamp((Nominal - Minimum) / (Maximum - Minimum), 0.05, 0.95);
        var d = Math.Abs(t - tc) / Math.Max(tc, 1 - tc);
        return Math.Clamp(1.8 * (1 - d) - 0.2, 0, 1);
    }

    private const int Ticks = 36;

    public override void Render(DrawingContext ctx)
    {
        var size = Math.Min(Bounds.Width, Bounds.Height);
        var c = new Point(Bounds.Width / 2, Bounds.Height / 2);
        var r = size / 2 - 22;
        if (r < 20) return;
        double f = 0, good = 0;
        if (HasValue)
        {
            var span = Maximum - Minimum;
            f = span <= 0 ? 0 : Math.Clamp((Value - Minimum) / span, 0, 1);
            good = GoodAt(f);
        }
        var col = HasValue ? Kitt.Level(good) : Kitt.Red;
        int lit = HasValue ? (int)Math.Round(f * Ticks) : 0;

        for (int i = 0; i < Ticks; i++)
        {
            double ang = (150 + 240.0 * i / (Ticks - 1)) * Math.PI / 180;
            Point p(double rr) => new(c.X + rr * Math.Cos(ang), c.Y + rr * Math.Sin(ang));
            var a = p(r - 9); var b = p(r + 9);
            var tc = Kitt.Gradient(GoodAt((double)i / (Ticks - 1)));
            if (i < lit)
            {
                ctx.DrawLine(Kitt.P(tc, 0.14, 13), a, b);
                ctx.DrawLine(Kitt.P(tc, 0.30, 8), a, b);
                ctx.DrawLine(Kitt.P(tc, 1, 4), a, b);
            }
            else ctx.DrawLine(Kitt.P(tc, 0.13, 4), a, b);
        }

        var txt = HasValue ? Value.ToString("F" + Decimals, System.Globalization.CultureInfo.InvariantCulture) : "---";
        double dw = Math.Min(r * 0.30, r * 1.35 / Math.Max(3, SevenSeg.Measure(txt, 1) / 1.0));
        dw = Math.Max(8, dw); double dh = dw * 1.9;
        double tw = SevenSeg.Measure(txt, dw);
        SevenSeg.Draw(ctx, txt, new Point(c.X - tw / 2 + dw * 0.1, c.Y - dh / 2 - r * 0.08), dw, dh, col);

        var fg = new SolidColorBrush(Kitt.Red, 0.85);
        var unit = new FormattedText(Unit, System.Globalization.CultureInfo.CurrentUICulture, FlowDirection.LeftToRight,
            new Typeface(FontFamily.Default, FontStyle.Normal, FontWeight.SemiBold), Math.Max(10, r * 0.16), fg);
        ctx.DrawText(unit, new Point(c.X - unit.Width / 2, c.Y + dh / 2 - r * 0.02));
        var ttl = new FormattedText(Title.ToUpperInvariant(), System.Globalization.CultureInfo.CurrentUICulture, FlowDirection.LeftToRight,
            new Typeface(FontFamily.Default, FontStyle.Normal, FontWeight.Bold), 12, new SolidColorBrush(Kitt.Red, 0.75));
        ctx.DrawText(ttl, new Point(c.X - ttl.Width / 2, Bounds.Height - ttl.Height - 2));
    }
}


