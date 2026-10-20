using Fleetwright.Shipgen;
using Fleetwright.Sim.Core;
using Fleetwright.Sim.Ships;

namespace Fleetwright.Sim.Combat;

/// <summary>A near miss (docs/battle/shells-armour-damage.md E7; research docs/battle/damage/15_near_misses.md,
/// <c>near_miss_ref.py</c>): a shell that missed bursts in the water, outside every hull, and a ship close by takes what
/// it throws. There is no terminal walk: the effects start from the burst.
/// <list type="bullet">
/// <item><b>The burst</b> (<see cref="Burst"/>, 15 §1): the fuze, not the calibre, decides where. A nose fuze fires as
/// the shell meets the sea, its <c>FuzeWaterMs</c> later: a few centimetres down. A base fuze's delay runs from the
/// water (<see cref="SimSettings.FuzeStartsOnWater"/>): metres down, unless its run met a hull first (P6's underwater
/// hit, the shell system's). Blind, not firing on the water, a dud filler, or a base fuze skipping off a shallow fall:
/// nothing.</item>
/// <item><b>Who's near</b> (<see cref="ReachM"/>, 15 §6.1): the ships whose waterplane lies within max(15·W^⅓,
/// 7.5·√W) m.</item>
/// <item><b>Splinters</b> (<see cref="Splinters"/>, 15 §2, §6.3): E1's field (<see cref="FragmentBurst.Hits"/>, the ring
/// leaning with the shell) on the hull's side, patch by patch, and on the parts standing above it that the side doesn't
/// hide, each through its plates; the water over the burst slows every piece on its way up as
/// <see cref="SimSettings.WaterDragL1"/> says (a few centimetres choke the fast low ones, a metre stops them all), and
/// down is gone. What gets through the side strikes the space behind it (its vitals, the parts in it, its men); the
/// side's waterline band holed by what gets through it; the parts above, and their men, as a burst's pieces do.</item>
/// <item><b>Shock</b> (<see cref="Shock"/>, 15 §3, §6.4): the near-miss shock factor (<see cref="Nsf"/>) at the nearest
/// wetted point of the hull, by its bands: the gear, the machinery and the structure, each as far as its own shock
/// reaches. A surface burst's is cut off by the sea's surface almost at once; an AP shell's, metres down, isn't.</item>
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

    /// <summary>A near miss on a ship: the burst at the hit's point (ship frame, in the water outside the hull), the shell
    /// flying along its direction at its speed, its filler's force on the hit (<see cref="Hit.Force"/>). A tool may pass
    /// a span by component to have the splinters expected into each added to it.</summary>
    public static EffectsResult Apply(ShipClass c, ShipCondition k, in Hit hit, SimSettings s, ref RandomStream r, Span<double> intoParts = default)
    {
        var res = new EffectsResult { Part = -1, MagazineRoom = -1 };
        Casualties.Begin(k);
        var water = HitQuery.WaterOf(c, k.Attitude);
        var p = hit.Point;
        double depth = Math.Max(water.At(p.X, p.Y) - p.Z, 0), surface = p.Z + depth;
        double w = Math.Max(hit.Shell.FillerKg * hit.Shell.FillerRe * hit.Force, 0);
        // the shock's nearest wetted point first: the splinters' holes in its space count toward a contact hole there
        double nsf = Nearest(c, k, p, surface, w, water, s, out int nearCell, out var nearAt, out double nearMm);
        double nearHoles = 0;
        if (hit.Shell.Fragments is { } f)
        {
            var frag = f.Of(hit.Force < 1);
            // a burst deeper than the heaviest piece gets through at a twentieth of its speed straight up throws nothing
            if (depth < ChokeE * s.WaterDragL1 * Math.Cbrt(Math.Max(frag.MassAt(1), Fragments.MinKg)))
                Splinters(c, k, hit, frag, depth, ReachM(hit.Shell, s), nearCell, ref nearHoles, s, ref res, ref r, intoParts);
        }
        if (w > 0)
            Shock(c, k, hit, w, surface, water, nsf, nearCell, nearAt, nearMm, nearHoles, s, ref res, ref r);
        Casualties.Draw(c, k, hit.Ms, s, ref res, ref r);
        return res;
    }

    /// <summary>e-foldings of a piece's speed in the water past which it's spent (e^−3: a twentieth).</summary>
    const double ChokeE = 3;

    // ------------------------------------------------------------------ splinters

    /// <summary>The burst's pieces on the ship (15 §6.3): the hull's side toward the burst in patches (the waterline band
    /// apart, a metre below it, then up to the deck in steps a third of the distance, at most 3 m high and 6 m long), each
    /// a slab of its plating and any belt over it there; what gets through each patch into the space behind it, holing the
    /// band; the space's vitals, the parts wholly inside it and its men by what gets through its side; then the parts
    /// standing above the side, below the line from the burst over the deck edge hidden by the hull, each through its own
    /// plates, and their men.</summary>
    static void Splinters(ShipClass c, ShipCondition k, in Hit hit, FragmentBurst frag, double depth, double reach, int nearCell, ref double nearHoles,
        SimSettings s, ref EffectsResult res, ref RandomStream r, Span<double> intoParts)
    {
        var p = hit.Point;
        var d = hit.Direction;
        double v = hit.SpeedMs, surface = p.Z + depth;
        var water = HitQuery.WaterOf(c, k.Attitude);
        var cells = c.Cells;
        var comps = c.Components;
        var pl = c.Plating;
        var plating = c.MaterialOf(pl.Material);

        // the hull's side, cell by cell: the face toward the burst (port, 0, or starboard, 1)
        int face = p.Y < 0 ? 0 : 1;
        double sg = face == 0 ? -1 : 1;
        for (int ci = 0; ci < cells.Length; ci++)
        {
            if (!(Blast.Outside(c, k, ci, face, out _, out _) > 0))
                continue;
            var b = cells[ci].Box;
            double xm = Math.Clamp(p.X, b.X0, b.X1);
            double wl = water.At(xm, sg * c.Hull.HalfWidth(xm, b.Top));
            double z0 = Math.Max(b.Base, wl - s.NearMissBandM), z1 = b.Top;
            if (z1 <= z0)
                continue;
            // the burst must be outboard of the side to see it; how far the face is from it
            double hwNear = c.Hull.HalfWidth(xm, Math.Clamp(p.Z, z0, z1));
            if (!(sg * p.Y > hwNear) || !(hwNear > 0.5))
                continue;
            double zn = Math.Clamp(p.Z, z0, z1), dxn = p.X - xm, dyn = p.Y - sg * hwNear, dzn = p.Z - zn;
            double near = Math.Sqrt(dxn * dxn + dyn * dyn + dzn * dzn);
            if (near > reach)
                continue;
            double step = Math.Max(near / 3, 1), dxp = Math.Min(step, 6), dzp = Math.Min(step, 3);
            int nx = Math.Max(1, (int)Math.Ceiling((b.X1 - b.X0) / dxp));
            double cellLambda = 0;
            for (int ix = 0; ix < nx; ix++)
            {
                double x0 = b.X0 + (b.X1 - b.X0) * ix / nx, x1 = b.X0 + (b.X1 - b.X0) * (ix + 1) / nx, xc = (x0 + x1) / 2;
                double side = Mild(c, plating, pl, xc);
                for (double za = z0; za < z1 - 1e-6;)
                {
                    // the band's edges, then steps
                    double zb = Math.Min(z1, za + dzp);
                    if (za < wl && zb > wl)
                        zb = wl;
                    else if (za < wl + s.NearMissBandM && zb > wl + s.NearMissBandM)
                        zb = wl + s.NearMissBandM;
                    double zc = (za + zb) / 2, hw = c.Hull.HalfWidth(xc, zc);
                    if (hw > 0.5)
                    {
                        var slab = new Box(x0, x1, sg * hw - 0.05, sg * hw + 0.05, za, zb);
                        var at = new P3(xc, sg * hw, zc);
                        double mild = side + Belt(c, xc, zc);
                        double drag = WaterDragM(p, surface, at, s);
                        double lambda = frag.Hits(p, d, v, slab, mild, new P3(0, sg, 0), drag);
                        if (lambda > 1e-4)
                        {
                            cellLambda += lambda;
                            // the waterline band: holes at their height, each a ragged splinter's
                            if (zb <= wl + s.NearMissBandM + 1e-6)
                            {
                                int n = Poisson(lambda, ref r);
                                if (n > 0)
                                {
                                    double dx = at.X - p.X, dy = at.Y - p.Y, dz = at.Z - p.Z, rr = Math.Sqrt(dx * dx + dy * dy + dz * dz);
                                    double ob = double.RadiansToDegrees(Math.Acos(Math.Clamp(Math.Abs(dy) / Math.Max(rr, 1e-6), 0, 1)));
                                    double a = n * s.SplinterHoleK * frag.ThroughAreaM2(rr + drag, v, mild, ob);
                                    k.Hole(ci, a, at, n);
                                    res.BreachM2 += a;
                                    if (ci == nearCell)
                                        nearHoles += a;
                                }
                            }
                        }
                    }
                    za = zb;
                }
            }
            res.SideThrough += cellLambda;
            if (cellLambda > 1e-4)
                Behind(c, k, hit, frag, ci, sg, cellLambda, Mild(c, plating, pl, (b.X0 + b.X1) / 2), surface, s, ref res, ref r);
        }

        // the parts above the side, as far as the hull doesn't hide them
        for (int i = 0; i < comps.Length; i++)
        {
            ref readonly var comp = ref comps[i];
            if (comp.Bounds.Top <= 0.05)
                continue;   // inside the hull: the side's (above)
            if (!Seen(c, comp.Bounds, p, water, out var box))
                continue;
            if (Effects.BoxDistance(box, p) > reach)
                continue;
            double raw = Effects.Armour(c, i), mild = Effects.PartMild(c, i, raw);
            var mid = new P3((box.X0 + box.X1) / 2, (box.Y0 + box.Y1) / 2, (box.Base + box.Top) / 2);
            double drag = WaterDragM(p, surface, Nearest(box, p), s);
            double into = frag.Hits(p, d, v, box, mild, default, drag);
            res.UpperThrough += into;
            if (intoParts.Length > i)
                intoParts[i] += into;
            double u = into > 1e-4 ? r.NextDouble() : 1;
            bool pierced = u < 1 - Math.Exp(-into), struck = u < 1 - Math.Exp(-into * Effects.PartVital(c, i, s));
            if (raw > 0 && !pierced)
                struck = false;
            if (struck)
            {
                Effects.Harm(ref res, k, i);
                if (res.Part < 0)
                    res.Part = i;
            }
            // its men: an open or shielded mount's stand open; the floor hidden by the side, none. In the open they meet the
            // free field (16 §4.2: S is the flux a man meets inside a steel compartment, ricochets and debris; 13's field is 1)
            if (comp.BattleCrew > 0 && box.Base <= comp.Bounds.Base + StandM && Casualties.Men(c, k, Casualties.Part(i)) > 0)
            {
                int m = c.MountOf(i);
                double crewMild = Effects.PartMild(c, i, m >= 0 && c.Mounts[m].Component == i && !Casualties.Enclosed(c, m) ? 0 : raw);
                var (pk, pc) = Casualties.PiecesRisk(comp.Bounds, Casualties.Cover(c, Casualties.Part(i), s), frag, p, d, v, crewMild > 0 ? 1 : 1 / s.CasualtyS,
                    crewMild, default, 0, s, WaterDragM(p, surface, mid, s));
                Casualties.Add(k, Casualties.Part(i), pk, pc);
            }
        }
    }

    /// <summary>What got through a space's side (λ pieces): its vitals hit at their share, the parts wholly inside it struck
    /// through the side and their own plates, and its men.</summary>
    static void Behind(ShipClass c, ShipCondition k, in Hit hit, FragmentBurst frag, int cell, double sg, double lambda, double sideMild,
        double surface, SimSettings s, ref EffectsResult res, ref RandomStream r)
    {
        var p = hit.Point;
        var box = c.Cells[cell].Box;
        foreach (int room in c.RoomsIn(cell))
        {
            int node = c.RoomNode(room);
            if (r.NextDouble() < 1 - Math.Exp(-lambda * Effects.Vital(c, room, s)))
            {
                Effects.Harm(ref res, k, node);
                if (res.Part < 0)
                    res.Part = node;
            }
        }
        var normal = new P3(0, sg, 0);
        var comps = c.Components;
        for (int j = 0; j < comps.Length; j++)
        {
            ref readonly var comp = ref comps[j];
            if (comp.Bounds.Top > 0.05 || !Effects.Overlaps(comp.Bounds, box))
                continue;
            double into = frag.Hits(p, hit.Direction, hit.SpeedMs, comp.Bounds, sideMild + Effects.PartMild(c, j, Effects.Armour(c, j)), normal,
                WaterDragM(p, surface, Nearest(comp.Bounds, p), s));
            if (into > 1e-4 && r.NextDouble() < 1 - Math.Exp(-into * Effects.PartVital(c, j, s)))
                Effects.Harm(ref res, k, j);
        }
        int g = Casualties.Cell(c, cell);
        if (Casualties.Men(c, k, g) > 0)
        {
            var mid = new P3((box.X0 + box.X1) / 2, (box.Y0 + box.Y1) / 2, (box.Base + box.Top) / 2);
            var (pk, pc) = Casualties.PiecesRisk(box, Casualties.Cover(c, g, s), frag, p, hit.Direction, hit.SpeedMs, 1, sideMild + Belt(c, mid.X, mid.Z),
                normal, 0, s, WaterDragM(p, surface, mid, s));
            Casualties.Add(k, g, pk, pc);
        }
    }

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
        if (!(Blast.Outside(c, k, cell, face, out mm, out _) > 0))
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

    /// <summary>The outside faces that can be wet: the sides and the bottom.</summary>
    static readonly int[] WetFaces = [0, 1, 3];   // an array: a constant span literal allocates in unoptimised code (RuntimeHelpers.CreateSpan)

    /// <summary>The ship's shock factor: the most of any wetted face's at its nearest point (15 §6.4: one number per ship),
    /// that face's space, point and plate.</summary>
    static double Nearest(ShipClass c, ShipCondition k, P3 p, double surface, double w, in Waterplane water, SimSettings s, out int cell, out P3 at,
        out double mm)
    {
        (cell, at, mm) = (-1, default, 0);
        double best = 0;
        if (!(w > 0))
            return 0;
        for (int i = 0; i < c.Cells.Length; i++)
            foreach (int face in WetFaces)
                if (WetFace(c, k, i, face, p, water, s, out var pt, out _, out double plate) && Nsf(w, p, surface, pt) is double n && n > best)
                    (best, cell, at, mm) = (n, i, pt, plate);
        return best;
    }

    /// <summary>What a near miss's shock does (15 §6.4; its bands, <see cref="SimSettings.NearShockBands"/>):
    /// <list type="bullet">
    /// <item><b>the structure:</b> every wetted face's seams by r(D) over the patch where D (5·NSF, scaled by its plate
    /// against <see cref="SimSettings.ShockPlateMm"/>) is past the weep, falling as 1/R from its nearest point; the
    /// transverse bulkheads under the sea likewise, by their own plate; at D 5 on the nearest face a contact burst's hole
    /// there (less what the splinters holed it already: the larger, not both);</item>
    /// <item><b>the machinery:</b> each boiler and engine room by its own NSF: its gear damaged across the castings' band
    /// (times the grade), the shafts through it strained across the hull's, the room again across the buckling's;</item>
    /// <item><b>the gear</b> (with director fire control, E4's: breakers to trip): across the fragile band the section's
    /// power round the nearest point out and the directors' gyros rolling E4's failure, across the castings' the whole ship's breakers, each
    /// for a while;</item>
    /// <item><b>the stern:</b> a burst near it shakes the rudders, shafts and propellers by their own NSF across the hull's
    /// band (Ilex, Fortune).</item>
    /// </list></summary>
    static void Shock(ShipClass c, ShipCondition k, in Hit hit, double w, double surface, in Waterplane water, double nsf, int nearCell, P3 nearAt,
        double nearMm, double nearHoles, SimSettings s, ref EffectsResult res, ref RandomStream r)
    {
        var p = hit.Point;
        var b = s.NearShockBands;
        long now = hit.Ms;
        res.Nsf = nsf;
        double grade = s.NearShockGradeK[Math.Min((int)c.ShockGrade, s.NearShockGradeK.Length - 1)], gear = nsf / grade;

        // the structure: the shell's seams, face by face
        if (5 * nsf * s.ShockPlateMm / Math.Max(nearMm, 1) >= s.LeakWeepD)
            for (int i = 0; i < c.Cells.Length; i++)
                foreach (int face in WetFaces)
                {
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
        // the contact rule at D 5 on the nearest face (11 §3.1), less the splinters' holes there
        if (nearCell >= 0 && 5 * nsf * s.ShockPlateMm / Math.Max(nearMm, 1) >= 5)
        {
            double hole = s.HoleRadiusK * Math.Cbrt(w) * Math.Sqrt(6 / Math.Max(nearMm, 1)), more = Math.PI * hole * hole - nearHoles;
            if (more > 0)
            {
                k.Hole(nearCell, more, nearAt);
                (res.BreachM2, res.ShockM2) = (res.BreachM2 + more, res.ShockM2 + more);
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
        for (int room = 0; room < c.Rooms.Length; room++)
        {
            if (c.Rooms[room].Kind is not (RoomKind.BoilerRoom or RoomKind.EngineRoom))
                continue;
            double n = 0;
            foreach (int cell in c.Links(c.Rooms[room].Cells))
                n = Math.Max(n, Nsf(w, p, surface, Nearest(c.Cells[cell].Box, p)));
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

        // the gear: breakers and the directors' gyros (with something to trip)
        if (NonPen.Electrics(c))
        {
            if (Chance(Ramp(gear, b[0], b[1]), ref r))
                res.Shock |= Effects.PowerOut(c, k, nearAt, -1, -1, s.ShockZoneBM, NonPen.Minutes(s.NearTripMin, ref r), now);
            if (Chance(Ramp(gear, b[1], b[2]), ref r))
            {
                res.Shock |= Effects.PowerOut(c, k, nearAt, -1, -1, double.PositiveInfinity, NonPen.Minutes(s.NearTripMin, ref r), now);
                res.Blackout = true;
            }
            // the gyros roll a failure (15 §6.4) at E4's chance for a heavy shock, the grade already in the edges
            if (c.Directors.Length > 0 && Chance(Ramp(gear, b[0], b[1]) * s.ShockDirectorGyro[0], ref r))
            {
                long ms = NonPen.Minutes(s.DirectorGyroMin, ref r);
                for (int d = 0; d < c.Directors.Length; d++)
                    k.Out(k.DirectorDown, d, now, ms);
                res.Shock = true;
            }
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

    /// <summary>A man's height over a floor: a part whose floor the side hides by more has no men in the burst's sight.</summary>
    const double StandM = 1.8;

    /// <summary>The hull's own side plating at x as mild steel against fragments: amidships or at the ends.</summary>
    static double Mild(ShipClass c, in MaterialRow plating, in PlatingRow pl, double x) =>
        plating.FragmentMildMm(x >= pl.MidX0 && x <= pl.MidX1 ? pl.ShellMm : pl.ShellEndMm);

    /// <summary>The side armour over the hull's side at (x, z), as mild steel against fragments (0: none there).</summary>
    static double Belt(ShipClass c, double x, double z)
    {
        double mm = 0;
        foreach (ref readonly var sp in c.SidePlates)
        {
            if (x < sp.X0 || x > sp.X1 || z < sp.Bottom || z > sp.Top)
                continue;
            double t = !double.IsNaN(sp.BottomMm) && !double.IsNaN(sp.TaperFrom) && z < sp.TaperFrom && sp.TaperFrom > sp.Bottom
                ? sp.BottomMm + (sp.ThicknessMm - sp.BottomMm) * Math.Clamp((z - sp.Bottom) / (sp.TaperFrom - sp.Bottom), 0, 1)
                : sp.ThicknessMm;
            mm = Math.Max(mm, c.MaterialOf(sp.Material).FragmentMildMm(t));
        }
        return mm;
    }

    /// <summary>The part of a box a burst at p sees over the hull (15 §2.4: the leaning band clears a low side and hits what
    /// stands above it): above the sea, and above the line from the burst over the deck edge on its side at the box's x
    /// (outboard of the edge, all of it). False when none of it shows.</summary>
    static bool Seen(ShipClass c, in Box b, P3 p, in Waterplane water, out Box seen)
    {
        double xm = Math.Clamp(p.X, b.X0, b.X1), sg = p.Y < 0 ? -1 : 1;
        double deck = c.Hull.DeckTop(xm), edge = c.Hull.HalfWidth(xm, deck);
        double inboard = edge - (sg > 0 ? b.Y1 : -b.Y0);   // how far inboard of the edge the box's near side stands
        double outboard = sg * p.Y - edge;                  // how far outboard of it the burst is
        double zmin = water.At(xm, sg * edge);
        if (inboard > 0 && outboard > 0.05)
            zmin = Math.Max(zmin, deck + (deck - p.Z) * inboard / outboard);
        seen = b with { Base = Math.Max(b.Base, zmin) };
        return seen.Top > seen.Base + 0.05;
    }

    /// <summary>The water a piece goes through from a burst at p (the sea's surface at surfaceZ) to a point, as the air that
    /// slows it as much, over the air it's counted in already: the water's share of the way (the depth over the sine of
    /// its elevation; all of it to a point under the sea) times L₁ over the water's L₁, less one.</summary>
    static double WaterDragM(P3 p, double surfaceZ, P3 to, SimSettings s)
    {
        double dx = to.X - p.X, dy = to.Y - p.Y, dz = to.Z - p.Z, rr = Math.Sqrt(dx * dx + dy * dy + dz * dz);
        double depth = surfaceZ - p.Z;
        double wet = to.Z < surfaceZ || dz <= 1e-6 ? rr : Math.Min(depth * rr / dz, rr);
        return wet * (Fragments.DragL1 / s.WaterDragL1 - 1);
    }

    /// <summary>The point of a box nearest p.</summary>
    static P3 Nearest(in Box b, P3 p) => new(Math.Clamp(p.X, b.X0, b.X1), Math.Clamp(p.Y, b.Y0, b.Y1), Math.Clamp(p.Z, b.Base, b.Top));

    /// <summary>A Poisson count of mean λ (Knuth's product under 30; a rounded normal over it).</summary>
    static int Poisson(double lambda, ref RandomStream r)
    {
        if (lambda >= 30)
            return Math.Max(0, (int)Math.Round(lambda + Math.Sqrt(lambda) * r.NextGaussian()));
        double l = Math.Exp(-lambda), prod = r.NextDouble();
        int n = 0;
        while (prod > l)
        {
            n++;
            prod *= r.NextDouble();
        }
        return n;
    }
}
