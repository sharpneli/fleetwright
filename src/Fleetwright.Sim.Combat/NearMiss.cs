using Fleetwright.Shipgen;
using Fleetwright.Sim.Core;
using Fleetwright.Sim.Ships;

namespace Fleetwright.Sim.Combat;

/// <summary>A near miss (docs/battle/shells-armour-damage.md E7; research docs/battle/damage/15_near_misses.md,
/// <c>near_miss_ref.py</c>): a shell that missed bursts in the water or over it, outside every hull, and a ship close by takes
/// what it does. There is no terminal walk: it's a burst outside her, on the one path every explosion takes
/// (<see cref="Effects.Outside"/>; docs/battle/explosions.md "One outside"): its near field, its pieces and, in the sea, its shock
/// and its bubble. What's a near miss's own is where the shell bursts and who's near enough to feel it:
/// <list type="bullet">
/// <item><b>The burst</b> (<see cref="Burst"/>, 15 §1): the fuze, not the calibre, decides where. A nose fuze fires as
/// the shell meets the sea, its <c>FuzeWaterMs</c> later: a few centimetres down. A base fuze's delay runs from the
/// water (<see cref="SimSettings.FuzeStartsOnWater"/>): metres down, unless its run met a hull first (P6's underwater
/// hit, the shell system's). Blind, not firing on the water, a dud filler, or a base fuze skipping off a shallow fall:
/// nothing.</item>
/// <item><b>Who's near</b> (<see cref="ReachM"/>, 15 §6.1): the ships whose waterplane lies within max(15·W^⅓,
/// 7.5·√W) m.</item>
/// <item><b>Shock</b> (<see cref="Shock"/>, 15 §3, §6.4), the water's part of any burst in the sea: the near-miss shock factor
/// (<see cref="Nsf"/>) at the nearest wetted point of the hull, by its bands: the gear, the machinery and the structure, each as far
/// as its own shock reaches. A surface burst's is cut off by the sea's surface almost at once; an AP shell's, metres down,
/// isn't.</item>
/// </list>
/// Pure and allocation-free; the draws come from the stream it's given.</summary>
public static class NearMiss
{
    /// <summary>How far a shell's burst in the water reaches a ship's waterplane, m: max(K·W^⅓, K'·√W), W the filler's TNT
    /// kg (0: none).</summary>
    public static double ReachM(in ShellTypeRow shell, SimSettings s)
    {
        double w = Math.Max(shell.FillerKg * shell.FillerRe, 0);
        return w > 0 ? Math.Max(s.NearMissSplinterK * Math.Cbrt(w), s.NearMissShockK * Math.Sqrt(w)) : 0;
    }

    /// <summary>Where a shell that met the sea at v, falling at fallDeg, bursts (15 §1.2): how far along its run under the
    /// water (straight on, P6's), and its filler's force (1 full, <see cref="SimSettings.LowOrderK"/> low-order; 0 it
    /// doesn't burst). A nose fuze fires its water time on (v·t: the depth v·t·sin fall); a base fuze its delay, scattered,
    /// and its water time on, the shell slowing on its run. A base fuze on a fall flatter than
    /// <see cref="SimSettings.WaterRicochetDeg"/> skips on (v0: no burst); a nose fuze fires on the graze.</summary>
    public static (double RunM, double Force) Burst(in ShellTypeRow shell, double v, double fallDeg, SimSettings s, ref RandomStream r)
    {
        if (!(shell.FillerKg > 0))
            return (0, 0);
        bool nose = shell.Fuze == Fuze.Nose;
        if (!nose && (fallDeg < s.WaterRicochetDeg || !WaterRun.FuzeStarts(shell, s)))
            return (0, 0);
        if (r.NextDouble() < shell.FuzeBlind || r.NextDouble() >= shell.FuzeWaterFires)
            return (0, 0);
        var (o, force) = Terminal.Initiate(shell, s, ref r);
        if (o == Outcome.Dud)
            return (0, 0);
        double t = shell.FuzeWaterMs / 1000;
        if (!nose)
            t += shell.FuzeDelayS * Math.Max(1 + shell.FuzeScatter * r.NextGaussian(), 0);
        return (WaterRun.RunInM(v, t, WaterRun.DecayM(shell, s)), force);
    }

