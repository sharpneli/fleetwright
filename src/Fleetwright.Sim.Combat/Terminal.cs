using Fleetwright.Shipgen;
using Fleetwright.Sim.Core;
using Fleetwright.Sim.Ships;

namespace Fleetwright.Sim.Combat;

/// <summary>What a hit came to: the outcome class, the explanation the damage report shows (08-gunfire-effects.md §3.3).</summary>
public enum Outcome : byte
{
    /// <summary>Stopped by a plate it couldn't get through.</summary>
    Defeated,
    /// <summary>Glanced off a plate it met too obliquely to get through.</summary>
    Ricochet,
    /// <summary>Went through without its fuze arming: two holes, and what was on its line of flight.</summary>
    PassedThrough,
    /// <summary>Armed and burst inside, after its fuze's delay (or where a plate stopped it).</summary>
    BurstInside,
    /// <summary>Burst while getting through a plate.</summary>
    BurstInPlate,
    /// <summary>Armed and burst inside, low-order.</summary>
    LowOrder,
    /// <summary>A nose fuze burst on a plate too thick to punch through.</summary>
    BurstOutside,
    /// <summary>Armed, but didn't burst.</summary>
    Dud,
    /// <summary>Holed the plate and broke up: shattered on a face-hardened plate, or its body gave way.</summary>
    BrokeUp,
    /// <summary>Armed, but out of the far side before its delay ran out.</summary>
    BurstBeyond,
    /// <summary>An underwater charge (a torpedo's) went off against the hull, and got inside it (4.6).</summary>
    Exploded,
    /// <summary>An underwater charge went off against the hull, and the torpedo protection held it.</summary>
    Held,
}

/// <summary>Where a burst was, as the effects need it: none, outside a plate, in it, or inside.</summary>
public enum BurstKind : byte
{
    None,
    Outside,
    InPlate,
    Inside,
}

/// <summary>The terminal model's answer for one hit: the outcome, the burst (where along the path, its kind and its
/// force as a share of one inside), where the shell's body stopped (the line of flight's end: its burst, the plate
/// that stopped it, or where it left the ship), the crossing that decided it (the plate that stopped, armed or burst
/// it; -1 for none), whether it struck under water, and its speed there; and what the plates did to the shell on the
/// way (P2): its cap knocked off, shattered on a face-hardened plate (its nose bare there, or the plate past its
/// body's limits: whether it then got through or not), or broken getting through just over a plate's limit.</summary>
public struct TerminalResult
{
    public Outcome Outcome;
    public BurstKind Burst;
    public double BurstT, StopT, Force;
    public int Plate;
    public bool Underwater;
    public double StrikeMs;
    public bool Decapped, Shattered, BrokeNearLimit;

    public readonly bool Bursts => Burst != BurstKind.None;
}

