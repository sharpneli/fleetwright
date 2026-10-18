using Fleetwright.Shipgen;
namespace Fleetwright.Sim.Ships;

/// <summary>What an observation post is: a lookout at a masthead, a director (with its rangefinder), the bridge, aft
/// control, or the conning tower's slits.</summary>
public enum EyeKind : byte
{
    Masthead,
    Director,
    Bridge,
    AftControl,
    ConningTower,
}

/// <summary>An observation post (docs/battle/plan.md step 3.2a): its component, where it is (ship-local x, y), its eye's
/// height over the waterline at rest (m), its rangefinder's base (m, 0 for none) and the bearings it can't see along, a
/// bit per sector of <see cref="Sight.SectorDeg"/> clockwise from dead ahead (sector k covers k·5.625° to (k+1)·5.625°):
/// those where a taller part of the ship stands in the way; a director's, its rangefinder's make (its battery's fit,
/// fire-control.md F3).</summary>
public readonly record struct EyeRow(EyeKind Kind, int Component, double X, double Y, double HeightM, double RangefinderM, ulong Blind,
    OpticsRow Optics = default)
{
    /// <summary>Whether it can see along a bearing relative to the bow, degrees clockwise.</summary>
    public bool Sees(double relativeDeg) => (Blind & (1UL << Sight.Sector(relativeDeg))) == 0;

    /// <summary>Whether it takes ranges with a rangefinder: a director's, of a make that's read.</summary>
    public bool Ranges => Kind == EyeKind.Director && RangefinderM > 0 && Optics.Reads;
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

    /// <summary>The silhouette's raster, m.</summary>
    const double Cell = 0.5;

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

    /// <summary>The posts: a lookout at each masthead (PLACEHOLDER, DesignerPlaceholders.MastLookoutUnderTopM: shipgen
    /// has no spotting top), each director in its hood, the highest block of the bridge and of aft control, and the
    /// conning tower.</summary>
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
                    eyes.Add(Eye(c, EyeKind.Masthead, i, x.X, x.Y, x.Bounds.Top - DesignerPlaceholders.MastLookoutUnderTopM - wl, 0));
                    break;
                case ComponentKind.ConningTower:
                    eyes.Add(Eye(c, EyeKind.ConningTower, i, x.X, x.Y, x.Bounds.Top - SlitsUnderRoofM - wl, 0));
                    break;
                case ComponentKind.Superstructure when x.Role == ComponentRole.Director:
                    var b = x.Bounds;
                    double rf = x.Detail >= 0 ? c.directors[x.Detail].RangefinderM : 0;
                    eyes.Add(Eye(c, EyeKind.Director, i, (b.X0 + b.X1) / 2, (b.Y0 + b.Y1) / 2, b.Base + EyeOverFloorM - wl, double.IsNaN(rf) ? 0 : rf) with
                    {
                        Optics = x.Detail >= 0 ? OpticsOf(c, c.directors[x.Detail].Battery) : default,
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
    /// the eye looks out from its face).</summary>
    static EyeRow Eye(ShipClass c, EyeKind kind, int own, double x, double y, double heightM, double rangefinderM)
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
                        or ComponentKind.ShaftAlley or ComponentKind.Propeller or ComponentKind.Rudder or ComponentKind.TorpedoTube)
                    continue;
                if (Crosses(o.Bounds, x, y, dx, dy))
                    blind |= 1UL << s;
            }
        }
        return new EyeRow(kind, own, x, y, heightM, rangefinderM, blind);
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
    /// on (x by height) and end on (y by height) at <see cref="Cell"/>; the area above each whole metre over the
    /// waterline into the class's Numbers pool.</summary>
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
        foreach (var o in c.components)
            if (o.Bounds.Top > wl && Shows(o.Kind))
            {
                Fill(side, o.Bounds.X0, o.Bounds.X1, x0, o.Bounds.Base, o.Bounds.Top);
                Fill(end, o.Bounds.Y0, o.Bounds.Y1, -ymax, o.Bounds.Base, o.Bounds.Top);
            }
        int metres = (int)Math.Ceiling(top) + 1;
        Slice Table(bool[,] g)
        {
            int start = numbers.Count;
            int n0 = g.GetLength(0);
            for (int m = 0; m < metres; m++)
            {
                int k0 = (int)Math.Round(m / Cell);
                int cells = 0;
                for (int i = 0; i < n0; i++)
                    for (int k = k0; k < g.GetLength(1); k++)
                        if (g[i, k])
                            cells++;
                numbers.Add(cells * Cell * Cell);
            }
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
