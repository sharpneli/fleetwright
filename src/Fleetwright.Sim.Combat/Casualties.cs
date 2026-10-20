using Fleetwright.Shipgen;
using Fleetwright.Sim.Core;
using Fleetwright.Sim.Ships;

namespace Fleetwright.Sim.Combat;

/// <summary>Casualties per hit (docs/battle/shells-armour-damage.md E6; research docs/battle/damage/16_casualties.md,
/// <c>casualty_ref.py</c>): men, not area. A hit's risks fall on <b>groups</b> of men, a station's battle crew (a part's,
/// by component, then a cell's; less those already killed or out there), each at a grid of points over its floor at a
/// man's height:
/// <list type="bullet">
/// <item><b>the burst's pieces</b> (<see cref="Pieces"/>, 16 §4.1): E1's sprays, those still carrying 80 J at the point
/// through the plate between, on a man's 0.5 m², times the people's flux scale S; the side ring as it leans with the
/// shell, the patch's share in it taking the ring's flux; a share of the men by where they are sees it, the rest take a
/// tenth;</item>
/// <item><b>blast</b> (<see cref="Gas"/>, 16 §4.5): in a closed space by what its boundaries felt (E5; Bowen), in the
/// open by distance;</item>
/// <item><b>the shell's own way</b> (16 §4.3): the men in its lane killed (<see cref="Lane"/>), and each plate it gets
/// through throwing its debris ahead of it (<see cref="Debris"/>);</item>
/// <item><b>spall</b> off a plate that held (<see cref="Spall"/>, E3; 14 §5.1), and <b>propellant flash</b>
/// (<see cref="Flash"/>, 16 §4.6), a share of the men it reaches.</item>
/// </list>
/// Each risk adds to the group's (independent: P(killed) = 1 − Π(1 − pᵢ)); <see cref="Draw"/> then rolls each man
/// killed, wounded or untouched, and of the wounded who are out for the action and who walk back to their station
/// later (16 §4.8). No factor afterwards: the scatter is the hit point, the ring, the spall roll and the men. Pure and
/// allocation-free; the scratch is on the ship's condition.</summary>
public static class Casualties
{
    /// <summary>A grid of points over a group's floor, Grid × Grid, at a man's height (or half the space's).</summary>
    const int Grid = 3;
    const double ManHeightM = 1.0;

    /// <summary>A part's group, and a cell's (after the components).</summary>
    public static int Part(int component) => component;
    public static int Cell(ShipClass c, int cell) => c.Components.Length + cell;

    /// <summary>The men at a station now: its battle crew less those killed or out there.</summary>
    public static int Men(ShipClass c, ShipCondition k, int g) =>
        Math.Max((g < c.Components.Length ? c.Components[g].BattleCrew : c.Cells[g - c.Components.Length].BattleCrew) - k.Gone[g], 0);

    /// <summary>The box a group stands in.</summary>
    public static Box BoxOf(ShipClass c, int g) => g < c.Components.Length ? c.Components[g].Bounds : c.Cells[g - c.Components.Length].Box;

    /// <summary>The share of a group's men who see a burst (16 §4.1 [EST]): a mount's by its kind (an enclosed gunhouse, a
    /// casemate, an open or shielded mount), another part's by its component kind, a cell's by its room's kind.</summary>
    public static double Cover(ShipClass c, int g, SimSettings s)
    {
        if (g < c.Components.Length)
        {
            int m = c.MountOf(g);
            if (m >= 0 && c.Mounts[m].Component == g)
                return s.CoverMount[Enclosed(c, m) ? 0 : c.Mounts[m].Casemate ? 1 : 2];
            return s.CoverPart[Math.Min((int)c.Components[g].Kind, s.CoverPart.Length - 1)];
        }
        int room = c.Cells[g - c.Components.Length].Room;
        return s.CoverRoom[room >= 0 ? Math.Min((int)c.Rooms[room].Kind, s.CoverRoom.Length - 1) : 0];
    }

