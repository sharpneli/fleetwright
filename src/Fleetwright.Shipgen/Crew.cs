namespace Fleetwright.Shipgen;

/// <summary>crew: the complement and the volume it needs (research/crew-space-model.md), and battle stations.</summary>
public static class Crew
{
    const double USABLE = 0.65, CREW_T = 0.12, PROVISIONS_T_PER_M3 = 0.6;

    static PyDict Std(string name, double sr, double sc, double so, double mess, double gk, double gmin, double san, double sick,
        double welfare, double serv, double passage, double deck, double prov, object water, double hotel, double cpo, long tol) =>
        PyDict.Of(("name", name), ("sleep_rating_m2", sr), ("sleep_cpo_m2", sc), ("sleep_officer_m2", so), ("mess_seats_per_man", mess),
            ("galley_k", gk), ("galley_min_m2", gmin), ("sanitary_m2", san), ("sickbay_beds_per_man", sick), ("welfare_m2", welfare),
            ("services_k", serv), ("passage_factor", passage), ("deck_height_m", deck), ("provisions_m3_per_day", prov),
            ("water_l_per_day", water), ("hotel_fraction", hotel), ("cpo_fraction", cpo), ("tolerance_days", tol));

    static PyDict Standard(string key) => key switch
    {
        "H0" => Std("Sleep at station (MTB, PT boat)", 0.6, 0.8, 1.5, 0.0, 0.25, 1.0, 0.06, 0.0, 0.0, 0.0, 1.10, 1.9, 0.006, 8L, 0.0, 0.0, 2),
        "H1" => Std("Hammocks over mess tables", 1.1, 1.8, 5.0, 0.0, 0.30, 4.0, 0.10, 0.010, 0.0, 0.03, 1.15, 2.3, 0.007, 15L, 0.05, 0.08, 30),
        "H2" => Std("Tiered bunks and separate messdecks", 1.3, 2.5, 6.0, 0.33, 0.30, 5.0, 0.20, 0.010, 0.03, 0.05, 1.20, 2.4, 0.009, 60L,
            0.06, 0.08, 60),
        "H3" => Std("Cold-war bunks with lockers and lounges", 1.9, 3.5, 7.5, 0.30, 0.35, 6.0, 0.35, 0.012, 0.15, 0.08, 1.25, 2.6, 0.010,
            120L, 0.07, 0.08, 120),
        "H4" => Std("Modern small messes", 2.8, 5.0, 9.0, 0.30, 0.40, 8.0, 0.45, 0.015, 0.30, 0.10, 1.28, 2.8, 0.011, 180L, 0.07, 0.08, 180),
        "H5" => Std("Single cabins (merchant)", 5.0, 7.0, 10.0, 0.50, 0.40, 8.0, 0.60, 0.015, 0.50, 0.12, 1.30, 2.8, 0.011, 200L, 0.08, 0.08,
            365),
        _ => throw new PyKeyError(key),
    };

    static PyDict Choices() => PyDict.Of(("endurance_days", null), ("distiller", true), ("buffer_days", 5L), ("water_l_per_day", null),
        ("berth_ratio", 1.0), ("officer_fraction", null));

    /// <summary>The design's crew settings over the defaults (the standard over the style's default standard block).</summary>
    public static PyDict Spec(PyDict design, string defaultStandard = "H2")
    {
        var c = design.DOr("crew");
        var std = PyDict.Merge(Standard(defaultStandard), c.Or("standard", null) as PyDict);
        var choices = Choices();
        var out_ = PyDict.Merge(choices, c.Where(choices.Has));
        out_["standard"] = std;
        if (out_["water_l_per_day"] is null)
            out_["water_l_per_day"] = std["water_l_per_day"];
        return out_;
    }

    public static List<string> Validate(PyDict design, string defaultStandard = "H2")
    {
        var s = Spec(design, defaultStandard);
        var errs = new List<string>();
        foreach (var kv in s.D("standard"))
            if (kv.Key != "name" && !(Py.IsNumber(kv.Value) && Py.ToDouble(kv.Value) >= 0))
                errs.Add($"crew.standard.{kv.Key} must be a number, 0 or more");
        double hf = s.D("standard").F("hotel_fraction");
        if (!(0 <= hf && hf < 0.9))
            errs.Add("crew.standard.hotel_fraction must be 0..0.9");
        if (s["endurance_days"] is not null && !(Py.ToDouble(s["endurance_days"]) > 0))
            errs.Add("crew.endurance_days must be above 0");
        if (!(Py.ToDouble(s["berth_ratio"]) > 0))
            errs.Add("crew.berth_ratio must be above 0");
        return errs;
    }

