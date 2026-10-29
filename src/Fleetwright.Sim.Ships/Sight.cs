using Fleetwright.Shipgen;
namespace Fleetwright.Sim.Ships;

/// <summary>What an observation post is: a director (with its rangefinder), the bridge, aft control, the conning tower's
/// slits, a lookout in a mast's top, a rangefinder in a gunhouse's hood, or a control position's rangefinder in its hood
/// (after control's, the conning tower's). Masthead was a lookout under a bare mast's head, a placeholder until tops
/// (fire-control.md F4d): no eye is of it now.</summary>
public enum EyeKind : byte
{
    Masthead,
    Director,
    Bridge,
    AftControl,
    ConningTower,
    Top,
    Turret,
    Rangefinder,
}

/// <summary>An observation post (docs/battle/plan.md step 3.2a): its component, where it is (ship-local x, y), its eye's
/// height over the waterline at rest (m), its rangefinder's base (m, 0 for none) and the bearings it can't see along, a
/// bit per sector of <see cref="Sight.SectorDeg"/> clockwise from dead ahead (sector k covers k·5.625° to (k+1)·5.625°):
/// those where a taller part of the ship stands in the way; a director's, its rangefinder's make (its battery's fit,
/// fire-control.md F3); and what it stands on (F5: how much it shakes).</summary>
public readonly record struct EyeRow(EyeKind Kind, int Component, double X, double Y, double HeightM, double RangefinderM, ulong Blind,
    OpticsRow Optics = default, InstrumentSupport Support = InstrumentSupport.Deck)
{
    /// <summary>Whether it can see along a bearing relative to the bow, degrees clockwise.</summary>
    public bool Sees(double relativeDeg) => (Blind & (1UL << Sight.Sector(relativeDeg))) == 0;

    /// <summary>Whether it takes ranges with a rangefinder: a director's or a gunhouse's, of a make that's read.</summary>
    public bool Ranges => Kind is EyeKind.Director or EyeKind.Turret or EyeKind.Rangefinder && RangefinderM > 0 && Optics.Reads;
}

/// <summary>What a ship shows to an observer (docs/battle/plan.md step 3.2a): its highest point over the waterline at
/// rest; the area it shows side on and end on above each whole metre over the waterline (<see cref="Sight.AreaAbove"/>),
/// in the class's Numbers pool from 0 m up; and what identification reads: its funnels, masts and main mounts, and its
/// length.</summary>
public readonly record struct SilhouetteRow(double TopM, Slice SideAreaM2, Slice EndAreaM2, int Funnels, int Masts, int MainMounts, double LengthM);

/// <summary>Sighting's facts about a class, compiled from its own rows (one physical model: no look of its own): its
/// observation posts and their blind arcs, and its silhouette.</summary>
public static class Sight
{
    public const int Sectors = 64;
    public const double SectorDeg = 360.0 / Sectors;

    /// <summary>An eye in a hood or a tower stands this high over the floor it's on, m (shipgen's FireControl.EyeH).</summary>
    public const double EyeOverFloorM = 1.5;

    /// <summary>The conning tower's slits are this far under its roof, m.</summary>
    public const double SlitsUnderRoofM = 0.6;

    /// <summary>Something in the way blinds a sector only if it's this wide across the line of sight, m: a mast, a yard's
    /// stay or a rangefinder's arm is looked past.</summary>
    const double MaskMinWidthM = 1.5;

    /// <summary>The silhouette's raster, m: fine enough for a mast's pole (F4d; it was 0.5 m).</summary>
    const double Cell = 0.25;

    /// <summary>How wide a mast's lower part shows side on and end on, m [EST]: a pole's 0.8 m tube; a tripod's leg and
    /// its two struts raked aft (side on the struts in line, one and a half legs' worth on average as they close to the
    /// apex; end on two); a cage's or a lattice's thin struts round a ~5 m envelope, about 0.3 of it solid; a tower's
    /// plated column (its hitbox).</summary>
    static (double Side, double End) MastWidthM(MastKind k) => k switch
    {
        MastKind.Tripod => (1.2, 1.6),
        MastKind.Cage or MastKind.Lattice => (1.5, 1.5),
        MastKind.Tower => (3.6, 3.6),
        _ => (0.8, 0.8),
    };

