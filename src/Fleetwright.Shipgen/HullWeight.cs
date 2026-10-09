using System.Text.Json.Serialization;

namespace Fleetwright.Shipgen;

/// <summary>The hull's construction standard: its steel's yield, how much joining adds, and how heavy its plate runs
/// against the norm.</summary>
public sealed record Construction(string Name, double YieldMpa, double JoinFactor, double Standard);

/// <summary>hull.plating with its defaults: shell plate beyond the structure's own, its material, and deck planking.</summary>
public sealed record PlatingSpec(double ShellMm, string? Material, double DeckWoodMm);

/// <summary>The hull structure's weight and girder. The plate model fills every field; a box-model hull (planing craft)
/// only T, ShellT and PlateOwnMm.</summary>
public sealed record HullStructure(double T, double ShellT)
{
    public double? MinGaugeT { get; init; }
    public double? StrengthT { get; init; }
    public double? TMinMm { get; init; }
    public double? TStrMm { get; init; }
    public double? StressMpa { get; init; }
    public double? IReqM4 { get; init; }
    public double? IArmourM4 { get; init; }
    public double? IPlatingM4 { get; init; }
    /// <summary>The girder's depth when a strength deck stands above the main deck.</summary>
    public double? DepthM { get; init; }
    /// <summary>A box-model hull's own plate.</summary>
    public double? PlateOwnMm { get; init; }

    public bool PlateModel => TMinMm != null;
}

/// <summary>The plating the game's damage model sees: mm of each kind of plate, unarmoured.</summary>
public sealed record HullPlates(
    [property: JsonIgnore(Condition = JsonIgnoreCondition.Never)] string? Material,
    double ShellMm, double ShellEndMm, double StrengthDeckMm, double StrengthDeckEndMm, double MidX0, double MidX1,
    double DeckMm, double BulkheadMm, double InnerBottomMm, double SuperstructureMm, double ControlMm, double DeckWoodMm)
    : IPyValue
{
    public object? ToPy() => JsonBridge.ToPy(this, ShipgenJson.Default.HullPlates);
}

/// <summary>Hull structure weight, by plate area x thickness (research/hull-weight-model.md, "Tier 2").</summary>
public static class HullWeight
{
    public const double RHO = 7.85e-3, RHO_WOOD = 0.7e-3;
    const double K_S = 2.5, C_M = 40.0, F_FIT = 0.10, SF = 2.1, SIG_CAP = 185.0;
    static readonly (double A, double B) T_MIN = (4.0, 0.03);
    const double LONG = 350.0;
    const double SHELL_SIDE = 0.90, SHELL_BOTTOM = 0.95, INT_DECK = 0.85, INT_DECK_T = 0.60;
    const double BULKHEADS = 1 + 1 / 0.06, BHD_AREA = 0.75 * 0.80, BHD_T = 0.70, DB_AREA = 0.80 * 1.60;
    const double GIRDER_TAPER = 0.75, NEUTRAL_AXIS = 0.45, ARM_DECK_WIDTH = 0.85;
    public const double SUP_PLATE_K = 0.8;

    /// <summary>hull.construction with its defaults filled in.</summary>
    public static Construction ConstructionOf(Design design)
    {
        var c = design.Hull?.Construction;
        return new Construction(c?.Name ?? "High-tensile steel, all welded", c?.YieldMpa ?? 350, c?.JoinFactor ?? 1.00,
            c?.Standard ?? 1.0);
    }

    /// <summary>hull.plating with its defaults filled in.</summary>
    public static PlatingSpec PlatingOf(Design design)
    {
        var p = design.Hull?.Plating;
        return new PlatingSpec(p?.ShellMm ?? 0.0, p?.Material, p?.DeckWoodMm ?? 0.0);
    }

    /// <summary>The minimum gauge, mm: the thinnest plate the hull is built of.</summary>
    public static double TMinMm(double L, Construction c) => (T_MIN.A + T_MIN.B * Math.Min(L, LONG)) * c.Standard;

    /// <summary>Plate thicker than the structure's own, t: plain steel plate over the area.</summary>
    public static double ExtraPlateT(double areaM2, double mm, double ownMm) => RHO * areaM2 * Math.Max(0.0, mm - ownMm);

