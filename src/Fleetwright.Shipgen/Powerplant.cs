namespace Fleetwright.Shipgen;

/// <summary>A plant technology with nothing left out (TechInput over the style's default).</summary>
public sealed record Tech(string Name, string Fuel, double WeightKgPerKw, double StressFloor, double SfcGPerKwh,
    double DensityTPerM3, double UnitMaxMw, TechUnit Unit, double BoilerFraction, double CrewK, string PartLoad, Draught Draught,
    string Boilers, string Engines);

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
    public const double KwPerShp = 0.7457;

    /// <summary>The design rating a design gets when it gives none: the middle, a cruiser's (research: capital ships
    /// 0-0.2, cruisers 0.5, destroyers 0.8-1). Not 0: a new design that picks an older plant would get the most
    /// conservative one, and with it a destroyer twice its size.</summary>
    public const double DefaultStress = 0.5;
    const double CasingK = 3.0;
    const int MaxFunnels = 60;
    static readonly Dictionary<string, string[]> Arrangements = new(StringComparer.Ordinal)
    {
        ["grouped"] = ["boiler", "engine"],
        ["unit"] = ["boiler", "engine", "boiler", "engine"],
    };
    const int MaxGroups = 8;
    const double GroupK = 0.05;
    const double RoomGangway = 2.0;
    public const double SteelFrame = 0.92;

    public static readonly IReadOnlyDictionary<string, (double Lhv, double Stowage)> Fuels =
        new Dictionary<string, (double, double)>(StringComparer.Ordinal)
        {
            ["coal"] = (30.0, 1.30), ["oil"] = (41.0, 1.07), ["diesel"] = (42.8, 1.19), ["petrol"] = (44.0, 1.37),
        };

    public static readonly IReadOnlyDictionary<string, (double[] Pts, double Over)> Curves =
        new Dictionary<string, (double[], double)>(StringComparer.Ordinal)
        {
            ["REC"] = ([1.45, 1.20, 1.07, 1.02, 1.00], 0.5),
            ["DT"] = ([2.60, 1.90, 1.40, 1.12, 1.00], 0.3),
            ["GTB"] = ([1.90, 1.45, 1.18, 1.05, 1.00], 0.3),
            ["DSL"] = ([1.25, 1.10, 1.03, 1.00, 1.01], 0.4),
            ["GTS"] = ([2.80, 1.85, 1.35, 1.12, 1.00], 0.2),
            ["ICR"] = ([1.35, 1.12, 1.03, 1.00, 1.00], 0.2),
        };
    public static readonly double[] CurveLoads = [0.10, 0.25, 0.50, 0.75, 1.00];
    static readonly string[] SteamCurves = ["REC", "DT", "GTB"];

    static readonly string[] DraughtSystems = ["natural", "forced_boost", "forced", "exhaust"];
    public const double AmbientK = 288.0;
    static readonly Dictionary<string, double> SmokeK = new(StringComparer.Ordinal)
        { ["coal_natural"] = 4.0, ["coal"] = 3.0, ["oil"] = 1.5, ["oil_heated"] = 1.0, ["diesel"] = 0.5, ["petrol"] = 0.5 };

    /// <summary>The smoke a fuel makes well fired (docs/battle/smoke.md Z0): its particles, kg a kg of fuel, and their mass
    /// extinction, m²/g [EST: hand-fired bituminous coal's particulate ~1% (EPA AP-42), most of it soot and ash; heavy
    /// fuel oil well fired ~0.15%, nearly all soot; diesel's and petrol's less. Forcing, stoking and burning rich come
    /// later; tuned when the smoke system is whole].</summary>
    static readonly Dictionary<string, (double Yield, double M2PerG)> Smoke = new(StringComparer.Ordinal)
    {
        ["coal"] = (0.010, 5.0), ["oil"] = (0.0015, 7.0), ["diesel"] = (0.002, 8.0), ["petrol"] = (0.0005, 8.0),
    };

    /// <summary>The power shares the plant report gives the fuel burnt at (docs/battle/smoke.md Z0), of the rated power;
    /// the overload's top is added after the last.</summary>
    public static readonly double[] FuelShares = [0.05, 0.1, 0.25, 0.5, 0.75, 1.0];

    /// <summary>A mature-ish 1940 high-pressure geared turbine plant: the default tech.</summary>
    public static readonly TechInput DefaultTech = new()
    {
        Name = "High-pressure geared turbines (1940)", Fuel = "oil", WeightKgPerKw = 35.2, StressFloor = 0.5, SfcGPerKwh = 349,
        DensityTPerM3 = 0.34, UnitMaxMw = 42.8, Unit = new() { Mw = 30, HeightM = 5.0, WidthM = 5.0, LengthM = 8 },
        BoilerFraction = 0.5, CrewK = 10, PartLoad = "GTB", Boilers = "small_tube", Engines = "geared_turbine",
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
                Pick(dr.GasTempK, bd.GasTempK), Pick(dr.AirFuelRatio, bd.AirFuelRatio), dr.NaturalFraction ?? bd.NaturalFraction),
            Text(t?.Boilers, b.Boilers), Text(t?.Engines, b.Engines));
    }

    /// <summary>The boilers' kinds (docs/battle/shell-contents.md C1b; damage/09 §2.1): none (a plant without boilers),
    /// cylindrical (Scotch, return-tube), locomotive, large-tube water-tube (Belleville, Niclausse, Babcock), small-tube
    /// water-tube (Yarrow, Thornycroft, Normand, the express boilers).</summary>
    public static readonly string[] BoilerKinds = ["none", "cylindrical", "locomotive", "large_tube", "small_tube"];

    /// <summary>The engines' kinds: compound, triple or quadruple expansion (vte), direct-drive turbines, geared turbines,
    /// diesels, petrol engines, gas turbines.</summary>
    public static readonly string[] EngineKinds = ["compound", "vte", "direct_turbine", "geared_turbine", "diesel", "petrol", "gas_turbine"];

    /// <summary>The kinds a plant's name says, for a block that doesn't give them (DesignFormat 9 → 10 reads the name of
    /// the template a block was picked from); then its part-load curve and whether it has boilers. Null for a part the
    /// name doesn't tell and nothing else does.</summary>
    public static (string Boilers, string Engines, bool Guessed) KindsOf(string? name, string? partLoad, double? boilerFraction)
    {
        string n = (name ?? "").ToLowerInvariant();
        string? boilers = boilerFraction is 0 ? "none"
            : n.Contains("scotch") || n.Contains("cylindrical") ? "cylindrical"
            : n.Contains("locomotive") ? "locomotive"
            : n.Contains("large-tube") ? "large_tube"
            : n.Contains("small-tube") || n.Contains("express") || n.Contains("water-tube") || n.Contains("boilers") ? "small_tube"
            : null;
        string? engines = n.Contains("compound") ? "compound"
            : n.Contains("expansion") ? "vte"
            : n.Contains("direct-drive") ? "direct_turbine"
            : n.Contains("geared") ? "geared_turbine"
            : n.Contains("gas turbine") ? "gas_turbine"
            : n.Contains("diesel") ? "diesel"
            : n.Contains("petrol") ? "petrol"
            : null;
        bool guessed = engines is null || boilers is null && engines is not ("direct_turbine" or "geared_turbine");
        engines ??= partLoad switch { "REC" => "vte", "DT" => "direct_turbine", "GTB" => "geared_turbine", "GTS" => "gas_turbine", _ => "diesel" };
        boilers ??= engines is "diesel" or "petrol" or "gas_turbine" ? "none" : "small_tube";
        return (boilers, engines, guessed);
    }

    /// <summary>The design's plant: its tech over the default tech, and its choices over the defaults.</summary>
    public static PlantSpec Spec(Design design, TechInput? defaultTech = null)
    {
        var m = design.Machinery ?? new MachineryInput();
        var tech = TechOf(m.Tech, defaultTech ?? DefaultTech);
        return new PlantSpec(tech, m.Stress ?? DefaultStress, m.Shafts, m.UnitsPerShaft ?? 1, m.Transmission ?? "mechanical",
            m.Arrangement ?? new Arrangement("grouped", null), m.CentrelineBulkhead ?? false,
            m.Bunkers ?? (tech.Fuel == "coal" ? "wing" : "ends"), m.WingBunkerM ?? 2.0, m.Rudders ?? 1);
    }

    public static List<string> Validate(Design design, TechInput? defaultTech = null)
    {
        var p = Spec(design, defaultTech);
        var t = p.Tech;
        var errs = new List<string>();
        if (!Fuels.ContainsKey(t.Fuel))
            errs.Add($"machinery.tech.fuel = {Style.Quote(t.Fuel)}: use {string.Join(", ", Fuels.Keys)}");
        if (!Curves.ContainsKey(t.PartLoad))
            errs.Add($"machinery.tech.part_load = {Style.Quote(t.PartLoad)}: use {string.Join(", ", Curves.Keys)}");
        if (!BoilerKinds.Contains(t.Boilers))
            errs.Add($"machinery.tech.boilers = {Style.Quote(t.Boilers)}: use {string.Join(", ", BoilerKinds)}");
        if (!EngineKinds.Contains(t.Engines))
            errs.Add($"machinery.tech.engines = {Style.Quote(t.Engines)}: use {string.Join(", ", EngineKinds)}");
        var dr = t.Draught;
        if (!DraughtSystems.Contains(dr.System))
            errs.Add($"machinery.tech.draught.system = {Style.Quote(dr.System)}: use {string.Join(", ", DraughtSystems)}");
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
                     ("gas_temp_k", dr.GasTempK, AmbientK, $"funnel gas no hotter than the air ({AmbientK:F0} K) draws no air"),
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
            if (!(1 <= rooms.Count && rooms.Count <= MaxGroups) || rooms.Any(k => k is not ("boiler" or "engine")))
                errs.Add($"machinery.arrangement = {a}: a list of 1..{MaxGroups} \"boiler\" and \"engine\" groups, " +
                         "forward to aft");
        }
        else if (!Arrangements.ContainsKey(a.Name!))
            errs.Add($"machinery.arrangement = {Style.Quote(a.Name)}: use {string.Join(", ", Arrangements.Keys)} or a list of rooms");
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
        double kw = Math.Max(shp, 1.0) * KwPerShp;
        double mw = kw / 1000.0;
        long shafts = p.Shafts is int n && n != 0 ? n : Math.Max(1L, Math.Min(4L, (long)Math.Ceiling(mw / t.UnitMaxMw)));
        long perShaft = Math.Max(p.UnitsPerShaft, (long)Math.Ceiling(mw / shafts / t.UnitMaxMw));
        bool elec = p.Transmission == "electric";
        double stressMult = 1 - (1 - t.StressFloor) * s;
        double wSpec = t.WeightKgPerKw * stressMult * (elec ? 1.3 : 1.0);
        double sfc = t.SfcGPerKwh * (1 + 0.08 * s) * (elec ? 1.06 : 1.0);
        return new RatedPlant(kw, shafts, perShaft, shafts * perShaft, mw / (shafts * perShaft), kw * wSpec / 1000.0, sfc,
            t.DensityTPerM3 * (0.85 + 0.15 * s), kw * (1 - 0.15 * s), 1 + 0.15 * (1 - s), (long)Math.Round(t.CrewK * Math.Pow(mw * stressMult, 0.75)),
            perShaft > p.UnitsPerShaft);
    }

    public static double CurveMult(string name, double f)
    {
        var (pts, over) = Curves[name];
        if (f >= 1.0)
            return pts[^1] + over * (f - 1.0) / 0.1;
        if (f <= CurveLoads[0])
            return pts[0] * Math.Pow(CurveLoads[0] / Math.Max(f, 0.02), 0.25);
        for (int i = 0; i < pts.Length - 1; i++)
        {
            double f0 = CurveLoads[i], m0 = pts[i], f1 = CurveLoads[i + 1], m1 = pts[i + 1];
            if (f <= f1)
                return m0 + (m1 - m0) * (f - f0) / (f1 - f0);
        }
        return pts[^1];
    }

    /// <summary>Fuel burnt at shp_load, kg per hour.</summary>
    public static double FuelRate(PlantSpec p, double shpRated, double shpLoad)
    {
        var r = Rated(p, shpRated);
        double kw = shpLoad * KwPerShp;
        string curve = p.Tech.PartLoad;
        double f;
        if (SteamCurves.Contains(curve))
            f = kw / r.Kw;
        else
        {
            double unitKw = r.Kw / r.Units;
            long running = Math.Max(1L, Math.Min(r.Units, (long)Math.Ceiling(kw / unitKw - 1e-9)));
            f = kw / (running * unitKw);
        }
        return kw * r.Sfc * CurveMult(curve, f) / 1000.0;
    }

    /// <summary>The machinery space for an inside width w_avail and a height h_avail.</summary>
    public static MachinerySpace Space(PlantSpec p, double shp, double wAvail, double hAvail)
    {
        var t = p.Tech;
        var r = Rated(p, shp);
        var u = t.Unit;
        // the units' size for the space: as big as the power per shaft asks, up to the largest the technology builds, and
        // past that more of them, counted as a fraction (the plant's whole units halve in size when a shaft takes a
        // second one, and its rooms shouldn't jump with them)
        double mwSpace = r.Kw / 1000.0 / (r.Shafts * Math.Max(p.UnitsPerShaft, r.Kw / 1000.0 / r.Shafts / t.UnitMaxMw));
        double k = Math.Pow(mwSpace / u.Mw, 1.0 / 3);
        double hU = u.HeightM * k, wU = u.WidthM * k, lU = u.LengthM * k;
        double hEff = Math.Max(1.0, Math.Max(hU, Math.Min(hAvail, hU + 2.5)));
        double pitch = wU + 0.8;
        bool split = p.CentrelineBulkhead;
        double wSide = split ? wAvail / 2 : wAvail;
        long rows = wSide > 0 ? (long)Math.Floor(wSide / pitch) : 0;
        // the width the plant fills: whole rows of units and half what's left beside them, taken at its mean (a quarter
        // of a pitch short of the space), so the machinery doesn't lengthen by a room when the beam crosses a row
        double wEff = Math.Max(0.0, wSide - 0.25 * pitch) * (split ? 2 : 1);
        double volume = r.WeightT / r.Density;
        double bf = t.BoilerFraction;
        double hBoil = Math.Max(1.0, Math.Max(hAvail, hU));
        var order = Groups(p).Order;
        int nB = order.Count(x => x == "boiler"), nE = order.Count(x => x == "engine");
        double groupK = 1 + GroupK * Math.Max(0, order.Count - 2);
        double roomMin = lU + RoomGangway;
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
        double stow = Fuels[fuel].Stowage;
        double left = fuelT, wing = 0.0, wEnd;
        if (fuel == "coal")
        {
            if (p.Bunkers == "wing")
            {
                wing = Math.Min(left, 2 * p.WingBunkerM * length * (depth - Geometry.DoubleBottom(depth)) * 0.9 / stow);
                left -= wing;
            }
            wEnd = wAvail + (p.Bunkers == "wing" ? 2 * p.WingBunkerM : 0.0);
        }
        else
        {
            double db = Geometry.DoubleBottom(depth) * shipL * shipB * cb * 0.6 / stow;
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
        var order = a.Name is string s ? Arrangements[s].ToList() : a.Rooms!.ToList();
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
        var result = new List<(string, double)>();
        if (half > 0.05)
            result.Add(("bunker", half));
        if (sp.Boilers <= 0.05)
        {
            result.AddRange(groups.Where(g => g.First == "engine"));
            if (half > 0.05)
                result.Add(("bunker", half));
            return result;
        }
        int mid = groups.Count / 2;
        result.AddRange(groups.Take(mid));
        if (half > 0.05)
            result.Add(("bunker", half));
        result.AddRange(groups.Skip(mid));
        return result;
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
        return 0.3 * Math.Sqrt(2 * 9.81 * Math.Max(stackM, 1.0) * (1 - AmbientK / t)) * Math.Max(0.5, 1 - 0.02 * trunkM);
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
        if (want > MaxFunnels)
        {
            want = Math.Max(MaxFunnels, counts.Count);
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
        double w = Math.Min(wMax, Math.Sqrt(CasingK * area / n / (0.785 * 1.5)));
        w = Math.Max(w, sysname != "exhaust" ? 2.2 : 1.0);
        if (needed > n)
            v *= Math.Max(1.0, area / (n * 0.785 * w * 1.5 * w / CasingK));
        return new FunnelPlan(counts, w, 1.5 * w, v, area, q, reach, needed > n ? needed : null);
    }

    /// <summary>The extinction area of the smoke burning a kg of a fuel makes, m² (docs/battle/smoke.md Z0).</summary>
    public static double SmokeM2PerKg(string fuel) => Smoke.TryGetValue(fuel, out var s) ? s.Yield * s.M2PerG * 1000 : 0;

    /// <summary>The funnel gas a kg of fuel makes, m³ at the gas's temperature: the fuel and its air at
    /// 353 / T kg/m³ (<see cref="GasFlow"/>'s, a kg at a time).</summary>
    public static double GasM3PerKg(PlantSpec p) => (1 + p.Tech.Draught.AirFuelRatio) / (353.0 / p.Tech.Draught.GasTempK);

    /// <summary>How far aft of a funnel its smoke blinds a control position.</summary>
    public static double SmokeReach(PlantSpec p, double shp)
    {
        var t = p.Tech;
        var d = t.Draught;
        double k = t.Fuel switch
        {
            "coal" => SmokeK[d.System == "natural" ? "coal_natural" : "coal"],
            "oil" => SmokeK[d.GasTempK <= 480 ? "oil_heated" : "oil"],
            var f => SmokeK[f],
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
    public static PlantReport Published(PlantSpec p, double shp)
    {
        var t = p.Tech;
        var r = Rated(p, shp);
        return new PlantReport
        {
            Name = t.Name, Fuel = t.Fuel, RatedKw = (long)Math.Round(r.Kw), RatedShp = Math.Round(shp / 10.0) * 10,
            ContinuousKw = (long)Math.Round(r.ContinuousKw), OverloadMax = Math.Round(r.Overload, 3), Shafts = r.Shafts, Units = r.Units,
            UnitMw = Math.Round(r.UnitMw, 2), WeightT = Math.Round(r.WeightT, 1), SfcGPerKwh = Math.Round(r.Sfc, 1),
            PartLoad = new PartLoad(t.PartLoad, CurveLoads, Curves[t.PartLoad].Pts, Curves[t.PartLoad].Over),
            Draught = t.Draught.System, NaturalFraction = t.Draught.System == "forced_boost" ? t.Draught.NaturalFraction ?? 0.6 : null,
            Stress = p.Stress, Transmission = p.Transmission, Arrangement = p.Arrangement, Crew = r.Crew, Boilers = t.Boilers, Engines = t.Engines,
            GasTempK = t.Draught.GasTempK, GasM3PerKg = Math.Round(GasM3PerKg(p), 3), SmokeM2PerKg = Math.Round(SmokeM2PerKg(t.Fuel), 3),
            FuelShares = [.. FuelShares, Math.Round(r.Overload, 3)],
            FuelKgS = [.. FuelShares.Append(r.Overload).Select(f => Math.Round(FuelRate(p, shp, shp * f) / 3600, 4))],
        };
    }
}
