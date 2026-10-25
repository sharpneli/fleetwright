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
    /// <summary>Holed the plate, but in pieces: between the plate's holing limit and its Navy limit, or shattered on it
    /// (P4). Its pieces go through the hole; no burst.</summary>
    Holed,
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
/// body's limits: whether it then got through or not), or broken getting through just over a plate's limit; and for a
/// shell a plate stopped within its spall band, the band's severity s, 0 to 1 (damage/14 §2.1: (u − spall_u)/(holed_u −
/// spall_u), u = V/V_L; E3's), NaN outside it.</summary>
public struct TerminalResult
{
    public Outcome Outcome;
    public BurstKind Burst;
    public double BurstT, StopT, Force;
    public int Plate;
    public bool Underwater;
    public double StrikeMs;
    public bool Decapped, Shattered, BrokeNearLimit;
    public double Spall;
    /// <summary>P5: the crossing of the plate it glanced off (-1: none), and how far the plates it got through turned it
    /// toward their normals in all, degrees.</summary>
    public int Glance;
    public double TurnDeg;
    /// <summary>E1: the shell's speed when it burst, m/s (0 against a plate that stopped it): its fragments fly with it,
    /// the side ring leaning forward, and the nose slug and base plug go on at it. E2: for a shell that holed a plate in
    /// pieces (<see cref="Outcome.Holed"/>, <see cref="Outcome.BrokeUp"/>), its pieces' speed through the hole.</summary>
    public double BurstMs;

    /// <summary>E3: the shell's speed at the plate that stopped it, or that it glanced off: what drives a spall cloud. E4:
    /// also at the plate a nose fuze burst on without holing it.</summary>
    public double PlateMs;

    /// <summary>E4: u = v/V_L at that plate (its limit as this shell met it): how near it came to getting through; NaN
    /// where no plate held it.</summary>
    public double PlateU;

    /// <summary>E2: a shell that holed a plate in pieces, which go on through the hole (<see cref="Plate"/>).</summary>
    public readonly bool Broken => Outcome is Outcome.Holed or Outcome.BrokeUp && Plate >= 0;

    public readonly bool Bursts => Burst != BurstKind.None;
}

