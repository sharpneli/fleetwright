namespace Fleetwright.Shipgen;

/// <summary>Navarch's tuning: the constants of its weight, power and stability fits, which a style may change.</summary>
public sealed record Tuning
{
    public double HullK { get; init; } = 0.112;
    public double HullExp { get; init; } = 1.0;
    public double FreeboardA { get; init; } = 0.018;
    public double FreeboardB { get; init; } = 1.5;
    public double AdmiraltyA { get; init; } = 111.0;
    public double AdmiraltyB { get; init; } = 0.69;
    public double CruiseKn { get; init; } = 15.0;
    /// <summary>Cruise at the service speed (merchants) instead of CruiseKn.</summary>
    public bool CruiseAtService { get; init; }
    public double MiscFrac { get; init; } = 0.055;
    public double LcbFrac { get; init; } = -0.012;
    /// <summary>The planing-hull power model (Navarch.PlaningPower) instead of the admiralty coefficient.</summary>
    public bool PlaningPower { get; init; }
    public double PlaningRw { get; init; } = 0.13;
    public double PlaningRwDisp { get; init; } = 0.06;
    public double PlaningEta { get; init; } = 0.5;
    /// <summary>A box-model hull (HullWeight.BoxStructure) instead of the plate model, its own plate this thick.</summary>
    public bool BoxHull { get; init; }
    public double PlateOwnMm { get; init; }
    public double HullZFrac { get; init; } = 0.58;
    public double TbMax { get; init; } = 0.48;
    public double LbWarn { get; init; } = 4.5;
    public double FnWarn { get; init; } = 0.62;
    public double GmStiffFrac { get; init; } = 0.15;
    public double TrimTolFrac { get; init; } = 0.01;
    public double TrimWarnFrac { get; init; } = 0.004;
    public double FlightDeckTPerM2 { get; init; }
    public double HangarTPerM2 { get; init; }
}

/// <summary>"Naval architecture lite": the weight, power and stability model. The player never enters tonnage:
/// displacement is whatever the ship needs to carry its hull, machinery, fuel, armour and armament, so Solve iterates
/// to a fixed point.</summary>
public static class Navarch
{
    const double OverloadTb = 3.0;

    public const double SuperstructureTPerM2 = 0.32;

    /// <summary>The solved ship.</summary>
    public sealed class Result
    {
        public double Std, Full, Draught, Depth, Freeboard, PowerShp, Fuel, CruiseKn;
        public PlantSpec Plant = null!;
        public RatedPlant PlantRated = null!;
        public HullStructure Hull = null!;
        public ArmourLayout Armour = null!;
        public double GmFull, GmLight, RollS;
        public WindHeel? Wind;
        public double Lcg, Lcb, TrimM;
        public List<Weight> Weights = [];
        public OrderedDictionary<string, double> Groups = new(StringComparer.Ordinal);
        public List<string> Warnings = [], Errors = [];
    }

    public static double Froude(double vKn, double L) => vKn * 0.5144 / Math.Sqrt(9.81 * L);

    public static double AdmiraltyC(double vKn, double L, double B, double cb, Tuning tun)
    {
        double fn = Math.Max(Froude(vKn, L), 0.08);
        double c = tun.AdmiraltyA * Math.Pow(fn, -tun.AdmiraltyB);
        c *= Math.Pow(L / B / 8.0, 0.25);
        c *= Math.Pow(0.55 / cb, 0.5);
        return c;
    }

    public static double PowerRequired(double disp, double vKn, double L, double B, double cb, Tuning tun)
    {
        if (tun.PlaningPower)
            return PlaningPower(disp, vKn, tun);
        return Math.Pow(disp, 2.0 / 3) * Math.Pow(vKn, 3) / AdmiraltyC(vKn, L, B, cb, tun);
    }

    /// <summary>Fn∇ = V / sqrt(g ∇^(1/3)).</summary>
    public static double VolumetricFroude(double disp, double vKn) =>
        vKn * 0.5144 / Math.Sqrt(9.81 * Math.Pow(disp / Weight.SEAWATER, 1.0 / 3));

    /// <summary>PLACEHOLDER planing-hull power model.</summary>
    public static double PlaningPower(double disp, double vKn, Tuning tun)
    {
        double fnv = VolumetricFroude(disp, vKn);
        double hi = tun.PlaningRw, lo = tun.PlaningRwDisp;
        double rw = fnv < 1 ? lo * Math.Pow(fnv, 2) : lo + (hi - lo) * Math.Min(1.0, fnv - 1);
        double rKn = rw * disp * 9.81;
        return rKn * vKn * 0.5144 / tun.PlaningEta / 0.7457;
    }

    /// <summary>The style's standard freeboard at full load for an ocean-going hull; hull.freeboard scales it.</summary>
    public static double DesignFreeboard(double L, Tuning tun) => tun.FreeboardA * L + tun.FreeboardB;

    /// <summary>Weights from the payload hook: the ship so far, the fuel and where it stands, and the lcb.</summary>
    public sealed record PayloadContext(List<Weight> Items, double Fuel, double FuelX, double Lcb);

