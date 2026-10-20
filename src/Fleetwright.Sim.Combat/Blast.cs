using Fleetwright.Shipgen;
using Fleetwright.Sim.Core;
using Fleetwright.Sim.Ships;

namespace Fleetwright.Sim.Combat;

/// <summary>What a burst's gas did in a closed space (<see cref="Blast.Cascade"/>): how many spaces it reached and their
/// gas volume, the pressure at first and at the end and what the burst space's boundaries felt (MPa), the boundaries
/// that ruptured, the doors it blew and those it deformed, and the openings it left to the flooding: between spaces
/// (torn and blown open, m²; sprung seams, m²) and to the sea or air (torn and sprung, m²). Open: it vented as fast as it formed (no gas load at all).</summary>
public struct BlastResult
{
    public int Spaces, Ruptured, Doors, Deformed, Rounds;
    public double VolumeM3, FirstMPa, FeltMPa, LastMPa, OpenM2, LeakM2, HoleM2;
    public bool Open;
}

/// <summary>A burst in a closed space (docs/battle/shells-armour-damage.md E5; research docs/battle/damage/
/// 11_blast_confinement.md, <c>blast_confinement_ref.py</c>): after the shock, the hot gas stays until it vents or the
/// walls give way, a static load on every boundary at once.
/// <list type="bullet">
/// <item><b>The pressure</b> (<see cref="Pressure"/>, 11 §2.1): 0.28·η·[Q_d + φ·(Q_c − Q_d)]·W/V, the afterburn φ as
/// the space's air allows;</item>
/// <item><b>what a panel feels</b> (<see cref="Felt"/>, 11 §1.2): the pressure times min(1.3, π·t/15 ms), t its blowdown
/// through the space's vents; nothing once the vents are as big as the space's face (open);</item>
/// <item><b>the cascade</b> (<see cref="Cascade"/>, 11 §4.1): over the cell graph from the burst's space, every boundary
/// over its rupture pressure fails at once (0.06 MPa a mm, riveted ×0.85), a watertight door at half its bulkhead's;
/// a failed boundary between spaces joins the next space to the gas (the pressure falls), one to the outside vents it
/// (and holes it to the flooding); round on round until nothing more gives. Deformed boundaries leak (damage/17's
/// r(D)).</item>
/// </list>
/// Allocation-free: the region and its boundaries' marks live on the ship's condition. The draws come from the stream
/// it's given.</summary>
public static class Blast
{
    /// <summary>The gas pressure (gauge, MPa) of w kg of a shell's filler in a closed space of v m³ of gas (its air too).</summary>
    public static double Pressure(in ShellTypeRow f, double w, double v, SimSettings s) =>
        Pressure(f.FillerQDet, f.FillerQComb, f.FillerAirM3Kg, f.FillerEta, w, v, s);

    /// <summary>The gas pressure (gauge, MPa) of w kg of an explosive of these heats (MJ/kg), air (m³/kg) and gas share η
    /// in v m³: the afterburn share φ = 1/(1 + w·air/v) (11 §2.1).</summary>
    public static double Pressure(double qDet, double qComb, double air, double eta, double w, double v, SimSettings s)
    {
        if (!(w > 0) || !(v > 0))
            return 0;
        double phi = air > 0 ? 1 / (1 + w * air / v) : 0;
        return s.BlastHeatK * eta * (qDet + phi * Math.Max(qComb - qDet, 0)) * w / v;
    }

    /// <summary>What a boundary feels of a gas pressure p in v m³ vented by a m² (MPa, 11 §1.2): p times the load factor
    /// of its blowdown against a panel's period; 0 when open.</summary>
    public static double Felt(double p, double v, double a, SimSettings s)
    {
        if (!(p > 0))
            return 0;
        if (Open(v, a, s))
            return 0;
        double t = a > 0 ? 1000 * s.BurnStretch * v / (s.VentC * s.VentAlpha * a) * Math.Log((p + s.AtmosphereMPa) / s.AtmosphereMPa) : double.PositiveInfinity;
        return p * Math.Min(s.BlastLoadFactor, Math.PI * t / s.PanelMs);
    }

    /// <summary>Whether a space vented by a m² is open: no gas pressure builds in it (α·A ≥ 0.6·V^⅔).</summary>
    public static bool Open(double v, double a, SimSettings s) => s.VentAlpha * a >= s.OpenVentRatio * Math.Pow(Math.Max(v, 1e-6), 2.0 / 3);

