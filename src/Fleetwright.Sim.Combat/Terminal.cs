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
    /// <summary>Holed the plate and broke up.</summary>
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
/// it; -1 for none), whether it struck under water, and its speed there.</summary>
public struct TerminalResult
{
    public Outcome Outcome;
    public BurstKind Burst;
    public double BurstT, StopT, Force;
    public int Plate;
    public bool Underwater;
    public double StrikeMs;

    public readonly bool Bursts => Burst != BurstKind.None;
}

/// <summary>TERMINAL (docs/battle/fighting.md 4.3a): a port of research/simulation/damage-research/gunfire_ref.py
/// <c>resolve()</c>, walked along the hit's own <see cref="HitPath"/> instead of the reference's zones. Plate by plate,
/// each at its obliquity:
/// <list type="bullet">
/// <item><b>penetration:</b> the reference's Garzke and Dulin fit (normal impact) with its obliquity terms for vertical
/// and horizontal plate, against the plate's thickness times its material's quality, drawn within
/// <see cref="SimSettings.PenSpread"/>; a common shell at <see cref="SimSettings.CommonPenShare"/>; the shell slows by
/// the reference's residual rule, v·√(1 - t/T);</item>
/// <item><b>a nose fuze</b> bursts on the first plate: just inside it if it's under <see cref="SimSettings.HePenCalibres"/>
/// (times √cos ob), outside if not;</item>
/// <item><b>a base fuze</b> arms on a plate over Okun's threshold (<see cref="SimSettings.FuzeArmCalibres"/> at normal
/// impact, less at obliquity), or on the machinery of a space it crosses (<see cref="SimSettings.MachineryArmChance"/>).
/// Armed, the quality row decides: a burst after the delay distance (the delay times the residual velocity), in the
/// plate, low-order, a dud, or broken up. A plate it can't get through stops it: about to burst, it bursts there; not
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
    /// its outcome. quality: each of the class's materials' resistance (<see cref="Qualities"/>).</summary>
    public static TerminalResult Resolve(ShipClass c, in Hit hit, HitPath path, ReadOnlySpan<double> quality, SimSettings s, ref RandomStream r)
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

            double t = x.Mm > 0 ? x.Mm * Quality(quality, x.Material) : 0;
            double ob = Obliquity(hit.Direction, x.Normal);
            bool deck = !double.IsNaN(x.Normal.Z) && Math.Abs(x.Normal.Z) > 0.7;
            if (nose)
            {
                double punch = s.HePenCalibres * calMm * Math.Sqrt(Math.Max(Math.Cos(double.DegreesToRadians(ob)), 1e-3));
                res.Plate = i;
                return t <= punch
                    ? Burst(ref res, x.T + 0.5, 1, Outcome.BurstInside, BurstKind.Inside)
                    : Burst(ref res, Math.Max(x.T - 0.3, 0), s.BurstOutsideK, Outcome.BurstOutside, BurstKind.Outside);
            }

            double cap = PenMm(shell, v, ob, deck) * (shell.Kind == ShellKind.Common ? s.CommonPenShare : 1);
            if (t > cap * r.NextDouble(1 - s.PenSpread, 1 + s.PenSpread))
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
            v *= Math.Sqrt(Math.Max(0, 1 - t / cap));
            if (!armed && t >= FuzeArmMm(calMm, ob, s))
            {
                armed = true;
                if (Arm(ref res, ref r, s, shell, x.T, v, i, ref burstT, ref force))
                    return res;
            }
        }
        return Gone(ref res, armed, path.Length);
    }

    /// <summary>Armed at T with velocity v: the quality row's roll. A burst in the plate or a break-up decides now
    /// (true); a burst after the delay (or low-order) sets where; a dud goes on.</summary>
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
        x -= row.InPlate;
        if (x < row.Dud)
        {
            res.Outcome = Outcome.Dud;
            return false;
        }
        (res.Outcome, res.StopT) = (Outcome.BrokeUp, t + 1);
        return true;
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

    /// <summary>The reference's capability, mm of Class A-equivalent plate: Garzke and Dulin at normal impact
    /// (05-mechanics-and-games.md §A3), times 0.967·cos^1.59 for a vertical plate and 0.82·cos^1.125 for a deck
    /// (fitted to the navweaps 16"/50 Mk 8 tables).</summary>
    public static double PenMm(in ShellTypeRow shell, double v, double obliquityDeg, bool deck)
    {
        double w = shell.MassKg / 0.4536, dIn = shell.CalibreMm / 25.4, vf = Math.Max(v, 0) / 0.3048;
        double normal = 25.4 * 0.0004689 * Math.Pow(w, 0.55) * Math.Pow(dIn, -0.65) * Math.Pow(vf, 1.1);
        double c = Math.Max(1e-3, Math.Cos(double.DegreesToRadians(obliquityDeg)));
        return deck ? normal * 0.82 * Math.Pow(c, 1.125) : normal * 0.967 * Math.Pow(c, 1.59);
    }

    /// <summary>Okun's base-fuze arming plate, mm (Mk 21, a single plate): less at obliquity, flat past 61°.</summary>
    public static double FuzeArmMm(double calibreMm, double obliquityDeg, SimSettings s)
    {
        double r = double.DegreesToRadians(Math.Min(obliquityDeg, 61));
        return s.FuzeArmCalibres * calibreMm * ((1 + Math.Cos(2 * r)) / 2 + 0.4537 * Math.Pow(Math.Sin(r), 5.7019));
    }

    /// <summary>The angle between the flight and a plate's normal, degrees (0 square on; no normal: square on).</summary>
    public static double Obliquity(P3 d, P3 n)
    {
        if (double.IsNaN(n.X))
            return 0;
        double c = Math.Abs(d.X * n.X + d.Y * n.Y + d.Z * n.Z);
        return double.RadiansToDegrees(Math.Acos(Math.Min(c, 1)));
    }

    static double Quality(ReadOnlySpan<double> quality, int material) => material >= 0 && material < quality.Length ? quality[material] : 1;

    /// <summary>Each of a class's materials' resistance from the settings (by name; unlisted: the settings' other).</summary>
    public static double[] Qualities(ShipClass c, SimSettings s)
    {
        var q = new double[c.MaterialCount];
        for (int i = 0; i < q.Length; i++)
            q[i] = s.ArmourQuality.TryGetValue(c.Material(i)!, out double v) ? v : s.ArmourQualityOther;
        return q;
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
