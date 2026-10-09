namespace Fleetwright.Shipgen;

/// <summary>hullweight: hull structure weight, by plate area x thickness (research/hull-weight-model.md, "Tier 2").</summary>
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

    static PyDict Default() => PyDict.Of(("name", "High-tensile steel, all welded"), ("yield_mpa", 350L),
        ("join_factor", 1.00), ("standard", 1.0));

    /// <summary>hull.construction with its defaults filled in.</summary>
    public static PyDict Construction(PyDict design) =>
        PyDict.Merge(Default(), design.DOr("hull").Or("construction", null) as PyDict);

    /// <summary>hull.plating with its defaults filled in.</summary>
    public static PyDict Plating(PyDict design) =>
        PyDict.Merge(PyDict.Of(("shell_mm", 0.0), ("material", null), ("deck_wood_mm", 0.0)),
            design.DOr("hull").Or("plating", null) as PyDict);

    /// <summary>The minimum gauge, mm: the thinnest plate the hull is built of.</summary>
    public static double TMinMm(double L, PyDict c) => (T_MIN.A + T_MIN.B * Py.Min(L, LONG)) * c.F("standard");

    /// <summary>Plate thicker than the structure's own, t: plain steel plate over the area.</summary>
    public static double ExtraPlateT(double areaM2, double mm, double ownMm) => RHO * areaM2 * Py.Max(0.0, mm - ownMm);

    /// <summary>The plating the game's damage model sees, from the structure h: mm of each kind of plate, unarmoured.</summary>
    public static PyDict Plates(PyDict h, double L, double shellMm, object? material, double supMm, double controlMm,
        double deckWoodMm = 0.0)
    {
        double tMin = h.Has("t_min_mm") ? h.F("t_min_mm") : h.F("plate_own_mm", 0.0);
        double tStr = h.F("t_str_mm", 0.0);
        static double R(double v) => Py.Round(v, 1);
        double ownSup = SUP_PLATE_K * tMin;
        return PyDict.Of(("material", material), ("shell_mm", R(Py.Max(tMin, tStr, shellMm))),
            ("shell_end_mm", R(Py.Max(tMin, shellMm))), ("strength_deck_mm", R(Py.Max(tMin, tStr))),
            ("strength_deck_end_mm", R(tMin)), ("mid_x0", R(-GIRDER_TAPER * L / 2)), ("mid_x1", R(GIRDER_TAPER * L / 2)),
            ("deck_mm", R(INT_DECK_T * tMin)), ("bulkhead_mm", R(BHD_T * tMin)), ("inner_bottom_mm", R(tMin)),
            ("superstructure_mm", R(Py.Max(ownSup, supMm))), ("control_mm", R(Py.Max(ownSup, supMm, controlMm))),
            ("deck_wood_mm", R(deckWoodMm)));
    }

    public static double AllowableStress(PyDict c) => Py.Min(c.F("yield_mpa") / SF, SIG_CAP);

    /// <summary>The strength deck's area, as the model takes it.</summary>
    public static double DeckArea(double L, double B, double cb) => B * L * (0.66 + 0.33 * cb);

    /// <summary>What the model weighs per m^2 of strength deck at minimum gauge.</summary>
    public static double DeckTPerM2(double L, PyDict c) =>
        RHO * K_S * (T_MIN.A + T_MIN.B * Py.Min(L, LONG)) * c.F("standard") * (1 + F_FIT) * c.F("join_factor");

    /// <summary>A raised stretch of hull (forecastle, poop) at minimum gauge.</summary>
    public static double RaisedT(double L, PyDict c, double deckM2, double sideM2, double endM2, double shellMm = 0.0) =>
        DeckTPerM2(L, c) * (deckM2 + SHELL_SIDE * sideM2 + BHD_T * endM2)
        + ExtraPlateT(SHELL_SIDE * sideM2, shellMm, TMinMm(L, c)) * c.F("join_factor");

    /// <summary>The hull structure: dict(t, min_gauge_t, strength_t, shell_t, t_min_mm, t_str_mm, stress_mpa, i_req_m4,
    /// i_armour_m4, i_plating_m4).</summary>
    public static PyDict WeightOf(double L, double B, double D, double cb, double full, PyDict c, double nInt,
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
        double m = full * 9.81 * L / C_M * Py.Pow(Py.Min(1.0, LONG / L), 2);
        double G = girderDepth ?? D;
        double iReq = m / (sig * 1000) * (G / 2);
        object iArmObj = armourDecks.Count == 0 ? 0L
            : Py.Sum(armourDecks.Select(a => ARM_DECK_WIDTH * B * a.Mm / 1000 * Py.Pow(a.Z - NEUTRAL_AXIS * G, 2)));
        double iArm = Py.ToDouble(iArmObj);
        double zPerMm = G * (B + G / 3) / 1000;
        double tStr = Py.Max(0.0, iReq - iArm) / (G / 2) / zPerMm;
        double wMin = RHO * K_S * tMin * (aShell + aDeck + aInt * INT_DECK_T + aBhd * BHD_T + aDb);
        double wStr = RHO * GIRDER_TAPER * (aShell + aDeck) * Py.Max(0.0, tStr - tMin);
        double k = (1 + F_FIT) * c.F("join_factor");
        double aSide = Py.Max(0.0, 2 * SHELL_SIDE * D * L - armouredSideM2);
        double wShell = (ExtraPlateT((1 - GIRDER_TAPER) * aSide, shellMm, tMin)
                         + ExtraPlateT(GIRDER_TAPER * aSide, shellMm, Py.Max(tMin, tStr))) * c.F("join_factor");
        return PyDict.Of(("t", (wMin + wStr) * k + wShell), ("min_gauge_t", wMin * k), ("strength_t", wStr * k),
            ("shell_t", wShell), ("t_min_mm", tMin), ("t_str_mm", tStr), ("stress_mpa", sig), ("i_req_m4", iReq),
            ("i_armour_m4", iArmObj), ("i_plating_m4", Py.Max(tStr, tMin) * zPerMm * G / 2));
    }

    const double STACK_DECK = 0.6;
    static readonly (double Lo, double Hi) INNER_BOTTOM_T = (4000.0, 10000.0);
    const double GIRDER_MID = 0.2;

    /// <summary>The mean height of raised stretches of hull over the midbody, |x| &lt;= GIRDER_MID L.</summary>
    public static double RaisedGirderH(IReadOnlyList<PyDict> raised, double L)
    {
        double a = -GIRDER_MID * L, b = GIRDER_MID * L;
        double tot = 0.0;
        foreach (var s in raised)
            tot += Py.Max(0.0, Py.Min(b, s.F("x1")) - Py.Max(a, s.F("x0"))) * s.F("levels") * Geometry.DECK_PITCH;
        return tot / (b - a);
    }

    /// <summary>The hull's structure weight and girder. arm: armour.armour_geometry; above: the style's strength deck
    /// above the main deck (dict(h, decks, plates)) or null; raised: lay.raised.</summary>
    public static PyDict HullStructure(PyDict design, double L, double B, double cb, double D, double full, PyDict arm,
        PyDict? above, IReadOnlyList<PyDict> raised)
    {
        double nInt = STACK_DECK * Py.Max(0.0, (D - Powerplant.DoubleBottom(D) - Decks.MIN_TIER) / Geometry.DECK_PITCH);
        var (lo, hi) = INNER_BOTTOM_T;
        double inner = Py.Min(1.0, Py.Max(0.0, (full - lo) / (hi - lo)));
        var plates = arm.L("decks").Cast<PyDict>().Where(d => d.F("x0") <= 0.0 && 0.0 <= d.F("x1"))
            .Select(d => (d.F("mm"), d.F("z"))).ToList();
        double depth = D;
        if (above != null)
        {
            depth = D + above.F("h");
            nInt = nInt + above.F("decks");
            plates = plates.Concat(((List<(double, double)>)above["plates"]!)).ToList();
        }
        double rh = raised.Count > 0 ? RaisedGirderH(raised, L) : 0.0;
        object sideArmObj = Py.SumObj(arm.L("strakes").Cast<PyDict>().Where(s => !Py.Eq(s["kind"], "box"))
            .Select(s => (object?)((s.F("x1") - s.F("x0")) * (s.F("top") - s.F("bottom")))));
        double sideArm = Py.ToDouble(sideArmObj);
        if (arm.F("belt_mm") > 0)
            sideArm += (arm.F("x1") - arm.F("x0")) * (arm.F("belt_top") - arm.F("belt_bottom"));
        var out_ = WeightOf(L, B, depth, cb, full, Construction(design), nInt, inner, plates, bulkheadDepth: D,
            girderDepth: rh != 0 ? depth + rh : null, shellMm: Plating(design).F("shell_mm"),
            armouredSideM2: 2 * Py.Max(0.0, sideArm));
        out_["depth_m"] = depth;
        return out_;
    }

    /// <summary>A box-model hull (planing craft) has no plate model: structure = hull_k (L B D)^hull_exp plus the shell's
    /// extra plating; its own gauge is the style's (plate_own_mm).</summary>
    public static PyDict BoxStructure(PyDict design, double L, double B, double D, PyDict tun)
    {
        double plank = tun.F("plate_own_mm", 0.0);
        double shellT = ExtraPlateT(2 * SHELL_SIDE * D * L, Plating(design).F("shell_mm"), plank);
        return PyDict.Of(("t", tun.F("hull_k") * Py.Pow(L * B * D, tun.F("hull_exp")) + shellT), ("shell_t", shellT),
            ("plate_own_mm", plank));
    }

    /// <summary>Warnings on a solved hull's structure.</summary>
    public static List<string> StructureChecks(PyDict h)
    {
        var out_ = new List<string>();
        if (h.F("strength_t", 0.0) > h.F("min_gauge_t", double.PositiveInfinity))
            out_.Add($"The hull is very long for its depth: {Py.F(h.F("strength_t"), 0, comma: true)} t of its plating (strength " +
                     $"deck and shell {Py.F(h.F("t_str_mm"), 0)} mm, where {Py.F(h.F("t_min_mm"), 0)} mm would do) only " +
                     "keeps it from breaking in two. A shorter hull or an armour deck high in it would help.");
        return out_;
    }

    public static List<string> Validate(PyDict design)
    {
        var f = design.DOr("hull").Get("freeboard", 1.0);
        bool ok = Py.IsNumber(f) && Py.ToDouble(f) > 0;
        var errs = ok ? new List<string>() : ["hull.freeboard: a factor above 0 on the style's standard freeboard (1.0)"];
        errs.AddRange(ConstructionErrors(design));
        errs.AddRange(PlatingErrors(design));
        return errs;
    }

    static List<string> PlatingErrors(PyDict design)
    {
        var p = design.DOr("hull").Get("plating");
        if (p is null)
            return [];
        if (p is not PyDict pd)
            return ["hull.plating: use {\"shell_mm\", \"material\", \"deck_wood_mm\"}"];
        var errs = pd.Keys.Where(k => !(k is "shell_mm" or "material" or "deck_wood_mm"))
            .Select(k => $"hull.plating.{k}: not a plating setting (shell_mm, material, deck_wood_mm)").ToList();
        if (pd.Has("shell_mm") && !(Py.IsNumber(pd["shell_mm"]) && Py.ToDouble(pd["shell_mm"]) >= 0))
            errs.Add("hull.plating.shell_mm: a number, 0 or more (0: the structure's own gauge)");
        if (pd.Has("deck_wood_mm") && !(Py.IsNumber(pd["deck_wood_mm"]) && Py.ToDouble(pd["deck_wood_mm"]) >= 0))
            errs.Add("hull.plating.deck_wood_mm: a number, 0 or more (0: a bare steel deck)");
        if (pd.Has("material") && !(pd["material"] is string ms && ms.Length > 0))
            errs.Add("hull.plating.material: name the material as a string");
        return errs;
    }

    static List<string> ConstructionErrors(PyDict design)
    {
        var c = design.DOr("hull").Get("construction");
        if (c is null)
            return [];
        if (c is not PyDict cd)
            return ["hull.construction: use {\"name\", \"yield_mpa\", \"join_factor\", \"standard\"} " +
                    "(hull-templates.md has examples)"];
        var errs = new[] { "yield_mpa", "join_factor", "standard" }
            .Where(k => cd.Has(k) && !(Py.IsNumber(cd[k]) && Py.ToDouble(cd[k]) > 0))
            .Select(k => $"hull.construction.{k}: must be a number above 0").ToList();
        if (cd.Has("name") && cd["name"] is not string)
            errs.Add("hull.construction.name: use a string");
        return errs;
    }
}