    /// <summary>The plating the game's damage model sees, from the structure h: mm of each kind of plate, unarmoured.
    /// ownMm: the hull's own gauge where the structure has no plate model.</summary>
    public static HullPlates Plates(HullStructure h, double ownMm, double L, double shellMm, string? material, double supMm,
        double controlMm, double deckWoodMm = 0.0)
    {
        double tMin = h.TMinMm ?? ownMm;
        double tStr = h.TStrMm ?? 0.0;
        static double R(double v) => Math.Round(v, 1);
        double ownSup = SUP_PLATE_K * tMin;
        return new HullPlates(material, R(Math.Max(tMin, Math.Max(tStr, shellMm))), R(Math.Max(tMin, shellMm)),
            R(Math.Max(tMin, tStr)), R(tMin), R(-GIRDER_TAPER * L / 2), R(GIRDER_TAPER * L / 2), R(INT_DECK_T * tMin),
            R(BHD_T * tMin), R(tMin), R(Math.Max(ownSup, supMm)), R(Math.Max(ownSup, Math.Max(supMm, controlMm))), R(deckWoodMm));
    }

    public static double AllowableStress(Construction c) => Math.Min(c.YieldMpa / SF, SIG_CAP);

    /// <summary>The strength deck's area, as the model takes it.</summary>
    public static double DeckArea(double L, double B, double cb) => B * L * (0.66 + 0.33 * cb);

    /// <summary>What the model weighs per m^2 of strength deck at minimum gauge.</summary>
    public static double DeckTPerM2(double L, Construction c) =>
        RHO * K_S * (T_MIN.A + T_MIN.B * Math.Min(L, LONG)) * c.Standard * (1 + F_FIT) * c.JoinFactor;

    /// <summary>A raised stretch of hull (forecastle, poop) at minimum gauge.</summary>
    public static double RaisedT(double L, Construction c, double deckM2, double sideM2, double endM2, double shellMm = 0.0) =>
        DeckTPerM2(L, c) * (deckM2 + SHELL_SIDE * sideM2 + BHD_T * endM2)
        + ExtraPlateT(SHELL_SIDE * sideM2, shellMm, TMinMm(L, c)) * c.JoinFactor;

    /// <summary>The hull structure: its weight, what it is made of and its girder.</summary>
    public static HullStructure WeightOf(double L, double B, double D, double cb, double full, Construction c, double nInt,
        double doubleBottom, IReadOnlyList<(double Mm, double Z)> armourDecks, double? bulkheadDepth = null,
        double? girderDepth = null, double shellMm = 0.0, double armouredSideM2 = 0.0)
    {
        double aShell = 2 * SHELL_SIDE * D * L + SHELL_BOTTOM * B * L * Math.Sqrt(cb);
        double aDeck = DeckArea(L, B, cb);
        double aInt = nInt * INT_DECK * aDeck;
        double aBhd = BULKHEADS * BHD_AREA * B * (bulkheadDepth ?? D);
        double aDb = doubleBottom * B * L * cb * DB_AREA;
        double tMin = TMinMm(L, c);
        double sig = AllowableStress(c);
        double m = full * 9.81 * L / C_M * Math.Pow(Math.Min(1.0, LONG / L), 2);
        double G = girderDepth ?? D;
        double iReq = m / (sig * 1000) * (G / 2);
        double iArm = armourDecks.Sum(a => ARM_DECK_WIDTH * B * a.Mm / 1000 * Math.Pow(a.Z - NEUTRAL_AXIS * G, 2));
        double zPerMm = G * (B + G / 3) / 1000;
        double tStr = Math.Max(0.0, iReq - iArm) / (G / 2) / zPerMm;
        double wMin = RHO * K_S * tMin * (aShell + aDeck + aInt * INT_DECK_T + aBhd * BHD_T + aDb);
        double wStr = RHO * GIRDER_TAPER * (aShell + aDeck) * Math.Max(0.0, tStr - tMin);
        double k = (1 + F_FIT) * c.JoinFactor;
        double aSide = Math.Max(0.0, 2 * SHELL_SIDE * D * L - armouredSideM2);
        double wShell = (ExtraPlateT((1 - GIRDER_TAPER) * aSide, shellMm, tMin)
                         + ExtraPlateT(GIRDER_TAPER * aSide, shellMm, Math.Max(tMin, tStr))) * c.JoinFactor;
        return new HullStructure((wMin + wStr) * k + wShell, wShell)
        {
            MinGaugeT = wMin * k, StrengthT = wStr * k, TMinMm = tMin, TStrMm = tStr, StressMpa = sig, IReqM4 = iReq,
            IArmourM4 = iArm, IPlatingM4 = Math.Max(tStr, tMin) * zPerMm * G / 2,
        };
    }