/// <summary>TERMINAL (docs/battle/fighting.md 4.3a): a port of research/simulation/damage-research/gunfire_ref.py
/// <c>resolve()</c>, walked along the hit's own <see cref="HitPath"/> instead of the reference's zones. Plate by plate,
/// each at its obliquity:
/// <list type="bullet">
/// <item><b>penetration:</b> Thompson's limit velocity (<see cref="LimitMs(in ShellTypeRow, double, double)"/>, the
/// same for belts and decks) of the plate's thickness times its material's q, for a common shell over
/// <see cref="SimSettings.CommonPenShare"/>, times the plate's quality on this ship and this shot's scatter
/// (<see cref="PlateQuality"/>, <see cref="SimSettings.PlateShotSigma"/>; P7): through at
/// or above it, slowed by Lambert-Jonas, √(v² − V_L²) (<see cref="Residual"/>: the plate costs the energy that just
/// gets through it) and times the cosine of its turn: through, it turns toward the plate's normal by Okun's M79
/// (<see cref="Turn"/>), and the path is traced again on its new line (<see cref="PathBender"/>);</item>
/// <item><b>the glance</b> (P5): from <see cref="SimSettings.RicochetFromDeg"/>, a whole shell glances off a plate that
/// could stop its motion across the plate square on (<see cref="Glances"/>), whether it would get through or not; it
/// skids off along the plate's face at v·sin ob and the walk goes on along that line, its fuze started as on a plate
/// that held;</item>
/// <item><b>the shell's state</b> (docs/battle/shells-armour-damage.md P2): a cap is knocked off by any face-hardened
/// plate, or a homogeneous one over its threshold (<see cref="Decaps"/>), and its mass goes with it; within the gap
/// behind (<see cref="CapGapCalibres"/>) the nose still meets the next plate as if capped. A face-hardened plate
/// shatters a bare nose (uncapped, decapped, a soft cap past 15-20°, the cap head past 45°: <see cref="BareShare"/>) or
/// a body past its proof (<see cref="ShatterChance"/>): the plate holds as if <see cref="ShatterBonus"/> thicker, and a
/// shell that still gets through holes it in pieces. The body's quality divides a face-hardened plate's thickness.
/// Just over a plate's limit, a body may break getting through (<see cref="NearLimitBreak"/>, most at 45°): the
/// effective limit, above which it's through whole with its fuze working;</item>
/// <item><b>three limits</b> (P4; armor/00 §1): from the plate's holing limit (its material's <c>HoledU</c> times V_L:
/// face-hardened 0.94) up to the Navy limit V_L the plate is holed by a shell that breaks in it (<c>Holed</c>); from V_L
/// whole through; past the near-limit band, through and effective. Under the holing limit it's stopped, and from
/// <c>SpallU</c> its back throws spall (the severity on the result, E3's);</item>
/// <item><b>the filler</b> (P3; damage/12 §4) may burst in a plate the shell gets through, the more likely the nearer the
/// plate came to holding (<see cref="InPlateChance"/>), whatever the fuze;</item>
/// <item><b>a nose fuze</b> fires on the first plate: just inside it if it's under <see cref="SimSettings.HePenCalibres"/>
/// (times √cos ob), outside if not;</item>
/// <item><b>a base fuze</b> starts on a plate over its own threshold (<see cref="FuzeStartMm"/>; a two-plate fuze adds up
/// thin plates in quick succession, <see cref="SimSettings.FuzeSumCalibres"/>), on any face-hardened plate, on the
/// water (<see cref="SimSettings.FuzeStartsOnWater"/>), or on the machinery of a space it crosses
/// (<see cref="SimSettings.MachineryArmChance"/>), even on a plate that then holds. Started, the fuze may be blind (its
/// <c>FuzeBlind</c>), and the filler detonates fully by its explosive's rate with the booster, else goes low-order
/// (<see cref="SimSettings.LowOrderShare"/>) or is a dud (<see cref="Start"/>); it bursts the delay (scattered by the
/// fuze's own share) times the residual velocity on, and runs on time: a plate that slows it after brings the burst in
/// by the speed it lost (<see cref="Slowed"/>; C0 of docs/battle/shell-contents.md). A plate it can't get through stops it: running since an earlier
/// plate, it bursts against this one; started on this one, it bursts on its face; not started, it's defeated. Out of the
/// far side before its delay runs out, it bursts beyond; a shell that glanced and leaves unburst is a ricochet;</item>
/// <item><b>under water:</b> a shell that met the sea before the hull strikes at the speed its run there left it
/// (<see cref="WaterRun"/>, <see cref="Hit.WaterRunM"/>); a fuze started on the water arms it at the hull, with what's
/// left of its delay (<see cref="SimSettings.FuzeStartsOnWater"/>).</item>
/// </list>
/// Pure and allocation-free; the draws come from the stream it's given.</summary>
public static class Terminal
{
    /// <summary>The hit's path through its class (traced from just outside the hit point along the shell's flight) to
    /// its outcome; the bender bends the path where the shell turns or glances. A trace, if given, is told each plate
    /// and where the fuze started.</summary>
    public static TerminalResult Resolve(ShipClass c, in Hit hit, HitPath path, PathBender bender, in RandomStream lots, SimSettings s,
        ref RandomStream r, ShipCondition? k = null, TerminalTrace? trace = null)
    {
        var shell = hit.Shell;
        var res = new TerminalResult
        {
            Outcome = Outcome.PassedThrough, BurstT = double.NaN, StopT = path.Length, Plate = -1, Spall = double.NaN, Glance = -1,
            PlateU = double.NaN,
        };
        double calMm = shell.CalibreMm, v = hit.SpeedMs;
        // dead: a nose fuze that failed on a plate the shell punched, which goes on inert (E8)
        bool nose = shell.Fuze == Fuze.Nose, inside = false, started = false, dead = false;
        double burstT = double.NaN, force = 1;
        // a two-plate fuze's impulse so far, and where it last took a plate
        double impulse = 0, impulseT = double.NegativeInfinity;
        var xs = path.Crossings;
        // the shell's line now, and a turn waiting for the plates at its point to be crossed before the path bends
        var dir = hit.Direction;
        bool turned = false;
        double turnT = 0;

        // under water first: the shell met the sea before the ship, and ran on through it to the hull
        double decayM = WaterRun.DecayM(shell, s);
        res.Underwater = hit.WaterRunM > 0;
        v = WaterRun.Speed(v, hit.WaterRunM, decayM);
        res.StrikeMs = v;
        trace?.Begin(v);
        bool waterFuze = res.Underwater && WaterRun.FuzeStarts(shell, s);

        // the cap: on (a diving shell's cap head comes off on the water), or lost at a plate, with the gap behind it in
        // which the nose still meets a plate as if capped
        var cap = res.Underwater && shell.Cap == Cap.BreakAway ? Cap.None : shell.Cap;
        double massKg = cap == shell.Cap ? shell.MassKg : shell.MassKg * (1 - shell.CapShare);
        double capOffT = double.NaN, capGapM = 0;
        // what the shell is among (C1c): the cell and the part it's in, where it may next meet a solid, the last one struck
        var among = new Among { Cell = -1, Part = -1, FromT = double.NegativeInfinity, SkinT = double.NaN, CoreT = double.NaN };

        for (int i = 0; i < xs.Length; i++)
        {
            if (turned && xs[i].T > turnT + HitPath.SameM)
            {
                // past the plates the turn was made at: the path bends there, onto the shell's new line
                turned = false;
                bender.Bend(c, path, i, turnT, dir);
                xs = path.Crossings;
                if (i >= xs.Length)
                    break;
            }
            // in a space, before the next crossing: what it stows slows it (oil, coal, water to a level: C2-C3), and it may
            // strike a solid there (C1c), put on the path where it meets it
            if (!nose && (among.Cell >= 0 || among.Part >= 0) && xs[i].T - among.FromT > HitPath.SameM)
            {
                // in a part (a gun house, a barbette) what's in it; else the space's contents and what it stows
                bool inPart = among.Part >= 0;
                int at = inPart ? among.Part : c.Cells[among.Cell].Room;
                var rows = at < 0 ? default : inPart ? c.PartContents(at) : c.RoomContentsOf(at);
                double ts = NextSolid(rows, path, i, among.FromT, out int row, ref r);
                if (k is not null && !inPart)
                    Media(c, k, path, i, among.Cell, among.FromT, Math.Min(ts, xs[i].T), shell, s, ref v, ref burstT, trace);
                if (ts < xs[i].T && Put(c, path, i, ts, dir, rows[row], inPart ? PlateKind.PartContents : PlateKind.Contents, at, ref among, ref r))
                    xs = path.Crossings;
            }
            ref readonly var x = ref xs[i];
            if (x.T >= burstT)
                return Burst(ref res, burstT, force, res.Outcome, BurstKind.Inside, v);
            among.Enter(x);
            // into a shaft: it's solid steel, struck where the line crosses it (C4c)
            if (!nose && x.Kind == CrossingKind.PartIn && c.Components[x.Index].Kind == ComponentKind.Shaft && Shaft(c, path, i, dir, ref among, ref r))
                xs = path.Crossings;
            switch (x.Kind)
            {
                case CrossingKind.HullIn or CrossingKind.PartIn:
                    inside = true;
                    if (waterFuze && !started)
                    {
                        // started on the water: running at the hull, with what's left of its delay
                        started = true;
                        Start(ref res, ref r, shell, s, x.T, v, i, WaterRun.TimeS(hit.SpeedMs, hit.WaterRunM, decayM), ref burstT, ref force);
                        trace?.Fuzed(x.T, i, FuzeCause.Water);
                    }
                    break;
                case CrossingKind.WaterIn when inside:
                    return Gone(ref res, started, x.T);   // out of the ship into the sea: the shell's gone
            }
            // a nose fuze fires on anything solid it enters (a funnel, an open mount, a rudder), plated or not
            bool solid = nose && x.Kind is CrossingKind.HullIn or CrossingKind.PartIn;
            if (!solid && (x.Plate == PlateKind.None || !(x.Mm > 0)))
                continue;

            double t = x.Mm > 0 ? x.Mm * c.MaterialQ(x.Material, x.Mm, calMm) : 0;
            double ob = Obliquity(dir, x.Normal);
            if (nose)
            {
                bool punches = t <= NosePunchMm(calMm, ob, s);
                if (!punches && t > 0)
                    (res.PlateMs, res.PlateU) = (v, v / LimitMs(t, calMm, massKg, ob));
                if (trace != null)
                {
                    trace.Add(new PlateStep
                    {
                        Crossing = i, T = x.T, Point = x.Point, Mm = x.Mm, EffectiveMm = t, ObliquityDeg = ob, InMs = v,
                        LimitMs = t > 0 ? LimitMs(t, calMm, massKg, ob) : 0, Quality = 1, OutMs = punches ? v : 0,
                        Call = punches ? PlateCall.NoseInside : PlateCall.NoseOutside, FuzeLeftS = double.NaN,
                    });
                    if (!dead)
                        trace.Fuzed(x.T, i, FuzeCause.Nose);
                }
                if (dead)
                {
                    // inert: its thin body goes through what it punches and lies against the first plate it doesn't
                    if (punches)
                        continue;
                    (res.Plate, res.StopT) = (i, x.T);
                    return res;
                }
                var nosed = Nose(ref res, ref r, shell, s, x.T, i, punches, v);
                if (!(nosed.Outcome == Outcome.Dud && punches))
                    return nosed;
                dead = true;   // blind or a failed filler, through this plate: on, inert
                continue;
            }

            // the plate as this shell meets it: a common shell's weaker body; on face-hardened plate the body's quality,
            // and the face shattering a bare nose or a body past its limits; its limit by the plate's lot on this ship and
            // this shot's scatter (P7). Its thickness in calibres, for its cap and its body, is its mass's in steel
            double te = t / (shell.Kind == ShellKind.Common ? s.CommonPenShare : 1);
            ref readonly var mat = ref c.MaterialOf(x.Material);
            double td = mat.SteelMm(x.Mm) / calMm;
            // contents have no lot: only the shot's scatter (the user, 2026-10-19)
            double quality = (x.Plate.IsContents() ? 1 : PlateQuality(lots, x.Plate, x.Index, s))
                * Math.Max(1 + s.PlateShotSigma * r.NextGaussian(), MinQuality);
            if (x.Plate == PlateKind.SideArmour && k is not null && k.Loosened(x.Index, x.Point, s.BeltRepeatM))
                quality *= s.LoosenedPlate;   // a belt plate a leak loosened before (E4): its bolts pulled, its backing crushed
            // a face-hardened plate's face is outboard (its normal's way): a shell going out through it meets its soft back
            // first, and the face's rules don't apply (shatter, decapping, the body's quality); it's a plate of its q [EST]
            bool hard = mat.FaceHardened && !FromBehind(dir, x.Normal), shatter = false;
            if (hard)
            {
                double p = ShatterChance(shell, mat.FaceShatters, td, ob, x.T - capOffT < capGapM ? 0 : BareShare(cap, ob), s);
                shatter = p >= 1 || p > 0 && r.NextDouble() < p;
                te = FaceHardMm(te, shell, ob, shatter, s);
            }
            double vl = LimitMs(te, calMm, massKg, ob) * quality;
            bool decapped = false;
            if (cap != Cap.None && (hard || Decaps(cap, td, ob, ref r)))
            {
                // the cap's knocked off, whether the plate holds or not
                (capOffT, capGapM, cap) = (x.T, CapGapCalibres(cap, ob) * calMm / 1000, Cap.None);
                massKg *= 1 - shell.CapShare;
                res.Decapped = decapped = true;
            }
            res.Shattered |= shatter;
            var step = trace == null ? default : new PlateStep
            {
                Crossing = i, T = x.T, Point = x.Point, Mm = x.Mm, EffectiveMm = te, ObliquityDeg = ob, InMs = v, LimitMs = vl, Quality = quality,
                Decapped = decapped, FuzeLeftS = double.NaN, Name = among.NameOf(x),
            };
            if (!shatter && Glances(v, te, calMm, massKg, ob, s, quality))
            {
                // glanced: the plate soaks up its motion across it (a dent), whether it would have got through or not, and
                // it skids off along the plate's face, its fuze started as on a plate that held; the path bends at once,
                // so nothing behind the plate is crossed
                (res.Glance, res.PlateMs, res.PlateU) = (i, v, v / vl);
                double skid = v * Math.Sin(double.DegreesToRadians(ob));
                burstT = Slowed(burstT, x.T, v, skid);
                if (!started && StartsOn(shell, mat.SteelMm(x.Mm), ob, hard, x.T, ref impulse, ref impulseT, s))
                {
                    started = true;
                    Start(ref res, ref r, shell, s, x.T, skid, i, 0, ref burstT, ref force);
                    trace?.Fuzed(x.T, i, Cause(x));
                }
                v = skid;
                if (trace != null)
                {
                    (step.Call, step.OutMs, step.FuzeLeftS) = (PlateCall.Glanced, v, FuzeLeftS(burstT, x.T, v));
                    trace.Add(step);
                }
                dir = Skid(dir, x.Normal, s.GlanceLiftDeg);
                turned = false;
                bender.Bend(c, path, i + 1, x.T, dir);
                xs = path.Crossings;
                continue;
            }
            if (v < vl)
            {
                // under the Navy limit: from the plate's holing limit it holes it, breaking in it (its pieces through);
                // else stopped here, its back giving spall in the band below that. Stopped, running since an earlier
                // plate, it bursts against this one (a dud lies there); else it's defeated, the impact starting its fuze
                // if the plate's enough, and then it bursts on the plate's face
                double u = v / vl;
                (res.Plate, res.StopT, res.PlateMs, res.PlateU) = (i, x.T, v, u);
                if (trace != null)
                {
                    step.Call = u >= mat.HoledU ? PlateCall.Holed : PlateCall.Stopped;
                    trace.Add(step);
                }
                if (u >= mat.HoledU)
                {
                    if (FiresIn(ref res, ref r, shell, mat.SteelMm(x.Mm), ob, hard, x, i, v, ref started, ref burstT, ref force, ref impulse, ref impulseT, s,
                        trace))
                        return res;
                    (res.Outcome, res.Burst, res.BurstT, res.StopT, res.BurstMs) = (Outcome.Holed, BurstKind.None, double.NaN, x.T + 0.5, 0);
                    return res;
                }
                if (u >= mat.SpallU)
                    res.Spall = (u - mat.SpallU) / Math.Max(mat.HoledU - mat.SpallU, 1e-9);
                if (!double.IsNaN(burstT))
                    return Burst(ref res, Math.Max(x.T - 0.3, 0), force, res.Outcome, BurstKind.Inside, 0);
                if (started)
                    return res;
                if (StartsOn(shell, mat.SteelMm(x.Mm), ob, hard, x.T, ref impulse, ref impulseT, s))
                {
                    Start(ref res, ref r, shell, s, x.T, 0, i, 0, ref burstT, ref force);
                    trace?.Fuzed(x.T, i, Cause(x));
                    if (res.Outcome != Outcome.Dud)
                        (res.Burst, res.BurstT, res.Force) = (BurstKind.Outside, Math.Max(x.T - 0.3, 0), s.BurstOutsideK * force);
                }
                res.Outcome = Outcome.Defeated;
                return res;
            }
            // through: shattered, it's in pieces; just over the limit, the body may give way; whole, the filler, shocked by
            // the plate, may burst in it, the likelier the nearer the plate came to holding
            bool near = !shatter && v < s.NearLimitBand * vl && td >= s.BreakFromTD && r.NextDouble() < NearLimitBreak(ob, s);
            if ((shatter || near) && FiresIn(ref res, ref r, shell, mat.SteelMm(x.Mm), ob, hard, x, i, v, ref started, ref burstT, ref force, ref impulse,
                    ref impulseT, s, trace))
                return res;
            bool inPlate = !shatter && !near && InPlate(shell, v, vl, ref r);
            // whole and through, it turns toward the plate's normal, the turn costing it a little more (P5); the path
            // bends once the plates at this point are crossed
            double df = Turn(v, vl, ob), vOut = Residual(v, vl) * Math.Cos(double.DegreesToRadians(df));
            burstT = Slowed(burstT, x.T, v, vOut);
            v = vOut;
            if (!shatter && !near && !inPlate && df >= s.NormaliseMinDeg)
            {
                dir = TurnToward(dir, x.Normal, df);
                (turned, turnT) = (true, x.T);
                res.TurnDeg += df;
                step.TurnDeg = df;
            }
            if (trace != null)
            {
                (step.Call, step.OutMs) = (shatter ? PlateCall.Shattered : near ? PlateCall.BrokeUp : inPlate ? PlateCall.BurstInPlate : PlateCall.Through, v);
                trace.Add(step);
            }
            if (shatter || near)
            {
                // shattered, only its pieces pass the hole; broken just over the limit, the body goes on in pieces
                res.BrokeNearLimit = near;
                (res.Plate, res.Outcome, res.Burst, res.BurstT, res.StopT, res.BurstMs) =
                    (i, shatter ? Outcome.Holed : Outcome.BrokeUp, BurstKind.None, double.NaN, x.T + (shatter ? 0.5 : 1), v);
                return res;
            }
            if (inPlate)
            {
                res.Plate = i;
                return Burst(ref res, x.T + 0.01, s.BurstInPlateK, Outcome.BurstInPlate, BurstKind.InPlate, v);
            }
            if (!started && StartsOn(shell, mat.SteelMm(x.Mm), ob, hard, x.T, ref impulse, ref impulseT, s))
            {
                started = true;
                Start(ref res, ref r, shell, s, x.T, v, i, 0, ref burstT, ref force);
                trace?.Fuzed(x.T, i, Cause(x));
            }
            trace?.FuzeLeft(FuzeLeftS(burstT, x.T, v));
            if (among.Interior(x, out double runM, out double rho, out var medium, out string? name))
            {
                // through a struck object's skin: its interior, a medium, slows it (and a fuze running on time may burst in it)
                double vIn = v;
                WaterRun.Through(ref v, ref burstT, x.T, runM, WaterRun.DecayM(shell, s, rho, medium));
                trace?.Add(new PlateStep
                {
                    Crossing = i, T = x.T, Point = x.Point, InMs = vIn, OutMs = v, FuzeLeftS = FuzeLeftS(burstT, x.T + runM, v), Name = name, RunM = runM,
                    Call = PlateCall.Medium, Quality = 1,
                });
            }
        }
        // a turn at the path's last plates: bent there all the same, so the line beyond (a burst out there) is the shell's
        if (turned)
            bender.Bend(c, path, xs.Length, turnT, dir);
        return Gone(ref res, started, path.Length);
    }

