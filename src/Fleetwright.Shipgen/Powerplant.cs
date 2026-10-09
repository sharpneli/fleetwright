namespace Fleetwright.Shipgen;

/// <summary>powerplant: the propulsion plant from its technology and the design's choices (research/powerplant-model.md).
/// Shaft power in kW inside this module (the rest uses shp), weights in tonnes, metres.</summary>
public static class Powerplant
{
    public const double KW_PER_SHP = 0.7457;
    const double CASING = 3.0;
    const int MAX_FUNNELS = 60;
    static readonly Dictionary<string, string[]> ARRANGEMENTS = new(StringComparer.Ordinal)
    {
        ["grouped"] = ["boiler", "engine"],
        ["unit"] = ["boiler", "engine", "boiler", "engine"],
    };
    static readonly string[] ArrangementNames = ["grouped", "unit"];
    const int MAX_GROUPS = 8;
    const double GROUP_K = 0.05;
    const double ROOM_GANGWAY = 2.0;
    public const double STEEL_FRAME = 0.92;
    const double DOUBLE_BOTTOM_FRAC = 0.07;
    const double DOUBLE_BOTTOM_MIN = 1.0;

    public static readonly IReadOnlyDictionary<string, (double Lhv, double Stowage)> FUELS =
        new Dictionary<string, (double, double)>(StringComparer.Ordinal)
        {
            ["coal"] = (30.0, 1.30), ["oil"] = (41.0, 1.07), ["diesel"] = (42.8, 1.19), ["petrol"] = (44.0, 1.37),
        };
    static readonly string[] FuelNames = ["coal", "oil", "diesel", "petrol"];

    public static readonly IReadOnlyDictionary<string, (double[] Pts, double Over)> CURVES =
        new Dictionary<string, (double[], double)>(StringComparer.Ordinal)
        {
            ["REC"] = ([1.45, 1.20, 1.07, 1.02, 1.00], 0.5),
            ["DT"] = ([2.60, 1.90, 1.40, 1.12, 1.00], 0.3),
            ["GTB"] = ([1.90, 1.45, 1.18, 1.05, 1.00], 0.3),
            ["DSL"] = ([1.25, 1.10, 1.03, 1.00, 1.01], 0.4),
            ["GTS"] = ([2.80, 1.85, 1.35, 1.12, 1.00], 0.2),
            ["ICR"] = ([1.35, 1.12, 1.03, 1.00, 1.00], 0.2),
        };
    static readonly string[] CurveNames = ["REC", "DT", "GTB", "DSL", "GTS", "ICR"];
    public static readonly double[] CURVE_LOADS = [0.10, 0.25, 0.50, 0.75, 1.00];
    static readonly string[] STEAM_CURVES = ["REC", "DT", "GTB"];

    static readonly string[] DRAUGHT_SYSTEMS = ["natural", "forced_boost", "forced", "exhaust"];
    public const double AMBIENT_K = 288.0;
    static readonly Dictionary<string, double> SMOKE_K = new(StringComparer.Ordinal)
        { ["coal_natural"] = 4.0, ["coal"] = 3.0, ["oil"] = 1.5, ["oil_heated"] = 1.0, ["diesel"] = 0.5, ["petrol"] = 0.5 };

    /// <summary>A mature-ish 1940 high-pressure geared turbine plant: the default tech.</summary>
    public static PyDict DefaultTech() => PyDict.Of(
        ("name", "High-pressure geared turbines (1940)"), ("fuel", "oil"), ("weight_kg_per_kw", 35.2),
        ("stress_floor", 0.5), ("sfc_g_per_kwh", 349L), ("density_t_per_m3", 0.34), ("unit_max_mw", 42.8),
        ("unit", PyDict.Of(("mw", 30L), ("height_m", 5.0), ("width_m", 5.0), ("length_m", 8L))),
        ("boiler_fraction", 0.5), ("crew_k", 10L), ("part_load", "GTB"),
        ("draught", PyDict.Of(("system", "forced"), ("velocity_m_s", 14.0), ("reach_m", 27.0), ("gas_temp_k", 450L),
            ("air_fuel_ratio", 15L))));