    /// <summary>How far a point (ship frame, x and y) is from the ship's level waterplane, m (0 over it: under the hull).</summary>
    public static double WaterplaneM(ShipClass c, double x, double y)
    {
        var wl = c.Solids.Waterline;
        double ay = Math.Abs(y), best = double.PositiveInfinity;
        for (int i = 0; i + 1 < wl.Length; i++)
        {
            var a = wl[i];
            var b = wl[i + 1];
            if (x >= a.X && x <= b.X && b.X > a.X && ay <= a.Y + (b.Y - a.Y) * (x - a.X) / (b.X - a.X))
                return 0;
            double ex = b.X - a.X, ey = b.Y - a.Y, l2 = ex * ex + ey * ey;
            double u = l2 > 0 ? Math.Clamp(((x - a.X) * ex + (ay - a.Y) * ey) / l2, 0, 1) : 0;
            double dx = x - a.X - u * ex, dy = ay - a.Y - u * ey;
            best = Math.Min(best, dx * dx + dy * dy);
        }
        return Math.Sqrt(best);
    }

    /// <summary>A near miss on a ship: the burst at the hit's point (ship frame, outside the hull), the shell flying along its direction
    /// at its speed, its filler's force on the hit (<see cref="Hit.Force"/>): a burst outside her (<see cref="Effects.Outside"/>). A tool
    /// may pass a span by component to have the pieces expected into each added to it.</summary>
    public static EffectsResult Apply(ShipClass c, ShipCondition k, in Hit hit, SimSettings s, ref RandomStream r, Span<double> intoParts = default) =>
        Effects.Outside(c, k, hit.Shell, hit.Point, hit.Direction, hit.SpeedMs, hit.Force, hit.Ms, s, ref r, intoParts);


    // ------------------------------------------------------------------ shock

    const double Lb = 0.45359237, Ft = 0.3048, SoundMs = 1500;

    /// <summary>The near-miss shock factor (15 §3.1) of wKg of TNT bursting at a point (the sea's surface at surfaceZ) at a
    /// point of the hull: √W/R (lb, ft), the keel-shock angle's (1 + sin a)/2 (a the line's angle up from the burst to the
    /// hull: 0 alongside, 90° under the keel), and the share of the pulse's energy the surface's reflection leaves it,
    /// √(1 − e^(−2Δt/θ)) (Δt the reflection's lag, Cole's decay θ = 6.0·10⁻⁵·W^⅓·(R/W^⅓)^0.18 s).</summary>
    public static double Nsf(double wKg, P3 burst, double surfaceZ, P3 at)
    {
        if (!(wKg > 0))
            return 0;
        double wLb = wKg / Lb;
        double dx = at.X - burst.X, dy = at.Y - burst.Y, dz = at.Z - burst.Z;
        double rr = Math.Max(Math.Sqrt(dx * dx + dy * dy + dz * dz), 0.5);
        double sinA = Math.Max(0, dz / rr);
        double iz = at.Z - (2 * surfaceZ - burst.Z), ri = Math.Sqrt(dx * dx + dy * dy + iz * iz);
        double dt = Math.Max(ri - rr, 0) / SoundMs;
        double rFt = rr / Ft, cube = Math.Cbrt(wLb), theta = 6.0e-5 * cube * Math.Pow(rFt / cube, 0.18);
        return Math.Sqrt(wLb) / rFt * (1 + sinA) / 2 * Math.Sqrt(1 - Math.Exp(-2 * dt / theta));
    }

