namespace Fleetwright.Shipgen;

/// <summary>A plant technology with nothing left out (TechInput over the style's default).</summary>
public sealed record Tech(string Name, string Fuel, double WeightKgPerKw, double StressFloor, double SfcGPerKwh,
    double DensityTPerM3, double UnitMaxMw, TechUnit Unit, double BoilerFraction, double CrewK, string PartLoad, Draught Draught);

/// <summary>One machinery unit at the tech's reference power: its size in metres.</summary>
public sealed record TechUnit(double Mw, double HeightM, double WidthM, double LengthM);

/// <summary>How the boilers or engines get their air and lose their gas: the system, the funnel gas velocity, how far
/// an uptake may lead, the gas temperature, the air-fuel ratio and (forced boost) the natural share.</summary>
public sealed record Draught(string System, double VelocityMS, double ReachM, double GasTempK, double AirFuelRatio,
    double? NaturalFraction);

/// <summary>The design's plant: its technology and choices, defaults filled in.</summary>
public sealed record PlantSpec(Tech Tech, double Stress, int? Shafts, int UnitsPerShaft, string Transmission,
    Arrangement Arrangement, bool CentrelineBulkhead, string Bunkers, double WingBunkerM, int Rudders)
{
    public bool IsSteam => Tech.BoilerFraction > 0;
}

/// <summary>The plant at its rated power.</summary>
public sealed record RatedPlant(double Kw, long Shafts, long UnitsPerShaft, long Units, double UnitMw, double WeightT, double Sfc,
    double Density, double ContinuousKw, double Overload, long Crew, bool RaisedUnits);

/// <summary>The machinery space: its length, the boiler and engine groups forward to aft, the unit rows across and the
/// unit's size (length, width, height), how far the units stand above the space, and whether a row fits at all.</summary>
public sealed record MachinerySpace(double Length, double Boilers, double Engines, List<string> Order, List<double> Lengths,
    long Rows, (double L, double W, double H) Unit, double Protrusion, double Volume, double WEff, double HEff, double HBoilers,
    bool Fits);

/// <summary>The funnels for the boiler groups: how many over each, their width and length, the gas velocity and area, the
/// gas flow per MW, the uptakes' reach, and how many the gas needed when that was more than MaxFunnels.</summary>
public sealed record FunnelPlan(List<long> Counts, double Width, double Length, double Velocity, double Area, double Gas,
    double Reach, long? Needed)
{
    public long Total => Counts.Sum();
}