    /// <summary>A shell breaking in a plate (holing it under its limit, shattered on it, or broken just over it, E8): a fuze
    /// that fires within the plate bursts it there first, before it's in pieces. Started on this plate with next to no
    /// delay (Shimose's Ijuin fuze on a Krupp belt: "exploded the moment they touched", 06 §5, 15 §2.3) it bursts on the
    /// face; running since an earlier plate, against it. Whether it burst (the result's set).</summary>
    static bool FiresIn(ref TerminalResult res, ref RandomStream r, in ShellTypeRow shell, double steelMm, double ob, bool hard, in Crossing x, int i,
        double v, ref bool started, ref double burstT, ref double force, ref double impulse, ref double impulseT, SimSettings s, TerminalTrace? trace)
    {
        bool here = false;
        if (!started && StartsOn(shell, steelMm, ob, hard, x.T, ref impulse, ref impulseT, s))
        {
            (started, here) = (true, true);
            Start(ref res, ref r, shell, s, x.T, v, i, 0, ref burstT, ref force);
            trace?.Fuzed(x.T, i, Cause(x));
        }
        double across = steelMm / 1000 / Math.Max(Math.Cos(double.DegreesToRadians(ob)), 0.1);
        if (double.IsNaN(burstT) || burstT > x.T + across)
            return false;
        res.Plate = i;
        if (here)
            Burst(ref res, Math.Max(x.T - 0.3, 0), s.BurstOutsideK * force, Outcome.BurstOutside, BurstKind.Outside, v);
        else
            Burst(ref res, Math.Max(x.T - 0.3, 0), force, res.Outcome, BurstKind.Inside, 0);
        return true;
    }