    static PyDict Choices() => PyDict.Of(("stress", 0.0), ("shafts", null), ("units_per_shaft", 1L),
        ("transmission", "mechanical"), ("arrangement", "grouped"), ("centreline_bulkhead", false), ("bunkers", null),
        ("wing_bunker_m", 2.0), ("rudders", 1L));

    /// <summary>The design's plant: its tech over the default tech, and its choices over the defaults.</summary>
    public static PyDict Spec(PyDict design, PyDict? defaultTech = null)
    {
        var m = design.DOr("machinery");
        var b = defaultTech ?? DefaultTech();
        var mt = m.Or("tech", null) as PyDict;
        var tech = PyDict.Merge(b, mt);
        tech["unit"] = PyDict.Merge(b.D("unit"), (mt?.Or("unit", null) as PyDict));
        tech["draught"] = PyDict.Merge(b.D("draught"), (mt?.Or("draught", null) as PyDict));
        var choices = Choices();
        var p = PyDict.Merge(choices, m.Where(choices.Has));
        p["tech"] = tech;
        if (p["bunkers"] is null)
            p["bunkers"] = Py.Eq(tech["fuel"], "coal") ? "wing" : "ends";
        return p;
    }

    public static List<string> Validate(PyDict design, PyDict? defaultTech = null)
    {
        var p = Spec(design, defaultTech);
        var t = p.D("tech");
        var errs = new List<string>();
        if (!(t["fuel"] is string f && FUELS.ContainsKey(f)))
            errs.Add($"machinery.tech.fuel = {Py.Repr(t["fuel"])}: use {string.Join(", ", FuelNames)}");
        if (!(t["part_load"] is string pl && CURVES.ContainsKey(pl)))
            errs.Add($"machinery.tech.part_load = {Py.Repr(t["part_load"])}: use {string.Join(", ", CurveNames)}");
        var dr = t.D("draught");
        if (!(dr["system"] is string sys && DRAUGHT_SYSTEMS.Contains(sys)))
            errs.Add($"machinery.tech.draught.system = {Py.Repr(dr["system"])}: use {string.Join(", ", DRAUGHT_SYSTEMS)}");
        foreach (var k in new[] { "weight_kg_per_kw", "sfc_g_per_kwh", "density_t_per_m3", "unit_max_mw", "crew_k" })
            if (!(Py.ToDouble(t[k]) > 0))
                errs.Add($"machinery.tech.{k} must be above 0");
        foreach (var k in new[] { "mw", "height_m", "width_m", "length_m" })
            if (!(Py.ToDouble(t.D("unit")[k]) > 0))
                errs.Add($"machinery.tech.unit.{k} must be above 0");
        foreach (var (k, lo, why) in new[]
                 {
                     ("velocity_m_s", 0.0, "the funnel gas must move"),
                     ("gas_temp_k", AMBIENT_K, $"funnel gas no hotter than the air ({Py.F(AMBIENT_K, 0)} K) draws no air"),
                     ("reach_m", -1e-9, "an uptake can't lead a negative distance"),
                     ("air_fuel_ratio", -1e-9, "the boilers can't burn a negative amount of air"),
                 })
            if (dr.Has(k) && !(Py.IsNumber(dr[k]) && Py.ToDouble(dr[k]) > lo))
                errs.Add($"machinery.tech.draught.{k} = {Py.Repr(dr[k])}: {why}");
        if (dr.Has("natural_fraction") && !(Py.IsNumber(dr["natural_fraction"]) && 0 <= Py.ToDouble(dr["natural_fraction"])
                                                                                  && Py.ToDouble(dr["natural_fraction"]) <= 1))
            errs.Add("machinery.tech.draught.natural_fraction must be 0..1");
        double bf = Py.ToDouble(t["boiler_fraction"]);
        if (!(0 <= bf && bf < 1))
            errs.Add("machinery.tech.boiler_fraction must be 0..1");
        double stress = Py.ToDouble(p["stress"]);
        if (!(0 <= stress && stress <= 1))
            errs.Add("machinery.stress must be 0..1");
        if (!Py.In(p["transmission"], "mechanical", "electric"))
            errs.Add($"machinery.transmission = {Py.Repr(p["transmission"])}: use mechanical or electric");
        var a = p["arrangement"];
        if (a is List<object?> al)
        {
            if (!(1 <= al.Count && al.Count <= MAX_GROUPS) || al.Any(k => !Py.In(k, "boiler", "engine")))
                errs.Add($"machinery.arrangement = {Py.Repr(a)}: a list of 1..{MAX_GROUPS} \"boiler\" and \"engine\" groups, " +
                         "forward to aft");
        }
        else if (!(a is string an && ARRANGEMENTS.ContainsKey(an)))
            errs.Add($"machinery.arrangement = {Py.Repr(a)}: use {string.Join(", ", ArrangementNames)} or a list of rooms");
        if (!Py.In(p["bunkers"], "wing", "ends"))
            errs.Add($"machinery.bunkers = {Py.Repr(p["bunkers"])}: use wing or ends");
        if (p["shafts"] is not null && !(Py.IsInt(p["shafts"]) && 1 <= Py.ToLong(p["shafts"]) && Py.ToLong(p["shafts"]) <= 8))
            errs.Add("machinery.shafts must be 1..8");
        if (!(Py.IsInt(p["rudders"]) && 1 <= Py.ToLong(p["rudders"]) && Py.ToLong(p["rudders"]) <= 8))
            errs.Add("machinery.rudders must be 1..8");
        return errs;
    }

