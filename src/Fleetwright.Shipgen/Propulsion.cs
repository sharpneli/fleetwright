namespace Fleetwright.Shipgen;

/// <summary>propulsion: the propulsion train for the hitboxes: shafts, shaft alleys, propellers and rudders. Built once
/// per ship from the finished layout; nothing here is weighed.</summary>
public static class Propulsion
{
    public static readonly (double X0, double X1, double Hw) STEERING = (0.03, 0.08, 0.25);
    const double SHAFT_Y = 0.28, STAGGER = 0.05, DP_K = 1.2, DP_K_PLANING = 0.7, DP_T = 0.75, DP_MIN = 0.3;
    const double RUDDER_K = 0.017, RUDDER_H = 0.7, RUDDER_BALANCE = 0.25, RUDDER_THICK = 0.15, STOCK_AT = 0.2;
    public const double ALLEY_W = 2.4, ALLEY_H = 2.6;
    const double SHAFT_ABOVE_IB = 1.2;
    public const double SHAFT_R = 0.3;

    /// <summary>The steering gear's stretch (x0, x1) by the rule.</summary>
    public static (double, double) SteeringSpan(double L) => (-L / 2 + STEERING.X0 * L, -L / 2 + STEERING.X1 * L);

    /// <summary>n positions across: a centreline one when n is odd, pairs out to y_out. [(y, rank)], starboard to port.</summary>
    public static List<(double Y, long Rank)> Spread(long n, double yOut)
    {
        long pairs = n / 2;
        var out_ = n % 2 != 0 ? new List<(double, long)> { (0.0, 0) } : [];
        for (long k = 1; k <= pairs; k++)
        {
            double y = yOut * k / pairs;
            out_.Add((y, k));
            out_.Add((-y, k));
        }
        return out_.OrderBy(v => -v.Item1).ToList();
    }

    static List<(double X0, double X1, string? Id)> EngineRooms(Layout lay)
    {
        var rooms = lay.Compartments.Where(c => Py.Eq(c["kind"], "engine_room"))
            .Select(c => (c.F("x0"), c.F("x1"), (string?)c.S("id"))).ToList();
        if (rooms.Count == 0 && lay.Geo.Machinery is { } gm)
            rooms = [(gm.X0, gm.X1, null)];
        return rooms.OrderBy(r => -r.Item2).ToList();
    }

