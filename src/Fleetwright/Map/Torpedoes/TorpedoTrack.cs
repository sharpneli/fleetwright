namespace Fleetwright.Map.Torpedoes;

/// <summary>A torpedo's bubble track as it surfaces (docs/visuals/torpedoes.md R1), the one model the table's line and
/// the world view's ribbon both read. The exhaust's bubbles are let go at the torpedo's depth and rise at speeds between
/// <see cref="RiseSlowMs"/> (mm bubbles on their own) and <see cref="RiseFastMs"/> (a bubbly plume dragging its water up),
/// so the water the torpedo passed <c>since</c> seconds ago shows its track from d / fast and in full by d / slow: a soft
/// head (physically well behind the torpedo) and a tail that goes on surfacing after the run has ended. The surfaced film
/// fades over <see cref="LifeS"/>; it's about 1 m + 0.25 d wide where it comes up and spreads as √(age). A mount's
/// torpedo dives below its set depth after it falls in and recovers over <see cref="DiveRunM"/>, so its track breaks
/// there. **Drawn closer than physics** (the user, 2026-10-15: the track must show where the torpedo is, a small gap reads,
/// a large one confuses): the surfacing times are <see cref="LagShare"/> of the bubbles' own, so at 6 m down the head
/// is ~1 s (25 m at 41 kn) behind the torpedo, not ~7.5 s, and full by ~3.6 s. Estimates from physics (no source gives
/// them yet: research/REQUESTS.md, 2026-10-15), set through <c>-tracks=</c> (<see cref="TorpedoLook"/>). Display only; pure functions.</summary>
public static class TorpedoTrack
{
    /// <summary>The bubbles' rise, m/s: the first up, and the bulk.</summary>
    public static double RiseFastMs = 0.8, RiseSlowMs = 0.25;

    /// <summary>The share of the bubbles' rise times the track is drawn with (1 physical): a departure for readability.</summary>
    public static double LagShare = 0.15;

    /// <summary>How long the surfaced film lasts, s: the table's 90 s.</summary>
    public static double LifeS = 90;

    /// <summary>The width where the bubbles come up, m: a metre and a share of the depth (the plume spreads as it rises);
    /// then its spreading, m²/s (to ~7 m by 90 s from 4 m down).</summary>
    public static double WidthM0 = 1, WidthDepthShare = 0.25, SpreadM2S = 0.5;

    /// <summary>A mount's torpedo's dive after it falls in: deepest (1 + <see cref="DiveShare"/> times its set depth) a
    /// third of the way along <see cref="DiveRunM"/>, back at its depth by its end.</summary>
    public static double DiveShare = 1, DiveRunM = 150;

    /// <summary>The depth a torpedo is at after running <paramref name="runM"/>, m, set to <paramref name="depthM"/>: a
    /// mount's dives first (<paramref name="dives"/>), a tube's starts at its depth.</summary>
    public static double Depth(double runM, double depthM, bool dives)
    {
        if (!dives || runM <= 0 || runM >= DiveRunM)
            return depthM;
        double x = runM / DiveRunM;
        // a hump, highest at a third: x(1-x)² normalised (4/27 at x = 1/3)
        return depthM * (1 + DiveShare * x * (1 - x) * (1 - x) * 27 / 4);
    }

    /// <summary>The share of the gas let go at a point that has surfaced, <paramref name="sinceS"/> after the torpedo passed
    /// it at <paramref name="depthM"/>.</summary>
    public static double Surfaced(double sinceS, double depthM)
    {
        double a = HeadLagS(depthM), b = FullS(depthM);
        if (sinceS <= a)
            return 0;
        if (sinceS >= b)
            return 1;
        double t = (sinceS - a) / (b - a);
        return t * t * (3 - 2 * t);
    }

    /// <summary>How the track shows at a point, 0 to 1: what has surfaced, fading over its life (the square of what's
    /// left, as the table's line faded).</summary>
    public static double Strength(double sinceS, double depthM)
    {
        double left = 1 - sinceS / LifeS;
        return left <= 0 ? 0 : Surfaced(sinceS, depthM) * left * left;
    }

    /// <summary>The track's width at a point, m.</summary>
    public static double WidthM(double sinceS, double depthM)
    {
        double w0 = WidthM0 + WidthDepthShare * depthM;
        return Math.Sqrt(w0 * w0 + SpreadM2S * Math.Max(sinceS - HeadLagS(depthM), 0));
    }

    /// <summary>How long after the torpedo passes its track first shows, s: its head's lag.</summary>
    public static double HeadLagS(double depthM) => LagShare * depthM / RiseFastMs;

    /// <summary>How long after the torpedo passes its track shows in full, s.</summary>
    public static double FullS(double depthM) => LagShare * depthM / RiseSlowMs;

    /// <summary>How long a run is kept after its end, s: until the last of it has surfaced and faded.</summary>
    public static double KeepS => LifeS;
}