/// <summary>TERMINAL (docs/battle/fighting.md 4.3a): a port of research/simulation/damage-research/gunfire_ref.py
/// <c>resolve()</c>, walked along the hit's own <see cref="HitPath"/> instead of the reference's zones. Plate by plate,
/// each at its obliquity:
/// <list type="bullet">
/// <item><b>penetration:</b> Thompson's limit velocity (<see cref="LimitMs(in ShellTypeRow, double, double)"/>, the
/// same for belts and decks) of the plate's thickness times its material's q, drawn within
/// <see cref="SimSettings.PenSpread"/>, and for a common shell over <see cref="SimSettings.CommonPenShare"/>: through at
/// or above it, slowed by Lambert-Jonas, √(v² − V_L²) (<see cref="Residual"/>: the plate costs the energy that just
/// gets through it);</item>
/// <item><b>the shell's state</b> (docs/battle/shells-armour-damage.md P2): a cap is knocked off by any face-hardened
/// plate, or a homogeneous one over its threshold (<see cref="Decaps"/>), and its mass goes with it; within the gap
/// behind (<see cref="CapGapCalibres"/>) the nose still meets the next plate as if capped. A face-hardened plate
/// shatters a bare nose (uncapped, decapped, a soft cap past 15-20°, the cap head past 45°: <see cref="BareShare"/>) or
/// a body past its proof (<see cref="ShatterChance"/>): the plate holds as if <see cref="ShatterBonus"/> thicker, and a
/// shell that still gets through is broken up. The body's quality divides a face-hardened plate's thickness. Just over a
/// plate's limit, a body may break getting through (<see cref="NearLimitBreak"/>, most at 45°);</item>
/// <item><b>a nose fuze</b> bursts on the first plate: just inside it if it's under <see cref="SimSettings.HePenCalibres"/>
/// (times √cos ob), outside if not;</item>
/// <item><b>a base fuze</b> arms on a plate over Okun's threshold (<see cref="SimSettings.FuzeArmCalibres"/> at normal
/// impact, less at obliquity), or on the machinery of a space it crosses (<see cref="SimSettings.MachineryArmChance"/>).
/// Armed, the quality row decides: a burst after the delay distance (the delay times the residual velocity), in the
/// plate, low-order, or a dud. A plate it can't get through stops it: about to burst, it bursts there; not
/// armed, it's defeated, or glances off past <see cref="SimSettings.RicochetFromDeg"/>, bursting outside or not as the
/// reference rolls; out of the far side before its delay runs out, it bursts beyond;</item>
/// <item><b>under water:</b> a shell that met the sea before the hull strikes at the speed its run there left it
/// (<see cref="WaterRun"/>, <see cref="Hit.WaterRunM"/>); a fuze started on the water arms it at the hull, with what's
/// left of its delay (<see cref="SimSettings.FuzeStartsOnWater"/>).</item>
/// </list>
/// Pure and allocation-free; the draws come from the stream it's given.</summary>
public static class Terminal
{
    /// <summary>The hit's path through its class (traced from just outside the hit point along the shell's flight) to
    /// its outcome.</summary>
    public static TerminalResult Resolve(ShipClass c, in Hit hit, HitPath path, SimSettings s, ref RandomStream r)
    {
        var shell = hit.Shell;
        var res = new TerminalResult { Outcome = Outcome.PassedThrough, BurstT = double.NaN, StopT = path.Length, Plate = -1 };
        double calMm = shell.CalibreMm, v = hit.SpeedMs;
        bool nose = shell.Fuze == Fuze.Nose, inside = false, armed = false;
        double burstT = double.NaN, force = 1;
        var xs = path.Crossings;

        // under water first: the shell met the sea before the ship, and ran on through it to the hull
        double decayM = WaterRun.DecayM(shell, s);
        res.Underwater = hit.WaterRunM > 0;
        v = WaterRun.Speed(v, hit.WaterRunM, decayM);
        res.StrikeMs = v;
        bool waterFuze = res.Underwater && WaterRun.FuzeStarts(shell, s);

        // the cap: on (a diving shell's cap head comes off on the water), or lost at a plate, with the gap behind it in
        // which the nose still meets a plate as if capped
        var cap = res.Underwater && shell.Cap == Cap.BreakAway ? Cap.None : shell.Cap;
        double massKg = cap == shell.Cap ? shell.MassKg : shell.MassKg * (1 - shell.CapShare);
        double capOffT = double.NaN, capGapM = 0;

        for (int i = 0; i < xs.Length; i++)
        {
            ref readonly var x = ref xs[i];
            if (x.T >= burstT)
                return Burst(ref res, burstT, force, res.Outcome, BurstKind.Inside);
            switch (x.Kind)
            {
                case CrossingKind.HullIn or CrossingKind.PartIn:
                    inside = true;
                    if (waterFuze && !armed)
                    {
                        // started on the water: armed at the hull, with what's left of its delay
                        armed = true;
                        double left = Math.Max(shell.FuzeDelayS - WaterRun.TimeS(hit.SpeedMs, hit.WaterRunM, decayM), 0);
                        if (Arm(ref res, ref r, s, shell with { FuzeDelayS = left }, x.T, v, i, ref burstT, ref force))
                            return res;
                    }
                    break;
                case CrossingKind.WaterIn when inside:
                    return Gone(ref res, armed, x.T);   // out of the ship into the sea: the shell's gone
                case CrossingKind.CellIn when !armed && !nose && IsMachinery(c, x.Index) && r.NextDouble() < s.MachineryArmChance:
                    armed = true;
                    if (Arm(ref res, ref r, s, shell, x.T, v, i, ref burstT, ref force))
                        return res;
                    break;
            }
            // a nose fuze fires on anything solid it enters (a funnel, an open mount, a rudder), plated or not
            bool solid = nose && x.Kind is CrossingKind.HullIn or CrossingKind.PartIn;
            if (!solid && (x.Plate == PlateKind.None || !(x.Mm > 0)))
                continue;

            double t = x.Mm > 0 ? x.Mm * c.MaterialQ(x.Material, x.Mm, calMm) : 0;
            double ob = Obliquity(hit.Direction, x.Normal);
            if (nose)
            {
                double punch = s.HePenCalibres * calMm * Math.Sqrt(Math.Max(Math.Cos(double.DegreesToRadians(ob)), 1e-3));
                res.Plate = i;
                return t <= punch
                    ? Burst(ref res, x.T + 0.5, 1, Outcome.BurstInside, BurstKind.Inside)
                    : Burst(ref res, Math.Max(x.T - 0.3, 0), s.BurstOutsideK, Outcome.BurstOutside, BurstKind.Outside);
            }

            // the plate as this shell meets it: its resistance drawn, and a common shell's weaker body; on face-hardened
            // plate the body's quality, and the face shattering a bare nose or a body past its limits
            double te = t * r.NextDouble(1 - s.PenSpread, 1 + s.PenSpread) / (shell.Kind == ShellKind.Common ? s.CommonPenShare : 1);
            double td = x.Mm / calMm;
            bool hard = x.Material >= 0 && c.MaterialOf(x.Material).FaceHardened, shatter = false;
            if (hard)
            {
                double p = ShatterChance(shell, c.MaterialOf(x.Material).FaceShatters, td, ob, x.T - capOffT < capGapM ? 0 : BareShare(cap, ob), s);
                shatter = p >= 1 || p > 0 && r.NextDouble() < p;
                te = FaceHardMm(te, shell, ob, shatter, s);
            }
            double vl = LimitMs(te, calMm, massKg, ob);
            if (cap != Cap.None && (hard || Decaps(cap, td, ob, ref r)))
            {
                // the cap's knocked off, whether the plate holds or not
                (capOffT, capGapM, cap) = (x.T, CapGapCalibres(cap, ob) * calMm / 1000, Cap.None);
                massKg *= 1 - shell.CapShare;
                res.Decapped = true;
            }
            res.Shattered |= shatter;
            if (v < vl)
            {
                // stopped here: about to burst, it bursts against the plate, this side of it; a dud lies there; never
                // armed, it's defeated or glances off
                res.Plate = i;
                res.StopT = x.T;
                if (!double.IsNaN(burstT))
                    return Burst(ref res, Math.Max(x.T - 0.3, 0), force, res.Outcome, BurstKind.Inside);
                if (armed)
                    return res;
                bool ric = ob >= s.RicochetFromDeg && r.NextDouble() < Math.Min(1, (ob - s.RicochetFromDeg) / s.RicochetSpanDeg);
                res.Outcome = ric ? Outcome.Ricochet : Outcome.Defeated;
                if (r.NextDouble() < (ric ? 0.35 : 0.5))
                    (res.Burst, res.BurstT, res.Force) = (BurstKind.Outside, Math.Max(x.T - 0.3, 0), s.BurstOutsideK);
                return res;
            }
            // through: shattered, it's in pieces; just over the limit, the body may give way
            bool near = !shatter && v < s.NearLimitBand * vl && td >= s.BreakFromTD && r.NextDouble() < NearLimitBreak(ob, s);
            v = Residual(v, vl);
            if (shatter || near)
            {
                res.BrokeNearLimit = near;
                (res.Plate, res.Outcome, res.Burst, res.BurstT, res.StopT) = (i, Outcome.BrokeUp, BurstKind.None, double.NaN, x.T + 1);
                return res;
            }
            if (!armed && t >= FuzeArmMm(calMm, ob, s))
            {
                armed = true;
                if (Arm(ref res, ref r, s, shell, x.T, v, i, ref burstT, ref force))
                    return res;
            }
        }
        return Gone(ref res, armed, path.Length);
    }