    /// <summary>A space's gas: its volume's free share (its permeability) less the water in it, at least a tenth.</summary>
    public static double GasM3(ShipClass c, ShipCondition k, int cell)
    {
        ref readonly var x = ref c.Cells[cell];
        double v = x.VolumeM3 * Math.Clamp(x.Permeability, 0.05, 1);
        return Math.Max(v - k.WaterM3[cell], 0.1 * v);
    }

    /// <summary>A space's vents to the outside: its room kind's share of V^⅔ (uptakes, vent trunks; settings until the
    /// designer emits them), and the holes already in it.</summary>
    public static double VentM2(ShipClass c, ShipCondition k, int cell, SimSettings s)
    {
        ref readonly var x = ref c.Cells[cell];
        double share = x.Room >= 0 ? s.RoomVentShare[Math.Min((int)c.Rooms[x.Room].Kind, s.RoomVentShare.Length - 1)] : s.RoomVentShare[0];
        return share * Math.Pow(Math.Max(x.VolumeM3, 1e-6), 2.0 / 3) + k.BreachM2[cell];
    }

    /// <summary>The outside faces of a space: its side to port and starboard (where its box reaches the hull's side), its
    /// top when it's the weather deck, its bottom when it's the keel's tier.</summary>
    const int Faces = 4;