    /// <summary>A space's wetted outside face (0 port side, 1 starboard, 3 bottom): its point nearest p (on the side, at
    /// least <see cref="SimSettings.ShockWetM"/> under the sea), its wetted area and its plate (mm of steel: the shell and
    /// any belt over it). False when the face isn't outside or isn't wet.</summary>
    static bool WetFace(ShipClass c, ShipCondition k, int cell, int face, P3 p, in Waterplane water, SimSettings s, out P3 at, out double areaM2,
        out double mm)
    {
        (at, areaM2) = (default, 0);
        if (!(Blast.Outside(k, cell, face, out mm, out _) > 0))
            return false;
        var b = c.Cells[cell].Box;
        double x = Math.Clamp(p.X, b.X0, b.X1);
        if (face < 2)
        {
            double sg = face == 0 ? -1 : 1, top = Math.Min(b.Top, water.At(x, sg * c.Hull.HalfWidth(x, b.Top)) - s.ShockWetM);
            if (top <= b.Base)
                return false;
            double z = Math.Clamp(p.Z, b.Base, top);
            (at, areaM2) = (new P3(x, sg * c.Hull.HalfWidth(x, z), z), (b.X1 - b.X0) * (top - b.Base));
            return true;
        }
        double hw = c.Hull.HalfWidth(x, b.Base), y0 = Math.Max(b.Y0, -hw), y1 = Math.Min(b.Y1, hw);
        if (y1 < y0)
            (y0, y1) = (b.Y0, b.Y1);
        (at, areaM2) = (new P3(x, Math.Clamp(p.Y, y0, y1), b.Base), (b.X1 - b.X0) * (b.Y1 - b.Y0));
        return true;
    }

    /// <summary>The outside faces that can be wet: the sides and the bottom (0, 1 and 3; 2 is the weather deck). Walked
    /// by index, not over an array: a static one is made by the class's initialiser at the process's first near miss, in
    /// the middle of a fight (docs/battle/balance.md BL1r), and a constant span literal allocates in unoptimised code.</summary>
    static int WetFaceAt(int j) => j < 2 ? j : 3;

    const int WetFaceCount = 3;

    /// <summary>The ship's shock factor: the most of any wetted face's at its nearest point (15 §6.4: one number per ship),
    /// that face's space, point and plate.</summary>
    internal static double Nearest(ShipClass c, ShipCondition k, P3 p, double surface, double w, in Waterplane water, SimSettings s, out int cell, out P3 at,
        out double mm)
    {
        (cell, at, mm) = (-1, default, 0);
        double best = 0;
        if (!(w > 0))
            return 0;
        for (int i = 0; i < c.Cells.Length; i++)
            for (int j = 0; j < WetFaceCount; j++)
                if (WetFace(c, k, i, WetFaceAt(j), p, water, s, out var pt, out _, out double plate) && Nsf(w, p, surface, pt) is double n && n > best)
                    (best, cell, at, mm) = (n, i, pt, plate);
        return best;
    }