    /// <summary>A gunhouse proper: a turret a mount's crew works inside (a battleship's or a dual-purpose one).</summary>
    internal static bool Enclosed(ShipClass c, int mount) => c.Enclosed(mount);

    /// <summary>A new hit: no group touched yet.</summary>
    internal static void Begin(ShipCondition k)
    {
        k.RiskId++;
        k.TouchedN = 0;
    }

    /// <summary>One more independent risk on a group's men: killed with pk, a casualty (killed or wounded) with pc.</summary>
    internal static void Add(ShipCondition k, int g, double pk, double pc)
    {
        pc = Math.Max(pc, pk);
        if (!(pc > 1e-9))
            return;
        if (k.RiskStamp[g] != k.RiskId)
        {
            (k.RiskStamp[g], k.RiskK[g], k.RiskC[g]) = (k.RiskId, 0, 0);
            k.Touched[k.TouchedN++] = g;
        }
        k.RiskK[g] += Log1m(pk);
        k.RiskC[g] += Log1m(pc);
    }

    static double Log1m(double p) => p >= 1 ? -60 : Math.Log(1 - p);

    /// <summary>A man's chance of being struck at least once (q 1) or killed (q the kill per piece) by λ pieces expected on
    /// an exposed man, a share cover of the men exposed and the rest taking shielded of it.</summary>
    static double Struck(double lambda, double q, double cover, double shielded) =>
        cover * (1 - Math.Exp(-q * lambda)) + (1 - cover) * (1 - Math.Exp(-q * shielded * lambda));

    /// <summary>A group's grid point i (of Grid²) and the half-width of its patch, m.</summary>
    static P3 Point(in Box b, int i, out double halfM)
    {
        double dx = (b.X1 - b.X0) / Grid, dy = (b.Y1 - b.Y0) / Grid;
        halfM = 0.5 * Math.Max(dx, dy);
        return new P3(b.X0 + (i % Grid + 0.5) * dx, b.Y0 + (i / Grid + 0.5) * dy, b.Base + Math.Min(ManHeightM, 0.5 * (b.Top - b.Base)));
    }

    /// <summary>A burst's pieces on a group (16 §4.1; <see cref="PiecesRisk"/>).</summary>
    internal static void Pieces(ShipClass c, ShipCondition k, int g, FragmentBurst b, P3 at, P3 dir, double v, double share, double mildMm,
        P3 normal, double openW, SimSettings s)
    {
        if (Men(c, k, g) <= 0)
            return;
        var (pk, pc) = PiecesRisk(BoxOf(c, g), Cover(c, g, s), b, at, dir, v, share, mildMm, normal, openW, s);
        Add(k, g, pk, pc);
    }