    /// <summary>A topmast's pole over a mast's top (or over 0.6 of a bare mast's height), m wide [EST].</summary>
    const double TopmastWidthM = 0.4, BareLowerShare = 0.6;

    public static int Sector(double relativeDeg)
    {
        double a = relativeDeg % 360;
        if (a < 0)
            a += 360;
        return Math.Min((int)(a / SectorDeg), Sectors - 1);
    }

    /// <summary>The area a table holds above a height over the waterline, m² (linear between its metres; nothing above
    /// its top).</summary>
    public static double AreaAbove(ReadOnlySpan<double> table, double heightM)
    {
        if (table.Length == 0)
            return 0;
        if (heightM <= 0)
            return table[0];
        int i = (int)heightM;
        if (i >= table.Length - 1)
            return 0;
        double f = heightM - i;
        return table[i] + (table[i + 1] - table[i]) * f;
    }

    /// <summary>The posts: a lookout in each mast's top (a crow's nest's too), each director in its hood, each control
    /// position's and gunhouse's rangefinder, the highest block of the bridge and of aft control, and the conning
    /// tower.</summary>
    internal static EyeRow[] Eyes(ShipClass c)
    {
        var comps = c.components;
        double wl = c.Vertical.Waterline;   // under the main deck: a height over the deck less this is over the sea
        var eyes = new List<EyeRow>();
        int bridge = -1, aft = -1;
        for (int i = 0; i < comps.Length; i++)
        {
            var x = comps[i];
            switch (x.Kind)
            {
                case ComponentKind.Mast:
                    // a lookout in its top (fire-control.md F4: its floor + the eye's height, a crow's nest's too); a bare
                    // mast has none (F4d)
                    int top = TopOf(c, i);
                    if (top >= 0)
                        eyes.Add(Eye(c, EyeKind.Top, top, x.X, x.Y, comps[top].Bounds.Base + EyeOverFloorM - wl, 0) with { Support = Kinds.SupportOf(x.Mast) });
                    break;
                case ComponentKind.ConningTower:
                    eyes.Add(Eye(c, EyeKind.ConningTower, i, x.X, x.Y, x.Bounds.Top - SlitsUnderRoofM - wl, 0) with { Support = InstrumentSupport.Tower });
                    break;
                case ComponentKind.Superstructure when x.Role == ComponentRole.Director:
                    var b = x.Bounds;
                    double rf = x.Detail >= 0 ? c.directors[x.Detail].RangefinderM : 0;
                    eyes.Add(Eye(c, EyeKind.Director, i, (b.X0 + b.X1) / 2, (b.Y0 + b.Y1) / 2, b.Base + EyeOverFloorM - wl, double.IsNaN(rf) ? 0 : rf) with
                    {
                        Optics = x.Detail >= 0 ? OpticsOf(c, c.directors[x.Detail].Battery) : default, Support = SupportUnder(c, i),
                    });
                    break;
                case ComponentKind.Superstructure when x.Role == ComponentRole.Rangefinder:
                    var rb = x.Bounds;
                    eyes.Add(Eye(c, EyeKind.Rangefinder, i, (rb.X0 + rb.X1) / 2, (rb.Y0 + rb.Y1) / 2, rb.Base + EyeOverFloorM - wl, x.RangefinderM) with
                    {
                        Optics = OpticsOf(c, Battery.Main), Support = SupportUnder(c, i),
                    });
                    break;
                case ComponentKind.Superstructure when x.Role == ComponentRole.Bridge:
                    if (bridge < 0 || x.Bounds.Base > comps[bridge].Bounds.Base)
                        bridge = i;
                    break;
                case ComponentKind.Superstructure when x.Role == ComponentRole.AftControl:
                    if (aft < 0 || x.Bounds.Base > comps[aft].Bounds.Base)
                        aft = i;
                    break;
            }
        }
        foreach (var (i, kind) in new[] { (bridge, EyeKind.Bridge), (aft, EyeKind.AftControl) })
            if (i >= 0)
            {
                var b = comps[i].Bounds;
                eyes.Add(Eye(c, kind, i, (b.X0 + b.X1) / 2, (b.Y0 + b.Y1) / 2, b.Base + EyeOverFloorM - wl, 0));
            }
        return [.. eyes.OrderByDescending(e => e.HeightM).ThenBy(e => e.Component)];
    }

