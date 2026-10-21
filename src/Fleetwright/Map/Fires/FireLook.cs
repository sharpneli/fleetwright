using System.Globalization;

namespace Fleetwright.Map.Fires;

/// <summary>The world view's fires' knobs (docs/visuals/hits-and-fires.md I4, I5), one place, for side-by-side captures:
/// <c>-fires=height:1.2,light:0.5</c>. The estimates are flagged [EST]: placeholders until the research requests come
/// back (decision 9).</summary>
public static class FireLook
{
    /// <summary>A scale on the flames' height (1: Heskestad's, L = 0.235 Q^0.4 − 1.02 D, Q in kW over D m).</summary>
    public static double Height = 1;

    /// <summary>The flames' puffing: Q's tongues rise and break at f = 1.5/√D Hz (a pool fire's), by this much of their
    /// length (the flicker's depth).</summary>
    public static double Puff = 0.25;

    /// <summary>Charges burning (a propellant seat): their flare's height over the fire's own at its Q, and its heat's
    /// lift toward white [EST].</summary>
    public static double Flare = 1.4;

    /// <summary>The flames lean downwind in the apparent wind: this share of their length at most (a strong wind bends a
    /// flame to ~45°) [EST].</summary>
    public static double Lean = 0.7;

    /// <summary>The flames' temperature for their light, K (a sooty fire's ~1,200 K), and the share of their light as
    /// drawn that they throw on the sea, the ships and the smoke [EST].</summary>
    public static double Kelvin = 1250, LightShare = 0.3;

    /// <summary>The smoke (I5): the field's (<c>SmokeSources.Fire</c>, docs/battle/smoke.md Z4) times a gain for
    /// side-by-side captures; the steam's, m² per kg boiled, and its evaporation, s; a puff laid every so often, s (the
    /// sea's clock); the share of the smoke's book the fires may fill to (the funnels get less) [EST].</summary>
    public static double SmokeGain = 1, SteamM2Kg = 500, SteamS = 15, EveryS = 2, Spare = 0.9;

    /// <summary>Which parts draw: 1 the flames, 2 their lights, 4 their smoke (I5).</summary>
    public static int Parts = 7;

    /// <summary>Off: no world view fires (the table's glow stays).</summary>
    public static bool On = true;

    public static bool Set(string kv)
    {
        var p = kv.Split(':');
        if (p.Length != 2 || !double.TryParse(p[1], CultureInfo.InvariantCulture, out double v))
            return false;
        switch (p[0])
        {
            case "height": Height = Math.Max(v, 0); break;
            case "puff": Puff = Math.Clamp(v, 0, 1); break;
            case "flare": Flare = Math.Max(v, 0); break;
            case "lean": Lean = Math.Clamp(v, 0, 1); break;
            case "kelvin": Kelvin = Math.Max(v, 600); break;
            case "light": LightShare = Math.Max(v, 0); break;
            case "smoke": SmokeGain = Math.Max(v, 0); break;
            case "steam": SteamM2Kg = Math.Max(v, 0); break;
            case "steams": SteamS = Math.Max(v, 0.1); break;
            case "every": EveryS = Math.Max(v, 0.2); break;
            case "parts": Parts = (int)v; break;
            case "on": On = v != 0; break;
            default: return false;
        }
        return true;
    }

    public const string Knobs = "height, puff, flare, lean, kelvin, light, smoke, steam, steams, every, parts (1 flames, 2 lights, 4 smoke), on";
}