    /// <summary>A man's chance of being killed, and of being a casualty, standing in a box among men a share cover of whom
    /// see the burst (16 §4.1): the burst at a point, the shell flying along dir at v there, share of a full burst's
    /// pieces its way (a broken shell's, a burst on a plate's), through mildMm of mild steel (on the face whose normal is
    /// given, else square to them) to the box's middle; with openW (TNT kg) its blast in the open too (killed within
    /// <see cref="SimSettings.OpenKillK"/>·W^⅓, a share hurt out to <see cref="SimSettings.OpenHurtK"/>·W^⅓). Averaged over
    /// the box's points. dragM: the water a near miss's pieces go through first, as the air that slows them as much (E7).</summary>
    public static (double Killed, double Casualty) PiecesRisk(in Box box, double cover, FragmentBurst b, P3 at, P3 dir, double v, double share,
        double mildMm, P3 normal, double openW, SimSettings s, double dragM = 0)
    {
        double mx = (box.X0 + box.X1) / 2 - at.X, my = (box.Y0 + box.Y1) / 2 - at.Y, mz = (box.Base + box.Top) / 2 - at.Z;
        double rc = Math.Max(Math.Sqrt(mx * mx + my * my + mz * mz), 0.4);
        // the plate at its obliquity toward the box's middle (THOR's sec^1.286, in thickness)
        double e = 0;
        if (mildMm > 0)
        {
            double cosOb = normal == default ? 1 : Math.Abs((mx * normal.X + my * normal.Y + mz * normal.Z) / rc);
            double sec = 1 / Math.Max(cosOb, Math.Cos(double.DegreesToRadians(80)));
            e = Math.Pow(mildMm / 25.4 * Math.Pow(sec, Fragments.ThorG / Fragments.ThorA), Fragments.ThorA);
        }
        var n = b.Stopping(rc + dragM, v, e, s.ManStopJ);
        double sa = s.CasualtyS * share * s.ManAreaM2, all = n.Side + n.Ogive + n.Tail;
        double rk = openW > 0 ? s.OpenKillK * Math.Cbrt(openW) : 0, rh = openW > 0 ? s.OpenHurtK * Math.Cbrt(openW) : 0;
        double near = Math.Max(Effects.BoxDistance(box, at), 0.4);
        if (near >= rh && !(sa * all / (near * near) >= 1e-5))
            return (0, 0);
        double q = s.CasualtyQ, sh = s.ShieldedShare, pk = 0, pc = 0;
        for (int i = 0; i < Grid * Grid; i++)
        {
            var pt = Point(box, i, out double half);
            var f = b.Flux(at, dir, v, pt, half, n);
            double lin = sa * f.InRing, lout = sa * f.OutRing;
            double k1 = f.Ring * Struck(lin, q, cover, sh) + (1 - f.Ring) * Struck(lout, q, cover, sh);
            double c1 = f.Ring * Struck(lin, 1, cover, sh) + (1 - f.Ring) * Struck(lout, 1, cover, sh);
            if (rh > 0)
            {
                double dx = pt.X - at.X, dy = pt.Y - at.Y, dz = pt.Z - at.Z, r = Math.Sqrt(dx * dx + dy * dy + dz * dz);
                double kb = r < rk ? 1 : 0, cb = r < rk ? 1 : r < rh ? s.OpenHurtShare : 0;
                (k1, c1) = (1 - (1 - k1) * (1 - kb), 1 - (1 - c1) * (1 - cb));
            }
            pk += k1;
            pc += c1;
        }
        return (pk / (Grid * Grid), pc / (Grid * Grid));
    }

    /// <summary>A burst's gas on a group's men in a closed space (<see cref="GasRisk"/>).</summary>
    internal static void Gas(ShipCondition k, int g, double feltMPa, SimSettings s)
    {
        var (pk, pc) = GasRisk(feltMPa, s);
        Add(k, g, pk, pc);
    }

    /// <summary>A man's chance of being killed, and of being a casualty, in a closed space whose boundaries felt a gas
    /// pressure (16 §4.5; 11): killed by Bowen's lethality, out of action by the eardrums' and lungs'; no cover from it.</summary>
    public static (double Killed, double Casualty) GasRisk(double feltMPa, SimSettings s) => feltMPa > 0
        ? (LogNormal(feltMPa, s.BlastKillMPa, s.BlastKillSigma), LogNormal(feltMPa, s.BlastHurtMPa, s.BlastHurtSigma))
        : (0, 0);

    static double LogNormal(double x, double median, double sigma) => 0.5 * (1 + Erf(Math.Log(x / median) / (sigma * Math.Sqrt(2))));

    /// <summary>The error function (Abramowitz and Stegun 7.1.26, to 1.5e-7).</summary>
    static double Erf(double x)
    {
        double t = 1 / (1 + 0.3275911 * Math.Abs(x));
        double y = 1 - ((((1.061405429 * t - 1.453152027) * t + 1.421413741) * t - 0.284496736) * t + 0.254829592) * t * Math.Exp(-x * x);
        return x < 0 ? -y : y;
    }

    /// <summary>The shell's line from a to b through a group's space (<see cref="LaneShare"/>): the men in its lane killed.</summary>
    internal static void Lane(ShipClass c, ShipCondition k, int g, P3 a, P3 b, double calibreM, SimSettings s)
    {
        if (Men(c, k, g) <= 0)
            return;
        double share = LaneShare(BoxOf(c, g), a, b, calibreM, s);
        Add(k, g, share, share);
    }

