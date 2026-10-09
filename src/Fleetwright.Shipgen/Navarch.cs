namespace Fleetwright.Shipgen;

/// <summary>navarch: "naval architecture lite" weight, power and stability model. The player never enters tonnage:
/// displacement is whatever the ship needs to carry its hull, machinery, fuel, armour and armament, so solve() iterates
/// to a fixed point.</summary>
public static class Navarch
{
    const double OVERLOAD_TB = 3.0;

    public static PyDict Tuning() => PyDict.Of(("hull_k", 0.112), ("hull_exp", 1.0), ("freeboard_a", 0.018),
        ("freeboard_b", 1.5), ("admiralty_a", 111.0), ("admiralty_b", 0.69), ("cruise_kn", 15.0), ("misc_frac", 0.055),
        ("superstructure_t_per_m2", 0.32), ("lcb_frac", -0.012), ("planing_rw", 0.13), ("planing_rw_disp", 0.06),
        ("planing_eta", 0.5));

    public const double SUPERSTRUCTURE_T_PER_M2 = 0.32;

    /// <summary>The solved ship.</summary>
    public sealed class Result
    {
        public double Std, Full, Draught, Depth, Freeboard, PowerShp, Fuel, CruiseKn;
        public PyDict Plant = new(), PlantRated = new(), Hull = new(), Armour = new();
        public double GmFull, GmLight, RollS;
        public PyDict Wind = new();
        public double Lcg, Lcb, TrimM;
        public List<Weight> Weights = [];
        public OrderedDictionary<string, double> Groups = new(StringComparer.Ordinal);
        public List<string> Warnings = [], Errors = [];
    }

    public static double Froude(double vKn, double L) => vKn * 0.5144 / Math.Sqrt(9.81 * L);

    public static double AdmiraltyC(double vKn, double L, double B, double cb, PyDict tun)
    {
        double fn = Math.Max(Froude(vKn, L), 0.08);
        double c = tun.F("admiralty_a") * Math.Pow(fn, -tun.F("admiralty_b"));
        c *= Math.Pow(L / B / 8.0, 0.25);
        c *= Math.Pow(0.55 / cb, 0.5);
        return c;
    }

    public static double PowerRequired(double disp, double vKn, double L, double B, double cb, PyDict tun)
    {
        if (Py.Eq(tun.Get("power_model"), "planing"))
            return PlaningPower(disp, vKn, tun);
        return Math.Pow(disp, 2.0 / 3) * Math.Pow(vKn, 3) / AdmiraltyC(vKn, L, B, cb, tun);
    }

    /// <summary>Fn∇ = V / sqrt(g ∇^(1/3)).</summary>
    public static double VolumetricFroude(double disp, double vKn) =>
        vKn * 0.5144 / Math.Sqrt(9.81 * Math.Pow(disp / Weight.SEAWATER, 1.0 / 3));

    /// <summary>PLACEHOLDER planing-hull power model.</summary>
    public static double PlaningPower(double disp, double vKn, PyDict tun)
    {
        double fnv = VolumetricFroude(disp, vKn);
        double hi = tun.F("planing_rw"), lo = tun.F("planing_rw_disp");
        double rw = fnv < 1 ? lo * Math.Pow(fnv, 2) : lo + (hi - lo) * Math.Min(1.0, fnv - 1);
        double rKn = rw * disp * 9.81;
        return rKn * vKn * 0.5144 / tun.F("planing_eta") / 0.7457;
    }

    /// <summary>The style's standard freeboard at full load for an ocean-going hull; hull.freeboard scales it.</summary>
    public static double DesignFreeboard(double L, PyDict tun) => tun.F("freeboard_a") * L + tun.F("freeboard_b");

    /// <summary>Weights from the payload hook: the ship so far, the fuel and where it stands, and the lcb.</summary>
    public sealed record PayloadContext(List<Weight> Items, double Fuel, double FuelX, double Lcb);

