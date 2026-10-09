namespace Fleetwright.Shipgen;

/// <summary>propulsion: the propulsion train for the hitboxes: shafts, shaft alleys, propellers and rudders. Built once
/// per ship from the finished layout; nothing here is weighed.</summary>
public static class Propulsion
{
    public static readonly (double X0, double X1, double Hw) Steering = (0.03, 0.08, 0.25);
    const double ShaftY = 0.28, Stagger = 0.05, DpK = 1.2, DpKPlaning = 0.7, DpT = 0.75, DpMin = 0.3;
    const double RudderK = 0.017, RudderH = 0.7, RudderBalance = 0.25, RudderThick = 0.15, StockAt = 0.2;
    public const double AlleyW = 2.4, AlleyH = 2.6;
    const double ShaftAboveIb = 1.2;
    public const double ShaftR = 0.3;

    /// <summary>The steering gear's stretch (x0, x1) by the rule.</summary>
    public static (double, double) SteeringSpan(double L) => (-L / 2 + Steering.X0 * L, -L / 2 + Steering.X1 * L);

    /// <summary>n positions across: a centreline one when n is odd, pairs out to y_out. [(y, rank)], starboard to port.</summary>
    public static List<(double Y, long Rank)> Spread(long n, double yOut)
    {
        long pairs = n / 2;
        var result = n % 2 != 0 ? new List<(double, long)> { (0.0, 0) } : [];
        for (long k = 1; k <= pairs; k++)
        {
            double y = yOut * k / pairs;
            result.Add((y, k));
            result.Add((-y, k));
        }
        return result.OrderBy(v => -v.Item1).ToList();
    }

    static List<(double X0, double X1, string? Id)> EngineRooms(Layout lay)
    {
        var rooms = lay.Compartments.Where(c => c.Kind == "engine_room").Select(c => (c.X0, c.X1, (string?)c.Id)).ToList();
        if (rooms.Count == 0 && lay.Geo.Machinery is { } gm)
            rooms = [(gm.X0, gm.X1, null)];
        return rooms.OrderBy(r => -r.Item2).ToList();
    }

    /// <summary>The stern gear, which the hull's lines must make room for; heights above the keel.</summary>
    public static Gear Gear(Layout lay, Design design, Navarch.Result res)
    {
        double L = lay.Hull.L, B = lay.Hull.B;
        double D = res.Depth, T = res.Draught;
        long n = Math.Max(1L, res.PlantRated.Shafts);
        long nR = Math.Max(1L, design.Machinery?.Rudders ?? 1);
        bool planing = design.StyleName == "planing";
        double ib = planing ? 0.0 : Powerplant.DoubleBottom(D);
        double mw = res.PowerShp * 0.7457 / 1000.0 / n;
        double dp = Math.Max(DpMin, (planing ? DpKPlaning : DpK) * Math.Pow(mw, 0.4));
        if (!planing)
            dp = Math.Min(dp, DpT * T);
        var (st0, st1) = lay.Geo.SteeringSpan(L);
        double xR = st0 + StockAt * (st1 - st0);

        double area = RudderK * L * T / nR;
        double hR = RudderH * T;
        double chord = area / hR;
        var shaftYs = Spread(n, ShaftY * B);
        double inner = shaftYs.Select(s => Math.Abs(s.Y)).Where(a => a > 1e-6).DefaultIfEmpty(0.1 * B).Min();
        var rYs = Spread(nR, inner).Select(s => s.Y).ToList();
        double rTop = (planing ? 0.15 : 0.85) * T;
        var rudders = rYs.Select((y, k) => new Rudder(nR == 1 ? "Rudder" : $"Rudder {k + 1}", xR, y, chord, xR - (1 - RudderBalance) * chord,
            xR + RudderBalance * chord, Math.Max(0.1, RudderThick * chord), rTop - hR - (planing ? 0.3 * T : 0.0), rTop, area)).ToList();

        double xP0 = xR + RudderBalance * chord + 0.25 * dp + 0.3;
        double zP = planing ? -0.55 * dp : Math.Min(0.05 * T + 0.5 * dp, T - 0.6 * dp);
        var rooms = EngineRooms(lay);
        long pairs = n / 2;
        long groups = pairs + n % 2;
        var shafts = new List<Shaft>();
        var props = new List<Propeller>();
        var mach = lay.Geo.Machinery;
        for (int k = 0; k < shaftYs.Count; k++)
        {
            var (y, rank) = shaftYs[k];
            string sid = $"Shaft {k + 1}", pid = $"Propeller {k + 1}";
            long g = pairs - rank;
            (double X0, double X1, string? Id)? room = rooms.Count > 0
                ? rooms[(int)Math.Min(rooms.Count - 1, g * rooms.Count / groups)] : null;
            double xs = room is { } rm ? (rm.X0 + rm.X1) / 2 : (mach is { } mm ? mm.X0 : -0.2 * L);
            double zs = ib + ShaftAboveIb;
            double xp = Math.Min(xP0 + Stagger * L * (rank - (n % 2 == 0 ? 1 : 0)), xs - 1.0);
            string pos = Math.Abs(y) < 1e-6 ? "centre" : pairs == 1 ? "wing" : rank == pairs ? "outer" : "inner";
            shafts.Add(new Shaft(sid, y, pos, room?.Id, pid, (xs, y, zs), (xp, y, zP)));
            props.Add(new Propeller(pid, xp, y, zP, dp, sid, pos));
        }
        return new Gear(n, planing, ib, shafts, props, rudders, mw);
    }