    const double STACK_DECK = 0.6;
    static readonly (double Lo, double Hi) INNER_BOTTOM_T = (4000.0, 10000.0);
    const double GIRDER_MID = 0.2;

    /// <summary>The mean height of raised stretches of hull over the midbody, |x| &lt;= GIRDER_MID L.</summary>
    public static double RaisedGirderH(IReadOnlyList<RaisedStretch> raised, double L)
    {
        double a = -GIRDER_MID * L, b = GIRDER_MID * L;
        double tot = 0.0;
        foreach (var s in raised)
            tot += Math.Max(0.0, Math.Min(b, s.X1) - Math.Max(a, s.X0)) * s.Levels * Geometry.DECK_PITCH;
        return tot / (b - a);
    }

    /// <summary>The hull's structure weight and girder. arm: the armour's geometry; above: the style's strength deck
    /// above the main deck, or null; raised: the layout's raised stretches.</summary>
    public static HullStructure HullStructure(Design design, double L, double B, double cb, double D, double full, ArmourLayout arm,
        StrengthDeck? above, IReadOnlyList<RaisedStretch> raised)
    {
        double nInt = STACK_DECK * Math.Max(0.0, (D - Powerplant.DoubleBottom(D) - Decks.MIN_TIER) / Geometry.DECK_PITCH);
        var (lo, hi) = INNER_BOTTOM_T;
        double inner = Math.Min(1.0, Math.Max(0.0, (full - lo) / (hi - lo)));
        var plates = arm.Decks.Where(d => d.X0 <= 0.0 && 0.0 <= d.X1).Select(d => (d.Mm, d.Z)).ToList();
        double depth = D;
        if (above != null)
        {
            depth = D + above.H;
            nInt += above.Decks;
            plates.AddRange(above.Plates);
        }
        double rh = raised.Count > 0 ? RaisedGirderH(raised, L) : 0.0;
        double sideArm = arm.Strakes.Where(s => s.Kind != "box").Sum(s => (s.X1 - s.X0) * (s.Top - s.Bottom));
        if (arm.BeltMm > 0)
            sideArm += (arm.X1 - arm.X0) * (arm.BeltTop - arm.BeltBottom);
        var h = WeightOf(L, B, depth, cb, full, ConstructionOf(design), nInt, inner, plates, bulkheadDepth: D,
            girderDepth: rh != 0 ? depth + rh : null, shellMm: PlatingOf(design).ShellMm,
            armouredSideM2: 2 * Math.Max(0.0, sideArm));
        return h with { DepthM = depth };
    }

    /// <summary>A box-model hull (planing craft) has no plate model: structure = hull_k (L B D)^hull_exp plus the shell's
    /// extra plating; its own gauge is the style's (PlateOwnMm).</summary>
    public static HullStructure BoxStructure(Design design, double L, double B, double D, Tuning tun)
    {
        double plank = tun.PlateOwnMm;
        double shellT = ExtraPlateT(2 * SHELL_SIDE * D * L, PlatingOf(design).ShellMm, plank);
        return new HullStructure(tun.HullK * Math.Pow(L * B * D, tun.HullExp) + shellT, shellT) { PlateOwnMm = plank };
    }

    /// <summary>Warnings on a solved hull's structure.</summary>
    public static List<string> StructureChecks(HullStructure h)
    {
        var out_ = new List<string>();
        if ((h.StrengthT ?? 0.0) > (h.MinGaugeT ?? double.PositiveInfinity))
            out_.Add($"The hull is very long for its depth: {h.StrengthT:N0} t of its plating (strength " +
                     $"deck and shell {h.TStrMm:F0} mm, where {h.TMinMm:F0} mm would do) only " +
                     "keeps it from breaking in two. A shorter hull or an armour deck high in it would help.");
        return out_;
    }