    /// <summary>What a post stands on (fire-control.md F5): the part under its floor, across its middle: a mast's top
    /// (the mast's kind), the conning tower, a gunhouse, else a superstructure's roof.</summary>
    static InstrumentSupport SupportUnder(ShipClass c, int post)
    {
        var comps = c.components;
        var b = comps[post].Bounds;
        double x = (b.X0 + b.X1) / 2, y = (b.Y0 + b.Y1) / 2;
        for (int i = 0; i < comps.Length; i++)
        {
            var o = comps[i];
            if (i == post || Math.Abs(o.Bounds.Top - b.Base) > SupportGapM || x < o.Bounds.X0 || x > o.Bounds.X1 || y < o.Bounds.Y0 || y > o.Bounds.Y1)
                continue;
            switch (o.Kind)
            {
                case ComponentKind.Top:
                    for (int m = 0; m < comps.Length; m++)
                        if (comps[m].Kind == ComponentKind.Mast && c.componentIds[i].StartsWith(c.componentIds[m] + " top", StringComparison.Ordinal))
                            return Kinds.SupportOf(comps[m].Mast);
                    return InstrumentSupport.Pole;
                case ComponentKind.ConningTower:
                    return InstrumentSupport.Tower;
                case ComponentKind.Main or ComponentKind.Secondary:
                    return InstrumentSupport.Turret;
            }
        }
        return InstrumentSupport.Deck;
    }

    /// <summary>How far a post's floor may be from the roof under it, m, for that roof to carry it.</summary>
    const double SupportGapM = 0.6;

    /// <summary>A mast's top: the lowest top component it carries (its id's "top" ones), or -1.</summary>
    static int TopOf(ShipClass c, int mast)
    {
        var comps = c.components;
        string prefix = c.componentIds[mast] + " top";
        int best = -1;
        for (int i = 0; i < comps.Length; i++)
            if (comps[i].Kind == ComponentKind.Top && c.componentIds[i].StartsWith(prefix, StringComparison.Ordinal)
                && (best < 0 || comps[i].Bounds.Base < comps[best].Bounds.Base))
                best = i;
        return best;
    }

    /// <summary>A director's rangefinders' make: its battery's fit's optics.</summary>
    static OpticsRow OpticsOf(ShipClass c, Battery battery) =>
        FireControlBlocks.Fit(c.design, FireControlTemplates.FitOf(c.design.FireControl?.Of(battery switch
        {
            Battery.Main => "main",
            Battery.Secondary => "secondary",
            _ => "aa",
        }))).Optics;

    /// <summary>An eye with its blind sectors: each sector whose middle bearing passes through a part of the ship
    /// standing across the eye's height (a line to the horizon runs at about that height, so a director on a roof above
    /// doesn't block it), wide enough to block, and not the eye's own or one it's inside (overlapping blocks of a tower:
    /// the eye looks out from its face). Light AA mounts don't: open guns behind a low shield, each a few degrees
    /// wide and with sea between them, that a post on their deck looks over and past (a row of them along each beam had
    /// blinded Bismarck's secondary directors broadside).</summary>
    static EyeRow Eye(ShipClass c, EyeKind kind, int own, double x, double y, double heightM, double rangefinderM) =>
        new(kind, own, x, y, heightM, rangefinderM, BlindOf(c, own, x, y, heightM));

    /// <summary>The searchlights' blind sectors (docs/battle/night.md Q4), by the eyes' rule from each lamp.</summary>
    internal static void SearchlightArcs(ShipClass c)
    {
        for (int i = 0; i < c.searchlights.Length; i++)
        {
            ref readonly var l = ref c.searchlights[i];
            c.searchlights[i] = l with { Blind = BlindOf(c, l.Component, l.X, l.Y, l.HeightM) };
        }
    }