    /// <summary>Crew of one gun mount, handling rooms included.</summary>
    public static double GunCrew(double calibreMm, double barrels) => barrels * (0.5 + 0.09 * calibreMm) + 0.02 * calibreMm;

    public static double TorpedoCrew(PyDict t) => t.B("fixed_tube") ? 0.5 * t.F("barrels") : 2 + 0.6 * t.F("barrels");

    /// <summary>Deck, command, signals, control and damage control: k x standard displacement^0.5, tapering below 1,000 t.</summary>
    static double DeckCrew(double stdT, double k = 0.8) =>
        k * Math.Pow(Math.Max(stdT, 1.0), 0.5) * Math.Pow(Math.Min(1.0, stdT / 1000.0), 0.3);

    /// <summary>The complement by department: {department: men}, with totals.</summary>
    static PyDict Complement(Layout lay, Navarch.Result res, PyDict? extra, double deckK, object? officerFraction, double hotelFraction)
    {
        double guns = 0.0, torps = 0.0;
        foreach (var m in lay.Mounts)
        {
            var t = m.D("t");
            if (Py.Eq(m["kind"], "torpedo"))
                torps += TorpedoCrew(t);
            else
                guns += GunCrew(t.F("calibre_mm"), t.F("barrels"));
        }
        foreach (var a in lay.Aa)
        {
            var cfg = Geometry.AA_CFG[a.S("type")];
            guns += GunCrew(cfg.CalibreMm, cfg.Barrels);
        }
        var deps = PyDict.Of(("engineering", res.PlantRated.Get("crew", 0L)), ("weapons", (long)Math.Round(guns + torps)),
            ("deck_and_command", (long)Math.Round(DeckCrew(res.Std, deckK))));
        if (extra != null)
            deps.Update(extra);
        long ops = Py.ToLong(Py.SumObj(deps.Values));
        long total = (long)Math.Ceiling(ops / (1 - hotelFraction));
        deps["hotel"] = total - ops;
        double fo = officerFraction is not null ? Py.ToDouble(officerFraction) : (total < 30 ? 0.15 : 0.08);
        return PyDict.Of(("departments", deps), ("total", total), ("officers", Math.Max(1L, (long)Math.Round(fo * total))));
    }

    /// <summary>Volumes the crew needs: living space, provisions, water tankage and the distiller, and comfort inputs.</summary>
    static PyDict Needs(PyDict c, long n, long officers, double hAvail)
    {
        var s = c.D("standard");
        long nc = (long)Math.Round(s.F("cpo_fraction") * n);
        long nr = Math.Max(0, n - officers - nc);
        double b = c.F("berth_ratio");
        double tEnd = c.F("endurance_days");
        double aSleep = nr * b * s.F("sleep_rating_m2") + nc * s.F("sleep_cpo_m2") + officers * s.F("sleep_officer_m2");
        double aMess = s.F("mess_seats_per_man") * (nr + nc) * 1.1 + (s.F("mess_seats_per_man") != 0 ? 0.8 * officers * 1.6 : 0.0);
        double aGalley = Math.Max(s.F("galley_min_m2"), s.F("galley_k") * Math.Pow(n, 0.8));
        double aSan = Math.Max(1.0, s.F("sanitary_m2") * n);
        long beds = n >= 15 && tEnd > 3 ? (long)Math.Ceiling(s.F("sickbay_beds_per_man") * n) : 0;
        double aMed = (beds != 0 ? 6 + 4 * beds : 0) + (n > 1000 && s.F("sickbay_beds_per_man") >= 0.01 ? 40 : 0);
        double aWelfare = s.F("welfare_m2") * n;
        double aServ = s.F("services_k") * Math.Pow(n, 0.85) * (tEnd > 7 ? 1 : 0.5);
        double aNet = aSleep + aMess + aGalley + aSan + aMed + aWelfare + aServ;
        double hEff = Math.Min(s.F("deck_height_m"), hAvail);
        double live = aNet * s.F("passage_factor") * hEff;
        double prov = n * tEnd * s.F("provisions_m3_per_day") * 1.4;
        double wDay = c.F("water_l_per_day");
        bool dist = Py.Truthy(c["distiller"]);
        double water = n * wDay * (dist ? Math.Min(c.F("buffer_days"), tEnd) : tEnd) / 1000.0;
        double qDist = dist ? 1.2 * n * wDay / 1000.0 : 0.0;
        return PyDict.Of(("living_m3", live), ("provisions_m3", prov), ("water_m3", water), ("distiller_m3_per_day", qDist),
            ("distiller_volume_m3", 0.06 * qDist), ("net_area_m2", aNet), ("sleep_m2_per_man", aSleep / Math.Max(1, n)),
            ("headroom_m", hEff), ("sickbay_beds", beds), ("ratings", nr), ("cpos", nc));
    }