    /// <summary>The propulsion train: the gear's shafts (now with where each leaves the hull), the shaft alleys, and the
    /// steering gear's room id.</summary>
    public static Train Build(Layout lay, Design design, Navarch.Result res, HullForm form, Gear? gr = null)
    {
        gr ??= Gear(lay, design, res);
        var mach = lay.Geo.Machinery;
        var shafts = new List<Shaft>();
        var alleys = new List<Alley>();
        for (int k = 0; k < gr.Shafts.Count; k++)
        {
            var sh = gr.Shafts[k];
            var ((xs, y, zs), (xp, _, zP)) = (sh.P0, sh.P1);
            double ZAt(double x) => xs != xp ? zs + (zP - zs) * (xs - x) / (xs - xp) : zs;
            double exitX = xp + 0.3;
            long steps = Math.Max(2L, (long)((xs - xp) / 0.5));
            for (long j = 0; j <= steps; j++)
            {
                double x = xs - (xs - xp) * j / steps;
                if (Math.Abs(y) + ShaftR + 0.2 > form.HalfWidth(x, ZAt(x)))
                {
                    exitX = x;
                    break;
                }
            }
            double a0 = mach is { } mm ? mm.X0 : xs;
            string? alley = null;
            if (exitX < a0 - 0.5)
            {
                double zt0 = ZAt(a0), zt1 = ZAt(exitX);
                alley = $"Shaft alley {k + 1}";
                alleys.Add(new Alley(alley, sh.Id, exitX, a0, y, Math.Max(gr.Ib, Math.Min(zt0, zt1) - AlleyH / 2), Math.Max(zt0, zt1) + AlleyH / 2));
            }
            shafts.Add(sh with { ExitX = exitX, Alley = alley });
        }
        var steering = lay.Compartments.FirstOrDefault(c => c.Kind == "steering")?.Id;
        return new Train(shafts, gr.Propellers, gr.Rudders, alleys, gr.RatedMwPerShaft, steering);
    }

    /// <summary>Link the train into the subdivision: each engine room lists its shafts, the steering gear its rudders,
    /// and every cell a shaft or alley passes through lists it in Through. D: the hull's depth.</summary>
    public static void Link(Train tr, SubdivisionData sub, double D)
    {
        var rooms = new Dictionary<string, Room>(StringComparer.Ordinal);
        foreach (var r in sub.Rooms)
            rooms[r.Id] = r;
        foreach (var sh in tr.Shafts)
            if (sh.EngineRoom is { } er && rooms.TryGetValue(er, out var room))
                (room.Shafts ??= []).Add(sh.Id);
        if (tr.Steering is { } st && rooms.TryGetValue(st, out var sr))
            sr.Rudders = tr.Rudders.Select(rd => rd.Id).ToList();
        foreach (var c in sub.Cells)
        {
            bool Inside(double x, double y, double zz) => c.X0 <= x && x < c.X1 && c.Y0 <= y && y < c.Y1 && c.Base <= zz && zz < c.Top;
            foreach (var sh in tr.Shafts)
            {
                var ((x0, y, z0), (x1, _, z1)) = (sh.P0, sh.P1);
                double exitX = sh.ExitX;
                if (!(c.Y0 <= y && y < c.Y1 && c.X0 < x0 && c.X1 > exitX))
                    continue;
                long n = Math.Max(2L, (long)((x0 - exitX) / 0.5));
                bool any = false;
                for (long k = 0; k <= n && !any; k++)
                {
                    double x = x0 - (x0 - exitX) * k / n;
                    any = Inside(x, y, z0 + (z1 - z0) * (x0 - x) / (x0 - x1) - D);
                }
                if (any)
                    (c.Through ??= []).Add(sh.Id);
            }
            foreach (var a in tr.Alleys)
            {
                double hw = AlleyW / 2;
                if (Math.Min(c.X1, a.X1) - Math.Max(c.X0, a.X0) > 0.05 && Math.Min(c.Y1, a.Y + hw) - Math.Max(c.Y0, a.Y - hw) > 0.05 &&
                    Math.Min(c.Top, a.Top - D) - Math.Max(c.Base, a.Base - D) > 0.05)
                    (c.Through ??= []).Add(a.Id);
            }
        }
    }
}

/// <summary>A rudder: its stock at X, Chord long from X0 to X1, Thick, from Base to Top above the keel.</summary>
public sealed record Rudder(string Id, double X, double Y, double Chord, double X0, double X1, double Thick, double Base, double Top, double AreaM2);

/// <summary>A shaft from P0 (in its engine room) to P1 (its propeller), heights above the keel; ExitX where it leaves
/// the hull, and its alley (once the train is built).</summary>
public sealed record Shaft(string Id, double Y, string Position, string? EngineRoom, string Propeller, (double X, double Y, double Z) P0,
    (double X, double Y, double Z) P1)
{
    public double ExitX { get; init; }
    public string? Alley { get; init; }
}

public sealed record Propeller(string Id, double X, double Y, double Z, double Diameter, string Shaft, string Position);

/// <summary>The stern gear: Screws shafts (Ib: the inner bottom's height), their propellers, the rudders.</summary>
public sealed record Gear(long Screws, bool Planing, double Ib, List<Shaft> Shafts, List<Propeller> Propellers, List<Rudder> Rudders,
    double RatedMwPerShaft);

public sealed record Alley(string Id, string Shaft, double X0, double X1, double Y, double Base, double Top);

public sealed record Train(List<Shaft> Shafts, List<Propeller> Propellers, List<Rudder> Rudders, List<Alley> Alleys, double RatedMwPerShaft,
    string? Steering);