/// <summary>The propulsion plant from its technology and the design's choices (research/powerplant-model.md). Shaft
/// power in kW inside this class (the rest uses shp), weights in tonnes, metres.</summary>
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
    public static readonly double[] CURVE_LOADS = [0.10, 0.25, 0.50, 0.75, 1.00];
    static readonly string[] STEAM_CURVES = ["REC", "DT", "GTB"];

    static readonly string[] DRAUGHT_SYSTEMS = ["natural", "forced_boost", "forced", "exhaust"];
    public const double AMBIENT_K = 288.0;
    static readonly Dictionary<string, double> SMOKE_K = new(StringComparer.Ordinal)
        { ["coal_natural"] = 4.0, ["coal"] = 3.0, ["oil"] = 1.5, ["oil_heated"] = 1.0, ["diesel"] = 0.5, ["petrol"] = 0.5 };

    /// <summary>A mature-ish 1940 high-pressure geared turbine plant: the default tech.</summary>
    public static readonly TechInput DefaultTech = new()
    {
        Name = "High-pressure geared turbines (1940)", Fuel = "oil", WeightKgPerKw = 35.2, StressFloor = 0.5, SfcGPerKwh = 349,
        DensityTPerM3 = 0.34, UnitMaxMw = 42.8, Unit = new() { Mw = 30, HeightM = 5.0, WidthM = 5.0, LengthM = 8 },
        BoilerFraction = 0.5, CrewK = 10, PartLoad = "GTB",
        Draught = new() { System = "forced", VelocityMS = 14.0, ReachM = 27.0, GasTempK = 450, AirFuelRatio = 15 },
    };

    /// <summary>The design's tech over the default tech, key by key.</summary>
    static Tech TechOf(TechInput? t, TechInput b)
    {
        T Pick<T>(T? v, T? d) where T : struct => v ?? d ?? throw new InvalidOperationException("the default tech is incomplete");
        string Text(string? v, string? d) => v ?? d ?? throw new InvalidOperationException("the default tech is incomplete");
        UnitInput u = t?.Unit ?? new(), bu = b.Unit!;
        DraughtInput dr = t?.Draught ?? new(), bd = b.Draught!;
        return new Tech(Text(t?.Name, b.Name), Text(t?.Fuel, b.Fuel), Pick(t?.WeightKgPerKw, b.WeightKgPerKw),
            Pick(t?.StressFloor, b.StressFloor), Pick(t?.SfcGPerKwh, b.SfcGPerKwh), Pick(t?.DensityTPerM3, b.DensityTPerM3),
            Pick(t?.UnitMaxMw, b.UnitMaxMw),
            new TechUnit(Pick(u.Mw, bu.Mw), Pick(u.HeightM, bu.HeightM), Pick(u.WidthM, bu.WidthM), Pick(u.LengthM, bu.LengthM)),
            Pick(t?.BoilerFraction, b.BoilerFraction), Pick(t?.CrewK, b.CrewK), Text(t?.PartLoad, b.PartLoad),
            new Draught(Text(dr.System, bd.System), Pick(dr.VelocityMS, bd.VelocityMS), Pick(dr.ReachM, bd.ReachM),
                Pick(dr.GasTempK, bd.GasTempK), Pick(dr.AirFuelRatio, bd.AirFuelRatio), dr.NaturalFraction ?? bd.NaturalFraction));
    }

    /// <summary>The design's plant: its tech over the default tech, and its choices over the defaults.</summary>
    public static PlantSpec Spec(Design design, TechInput? defaultTech = null)
    {
        var m = design.Machinery ?? new MachineryInput();
        var tech = TechOf(m.Tech, defaultTech ?? DefaultTech);
        return new PlantSpec(tech, m.Stress ?? 0.0, m.Shafts, m.UnitsPerShaft ?? 1, m.Transmission ?? "mechanical",
            m.Arrangement ?? new Arrangement("grouped", null), m.CentrelineBulkhead ?? false,
            m.Bunkers ?? (tech.Fuel == "coal" ? "wing" : "ends"), m.WingBunkerM ?? 2.0, m.Rudders ?? 1);
    }

    public static List<string> Validate(Design design, TechInput? defaultTech = null)
    {
        var p = Spec(design, defaultTech);
        var t = p.Tech;
        var errs = new List<string>();
        if (!FUELS.ContainsKey(t.Fuel))
            errs.Add($"machinery.tech.fuel = {Style.Quote(t.Fuel)}: use {string.Join(", ", FUELS.Keys)}");
        if (!CURVES.ContainsKey(t.PartLoad))
            errs.Add($"machinery.tech.part_load = {Style.Quote(t.PartLoad)}: use {string.Join(", ", CURVES.Keys)}");
        var dr = t.Draught;
        if (!DRAUGHT_SYSTEMS.Contains(dr.System))
            errs.Add($"machinery.tech.draught.system = {Style.Quote(dr.System)}: use {string.Join(", ", DRAUGHT_SYSTEMS)}");
        foreach (var (k, v) in new[] { ("weight_kg_per_kw", t.WeightKgPerKw), ("sfc_g_per_kwh", t.SfcGPerKwh),
                     ("density_t_per_m3", t.DensityTPerM3), ("unit_max_mw", t.UnitMaxMw), ("crew_k", t.CrewK) })
            if (!(v > 0))
                errs.Add($"machinery.tech.{k} must be above 0");
        foreach (var (k, v) in new[] { ("mw", t.Unit.Mw), ("height_m", t.Unit.HeightM), ("width_m", t.Unit.WidthM), ("length_m", t.Unit.LengthM) })
            if (!(v > 0))
                errs.Add($"machinery.tech.unit.{k} must be above 0");
        foreach (var (k, v, lo, why) in new[]
                 {
                     ("velocity_m_s", dr.VelocityMS, 0.0, "the funnel gas must move"),
                     ("gas_temp_k", dr.GasTempK, AMBIENT_K, $"funnel gas no hotter than the air ({AMBIENT_K:F0} K) draws no air"),
                     ("reach_m", dr.ReachM, -1e-9, "an uptake can't lead a negative distance"),
                     ("air_fuel_ratio", dr.AirFuelRatio, -1e-9, "the boilers can't burn a negative amount of air"),
                 })
            if (!(v > lo))
                errs.Add($"machinery.tech.draught.{k} = {v}: {why}");
        if (dr.NaturalFraction is double nf && !(0 <= nf && nf <= 1))
            errs.Add("machinery.tech.draught.natural_fraction must be 0..1");
        if (!(0 <= t.BoilerFraction && t.BoilerFraction < 1))
            errs.Add("machinery.tech.boiler_fraction must be 0..1");
        if (!(0 <= p.Stress && p.Stress <= 1))
            errs.Add("machinery.stress must be 0..1");
        if (p.Transmission is not ("mechanical" or "electric"))
            errs.Add($"machinery.transmission = {Style.Quote(p.Transmission)}: use mechanical or electric");
        var a = p.Arrangement;
        if (a.Rooms is { } rooms)
        {
            if (!(1 <= rooms.Count && rooms.Count <= MAX_GROUPS) || rooms.Any(k => k is not ("boiler" or "engine")))
                errs.Add($"machinery.arrangement = {a}: a list of 1..{MAX_GROUPS} \"boiler\" and \"engine\" groups, " +
                         "forward to aft");
        }
        else if (!ARRANGEMENTS.ContainsKey(a.Name!))
            errs.Add($"machinery.arrangement = {Style.Quote(a.Name)}: use {string.Join(", ", ARRANGEMENTS.Keys)} or a list of rooms");
        if (p.Bunkers is not ("wing" or "ends"))
            errs.Add($"machinery.bunkers = {Style.Quote(p.Bunkers)}: use wing or ends");
        if (p.Shafts is int s && !(1 <= s && s <= 8))
            errs.Add("machinery.shafts must be 1..8");
        if (!(1 <= p.Rudders && p.Rudders <= 8))
            errs.Add("machinery.rudders must be 1..8");
        return errs;
    }

    /// <summary>The plant at its rated power: units, weight, fuel rate, crew, continuous and overload power.</summary>
    public static RatedPlant Rated(PlantSpec p, double shp)
    {
        var t = p.Tech;
        double s = p.Stress;
        double kw = Math.Max(shp, 1.0) * KW_PER_SHP;
        double mw = kw / 1000.0;
        long shafts = p.Shafts is int n && n != 0 ? n : Math.Max(1L, Math.Min(4L, (long)Math.Ceiling(mw / t.UnitMaxMw)));
        long perShaft = Math.Max(p.UnitsPerShaft, (long)Math.Ceiling(mw / shafts / t.UnitMaxMw));
        bool elec = p.Transmission == "electric";
        double wSpec = t.WeightKgPerKw * (1 - (1 - t.StressFloor) * s) * (elec ? 1.3 : 1.0);
        double sfc = t.SfcGPerKwh * (1 + 0.08 * s) * (elec ? 1.06 : 1.0);
        return new RatedPlant(kw, shafts, perShaft, shafts * perShaft, mw / (shafts * perShaft), kw * wSpec / 1000.0, sfc,
            t.DensityTPerM3 * (0.85 + 0.15 * s), kw * (1 - 0.15 * s), 1 + 0.15 * (1 - s), (long)Math.Round(t.CrewK * Math.Pow(mw, 0.75)),
            perShaft > p.UnitsPerShaft);
    }

    public static double CurveMult(string name, double f)
    {
        var (pts, over) = CURVES[name];
        if (f >= 1.0)
            return pts[^1] + over * (f - 1.0) / 0.1;
        if (f <= CURVE_LOADS[0])
            return pts[0] * Math.Pow(CURVE_LOADS[0] / Math.Max(f, 0.02), 0.25);
        for (int i = 0; i < pts.Length - 1; i++)
        {
            double f0 = CURVE_LOADS[i], m0 = pts[i], f1 = CURVE_LOADS[i + 1], m1 = pts[i + 1];
            if (f <= f1)
                return m0 + (m1 - m0) * (f - f0) / (f1 - f0);
        }
        return pts[^1];
    }

    /// <summary>Fuel burnt at shp_load, kg per hour.</summary>
    public static double FuelRate(PlantSpec p, double shpRated, double shpLoad)
    {
        var r = Rated(p, shpRated);
        double kw = shpLoad * KW_PER_SHP;
        string curve = p.Tech.PartLoad;
        double f;
        if (STEAM_CURVES.Contains(curve))
            f = kw / r.Kw;
        else
        {
            double unitKw = r.Kw / r.Units;
            long running = Math.Max(1L, Math.Min(r.Units, (long)Math.Ceiling(kw / unitKw - 1e-9)));
            f = kw / (running * unitKw);
        }
        return kw * r.Sfc * CurveMult(curve, f) / 1000.0;
    }

    public static double DoubleBottom(double depth) => Math.Max(DOUBLE_BOTTOM_MIN, DOUBLE_BOTTOM_FRAC * depth);

    /// <summary>The machinery space for an inside width w_avail and a height h_avail.</summary>
    public static MachinerySpace Space(PlantSpec p, double shp, double wAvail, double hAvail)
    {
        var t = p.Tech;
        var r = Rated(p, shp);
        var u = t.Unit;
        double k = Math.Pow(r.UnitMw / u.Mw, 1.0 / 3);
        double hU = u.HeightM * k, wU = u.WidthM * k, lU = u.LengthM * k;
        double hEff = Math.Max(1.0, Math.Max(hU, Math.Min(hAvail, hU + 2.5)));
        double pitch = wU + 0.8;
        bool split = p.CentrelineBulkhead;
        double wSide = split ? wAvail / 2 : wAvail;
        long rows = wSide > 0 ? (long)Math.Floor(wSide / pitch) : 0;
        double used = rows * pitch;
        double wEff = (used + 0.5 * (wSide - used)) * (split ? 2 : 1);
        double volume = r.WeightT / r.Density;
        double bf = t.BoilerFraction;
        double hBoil = Math.Max(1.0, Math.Max(hAvail, hU));
        var order = Groups(p).Order;
        int nB = order.Count(x => x == "boiler"), nE = order.Count(x => x == "engine");
        double groupK = 1 + GROUP_K * Math.Max(0, order.Count - 2);
        double roomMin = lU + ROOM_GANGWAY;
        double boilers = bf * volume / Math.Max(wEff, 0.5) / hBoil * groupK;
        double engines = Math.Max((1 - bf) * volume / Math.Max(wEff, 0.5) / hEff * groupK, roomMin);
        double bEach = nB != 0 ? Math.Max(boilers / nB, nB > 1 ? roomMin : 0.0) : 0.0;
        double eEach = Math.Max(engines / nE, roomMin);
        var lengths = order.Select(x => x == "boiler" ? bEach : eEach).ToList();
        return new MachinerySpace(lengths.Sum(), bEach * nB, eEach * nE, order, lengths, rows, (lU, wU, hU),
            Math.Max(0.0, hU - hAvail), volume, wEff, hEff, hBoil, rows > 0);
    }

    /// <summary>Where the fuel goes: (tonnes in wing bunkers, length of end bunkers or tanks).</summary>
    public static (double Wing, double End) Bunkers(PlantSpec p, double fuelT, double length, double wAvail, double hAvail,
        double shipL, double shipB, double cb, double depth, double draught = 0.0, double tds = 0.0)
    {
        string fuel = p.Tech.Fuel;
        double stow = FUELS[fuel].Stowage;
        double left = fuelT, wing = 0.0, wEnd;
        if (fuel == "coal")
        {
            if (p.Bunkers == "wing")
            {
                wing = Math.Min(left, 2 * p.WingBunkerM * length * (depth - DoubleBottom(depth)) * 0.9 / stow);
                left -= wing;
            }
            wEnd = wAvail + (p.Bunkers == "wing" ? 2 * p.WingBunkerM : 0.0);
        }
        else
        {
            double db = DoubleBottom(depth) * shipL * shipB * cb * 0.6 / stow;
            double layers = 2 * 0.5 * tds * 0.6 * shipL * draught / stow;
            left = Math.Max(0.0, left - db - layers);
            wEnd = wAvail;
        }
        double end = left > 0 ? left * stow / (Math.Max(wEnd, 1.0) * Math.Max(hAvail, 1.0) * 0.9) : 0.0;
        return (wing, end);
    }

    /// <summary>The machinery's groups forward to aft, and warnings for a list that doesn't fit the plant.</summary>
    public static (List<string> Order, List<string> Warns) Groups(PlantSpec p)
    {
        var a = p.Arrangement;
        var order = a.Name is string s ? ARRANGEMENTS[s].ToList() : a.Rooms!.ToList();
        var warns = new List<string>();
        if (!p.IsSteam && order.Contains("boiler"))
        {
            order = order.Where(k => k != "boiler").ToList();
            if (a.Name is null)
                warns.Add("machinery.arrangement lists boiler groups, but the plant has no boilers; they are left out.");
        }
        if (!order.Contains("engine"))
        {
            order.Add("engine");
            warns.Add("machinery.arrangement has no engine group; the engines go aft of the boilers.");
        }
        if (p.IsSteam && !order.Contains("boiler"))
        {
            order.Insert(order.IndexOf("engine"), "boiler");
            warns.Add("machinery.arrangement has no boiler group; the boilers go ahead of the engines.");
        }
        return (order, warns);
    }

    /// <summary>The machinery block, forward to aft: [(kind, length)], kind boiler, engine or bunker.</summary>
    public static List<(string Kind, double Len)> Segments(MachinerySpace sp, double endLen)
    {
        double half = endLen / 2;
        var groups = sp.Order.Zip(sp.Lengths).ToList();
        var out_ = new List<(string, double)>();
        if (half > 0.05)
            out_.Add(("bunker", half));
        if (sp.Boilers <= 0.05)
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
    public static double GasFlow(PlantSpec p)
    {
        var d = p.Tech.Draught;
        double sfc = Rated(p, 1000.0).Sfc;
        return sfc / 3600.0 * (1 + d.AirFuelRatio) / (353.0 / d.GasTempK);
    }

    public static double NaturalVelocity(PlantSpec p, double stackM, double trunkM = 0.0)
    {
        double t = p.Tech.Draught.GasTempK;
        return 0.3 * Math.Sqrt(2 * 9.81 * Math.Max(stackM, 1.0) * (1 - AMBIENT_K / t)) * Math.Max(0.5, 1 - 0.02 * trunkM);
    }

    /// <summary>Funnels for boiler groups of the given lengths: counts per group, width, length, gas velocity, area.</summary>
    public static FunnelPlan PlanFunnels(PlantSpec p, double shp, IReadOnlyList<double> groups, double beam, double stackM, long extra = 0)
    {
        var d = p.Tech.Draught;
        double mw = Rated(p, shp).Kw / 1000.0;
        double q = GasFlow(p);
        string sysname = d.System;
        double vNat = NaturalVelocity(p, stackM);
        double area, v;
        if (sysname == "natural")
            (area, v) = (mw * q / vNat, vNat);
        else if (sysname == "forced_boost")
        {
            area = Math.Max(mw * q / d.VelocityMS, mw * (d.NaturalFraction ?? 0.6) * q / vNat);
            v = d.VelocityMS;
        }
        else
        {
            v = d.VelocityMS;
            area = mw * q / v;
        }
        double wMax = Math.Min(0.22 * beam, 7.0);
        double aMax = 0.785 * wMax * 1.5 * wMax;
        long nArea = (long)Math.Ceiling(area / aMax - 1e-9);
        double reach = d.ReachM;
        var counts = new List<long>();
        foreach (var g in groups)
        {
            double lF = 1.5 * Math.Min(wMax, Math.Sqrt(area / Math.Max(1L, nArea) / (0.785 * 1.5)));
            counts.Add(Math.Max(1L, (long)Math.Ceiling(g / (2 * reach + lF) - 1e-9)));
        }
        if (counts.Count == 0)
            counts = [1];
        long want = Math.Max(nArea, counts.Sum()) + extra;
        long needed = want;
        if (want > MAX_FUNNELS)
        {
            want = Math.Max(MAX_FUNNELS, counts.Count);
            while (counts.Sum() > want)
            {
                long mx = counts.Max();
                counts[counts.IndexOf(mx)] -= 1;
            }
        }
        while (counts.Sum() < want)
        {
            int j = Enumerable.Range(0, counts.Count).MaxBy(k => (groups.Count > 0 ? groups[k] : 1) / (double)counts[k]);
            counts[j] += 1;
        }
        long n = counts.Sum();
        double w = Math.Min(wMax, Math.Sqrt(CASING * area / n / (0.785 * 1.5)));
        w = Math.Max(w, sysname != "exhaust" ? 2.2 : 1.0);
        if (needed > n)
            v *= Math.Max(1.0, area / (n * 0.785 * w * 1.5 * w / CASING));
        return new FunnelPlan(counts, w, 1.5 * w, v, area, q, reach, needed > n ? needed : null);
    }

    /// <summary>How far aft of a funnel its smoke blinds a control position.</summary>
    public static double SmokeReach(PlantSpec p, double shp)
    {
        var t = p.Tech;
        var d = t.Draught;
        double k = t.Fuel switch
        {
            "coal" => SMOKE_K[d.System == "natural" ? "coal_natural" : "coal"],
            "oil" => SMOKE_K[d.GasTempK <= 480 ? "oil_heated" : "oil"],
            var f => SMOKE_K[f],
        };
        return k * Math.Sqrt(Rated(p, shp).Kw / 1000.0 * GasFlow(p));
    }

    /// <summary>(funnel, uptake) tonnes.</summary>
    public static (double Funnel, double Uptake) FunnelWeight(double w, double l, double height, double uptakeVertical,
        double uptakeHorizontal)
    {
        double per = Math.PI * w + 2 * Math.Max(0.0, l - w);
        return (0.12 * per * height, 0.18 * per * (uptakeVertical + 1.5 * uptakeHorizontal));
    }

    /// <summary>The plant's static numbers for the game (report "plant").</summary>
    public static PyDict Published(PlantSpec p, double shp, PyDict? extra = null)
    {
        var t = p.Tech;
        var r = Rated(p, shp);
        var o = PyDict.Of(("name", t.Name), ("fuel", t.Fuel), ("rated_kw", (long)Math.Round(r.Kw)),
            ("rated_shp", Math.Round(shp / 10.0) * 10), ("continuous_kw", (long)Math.Round(r.ContinuousKw)),
            ("overload_max", Math.Round(r.Overload, 3)), ("shafts", r.Shafts), ("units", r.Units),
            ("unit_mw", Math.Round(r.UnitMw, 2)), ("weight_t", Math.Round(r.WeightT, 1)),
            ("sfc_g_per_kwh", Math.Round(r.Sfc, 1)),
            ("part_load", PyDict.Of(("curve", t.PartLoad), ("loads", CURVE_LOADS.Cast<object?>().ToList()),
                ("multipliers", CURVES[t.PartLoad].Pts.Cast<object?>().ToList()), ("overload_per_tenth", CURVES[t.PartLoad].Over))),
            ("draught", t.Draught.System), ("stress", p.Stress), ("transmission", p.Transmission),
            ("arrangement", p.Arrangement.Name ?? (object)p.Arrangement.Rooms!.Cast<object?>().ToList()), ("crew", r.Crew));
        if (t.Draught.System == "forced_boost")
            o["natural_fraction"] = t.Draught.NaturalFraction ?? 0.6;
        if (extra != null)
            o.Update(extra);
        return o;
    }
}
