using Fleetwright.Sim.Ships;

namespace Fleetwright.Sim.Combat;

/// <summary>What sets a jet off (<see cref="MagazineBlast.Opening"/>): a gun port, the turret's sighting hood, a deck
/// hatch or ventilator over the magazine, a side scuttle, a machinery room's ventilator beside it, or the barbette once
/// the gunhouse's roof lifts.</summary>
public enum OpeningKind : byte
{
    Port,
    Hood,
    Hatch,
    Scuttle,
    Vent,
    Barbette,
}

/// <summary>A magazine going up, as the screens draw it (docs/visuals/magazine-explosions.md X1): the port of shipgen's
/// vidgen <c>magazine.plan</c> and its correlations (research/vfx/magazine-explosion-vfx-research.md §2), one
/// implementation for the map and the tools. Per class and magazine room, from the ship:
/// <list type="bullet">
/// <item><b>M</b>, the propellant burned in the cascade window: the room's tonnes × the propellant's share of a round
/// (its mounts' charge against their shell) × <see cref="DesignerPlaceholders.MagazineFastShare"/>, until the venting
/// race (4.5) gives it;</item>
/// <item><b>the fireball</b> D = 5.8 M^⅓ (CCPS), its life 0.45 M^⅓ s under 30 t, else 2.6 M^⅙ s, lifting off to ≈ D;
/// <b>the blast</b>'s length λ = (0.5 ΔH M / p₀)^⅓ (the muzzle blast's frost and scour at that λ);</item>
/// <item><b>the openings</b> that jet flame before it (research §2.5): a port per barrel along the trained guns, the
/// sighting hood, hatches over the magazine (one per ~4 m), four side scuttles, the ventilators of machinery rooms that
/// share a bulkhead with it (Hood), then the barbette when the roof lifts, each failing on the prototype's
/// sequence;</item>
/// <item><b>the rest</b>: the barbette and the mount over the magazine (the gunhouse thrown), the nearest boiler room
/// (steam), the scale <see cref="K"/> that sizes vents, fires, debris and soot by the fireball against the research's
/// 20 t event, and the debris count.</item>
/// </list>
/// Positions are in the ship's frame (+x bow, +y starboard), heights over the waterline; a port's is in its mount's
/// frame (pivot at 0, guns along +x), turned by the mount's bearing when drawn. Times are seconds after the hit (the
/// sim's loss). Display only: nothing in the sim reads it.</summary>
public sealed class MagazineBlast
{
    /// <summary>The air's pressure, Pa; cordite's heat of explosion, J/kg (an order of magnitude, research §2.1).</summary>
    public const double P0 = 101_325, HeatJkg = 4.0e6;

    /// <summary>The research's reference event's fireball, m (20 t): what <see cref="K"/> is measured against.</summary>
    public const double ReferenceD = 157;

    /// <summary>The display's sequence, seconds: from the hit to the first opening failing (the hoists carry the flash
    /// down and back), then after that, each opening's (the prototype's), the roof lifting and the main event.</summary>
    public const double HitLeadS = 0.5, PortsS = 0, HoodS = 0.05, HatchS = 0.12, ScuttleS = 0.22, VentS = 0.35, LiftS = 0.45, MainS = 0.9;

    /// <summary>The magazine's pressure at the main event, bar absolute (the stand-in for the venting race's), and how
    /// fast it vents after, s.</summary>
    public const double PeakBar = 3.0, VentTauS = 0.7;

    /// <summary>An opening's jet: where (in the ship's frame, or its mount's for a port), which way (unit), its
    /// equivalent diameter and longest flame, m, and when it fails, s after the hit.</summary>
    public readonly record struct Opening(OpeningKind Kind, int Mount, P3 At, P3 Dir, double DiameterM, double MaxM, double FailS);

    public int Room { get; }
    /// <summary>The propellant burned in the cascade window, kg.</summary>
    public double MassKg { get; }
    /// <summary>The fireball's diameter, m, and its life, s.</summary>
    public double FireballM { get; }
    public double FireballS { get; }
    /// <summary>The blast's length λ, m.</summary>
    public double BlastLengthM { get; }
    /// <summary>The fireball against the reference's, at least 0.25: vents, fires, debris and soot scale by it.</summary>
    public double K { get; }
    public int Debris { get; }

    /// <summary>The magazine's middle (x, y), its span along the ship, the hull's half-breadth there, and the deck's
    /// height over the waterline over it.</summary>
    public double X { get; }
    public double Y { get; }
    public double X0 { get; }
    public double X1 { get; }
    public double HalfBeamM { get; }
    public double DeckZ { get; }