    public static bool IsSteam(PyDict p) => p.D("tech").F("boiler_fraction") > 0;

    /// <summary>The plant at its rated power: units, weight, fuel rate, crew, continuous and overload power.</summary>
    public static PyDict Rated(PyDict p, double shp)
    {
        var t = p.D("tech");
        double s = p.F("stress");
        double kw = Py.Max(shp, 1.0) * KW_PER_SHP;
        double mw = kw / 1000.0;
        double unitMax = t.F("unit_max_mw");
        long shafts = Py.Truthy(p["shafts"]) ? Py.ToLong(p["shafts"]) : Py.Max(1L, Py.Min(4L, Py.Ceil(mw / unitMax)));
        long perShaft = Py.Max(p.I("units_per_shaft"), Py.Ceil(mw / shafts / unitMax));
        bool elec = Py.Eq(p["transmission"], "electric");
        double wSpec = t.F("weight_kg_per_kw") * (1 - (1 - t.F("stress_floor")) * s) * (elec ? 1.3 : 1.0);
        double sfc = t.F("sfc_g_per_kwh") * (1 + 0.08 * s) * (elec ? 1.06 : 1.0);
        return PyDict.Of(("kw", kw), ("shafts", shafts), ("units_per_shaft", perShaft), ("units", shafts * perShaft),
            ("unit_mw", mw / (shafts * perShaft)), ("weight_t", kw * wSpec / 1000.0), ("sfc", sfc),
            ("density", t.F("density_t_per_m3") * (0.85 + 0.15 * s)), ("continuous_kw", kw * (1 - 0.15 * s)),
            ("overload", 1 + 0.15 * (1 - s)), ("crew", Py.Round(t.F("crew_k") * Py.Pow(mw, 0.75))),
            ("raised_units", perShaft > p.I("units_per_shaft")));
    }

    public static double CurveMult(string name, double f)
    {
        var (pts, over) = CURVES[name];
        if (f >= 1.0)
            return pts[^1] + over * (f - 1.0) / 0.1;
        if (f <= CURVE_LOADS[0])
            return pts[0] * Py.Pow(CURVE_LOADS[0] / Py.Max(f, 0.02), 0.25);
        for (int i = 0; i < pts.Length - 1; i++)
        {
            double f0 = CURVE_LOADS[i], m0 = pts[i], f1 = CURVE_LOADS[i + 1], m1 = pts[i + 1];
            if (f <= f1)
                return m0 + (m1 - m0) * (f - f0) / (f1 - f0);
        }
        return pts[^1];
    }

    /// <summary>Fuel burnt at shp_load, kg per hour.</summary>
    public static double FuelRate(PyDict p, double shpRated, double shpLoad)
    {
        var r = Rated(p, shpRated);
        double kw = shpLoad * KW_PER_SHP;
        string curve = p.D("tech").S("part_load");
        double f;
        if (STEAM_CURVES.Contains(curve))
            f = kw / r.F("kw");
        else
        {
            double unitKw = r.F("kw") / r.I("units");
            long running = Py.Max(1L, Py.Min(r.I("units"), Py.Ceil(kw / unitKw - 1e-9)));
            f = kw / (running * unitKw);
        }
        return kw * r.F("sfc") * CurveMult(curve, f) / 1000.0;
    }

