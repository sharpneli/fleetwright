using System.Globalization;

namespace Fleetwright.Map.Hits;

/// <summary>The world view's shell hits' knobs (docs/visuals/hits-and-fires.md), one place, for side-by-side captures:
/// <c>-hit=at:0.4,scale:1.2</c>. The estimates are flagged [EST]: placeholders until the research requests come back
/// (decision 9).</summary>
public static class HitLook
{
    /// <summary>A scale on a burst's fireball (1: MagazineBlast.Fireball at the filler's W, D = 5.8 W^⅓ m).</summary>
    public static double Scale = 1;

    /// <summary>A burst in an open space (its gas vented as fast as it formed: a superstructure, a deckhouse) shows this
    /// share of its fireball through the openings it blew [EST].</summary>
    public static double OpenShare = 0.7;

    /// <summary>A hit on a plate (stopped, broke up, holed, glanced): a hard flash this wide for a 12" shell, m, by the
    /// calibre, for this long, s (shell-impacts.md "Shatter": about 0.1 s) [EST]. A shell through thin plating flashes
    /// <see cref="ThroughShare"/> of it at each hole.</summary>
    public static double PlateFlashM = 3, PlateFlashS = 0.1, ThroughShare = 0.5;

    /// <summary>A burst inside: its flash lights the space for this share of its fireball's life, and its gas jets out of
    /// the hole it came in by for that share, as long as 45 d √2 (the magazine's ports' law at ~2 bar) and no longer than
    /// this share of its fireball [EST].</summary>
    public static double InsideLightShare = 0.4, VentShare = 0.6, VentReach = 0.6;

    /// <summary>A burst's smoke: m² of extinction area per gram of its filler's TNT-equivalent (TNT and picric soot heavily:
    /// a soot yield of ~0.1 at σ ≈ 8.7 m²/g; black powder's solids give its white-grey) [EST, gap 2]; a vent's share of it
    /// out of the hole; a hole's dust and paint, m² for a 12" (by the calibre squared) [EST].</summary>
    public static double SmokeM2g = 1.0, VentSmokeShare = 0.5, DustM2 = 300;

    /// <summary>What flies off (I2): the share of a burst's pieces of 50 g and more that mark the sea (the rest fall on
    /// her, short, or too small to see) and the most marks a burst leaves, and the foam's size as a shell's of this
    /// calibre, mm; a plate's fallen pieces as shells of this share of its calibre [EST].</summary>
    public static double PieceShare = 0.03, PieceMarks = 40, PieceMm = 30, FallenShare = 0.3;

    /// <summary>The share of a burst's light as drawn that it throws on the sea and the ships (as the magazine's).</summary>
    public static double LightShare = 0.3;

    /// <summary>Which parts draw, for measuring each's cost: 1 the fireballs, 2 the plates' flashes, 4 the vents' jets, 8
    /// the lights, 16 the smoke, 32 what flies off (splashes, pieces, sparks).</summary>
    public static int Parts = 63;

    /// <summary>A dev aid for captures: the hits of the newest 2 s drawn at this age, s after they struck (-1: off).</summary>
    public static double AtS = -1;

    /// <summary>Off: no world view hits (the table's pop stays).</summary>
    public static bool On = true;

    public static bool Set(string kv)
    {
        var p = kv.Split(':');
        if (p.Length != 2 || !double.TryParse(p[1], CultureInfo.InvariantCulture, out double v))
            return false;
        switch (p[0])
        {
            case "scale": Scale = Math.Max(v, 0); break;
            case "open": OpenShare = Math.Clamp(v, 0, 1); break;
            case "flash": PlateFlashM = Math.Max(v, 0); break;
            case "flashs": PlateFlashS = Math.Max(v, 0.01); break;
            case "through": ThroughShare = Math.Max(v, 0); break;
            case "inside": InsideLightShare = Math.Max(v, 0); break;
            case "vent": VentShare = Math.Max(v, 0); break;
            case "reach": VentReach = Math.Max(v, 0); break;
            case "smoke": SmokeM2g = Math.Max(v, 0); break;
            case "ventsmoke": VentSmokeShare = Math.Max(v, 0); break;
            case "dust": DustM2 = Math.Max(v, 0); break;
            case "light": LightShare = Math.Max(v, 0); break;
            case "pieces": PieceShare = Math.Max(v, 0); break;
            case "marks": PieceMarks = Math.Max(v, 0); break;
            case "piecemm": PieceMm = Math.Max(v, 1); break;
            case "fallen": FallenShare = Math.Max(v, 0); break;
            case "parts": Parts = (int)v; break;
            case "at": AtS = v; break;
            case "on": On = v != 0; break;
            default: return false;
        }
        return true;
    }

    public const string Knobs = "scale, open, flash, flashs, through, inside, vent, reach, smoke, ventsmoke, dust, light, pieces, marks, piecemm, fallen, parts (1 fireballs, 2 plate flashes, 4 vents, 8 lights, 16 smoke, 32 what flies off), at (a still at that age, s), on";
}