    /// <summary>The sectors blind from a point this high over the sea on a part (<see cref="Eye"/>'s rule).</summary>
    static ulong BlindOf(ShipClass c, int own, double x, double y, double heightM)
    {
        double eyeZ = heightM + c.Vertical.Waterline;   // back over the main deck, as the components' heights are
        ulong blind = 0;
        var comps = c.components;
        for (int s = 0; s < Sectors; s++)
        {
            double a = (s + 0.5) * SectorDeg * Math.PI / 180;
            double dx = Math.Cos(a), dy = Math.Sin(a);   // +x bow, +y starboard: clockwise from ahead
            for (int i = 0; i < comps.Length && (blind & (1UL << s)) == 0; i++)
            {
                var o = comps[i];
                if (i == own || o.Bounds.Top <= eyeZ || o.Bounds.Base > eyeZ || o.Bounds.Contains(x, y, eyeZ) || o.Kind is ComponentKind.Mast or ComponentKind.Uptake or ComponentKind.Shaft
                        or ComponentKind.ShaftAlley or ComponentKind.Propeller or ComponentKind.Rudder or ComponentKind.TorpedoTube or ComponentKind.Aa)
                    continue;
                if (Crosses(o.Bounds, x, y, dx, dy))
                    blind |= 1UL << s;
            }
        }
        return blind;
    }

    /// <summary>Whether a ray from (x, y) along (dx, dy) passes through a box's plan, and the box is at least
    /// <see cref="MaskMinWidthM"/> across the ray there.</summary>
    static bool Crosses(Box b, double x, double y, double dx, double dy)
    {
        // the slab test against the plan's rectangle, the ray starting at the eye
        double t0 = 0, t1 = double.PositiveInfinity;
        if (!Slab(b.X0 - x, b.X1 - x, dx, ref t0, ref t1) || !Slab(b.Y0 - y, b.Y1 - y, dy, ref t0, ref t1))
            return false;
        // its width across the ray: the box's extent along the ray's normal
        double nx = -dy, ny = dx;
        double across = Math.Abs(nx) * (b.X1 - b.X0) + Math.Abs(ny) * (b.Y1 - b.Y0);
        return across >= MaskMinWidthM;
    }

    static bool Slab(double lo, double hi, double d, ref double t0, ref double t1)
    {
        if (Math.Abs(d) < 1e-12)
            return lo <= 0 && hi >= 0;
        double a = lo / d, b = hi / d;
        if (a > b)
            (a, b) = (b, a);
        t0 = Math.Max(t0, a);
        t1 = Math.Min(t1, b);
        return t0 <= t1;
    }

