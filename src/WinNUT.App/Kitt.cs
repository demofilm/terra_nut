using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;

namespace WinNUT.App;

public static class Kitt
{
    public static readonly Color Red = Color.Parse("#FF2B14");
    public static readonly Color Amber = Color.Parse("#FFB000");
    public static readonly Color Hot = Color.Parse("#FFE9D6");

    public static Color Level(double good) => good < 0.2 ? Hot : good < 0.45 ? Amber : Red;

    /// <summary>Colour ramp red -> amber -> green for good in 0..1.</summary>
    public static Color Gradient(double good)
    {
        good = Math.Clamp(good, 0, 1);
        var low = Color.Parse("#FF2B14"); var mid = Color.Parse("#FFC400"); var high = Color.Parse("#22E06B");
        return good < 0.5 ? Lerp(low, mid, good / 0.5) : Lerp(mid, high, (good - 0.5) / 0.5);
    }

    private static Color Lerp(Color a, Color b, double t) => Color.FromRgb(
        (byte)(a.R + (b.R - a.R) * t), (byte)(a.G + (b.G - a.G) * t), (byte)(a.B + (b.B - a.B) * t));

    public static IBrush Glow(Color c, double a) => new SolidColorBrush(c, a);
    public static Pen P(Color c, double a, double w) => new(new SolidColorBrush(c, a), w, lineCap: PenLineCap.Round);
}

/// <summary>Old-school slanted 7-segment text renderer with dim ghost segments and a glow.</summary>
public static class SevenSeg
{
    // bit order: a b c d e f g
    private static readonly Dictionary<char, int> Mask = new()
    {
        ['0'] = 0b0111111, ['1'] = 0b0000110, ['2'] = 0b1011011, ['3'] = 0b1001111, ['4'] = 0b1100110,
        ['5'] = 0b1101101, ['6'] = 0b1111101, ['7'] = 0b0000111, ['8'] = 0b1111111, ['9'] = 0b1101111,
        ['-'] = 0b1000000, [' '] = 0,
    };
    private const double G = 0.18, Gv = 0.07;
    private static readonly (double u0, double v0, double u1, double v1)[] Segs =
    [
        (G, 0, 1 - G, 0), (1, Gv, 1, .5 - Gv), (1, .5 + Gv, 1, 1 - Gv), (G, 1, 1 - G, 1),
        (0, .5 + Gv, 0, 1 - Gv), (0, Gv, 0, .5 - Gv), (G, .5, 1 - G, .5),
    ];

    public static double Measure(string text, double digitW)
    {
        double w = 0;
        for (int i = 0; i < text.Length; i++) if (text[i] != '.') w += digitW * 1.45 + (i + 1 < text.Length && text[i + 1] == '.' ? digitW * 0.3 : 0);
        return w;
    }

    public static void Draw(DrawingContext ctx, string text, Point topLeft, double digitW, double digitH, Color color, bool ghost = true)
    {
        double thick = digitW * 0.17, slant = 0.12, x = topLeft.X + digitH * slant * 0.5, adv = digitW * 1.45;
        Point M(double bx, double u, double v) => new(bx + u * digitW + slant * (0.5 - v) * digitH, topLeft.Y + v * digitH);
        var chars = text.ToCharArray();
        for (int i = 0; i < chars.Length; i++)
        {
            char ch = chars[i];
            if (ch == '.') continue;
            bool dot = i + 1 < chars.Length && chars[i + 1] == '.';
            int m = Mask.TryGetValue(ch, out var mm) ? mm : 0b1000000;
            for (int s = 0; s < 7; s++)
            {
                var sg = Segs[s]; var p0 = M(x, sg.u0, sg.v0); var p1 = M(x, sg.u1, sg.v1);
                if ((m & (1 << s)) != 0)
                {
                    ctx.DrawLine(Kitt.P(color, 0.12, thick * 3.2), p0, p1);
                    ctx.DrawLine(Kitt.P(color, 0.28, thick * 1.9), p0, p1);
                    ctx.DrawLine(Kitt.P(color, 1.0, thick), p0, p1);
                }
                else if (ghost) ctx.DrawLine(Kitt.P(color, 0.07, thick), p0, p1);
            }
            if (dot)
            {
                var dp = M(x, 1.0 + 0.42, 1.0);
                ctx.DrawEllipse(Kitt.Glow(color, 0.25), null, dp, thick * 1.5, thick * 1.5);
                ctx.DrawEllipse(Kitt.Glow(color, 1), null, dp, thick * 0.6, thick * 0.6);
            }
            x += adv + (dot ? digitW * 0.3 : 0);
        }
    }
}


