using System.Globalization;
using System.Numerics;
using Fleetwright.Sim.Ships;

namespace Fleetwright.Map.Splashes;

/// <summary>The world view's splashes' knobs (docs/visuals/splashes.md), one place, for side-by-side captures:
/// <c>-splash=pitch:45,floor:2,dye:auto</c>. Each departure from the physical is named here, beside the number it
/// departs from.</summary>
public static class SplashLook
{
    /// <summary>The effects camera's pitch, degrees from straight down (decision 1, approved): tall effects are drawn as
    /// from a 3/4 view looking north, a height h going h·tan(pitch) up the screen. One for every zoom (decision 2), and
    /// every tall effect to come.</summary>
    public static double EffectPitchDeg = 45;

    /// <summary>tan of <see cref="EffectPitchDeg"/>: screen metres north per metre up.</summary>
    public static double EffectK => Math.Tan(EffectPitchDeg * Math.PI / 180);

    /// <summary>A column narrower than this on the screen, px, is drawn this wide with its opacity scaled down by as
    /// much (the size floor: it fades with altitude and never pops). Departs from the true size there.</summary>
    public static double FloorPx = 2;

    /// <summary>The column's clock under compression (decision 4, approved): it plays at the world's speed up to
    /// <see cref="ClockKnee"/>, then at knee·(speed/knee)^<see cref="ClockPower"/> (60x: 11x, a 12" column 0.73 s on the
    /// screen). Above the knee a salvo keeps its column with share <see cref="KeepK"/>·rate/speed, so about that many
    /// times the live columns of 1x at most.</summary>
    public static double ClockKnee = 2, ClockPower = 0.5, KeepK = 2;

    /// <summary>The foam's clock: the sea's, the world's speed capped (<see cref="SeaView.SpeedCap"/>). Above the cap
    /// the foam lasts longer than the world's time, so many more patches lie on the water at once: each is fainter by
    /// (cap/speed)^<see cref="FoamFaintPower"/>, never under <see cref="FoamFaintMin"/>.</summary>
    public static double FoamFaintPower = 0.5, FoamFaintMin = 0.3;

    /// <summary>The steps' scale in the marches (1: about one every 3 px of a column on the screen).</summary>
    public static double Steps = 1;

    /// <summary>A gain on the dyes' tint and stain (1 the research's 30-50% over white; the "vivid" toggle is 1.6).</summary>
    public static double Vivid = 1;

    /// <summary>A dev aid for captures: undyed splashes take a dye by their shooter (<c>dye:auto</c>), so the dye's
    /// look shows without a scenario that hands them out. The world's dyes are untouched.</summary>
    public static bool AutoDye;

    /// <summary>A gain on each splash's own variation in size, timing and start (SplashRenderer's Vary*: 1 as set,
    /// 0 every splash of a calibre alike).</summary>
    public static double Vary = 1;

    /// <summary>Which parts draw, for measuring each's cost: 1 foam, 2 stains, 4 shadows, 8 columns.</summary>
    public static int Parts = 15;

    /// <summary>Off: no world view splashes (the table's marks stay).</summary>
    public static bool On = true;

    /// <summary>The dyes' colours, sRGB-coded, by <see cref="Dye"/> (none: white): after the research's table (orange,
    /// red, pink, yellow, green, blue, black), strong enough to read as tinted water at 30-50% over white. Red and green
    /// are apart in lightness, so a red-green colour-blind eye still tells them apart; scenarios should still give a
    /// division's ships colours apart in lightness where they can.</summary>
    public static readonly Vector3[] Palette =
    [
        new(1f, 1f, 1f),
        new(1f, 0.52f, 0.1f),
        new(0.82f, 0.12f, 0.1f),
        new(1f, 0.45f, 0.68f),
        new(1f, 0.86f, 0.15f),
        new(0.2f, 0.78f, 0.3f),
        new(0.15f, 0.42f, 1f),
        new(0.08f, 0.08f, 0.09f),
    ];

    /// <summary>The column's playback rate at a clock speed.</summary>
    public static double ColumnRate(double speed) =>
        speed <= ClockKnee ? speed : ClockKnee * Math.Pow(speed / ClockKnee, ClockPower);

    /// <summary>The foam's clock rate at a clock speed.</summary>
    public static double FoamRate(double speed) => Math.Min(speed, SeaView.SpeedCap);

    /// <summary>The share of salvos that keep their column at a clock speed.</summary>
    public static double KeepShare(double speed) => speed <= ClockKnee ? 1 : Math.Min(1, KeepK * ColumnRate(speed) / speed);

    /// <summary>A splash's foam's strength at the clock speed it landed at.</summary>
    public static double FoamStrength(double speed) =>
        speed <= SeaView.SpeedCap ? 1 : Math.Max(FoamFaintMin, Math.Pow(SeaView.SpeedCap / speed, FoamFaintPower));

    public static bool Set(string kv)
    {
        var p = kv.Split(':');
        if (p.Length != 2)
            return false;
        if (p[0] == "dye")
        {
            AutoDye = p[1] == "auto";
            return p[1] is "auto" or "off";
        }
        if (!double.TryParse(p[1], CultureInfo.InvariantCulture, out double v))
            return false;
        switch (p[0])
        {
            case "pitch": EffectPitchDeg = Math.Clamp(v, 0, 80); break;
            case "floor": FloorPx = v; break;
            case "knee": ClockKnee = Math.Max(v, 1); break;
            case "power": ClockPower = Math.Clamp(v, 0, 1); break;
            case "keep": KeepK = v; break;
            case "faint": FoamFaintPower = v; break;
            case "steps": Steps = v; break;
            case "vivid": Vivid = v; break;
            case "on": On = v != 0; break;
            case "parts": Parts = (int)v; break;
            case "vary": Vary = Math.Max(v, 0); break;
            default: return false;
        }
        return true;
    }

    public const string Knobs = "pitch, floor, knee, power, keep, faint, steps, vivid, vary, on, parts (1 foam, 2 stains, 4 shadows, 8 columns), dye:auto";
}