    /// <summary>The mount over the magazine (its gunhouse thrown), or -1; its barbette's centre, radius and top over
    /// the waterline (NaN radius: none).</summary>
    public int Mount { get; }
    public double BarbetteX { get; }
    public double BarbetteY { get; }
    public double BarbetteR { get; } = double.NaN;
    public double BarbetteZ { get; }

    /// <summary>The nearest boiler room's middle along the ship (its steam), NaN for none.</summary>
    public double SteamX { get; } = double.NaN;

    readonly Opening[] openings;
    public ReadOnlySpan<Opening> Openings => openings;

    readonly double[] mountX, mountY;

    /// <summary>A mount's pivot in the ship's frame (its ports' and hood's jets turn about it).</summary>
    public double MountX(int mount) => mountX[mount];
    public double MountY(int mount) => mountY[mount];

    /// <summary>The main event (the fireball, the blast, the debris), s after the hit.</summary>
    public static double MainAtS => HitLeadS + MainS;

    /// <summary>The fireball for <paramref name="massKg"/> burned in the fast phase (research §2.2): its diameter, m, and
    /// life, s. The two laws of its life meet near 14 s, so the switch at 30 t is continuous.</summary>
    public static (double D, double S) Fireball(double massKg)
    {
        double c = Math.Cbrt(Math.Max(massKg, 0));
        return (5.8 * c, massKg < 30_000 ? 0.45 * c : 2.6 * Math.Pow(massKg, 1.0 / 6));
    }

    /// <summary>The blast's length λ, m, for <paramref name="massKg"/> burned, half its energy in the blast.</summary>
    public static double BlastLength(double massKg) => Math.Cbrt(0.5 * HeatJkg * Math.Max(massKg, 0) / P0);

    /// <summary>Heskestad's flame height, m, of a fire of <paramref name="kw"/> kW over an opening <paramref name="d"/> m
    /// across (the barbette's column, research §2.1).</summary>
    public static double Heskestad(double kw, double d) => Math.Max(0.235 * Math.Pow(Math.Max(kw, 0), 0.4) - 1.02 * d, 0);

    /// <summary>The magazine's pressure, bar absolute, <paramref name="t"/> s after the hit: rising while the burn races
    /// the vents, peaking as the boundaries fail, then venting down (the stand-in for the venting race's).</summary>
    public static double PressureBar(double t)
    {
        double a = t - HitLeadS;
        if (a < MainS)
            return 1 + (PeakBar - 1) * Math.Pow(Math.Max(a, 0) / MainS, 2);
        return 1 + (PeakBar - 1) * Math.Exp(-(a - MainS) / VentTauS);
    }

    /// <summary>An opening's flame, m, <paramref name="t"/> s after the hit: 45 d √(P/p₀), capped, growing in over its
    /// first 80 ms; 0 before it fails and once the pressure is spent.</summary>
    public static double JetLength(in Opening o, double t)
    {
        double a = t - o.FailS, p = PressureBar(t);
        if (a < 0 || p < 1.15)
            return 0;
        return Math.Min(45 * o.DiameterM * Math.Sqrt(p), o.MaxM) * Math.Min(1, a / 0.08);
    }

    /// <summary>How far the cloud lifts over two of its fireball's lives, in diameters, and how much wider it billows a
    /// life once grown.</summary>
    public const double LiftD = 0.5, Spread = 0.5;

    /// <summary>The fireball and its smoke as one body <paramref name="t"/> s after the main event (research §2.2, §3.4):
    /// <list type="bullet">
    /// <item>it grows to its diameter <paramref name="d"/> over half its life <paramref name="life"/>, then billows on
    /// wider (<see cref="Spread"/> a life) and flattens, all the while rising half its diameter over two lives
    /// (<see cref="LiftD"/>): one motion, never a pause, its foot staying within about a third of its diameter of the
    /// deck while it shows. A departure (the user, 2026-10-15): the research's fireball lifts off to ≈ D and climbs as
    /// a thermal to a kilometre, which on its stem read as a flower growing on its stalk; held down, it read as a rise
    /// stopped;</item>
    /// <item>its heat (0-1, the drawing's temperature scale) falls from white-hot over its life, faster at its top and
    /// outside (the soot skin, <paramref name="skin"/>: the share it cools there by the end);</item>
    /// <item>its optical depth through the middle, 8 at the fireball's end, falls only as the square root of its
    /// radius: its soot spreads wider, but the fires under it feed more (a departure from the area's fall, which thinned
    /// vidgen's cap to a haze).</item>
    /// </list>
    /// Heights are over the waterline from the deck at <paramref name="deckZ"/>; nothing before the main event.</summary>
    public static Body BodyAt(double d, double life, double deckZ, double t)
    {
        double rEnd = d / 2;
        double r = rEnd * (0.35 + 0.65 * SmoothStep(0, 0.5 * life, t) + Spread * Math.Max(t / life - 0.5, 0));
        double flat = 1 - 0.3 * SmoothStep(0.5 * life, 2 * life, t);
        // its foot near the wreck: the centre 0.7 of its radius over the deck, lifting on one smooth curve
        double zc = deckZ + 0.7 * r + d * LiftD * SmoothStep(0.15 * life, 2 * life, t);
        double heat = t <= life ? 1 - 0.55 * Math.Pow(t / life, 1.2) : 0.45 * Math.Exp(-(t - life) / (0.3 * life));
        double skin = Math.Min(t / life, 1);
        double tau = 8 * Math.Sqrt(rEnd / r);
        return new Body(zc, r, r * flat, heat, skin, tau);
    }

