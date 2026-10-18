using Fleetwright.Sim.Core;
using Fleetwright.Sim.Ships;

namespace Fleetwright.Sim.Combat;

/// <summary>How a gun is laid to put its shell on a point: the shell's flight (the plane's heading at the muzzle, the
/// elevation and speed it leaves with, the ship's own motion in, as <see cref="BallisticTable.Position"/> takes them) and
/// the gun's own lay (its elevation, and its horizontal direction in the world), with the time of flight.</summary>
public readonly record struct Lay(Vec3d Forward, double ElevationDeg, double Speed, double GunElevationDeg, Vec3d GunHorizontal, double TofS);

/// <summary>The fire-control chain's pure models (docs/battle/fighting.md 4.2b, a port of
/// research/simulation/fire_control_ref.py <c>engage()</c> cut to its spine): what a reading's error is, how a salvo
/// spreads, how a gun is laid for a point with its ship's motion, and how a spotter's judgement corrects the next
/// salvo. The gunnery system (the world's) runs them in time.</summary>
public static class FireControl
{
    public const double Mil = 1e-3;
    const double Arcsec = Math.PI / 648_000;

    /// <summary>One unit of a rangefinder's error at a range, metres: δ·R²/(B_eff·M_eff), the optics' acuity over its base
    /// times its useful magnification (<see cref="Magnification"/>), a base past the optics' rigid one delivering as the
    /// flex power of its length (fire-control-designer.md §3.1, NDRC 1941; Sensor.unit_error). The one law: fire control's
    /// readings and the sighting's fixes both take it.</summary>
    public static double RangefinderUnitM(double baseM, in OpticsRow o, double magnification, double rangeM)
    {
        double b = baseM > o.RigidBaseM && o.RigidBaseM > 0 ? o.RigidBaseM * Math.Pow(baseM / o.RigidBaseM, o.FlexExp) : baseM;
        return o.DeltaArcsec * Arcsec * rangeM * rangeM / Math.Max(b * magnification, 1e-6);
    }

    /// <summary>A rangefinder's useful magnification, standing on a support at a height (fire-control-designer.md §3.1,
    /// designer_ref.py RFSensor.M_eff): the air's shimmer and the support's shake cap it, M_eff = M / √(1 + (M/M_lim)²)
    /// with 1/M_lim² = 1/M_air² + 1/M_vib², M_vib the support's stiffness, less as it stands higher (√(25 m / h)) and half
    /// again on a gyro-stabilised mounting; normalised so the nominal ×28 on a tower is ×28 (the levels were set there,
    /// [CAL]); past the glass's top magnification the image dims (×√(M_max/M)), and M is never more than 1.3 M_max. Haze
    /// is the contrast's (F5c), not the shimmer's.</summary>
    public static double Magnification(in OpticsRow o, InstrumentSupport support, double heightM)
    {
        double m = o.MaxMagnification > 0 ? Math.Min(o.Magnification, 1.3 * o.MaxMagnification) : o.Magnification;
        double vib = Stiffness(support) * Math.Min(1, Math.Sqrt(25 / Math.Max(heightM, 1))) * (o.Gyro ? 1.5 : 1);
        double lim = 1 / Math.Sqrt(1 / (AirMagnification * AirMagnification) + 1 / (vib * vib));
        double me = m / Math.Sqrt(1 + m / lim * (m / lim)) / MagnificationNorm;
        return o.MaxMagnification > 0 && m > o.MaxMagnification ? me * Math.Sqrt(o.MaxMagnification / m) : me;
    }

    /// <summary>The magnification past which the clear air's shimmer costs (designer §3.1, M_ATM_CLEAR [INFERRED]).</summary>
    const double AirMagnification = 60;

    /// <summary>A support's stiffness, as the magnification its shake allows at 25 m (designer §3.1, MAST_STIFF [INFERRED]:
    /// turret 70, tower 60, deck 55, tripod 50, pole 32); a lattice mast as a tripod and a cage mast less, the US cage
    /// masts' whip being their known fault [EST].</summary>
    public static double Stiffness(InstrumentSupport s) => s switch
    {
        InstrumentSupport.Turret => 70,
        InstrumentSupport.Tower => 60,
        InstrumentSupport.Tripod or InstrumentSupport.Lattice => 50,
        InstrumentSupport.Cage => 40,
        InstrumentSupport.Pole => 32,
        _ => 55,
    };

    /// <summary>The nominal ×28 on a tower (60) at 25 m, as M_eff gives it before normalising, over 28 (designer_ref.py
    /// _REF_NORM, 0.835).</summary>
    static readonly double MagnificationNorm = 1 / Math.Sqrt(1 + Math.Pow(28 * Math.Sqrt(1.0 / (AirMagnification * AirMagnification) + 1.0 / (60 * 60)), 2));