    /// <summary>The share of the men on a box's floor in a shell's lane from a to b (16 §4.3): a calibre across and a man's
    /// half-width either side, along the floor while the line is at a man's height (<see cref="StandM"/> over the floor),
    /// at least its own width, over the floor's area.</summary>
    public static double LaneShare(in Box box, P3 a, P3 b, double calibreM, SimSettings s)
    {
        double z0 = box.Base, z1 = box.Base + StandM, dz = b.Z - a.Z, t0 = 0, t1 = 1;
        if (Math.Abs(dz) < 1e-9)
        {
            if (a.Z < z0 || a.Z > z1)
                return 0;
        }
        else
        {
            double ta = (z0 - a.Z) / dz, tb = (z1 - a.Z) / dz;
            (t0, t1) = (Math.Max(t0, Math.Min(ta, tb)), Math.Min(t1, Math.Max(ta, tb)));
            if (t1 <= t0)
                return 0;
        }
        double w = calibreM + 2 * s.LaneManM, along = Math.Sqrt((b.X - a.X) * (b.X - a.X) + (b.Y - a.Y) * (b.Y - a.Y)) * (t1 - t0);
        return Math.Min(1, w * Math.Max(along, w) / Math.Max((box.X1 - box.X0) * (box.Y1 - box.Y0), 1));
    }

    /// <summary>A standing man's height, m: the band over the floor a shell's lane kills in.</summary>
    const double StandM = 1.8;

    /// <summary>Spall off a plate that held on a group's men (E3; 14 §5.1), or the debris a plate the shell got through
    /// throws (<see cref="Debris"/>): the flakes still carrying 80 J at a point's angle off the cloud's axis, by the
    /// cloud's density there, on a man's area over the distance squared (no flux scale: 14's model meets Tiger's roof as
    /// it is), and the big piece one more within its cone; q <see cref="SimSettings.DebrisQ"/>.</summary>
    internal static void Spall(ShipClass c, ShipCondition k, int g, in SpallCloud cloud, SimSettings s)
    {
        Cloud(c, k, g, cloud, s);
    }

    /// <summary>The debris a plate the shell got through throws into a group's space (16 §4.3, with 14 §3.1's measured
    /// debris in place of 16's disc: <see cref="Combat.Spall.Perforated"/>), striking as spall does.</summary>
    internal static void Debris(ShipClass c, ShipCondition k, int g, in SpallCloud cloud, SimSettings s)
    {
        Cloud(c, k, g, cloud, s);
    }

    static void Cloud(ShipClass c, ShipCondition k, int g, in SpallCloud cloud, SimSettings s)
    {
        if (Men(c, k, g) <= 0 || !(cloud.Flakes > 0) && !(cloud.BigKg > 0))
            return;
        var (pk, pc) = CloudRisk(BoxOf(c, g), Cover(c, g, s), cloud, s);
        Add(k, g, pk, pc);
    }

    /// <summary>A man's chance of being killed, and of being a casualty, in a box a spall or debris cloud flies into, among
    /// men a share cover of whom see it (14 §5.1): averaged over the box's points.</summary>
    public static (double Killed, double Casualty) CloudRisk(in Box box, double cover, in SpallCloud cloud, SimSettings s)
    {
        double pk = 0, pc = 0;
        double big = double.DegreesToRadians(Combat.Spall.BigConeDeg), bigOmega = 2 * Math.PI * (1 - Math.Cos(big));
        for (int i = 0; i < Grid * Grid; i++)
        {
            var pt = Point(box, i, out _);
            double dx = pt.X - cloud.Origin.X, dy = pt.Y - cloud.Origin.Y, dz = pt.Z - cloud.Origin.Z, r = Math.Max(Math.Sqrt(dx * dx + dy * dy + dz * dz), 0.4);
            double th = Combat.Spall.Theta(cloud, dx / r, dy / r, dz / r);
            double lambda = cloud.Flakes > 0 && cloud.FlakeJ(th) >= s.ManStopJ ? cloud.Flakes * Combat.Spall.Density(th) * s.ManAreaM2 / (r * r) : 0;
            if (th <= big && cloud.BigJ >= s.ManStopJ)
                lambda += s.ManAreaM2 / (bigOmega * r * r);
            if (!(lambda > 0))
                continue;
            pk += Struck(lambda, s.DebrisQ, cover, s.ShieldedShare);
            pc += Struck(lambda, 1, cover, s.ShieldedShare);
        }
        return (pk / (Grid * Grid), pc / (Grid * Grid));
    }