    /// <summary>Armed at T with velocity v: the quality row's roll (the fuze's and the filler's failures; the body's are
    /// the plates'). A burst in the plate decides now (true); a burst after the delay (or low-order) sets where; a dud
    /// goes on.</summary>
    static bool Arm(ref TerminalResult res, ref RandomStream r, SimSettings s, in ShellTypeRow shell, double t, double v, int i, ref double burstT,
        ref double force)
    {
        var row = s.ShellOutcomes[Math.Clamp((int)shell.Quality, 0, s.ShellOutcomes.Length - 1)];
        double x = r.NextDouble();
        res.Plate = i;
        if (x < row.Burst + row.LowOrder)
        {
            bool low = x >= row.Burst;
            (force, res.Outcome) = low ? (s.LowOrderK, Outcome.LowOrder) : (1, Outcome.BurstInside);
            burstT = t + shell.FuzeDelayS * v * r.NextDouble(s.DelayLow, s.DelayHigh);
            return false;
        }
        x -= row.Burst + row.LowOrder;
        if (x < row.InPlate)
        {
            Burst(ref res, t + 0.01, s.BurstInPlateK, Outcome.BurstInPlate, BurstKind.InPlate);
            return true;
        }
        res.Outcome = Outcome.Dud;
        return false;
    }