    /// <summary>The silhouette: the hull from the waterline to its deck, and every part above the water, rastered side
    /// on (x by height) and end on (y by height) at <see cref="Cell"/>, the masts by their kind (their lower part as wide
    /// as <see cref="MastWidthM"/>, a topmast's pole over it) rather than their hitbox's column; the area above each
    /// whole metre over the waterline into the class's Numbers pool.</summary>
    internal static SilhouetteRow Silhouette(ShipClass c, List<double> numbers)
    {
        double wl = c.Vertical.Waterline;
        var hull = c.Hull;
        double top = 0, x0 = hull.X0, x1 = hull.X1, ymax = hull.YMax;
        foreach (var o in c.components)
            if (o.Bounds.Top > wl && Shows(o.Kind))
            {
                top = Math.Max(top, o.Bounds.Top - wl);
                (x0, x1) = (Math.Min(x0, o.Bounds.X0), Math.Max(x1, o.Bounds.X1));
                ymax = Math.Max(ymax, Math.Max(-o.Bounds.Y0, o.Bounds.Y1));
            }
        int nz = (int)Math.Ceiling(top / Cell) + 1, nx = (int)Math.Ceiling((x1 - x0) / Cell) + 1, ny = (int)Math.Ceiling(2 * ymax / Cell) + 1;
        var side = new bool[nx, nz];
        var end = new bool[ny, nz];
        void Fill(bool[,] g, double a0, double a1, double lo, double z0, double z1)
        {
            int i0 = Math.Max((int)Math.Floor((a0 - lo) / Cell), 0), i1 = Math.Min((int)Math.Ceiling((a1 - lo) / Cell), g.GetLength(0));
            int k0 = Math.Max((int)Math.Floor((z0 - wl) / Cell), 0), k1 = Math.Min((int)Math.Ceiling((z1 - wl) / Cell), g.GetLength(1));
            for (int i = i0; i < i1; i++)
                for (int k = k0; k < k1; k++)
                    g[i, k] = true;
        }
        // the hull: its deck's height along it, its beam at the waterline
        for (int i = 0; i < nx; i++)
        {
            double x = x0 + (i + 0.5) * Cell;
            if (x >= hull.X0 && x <= hull.X1)
                Fill(side, x - Cell / 2, x + Cell / 2, x0, wl, hull.DeckTop(x));
        }
        double halfBeam = 0;
        for (double x = hull.X0; x <= hull.X1; x += 1)
            halfBeam = Math.Max(halfBeam, hull.HalfWidth(x, 0));
        double deckTop = 0;
        foreach (double t in hull.DeckTops)
            deckTop = Math.Max(deckTop, t);
        Fill(end, -halfBeam, halfBeam, -ymax, wl, deckTop);
        for (int i = 0; i < c.components.Length; i++)
        {
            var o = c.components[i];
            if (o.Bounds.Top <= wl || !Shows(o.Kind))
                continue;
            if (o.Kind == ComponentKind.Mast)
            {
                var (ws, we) = MastWidthM(o.Mast);
                int t = TopOf(c, i);
                double lower = t >= 0 ? c.components[t].Bounds.Base : o.Bounds.Base + BareLowerShare * (o.Bounds.Top - o.Bounds.Base);
                Fill(side, o.X - ws / 2, o.X + ws / 2, x0, o.Bounds.Base, lower);
                Fill(end, o.Y - we / 2, o.Y + we / 2, -ymax, o.Bounds.Base, lower);
                Fill(side, o.X - TopmastWidthM / 2, o.X + TopmastWidthM / 2, x0, lower, o.Bounds.Top);
                Fill(end, o.Y - TopmastWidthM / 2, o.Y + TopmastWidthM / 2, -ymax, lower, o.Bounds.Top);
                continue;
            }
            Fill(side, o.Bounds.X0, o.Bounds.X1, x0, o.Bounds.Base, o.Bounds.Top);
            Fill(end, o.Bounds.Y0, o.Bounds.Y1, -ymax, o.Bounds.Base, o.Bounds.Top);
        }
        int metres = (int)Math.Ceiling(top) + 1;
        Slice Table(bool[,] g)
        {
            // each raster row's cells, then the rows above each metre
            int n0 = g.GetLength(0), n1 = g.GetLength(1);
            var above = new int[n1 + 1];
            for (int k = n1 - 1; k >= 0; k--)
            {
                int row = 0;
                for (int i = 0; i < n0; i++)
                    row += g[i, k] ? 1 : 0;
                above[k] = above[k + 1] + row;
            }
            int start = numbers.Count;
            for (int m = 0; m < metres; m++)
                numbers.Add(above[Math.Min((int)Math.Round(m / Cell), n1)] * Cell * Cell);
            return new Slice(start, metres);
        }
        int funnels = 0, masts = 0, main = 0;
        foreach (var o in c.components)
            (funnels, masts, main) = o.Kind switch
            {
                ComponentKind.Funnel => (funnels + 1, masts, main),
                ComponentKind.Mast => (funnels, masts + 1, main),
                ComponentKind.Main => (funnels, masts, main + 1),
                _ => (funnels, masts, main),
            };
        return new SilhouetteRow(top, Table(side), Table(end), funnels, masts, main, c.Length);
    }

    /// <summary>Whether a kind of part shows above the water (the ones inside the hull, or under it, don't).</summary>
    static bool Shows(ComponentKind k) => k is not (ComponentKind.Uptake or ComponentKind.Casing or ComponentKind.Shaft or ComponentKind.ShaftAlley
        or ComponentKind.Propeller or ComponentKind.Rudder or ComponentKind.TorpedoTube or ComponentKind.HangarBay);
}