/// <summary>ordnance: what a ship carries that burns or explodes, stowed low in the hull: the magazines for its guns'
/// ammunition, and a carrier's aviation ordnance and fuel.</summary>
public static class Ordnance
{
    public const double T_PER_M3 = 0.6;
    public const int TIERS = 2;
    public const double MIN_ROOM = 1.5;
    const double READY_K = 17000.0, READY_P = 1.4, WARHEAD_K = 2.0e-6;
    const double TORPEDO_MM = 533;

    /// <summary>(base, top) above the main deck of a room standing on the inner bottom, top on a deck of the stack and
    /// never above the plan's roof.</summary>
    public static (double Base, double Top) Span(PyDict plan, double? needH = null, int tiers = TIERS)
    {
        var decks = plan.L("decks").Select(Py.ToDouble).ToList();
        double D = decks[0], ib = plan.F("inner_bottom"), roof = plan.F("top");
        var ups = Py.Sorted(decks.Where(z => ib + 1e-6 < z && z <= roof + 1e-6));
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

    /// <summary>The height of a room given `tiers` deck spaces.</summary>
    public static double Height(PyDict plan, int tiers = TIERS)
    {
        var (b, t) = Span(plan, tiers: tiers);
        return t - b;
    }

    public static double AmmoT(PyDict t) => Batteries.MountWeights(t, 0.0, 0.0, 0).Ammo;

    /// <summary>Ready-use ammunition at a gun mount at action stations: (rounds, tonnes).</summary>
    public static (long N, double T) ReadyUse(double calibreMm, double barrels, double? cap = null)
    {
        double n = barrels * Py.Max(1L, Py.Round(READY_K * Py.Pow(calibreMm, -READY_P)));
        if (cap is double c)
            n = Py.Min(n, c);
        return ((long)n, n * Batteries.SHELL_K * Py.Pow(calibreMm, 3) / 1000.0 * Batteries.AMMO_MULT);
    }

    public static double WarheadKg(double diameterMm = TORPEDO_MM) => WARHEAD_K * Py.Pow(diameterMm, 3);

    public static double AmmoM3(PyDict t) => AmmoT(t) / T_PER_M3;

    /// <summary>The ids of the mounts that carry ammunition (guns, not torpedo tubes).</summary>
    public static List<string> Guns(IEnumerable<PyDict> mounts) =>
        mounts.Where(m => Py.In(m["kind"], "main", "secondary")).Select(m => m.S("id")).ToList();

    /// <summary>The magazine volume the mounts' booked ammunition ("Magazine &lt;id&gt;" weights) needs.</summary>
    public static double BookedM3(Layout lay, IEnumerable<string> mids)
    {
        var names = new HashSet<string>(mids.Select(m => $"Magazine {m}"), StringComparer.Ordinal);
        return Py.Sum(lay.Weights.Where(w => names.Contains(w.Name)).Select(w => w.W)) / T_PER_M3;
    }

    /// <summary>The length a zone `width` across needs for volume_m3 at `tiers` deck spaces tall.</summary>
    public static double ZoneLength(double volumeM3, double width, PyDict plan, int tiers = TIERS, double least = MIN_ROOM) =>
        Py.Max(least, volumeM3 / Py.Max(width * Height(plan, tiers), 1.0));

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
                double t = r.F("tonnes", 0.0) + Py.Sum(ms.Select(m => ammo[$"Magazine {m.S("id")}"].W));
                rooms.Add((r, ms, t, t / r.F("t_per_m3", T_PER_M3)));
            }
            double vol = Py.Sum(rooms.Select(r => r.V));
            if (vol <= 0)
                continue;
            double L = z.F("x1") - z.F("x0");
            var (bse, top) = Span(plan, vol / Py.Max(1.0, L * 2 * z.F("half_width")));
            double x = z.F("x1");
            foreach (var (r, ms, t, v) in rooms)
            {
                if (v <= 0)
                    continue;
                double l = L * v / vol;
                var c = PyDict.Of(("id", r["id"]), ("kind", r.Get("kind", "magazine")), ("x0", x - l), ("x1", x),
                    ("half_width", z["half_width"]), ("base", bse), ("top", top), ("tonnes", Py.Round(t, 1)));
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
        return PyDict.Of(("x0", m.F("x") - r), ("x1", m.F("x") + r), ("half_width", Py.Min(r, innerHw)), ("own", true),
            ("rooms", new List<object?> { PyDict.Of(("id", $"Magazine {m.S("id")}"), ("mounts", new List<object?> { m["id"] })) }));
    }
}