    /// <summary>What a burst's shock in the sea does to her (15 §6.4; its bands, <see cref="SimSettings.NearShockBands"/>), at a point p
    /// (the sea's surface at surfaceZ over it) at ms, w kg of TNT, its shock factor at her nearest wetted point (nsf, that point and its plate):
    /// <list type="bullet">
    /// <item><b>the structure:</b> every wetted face's seams by r(D) over the patch where D (5·NSF, scaled by its plate
    /// against <see cref="SimSettings.ShockPlateMm"/>) is past the weep, falling as 1/R from its nearest point; the
    /// transverse bulkheads under the sea likewise, by their own plate (its holes are the near field's, the burst's own:
    /// <see cref="GasNetwork.AddOutside"/>);</item>
    /// <item><b>the machinery:</b> each boiler and engine room by its own NSF: its gear damaged across the castings' band
    /// (times the grade), the shafts through it strained across the hull's, the room again across the buckling's;</item>
    /// <item><b>the gear</b> (with director fire control, E4's: breakers to trip): across the fragile band each mount's and
    /// director's breakers by its own NSF (one draw: the nearer trip first) and the directors' gyros rolling E4's failure
    /// at the nearest point's; the whole ship's breakers by her machinery spaces' NSF (her dynamos and switchboards) across
    /// the hull's band (White Plains, 0.3-0.6; the bomb record's 0.05-0.25 names gear and machinery, no ship-wide loss),
    /// each times the grade, each for a while;</item>
    /// <item><b>the stern:</b> a burst near it shakes the rudders, shafts and propellers by their own NSF across the hull's
    /// band (Ilex, Fortune).</item>
    /// </list></summary>
    internal static void Shock(ShipClass c, ShipCondition k, P3 p, long now, double w, double surface, in Waterplane water, double nsf, P3 nearAt,
        double nearMm, SimSettings s, ref EffectsResult res, ref RandomStream r)
    {
        var b = s.NearShockBands;
        res.Nsf = nsf;
        double grade = s.NearShockGradeK[Math.Min((int)c.ShockGrade, s.NearShockGradeK.Length - 1)], gear = nsf / grade;

        // the structure: the shell's seams, face by face
        if (5 * nsf * s.ShockPlateMm / Math.Max(nearMm, 1) >= s.LeakWeepD)
            for (int i = 0; i < c.Cells.Length; i++)
                for (int j = 0; j < WetFaceCount; j++)
                {
                    int face = WetFaceAt(j);
                    if (!WetFace(c, k, i, face, p, water, s, out var at, out double area, out double mm))
                        continue;
                    double d0 = 5 * Nsf(w, p, surface, at) * s.ShockPlateMm / Math.Max(mm, 1);
                    if (!(d0 >= s.LeakWeepD))
                        continue;
                    double leak = Math.Min(Seams(d0, Distance(p, at), area, c.ShellJoint, s) * Blast.Spread(s, ref r) * 1e-4, s.RupturedShare * area);
                    if (leak > 0)
                    {
                        k.Hole(i, leak, at, seam: true);
                        (res.BreachM2, res.ShockM2) = (res.BreachM2 + leak, res.ShockM2 + leak);
                    }
                }
        // the transverse bulkheads under the sea, by their own plate (each edge once, from its lower cell)
        if (5 * nsf * s.ShockPlateMm >= s.LeakWeepD)
            for (int i = 0; i < c.Cells.Length; i++)
            {
                var box = c.Cells[i].Box;
                var nb = c.Cells[i].Neighbours;
                for (int j = nb.Start; j < nb.End; j++)
                {
                    ref readonly var e = ref c.Neighbour(j);
                    if (e.Join != JoinKind.Bulkhead || e.Cell < i)
                        continue;
                    var other = c.Cells[e.Cell].Box;
                    if (Math.Min(box.Y1, other.Y1) - Math.Max(box.Y0, other.Y0) < 0.05)
                        continue;   // side by side across the ship: a longitudinal bulkhead
                    double xb = other.X0 >= box.X1 - 0.05 ? box.X1 : box.X0, y0 = Math.Max(box.Y0, other.Y0), y1 = Math.Min(box.Y1, other.Y1);
                    double z0 = Math.Max(box.Base, other.Base), z1 = Math.Min(Math.Min(box.Top, other.Top), water.At(xb, p.Y) - s.ShockWetM);
                    if (z1 <= z0)
                        continue;
                    var at = new P3(xb, Math.Clamp(p.Y, y0, y1), Math.Clamp(p.Z, z0, z1));
                    double d0 = 5 * Nsf(w, p, surface, at) * s.ShockPlateMm / Blast.JoinMm(c, e, box, other);
                    if (!(d0 >= s.LeakWeepD))
                        continue;
                    double area = (y1 - y0) * (z1 - z0);
                    double leak = Math.Min(Seams(d0, Distance(p, at), area, c.InternalsJoint, s) * Blast.Spread(s, ref r) * 1e-4, s.RupturedShare * area);
                    leak += Leaks.Sprung(c, k, i, j, leak, d0, area, s, ref r);   // its seams sprung and its closures loosened (DC1)
                    res.Blast.LeakM2 += leak;
                }
            }

        // the machinery, each space by its own shock
        var comps = c.Components;
        // the machinery spaces' worst where her dynamos and switchboards stand, on the floor on the centreline, abreast the
        // burst or at the space's nearer end: a small charge close alongside shakes the hull's near corner of a space, not
        // the plant across the ship
        double plant = -1;
        for (int room = 0; room < c.Rooms.Length; room++)
        {
            if (c.Rooms[room].Kind is not (RoomKind.BoilerRoom or RoomKind.EngineRoom))
                continue;
            double n = 0, floor = double.PositiveInfinity;
            foreach (int cell in c.Links(c.Rooms[room].Cells))
            {
                n = Math.Max(n, Nsf(w, p, surface, Nearest(c.Cells[cell].Box, p)));
                floor = Math.Min(floor, c.Cells[cell].Box.Base);
            }
            plant = Math.Max(plant, Nsf(w, p, surface, new P3(Math.Clamp(p.X, c.Rooms[room].X0, c.Rooms[room].X1), 0, floor)));
            int node = c.RoomNode(room);
            if (Chance(Ramp(n / grade, b[1], b[2]), ref r))
                Effects.Harm(ref res, k, node);
            if (Chance(Ramp(n, b[2], b[3]), ref r))
                for (int i = 0; i < comps.Length; i++)
                    if (comps[i].Kind == ComponentKind.Shaft && InRoom(c, room, comps[i].Bounds))
                        Effects.Harm(ref res, k, i);
            if (Chance(Ramp(n, b[3], b[4]), ref r))
                Effects.Harm(ref res, k, node);
        }

        // the gear: breakers (the mounts', directors' and searchlights') and the directors' gyros (with something to trip)
        // the breakers by each mount's and director's own shock, one draw for them all: the nearer trip first
        if (Ramp(gear, b[0], b[1]) > 0)
        {
            double u = r.NextDouble();
            long ms = -1;
            for (int m = 0; m < c.Mounts.Length; m++)
            {
                if (!c.OnElectric(m))
                    continue;   // off the plant: no breaker to trip (Effects.PowerOut)
                ref readonly var mr = ref c.Mounts[m];
                double n = Nsf(w, p, surface, Nearest(comps[mr.Component].Bounds, p));
                if (mr.Barbette >= 0)
                    n = Math.Max(n, Nsf(w, p, surface, Nearest(comps[mr.Barbette].Bounds, p)));
                if (u < Ramp(n / grade, b[0], b[1]))
                {
                    ms = ms < 0 ? NonPen.Minutes(s.NearTripMin, ref r) : ms;
                    k.PowerOff(m, now, ms, s);
                    res.Shock = true;
                }
            }
            for (int d = 0; d < c.Directors.Length; d++)
                if (u < Ramp(Nsf(w, p, surface, Nearest(comps[c.Directors[d].Component].Bounds, p)) / grade, b[0], b[1]))
                {
                    ms = ms < 0 ? NonPen.Minutes(s.NearTripMin, ref r) : ms;
                    k.Out(k.DirectorPower, d, now, ms);
                    res.Shock = true;
                }
            for (int l = 0; l < c.Searchlights.Length; l++)
                if (u < Ramp(Nsf(w, p, surface, Nearest(comps[c.Searchlights[l].Component].Bounds, p)) / grade, b[0], b[1]))
                {
                    ms = ms < 0 ? NonPen.Minutes(s.NearTripMin, ref r) : ms;
                    k.Out(k.SearchlightDown, l, now, ms);
                    res.Shock = true;
                }
        }
        // the whole ship's by her machinery spaces' (15 §3.3: White Plains' "most of the circuit breakers" at 0.3-0.6)
        if (Chance(plant >= 0 ? Ramp(plant / grade, s.NearPlantTrip[0], s.NearPlantTrip[1]) : Ramp(nsf / grade, b[2], b[3]), ref r))
        {
            res.Shock |= Effects.Blackout(c, k, nearAt, NonPen.Minutes(s.NearTripMin, ref r), now, s);
            res.Blackout = true;
        }
        // the gyros roll a failure (15 §6.4) at E4's chance for a heavy shock, the grade already in the edges
        if (NonPen.DirectorFire(c) && Chance(Ramp(gear, b[0], b[1]) * s.ShockDirectorGyro[0], ref r))
        {
            long ms = NonPen.Minutes(s.DirectorGyroMin, ref r);
            for (int d = 0; d < c.Directors.Length; d++)
                k.Out(k.DirectorDown, d, now, ms);
            res.Shock = true;
        }

        // the stern: rudders, shafts and propellers near a burst there
        if (p.X - c.Solids.Waterline[0].X <= s.SternShare * c.Length)
            for (int i = 0; i < comps.Length; i++)
            {
                if (comps[i].Kind is not (ComponentKind.Rudder or ComponentKind.Shaft or ComponentKind.Propeller))
                    continue;
                if (!Chance(Ramp(Nsf(w, p, surface, Nearest(comps[i].Bounds, p)), b[2], b[3]), ref r))
                    continue;
                if (comps[i].Kind == ComponentKind.Rudder)
                    Effects.Count(ref res, k, i, k.Worsen(i, PartState.Disabled) ? PartState.Disabled : PartState.Ok);
                else
                    Effects.Harm(ref res, k, i);
            }
    }

