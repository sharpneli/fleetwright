using Fleetwright.Shipgen;
using Fleetwright.Sim.Core;
using Fleetwright.Sim.Ships;

namespace Fleetwright.Sim.Combat;

/// <summary>A class's outside faces (port side, starboard side, the weather deck, the bottom; cell x 4 + face): their area
/// (0: not outside), the plate there as a burst from outside meets it (a belt in it), the plate that holds against a pressure
/// from inside (a belt is hung on the side and comes away with it: docs/battle/explosions.md EX3) and their middles. Shared by
/// every ship and world of the class, read only.</summary>
public sealed class HullFaces
{
    static readonly System.Runtime.CompilerServices.ConditionalWeakTable<ShipClass, HullFaces> Table = new();

    public readonly double[] Area, Mm, HeldMm;
    public readonly P3[] Point;

    HullFaces(ShipClass c)
    {
        int n = c.Cells.Length * 4;
        (Area, Mm, HeldMm, Point) = (new double[n], new double[n], new double[n], new P3[n]);
        for (int i = 0; i < c.Cells.Length; i++)
            for (int f = 0; f < 4; f++)
            {
                int x = i * 4 + f;
                Area[x] = Blast.Outside(c, i, f, out Mm[x], out Point[x]);
                HeldMm[x] = f < 2 && c.Cells[i].BeltMm > 0 ? Mm[x] - c.MaterialOf(c.Cells[i].BeltMaterial).SteelMm(c.Cells[i].BeltMm) : Mm[x];
            }
    }

    public static HullFaces Of(ShipClass c) => Table.GetValue(c, static x => new HullFaces(x));
}

