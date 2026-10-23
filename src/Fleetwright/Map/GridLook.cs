using System.Globalization;
using System.Numerics;

namespace Fleetwright.Map;

/// <summary>The map grid's knobs (docs/visuals/grid.md), set by eye (<c>-grid=key:value,...</c>): how a level's lines
/// look by how far apart they are on the screen, in reference pixels (a 1080-tall window's, as <see cref="WorldView"/>
/// measures), and the blend band where the square grid takes over from the graticule. Every look is a smooth function
/// of the zoom: nothing switches.</summary>
public static class GridLook
{
    /// <summary>Spacings, reference px: under <see cref="Hidden"/> a level isn't drawn, it fades in to the minor look
    /// by <see cref="MinorFrom"/>, and grows from <see cref="MajorFrom"/> to the major look at <see cref="MajorAt"/>.</summary>
    public static double Hidden = 8, MinorFrom = 30, MajorFrom = 120, MajorAt = 500;

    public static float MinorAlpha = 0.13f, MajorAlpha = 0.26f, MinorWidth = 1f, MajorWidth = 1.4f;

    public static readonly Vector3 LineRgb = new(0.75f, 0.85f, 0.95f), ZeroRgb = new(0.85f, 0.92f, 1.0f);

    /// <summary>The equator's and the prime meridian's extra: alpha times, width plus.</summary>
    public static float ZeroGain = 1.6f, ZeroWidth = 0.5f;

    /// <summary>The blend band, metres per reference pixel: the square grid wholly in at and under
    /// <see cref="SquaresBelow"/>, the graticule wholly at and over <see cref="GraticuleAbove"/>.</summary>
    public static double SquaresBelow = 100, GraticuleAbove = 300;

    /// <summary>The square grid's anchor limits (<see cref="SquareGrid"/>): degrees between the grid's north and the
    /// view's, nautical miles from the view, and the fade to a new anchor, real seconds.</summary>
    public static double ReanchorDeg = 1, ReanchorNm = 100, ReanchorSeconds = 0.4;

    /// <summary>Counts changes to the knobs: the grid's lines are rebuilt when it moves.</summary>
    public static int Version;

    /// <summary>A line fainter than this isn't drawn at all: a level just arriving costs nothing until it shows.</summary>
    public const float Faintest = 1 / 150f;

    /// <summary>How far a level's lines are apart on the screen, reference px, for a spacing in metres.</summary>
    public static double Px(double spacingM, in MapView v) => spacingM / (v.MetresPerPixel * v.Height / WorldView.ReferenceHeightPx);

    /// <summary>How much a level is drawn at all, 0 to 1, by its spacing in reference px.</summary>
    public static double Fade(double px) => Step(Hidden, MinorFrom, px);

    /// <summary>How far a level has grown from the minor look to the major, 0 to 1.</summary>
    public static double Weight(double px) => Step(MajorFrom, MajorAt, px);

    /// <summary>A level's line, by its spacing in reference px, with <paramref name="alpha"/> over it all.</summary>
    public static (Vector4 Color, float Width) Line(double px, float alpha, bool zero = false)
    {
        float w = (float)Weight(px), a = (float)Fade(px) * (MinorAlpha + (MajorAlpha - MinorAlpha) * w) * alpha;
        float width = MinorWidth + (MajorWidth - MinorWidth) * w;
        return zero ? (new Vector4(ZeroRgb, Math.Min(1, a * ZeroGain)), width + ZeroWidth) : (new Vector4(LineRgb, a), width);
    }

    /// <summary>How much of the square grid shows, 0 (the graticule) to 1 (the squares): a smoothstep over the blend
    /// band in the log of the zoom.</summary>
    public static double Squares(in MapView v) =>
        1 - Step(SquaresBelow, GraticuleAbove, v.MetresPerPixel * v.Height / WorldView.ReferenceHeightPx);

    /// <summary>0 at and under <paramref name="a"/>, 1 at and over <paramref name="b"/>, a smoothstep in the log between.</summary>
    static double Step(double a, double b, double x)
    {
        if (x <= a)
            return 0;
        if (x >= b)
            return 1;
        double t = Math.Log(x / a) / Math.Log(b / a);
        return t * t * (3 - 2 * t);
    }

    public static bool Set(string kv)
    {
        var p = kv.Split(':');
        if (p.Length != 2)
            return false;
        if (p[0] == "units")
        {
            if (p[1] is not ("nm" or "km"))
                return false;
            MapUnits.Unit = p[1] == "nm" ? DistanceUnit.NauticalMiles : DistanceUnit.Kilometres;
            Version++;
            return true;
        }
        if (!double.TryParse(p[1], CultureInfo.InvariantCulture, out double v))
            return false;
        switch (p[0])
        {
            case "hidden": Hidden = v; break;
            case "minorfrom": MinorFrom = v; break;
            case "majorfrom": MajorFrom = v; break;
            case "majorat": MajorAt = v; break;
            case "minoralpha": MinorAlpha = (float)v; break;
            case "majoralpha": MajorAlpha = (float)v; break;
            case "minorwidth": MinorWidth = (float)v; break;
            case "majorwidth": MajorWidth = (float)v; break;
            case "squares": SquaresBelow = v; break;
            case "graticule": GraticuleAbove = v; break;
            case "reanchordeg": ReanchorDeg = v; break;
            case "reanchornm": ReanchorNm = v; break;
            case "reanchors": ReanchorSeconds = v; break;
            default: return false;
        }
        Version++;
        return true;
    }
}