    /// <summary>The gas of w kg of a shell's filler bursting at a point in a space (11 §4.1, §5.1): the near zone first (a
    /// boundary of the space within it holed nearHoleM in radius, the next space joining the gas through the hole), then the
    /// cascade; ventM2 more vent from the start (a near-wall burst's own hole). It leaves on the condition: openings
    /// between spaces (<see cref="ShipCondition.JoinOpenM2"/>: ruptured boundaries a share of their area, blown doors,
    /// deformed ones' seam leaks), holes to the sea or air, and what each reached space felt
    /// (<see cref="ShipCondition.BlastFelt"/>, stamped <see cref="ShipCondition.BlastId"/>).</summary>
    public static BlastResult Cascade(ShipClass c, ShipCondition k, int cell, P3 at, double w, in ShellTypeRow f, double ventM2, double nearM,
        double nearHoleM, SimSettings s, ref RandomStream r)
    {
        var res = new BlastResult();
        int id = ++k.BlastId, n = 0;
        double v = 0, a = ventM2;
        var cells = c.Cells;

        Join(c, k, cell, id, ref n, ref v, ref a, s);
        // the near zone: the space's boundaries within it holed through (by the shock and fragments, at once); the gas
        // needs tens of ms to fill the next space through them, longer than a panel takes to load, so the burst space's
        // own walls feel its pressure first and the holed spaces join after the first round, as through a blown door
        var b0 = cells[cell].Box;
        var pool = cells[cell].Neighbours;
        for (int j = pool.Start; j < pool.End; j++)
        {
            ref readonly var e = ref c.Neighbour(j);
            if (k.BlastStamp[e.Cell] == id || e.Join == JoinKind.Open || FaceDistance(b0, cells[e.Cell].Box, e.Join, at) > nearM)
                continue;
            double hole = Math.Min(Math.PI * nearHoleM * nearHoleM, Area(b0, cells[e.Cell].Box, e.Join));
            (k.JoinStamp[j], k.JoinFate[j], k.JoinFelt[j]) = (id, Fate.Holed, 0);
            Open(c, k, cell, j, hole);
            res.OpenM2 += hole;
        }

        for (int round = 0; round < 32; round++)
        {
            // spaces joined open (one room over several cells) are one space: the gas is in all of them at once
            for (int i = 0; i < n; i++)
            {
                var on = cells[k.BlastRegion[i]].Neighbours;
                for (int j = on.Start; j < on.End; j++)
                    if (c.Neighbour(j).Join == JoinKind.Open && k.BlastStamp[c.Neighbour(j).Cell] != id)
                        Join(c, k, c.Neighbour(j).Cell, id, ref n, ref v, ref a, s);
            }
            double p = Pressure(f, w, v, s), felt = Felt(p, v, a, s);
            if (round == 0)
                (res.FirstMPa, res.FeltMPa, res.Open) = (p, felt, felt <= 0);
            res.LastMPa = p;
            res.Rounds = round + 1;
            for (int i = 0; i < n; i++)
                if (double.IsNaN(k.BlastFelt[k.BlastRegion[i]]))
                    k.BlastFelt[k.BlastRegion[i]] = felt;
            if (felt <= 0)
                break;
            bool changed = false;
            for (int i = 0, n0 = n; i < n0; i++)
            {
                int ci = k.BlastRegion[i];
                var box = cells[ci].Box;
                var nb = cells[ci].Neighbours;
                for (int j = nb.Start; j < nb.End; j++)
                {
                    ref readonly var e = ref c.Neighbour(j);
                    if (k.BlastStamp[e.Cell] == id)
                        continue;
                    if (e.Join == JoinKind.Open)
                        continue;   // joined at the round's start
                    if (k.JoinStamp[j] == id && k.JoinFate[j] == Fate.Holed)
                    {
                        Join(c, k, e.Cell, id, ref n, ref v, ref a, s);   // holed in the near zone: the gas through the hole
                        changed = true;
                        continue;
                    }
                    if (k.JoinStamp[j] != id)
                        (k.JoinStamp[j], k.JoinFate[j], k.JoinFelt[j]) = (id, Fate.None, 0);
                    k.JoinFelt[j] = Math.Max(k.JoinFelt[j], felt);
                    var other = cells[e.Cell].Box;
                    double mm = JoinMm(c, e, box, other), rupture = s.RuptureMPaMm * mm * Rivets(c.InternalsJoint, s);
                    if (felt >= rupture)
                    {
                        k.JoinFate[j] = Fate.Ruptured;
                        double open = s.RupturedShare * Area(box, other, e.Join);
                        Open(c, k, ci, j, open);
                        (res.OpenM2, res.Ruptured) = (res.OpenM2 + open, res.Ruptured + 1);
                        Join(c, k, e.Cell, id, ref n, ref v, ref a, s);
                        changed = true;
                    }
                    else if (Door(c, e, ci) && felt >= Math.Max(s.DoorMinMPa, s.DoorShare * rupture))
                    {
                        k.JoinFate[j] = Fate.Door;
                        Open(c, k, ci, j, s.DoorM2);
                        (res.OpenM2, res.Doors) = (res.OpenM2 + s.DoorM2, res.Doors + 1);
                        Join(c, k, e.Cell, id, ref n, ref v, ref a, s);
                        changed = true;
                    }
                }
                // its outside faces: torn open to the sea or the air, a vent for the gas and a hole for the flooding
                for (int face = 0; face < Faces; face++)
                {
                    double area = Outside(c, k, ci, face, out double mm, out var point);
                    if (!(area > 0) || k.FaceStamp[ci * Faces + face] == id && k.FaceFate[ci * Faces + face] == Fate.Ruptured)
                        continue;
                    if (k.FaceStamp[ci * Faces + face] != id)
                        (k.FaceStamp[ci * Faces + face], k.FaceFate[ci * Faces + face], k.FaceFelt[ci * Faces + face]) = (id, Fate.None, 0);
                    k.FaceFelt[ci * Faces + face] = Math.Max(k.FaceFelt[ci * Faces + face], felt);
                    if (felt < s.RuptureMPaMm * mm * Rivets(c.ShellJoint, s))
                        continue;
                    k.FaceFate[ci * Faces + face] = Fate.Ruptured;
                    double hole = s.RupturedShare * area;
                    k.Hole(ci, hole, point);
                    (res.HoleM2, res.Ruptured) = (res.HoleM2 + hole, res.Ruptured + 1);
                    a += area;
                    changed = true;
                }
            }
            if (!changed)
                break;
        }

        // what held but deformed: its seams sprung, a leak by r(D), D = felt / deform (damage/17 §3.2)
        for (int i = 0; i < n; i++)
        {
            int ci = k.BlastRegion[i];
            var box = cells[ci].Box;
            var nb = cells[ci].Neighbours;
            for (int j = nb.Start; j < nb.End; j++)
            {
                if (k.JoinStamp[j] != id || k.JoinFate[j] != Fate.None)
                    continue;
                ref readonly var e = ref c.Neighbour(j);
                var other = cells[e.Cell].Box;
                k.JoinFate[j] = Fate.Deformed;   // once, from whichever side
                double d = k.JoinFelt[j] / (s.DeformMPaMm * JoinMm(c, e, box, other) * Rivets(c.InternalsJoint, s)), area = Area(box, other, e.Join);
                double leak = Leak(d, area, c.InternalsJoint, s, ref r);
                leak += Leaks.Sprung(c, k, ci, j, leak, d, area, s, ref r);   // its seams sprung and its closures loosened (DC1)
                if (leak > 0)
                    (res.LeakM2, res.Deformed) = (res.LeakM2 + leak, res.Deformed + 1);
            }
            for (int face = 0; face < Faces; face++)
            {
                int fi = ci * Faces + face;
                if (k.FaceStamp[fi] != id || k.FaceFate[fi] != Fate.None)
                    continue;
                double area = Outside(c, k, ci, face, out double mm, out var point);
                k.FaceFate[fi] = Fate.Deformed;
                double leak = Leak(k.FaceFelt[fi] / (s.DeformMPaMm * mm * Rivets(c.ShellJoint, s)), area, c.ShellJoint, s, ref r);
                if (leak > 0)
                {
                    k.Hole(ci, leak, point, seam: true);
                    (res.HoleM2, res.Deformed) = (res.HoleM2 + leak, res.Deformed + 1);
                }
            }
        }
        res.Spaces = n;
        res.VolumeM3 = v;
        return res;
    }