    /// <summary>Propellant flash on a group's men (16 §4.6): its shares killed and wounded, a turret's train's or an open
    /// casemate battery's.</summary>
    internal static void Flash(ShipCondition k, int g, bool turret, SimSettings s)
    {
        var sh = turret ? s.FlashTurret : s.FlashBattery;
        Add(k, g, sh[0], sh[0] + sh[1]);
    }

    /// <summary>A magazine set off: the flash through its turrets' trains (16 §4.6), the men in the gunhouses, their
    /// barbettes, the handling rooms under them and the magazine itself.</summary>
    internal static void Train(ShipClass c, ShipCondition k, int magazine, SimSettings s)
    {
        if (magazine < 0)
            return;
        foreach (int cell in c.Links(c.Rooms[magazine].Cells))
            Flash(k, Cell(c, cell), true, s);
        for (int m = 0; m < c.Mounts.Length; m++)
        {
            ref readonly var mr = ref c.Mounts[m];
            if (mr.Magazine != magazine)
                continue;
            Flash(k, Part(mr.Component), true, s);
            if (mr.Barbette >= 0)
                Flash(k, Part(mr.Barbette), true, s);
            for (int room = 0; room < c.Rooms.Length; room++)
                if (c.Rooms[room].Kind == RoomKind.Handling && c.Rooms[room].Mount == mr.Component)
                    foreach (int cell in c.Links(c.Rooms[room].Cells))
                        Flash(k, Cell(c, cell), true, s);
        }
    }

    /// <summary>The hit's men rolled (16 §4.7): each of a touched group's killed, wounded or untouched by its risks; of
    /// the wounded a share out for the action, the rest back at their station after first aid. Killed and wounded leave
    /// the station now; the ship's counts and the hit's.</summary>
    internal static void Draw(ShipClass c, ShipCondition k, long now, SimSettings s, ref EffectsResult res, ref RandomStream r)
    {
        for (int i = 0; i < k.TouchedN; i++)
        {
            int g = k.Touched[i], n = Men(c, k, g);
            if (n <= 0)
                continue;
            double pk = 1 - Math.Exp(k.RiskK[g]), pc = Math.Max(pk, 1 - Math.Exp(k.RiskC[g]));
            int killed = 0, wounded = 0, walking = 0;
            for (int m = 0; m < n; m++)
            {
                double u = r.NextDouble();
                if (u < pk)
                    killed++;
                else if (u < pc)
                {
                    wounded++;
                    if (r.NextDouble() >= s.WoundedOutShare)
                        walking++;
                }
            }
            if (killed + wounded == 0)
                continue;
            k.Gone[g] += killed + wounded;
            (k.Killed, k.Wounded, k.CrewLost) = (k.Killed + killed, k.Wounded + wounded, k.CrewLost + killed + wounded);
            (res.Killed, res.Wounded) = (res.Killed + killed, res.Wounded + wounded);
            if (walking > 0)
            {
                double m0 = s.WoundedBackMin[0], m1 = s.WoundedBackMin[1];
                k.Back(g, walking, now + (long)(60_000 * (m0 + (m1 - m0) * r.NextDouble())));
            }
        }
        res.CrewLost += res.Killed + res.Wounded;
        k.TouchedN = 0;
    }
}