    static readonly string[] TAKEN = ["magazine", "boiler_room", "engine_room", "bunker", "hold", "cargo_tank", "fuel_tank", "steering"];

    /// <summary>The ship's empty volume, m3.</summary>
    static PyDict CrewSpace(Layout lay, PyDict design, Navarch.Result res)
    {
        var hull = lay.Hull;
        double L = hull.L, B = hull.B;
        double cb = design.D("hull").F("block_coefficient");
        double D = res.Depth, T = res.Draught;
        double db = Powerplant.DoubleBottom(D);
        var plan = lay.Geo.Plant ?? new PyDict();
        double low = (plan.B("armoured") ? plan.F("top", D) : D) - db;
        double hullV = L * B * (T * cb + Math.Max(0.0, D - T) * Geometry.Cwp(cb)) - db * L * B * Geometry.Cwp(cb) * 0.9;
        object raisedObj = Py.SumObj(lay.Decks.Where(dk => Py.Eq(dk["kind"], "deck"))
            .Select(dk => (object?)(Area(Geometry.Pts(dk["points"])) * (dk.F("top") - dk.F("base")))));
        double raised = Py.ToDouble(raisedObj);
        double taken = 0.0;
        foreach (var c in lay.Compartments)
        {
            if (!TAKEN.Contains(c.S("kind")))
                continue;
            double h = c.Has("top") && c.Has("base") ? c.F("top") - c.F("base")
                : Py.In(c["kind"], "hold", "cargo_tank") ? D - db : low;
            taken += (c.F("x1") - c.F("x0")) * 2 * c.F("half_width") * h;
        }
        var cit = lay.Geo.Citadel;
        if (plan.B("tds") && cit is { } ct)
            taken += 2 * plan.F("tds") * (ct.X1 - ct.X0) * low;
        var rooms = new PyDict();
        foreach (var b in lay.Blocks)
            if (!Py.Eq(b["kind"], "director") && !Py.In(b["role"], "hangar", "director", "casemate"))
                rooms[b.S("id")] = (b.Has("area") ? b.F("area") : (b.F("x1") - b.F("x0")) * b.F("w")) * Layout.LEVEL_H * 0.9;
        double sup = Py.ToDouble(Py.SumObj(rooms.Values));
        double free = Math.Max(0.0, hullV - taken) + raised + sup;
        return PyDict.Of(("hull_m3", hullV + raised), ("taken_m3", taken), ("superstructure_m3", sup), ("free_m3", free),
            ("usable_m3", USABLE * free), ("hull_usable_m3", USABLE * (Math.Max(0.0, hullV - taken) + raised)), ("blocks_m3", rooms));
    }

    /// <summary>n men over rooms by volume ({id: m3}): whole men, the remainders to the largest fractions, {id: men} for
    /// the rooms that get any.</summary>
    public static List<(K Key, long Men)> Spread<K>(long n, IReadOnlyList<(K Key, double V)> vols)
    {
        double total = vols.Select(v => v.V).Sum();
        if (n <= 0 || total <= 0)
            return [];
        var shares = vols.Select(v => n * v.V / total).ToList();
        var men = shares.Select(s => (long)s).ToList();
        long left = n - men.Sum();
        var order = Enumerable.Range(0, shares.Count).OrderBy(k => men[k] - shares[k]).ToList();
        foreach (var k in order.Take((int)Math.Max(0, left)))
            men[k] += 1;
        return Enumerable.Range(0, vols.Count).Where(i => men[i] != 0).Select(i => (vols[i].Key, men[i])).ToList();
    }

    static double Area(IReadOnlyList<Pt> pts)
    {
        int n = pts.Count;
        return Math.Abs(Enumerable.Range(0, n).Select(i => pts[i].X * pts[(i + 1) % n].Y - pts[(i + 1) % n].X * pts[i].Y).Sum()) / 2;
    }