    /// <summary>The walk's end with nothing more in the way: about to burst, beyond the far side; a dud, as it is;
    /// never armed, through.</summary>
    static TerminalResult Gone(ref TerminalResult res, bool armed, double t)
    {
        res.StopT = Math.Min(res.StopT, t);
        if (res.Outcome != Outcome.Dud)
            res.Outcome = armed ? Outcome.BurstBeyond : Outcome.PassedThrough;
        return res;
    }

    static TerminalResult Burst(ref TerminalResult res, double t, double force, Outcome o, BurstKind kind)
    {
        (res.Outcome, res.Burst, res.BurstT, res.StopT, res.Force) = (o, kind, t, t, force);
        return res;
    }

    // ------------------------------------------------------------------ the physics

    const double In = 25.4, Lb = 0.45359237, Fps = 0.3048;

    /// <summary>Thompson's limit velocity, m/s (the US Navy ballistic limit, F_STD of 1930-31: armor/01, armor_pen_ref.py
    /// <c>thompson_vl</c>): the speed at which a shell of a calibre and mass just gets through tMm of the formula's plate
    /// (STS, q 1) at an obliquity. V_L = 0.024056·D·√(T/W)·F/cos Ob with F = 6·(T/D − 0.45)·(Ob² + 2000) + 40000 (inches,
    /// pounds, ft/s, Ob in degrees). F is fitted to 75°: past it F takes 75° and the 1/cos the true angle.</summary>
    public static double LimitMs(double tMm, double calibreMm, double massKg, double obliquityDeg)
    {
        double d = calibreMm / In, t = Math.Max(tMm, 0) / In, w = massKg / Lb, o = Math.Min(obliquityDeg, 75);
        double f = 6 * (t / d - 0.45) * (o * o + 2000) + 40000;
        return 0.024056 * d * Math.Sqrt(t / w) * f / Cos(obliquityDeg) * Fps;
    }

    public static double LimitMs(in ShellTypeRow shell, double tMm, double obliquityDeg) =>
        LimitMs(tMm, shell.CalibreMm, shell.MassKg, obliquityDeg);

