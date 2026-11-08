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

    /// <summary>The share of the time an instrument's man holds her in its field as his ship rolls and pitches
    /// (docs/battle/error-sources.md ES5): the line of sight moves with the ship in elevation by her roll's share across it
    /// and her pitch's along it (level: roll·sin β + pitch·cos β, β her bearing off the bow; her yaw is the trainer's, as
    /// a director's), and he follows it by hand a lag behind (<paramref name="followS"/>), so it wanders off her by about
    /// that lag times the motion's rate (a first-order follower's residual where the motion is slow against it, 1σ, the
    /// roll's and pitch's rates 1σ, rad/s); he holds her while it's within half the field, an apparent field over the
    /// magnification (<paramref name="apparentFieldDeg"/>: the Bismarck trials' 1.5° at ×40, where a ±1° oscillation
    /// could lose her: stabilisation-tech.md Q4). A gyro-stabilised mounting takes the motion out (Pollen's Argo, a
    /// stabilised sight); an instrument without magnification (an eye) has no field to lose her from. Scaled by the
    /// ladder's rung (0 a still ship).</summary>
    public static double Held(in OpticsRow o, double rollRateRadS, double pitchRateRadS, double relativeDeg, double followS,
        double apparentFieldDeg, double scale)
    {
        if (o.Gyro || !(o.Magnification > 0) || !(scale > 0))
            return 1;
        var (sb, cb) = Math.SinCos(double.DegreesToRadians(relativeDeg));
        double rate = Math.Sqrt(Math.Pow(rollRateRadS * sb, 2) + Math.Pow(pitchRateRadS * cb, 2));
        double sr = scale * followS * rate;
        if (!(sr > 0))
            return 1;
        double half = 0.5 * double.DegreesToRadians(apparentFieldDeg) / o.Magnification;
        return SmokeModel.Erf(half / (sr * Math.Sqrt(2)));
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

    /// <summary>A shell's own dispersion about its salvo's centre, 1 sigma in range and across, metres, from its gun's
    /// pattern (its 100% spread, % of the range) as quoted for a salvo of <paramref name="patternGuns"/> shells
    /// (<see cref="SimSettings.PatternReferenceGuns"/>): that pattern is ratio(n)·D, D = 0.798σ (USNI 1917; Gun.sigma_D),
    /// so a full salvo of our shells matches the pattern it's quoted at within some 7% (the ratio sits a little above the
    /// normal's: D kept, the core of the fall). The n is the quoted salvo's, never the guns firing now: a shell's scatter
    /// is its gun's, and a salvo's pattern grows with the shells in it (the USN's 1944 expected patterns, 1.0% of the
    /// range for 3 guns, 1.9% for 9: the same σ a shell; fire-control-research/dispersion-and-hits.md §1.2, §1.4). The
    /// old unsourced 0.85 on σ is gone (2026-11-08, §1.4).</summary>
    public static (double Range, double Across) Dispersion(double patternPct, double rangeM, double patternGuns, double deflectionRatio)
    {
        double ratio = Interp(patternGuns, PatternGuns, PatternRatio);
        double sr = patternPct / 100 * rangeM / ratio / 0.798;
        return (sr, deflectionRatio * sr);
    }

    /// <summary>A salvo's own dispersion by its flight (docs/battle/error-sources.md ES7), 1σ in range and across, m: each
    /// shell's scatter is its muzzle velocity's and its jump's (the angle it leaves at off the gun's laid elevation), each
    /// through the flight it's fired on: the range a 1% faster shell adds (<paramref name="perMvPct"/>, m) and a radian
    /// higher (<paramref name="perRad"/>, m). A flat trajectory turns a milliradian into far more range than a steep one
    /// (in vacuum the range per radian over the range is 2 cot 2θ: some 1.6% a mil at 3.5°, 0.3% at 15°), so close in the
    /// pattern is a larger share of the range than at the battle ranges it's quoted at. The design's pattern
    /// (<see cref="Dispersion"/>) holds at a reference flight (<paramref name="refRangeM"/>, its sensitivities), its share there
    /// not the jump's (<paramref name="jumpMil"/>, 1σ) the velocity's; across the line, as before (an angle, the trainer's).</summary>
    public static (double Range, double Across) Dispersion(double patternPct, double rangeM, double patternGuns, double deflectionRatio,
        double perMvPct, double perRad, double refRangeM, double refPerMvPct, double refPerRad, double jumpMil)
    {
        var (sr, sd) = Dispersion(patternPct, rangeM, patternGuns, deflectionRatio);
        if (!(refRangeM > 0) || !(refPerMvPct > 0) || !(refPerRad > 0) || !(perRad > 0))
            return (sr, sd);
        double reference = Dispersion(patternPct, refRangeM, patternGuns, deflectionRatio).Range, jumpRad = jumpMil * Mil;
        double mv = Math.Sqrt(Math.Max(reference * reference - Math.Pow(jumpRad * refPerRad, 2), 0)) / refPerMvPct;
        return (Math.Sqrt(Math.Pow(mv * perMvPct, 2) + Math.Pow(jumpRad * perRad, 2)), sd);
    }

    /// <summary>A shell's own dispersion by its flight (ES7), from the guns' table at the elevation it's fired at (from a
    /// muzzle h0 over the sea), and the design's pattern at the settings' reference elevation and salvo
    /// (<see cref="SimSettings.PatternReferenceGuns"/>); the fixed share of the range where the guns can't reach the
    /// reference. The gunnery's, the dev's salvo's and the matchup's one rule. The sensitivities at the salvo's own flight
    /// may be given (the gunnery has them already).</summary>
    public static (double Range, double Across) Dispersion(BallisticTable table, double patternPct, double rangeM, double elevationDeg, double h0,
        SimSettings s, double perMvPct = double.NaN, double perRad = double.NaN)
    {
        double eRef = Math.Min(s.DispersionReferenceDeg, table.MaxElevationDeg - 1), guns = s.PatternReferenceGuns;
        var reference = table.Fall(eRef, h0);
        if (double.IsNaN(reference.Range) || double.IsNaN(elevationDeg))
            return Dispersion(patternPct, rangeM, guns, s.DispersionDeflectionRatio);
        if (double.IsNaN(perMvPct))
            perMvPct = PerMvPct(table, elevationDeg, h0);
        if (double.IsNaN(perRad))
            perRad = table.RangePerDeg(elevationDeg, h0) * 180 / Math.PI;
        return Dispersion(patternPct, rangeM, guns, s.DispersionDeflectionRatio, perMvPct, perRad, reference.Range, PerMvPct(table, eRef, h0),
            table.RangePerDeg(eRef, h0) * 180 / Math.PI, s.DispersionJumpMil);
    }

    /// <summary>The range a 1% faster shell adds at an elevation from a muzzle h0 over the sea, m.</summary>
    public static double PerMvPct(BallisticTable table, double elevationDeg, double h0) =>
        (table.Fall(elevationDeg, h0, 1.01).Range - table.Fall(elevationDeg, h0, 0.99).Range) / 2;

    /// <summary>A salvo's pattern over D by its shells (dispersion-and-hits.md §1.1): USNI 1917's 3 and 5-12, Jurens's 2
    /// and 4 (2.89 for USNI's 2.74, which breaks the run); one shell has no pattern.</summary>
    static readonly double[] PatternGuns = [2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12],
        PatternRatio = [1.69, 2.43, 2.89, 3.21, 3.47, 3.67, 3.85, 4.0, 4.13, 4.24, 4.34];

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
    /// deflection seen (<see cref="Ladder"/> turns it into the next salvo's correction); doubtful when no splash fell in
    /// line with her (off for line: only the line is corrected).</summary>
    public enum Sign : byte
    {
        Short,
        Over,
        Straddle,
        Doubtful,
    }

    /// <summary>The bracket a battery's spots make, kept in correction space (fire-control.md F6;
    /// fire-control-research.md §9, computing.md §2.5). Each spotted salvo says the target lies beyond the range
    /// correction it was fired with (short), short of it (over) or about on it (a straddle), so the spots bound the
    /// correction the target needs from below and above, whatever order they come in. Open on a side, the next salvo
    /// goes a step beyond the bound seen; closed, into its middle: the RN's 400, 200, 100 yd and the USN's bracket and
    /// halve are both this, and the bracket is never let narrower than a floor (the USN's "never below the pattern").
    /// It isn't hard: each bound widens with time at the computer's own doubt of the range rate (the truth drifts in
    /// range as the rate's error carries it), and a spot against a bound drops it. It's the ranging's: firing for effect,
    /// the spots walk the correction instead (GunnerySystem.Walk).</summary>
    public struct Bracket
    {
        /// <summary>The correction the target needs is over <see cref="Lo"/> and under <see cref="Hi"/>, m along the line
        /// of fire (infinite: open), as each stood when it was set.</summary>
        public double Lo, Hi;
        public long LoMs, HiMs;
        /// <summary>The step beyond a bound while the other side is open, m.</summary>
        public double Step;
        /// <summary>The correction to fire with while both sides are open, m: none, or what its battery carried from its
        /// last target (docs/battle/balance.md BL1ad).</summary>
        public double Mid;

        /// <summary>Closed about a correction, a width wide (the walk's, as a disturbance sends fire for effect back to
        /// ranging).</summary>
        public static Bracket About(double at, double widthM, long ms, double stepM) =>
            new() { Lo = at - 0.5 * widthM, Hi = at + 0.5 * widthM, LoMs = ms, HiMs = ms, Step = stepM };

        /// <summary>Open beyond a salvo's correction the way its spot said (the range lost): short, the target lies beyond
        /// it; over, short of it.</summary>
        public static Bracket Beyond(Sign side, double at, long ms, double stepM) => side == Sign.Short
            ? new() { Lo = at, Hi = double.PositiveInfinity, LoMs = ms, HiMs = ms, Step = stepM }
            : new() { Lo = double.NegativeInfinity, Hi = at, LoMs = ms, HiMs = ms, Step = stepM };

        public static Bracket Open(double stepM, double midM = 0) =>
            new() { Lo = double.NegativeInfinity, Hi = double.PositiveInfinity, Step = stepM, Mid = midM };

        public readonly bool Closed => !double.IsInfinity(Lo) && !double.IsInfinity(Hi);

        /// <summary>The low bound at a time, widened at the drift (m/s) since it was set.</summary>
        public readonly double LoAt(long ms, double driftMs) => Lo - driftMs * Math.Max(ms - LoMs, 0) / 1000.0;

        public readonly double HiAt(long ms, double driftMs) => Hi + driftMs * Math.Max(ms - HiMs, 0) / 1000.0;

        /// <summary>The correction to fire with now: its middle closed, a step beyond its bound open, before the first spot
        /// its <see cref="Mid"/>. It's a correction on the computer's solution, which the guns follow as its readings move it (the
        /// clock's range cuts: BL1z); what the spots learnt stands however long ago they learnt it (a hold that lapsed back to
        /// the bare solution put the fire back on its ballistic bias: 2026-11-02).</summary>
        public readonly double Aim(long ms, double driftMs)
        {
            double lo = LoAt(ms, driftMs), hi = HiAt(ms, driftMs);
            if (double.IsInfinity(Lo) && double.IsInfinity(Hi))
                return Mid;
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

        /// <summary>The bracket opened round its middle to at least a width (the solution disturbed: own ship turning, the
        /// target seen to turn).</summary>
        public void Widen(long ms, double driftMs, double widthM)
        {
            if (!Closed)
                return;
            double lo = LoAt(ms, driftMs), hi = HiAt(ms, driftMs), mid = 0.5 * (lo + hi), half = 0.5 * Math.Max(hi - lo, widthM);
            (Lo, Hi, LoMs, HiMs) = (mid - half, mid + half, ms, ms);
        }
    }
}