/// <summary>The plate and spaces an explosion's gas meets (docs/battle/explosions.md; research docs/battle/damage/
/// 11_blast_confinement.md): what a filler's gas holds, what's open, the boundaries' plate and shared areas, how a boundary
/// that held leaks. <see cref="GasNetwork"/> runs the gas itself.</summary>
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
        return s.BlastHeatK * EnergyMJ(qDet, qComb, air, eta, w, v) / v;
    }

    /// <summary>What w kg of a shell's filler gives its gas in v m³ of it, MJ.</summary>
    public static double EnergyMJ(in ShellTypeRow f, double w, double v) => EnergyMJ(f.FillerQDet, f.FillerQComb, f.FillerAirM3Kg, f.FillerEta, w, v);

    static double EnergyMJ(double qDet, double qComb, double air, double eta, double w, double v)
    {
        double phi = air > 0 ? 1 / (1 + w * air / v) : 0;
        return eta * (qDet + phi * Math.Max(qComb - qDet, 0)) * w;
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

    /// <summary>Whether a space's top is the weather deck: no space over it.</summary>
    static bool Weather(ShipClass c, int cell)
    {
        var b = c.Cells[cell].Box;
        foreach (ref readonly var e in c.Neighbours(c.Cells[cell].Neighbours))
            if (c.Cells[e.Cell].Box.Base >= b.Top - 0.05)
                return false;
        return true;
    }

    /// <summary>Whether a cell has none of her cells under it.</summary>
    static bool NothingUnder(ShipClass c, int cell)
    {
        var b = c.Cells[cell].Box;
        foreach (ref readonly var e in c.Neighbours(c.Cells[cell].Neighbours))
            if (c.Cells[e.Cell].Box.Top <= b.Base + 0.05)
                return false;
        return true;
    }

    /// <summary>How much of a space's side another of her spaces stands outboard of, m²: to port on face 0, to starboard on 1.</summary>
    static double Outboard(ShipClass c, int cell, int face)
    {
        var b = c.Cells[cell].Box;
        double covered = 0;
        foreach (ref readonly var e in c.Neighbours(c.Cells[cell].Neighbours))
        {
            var o = c.Cells[e.Cell].Box;
            if (face == 0 ? o.Y1 > b.Y0 + 0.05 : o.Y0 < b.Y1 - 0.05)
                continue;
            covered += Math.Max(Math.Min(b.X1, o.X1) - Math.Max(b.X0, o.X0), 0) * Math.Max(Math.Min(b.Top, o.Top) - Math.Max(b.Base, o.Base), 0);
        }
        return covered;
    }

    /// <summary>The marks a boundary gets in a run.</summary>
    internal static class Fate
    {
        public const byte None = 0, Holed = 1, Ruptured = 2, Door = 3, Deformed = 4;
    }

    /// <summary>The load a boundary gives at, as a D: a load's D is 5 at its plate's rupture pressure and 1 where it starts to
    /// deform (damage/17 §3.2).</summary>
    public const double RuptureD = 5;

    /// <summary>A plate's rupture pressure, MPa: its thickness and joint.</summary>
    public static double RuptureMPa(double mm, Joint joint, SimSettings s) => s.RuptureMPaMm * mm * (joint == Joint.Riveted ? s.RivetedK : 1);

    /// <summary>r(D), cm² a m² of the boundary, from D 1 on: damage/17's curve, log-linear to <see cref="SimSettings.LeakR5"/> at
    /// rupture and running on past it.</summary>
    static double Curve(double d, SimSettings s) => s.LeakR1 * Math.Pow(s.LeakR5 / s.LeakR1, (d - 1) / 4);

    /// <summary>The share of a boundary a load of D has opened: <see cref="Curve"/> run on past rupture (about 1% open) to what a
    /// torn-out boundary leaves, rather than jumping there: a plate just past its limit has cracked, and one far past it has gone.
    /// Under D 1 its seams only weep, for the flooding afterwards.</summary>
    public static double Opening(double d, Joint joint, SimSettings s)
    {
        if (!(d >= 1))
            return 0;
        double ratio = Curve(d, s) * 1e-4;
        return Math.Min(joint == Joint.Welded && d < RuptureD ? ratio * s.LeakWeldedK : ratio, s.RupturedShare);
    }

    /// <summary>The share of a boundary's plate cut, for the girder, by what it has torn: past rupture its opening is a crack,
    /// the area going as the length squared, and all of it is gone when it's torn out.</summary>
    public static double LostShare(double tornM2, double areaM2, SimSettings s) => Math.Sqrt(Math.Min(tornM2 / (s.RupturedShare * areaM2), 1));

    /// <summary>The share of a boundary's plate cut by a hole through it, all of it open: as wide as it's long, not a crack's
    /// narrow slot (<see cref="LostShare"/>).</summary>
    public static double HoleShare(double holeM2, double areaM2) => Math.Sqrt(Math.Min(holeM2 / areaM2, 1));

    /// <summary>A deformed boundary's seam leak, m² (damage/17 leakage §3.2): r(D) cm² per m² of it, lognormal about the
    /// curve, welded less.</summary>
    public static double Leak(double d, double areaM2, Joint joint, SimSettings s, ref RandomStream r)
    {
        if (!(d >= s.LeakWeepD) || !(areaM2 > 0))
            return 0;
        return Math.Min(LeakRatio(d, joint, s) * Spread(s, ref r) * 1e-4, s.RupturedShare) * areaM2;
    }

    /// <summary>What a load leaves in seams that held, cm² a m²: 0 below the weep, D² to D 1, then <see cref="Curve"/> to rupture
    /// and no further, welded less; without its spread (<see cref="Spread"/>).</summary>
    public static double LeakRatio(double d, Joint joint, SimSettings s)
    {
        if (!(d >= s.LeakWeepD))
            return 0;
        double ratio = d < 1 ? s.LeakR1 * d * d : Curve(Math.Min(d, RuptureD), s);
        return joint == Joint.Welded ? ratio * s.LeakWeldedK : ratio;
    }

    /// <summary>A boundary's draw about the curve: lognormal, σ <see cref="SimSettings.LeakSigma"/>, mean 1.</summary>
    public static double Spread(SimSettings s, ref RandomStream r) => Math.Exp(s.LeakSigma * r.NextGaussian() - 0.5 * s.LeakSigma * s.LeakSigma);

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

    /// <summary>The plate two spaces share against a point: how far the point is from its plane (the return), where the point
    /// falls in that plane (u, w), and the plate's rectangle there.</summary>
    internal static double Shared(in Box a, in Box b, JoinKind join, P3 p, out double u, out double w, out double u0, out double u1, out double w0, out double w1)
    {
        double x0 = Math.Max(a.X0, b.X0), x1 = Math.Min(a.X1, b.X1), y0 = Math.Max(a.Y0, b.Y0), y1 = Math.Min(a.Y1, b.Y1), z0 = Math.Max(a.Base, b.Base), z1 = Math.Min(a.Top, b.Top);
        if (join == JoinKind.Deck)
        {
            (u, w, u0, u1, w0, w1) = (p.X, p.Y, x0, x1, y0, y1);
            return Math.Abs(p.Z - (b.Base >= a.Top - 0.05 ? a.Top : a.Base));
        }
        if (y1 - y0 < 0.05)   // side by side across the ship
        {
            (u, w, u0, u1, w0, w1) = (p.X, p.Z, x0, x1, z0, z1);
            return Math.Abs(p.Y - (b.Y0 >= a.Y1 - 0.05 ? a.Y1 : a.Y0));
        }
        (u, w, u0, u1, w0, w1) = (p.Y, p.Z, y0, y1, z0, z1);
        return Math.Abs(p.X - (b.X0 >= a.X1 - 0.05 ? a.X1 : a.X0));
    }

    /// <summary>A space's outside face, from the class's table.</summary>
    internal static double Outside(ShipCondition k, int cell, int face, out double mm, out P3 point)
    {
        var h = k.Faces;
        int x = cell * 4 + face;
        (mm, point) = (h.Mm[x], h.Point[x]);
        return h.Area[x];
    }

    /// <summary>A space's outside face (port side, starboard side, the weather deck over it, the bottom under it): its
    /// area (0: not outside; a side, what no other space stands outboard of), the plate there (mm of steel: the shell plating and a belt over it, the strength deck, the
    /// bottom) and its middle. Her bottom is under the keel's tier, and past the double bottom's end under any lowest cell, where
    /// the cell is a box and her bottom rises: there it's where the hull first has breadth inside the box, as wide as the hull
    /// is there (docs/battle/explosions.md EX3).</summary>
    internal static double Outside(ShipClass c, int cell, int face, out double mm, out P3 point)
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
                double open = len * (b.Top - b.Base) - Outboard(c, cell, face);   // the side is the outermost space's: a thin wing's, not what's behind it
                if (!(open > 1e-3))
                    return 0;
                mm = (mid ? pl.ShellMm : pl.ShellEndMm) + (x.BeltMm > 0 ? c.MaterialOf(x.BeltMaterial).SteelMm(x.BeltMm) : 0);
                point = new P3(xm, face == 0 ? -hw : hw, zm);
                return open;
            case 2:
                if (!Weather(c, cell))
                    return 0;
                mm = mid ? pl.StrengthDeckMm : pl.StrengthDeckEndMm;
                point = new P3(xm, (b.Y0 + b.Y1) / 2, b.Top);
                return len * (b.Y1 - b.Y0);
            default:
                if (c.Tiers[x.Tier].Floor < 0)
                {
                    mm = mid ? pl.ShellMm : pl.ShellEndMm;
                    point = new P3(xm, (b.Y0 + b.Y1) / 2, b.Base);
                    return len * (b.Y1 - b.Y0);
                }
                if (!NothingUnder(c, cell))
                    return 0;
                double z = b.Base;
                while (z < b.Top && !(c.Hull.HalfWidth(xm, z) > 0.3))
                    z += 0.25;
                double half = c.Hull.HalfWidth(xm, z), y0 = Math.Max(b.Y0, -half), y1 = Math.Min(b.Y1, half);
                if (!(z < b.Top) || !(y1 > y0))
                    return 0;
                mm = mid ? pl.ShellMm : pl.ShellEndMm;
                point = new P3(xm, (y0 + y1) / 2, z);
                return len * (y1 - y0);
        }
    }
}