    /// <summary>A space joins the gas: marked, its felt pressure to come, its gas and vents added.</summary>
    static void Join(ShipClass c, ShipCondition k, int i, int id, ref int n, ref double v, ref double a, SimSettings s)
    {
        k.BlastStamp[i] = id;
        k.BlastFelt[i] = double.NaN;
        k.BlastBefore[i] = v;
        k.BlastRegion[n++] = i;
        v += GasM3(c, k, i);
        a += VentM2(c, k, i, s);
    }

    /// <summary>Whether a space's top is the weather deck: no space over it.</summary>
    static bool Weather(ShipClass c, int cell)
    {
        var b = c.Cells[cell].Box;
        foreach (ref readonly var e in c.Neighbours(c.Cells[cell].Neighbours))
            if (c.Cells[e.Cell].Box.Base >= b.Top - 0.05)
                return false;
        return true;
    }

    /// <summary>The marks a boundary gets in a cascade.</summary>
    internal static class Fate
    {
        public const byte None = 0, Holed = 1, Ruptured = 2, Door = 3, Deformed = 4;
    }

    /// <summary>A deformed boundary's seam leak, m² (damage/17 leakage §3.2): r(D) cm² per m² of it, lognormal about the
    /// curve, welded less.</summary>
    public static double Leak(double d, double areaM2, Joint joint, SimSettings s, ref RandomStream r)
    {
        if (!(d >= s.LeakWeepD) || !(areaM2 > 0))
            return 0;
        return Math.Min(LeakRatio(d, joint, s) * Spread(s, ref r) * 1e-4, s.RupturedShare) * areaM2;
    }

    /// <summary>r(D), cm² a m² of the boundary: 0 below the weep, welded less; the curve itself, without its spread
    /// (<see cref="Spread"/>).</summary>
    public static double LeakRatio(double d, Joint joint, SimSettings s)
    {
        if (!(d >= s.LeakWeepD))
            return 0;
        double ratio = d < 1 ? s.LeakR1 * d * d : s.LeakR1 * Math.Pow(s.LeakR5 / s.LeakR1, (Math.Min(d, 5) - 1) / 4);
        return joint == Joint.Welded ? ratio * s.LeakWeldedK : ratio;
    }

    /// <summary>A boundary's draw about the curve: lognormal, σ <see cref="SimSettings.LeakSigma"/>, mean 1.</summary>
    public static double Spread(SimSettings s, ref RandomStream r) => Math.Exp(s.LeakSigma * r.NextGaussian() - 0.5 * s.LeakSigma * s.LeakSigma);

    /// <summary>An opening between two spaces for the flooding, on the edge's one entry the flooding reads (the lower
    /// cell's).</summary>
    internal static void Open(ShipClass c, ShipCondition k, int from, int poolIndex, double m2) => k.JoinOpenM2[Leaks.Entry(c, from, poolIndex)] += m2;

    static double Rivets(Joint j, SimSettings s) => j == Joint.Riveted ? s.RivetedK : 1;

    static bool Door(ShipClass c, in Neighbour e, int from) => Leaks.HasDoor(c, e, from);

    /// <summary>A join's plate, mm of steel: a bulkhead's plate where the two spaces meet (and its armour there), a deck's
    /// plate and its armour over them.</summary>
    internal static double JoinMm(ShipClass c, in Neighbour e, in Box a, in Box b)
    {
        double z = (Math.Max(a.Base, b.Base) + Math.Min(a.Top, b.Top)) / 2;
        if (e.Join == JoinKind.Bulkhead)
        {
            ref readonly var h = ref c.Bulkheads[e.Through];
            double mm = h.PlateAt(z);
            if (h.ArmourMm > 0 && z >= h.ArmourBottom && z <= h.ArmourTop)
                mm += c.MaterialOf(h.ArmourMaterial).SteelMm(h.ArmourMm);
            return Math.Max(mm, 1);
        }
        if (e.Join == JoinKind.Deck)
        {
            double mm = c.Decks[e.Through].PlateMm;
            double x = (Math.Max(a.X0, b.X0) + Math.Min(a.X1, b.X1)) / 2, y = (Math.Max(a.Y0, b.Y0) + Math.Min(a.Y1, b.Y1)) / 2;
            foreach (ref readonly var d in c.ArmourDecks)
                if (d.Deck == e.Through && d.Covers(x, y, c.Hull))
                    mm += c.MaterialOf(d.Material).SteelMm(d.ThicknessMm);
            return Math.Max(mm, 1);
        }
        return 1;
    }