    public static double DoubleBottom(double depth) => Py.Max(DOUBLE_BOTTOM_MIN, DOUBLE_BOTTOM_FRAC * depth);

    /// <summary>The machinery space for an inside width w_avail and a height h_avail.</summary>
    public static PyDict Space(PyDict p, double shp, double wAvail, double hAvail)
    {
        var t = p.D("tech");
        var r = Rated(p, shp);
        var u = t.D("unit");
        double k = Py.Pow(r.F("unit_mw") / u.F("mw"), 1.0 / 3);
        double hU = u.F("height_m") * k, wU = u.F("width_m") * k, lU = u.F("length_m") * k;
        double hEff = Py.Max(1.0, hU, Py.Min(hAvail, hU + 2.5));
        double pitch = wU + 0.8;
        bool split = Py.Truthy(p["centreline_bulkhead"]);
        double wSide = split ? wAvail / 2 : wAvail;
        long rows = wSide > 0 ? Py.Int(Py.FloorDiv(wSide, pitch)) : 0;
        double used = rows * pitch;
        double wEff = (used + 0.5 * (wSide - used)) * (split ? 2 : 1);
        double volume = r.F("weight_t") / r.F("density");
        double bf = t.F("boiler_fraction");
        double hBoil = Py.Max(1.0, hAvail, hU);
        var order = Groups(p).Order;
        int nB = order.Count(x => x == "boiler"), nE = order.Count(x => x == "engine");
        double groupK = 1 + GROUP_K * Math.Max(0, order.Count - 2);
        double roomMin = lU + ROOM_GANGWAY;
        double boilers = bf * volume / Py.Max(wEff, 0.5) / hBoil * groupK;
        double engines = Py.Max((1 - bf) * volume / Py.Max(wEff, 0.5) / hEff * groupK, roomMin);
        double bEach = nB != 0 ? Py.Max(boilers / nB, nB > 1 ? roomMin : 0.0) : 0.0;
        double eEach = Py.Max(engines / nE, roomMin);
        var lengths = order.Select(x => x == "boiler" ? bEach : eEach).ToList();
        return PyDict.Of(("length", Py.Sum(lengths)), ("boilers", bEach * nB), ("engines", eEach * nE),
            ("order", order.Cast<object?>().ToList()), ("lengths", lengths.Cast<object?>().ToList()), ("rows", rows),
            ("unit", new object?[] { lU, wU, hU }), ("protrusion", Py.Max(0.0, hU - hAvail)), ("volume", volume),
            ("w_eff", wEff), ("h_eff", hEff), ("h_boilers", hBoil), ("fits", rows > 0));
    }

    /// <summary>Where the fuel goes: (tonnes in wing bunkers, length of end bunkers or tanks).</summary>
    public static (double Wing, double End) Bunkers(PyDict p, double fuelT, double length, double wAvail, double hAvail,
        double shipL, double shipB, double cb, double depth, double draught = 0.0, double tds = 0.0)
    {
        string fuel = p.D("tech").S("fuel");
        double stow = FUELS[fuel].Stowage;
        double left = fuelT, wing = 0.0, wEnd;
        if (fuel == "coal")
        {
            if (Py.Eq(p["bunkers"], "wing"))
            {
                wing = Py.Min(left, 2 * p.F("wing_bunker_m") * length * (depth - DoubleBottom(depth)) * 0.9 / stow);
                left -= wing;
            }
            wEnd = wAvail + (Py.Eq(p["bunkers"], "wing") ? 2 * p.F("wing_bunker_m") : 0.0);
        }
        else
        {
            double db = DoubleBottom(depth) * shipL * shipB * cb * 0.6 / stow;
            double layers = 2 * 0.5 * tds * 0.6 * shipL * draught / stow;
            left = Py.Max(0.0, left - db - layers);
            wEnd = wAvail;
        }
        double end = left > 0 ? left * stow / (Py.Max(wEnd, 1.0) * Py.Max(hAvail, 1.0) * 0.9) : 0.0;
        return (wing, end);
    }