    /// <summary>The thickest plate (mm, by its material's quality) a nose fuze's shell punches before it bursts, so it
    /// bursts just inside it: <see cref="SimSettings.HePenCalibres"/> of its calibre, times √cos of the obliquity.</summary>
    public static double NosePunchMm(double calibreMm, double obliquityDeg, SimSettings s) =>
        s.HePenCalibres * calibreMm * Math.Sqrt(Math.Max(Math.Cos(double.DegreesToRadians(obliquityDeg)), 1e-3));

    /// <summary>A nose fuze on the first solid thing it meets, the crossing at T: blind, the shell goes on through a plate it
    /// punches (a dud, walked on inert by the caller) or lies against one it doesn't; else the filler (<see cref="Initiate"/>)
    /// bursts just inside a plate it punches, or outside one it doesn't, or is a dud.</summary>
    static TerminalResult Nose(ref TerminalResult res, ref RandomStream r, in ShellTypeRow shell, SimSettings s, double t, int i, bool punches, double v)
    {
        res.Plate = i;
        var (o, force) = r.NextDouble() < shell.FuzeBlind ? (Outcome.Dud, 0.0) : Initiate(shell, s, ref r);
        if (o == Outcome.Dud)
        {
            res.Outcome = Outcome.Dud;
            if (!punches)
                res.StopT = t;
            return res;
        }
        return punches
            ? Burst(ref res, t + 0.5, force, o, BurstKind.Inside, v)
            : Burst(ref res, Math.Max(t - 0.3, 0), s.BurstOutsideK * force, Outcome.BurstOutside, BurstKind.Outside, v);
    }

