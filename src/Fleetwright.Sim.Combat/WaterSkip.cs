using Fleetwright.Sim.Core;
using Fleetwright.Sim.Ships;

namespace Fleetwright.Sim.Combat;

/// <summary>A shell skipping off the sea (docs/battle/shells-armour-damage.md E7's ricochet; research
/// simulation/damage-research/16-water-ricochet.md, <c>water_ricochet_ref.py</c>): what the graze is and what leaves it.
/// The shell system flies what leaves (<see cref="SkipFlight"/>); this is the graze alone. Pure, no garbage.
/// <list type="bullet">
/// <item><b>The face it meets</b> (<see cref="Facet"/>; 16 §1.4): the sea isn't flat. The slope of the waves long enough
/// for a shell to plane on (<see cref="RollSea.SlopeRms"/>: 5 m and longer) tilts the water it meets, along its line and
/// across it, and a shell falling flat meets more of the faces turned toward it than of the backs (as a low sun lights
/// them), so even a shallow shell often meets the sea steeply, and plunges.</item>
/// <item><b>Skip or plunge</b>: under <see cref="SimSettings.WaterRicochetDeg"/> to the face it skips, a hard limit
/// [EST, 16 R1: a shell's bulk density and elongated bodies put it past the steel sphere's 6.4°].</item>
/// <item><b>Its speed</b> (<see cref="Kept"/>; 16 §2.1): exp(−(θ/θv)^p), the .50's Weibull fit scaled to a shell's
/// density: little lost on a flat graze, most near the limit, where it planes deep and long.</item>
/// <item><b>Its new line</b> (<see cref="ExitDeg"/>, <see cref="AcrossDeg"/>; 16 §2.2-2.3): a pointed shell pitches nose
/// up while it planes and leaves steeper than it met the face, widely spread (lognormal about a median); and mostly to
/// the side its rifling turns it, wild when the graze took much of its speed. It leaves yawed (<see cref="YawDeg"/>,
/// 16 §2.5), the pitch that turned it, which drags it until it settles.</item>
/// <item><b>Its fuze</b> (16 §3): a nose fuze fires on the graze only when the sea meets it hard enough
/// (<see cref="NoseFires"/>: the RN's 1921 minima); a base fuze may be started by the graze's deceleration, readily if it
/// starts on thin plate, seldom if it was made not to (<see cref="BaseStarts"/>), and then bursts in the air its delay
/// on.</item>
/// </list></summary>
public static class WaterSkip
{
    /// <summary>The sea's face where a shell falling at fallDeg meets it, its slope along the shell's line (rising ahead: the
    /// face turned toward it) and across it (rising to the right), as tangents: the waves' slopes drawn normal with the rms
    /// given, along weighted by how much of each the shell meets, sin(fall + α)/cos α = sin fall + tan α·cos fall (the faces
    /// turned to it more, a back it can't reach not at all; crests shadowing troughs left out [EST]).</summary>
    public static (double Along, double Across) Facet(double fallDeg, double rmsAlong, double rmsAcross, ref RandomStream r)
    {
        double across = rmsAcross > 0 ? rmsAcross * r.NextGaussian() : 0;
        if (!(rmsAlong > 0))
            return (0, across);
        // u = tan fall + slope: normal about tan fall, weighted by u where u > 0. Rejection: a normal kept by u over a
        // bound (six sigmas past the mean's), the last positive one taken if none is
        double m = Math.Tan(double.DegreesToRadians(Math.Clamp(fallDeg, 0, 89))), top = m + 6 * rmsAlong, last = double.NaN;
        for (int i = 0; i < 64; i++)
        {
            double u = m + rmsAlong * r.NextGaussian();
            if (!(u > 0))
                continue;
            last = u;
            if (r.NextDouble() * top < u)
                return (u - m, across);
        }
        return (double.IsNaN(last) ? 0 : last - m, across);
    }

    /// <summary>The unit normal of a face with slopes along and across a line (tangents), the line's way over the sea
    /// (unit, horizontal), up (unit) and right (forward × up).</summary>
    public static Vec3d Normal(Vec3d up, Vec3d forward, Vec3d right, double along, double across) =>
        (up - forward * along - right * across).Normalized();

    /// <summary>The share of its speed a shell keeps off a graze it met the face at meetDeg: exp(−(θ/θv)^p) [16 R2, EST: the
    /// .50 M2's fit, 7.37° and 2.258, its angle scaled toward a shell's lower density].</summary>
    public static double Kept(double meetDeg, SimSettings s) =>
        Math.Exp(-Math.Pow(Math.Max(meetDeg, 0) / s.WaterRicochetKeptDeg, s.WaterRicochetKeptPower));