    /// <summary>Double-bottom tankage left for fresh water after the fuel, m3.</summary>
    static double TankRoom(Layout lay, PyDict design, Navarch.Result res)
    {
        var p = res.Plant;
        string fuel = p.D("tech").S("fuel");
        double oil = fuel == "coal" ? 0.0 : res.Fuel * Powerplant.FUELS[fuel].Stowage;
        double D = res.Depth;
        double tank = Powerplant.DoubleBottom(D) * lay.Hull.L * lay.Hull.B * design.D("hull").F("block_coefficient") * 0.6;
        return Math.Max(0.0, tank - oil);
    }

    /// <summary>Crew the laid-out ship: its complement, the volume the crew needs against the volume the ship has, and the
    /// weights of crew, provisions and water. Sets lay.crew.</summary>
    public static void Apply(Layout lay, PyDict design, Navarch.Result res, Style style)
    {
        var c = Spec(design, style.CREW_STANDARD);
        long rangeDays = (long)Math.Ceiling(design.F("range_nm", 6000) / Math.Max(res.CruiseKn, 1.0) / 24.0 - 1e-9);
        if (c["endurance_days"] is null)
            c["endurance_days"] = Math.Max(1L, rangeDays);
        var s = c.D("standard");
        var comp = Complement(lay, res, style.CrewExtra(design), style.CREW_DECK_K, c["officer_fraction"], s.F("hotel_fraction"));
        long n = comp.I("total");
        double hAvail = res.Depth - Powerplant.DoubleBottom(res.Depth);
        var nd = Needs(c, n, comp.I("officers"), hAvail);
        var room = CrewSpace(lay, design, res);
        double waterTanks = TankRoom(lay, design, res);
        double waterFree = Math.Max(0.0, nd.F("water_m3") - waterTanks);
        double need = nd.F("living_m3") + nd.F("provisions_m3") + waterFree + nd.F("distiller_volume_m3");
        if (need > room.F("usable_m3"))
            lay.Fail("length", $"No room for the crew: {n} men need about {need:N0} m3 for quarters, provisions and " +
                               $"water, but the ship has about {room.F("usable_m3"):N0} m3 to spare. Use a lower " +
                               "habitability standard, a shorter endurance, a distiller, or fewer men (guns, power).");
        if (Py.ToDouble(c["endurance_days"]) < rangeDays)
            lay.Warnings.Add($"Provisions for {Py.Str(c["endurance_days"])} days, but the fuel lasts {rangeDays} days at " +
                             "cruising speed.");
        double frac = need > 0 ? Math.Min(1.0, Math.Max(0.0, (need - room.F("hull_usable_m3")) / need)) : 0.0;
        long up = (long)Math.Round(n * frac);
        var blocksM3 = room.D("blocks_m3").Select(kv => (kv.Key, Py.ToDouble(kv.Value))).ToList();
        var upBlocks = Spread(up, blocksM3);
        double xMid = lay.Geo.MachineryMid(lay.Hull.L);
        var byId = new Dictionary<string, PyDict>(StringComparer.Ordinal);
        foreach (var b in lay.Blocks)
            byId[b.S("id")] = b;
        foreach (var (bid, m) in upBlocks)
        {
            var b = byId[bid];
            lay.Weights.Add(new Weight($"Crew and effects ({bid})", "misc", m * CREW_T, (b.F("x0") + b.F("x1")) / 2,
                ZRel.Deck(Layout.BlockBase(b) + 1.3)));
        }
        lay.Weights.Add(new Weight("Crew and effects", "misc", (n - up) * CREW_T, 0.0, ZRel.Deck(-1.5)));
        lay.Weights.Add(new Weight("Provisions", "misc", nd.F("provisions_m3") * PROVISIONS_T_PER_M3, 0.0, ZRel.Frac(0.4)));
        lay.Weights.Add(new Weight("Fresh water", "misc", nd.F("water_m3"), xMid, ZRel.Frac(0.05)));
        lay.Crew = PyDict.Of(("complement", n), ("quartered_in_superstructure", up),
            ("superstructure_quarters", PyDict.Of(upBlocks.Select(t => (t.Key, (object?)t.Men)).ToArray())),
            ("officers", comp["officers"]), ("cpos", nd["cpos"]), ("ratings", nd["ratings"]), ("departments", comp["departments"]),
            ("standard", s.Get("name", "")), ("endurance_days", c["endurance_days"]), ("range_days", rangeDays),
            ("distiller", c["distiller"]), ("berth_ratio", c["berth_ratio"]), ("living_m3", (long)Math.Round(nd.F("living_m3"))),
            ("provisions_m3", (long)Math.Round(nd.F("provisions_m3"))), ("water_m3", (long)Math.Round(nd.F("water_m3"))),
            ("water_in_double_bottom_m3", (long)Math.Round(nd.F("water_m3") - waterFree)),
            ("distiller_m3_per_day", Math.Round(nd.F("distiller_m3_per_day"), 1)), ("space_needed_m3", (long)Math.Round(need)),
            ("space_usable_m3", (long)Math.Round(room.F("usable_m3"))), ("space_free_m3", (long)Math.Round(room.F("free_m3"))),
            ("sleep_m2_per_man", Math.Round(nd.F("sleep_m2_per_man"), 2)), ("sleep_standard_m2", s["sleep_rating_m2"]),
            ("headroom_m", Math.Round(nd.F("headroom_m"), 2)), ("deck_height_m", s["deck_height_m"]), ("sickbay_beds", nd["sickbay_beds"]),
            ("tolerance_days", s["tolerance_days"]));
    }

