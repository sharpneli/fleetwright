using System.Globalization;

namespace Fleetwright.Map.Flashes;

/// <summary>The world view's muzzle flashes' knobs (docs/visuals/muzzle-flashes.md), one place, for side-by-side
/// captures: <c>-flash=gain:2,offset:0</c>. Each departure from the physical is named here, beside the number it departs
/// from.</summary>
public static class FlashLook
{
    /// <summary>A gain on the flash's brightness over the physical contrast against a lit white deck (1: as physical).</summary>
    public static double Gain = 1;

    /// <summary>The halo's radii, px, and each one's share of the compressed source (the research's bloom: 1.5, 6 and
    /// 18 px at 0.2, 0.1 and 0.05), drawn analytically by each flash.</summary>
    public static double HaloScale = 1, HaloGain = 1;

    /// <summary>The compression's knee for the halo's source, in display units (1 a white deck): x/(1 + x/knee).</summary>
    public static double HaloKnee = 6;

    /// <summary>Each mount's start after its record, up to this, s, from its seed (the splashes' lesson: a broadside's
    /// flashes all at once read as clones). Display only; the salvo's ripple will come with fire control.</summary>
    public static double OffsetS = 0.04;

    /// <summary>The fewest frames a flash shows over, its intensity scaled to keep its Blondel-Rey effective intensity
    /// (research §7.2: the eye sees a short flash by its integral over ~0.2 s), so a 4" flash at 1x or a 12" at 60x is
    /// never a one-frame blink. Departs from the true duration there.</summary>
    public static double FloorFrames = 2;

    /// <summary>A shot's secondary flash's intensity scatter, lognormal σ (the research's 0.25).</summary>
    public static double Scatter = 0.25;

    /// <summary>The air's relative humidity and temperature, °C, until the weather has them (the smoke's water fog).</summary>
    public static double Humidity = 0.7, AirC = 15;

    /// <summary>Which parts draw, for measuring each's cost: 1 the flashes.</summary>
    public static int Parts = 1;

    /// <summary>A dev aid for captures: the shots of the newest 2 s in the book drawn at this age, s, instead of their own
    /// (-1: off), and kept while it is.</summary>
    public static double AtS = -1;

    /// <summary>Off: no world view flashes (the table's marks stay).</summary>
    public static bool On = true;

    public static bool Set(string kv)
    {
        var p = kv.Split(':');
        if (p.Length != 2 || !double.TryParse(p[1], CultureInfo.InvariantCulture, out double v))
            return false;
        switch (p[0])
        {
            case "gain": Gain = Math.Max(v, 0); break;
            case "halo": HaloGain = Math.Max(v, 0); break;
            case "haloscale": HaloScale = Math.Max(v, 0.1); break;
            case "knee": HaloKnee = Math.Max(v, 0.1); break;
            case "offset": OffsetS = Math.Max(v, 0); break;
            case "scatter": Scatter = Math.Max(v, 0); break;
            case "rh": Humidity = Math.Clamp(v, 0, 1); break;
            case "air": AirC = v; break;
            case "parts": Parts = (int)v; break;
            case "frames": FloorFrames = Math.Max(v, 0); break;
            case "at": AtS = v; break;
            case "on": On = v != 0; break;
            default: return false;
        }
        return true;
    }

    public const string Knobs = "gain, halo, haloscale, knee, offset, scatter, rh, air, frames, parts (1 flashes), at (a still at that age, s), on";
}