    /// <summary>The body's state (<see cref="BodyAt"/>): its centre's height and radii (across, up), m; its core's heat
    /// (0-1) and how far the soot skin has grown over it (0-1); its optical depth through the middle.</summary>
    public readonly record struct Body(double CentreZ, double R, double Rv, double Heat, double Skin, double Tau);

    static double SmoothStep(double a, double b, double x)
    {
        double u = Math.Clamp((x - a) / (b - a), 0, 1);
        return u * u * (3 - 2 * u);
    }

    /// <summary>The event for a class's magazine room.</summary>
    public static MagazineBlast Of(ShipClass c, int room) => new(c, room);

    MagazineBlast(ShipClass c, int room)
    {
        ref readonly var r = ref c.Rooms[room];
        if (r.Kind != RoomKind.Magazine)
            throw new ArgumentException($"room {room} of {c.Name} isn't a magazine");
        Room = room;
        double wl = c.Vertical.Waterline;
        mountX = new double[c.Mounts.Length];
        mountY = new double[c.Mounts.Length];
        for (int m = 0; m < c.Mounts.Length; m++)
            (mountX[m], mountY[m]) = (c.Mounts[m].X, c.Mounts[m].Y);

        // the mounts it serves, and the propellant's share of their rounds
        Span<int> served = stackalloc int[Math.Max(c.Mounts.Length, 1)];
        int ns = 0;
        for (int m = 0; m < c.Mounts.Length; m++)
            if (c.Mounts[m].Magazine == room || r.Mount >= 0 && c.Mounts[m].Component == r.Mount)
                served[ns++] = m;
        double share = 0;
        int shares = 0;
        for (int i = 0; i < ns; i++)
        {
            ref readonly var mt = ref c.Mounts[served[i]];
            if (mt.Battery < 0 || mt.Type >= c.Charges.Length)
                continue;
            double charge = c.Charges[mt.Type].MassKg, shell = c.Loadout.ShellTypes[c.Loadout.Fires(mt.Battery)].MassKg;   // the design's magazines
            if (charge > 0 && shell > 0)
            {
                share += charge / (charge + shell);
                shares++;
            }
        }
        share = shares > 0 ? share / shares : DesignerPlaceholders.MagazinePropellantShare;
        double tonnes = double.IsFinite(r.Tonnes) && r.Tonnes > 0 ? r.Tonnes : DesignerPlaceholders.MagazineTonnes(r.VolumeM3);
        MassKg = tonnes * 1000 * share * DesignerPlaceholders.MagazineFastShare;
        (FireballM, FireballS) = Fireball(MassKg);
        BlastLengthM = BlastLength(MassKg);
        K = Math.Max(0.25, FireballM / ReferenceD);
        Debris = (int)Math.Round(8 + 70 * (1 - Math.Exp(-MassKg / 15_000)));

        (X0, X1) = (r.X0, r.X1);
        double xc = (X0 + X1) / 2;
        var hull = c.Hull;
        DeckZ = hull.DeckTop(xc) - wl;
        HalfBeamM = hull.HalfWidth(xc, hull.DeckTop(xc) - 0.5);

        // the mount over it: the served one nearest the centreline and its middle, and its barbette
        Mount = -1;
        double best = double.PositiveInfinity;
        for (int i = 0; i < ns; i++)
        {
            ref readonly var mt = ref c.Mounts[served[i]];
            double score = Math.Abs(mt.Y) + Math.Abs(mt.X - xc);
            if (score < best)
                (best, Mount) = (score, served[i]);
        }
        (X, Y) = (xc, 0);
        if (Mount >= 0 && c.Mounts[Mount].Barbette is int bc and >= 0)
        {
            ref readonly var b = ref c.Components[bc];
            (BarbetteX, BarbetteY, BarbetteR, BarbetteZ) = (b.X, b.Y, b.R, b.Bounds.Top - wl);
            (X, Y) = (b.X, b.Y);
        }

        var ops = new List<Opening>();
        double t0 = HitLeadS;
        for (int i = 0; i < ns; i++)
        {
            int m = served[i];
            ref readonly var mt = ref c.Mounts[m];
            var tt = c.TurretTypes[mt.Type];
            double top = c.Components[mt.Component].Bounds.Top - wl;
            int barrels = Math.Max(tt.Barrels, 1);
            for (int j = 0; j < barrels; j++)   // where each barrel leaves the gunhouse, along the guns (a little up)
                ops.Add(new Opening(OpeningKind.Port, m, new P3(tt.R, (j - (barrels - 1) / 2.0) * tt.Spacing, top - 1.5), Unit(1, 0, 0.05),
                    2.5 * mt.CalibreMm / 1000, 90, t0 + PortsS + 0.02 * j));
            ops.Add(new Opening(OpeningKind.Hood, m, new P3(-2, 0, top), new P3(0, 0, 1), 0.6, 50, t0 + HoodS));
        }
        // hatches and ventilators over the magazine, beside the barbette: up, leaning outboard a little
        int nh = Math.Max(2, (int)Math.Round((X1 - X0) / 4));
        double off = double.IsNaN(BarbetteR) ? 3 : BarbetteR + 2;
        for (int j = 0; j < nh; j++)
        {
            double x = X0 + (j + 0.5) * (X1 - X0) / nh, side = j % 2 == 1 ? 1 : -1;
            double y = side * Math.Min(off, 0.8 * Math.Max(hull.HalfWidth(x, hull.DeckTop(x) - 0.5), 1));
            ops.Add(new Opening(OpeningKind.Hatch, -1, new P3(x, y, hull.DeckTop(x) - wl + 0.5), Unit(0, 0.15 * side, 1), 0.8, 60,
                t0 + HatchS + 0.05 * j / Math.Max(1, nh - 1)));
        }
        // side scuttles: sideways from the hull under the deck edge, abreast the magazine
        for (int j = 0; j < 4; j++)
        {
            double side = j % 2 == 1 ? 1 : -1, x = X0 + (0.3 + 0.4 * (j / 2)) * (X1 - X0), deck = hull.DeckTop(x);
            double z = Math.Max(1.0, deck - wl - 1.5);
            ops.Add(new Opening(OpeningKind.Scuttle, -1, new P3(x, side * hull.HalfWidth(x, z + wl), z), Unit(0, side, 0.25), 0.4, 30,
                t0 + ScuttleS + 0.02 * j));
        }
        // machinery rooms sharing a bulkhead (Hood: the flame came out of the engine-room ventilators)
        double steam = double.PositiveInfinity;
        for (int i = 0; i < c.Rooms.Length; i++)
        {
            ref readonly var q = ref c.Rooms[i];
            if (q.Kind is not (RoomKind.BoilerRoom or RoomKind.EngineRoom))
                continue;
            double qx = (q.X0 + q.X1) / 2;
            if (q.Kind == RoomKind.BoilerRoom && Math.Abs(qx - xc) < steam)
                (steam, SteamX) = (Math.Abs(qx - xc), qx);
            if (Math.Abs(q.X0 - X1) < 0.6 || Math.Abs(q.X1 - X0) < 0.6)
                foreach (double side in (ReadOnlySpan<double>)[-1, 1])
                    ops.Add(new Opening(OpeningKind.Vent, -1, new P3(qx, side * 3, hull.DeckTop(qx) - wl + 4), Unit(0, 0.1 * side, 1), 1.2, 60,
                        t0 + VentS));
        }
        // the roof lifts and the barbette is an opening, capped by its column
        if (!double.IsNaN(BarbetteR))
            ops.Add(new Opening(OpeningKind.Barbette, -1, new P3(BarbetteX, BarbetteY, BarbetteZ), new P3(0, 0, 1), 2 * BarbetteR, 120, t0 + LiftS));
        openings = [.. ops];
    }

    static P3 Unit(double x, double y, double z)
    {
        double l = Math.Sqrt(x * x + y * y + z * z);
        return new P3(x / l, y / l, z / l);
    }
}