    public static List<string> Validate(Design design)
    {
        var errs = new List<string>();
        if (design.Hull?.Freeboard is double f && !(f > 0))
            errs.Add("hull.freeboard: a factor above 0 on the style's standard freeboard (1.0)");
        if (design.Hull?.Construction is { } c)
        {
            foreach (var (k, v) in new[] { ("yield_mpa", c.YieldMpa), ("join_factor", c.JoinFactor), ("standard", c.Standard) })
                if (v is double x && !(x > 0))
                    errs.Add($"hull.construction.{k}: must be a number above 0");
        }
        if (design.Hull?.Plating is { } p)
        {
            errs.AddRange(p.Extra.KeysOrEmpty().Select(k => $"hull.plating.{k}: not a plating setting (shell_mm, material, deck_wood_mm)"));
            if (p.ShellMm < 0)
                errs.Add("hull.plating.shell_mm: a number, 0 or more (0: the structure's own gauge)");
            if (p.DeckWoodMm < 0)
                errs.Add("hull.plating.deck_wood_mm: a number, 0 or more (0: a bare steel deck)");
            if (p.Material is "")
                errs.Add("hull.plating.material: name the material as a string");
        }
        return errs;
    }
}

/// <summary>What a ship carries that burns or explodes, stowed low in the hull: the magazines for its guns'
/// ammunition, and a carrier's aviation ordnance and fuel.</summary>
public static class Ordnance
{
    public const double T_PER_M3 = 0.6;
    public const int TIERS = 2;
    public const double MIN_ROOM = 1.5;
    const double READY_K = 17000.0, READY_P = 1.4, WARHEAD_K = 2.0e-6;
    const double TORPEDO_MM = 533;

    /// <summary>(base, top) above the main deck of a room standing on the inner bottom, top on a deck of the stack
    /// (decks: heights above the keel, the main deck first) and never above the roof.</summary>
    public static (double Base, double Top) Span(IReadOnlyList<double> decks, double innerBottom, double roof, double? needH = null,
        int tiers = TIERS)
    {
        double D = decks[0], ib = innerBottom;
        var ups = decks.Where(z => ib + 1e-6 < z && z <= roof + 1e-6).Order().ToList();
        if (ups.Count == 0)
            ups = [roof];
        double top;
        if (needH is null)
            top = ups[Math.Min(tiers, ups.Count) - 1];
        else
        {
            double nh = needH.Value;
            top = ups.Cast<double?>().FirstOrDefault(z => z >= ib + nh - 1e-6) ?? ups[^1];
        }
        return (ib - D, top - D);
    }

    public static (double Base, double Top) Span(PlantPlan plan, double? needH = null, int tiers = TIERS) =>
        Span(plan.Decks, plan.InnerBottom, plan.Top, needH, tiers);

    /// <summary>The height of a room given `tiers` deck spaces.</summary>
    public static double Height(PlantPlan plan, int tiers = TIERS)
    {
        var (b, t) = Span(plan, tiers: tiers);
        return t - b;
    }

    public static double AmmoT(PyDict t) => Batteries.MountWeights(t, 0.0, 0.0, 0).Ammo;

    /// <summary>Ready-use ammunition at a gun mount at action stations: (rounds, tonnes).</summary>
    public static (long N, double T) ReadyUse(double calibreMm, double barrels, double? cap = null)
    {
        double n = barrels * Math.Max(1L, (long)Math.Round(READY_K * Math.Pow(calibreMm, -READY_P)));
        if (cap is double c)
            n = Math.Min(n, c);
        return ((long)n, n * Batteries.SHELL_K * Math.Pow(calibreMm, 3) / 1000.0 * Batteries.AMMO_MULT);
    }

    public static double WarheadKg(double diameterMm = TORPEDO_MM) => WARHEAD_K * Math.Pow(diameterMm, 3);

    public static double AmmoM3(PyDict t) => AmmoT(t) / T_PER_M3;

    /// <summary>The ids of the mounts that carry ammunition (guns, not torpedo tubes).</summary>
    public static List<string> Guns(IEnumerable<PyDict> mounts) =>
        mounts.Where(m => Py.In(m["kind"], "main", "secondary")).Select(m => m.S("id")).ToList();

    /// <summary>The magazine volume the mounts' booked ammunition ("Magazine &lt;id&gt;" weights) needs.</summary>
    public static double BookedM3(Layout lay, IEnumerable<string> mids)
    {
        var names = new HashSet<string>(mids.Select(m => $"Magazine {m}"), StringComparer.Ordinal);
        return lay.Weights.Where(w => names.Contains(w.Name)).Select(w => w.W).Sum() / T_PER_M3;
    }

