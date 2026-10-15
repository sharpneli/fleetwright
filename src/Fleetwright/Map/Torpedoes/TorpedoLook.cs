using System.Globalization;

namespace Fleetwright.Map.Torpedoes;

/// <summary>The world view's torpedoes' knobs (docs/visuals/torpedoes.md), one place, for side-by-side captures:
/// <c>-tracks=fast:0.8,slow:0.25,life:90</c> (<c>-torpedo=</c> is the dev's spread). The track's own numbers live in
/// <see cref="TorpedoTrack"/> and are set from here. Each departure from the physical is named here, beside the number
/// it departs from.</summary>
public static class TorpedoLook
{
    /// <summary>Off: no world view torpedoes (the table's marks stay).</summary>
    public static bool On = true;

    /// <summary>The track's film at full strength (1: its surfaced share as the ribbon's foam).</summary>
    public static double Gain = 1;

    /// <summary>A track narrower than this on the screen, px, is drawn this wide, its foam thinned by the square root of
    /// as much (the size floor, as the splashes', but brighter than its true coverage): a departure zoomed out, so a 2 m
    /// track still reads at 4 m/px.</summary>
    public static double FloorPx = 2;

    /// <summary>How much of the small waves' slope variance the track's film takes away (0 none, the default until R2's
    /// captures judge it: decision 3).</summary>
    public static double Slick;

    /// <summary>The torpedo's darkness under the water over its physical contrast (decision 2: emphasised, a named
    /// departure; 1 is physical, e^(−2Kd) ≈ 0.1-0.14 at 4-6 m), and the least length it draws on the screen, px.</summary>
    public static double Body = 3.5, BodyFloorPx = 10;

    /// <summary>A dev aid for captures: every torpedo drawn as it is <c>at</c> seconds after the newest launch (NaN: as
    /// it is), so stills of the track are the same run to run.</summary>
    public static double AtS = double.NaN;

    /// <summary>The moment the torpedoes are drawn at: the frame's, or the still's (<see cref="AtS"/>).</summary>
    public static long DrawMs(TorpedoBook book, long nowMs)
    {
        if (double.IsNaN(AtS) || book.Count == 0)
            return nowMs;
        long newest = long.MinValue;
        for (int i = 0; i < book.Count; i++)
            newest = Math.Max(newest, book[i].LaunchMs);
        return newest + (long)(AtS * 1000);
    }

    public static bool Set(string kv)
    {
        var p = kv.Split(':');
        if (p.Length != 2 || !double.TryParse(p[1], CultureInfo.InvariantCulture, out double v))
            return false;
        switch (p[0])
        {
            case "fast": TorpedoTrack.RiseFastMs = Math.Max(v, 0.01); break;
            case "slow": TorpedoTrack.RiseSlowMs = Math.Clamp(v, 0.01, TorpedoTrack.RiseFastMs); break;
            case "life": TorpedoTrack.LifeS = Math.Max(v, 1); break;
            case "width": TorpedoTrack.WidthM0 = Math.Max(v, 0.1); break;
            case "spread": TorpedoTrack.SpreadM2S = Math.Max(v, 0); break;
            case "dive": TorpedoTrack.DiveShare = Math.Max(v, 0); break;
            case "on": On = v != 0; break;
            case "gain": Gain = Math.Max(v, 0); break;
            case "floor": FloorPx = Math.Max(v, 0); break;
            case "slick": Slick = Math.Max(v, 0); break;
            case "at": AtS = v; break;
            case "body": Body = Math.Max(v, 0); break;
            case "bodyfloor": BodyFloorPx = Math.Max(v, 0); break;
            default: return false;
        }
        return true;
    }

    public const string Knobs = "fast, slow, life, width, spread, dive, gain, floor, slick, body, bodyfloor, at (a still at S after the newest launch), on";
}