    /// <summary>The stern gear, which the hull's lines must make room for: dict(screws, planing, propellers, rudders,
    /// shafts), heights above the keel.</summary>
    public static PyDict Gear(Layout lay, Design design, Navarch.Result res)
    {
        double L = lay.Hull.L, B = lay.Hull.B;
        double D = res.Depth, T = res.Draught;
        long n = Math.Max(1L, res.PlantRated.Shafts);
        long nR = Math.Max(1L, design.Machinery?.Rudders ?? 1);
        bool planing = design.StyleName == "planing";
        double ib = planing ? 0.0 : Powerplant.DoubleBottom(D);
        double mw = res.PowerShp * 0.7457 / 1000.0 / n;
        double dp = Math.Max(DP_MIN, (planing ? DP_K_PLANING : DP_K) * Math.Pow(mw, 0.4));
        if (!planing)
            dp = Math.Min(dp, DP_T * T);
        var (st0, st1) = lay.Geo.SteeringSpan(L);
        double xR = st0 + STOCK_AT * (st1 - st0);

        double area = RUDDER_K * L * T / nR;
        double hR = RUDDER_H * T;
        double chord = area / hR;
        var shaftYs = Spread(n, SHAFT_Y * B);
        double inner = shaftYs.Select(s => Math.Abs(s.Y)).Where(a => a > 1e-6).DefaultIfEmpty(0.1 * B).Min();
        var rYs = Spread(nR, inner).Select(s => s.Y).ToList();
        double rTop = (planing ? 0.15 : 0.85) * T;
        var rudders = new List<object?>();
        for (int k = 0; k < rYs.Count; k++)
        {
            string rid = nR == 1 ? "Rudder" : $"Rudder {k + 1}";
            rudders.Add(PyDict.Of(("id", rid), ("x", xR), ("y", rYs[k]), ("chord", chord),
                ("x0", xR - (1 - RUDDER_BALANCE) * chord), ("x1", xR + RUDDER_BALANCE * chord),
                ("thick", Math.Max(0.1, RUDDER_THICK * chord)), ("base", rTop - hR - (planing ? 0.3 * T : 0.0)),
                ("top", rTop), ("area_m2", area)));
        }

        double xP0 = xR + RUDDER_BALANCE * chord + 0.25 * dp + 0.3;
        double zP = planing ? -0.55 * dp : Math.Min(0.05 * T + 0.5 * dp, T - 0.6 * dp);
        var rooms = EngineRooms(lay);
        long pairs = n / 2;
        long groups = pairs + n % 2;
        var shafts = new List<object?>();
        var props = new List<object?>();
        var mach = lay.Geo.Machinery;
        for (int k = 0; k < shaftYs.Count; k++)
        {
            var (y, rank) = shaftYs[k];
            string sid = $"Shaft {k + 1}", pid = $"Propeller {k + 1}";
            long g = pairs - rank;
            (double X0, double X1, string? Id)? room = rooms.Count > 0
                ? rooms[(int)Math.Min(rooms.Count - 1, g * rooms.Count / groups)] : null;
            double xs = room is { } rm ? (rm.X0 + rm.X1) / 2 : (mach is { } mm ? mm.X0 : -0.2 * L);
            double zs = ib + SHAFT_ABOVE_IB;
            double xp = xP0 + STAGGER * L * (rank - (n % 2 == 0 ? 1 : 0));
            xp = Math.Min(xp, xs - 1.0);
            string pos = Math.Abs(y) < 1e-6 ? "centre" : pairs == 1 ? "wing" : rank == pairs ? "outer" : "inner";
            shafts.Add(PyDict.Of(("id", sid), ("y", y), ("position", pos), ("engine_room", room?.Id), ("propeller", pid),
                ("p0", new object?[] { xs, y, zs }), ("p1", new object?[] { xp, y, zP })));
            props.Add(PyDict.Of(("id", pid), ("x", xp), ("y", y), ("z", zP), ("diameter", dp), ("shaft", sid), ("position", pos)));
        }
        return PyDict.Of(("screws", n), ("planing", planing), ("ib", ib), ("shafts", shafts), ("propellers", props),
            ("rudders", rudders), ("rated_mw_per_shaft", mw));
    }

    static (double X, double Y, double Z) P3(object? v)
    {
        var a = (object?[])v!;
        return (Py.ToDouble(a[0]), Py.ToDouble(a[1]), Py.ToDouble(a[2]));
    }

    /// <summary>The propulsion train: dict(shafts, propellers, rudders, alleys) and the steering gear's room id.</summary>
    public static PyDict Build(Layout lay, Design design, Navarch.Result res, HullForm form, PyDict? gr = null)
    {
        gr ??= Gear(lay, design, res);
        var mach = lay.Geo.Machinery;
        var shafts = new List<object?>();
        var alleys = new List<object?>();
        var grShafts = gr.L("shafts").Cast<PyDict>().ToList();
        for (int k = 0; k < grShafts.Count; k++)
        {
            var sh = grShafts[k];
            var (xs, y, zs) = P3(sh["p0"]);
            var (xp, _, zP) = P3(sh["p1"]);
            double ZAt(double x) => xs != xp ? zs + (zP - zs) * (xs - x) / (xs - xp) : zs;
            double exitX = xp + 0.3;
            long steps = Math.Max(2L, (long)((xs - xp) / 0.5));
            for (long j = 0; j <= steps; j++)
            {
                double x = xs - (xs - xp) * j / steps;
                if (Math.Abs(y) + SHAFT_R + 0.2 > form.HalfWidth(x, ZAt(x)))
                {
                    exitX = x;
                    break;
                }
            }
            var shOut = sh.Copy();
            shOut["exit_x"] = exitX;
            shafts.Add(shOut);
            double a0 = mach is { } mm ? mm.X0 : xs;
            if (exitX < a0 - 0.5)
            {
                double zt0 = ZAt(a0), zt1 = ZAt(exitX);
                alleys.Add(PyDict.Of(("id", $"Shaft alley {k + 1}"), ("shaft", sh["id"]), ("x0", exitX), ("x1", a0),
                    ("y", y), ("base", Math.Max(gr.F("ib"), Math.Min(zt0, zt1) - ALLEY_H / 2)), ("top", Math.Max(zt0, zt1) + ALLEY_H / 2)));
                shOut["alley"] = ((PyDict)alleys[^1]!)["id"];
            }
        }
        var steering = lay.Compartments.FirstOrDefault(c => Py.Eq(c["kind"], "steering"))?["id"];
        return PyDict.Of(("shafts", shafts), ("propellers", gr["propellers"]), ("rudders", gr["rudders"]),
            ("alleys", alleys), ("rated_mw_per_shaft", gr["rated_mw_per_shaft"]), ("steering", steering));
    }