    /// <summary>A salvo of n guns' own dispersion, 1 sigma in range and across, metres: the pattern (its 100% spread,
    /// % of the range) is ratio(n)·D, D = 0.798σ (USNI 1917), times 0.85 for the gun-to-gun scatter in quoted
    /// patterns (Gun.sigma_D).</summary>
    public static (double Range, double Across) Dispersion(double patternPct, double rangeM, int guns, double deflectionRatio)
    {
        double ratio = Interp(guns, PatternGuns, PatternRatio);
        double sr = patternPct / 100 * rangeM / ratio / 0.798 * 0.85;
        return (sr, deflectionRatio * sr);
    }

    static readonly double[] PatternGuns = [1, 2, 3, 4, 6, 8, 9, 10, 12], PatternRatio = [1.6, 1.9, 2.43, 2.74, 3.47, 3.85, 4.0, 4.13, 4.34];

    static double Interp(double x, double[] xs, double[] ys)
    {
        if (x <= xs[0])
            return ys[0];
        for (int i = 1; i < xs.Length; i++)
            if (x <= xs[i])
                return ys[i - 1] + (ys[i] - ys[i - 1]) * (x - xs[i - 1]) / (xs[i] - xs[i - 1]);
        return ys[^1];
    }

    /// <summary>How a gun at a muzzle on a ship moving at a velocity is laid so its shell comes down on a point at sea
    /// level: the shell leaves with the gun's velocity and the ship's, so the gun is trained off the line by the ship's
    /// motion across it, and the elevation is the one whose flight, at the speed and angle the two give, falls at the
    /// point's range (the range table's, by the secant). False beyond the gun's reach. The Earth's turning is left out,
    /// as the era's computers left it: the spot takes it off with the rest of the bias.</summary>
    /// <param name="guessDeg">An elevation near the answer (last second's), or NaN.</param>
    public static bool Solve(BallisticTable t, Vec3d muzzle, Vec3d shipVelocity, Vec3d aim, out Lay lay, double guessDeg = double.NaN)
    {
        lay = default;
        var up = muzzle.Normalized();
        double h0 = muzzle.Length - Earth.Radius;
        var toward = aim - up * Vec3d.Dot(aim, up);
        if (toward.Length < 1e-6)
            return false;
        var f = toward.Normalized();
        var c = Vec3d.Cross(f, up);   // to the right of the line
        double v0 = t.Key.MuzzleVelocityMs, vf = Vec3d.Dot(shipVelocity, f), vc = Vec3d.Dot(shipVelocity, c), vu = Vec3d.Dot(shipVelocity, up);
        double k = Math.Sqrt(Math.Max(v0 * v0 - vc * vc, 1));
        double d = Earth.Distance(muzzle, aim);
        double e0 = t.ElevationFor(d, h0, 1, guessDeg);
        if (double.IsNaN(e0))
            return false;
        // the gun's angle in the plane of fire, θ: the shell leaves at vh = vf + k cos θ along, vu + k sin θ up
        double Miss(double thetaDeg, out double e, out double speed, out double tof)
        {
            double th = double.DegreesToRadians(thetaDeg), vh = vf + k * Math.Cos(th), vz = vu + k * Math.Sin(th);
            e = double.RadiansToDegrees(Math.Atan2(vz, vh));
            speed = Math.Sqrt(vh * vh + vz * vz) / v0;
            var fall = t.Fall(e, h0, speed);
            tof = fall.Tof;
            return fall.Range - d;
        }
        double a = e0, ma = Miss(a, out double ea, out double sa, out double ta);
        double b = e0 + 0.05, mb = Miss(b, out double eb, out double sb, out double tb);
        for (int i = 0; i < 8 && Math.Abs(mb) > 0.01 && mb != ma; i++)
        {
            double next = b - mb * (b - a) / (mb - ma);
            (a, ma) = (b, mb);
            b = next;
            mb = Miss(b, out eb, out sb, out tb);
        }
        if (!(Math.Abs(mb) < 1) || b > t.Key.MaxElevationDeg + 1)
            return false;
        double thb = double.DegreesToRadians(b);
        var gunHorizontal = (f * (k * Math.Cos(thb)) - c * vc).Normalized();
        lay = new Lay(f, eb, sb, double.RadiansToDegrees(Math.Asin(Math.Clamp(k * Math.Sin(thb) / v0, -1, 1))), gunHorizontal, tb);
        return true;
    }

    /// <summary>A spotter's judgement of a salvo's fall against the target, by eye: over, short or straddling, and the
    /// deflection seen (<see cref="Ladder"/> turns it into the next salvo's correction).</summary>
    public enum Sign : byte
    {
        Short,
        Over,
        Straddle,
    }