    /// <summary>A base fuze started at T with velocity v (spentS of its delay already run, on the water): blind (its
    /// <c>FuzeBlind</c>), a dud; else the filler's initiation (<see cref="Initiate"/>) and, unless that's a dud, where it
    /// bursts: its delay, scattered by the fuze's own share (σ, a normal draw; none under 0), times v on.</summary>
    static void Start(ref TerminalResult res, ref RandomStream r, in ShellTypeRow shell, SimSettings s, double t, double v, int i, double spentS,
        ref double burstT, ref double force)
    {
        res.Plate = i;
        if (r.NextDouble() < shell.FuzeBlind)
        {
            res.Outcome = Outcome.Dud;
            return;
        }
        (res.Outcome, force) = Initiate(shell, s, ref r);
        if (res.Outcome == Outcome.Dud)
            return;
        double delay = shell.FuzeDelayS * Math.Max(1 + shell.FuzeScatter * r.NextGaussian(), 0) - spentS;
        burstT = t + Math.Max(delay, 0) * v;
    }

    /// <summary>The filler set off by its fuze (damage/12 §4 stage 2): a full detonation by its explosive's rate with the
    /// shell's booster (<c>FillerFull</c>; black powder always, its re pricing it), else low-order
    /// (<see cref="SimSettings.LowOrderShare"/>, <see cref="SimSettings.LowOrderK"/> of the blast) or a dud.</summary>
    internal static (Outcome, double) Initiate(in ShellTypeRow shell, SimSettings s, ref RandomStream r)
    {
        double x = r.NextDouble();
        if (x < shell.FillerFull)
            return (Outcome.BurstInside, 1);
        return x - shell.FillerFull < (1 - shell.FillerFull) * s.LowOrderShare ? (Outcome.LowOrder, s.LowOrderK) : (Outcome.Dud, 0);
    }

    /// <summary>Whether a plate mm thick at an obliquity starts a base fuze: any face-hardened plate; else one over its
    /// threshold (<see cref="FuzeStartMm"/>), or for a two-plate fuze the plates within
    /// <see cref="SimSettings.FuzeSumCalibres"/> of each other adding up to it (their shares of it, the impulse).</summary>
    static bool StartsOn(in ShellTypeRow shell, double mm, double obliquityDeg, bool hard, double t, ref double impulse, ref double impulseT,
        SimSettings s)
    {
        if (hard)
            return true;
        double need = FuzeStartMm(shell, obliquityDeg);
        if (!shell.FuzeTwoPlate)
            return mm >= need;
        if (t - impulseT > s.FuzeSumCalibres * shell.CalibreMm / 1000)
            impulse = 0;
        (impulse, impulseT) = (impulse + mm / Math.Max(need, 1e-9), t);
        return impulse >= 1;
    }

    /// <summary>Whether the filler bursts in a plate the shell meets at v, the plate's limit vl: <see cref="InPlateChance"/>.</summary>
    static bool InPlate(in ShellTypeRow shell, double v, double vl, ref RandomStream r)
    {
        double p = InPlateChance(shell, v, vl);
        return p >= 1 || p > 1e-6 && r.NextDouble() < p;
    }

    /// <summary>The chance the filler bursts in a plate the shell gets through (damage/12 §4 stage 1): in_plate·ρ², ρ the
    /// plate over what the shell beats there (at most 1: past it the plate holds), which the plate step has as (V_L/v)²:
    /// in_plate at the limit, falling fast below it (open question 1: the exponent against Jutland and Baden).</summary>
    public static double InPlateChance(in ShellTypeRow shell, double v, double vl)
    {
        if (!(shell.FillerInPlate > 0) || !(vl > 0))
            return 0;
        double rho = vl * vl / Math.Max(v * v, 1e-9);
        return Math.Min(1, shell.FillerInPlate * rho * rho);
    }

    /// <summary>The walk's end with nothing more in the way: about to burst, beyond the far side; a dud, as it is;
    /// never started, through.</summary>
    static TerminalResult Gone(ref TerminalResult res, bool started, double t)
    {
        res.StopT = Math.Min(res.StopT, t);
        if (res.Outcome != Outcome.Dud)
            res.Outcome = started ? Outcome.BurstBeyond : res.Glance >= 0 ? Outcome.Ricochet : Outcome.PassedThrough;
        if (res.Outcome == Outcome.Ricochet && res.Plate < 0)
            res.Plate = res.Glance;
        return res;
    }

    /// <summary>C0 (docs/battle/shell-contents.md): the fuze runs on time. A running fuze's burst, burstT along the path,
    /// when the shell is slowed at t from v0 to v1: the delay left, (burstT − t)/v0, is spent at the new speed, so the
    /// burst comes in to t + (burstT − t)·v1/v0. Not running (NaN): as it is.</summary>
    internal static double Slowed(double burstT, double t, double v0, double v1) =>
        double.IsNaN(burstT) || !(v0 > 0) ? burstT : t + Math.Max(burstT - t, 0) * Math.Max(v1, 0) / v0;

    /// <summary>The delay a running fuze has left at t, the shell at v, seconds (NaN: not running).</summary>
    static double FuzeLeftS(double burstT, double t, double v) => double.IsNaN(burstT) ? double.NaN : Math.Max(burstT - t, 0) / Math.Max(v, 1e-9);