    /// <summary>The length a zone `width` across needs for volume_m3 at `tiers` deck spaces tall.</summary>
    public static double ZoneLength(double volumeM3, double width, PlantPlan plan, int tiers = TIERS, double least = MIN_ROOM) =>
        Math.Max(least, volumeM3 / Math.Max(width * Height(plan, tiers), 1.0));

    /// <summary>Stow the ordnance in zones (dicts x0, x1, half_width, rooms, own). Adds the compartments, sets each
    /// mount's "magazine", and moves its "Magazine &lt;id&gt;" weight to its room. Returns {room id: (x0, x1, base, top)}.</summary>
    public static Dictionary<string, (double X0, double X1, double Base, double Top)> Stow(Layout lay, IReadOnlyList<PyDict> mounts,
        IEnumerable<PyDict> zones)
    {
        var plan = lay.Geo.Plant!;
        var byId = new Dictionary<string, PyDict>(StringComparer.Ordinal);
        foreach (var m in mounts)
            byId[m.S("id")] = m;
        var ammo = new Dictionary<string, Weight>(StringComparer.Ordinal);
        foreach (var w in lay.Weights)
            if (w.Name.StartsWith("Magazine ", StringComparison.Ordinal))
                ammo[w.Name] = w;
        var out_ = new Dictionary<string, (double, double, double, double)>(StringComparer.Ordinal);
        foreach (var z in zones)
        {
            var rooms = new List<(PyDict R, List<PyDict> Ms, double T, double V)>();
            foreach (PyDict r in z.L("rooms").Cast<PyDict>())
            {
                var mids = r.Get("mounts") is List<object?> ml ? ml.Cast<string>() : r.Get("mounts") is List<string> sl ? sl : [];
                var ms = mids.Where(mid => ammo.ContainsKey($"Magazine {mid}")).Select(mid => byId[mid]).ToList();
                double t = r.F("tonnes", 0.0) + ms.Select(m => ammo[$"Magazine {m.S("id")}"].W).Sum();
                rooms.Add((r, ms, t, t / r.F("t_per_m3", T_PER_M3)));
            }
            double vol = rooms.Select(r => r.V).Sum();
            if (vol <= 0)
                continue;
            double L = z.F("x1") - z.F("x0");
            var (bse, top) = Span(plan, vol / Math.Max(1.0, L * 2 * z.F("half_width")));
            double x = z.F("x1");
            foreach (var (r, ms, t, v) in rooms)
            {
                if (v <= 0)
                    continue;
                double l = L * v / vol;
                var c = PyDict.Of(("id", r["id"]), ("kind", r.Get("kind", "magazine")), ("x0", x - l), ("x1", x),
                    ("half_width", z["half_width"]), ("base", bse), ("top", top), ("tonnes", Math.Round(t, 1)));
                foreach (var kv in r)
                    if (!(kv.Key is "id" or "kind" or "mounts" or "tonnes" or "t_per_m3"))
                        c[kv.Key] = kv.Value;
                if (ms.Count == 1 && z.B("own"))
                    c["mount"] = ms[0]["id"];
                else if (ms.Count > 0)
                    c["mounts"] = ms.Select(m => m["id"]).ToList();
                lay.Compartments.Add(c);
                foreach (var m in ms)
                {
                    m["magazine"] = r["id"];
                    var w = ammo[$"Magazine {m.S("id")}"];
                    w.X = x - l / 2;
                    w.ZRel = ZRel.Deck((bse + top) / 2);
                }
                out_[r.S("id")] = (x - l, x, bse, top);
                x -= l;
            }
        }
        return out_;
    }

    /// <summary>A zone for one mount's own magazine: under it on the centreline, its diameter long.</summary>
    public static PyDict OwnZone(PyDict m, double innerHw)
    {
        double r = m.D("t").F("r");
        return PyDict.Of(("x0", m.F("x") - r), ("x1", m.F("x") + r), ("half_width", Math.Min(r, innerHw)), ("own", true),
            ("rooms", new List<object?> { PyDict.Of(("id", $"Magazine {m.S("id")}"), ("mounts", new List<object?> { m["id"] })) }));
    }
}