    /// <summary>The bracket a battery's spots make, kept in correction space (fire-control.md F6;
    /// fire-control-research.md §9, computing.md §2.5). Each spotted salvo says the target lies beyond the range
    /// correction it was fired with (short), short of it (over) or about on it (a straddle), so the spots bound the
    /// correction the target needs from below and above, whatever order they come in. Open on a side, the next salvo
    /// goes a step beyond the bound seen; closed, into its middle: the RN's 400, 200, 100 yd and the USN's bracket and
    /// halve are both this, and the bracket is never let narrower than a floor (the USN's "never below the pattern").
    /// It isn't hard: each bound widens with time at the computer's own doubt of the range rate (the truth drifts in
    /// range as the rate's error carries it), and a spot against a bound drops it.</summary>
    public struct Bracket
    {
        /// <summary>The correction the target needs is over <see cref="Lo"/> and under <see cref="Hi"/>, m along the line
        /// of fire (infinite: open), as each stood when it was set.</summary>
        public double Lo, Hi;
        public long LoMs, HiMs;
        /// <summary>The step beyond a bound while the other side is open, m.</summary>
        public double Step;

        public static Bracket Open(double stepM) => new() { Lo = double.NegativeInfinity, Hi = double.PositiveInfinity, Step = stepM };

        public readonly bool Closed => !double.IsInfinity(Lo) && !double.IsInfinity(Hi);

        /// <summary>The low bound at a time, widened at the drift (m/s) since it was set.</summary>
        public readonly double LoAt(long ms, double driftMs) => Lo - driftMs * Math.Max(ms - LoMs, 0) / 1000.0;

        public readonly double HiAt(long ms, double driftMs) => Hi + driftMs * Math.Max(ms - HiMs, 0) / 1000.0;

        /// <summary>The correction to fire with now. While the spots hold her (<paramref name="held"/>: a hit or a straddle
        /// lately), the bracket's: its middle closed, a step beyond its bound open; the computer's new ranges move the
        /// bracket with them (Rebase), so they never move the guns ("Sir, the target teleported 200 m": the user,
        /// 2026-10-18). Otherwise none while the solution lies inside the bracket (the spots say nothing against it: a computer
        /// that took a good range in stands), else the bracket's as before. None before the first spot.</summary>
        public readonly double Aim(long ms, double driftMs, bool held = false)
        {
            double lo = LoAt(ms, driftMs), hi = HiAt(ms, driftMs);
            if (double.IsInfinity(Lo) && double.IsInfinity(Hi) || !held && lo < 0 && hi > 0)
                return 0;
            return Closed ? 0.5 * (lo + hi) : !double.IsInfinity(Lo) ? lo + Step : hi - Step;
        }

        /// <summary>A spot of a salvo fired with the correction <paramref name="at"/> taken in at a time: a straddle sets
        /// the bracket to the pattern round it; an over (short) sets the high (low) bound, dropping the other if it's
        /// against it, and pushes the other out to the floor's width.</summary>
        public void Spot(Sign sign, double at, long ms, double driftMs, double patternM, double floorM)
        {
            double lo = LoAt(ms, driftMs), hi = HiAt(ms, driftMs);
            switch (sign)
            {
                case Sign.Straddle:
                    double half = 0.5 * Math.Max(patternM, floorM);
                    (lo, hi) = (at - half, at + half);
                    break;
                case Sign.Over:
                    if (at <= lo)
                        lo = double.NegativeInfinity;
                    hi = Math.Min(hi, at);
                    lo = Math.Min(lo, hi - floorM);
                    break;
                default:
                    if (at >= hi)
                        hi = double.PositiveInfinity;
                    lo = Math.Max(lo, at);
                    hi = Math.Max(hi, lo + floorM);
                    break;
            }
            (Lo, Hi, LoMs, HiMs) = (lo, hi, ms, ms);
        }

        /// <summary>The bracket moved by dr, m (the solution it's a correction to moved the other way).</summary>
        public void Shift(double dr) => (Lo, Hi) = (Lo + dr, Hi + dr);

        /// <summary>The bracket opened round its middle to at least a width (the solution disturbed: own ship turning, the
        /// target seen to turn).</summary>
        public void Widen(long ms, double driftMs, double widthM)
        {
            if (!Closed)
                return;
            double lo = LoAt(ms, driftMs), hi = HiAt(ms, driftMs), mid = 0.5 * (lo + hi), half = 0.5 * Math.Max(hi - lo, widthM);
            (Lo, Hi, LoMs, HiMs) = (mid - half, mid + half, ms, ms);
        }

        /// <summary>One side dropped (fire for effect gone wrong one way): the spots said the target is beyond (short) or
        /// short of (over) the bracket, so it opens that way from the bound they set.</summary>
        public void OpenToward(Sign side, long ms, double driftMs)
        {
            double lo = LoAt(ms, driftMs), hi = HiAt(ms, driftMs);
            (Lo, Hi, LoMs, HiMs) = side == Sign.Short ? (lo, double.PositiveInfinity, ms, ms) : (double.NegativeInfinity, hi, ms, ms);
        }
    }
}