    /// <summary>Link the train into the subdivision: each engine room lists its shafts, the steering gear its rudders,
    /// and every cell a shaft or alley passes through lists it in "through". D: the hull's depth.</summary>
    public static void Link(PyDict tr, PyDict sub, double D)
    {
        var rooms = new Dictionary<string, PyDict>(StringComparer.Ordinal);
        foreach (PyDict r in sub.L("rooms").Cast<PyDict>())
            rooms[r.S("id")] = r;
        foreach (PyDict sh in tr.L("shafts").Cast<PyDict>())
            if (sh["engine_room"] is string er && rooms.TryGetValue(er, out var room))
                ((List<object?>)room.SetDefault("shafts", new List<object?>())!).Add(sh["id"]);
        if (tr["steering"] is string st && rooms.TryGetValue(st, out var sr))
            sr["rudders"] = tr.L("rudders").Cast<PyDict>().Select(rd => rd["id"]).ToList();
        foreach (PyDict c in sub.L("cells").Cast<PyDict>())
        {
            double cx0 = c.F("x0"), cx1 = c.F("x1"), cy0 = c.F("y0"), cy1 = c.F("y1"), cb = c.F("base"), ct = c.F("top");
            bool Inside(double x, double y, double zz) => cx0 <= x && x < cx1 && cy0 <= y && y < cy1 && cb <= zz && zz < ct;
            foreach (PyDict sh in tr.L("shafts").Cast<PyDict>())
            {
                var (x0, y, z0) = P3(sh["p0"]);
                var (x1, _, z1) = P3(sh["p1"]);
                double exitX = sh.F("exit_x");
                if (!(cy0 <= y && y < cy1 && cx0 < x0 && cx1 > exitX))
                    continue;
                long n = Math.Max(2L, (long)((x0 - exitX) / 0.5));
                bool any = false;
                for (long k = 0; k <= n; k++)
                {
                    double x = x0 - (x0 - exitX) * k / n;
                    if (Inside(x, y, z0 + (z1 - z0) * (x0 - x) / (x0 - x1) - D))
                    {
                        any = true;
                        break;
                    }
                }
                if (any)
                    ((List<object?>)c.SetDefault("through", new List<object?>())!).Add(sh["id"]);
            }
            foreach (PyDict a in tr.L("alleys").Cast<PyDict>())
            {
                double hw = ALLEY_W / 2;
                if (Math.Min(cx1, a.F("x1")) - Math.Max(cx0, a.F("x0")) > 0.05 &&
                    Math.Min(cy1, a.F("y") + hw) - Math.Max(cy0, a.F("y") - hw) > 0.05 &&
                    Math.Min(ct, a.F("top") - D) - Math.Max(cb, a.F("base") - D) > 0.05)
                    ((List<object?>)c.SetDefault("through", new List<object?>())!).Add(a["id"]);
            }
        }
    }
}