    /// <summary>The machinery's groups forward to aft, and warnings for a list that doesn't fit the plant.</summary>
    public static (List<string> Order, List<string> Warns) Groups(PyDict p)
    {
        var a = p["arrangement"];
        var order = a is string s ? ARRANGEMENTS[s].ToList() : ((List<object?>)a!).Cast<string>().ToList();
        var warns = new List<string>();
        if (!IsSteam(p) && order.Contains("boiler"))
        {
            order = order.Where(k => k != "boiler").ToList();
            if (a is not string)
                warns.Add("machinery.arrangement lists boiler groups, but the plant has no boilers; they are left out.");
        }
        if (!order.Contains("engine"))
        {
            order.Add("engine");
            warns.Add("machinery.arrangement has no engine group; the engines go aft of the boilers.");
        }
        if (IsSteam(p) && !order.Contains("boiler"))
        {
            order.Insert(order.IndexOf("engine"), "boiler");
            warns.Add("machinery.arrangement has no boiler group; the boilers go ahead of the engines.");
        }
        return (order, warns);
    }

    /// <summary>The machinery block, forward to aft: [(kind, length)], kind boiler, engine or bunker.</summary>
    public static List<(string Kind, double Len)> Segments(PyDict p, PyDict sp, double endLen)
    {
        double half = endLen / 2;
        var order = sp.L("order").Cast<string>().ToList();
        var lens = sp.L("lengths").Select(Py.ToDouble).ToList();
        var groups = order.Zip(lens).Select(t => (t.First, t.Second)).ToList();
        var out_ = new List<(string, double)>();
        if (half > 0.05)
            out_.Add(("bunker", half));
        if (sp.F("boilers") <= 0.05)
        {
            out_.AddRange(groups.Where(g => g.First == "engine"));
            if (half > 0.05)
                out_.Add(("bunker", half));
            return out_;
        }
        int mid = groups.Count / 2;
        out_.AddRange(groups.Take(mid));
        if (half > 0.05)
            out_.Add(("bunker", half));
        out_.AddRange(groups.Skip(mid));
        return out_;
    }

    // ------------------------------------------------------------------ funnels

    /// <summary>Funnel gas, m3/s per MW of rated power.</summary>
    public static double GasFlow(PyDict p)
    {
        var d = p.D("tech").D("draught");
        double sfc = Rated(p, 1000.0).F("sfc");
        return sfc / 3600.0 * (1 + d.F("air_fuel_ratio", 15)) / (353.0 / d.F("gas_temp_k", 600));
    }

    public static double NaturalVelocity(PyDict p, double stackM, double trunkM = 0.0)
    {
        double t = p.D("tech").D("draught").F("gas_temp_k", 600);
        return 0.3 * Math.Sqrt(2 * 9.81 * Py.Max(stackM, 1.0) * (1 - AMBIENT_K / t)) * Py.Max(0.5, 1 - 0.02 * trunkM);
    }