    /// <summary>design: player input; placed: weights with x positions from the layout (null: a rough estimate, before
    /// anything is laid out); geo: layout facts (null before a layout exists).</summary>
    public static Result Solve(PyDict design, List<Weight>? placed = null, Geo? geo = null)
    {
        var style = Styles.Get(design);
        var tun = PyDict.Merge(Tuning(), style.Tuning(design));
        var plant = Powerplant.Spec(design, style.DefaultTech);
        var hullIn = design.D("hull");
        double L = hullIn.F("length"), B = hullIn.F("beam"), cb = hullIn.F("block_coefficient");
        double V = design.F("speed_kn");
        double rng = design.F("range_nm", 6000);
        geo ??= new Geo();
        var res = new Result();

        double disp = 200.0 * L * B * cb * 0.04;
        var own = placed?.Select(w => w.Clone()).ToList();
        double T = 0, D = 0, shp = 0, std = 0, full = 0, fuel = 0, vc = 0;
        PyDict hull = new();
        List<Weight> items = [], stdLoad = [], fullLoad = [];
        bool settled = false;
        for (int it = 0; it < 60; it++)
        {
            T = disp / (Weight.SEAWATER * L * B * cb);
            D = T + DesignFreeboard(L, tun) * hullIn.F("freeboard", 1.0);
            items = [];
            var arm = Armour.ArmourGeometry(design, L, T, D, geo);
            if (Py.Eq(tun.Get("hull_model"), "box"))
                hull = HullWeight.BoxStructure(design, L, B, D, tun);
            else
                hull = HullWeight.HullStructure(design, L, B, cb, D, disp, arm, style.StrengthDeck(design, D), geo.Raised);
            double zFrac = tun.F("hull_z_frac", 0.58) * (hull.F("depth_m", D) / D);
            items.Add(new Weight("Hull structure", "hull", hull.F("t"), -0.01 * L, ZRel.Frac(zFrac)));
            shp = PowerRequired(disp, V, L, B, cb, tun);
            items.Add(new Weight("Machinery", "machinery", Powerplant.Rated(plant, shp).F("weight_t"), geo.MachineryMid(L),
                ZRel.Frac(0.32)));
            if (own is null)
            {
                items.AddRange(RoughPayload(design, D));
                items.AddRange(style.RoughPayload(design, D));
            }
            else
                items.AddRange(own);
            items.AddRange(Armour.ArmourWeights(design, L, B, D, arm));
            items.AddRange(style.StructureWeights(design, L, B, T, D, geo, tun));
            double wood = HullWeight.Plating(design).F("deck_wood_mm");
            if (wood != 0)
            {
                var (area, wx, wz) = style.WeatherDeck(design, L, B);
                items.Add(new Weight("Deck planking", "hull", area * wood * HullWeight.RHO_WOOD, wx, ZRel.Deck(wz)));
            }
            double stdWoMisc = items.Select(w => w.W).Sum();
            std = stdWoMisc / (1 - tun.F("misc_frac"));
            items.Add(new Weight("Equipment, outfit, crew & stores", "misc", std - stdWoMisc, 0.0, ZRel.Frac(0.5)));
            vc = Py.Truthy(tun.Get("cruise_at_service")) ? V : Math.Min(tun.F("cruise_kn"), 0.6 * V);
            double shpC = PowerRequired(disp, vc, L, B, cb, tun);
            fuel = Powerplant.FuelRate(plant, shp, shpC) * (rng / vc) / 1000.0;
            (stdLoad, fullLoad) = style.PayloadWeights(design, L, D, geo, tun,
                new PayloadContext(items, fuel, geo.MachineryMid(L), tun.F("lcb_frac") * L));
            std += stdLoad.Select(w => w.W).Sum();
            full = std + fuel + fullLoad.Select(w => w.W).Sum();
            if (Math.Abs(full - disp) < 0.5)
            {
                disp = full;
                settled = true;
                break;
            }
            if (!double.IsFinite(full) || full > disp && full / (Weight.SEAWATER * L * B * cb) > OVERLOAD_TB * B)
            {
                res.Errors.Add($"The weights never settle on a {B:F1} m beam: the ship sinks deeper with every " +
                               "tonne it carries. A wider hull, or less armour or armament, would help.");
                settled = true;
                break;
            }
            disp = 0.5 * disp + 0.5 * full;
        }
        if (!settled)
            res.Errors.Add("The weights never settle: the ship needs a bigger hull to carry its load, which " +
                           $"needs a bigger hull again (still growing at {full:N0} t). Lighten the armour or " +
                           "armament.");

        items.AddRange(stdLoad);
        items.Add(new Weight("Fuel", "fuel", fuel, geo.MachineryMid(L), ZRel.Frac(0.18)));
        items.AddRange(fullLoad);
        foreach (var w in items)
            w.Z = w.ZRel.Kind == "frac" ? w.ZRel.V * D : D + w.ZRel.V;

        res.Std = std;
        res.Full = full;
        res.Fuel = fuel;
        res.Draught = full / (Weight.SEAWATER * L * B * cb);
        res.Depth = D;
        res.Freeboard = D - full / (Weight.SEAWATER * L * B * cb);
        res.PowerShp = shp;
        res.CruiseKn = vc;
        res.Plant = plant;
        res.PlantRated = Powerplant.Rated(plant, shp);
        res.Hull = hull;
        res.Armour = Armour.ArmourGeometry(design, L, res.Draught, res.Depth, geo);
        res.Weights = items;
        foreach (var w in items)
            res.Groups[w.Group] = (res.Groups.TryGetValue(w.Group, out var g) ? g : 0.0) + w.W;

        Stability.Evaluate(res, L, B, cb, tun, geo.Windage);

        double TB = res.Draught / B;
        double tbMax = tun.F("tb_max", 0.48);
        if (TB > tbMax)
            res.Errors.Add($"Hull overloaded: draught {res.Draught:F1} m is {TB:F2} x beam (max " +
                           $"{Py.Str(tun.Get("tb_max", 0.48))}). Carry less.");
        else if (TB > tbMax - 0.08)
            res.Warnings.Add($"Deep draught ({res.Draught:F1} m, {TB:F2} x beam): the hull is heavily loaded.");
        if (L / B < tun.F("lb_warn", 4.5))
            res.Warnings.Add($"Very beamy hull (L/B {L / B:F1}): hard to drive, needs a lot of power.");
        if (L / B > 12)
            res.Warnings.Add($"Very slender hull (L/B {L / B:F1}): weak structure and poor stability.");
        Stability.Checks(res, L, B, tun);
        double fn = Froude(V, L);
        res.Warnings.AddRange(style.Checks(design, res, tun));
        res.Warnings.AddRange(Armour.ArmourChecks(design, res, geo));
        res.Warnings.AddRange(HullWeight.StructureChecks(res.Hull));
        if (fn > tun.F("fn_warn", 0.62))
            res.Warnings.Add($"Speed {Py.Str(design["speed_kn"])} kn is extreme for a {L:F0} m hull (Froude {fn:F2}); " +
                             "power is enormous.");
        return res;
    }

    /// <summary>First-pass armament and superstructure estimate before the layout exists (all at x=0).</summary>
    public static List<Weight> RoughPayload(PyDict design, double D)
    {
        var out_ = Batteries.RoughArmament(design, D);
        out_.Add(new Weight("Superstructure", "superstructure", 0.012 * Math.Pow(design.D("hull").F("length"), 2),
            zRel: ZRel.Deck(4)));
        return out_;
    }
}