    static TerminalResult Burst(ref TerminalResult res, double t, double force, Outcome o, BurstKind kind, double v)
    {
        (res.Outcome, res.Burst, res.BurstT, res.StopT, res.Force, res.BurstMs) = (o, kind, t, t, force, v);
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

    /// <summary>How far a plate turns a shell that gets through it toward its normal, degrees (P5; Okun's M79, armor/05
    /// §1.5): DF = atan((1 − √K3)/(2·K2)) with K1 = VR² + VR·√(VR² − 1), K2 = sin Ob·cos Ob/K1, K3 = 1 − 4·K2², VR = v/V_L.
    /// Most for a shell barely through (30° at 45°), little for one far over (4° at twice V_L). The same at every
    /// obliquity: the research's stretch above 45° turned 16" shells 4-14° on Bismarck's 2" deck at 68°, Okun 2.65°.</summary>
    public static double Turn(double v, double vl, double obliquityDeg)
    {
        if (!(vl > 0) || v <= vl)
            return 0;
        double vr = v / vl, k1 = vr * vr + vr * Math.Sqrt(vr * vr - 1), o = double.DegreesToRadians(obliquityDeg);
        double k2 = Math.Sin(o) * Math.Cos(o) / k1;
        if (!(k2 > 1e-12))
            return 0;
        double k3 = Math.Max(1 - 4 * k2 * k2, 0);
        return double.RadiansToDegrees(Math.Atan((1 - Math.Sqrt(k3)) / (2 * k2)));
    }

    /// <summary>Whether a whole shell at v glances off a plate (P5): from <see cref="SimSettings.RicochetFromDeg"/> (the
    /// biting angle: below it the nose digs in), when its motion across the plate, v·cos ob, is less than the plate could
    /// stop square on (its limit at 0°, times <see cref="SimSettings.GlanceShare"/>). So a thin plate never turns a big
    /// shell away (an 18" at 460 m/s goes through 5 mm at 85°), a thick one glances anything grazing it. tMm is the plate
    /// as the shell meets it (its material, quality and draw).</summary>
    public static bool Glances(double v, double tMm, double calibreMm, double massKg, double obliquityDeg, SimSettings s, double quality = 1) =>
        obliquityDeg >= s.RicochetFromDeg && v * Cos(obliquityDeg) < s.GlanceShare * quality * LimitMs(tMm, calibreMm, massKg, 0);

    /// <summary>The quality of a ship's plate (P7): its lot's, drawn once for the ship from her own stream (lots) by the
    /// plate (its kind and row), a normal draw about 1 of <see cref="SimSettings.PlateLotSigma"/> on its limit velocity;
    /// the same every time she's hit there.</summary>
    public static double PlateQuality(in RandomStream lots, PlateKind kind, int index, SimSettings s)
    {
        var lot = lots.Derive(((ulong)kind << 32) | (uint)index);
        return Math.Max(1 + s.PlateLotSigma * lot.NextGaussian(), MinQuality);
    }

    /// <summary>The least a quality draw gives: a plate no worse than half its kind (far past any sigma used).</summary>
    const double MinQuality = 0.5;

    /// <summary>The thickest plate (the formula's, mm) a shell at v doesn't glance off at an obliquity: <see cref="Glances"/>
    /// the other way round (none under <see cref="SimSettings.RicochetFromDeg"/>: infinity). For readouts and the
    /// matchup.</summary>
    public static double GlanceMm(double v, double calibreMm, double massKg, double obliquityDeg, SimSettings s) =>
        obliquityDeg < s.RicochetFromDeg ? double.PositiveInfinity
            : PenMm(v * Cos(obliquityDeg) / Math.Max(s.GlanceShare, 1e-9), calibreMm, massKg, 0);

    /// <summary>The unit direction d turned by df degrees toward the plate's normal n (either way up: the side d goes
    /// toward), in the plane of the two.</summary>
    public static P3 TurnToward(P3 d, P3 n, double df)
    {
        double dn = d.X * n.X + d.Y * n.Y + d.Z * n.Z;
        if (dn < 0)
            (n, dn) = (new P3(-n.X, -n.Y, -n.Z), -dn);
        var (tx, ty, tz) = (d.X - dn * n.X, d.Y - dn * n.Y, d.Z - dn * n.Z);
        double tl = Math.Sqrt(tx * tx + ty * ty + tz * tz);
        if (!(tl > 1e-9))
            return d;
        double ob = Math.Atan2(tl, dn), to = Math.Max(ob - double.DegreesToRadians(df), 0);
        double cn = Math.Cos(to), ct = Math.Sin(to) / tl;
        return new P3(cn * n.X + ct * tx, cn * n.Y + ct * ty, cn * n.Z + ct * tz);
    }

    /// <summary>A glance's way off the plate: d's part along the plate's face, lifted liftDeg off it back to the side the
    /// shell came from (so the next trace doesn't run in the plate's plane).</summary>
    public static P3 Skid(P3 d, P3 n, double liftDeg)
    {
        double dn = d.X * n.X + d.Y * n.Y + d.Z * n.Z;
        var (tx, ty, tz) = (d.X - dn * n.X, d.Y - dn * n.Y, d.Z - dn * n.Z);
        double tl = Math.Sqrt(tx * tx + ty * ty + tz * tz);
        if (!(tl > 1e-9))
            return new P3(-d.X, -d.Y, -d.Z);
        double a = double.DegreesToRadians(liftDeg), back = dn > 0 ? -1 : 1;
        double ct = Math.Cos(a) / tl, cn = back * Math.Sin(a);
        return new P3(ct * tx + cn * n.X, ct * ty + cn * n.Y, ct * tz + cn * n.Z);
    }

    static double Cos(double obliquityDeg) => Math.Max(Math.Cos(double.DegreesToRadians(obliquityDeg)), 1e-6);

    /// <summary>The plate that starts a base fuze, mm (armor/00 §6.2): its start_cal calibres at normal, less at obliquity
    /// on Okun's curve, (1 + cos 2Ob)/2 + 0.4537·sin^5.7019 Ob, flat past 61°, stretched so that from 61° it's the fuze's
    /// own share (<c>FuzeOblique</c>: Okun's curve gives 0.45 there, the Mk 21 in the 16" Mk 8 0.25, 1.5" at 0° and
    /// 0.375" at 65°).</summary>
    public static double FuzeStartMm(in ShellTypeRow shell, double obliquityDeg)
    {
        double f = Okun(obliquityDeg), f61 = Okun(61);
        return shell.FuzeStartCal * shell.CalibreMm * (1 - (1 - f) * (1 - shell.FuzeOblique) / (1 - f61));

        static double Okun(double ob)
        {
            double r = double.DegreesToRadians(Math.Min(ob, 61));
            return (1 + Math.Cos(2 * r)) / 2 + 0.4537 * Math.Pow(Math.Sin(r), 5.7019);
        }
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

    /// <summary>Whether the flight meets a plate from behind: going the way of its normal (outward from the hull or the
    /// part), so its back first. No normal: from the front.</summary>
    public static bool FromBehind(P3 d, P3 n) => d.X * n.X + d.Y * n.Y + d.Z * n.Z > 0;

    /// <summary>The angle between the flight and a plate's normal, degrees (0 square on; no normal: square on).</summary>
    public static double Obliquity(P3 d, P3 n)
    {
        if (double.IsNaN(n.X))
            return 0;
        double c = Math.Abs(d.X * n.X + d.Y * n.Y + d.Z * n.Z);
        return double.RadiansToDegrees(Math.Acos(Math.Min(c, 1)));
    }

    static FuzeCause Cause(in Crossing x) => x.Plate.IsContents() ? FuzeCause.Contents : FuzeCause.Plate;

    // ------------------------------------------------------------------ what stands in the spaces (C1c)
    // docs/battle/shell-contents.md; research/simulation/damage-research/09-contents-in-the-path.md §1

    /// <summary>What the walk is among: the cell and the part it's in, where it may next meet a solid (the end of the last
    /// object struck, or the last crossing), and the last object struck: its skin's and core's T and names, and its
    /// interior, run once the shell is through the skin.</summary>
    struct Among
    {
        public int Cell, Part;
        public double FromT, SkinT, CoreT, InteriorM, InteriorRho;
        public MediumKind Medium;
        public string? SkinName, CoreName, InteriorName;

        /// <summary>Past a crossing: the cell or part it enters or leaves, and no solid before it.</summary>
        public void Enter(in Crossing x)
        {
            switch (x.Kind)
            {
                case CrossingKind.CellIn:
                    Cell = x.Index;
                    break;
                case CrossingKind.CellOut when x.Index == Cell:
                    Cell = -1;
                    break;
                case CrossingKind.PartIn:
                    Part = x.Index;
                    break;
                case CrossingKind.PartOut when x.Index == Part:
                    Part = -1;
                    break;
            }
            FromT = Math.Max(FromT, x.T);
        }

        public readonly string? NameOf(in Crossing x) => !x.Plate.IsContents() ? null
            : Math.Abs(x.T - SkinT) < HitPath.SameM / 2 ? SkinName : CoreName;

        /// <summary>Whether a crossing was a struck skin with an interior behind it, and that interior (once).</summary>
        public bool Interior(in Crossing x, out double runM, out double rho, out MediumKind medium, out string? name)
        {
            (runM, rho, medium, name) = (InteriorM, InteriorRho, Medium, InteriorName);
            if (!x.Plate.IsContents() || !(Math.Abs(x.T - SkinT) < HitPath.SameM / 2) || !(InteriorM > 0))
                return false;
            InteriorM = 0;
            return true;
        }
    }

    /// <summary>Where the shell, in a space with contents, next strikes one before crossing i (docs/battle/shell-contents.md
    /// C1c; infinity: it doesn't): over the stretch from where it may next meet one to the crossing, clipped to the
    /// contents' band, the next solid comes after an Exp(λ) draw (memoryless: a room cut into more cells meets as
    /// often).</summary>
    static double NextSolid(ReadOnlySpan<RoomContents> rows, HitPath path, int i, double t0, out int row, ref RandomStream r)
    {
        // each of its bands (a barbette has two) draws its next solid; the first met is struck
        (double best, row) = (double.PositiveInfinity, -1);
        double t1 = path.Crossings[i].T;
        for (int j = 0; j < rows.Length; j++)
        {
            ref readonly var k = ref rows[j];
            if (!k.Some)
                continue;
            var (ta, tb) = Band(path.At(t0).Z, path.At(t1).Z, t0, t1, k.ZLo, k.ZHi);
            if (!(tb > ta))
                continue;
            double ts = ta - Math.Log(1 - r.NextDouble()) * k.LambdaM;
            if (ts < tb - HitPath.SameM && ts < best)
                (best, row) = (ts, j);
        }
        return best;
    }

    /// <summary>A solid struck at ts, before crossing i: the object drawn from the room's mix; its skin goes on the path
    /// there, and its core, by its chance, behind its interior (or halfway through it), as plates the walk takes like any
    /// other; no other solid within its chord. False when the path is full.</summary>
    static bool Put(ShipClass c, HitPath path, int i, double ts, P3 dir, in RoomContents k, PlateKind plate, int room, ref Among a, ref RandomStream r)
    {
        double t1 = path.Crossings[i].T;
        var mix = c.ContentObjects(k.Mix);
        double u = r.NextDouble();
        int j = 0;
        while (j < mix.Length - 1 && u > mix[j].UpTo)
            j++;
        ref readonly var o = ref mix[j];
        double extent = Math.Min(k.ChordM, t1 - ts - HitPath.SameM), interior = o.InteriorRho > 0 ? o.InteriorShare * extent : 0;
        if (!path.Insert(i, Solid(path, plate, room, ts, dir, o.Skin, ref r)))
            return false;
        (a.SkinT, a.SkinName, a.InteriorM, a.InteriorRho, a.Medium, a.InteriorName) = (ts, o.Skin.Name, interior, o.InteriorRho, o.Interior, o.Name);
        a.CoreT = double.NaN;
        if (o.Core.Some && r.NextDouble() < o.CoreChance)
        {
            double tc = Math.Min(ts + (interior > 0 ? interior : 0.5 * extent), t1 - HitPath.SameM / 2);
            if (tc > ts + HitPath.SameM && path.Insert(i + 1, Solid(path, plate, room, tc, dir, o.Core, ref r)))
                (a.CoreT, a.CoreName) = (tc, o.Core.Name);
        }
        a.FromT = ts + extent;
        return true;
    }

    /// <summary>What a space stows slows the shell over [t0, t1] (docs/battle/shell-contents.md C2-C3): its room's medium
    /// (oil, coal, feed water) below the level it stands to (<see cref="ShipCondition.Level"/>), and the sea the flooding
    /// has let into the cell above that up to its own level, each a run on the fuze's clock
    /// (<see cref="WaterRun.Through"/>), in order along the path.</summary>
    static void Media(ShipClass c, ShipCondition k, HitPath path, int i, int cell, double t0, double t1, in ShellTypeRow shell, SimSettings s,
        ref double v, ref double burstT, TerminalTrace? trace)
    {
        if (!(t1 - t0 > HitPath.SameM))
            return;
        ref readonly var row = ref c.Cells[cell];
        int room = row.Room;
        double zRoom = room >= 0 && room < k.Level.Length ? k.Level[room] : double.NaN;
        double cap = row.VolumeM3 * row.Permeability, water = cell < k.WaterM3.Length ? k.WaterM3[cell] : 0;
        double zSea = water > 0 && cap > 0 ? row.Box.Base + Math.Min(water / cap, 1) * (row.Box.Top - row.Box.Base) : double.NaN;
        if (double.IsNaN(zRoom) && double.IsNaN(zSea))
            return;
        double z0 = path.At(t0).Z, z1 = path.At(t1).Z;
        var (ra, rb) = double.IsNaN(zRoom) ? (1.0, 0.0) : Band(z0, z1, t0, t1, double.NegativeInfinity, zRoom);
        var (wa, wb) = double.IsNaN(zSea) || zSea <= zRoom ? (1.0, 0.0) : Band(z0, z1, t0, t1, double.IsNaN(zRoom) ? double.NegativeInfinity : zRoom, zSea);
        // in order along the path: falling, the flood water above the stowed medium comes first
        if (wa < ra)
        {
            Run(path, i, wa, wb, WaterRun.SeaRho, MediumKind.Liquid, "sea water, flooded", shell, s, ref v, ref burstT, trace);
            if (room >= 0)
                Run(path, i, ra, rb, c.Stowages[room].Rho, c.Stowages[room].Kind, c.Stowages[room].Name, shell, s, ref v, ref burstT, trace);
        }
        else
        {
            if (room >= 0 && !double.IsNaN(zRoom))
                Run(path, i, ra, rb, c.Stowages[room].Rho, c.Stowages[room].Kind, c.Stowages[room].Name, shell, s, ref v, ref burstT, trace);
            Run(path, i, wa, wb, WaterRun.SeaRho, MediumKind.Liquid, "sea water, flooded", shell, s, ref v, ref burstT, trace);
        }
    }

    /// <summary>One run through a medium from ta to tb (none when empty, or past where the fuze has already burst it).</summary>
    static void Run(HitPath path, int i, double ta, double tb, double rho, MediumKind kind, string name, in ShellTypeRow shell, SimSettings s,
        ref double v, ref double burstT, TerminalTrace? trace)
    {
        if (!(tb - ta > 1e-6) || kind == MediumKind.None || !double.IsNaN(burstT) && burstT <= ta)
            return;
        double vIn = v;
        WaterRun.Through(ref v, ref burstT, ta, tb - ta, WaterRun.DecayM(shell, s, rho, kind));
        trace?.Add(new PlateStep
        {
            Crossing = i, T = ta, Point = path.At(ta), InMs = vIn, OutMs = v, FuzeLeftS = FuzeLeftS(burstT, tb, v), Name = name, RunM = tb - ta,
            Call = PlateCall.Medium, Quality = 1,
        });
    }

    /// <summary>A shaft the line crosses at crossing i (its PartIn), struck as the solid it is (docs/battle/shell-contents.md
    /// C4c; damage/09 §7.1): its offset b from the axis (fore and aft) where the line crosses it is the geometry's, from the
    /// line's chord through it across the axis; the steel it crosses there, through the bore (<see cref="Propulsion.ShaftBore"/>),
    /// goes on the path halfway through as one plate of that steel times cos θ, θ its obliquity on the shaft's face.</summary>
    static bool Shaft(ShipClass c, HitPath path, int i, P3 d, ref Among a, ref RandomStream r)
    {
        var xs = path.Crossings;
        int comp = xs[i].Index, j = i + 1;
        while (j < xs.Length && !(xs[j].Kind == CrossingKind.PartOut && xs[j].Index == comp))
            j++;
        double rM = c.Components[comp].R;
        if (j >= xs.Length || !(rM > 0))
            return false;
        double len = xs[j].T - xs[i].T, across = Math.Sqrt(d.Y * d.Y + d.Z * d.Z);
        double half = Math.Min(len * across / 2, rM), b = Math.Sqrt(Math.Max(rM * rM - half * half, 0)), sinB = Math.Min(b / rM, 1);
        double steel = Contents.BarChordMm(2000 * rM, Propulsion.ShaftBore, 1000 * b), cosOb = across * Math.Sqrt(1 - sinB * sinB);
        double tm = xs[i].T + len / 2;
        int at = i + 1;
        while (at < xs.Length && xs[at].T < tm)
            at++;
        var n = Struck(d, Math.Sqrt(Math.Max(1 - cosOb * cosOb, 0)), ref r);
        var hit = new Crossing
        {
            T = tm, Point = path.At(tm), Kind = CrossingKind.Struck, Plate = PlateKind.PartContents, Index = comp, Mm = steel * Math.Sqrt(1 - sinB * sinB),
            Material = c.ForgedMaterial, Normal = n,
        };
        if (!path.Insert(at, hit))
            return false;
        (a.SkinT, a.SkinName, a.InteriorM, a.CoreT) = (tm, "shaft", 0, double.NaN);
        return true;
    }

    /// <summary>The stretch of [t0, t1] (heights z0 to z1, straight) within the band zLo..zHi; empty when none.</summary>
    static (double, double) Band(double z0, double z1, double t0, double t1, double zLo, double zHi)
    {
        if (Math.Abs(z1 - z0) < 1e-9)
            return z0 >= zLo && z0 <= zHi ? (t0, t1) : (1, 0);
        double a = t0 + (zLo - z0) / (z1 - z0) * (t1 - t0), b = t0 + (zHi - z0) / (z1 - z0) * (t1 - t0);
        if (a > b)
            (a, b) = (b, a);
        return (Math.Max(a, t0), Math.Min(b, t1));
    }

    /// <summary>A struck solid as one plate on the path at T (damage/09 §1.3): its thickness drawn from its range, and its
    /// obliquity from its shape: a flat one square to the room's axis nearest the flight; a cylinder (along the ship) with
    /// sin θ uniform, a sphere sin² θ; a bar's offset b uniform across it, θ = asin(b/R), the steel it crosses its chord at
    /// b, as a plate of chord·cos θ (the plate model puts the 1/cos back).</summary>
    static Crossing Solid(HitPath path, PlateKind plate, int index, double t, P3 d, in SolidAt sol, ref RandomStream r)
    {
        double mm = sol.LoMm + (sol.HiMm - sol.LoMm) * r.NextDouble();
        P3 n;
        switch (sol.Shape)
        {
            case SolidShape.Flat:
                n = Square(d);
                break;
            case SolidShape.Bar:
            {
                double sin = r.NextDouble(), cos = Math.Sqrt(1 - sin * sin);
                n = Struck(d, sin, ref r);
                mm = Contents.BarChordMm(mm, sol.Bore, sin * mm / 2) * cos;
                break;
            }
            default:
                n = Struck(d, sol.Shape == SolidShape.Sphere ? Math.Sqrt(r.NextDouble()) : r.NextDouble(), ref r);
                break;
        }
        return new Crossing
        {
            T = t, Point = path.At(t), Kind = CrossingKind.Struck, Plate = plate, Index = index, Mm = mm, Material = sol.Material, Normal = n,
        };
    }

    /// <summary>The normal of the room's axis nearest the flight d, facing it.</summary>
    static P3 Square(P3 d)
    {
        double ax = Math.Abs(d.X), ay = Math.Abs(d.Y), az = Math.Abs(d.Z);
        return ax >= ay && ax >= az ? new P3(-Math.Sign(d.X), 0, 0) : ay >= az ? new P3(0, -Math.Sign(d.Y), 0) : new P3(0, 0, -Math.Sign(d.Z));
    }

    /// <summary>A surface normal at obliquity asin(sin) to the flight d: turned from facing it about the ship's long axis
    /// (a cylinder lying fore and aft is curved across it), either way.</summary>
    static P3 Struck(P3 d, double sin, ref RandomStream r)
    {
        // e: square to the flight and to the cylinder's axis (x); for a flight along the axis, to the vertical instead
        var (ex, ey, ez) = (0.0, d.Z, -d.Y);
        double l = Math.Sqrt(ey * ey + ez * ez);
        if (l < 1e-6)
            (ex, ey, ez, l) = (d.Y, -d.X, 0.0, Math.Sqrt(d.X * d.X + d.Y * d.Y));
        double side = r.NextDouble() < 0.5 ? -1 : 1, cos = Math.Sqrt(Math.Max(1 - sin * sin, 0)), k = side * sin / Math.Max(l, 1e-12);
        return new P3(-cos * d.X + k * ex, -cos * d.Y + k * ey, -cos * d.Z + k * ez);
    }

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