    /// <summary>The area two spaces share: a deck's in plan, a bulkhead's across the ship or along it.</summary>
    internal static double Area(in Box a, in Box b, JoinKind join)
    {
        double ox = Math.Max(Math.Min(a.X1, b.X1) - Math.Max(a.X0, b.X0), 0), oy = Math.Max(Math.Min(a.Y1, b.Y1) - Math.Max(a.Y0, b.Y0), 0),
            oz = Math.Max(Math.Min(a.Top, b.Top) - Math.Max(a.Base, b.Base), 0);
        if (join == JoinKind.Deck)
            return Math.Max(ox * oy, 1);
        return Math.Max(oy < 0.05 ? ox * oz : oy * oz, 1);
    }

    /// <summary>How far a point is from the face two spaces share.</summary>
    static double FaceDistance(in Box a, in Box b, JoinKind join, P3 p)
    {
        if (join == JoinKind.Deck)
            return Math.Abs(p.Z - (b.Base >= a.Top - 0.05 ? a.Top : a.Base));
        bool along = Math.Min(a.Y1, b.Y1) - Math.Max(a.Y0, b.Y0) < 0.05;   // side by side across the ship
        if (along)
            return Math.Abs(p.Y - (b.Y0 >= a.Y1 - 0.05 ? a.Y1 : a.Y0));
        return Math.Abs(p.X - (b.X0 >= a.X1 - 0.05 ? a.X1 : a.X0));
    }

    /// <summary>The ship's outside faces worked out ahead of its first burst (a bench's timing; the first burst does it
    /// otherwise).</summary>
    public static void Prepare(ShipClass c, ShipCondition k) => Outside(c, k, 0, 0, out _, out _);

    /// <summary>A space's outside face, from the ship's cache (worked out the first time a burst needs it).</summary>
    internal static double Outside(ShipClass c, ShipCondition k, int cell, int face, out double mm, out P3 point)
    {
        if (!k.FacesReady)
        {
            for (int i = 0; i < c.Cells.Length; i++)
                for (int f = 0; f < Faces; f++)
                    k.FaceArea[i * Faces + f] = Outside(c, i, f, out k.FaceMm[i * Faces + f], out k.FacePoint[i * Faces + f]);
            k.FacesReady = true;
        }
        int x = cell * Faces + face;
        (mm, point) = (k.FaceMm[x], k.FacePoint[x]);
        return k.FaceArea[x];
    }

    /// <summary>A space's outside face (port side, starboard side, the weather deck over it, the bottom under it): its
    /// area (0: not outside), the plate there (mm of steel: the shell plating and a belt over it, the strength deck, the
    /// bottom) and its middle.</summary>
    static double Outside(ShipClass c, int cell, int face, out double mm, out P3 point)
    {
        ref readonly var x = ref c.Cells[cell];
        var b = x.Box;
        double xm = (b.X0 + b.X1) / 2, zm = (b.Base + b.Top) / 2, len = b.X1 - b.X0;
        var pl = c.Plating;
        bool mid = xm >= pl.MidX0 && xm <= pl.MidX1;
        (mm, point) = (0, default);
        switch (face)
        {
            case 0:
            case 1:
                double hw = c.Hull.HalfWidth(xm, zm);
                if (!(hw > 0.5) || (face == 0 ? -b.Y0 : b.Y1) < hw - 0.6)
                    return 0;
                mm = (mid ? pl.ShellMm : pl.ShellEndMm) + (x.BeltMm > 0 ? c.MaterialOf(x.BeltMaterial).SteelMm(x.BeltMm) : 0);
                point = new P3(xm, face == 0 ? -hw : hw, zm);
                return len * (b.Top - b.Base);
            case 2:
                if (!Weather(c, cell))
                    return 0;
                mm = mid ? pl.StrengthDeckMm : pl.StrengthDeckEndMm;
                point = new P3(xm, (b.Y0 + b.Y1) / 2, b.Top);
                return len * (b.Y1 - b.Y0);
            default:
                if (c.Tiers[x.Tier].Floor >= 0)
                    return 0;
                mm = mid ? pl.ShellMm : pl.ShellEndMm;
                point = new P3(xm, (b.Y0 + b.Y1) / 2, b.Base);
                return len * (b.Y1 - b.Y0);
        }
    }
}