    /// <summary>design: the sized design; placed: weights with x positions from the layout (null: a rough estimate,
    /// before anything is laid out); geo: layout facts (null before a layout exists).</summary>
    public static Result Solve(Design design, List<Weight>? placed = null, Geo? geo = null)
    {
        var style = Styles.Get(design);
        var tun = style.Tuning(design);
        var plant = Powerplant.Spec(design, style.DefaultTech);
        double L = design.HullLength, B = design.HullBeam, cb = design.BlockCoefficient;
        double V = design.SpeedKn!.Value;
        double rng = design.RangeNm ?? 6000;
        geo ??= new Geo();
        var res = new Result();

        double disp = 200.0 * L * B * cb * 0.04;
        var own = placed?.Select(w => w.Clone()).ToList();
        double T = 0, D = 0, shp = 0, std = 0, full = 0, fuel = 0, vc = 0;
        HullStructure hull = null!;
        List<Weight> items = [], stdLoad = [], fullLoad = [];
        bool settled = false;
        for (int it = 0; it < 60; it++)
        {
            T = disp / (Weight.SEAWATER * L * B * cb);
            D = T + DesignFreeboard(L, tun) * (design.Hull?.Freeboard ?? 1.0);
            items = [];
            var arm = Armour.ArmourGeometry(design, L, T, D, geo);
            hull = tun.BoxHull
                ? HullWeight.BoxStructure(design, L, B, D, tun)
                : HullWeight.HullStructure(design, L, B, cb, D, disp, arm, style.StrengthDeckOf(design, D), geo.Raised);
            double zFrac = tun.HullZFrac * ((hull.DepthM ?? D) / D);
            items.Add(new Weight("Hull structure", "hull", hull.T, -0.01 * L, ZRel.Frac(zFrac)));
            shp = PowerRequired(disp, V, L, B, cb, tun);
            items.Add(new Weight("Machinery", "machinery", Powerplant.Rated(plant, shp).WeightT, geo.MachineryMid(L), ZRel.Frac(0.32)));
            if (own is null)
            {
                items.AddRange(RoughPayload(design, D));
                items.AddRange(style.RoughPayload(design, D));
            }
            else
                items.AddRange(own);
            items.AddRange(Armour.ArmourWeights(design, L, B, D, arm));
            items.AddRange(style.StructureWeights(design, L, B, T, D, geo, tun));
            double wood = HullWeight.PlatingOf(design).DeckWoodMm;
            if (wood != 0)
            {
                var (area, wx, wz) = style.WeatherDeck(design, L, B);
                items.Add(new Weight("Deck planking", "hull", area * wood * HullWeight.RHO_WOOD, wx, ZRel.Deck(wz)));
            }
            double stdWoMisc = items.Sum(w => w.W);
            std = stdWoMisc / (1 - tun.MiscFrac);
            items.Add(new Weight("Equipment, outfit, crew & stores", "misc", std - stdWoMisc, 0.0, ZRel.Frac(0.5)));
            vc = tun.CruiseAtService ? V : Math.Min(tun.CruiseKn, 0.6 * V);
            double shpC = PowerRequired(disp, vc, L, B, cb, tun);
            fuel = Powerplant.FuelRate(plant, shp, shpC) * (rng / vc) / 1000.0;
            (stdLoad, fullLoad) = style.PayloadWeights(design, L, D, geo, tun,
                new PayloadContext(items, fuel, geo.MachineryMid(L), tun.LcbFrac * L));
            std += stdLoad.Sum(w => w.W);
            full = std + fuel + fullLoad.Sum(w => w.W);
            if (Math.Abs(full - disp) < 0.5)
            {
                disp = full;
                settled = true;
                break;
            }
            if (!double.IsFinite(full) || full > disp && full / (Weight.SEAWATER * L * B * cb) > OverloadTb * B)
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
        if (TB > tun.TbMax)
            res.Errors.Add($"Hull overloaded: draught {res.Draught:F1} m is {TB:F2} x beam (max {tun.TbMax}). Carry less.");
        else if (TB > tun.TbMax - 0.08)
            res.Warnings.Add($"Deep draught ({res.Draught:F1} m, {TB:F2} x beam): the hull is heavily loaded.");
        if (L / B < tun.LbWarn)
            res.Warnings.Add($"Very beamy hull (L/B {L / B:F1}): hard to drive, needs a lot of power.");
        if (L / B > 12)
            res.Warnings.Add($"Very slender hull (L/B {L / B:F1}): weak structure and poor stability.");
        Stability.Checks(res, L, B, tun);
        double fn = Froude(V, L);
        res.Warnings.AddRange(style.Checks(design, res, tun));
        res.Warnings.AddRange(Armour.ArmourChecks(design, res, geo));
        res.Warnings.AddRange(HullWeight.StructureChecks(res.Hull));
        if (fn > tun.FnWarn)
            res.Warnings.Add($"Speed {V} kn is extreme for a {L:F0} m hull (Froude {fn:F2}); power is enormous.");
        return res;
    }

    /// <summary>First-pass armament and superstructure estimate before the layout exists (all at x=0).</summary>
    public static List<Weight> RoughPayload(Design design, double D)
    {
        var out_ = Batteries.RoughArmament(design, D);
        out_.Add(new Weight("Superstructure", "superstructure", 0.012 * Math.Pow(design.HullLength, 2), zRel: ZRel.Deck(4)));
        return out_;
    }
}
