using System.Globalization;

namespace Fleetwright.Map.Smoke;

/// <summary>The smoke field's drawing's knobs (docs/battle/smoke.md Z5), one place, set through <c>-smokelook=</c> for
/// side-by-side captures: drawn or not, a gain on its opacity (1: what blocks sight, exactly), whether the world view
/// lifts it its height up the screen (the effects camera), the column depth its quads reach out to, and its grain.</summary>
public static class SmokeFieldLook
{
    public static bool On = true;
    public static double Gain = 1;
    public static bool Lift = true;
    public static double EdgeTau = 0.005;

    /// <summary>The grain's gain (docs/battle/smoke.md Z5d): the noise's spread about 1 on each puff's τ (0: plain).</summary>
    public static double Grain = 0.6;

    /// <summary>Which passes run: 1 the accumulation, 2 the resolve.</summary>
    public static int Parts = 3;

    /// <summary>The most levels drawn into (1: every puff into the half-resolution level, at its cost).</summary>
    public static int Levels = 10;

    public static bool Set(string kv)
    {
        var p = kv.Split(':');
        if (p.Length != 2 || !double.TryParse(p[1], CultureInfo.InvariantCulture, out double v))
            return false;
        switch (p[0])
        {
            case "on": On = v != 0; break;
            case "gain": Gain = Math.Max(v, 0); break;
            case "lift": Lift = v != 0; break;
            case "edge": EdgeTau = Math.Clamp(v, 1e-4, 0.5); break;
            case "grain": Grain = Math.Clamp(v, 0, 2); break;
            case "parts": Parts = (int)v; break;
            case "levels": Levels = Math.Clamp((int)v, 1, 10); break;
            default: return false;
        }
        return true;
    }

    public const string Knobs = "on, gain, lift, edge, grain, parts (1 accumulate, 2 resolve), levels";
}