    /// <summary>Funnels for boiler groups of the given lengths: counts per group, width, length, gas velocity, area.</summary>
    public static PyDict FunnelPlan(PyDict p, double shp, IReadOnlyList<double> groups, double beam, double stackM, long extra = 0)
    {
        var d = p.D("tech").D("draught");
        double mw = Rated(p, shp).F("kw") / 1000.0;
        double q = GasFlow(p);
        string sysname = d.S("system");
        double vNat = NaturalVelocity(p, stackM);
        double area, v;
        if (sysname == "natural")
            (area, v) = (mw * q / vNat, vNat);
        else if (sysname == "forced_boost")
        {
            area = Py.Max(mw * q / d.F("velocity_m_s"), mw * d.F("natural_fraction", 0.6) * q / vNat);
            v = d.F("velocity_m_s");
        }
        else
        {
            v = d.F("velocity_m_s", 14.0);
            area = mw * q / v;
        }
        double wMax = Py.Min(0.22 * beam, 7.0);
        double aMax = 0.785 * wMax * 1.5 * wMax;
        long nArea = Py.Ceil(area / aMax - 1e-9);
        double reach = d.F("reach_m", 10.0);
        var counts = new List<long>();
        foreach (var g in groups)
        {
            double lF = 1.5 * Py.Min(wMax, Math.Sqrt(area / Py.Max(1L, nArea) / (0.785 * 1.5)));
            counts.Add(Py.Max(1L, Py.Ceil(g / (2 * reach + lF) - 1e-9)));
        }
        if (counts.Count == 0)
            counts = [1];
        long want = Py.Max(nArea, counts.Sum()) + extra;
        long needed = want;
        if (want > MAX_FUNNELS)
        {
            want = Py.Max(MAX_FUNNELS, counts.Count);
            while (counts.Sum() > want)
            {
                long mx = counts.Max();
                counts[counts.IndexOf(mx)] -= 1;
            }
        }
        while (counts.Sum() < want)
        {
            int j = Py.MaxBy(Enumerable.Range(0, counts.Count), k => (groups.Count > 0 ? groups[k] : 1) / (double)counts[k]);
            counts[j] += 1;
        }
        long n = counts.Sum();
        double w = Py.Min(wMax, Math.Sqrt(CASING * area / n / (0.785 * 1.5)));
        w = Py.Max(w, sysname != "exhaust" ? 2.2 : 1.0);
        if (needed > n)
            v *= Py.Max(1.0, area / (n * 0.785 * w * 1.5 * w / CASING));
        var o = PyDict.Of(("counts", counts.Cast<object?>().ToList()), ("width", w), ("length", 1.5 * w), ("velocity", v),
            ("area", area), ("gas", q), ("reach", reach));
        if (needed > n)
            o["needed"] = needed;
        return o;
    }

    /// <summary>How far aft of a funnel its smoke blinds a control position.</summary>
    public static double SmokeReach(PyDict p, double shp)
    {
        var t = p.D("tech");
        var d = t.D("draught");
        string fuel = t.S("fuel");
        double k;
        if (fuel == "coal")
            k = SMOKE_K[Py.Eq(d["system"], "natural") ? "coal_natural" : "coal"];
        else if (fuel == "oil")
            k = SMOKE_K[d.F("gas_temp_k", 600) <= 480 ? "oil_heated" : "oil"];
        else
            k = SMOKE_K[fuel];
        return k * Math.Sqrt(Rated(p, shp).F("kw") / 1000.0 * GasFlow(p));
    }

    /// <summary>(funnel, uptake) tonnes.</summary>
    public static (double Funnel, double Uptake) FunnelWeight(double w, double l, double height, double uptakeVertical,
        double uptakeHorizontal)
    {
        double per = Math.PI * w + 2 * Py.Max(0.0, l - w);
        return (0.12 * per * height, 0.18 * per * (uptakeVertical + 1.5 * uptakeHorizontal));
    }

    /// <summary>The plant's static numbers for the game (report "plant").</summary>
    public static PyDict Published(PyDict p, double shp, PyDict? extra = null)
    {
        var t = p.D("tech");
        var r = Rated(p, shp);
        string curve = t.S("part_load");
        var o = PyDict.Of(("name", t.Get("name", "")), ("fuel", t["fuel"]), ("rated_kw", Py.Round(r.F("kw"))),
            ("rated_shp", Py.Round(shp, -1)), ("continuous_kw", Py.Round(r.F("continuous_kw"))),
            ("overload_max", Py.Round(r.F("overload"), 3)), ("shafts", r["shafts"]), ("units", r["units"]),
            ("unit_mw", Py.Round(r.F("unit_mw"), 2)), ("weight_t", Py.Round(r.F("weight_t"), 1)),
            ("sfc_g_per_kwh", Py.Round(r.F("sfc"), 1)),
            ("part_load", PyDict.Of(("curve", t["part_load"]), ("loads", CURVE_LOADS.Cast<object?>().ToList()),
                ("multipliers", CURVES[curve].Pts.Cast<object?>().ToList()), ("overload_per_tenth", CURVES[curve].Over))),
            ("draught", t.D("draught")["system"]), ("stress", p["stress"]), ("transmission", p["transmission"]),
            ("arrangement", p["arrangement"]), ("crew", r["crew"]));
        if (Py.Eq(t.D("draught")["system"], "forced_boost"))
            o["natural_fraction"] = t.D("draught").Get("natural_fraction", 0.6);
        if (extra != null)
            o.Update(extra);
        return o;
    }
}