    /// <summary>The plate a shell at v m/s just gets through, mm of the formula's plate: <see cref="LimitMs(double,
    /// double, double, double)"/> inverted in closed form. With u = √T, V_L is the cubic a·u³ + b·u = c, a =
    /// 6·(Ob² + 2000)/D, b = 34600 − 2.7·Ob², c = V·cos Ob·√W/(0.024056·D); a and b are positive at every Ob F takes, so
    /// there is one real root, Cardano's in its hyperbolic form u = 2√(p/3)·sinh(⅓·asinh((3q/2p)·√(3/p))), p = b/a, q =
    /// c/a (the two-cube-root form cancels badly, the linear term dominating). For readouts (the matchup, benches); a
    /// hit never needs it.</summary>
    public static double PenMm(double v, double calibreMm, double massKg, double obliquityDeg)
    {
        double d = calibreMm / In, w = massKg / Lb, o = Math.Min(obliquityDeg, 75);
        double a = 6 * (o * o + 2000) / d, b = 34600 - 2.7 * o * o;
        double c = Math.Max(v, 0) / Fps * Cos(obliquityDeg) * Math.Sqrt(w) / (0.024056 * d);
        double p = b / a, q = c / a;
        double u = 2 * Math.Sqrt(p / 3) * Math.Sinh(Math.Asinh(1.5 * q / p * Math.Sqrt(3 / p)) / 3);
        return u * u * In;
    }

    public static double PenMm(in ShellTypeRow shell, double v, double obliquityDeg) => PenMm(v, shell.CalibreMm, shell.MassKg, obliquityDeg);

    /// <summary>The speed left after a plate of limit vl, Lambert-Jonas with a = 1, p = 2: ½mVr² = ½mV² − ½mV_L², the plate
    /// costing the energy that just gets through it. Thompson's F rising with T/D makes that energy grow faster than the
    /// thickness, so plates stacked hold less than one of their sum (2+1+2″ STS at 68° against a 14″: 0.75 of a solid
    /// 5″, 0.74 measured; armour-and-damage.md §3.8).</summary>
    public static double Residual(double v, double vl) => v > vl ? Math.Sqrt(v * v - vl * vl) : 0;

    static double Cos(double obliquityDeg) => Math.Max(Math.Cos(double.DegreesToRadians(obliquityDeg)), 1e-6);

    /// <summary>Okun's base-fuze arming plate, mm (Mk 21, a single plate): less at obliquity, flat past 61°.</summary>
    public static double FuzeArmMm(double calibreMm, double obliquityDeg, SimSettings s)
    {
        double r = double.DegreesToRadians(Math.Min(obliquityDeg, 61));
        return s.FuzeArmCalibres * calibreMm * ((1 + Math.Cos(2 * r)) / 2 + 0.4537 * Math.Pow(Math.Sin(r), 5.7019));
    }

    // ------------------------------------------------------------------ caps, shatter and break-up (P2)
    // research/simulation/armor/00-overview.md §5.2-5.3, 02 §3, 05 §4d and §4g; armor_pen_ref.py resolve_plate

    /// <summary>Whether a homogeneous plate of td calibres at an obliquity knocks a cap off (Okun's tech-085, T/D): a soft
    /// or Type 1 hard cap (and the cap head) 0.105-0.12 under 15° (an even chance by where it falls between), 0.0805 from
    /// 15°; a Type 2 hard cap 0.155-0.17 under 45°, 0.12-0.14 from it. Face-hardened plate always does.</summary>
    public static bool Decaps(Cap cap, double td, double obliquityDeg, ref RandomStream r)
    {
        var (lo, hi) = cap == Cap.Hard2
            ? obliquityDeg < 45 ? (0.155, 0.17) : (0.12, 0.14)
            : obliquityDeg < 15 ? (0.105, 0.12) : (0.0805, 0.0805);
        return td >= hi || td >= lo && r.NextDouble() < (td - lo) / (hi - lo);
    }

    /// <summary>The gap behind a plate that knocked a cap off, calibres, before the bare nose meets the next plate bare
    /// (tech-085): 2.3 at normal (1.6-3 by the plate) down to 1 from 30°; a soft cap's 0.5.</summary>
    public static double CapGapCalibres(Cap cap, double obliquityDeg) =>
        cap == Cap.Soft ? 0.5 : Math.Max(1, 2.3 - 1.3 * Math.Min(obliquityDeg, 30) / 30);

    /// <summary>The share of hits at an obliquity a cap leaves the nose bare: all with none; a soft cap tears off from 15°
    /// and surely by 20° (between, by the angle); the cap head gives way past 45°; a hard cap holds while it's on.</summary>
    public static double BareShare(Cap cap, double obliquityDeg) => cap switch
    {
        Cap.None => 1,
        Cap.Soft => Math.Clamp((obliquityDeg - 15) / 5, 0, 1),
        Cap.BreakAway => obliquityDeg > 45 ? 1 : 0,
        _ => 0,
    };