    const double HANDLING = 0.4;
    static readonly (double A, double B) COMMAND_K = (4, 0.2);
    const double AFT_CONTROL = 0.25;
    const long DIRECTOR_K = 2, STEERING_PARTY = 2;

    /// <summary>Where the complement stands at battle stations: (components {(kind, id): men}, rooms {id: men}, summary).</summary>
    static (List<((string Kind, string Id) Key, long Men)> Comps, OrderedDictionary<string, long> Rooms, PyDict Summary)
        BattleStations(Layout lay, PyDict sub)
    {
        var c = lay.Crew ?? new PyDict();
        var deps = (c.Or("departments", null) as PyDict)?.Copy() ?? new PyDict();
        var onComp = new OrderedDictionary<(string, string), long>();
        var onRoom = new OrderedDictionary<string, long>(StringComparer.Ordinal);
        var summary = new PyDict();
        if (deps.Count == 0)
            return ([], onRoom, summary);
        var barbettes = new HashSet<string>(lay.Mounts.Where(m => Geometry.HasBarbette(m.D("t"))).Select(m => m.S("id")), StringComparer.Ordinal);

        void Put<K>(OrderedDictionary<K, long> where, IEnumerable<(K Key, long Men)> men, string station) where K : notnull
        {
            foreach (var (k, m) in men)
            {
                if (m == 0)
                    continue;
                where[k] = (where.TryGetValue(k, out var v) ? v : 0) + m;
                summary[station] = Py.ToLong(summary.Get(station, 0L)) + m;
            }
        }

        static long Take(long n, long cap) => Math.Min(n, Math.Max(0, cap));

        var need = new List<((string, string) Key, double V)>();
        foreach (var m in lay.Mounts)
        {
            var t = m.D("t");
            need.Add(((m.S("kind"), m.S("id")), Py.Eq(m["kind"], "torpedo") ? TorpedoCrew(t) : GunCrew(t.F("calibre_mm"), t.F("barrels"))));
        }
        foreach (var a in lay.Aa)
        {
            var cfg = Geometry.AA_CFG[a.S("type")];
            need.Add((("aa", a.S("id")), GunCrew(cfg.CalibreMm, cfg.Barrels)));
        }
        foreach (var ((kind, k), men) in Spread(Py.ToLong(deps.Get("weapons", 0L)), need))
        {
            long below = kind != "aa" && barbettes.Contains(k) ? (long)Math.Round(HANDLING * men) : 0;
            Put(onComp, [((kind, k), men - below)], kind == "aa" ? "aa" : kind == "torpedo" ? "torpedoes" : "guns");
            Put(onComp, [(("barbette", $"{k} barbette"), below)], "handling");
        }
        long rest = deps.Where(kv => !(kv.Key is "weapons" or "engineering")).Sum(kv => Py.ToLong(kv.Value));
        var rooms = sub.L("rooms").Cast<PyDict>().ToList();
        var mach = rooms.Where(r => Py.In(r["kind"], "boiler_room", "engine_room") && !r.B("shared")).Select(r => (r.S("id"), r.F("volume_m3"))).ToList();
        if (mach.Count > 0)
            Put(onRoom, Spread(Py.ToLong(deps.Get("engineering", 0L)), mach), "machinery");
        else
            rest += Py.ToLong(deps.Get("engineering", 0L));
        var vol = new List<((string, string) Key, double V)>();
        var roles = new Dictionary<(string, string), string>();
        foreach (var b in lay.Blocks)
        {
            double area = Math.Abs(Geometry.PolygonCentroid(Geometry.BlockOutline(b)).Area);
            var key = ("superstructure", b.S("id"));
            int idx = vol.FindIndex(v => v.Key == key);
            double val = area * (Layout.BlockTop(b) - Layout.BlockBase(b));
            if (idx >= 0)
                vol[idx] = (key, val);
            else
                vol.Add((key, val));
            roles[key] = b.S("role");
        }
        string? Role((string, string) k) => roles.TryGetValue(k, out var r) ? r : null;
        var bridge = vol.Where(v => Role(v.Key) is "bridge" or "island").ToList();
        var aft = vol.Where(v => Role(v.Key) == "aft_control").ToList();
        if (bridge.Count == 0)
            bridge = vol.Where(v => Role(v.Key) != "director").ToList();
        if (bridge.Count > 0)
        {
            long n = Take((long)Math.Round(COMMAND_K.A + COMMAND_K.B * Py.ToDouble(deps.Get("deck_and_command", 0L))), rest);
            long nAft = aft.Count > 0 ? (long)Math.Round(AFT_CONTROL * n) : 0;
            Put(onComp, Spread(n - nAft, bridge), "command");
            Put(onComp, Spread(nAft, aft), "command");
            rest -= n;
        }
        foreach (var d in lay.Directors)
        {
            long n = Take(DIRECTOR_K + (long)Math.Round(d.F("rangefinder_m")), rest);
            Put(onComp, [(("superstructure", d.S("id")), n)], "directors");
            rest -= n;
        }
        var steer = rooms.Where(r => Py.Eq(r["kind"], "steering") && !r.B("shared")).Select(r => r.S("id")).ToList();
        if (steer.Count > 0)
        {
            long n = Take(STEERING_PARTY, rest);
            Put(onRoom, [(steer[0], n)], "steering");
            rest -= n;
        }
        long air = Take(Py.ToLong(deps.Get("air_group", 0L)), rest);
        if (air != 0)
        {
            var bays = lay.Compartments.Where(cc => Py.Eq(cc["kind"], "hangar")).Select(cc => (("hangar_bay", cc.S("id")), 1.0)).ToList();
            if (bays.Count == 0)
                bays = lay.Decks.Where(dk => Py.Eq(dk["kind"], "flight_deck")).Select(dk => (("flight_deck", dk.S("id")), 1.0)).ToList();
            bays = bays.GroupBy(b => b.Item1).Select(g => g.Last()).ToList();
            if (bays.Count > 0)
            {
                Put(onComp, Spread(air, bays), "air");
                rest -= air;
            }
        }
        var free = rooms.Where(r => Py.In(r["kind"], "accommodation", "stores") && !r.B("shared")).Select(r => (r.S("id"), r.F("volume_m3"))).ToList();
        if (free.Count > 0)
            Put(onRoom, Spread(rest, free), "repair");
        else if (vol.Count > 0)
        {
            var nd = vol.Where(v => Role(v.Key) != "director").ToList();
            Put(onComp, Spread(rest, nd.Count > 0 ? nd : vol), "repair");
        }
        return (onComp.Select(kv => (kv.Key, kv.Value)).ToList(), onRoom, summary);
    }