    /// <summary>r(D)'s seam leak over a plate the shock loads to d0 at its point r0 m from the burst, falling as 1/R out to
    /// the weep, over the patch of it that reaches (at most its area): cm², by rings.</summary>
    static double Seams(double d0, double r0, double areaM2, Joint joint, SimSettings s)
    {
        double rw = r0 * d0 / s.LeakWeepD, disc = Math.PI * (rw * rw - r0 * r0);
        if (!(disc > 1e-6))
            return 0;
        double sum = 0, k = Math.Pow(rw / r0, 1.0 / Rings);
        for (int i = 0; i < Rings; i++)
        {
            double ra = r0 * Math.Pow(k, i), rb = ra * k;
            sum += Blast.LeakRatio(d0 * r0 / Math.Sqrt(ra * rb), joint, s) * Math.PI * (rb * rb - ra * ra);
        }
        return sum * Math.Min(1, areaM2 / disc);
    }

    const int Rings = 6;

    /// <summary>A chance rising from 0 at a band's lower edge to 1 at its upper.</summary>
    static double Ramp(double x, double lo, double hi) => Math.Clamp((x - lo) / (hi - lo), 0, 1);

    /// <summary>A draw at a chance (none drawn at 0 or 1).</summary>
    static bool Chance(double p, ref RandomStream r) => p > 0 && (p >= 1 || r.NextDouble() < p);

    /// <summary>Whether a part's box lies in one of a room's spaces.</summary>
    static bool InRoom(ShipClass c, int room, in Box box)
    {
        foreach (int cell in c.Links(c.Rooms[room].Cells))
            if (Effects.Overlaps(c.Cells[cell].Box, box))
                return true;
        return false;
    }

    static double Distance(P3 a, P3 b)
    {
        double dx = a.X - b.X, dy = a.Y - b.Y, dz = a.Z - b.Z;
        return Math.Max(Math.Sqrt(dx * dx + dy * dy + dz * dz), 0.5);
    }

    /// <summary>The point of a box nearest p.</summary>
    static P3 Nearest(in Box b, P3 p) => new(Math.Clamp(p.X, b.X0, b.X1), Math.Clamp(p.Y, b.Y0, b.Y1), Math.Clamp(p.Z, b.Base, b.Top));
}