    /// <summary>The chance a face-hardened plate of td calibres shatters the shell (the terminal draws it, the matchup takes
    /// it as a share): a bare nose (<paramref name="bare"/>, the share of hits it meets the plate bare) on a face that
    /// shatters steel shot (<paramref name="faceShatters"/>, <see cref="MaterialRow.FaceShatters"/>), from <see cref="SimSettings.BreakFromTD"/>; or a body past its proof (the obliquity it was made
    /// to survive on plate of its T/D or thicker), surely <see cref="SimSettings.BreakSpanDeg"/> past it (RN 1914 APC,
    /// proofed at 15° on 0.4 calibres, broke up at 15-20°; Greenboy degrades past 20°).</summary>
    public static double ShatterChance(in ShellTypeRow shell, bool faceShatters, double td, double obliquityDeg, double bare, SimSettings s)
    {
        double body = td >= shell.BreakTD ? Math.Clamp((obliquityDeg - shell.BreakObDeg) / s.BreakSpanDeg, 0, 1) : 0;
        double nose = faceShatters && td >= s.BreakFromTD ? bare : 0;
        return 1 - (1 - body) * (1 - nose);
    }

    /// <summary>A face-hardened plate's thickness (mm, rated) as this shell meets it: over the body's quality, and times
    /// <see cref="ShatterBonus"/> if it shatters the shell (<see cref="ShatterChance"/>).</summary>
    public static double FaceHardMm(double mm, in ShellTypeRow shell, double obliquityDeg, bool shatter, SimSettings s) =>
        mm * (shatter ? ShatterBonus(obliquityDeg, s) : 1) / shell.BodyQuality;

    /// <summary>What a shattering shell makes of a face-hardened plate: <see cref="SimSettings.ShatterBonus"/> times its
    /// thickness at normal, fading to none at <see cref="SimSettings.ShatterToDeg"/> (Okun; the early trials' ×1.4).</summary>
    public static double ShatterBonus(double obliquityDeg, SimSettings s) =>
        obliquityDeg < s.ShatterToDeg ? 1 + (s.ShatterBonus - 1) * (1 - obliquityDeg / s.ShatterToDeg) : 1;

    /// <summary>The chance a body breaks getting through a plate just over its limit (under
    /// <see cref="SimSettings.NearLimitBand"/> times V_L), by the obliquity: <see cref="SimSettings.NearLimitBreakFloor"/>
    /// under 30° and past 60°, rising to <see cref="SimSettings.NearLimitBreakPeak"/> at 45° (the middle body's bending:
    /// armor/05 §1.2 and its plate-sequence outline).</summary>
    public static double NearLimitBreak(double obliquityDeg, SimSettings s) =>
        s.NearLimitBreakFloor + (s.NearLimitBreakPeak - s.NearLimitBreakFloor) * Math.Max(0, 1 - Math.Abs(obliquityDeg - 45) / 15);

    /// <summary>The angle between the flight and a plate's normal, degrees (0 square on; no normal: square on).</summary>
    public static double Obliquity(P3 d, P3 n)
    {
        if (double.IsNaN(n.X))
            return 0;
        double c = Math.Abs(d.X * n.X + d.Y * n.Y + d.Z * n.Z);
        return double.RadiansToDegrees(Math.Acos(Math.Min(c, 1)));
    }

    /// <summary>Whether a cell holds machinery: a boiler or engine room.</summary>
    public static bool IsMachinery(ShipClass c, int cell) => HasRoom(c, cell, RoomKind.BoilerRoom) || HasRoom(c, cell, RoomKind.EngineRoom);

    /// <summary>Whether a room of a kind is in a cell.</summary>
    public static bool HasRoom(ShipClass c, int cell, RoomKind kind)
    {
        ref readonly var row = ref c.Cells[cell];
        if (row.Room >= 0 && c.Rooms[row.Room].Kind == kind)
            return true;
        foreach (int room in c.Links(row.Also))
            if (c.Rooms[room].Kind == kind)
                return true;
        return false;
    }
}