    /// <summary>battle_crew on the rooms and cells where the complement stands at battle stations, and its summary in the
    /// report's crew. Returns the men on the components: {(kind, id): men}, for the hitboxes.</summary>
    public static Dictionary<(string Kind, string Id), long> AssignBattleCrew(Layout lay, PyDict sub)
    {
        var (comps, roomsSt, summary) = BattleStations(lay, sub);
        var cells = new Dictionary<string, PyDict>(StringComparer.Ordinal);
        foreach (PyDict cell in sub.L("cells").Cast<PyDict>())
            cells[cell.S("id")] = cell;
        foreach (PyDict r in sub.L("rooms").Cast<PyDict>())
        {
            if (roomsSt.TryGetValue(r.S("id"), out var men) && men != 0)
            {
                r["battle_crew"] = men;
                var own = r.L("cells").Cast<string>().Where(cid => Py.Eq(cells[cid]["room"], r["id"]))
                    .Select(cid => (cid, cells[cid].F("volume_m3"))).ToList();
                foreach (var (cid, m) in Spread(men, own))
                    cells[cid]["battle_crew"] = m;
            }
        }
        if (lay.Crew != null && summary.Count > 0)
            lay.Crew["battle_stations"] = summary;
        return comps.ToDictionary(t => t.Key, t => t.Men);
    }
}