    /// <summary>The angle it leaves the face at, degrees: lognormal about a median of base + share × the angle it met it
    /// at, capped [16 R3, EST: pointed bodies leave steeper than they met it, the spread wide].</summary>
    public static double ExitDeg(double meetDeg, SimSettings s, ref RandomStream r)
    {
        double median = s.WaterRicochetExitDeg + s.WaterRicochetExitShare * Math.Max(meetDeg, 0);
        return Math.Min(median * Math.Exp(s.WaterRicochetExitSigma * r.NextGaussian()), s.WaterRicochetExitMaxDeg);
    }

    /// <summary>The turn across its line, degrees, positive to the right: its rifling's hand (+1 right, −1 left) times the
    /// mean, and a normal spread widened by the speed it lost past <see cref="SimSettings.WaterRicochetWildKept"/> [16 R3,
    /// EST: the .50's ~10° right, naval operators' "mainly to the right" and "wildly unpredictable"].</summary>
    public static double AcrossDeg(double kept, int hand, SimSettings s, ref RandomStream r)
    {
        double wide = kept < s.WaterRicochetWildKept ? s.WaterRicochetWildKept / Math.Max(kept, 0.05) : 1;
        return hand * s.WaterRicochetRightDeg + s.WaterRicochetAcrossDeg * wide * r.NextGaussian();
    }

    /// <summary>Its yaw leaving, degrees: the pitch that turned it, |exit − meet|, and a wobble's |N(0, σ)| [16 R5, EST].</summary>
    public static double YawDeg(double meetDeg, double exitDeg, SimSettings s, ref RandomStream r) =>
        Math.Abs(exitDeg - meetDeg) + Math.Abs(s.WaterRicochetYawDeg * r.NextGaussian());

    /// <summary>Whether a nose fuze fires on a graze: the sea meeting it at v·sin θ of at least
    /// <see cref="SimSettings.WaterGrazeNoseMs"/> (the RN's 1921 minima for reliable functioning on water: the 4" at 2,100-2,400
    /// yd, under 2°, ~16-17 m/s; 16 §3.1). Flatter, it skips unburst.</summary>
    public static bool NoseFires(double v, double meetDeg, SimSettings s) =>
        v * Math.Sin(double.DegreesToRadians(Math.Max(meetDeg, 0))) >= s.WaterGrazeNoseMs;

    /// <summary>The graze's deceleration along the shell, g: half its section wetted at <see cref="SimSettings.WaterGrazeCd"/>,
    /// ½·½ρv²·Cd·A/m (16 §3.2, W5: a 12" at 500 m/s ~370 g, a 4" ~1,100 g).</summary>
    public static double GrazeG(in ShellTypeRow shell, double v, SimSettings s)
    {
        double d = shell.CalibreMm / 1000, area = Math.PI / 4 * d * d;
        return 0.25 * WaterRun.SeaRho * v * v * s.WaterGrazeCd * area / Math.Max(shell.MassKg, 1e-3) / 9.80665;
    }

    /// <summary>The chance a graze starts a base fuze: 1 − e^(−a/a_f), a the graze's deceleration and a_f the fuze's,
    /// <see cref="SimSettings.WaterGrazeFuzeG"/> for one started by 0.07 calibres of plate, in proportion to its start plate
    /// [16 R4, EST: the Ijuin's 0.01 starts on most grazes (Tsesarevich 1904), a robust heavy AP's seldom (Albert W. Grant's
    /// two ricochets), a small shell's more]. None where the water starts no fuze.</summary>
    public static double BaseStarts(in ShellTypeRow shell, double v, SimSettings s)
    {
        if (shell.Fuze != Fuze.Base || !WaterRun.FuzeStarts(shell, s))
            return 0;
        double need = s.WaterGrazeFuzeG * Math.Max(shell.FuzeStartCal, 0) / 0.07;
        return need > 0 ? -double.ExpM1(-GrazeG(shell, v, s) / need) : 1;
    }

    /// <summary>A base fuze on a graze (the shell's draws): started (<see cref="BaseStarts"/>), not blind and the filler not a
    /// dud (<see cref="Terminal.Initiate"/>), its delay scattered, s, and the filler's force; force 0 when it flies on
    /// unstarted.</summary>
    public static (double DelayS, double Force) BaseOnGraze(in ShellTypeRow shell, double v, SimSettings s, ref RandomStream r)
    {
        if (!(shell.FillerKg > 0) || shell.Fuze != Fuze.Base)
            return (0, 0);
        if (r.NextDouble() >= BaseStarts(shell, v, s) || r.NextDouble() < shell.FuzeBlind)
            return (0, 0);
        var (o, force) = Terminal.Initiate(shell, s, ref r);
        if (o == Outcome.Dud)
            return (0, 0);
        return (shell.FuzeDelayS * Math.Max(1 + shell.FuzeScatter * r.NextGaussian(), 0), force);
    }
}
