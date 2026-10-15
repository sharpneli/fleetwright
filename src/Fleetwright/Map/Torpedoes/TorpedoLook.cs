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
            default: return false;
        }
        return true;
    }

    public const string Knobs = "fast, slow, life, width, spread, dive, on";
}
