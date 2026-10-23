using System.Globalization;

namespace Fleetwright.Map;

/// <summary>The map symbols' knobs (docs/map-ui/symbols.md), set by eye (<c>-symbols=key:value,...</c>): how smooth a
/// curve is, how heavy the strokes, their casing, the labels' fade and halo, and whether anything moves. Sizes are in
/// reference pixels (a 1080-tall window's, as <see cref="WorldView"/> measures), times the UI's own factor.</summary>
public static class SymbolLook
{
    /// <summary>A curve's largest gap from the true one between its points, reference px.</summary>
    public static double Chord = 0.25;

    /// <summary>A gain on every stroke's width.</summary>
    public static float Width = 1;

    /// <summary>The dark casing under a stroke or a symbol (reference px wide, 0 none) and its alpha against the
    /// stroke's.</summary>
    public static float Casing = 1, CasingAlpha = 0.5f;

    /// <summary>The labels' fade in and out, real seconds; the halo under them (0 the old drop shadow).</summary>
    public static float LabelFade = 0.15f, Halo = 1;

    /// <summary>The display clock's eases (the selection's ring, the hover, an encounter's ring): 0 for captures.</summary>
    public static float Motion = 1;

    /// <summary>Target pixels a reference pixel: the target's height over 1080, times the UI's own factor.</summary>
    public static float Px(double targetHeight) => (float)(targetHeight / WorldView.ReferenceHeightPx) * UiFonts.UserScale;

    public static bool Set(string kv)
    {
        var p = kv.Split(':');
        if (p.Length != 2 || !double.TryParse(p[1], CultureInfo.InvariantCulture, out double v))
            return false;
        switch (p[0])
        {
            case "chord": Chord = Math.Max(v, 0.01); break;
            case "width": Width = (float)v; break;
            case "casing": Casing = (float)v; break;
            case "casingalpha": CasingAlpha = (float)v; break;
            case "labelfade": LabelFade = (float)v; break;
            case "halo": Halo = (float)v; break;
            case "motion": Motion = (float)v; break;
            default: return false;
        }
        return true;
    }
}
