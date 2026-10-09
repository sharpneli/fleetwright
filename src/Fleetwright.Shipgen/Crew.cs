namespace Fleetwright.Shipgen;

/// <summary>A habitability standard: berth and mess space per man, galley, sanitary, sickbay, welfare and service space,
/// how much passages add, the deck height, provisions and water per day, the hotel and CPO shares, and how many days at
/// sea the crew tolerates it.</summary>
public sealed record CrewStandard(string Name, double SleepRatingM2, double SleepCpoM2, double SleepOfficerM2,
    double MessSeatsPerMan, double GalleyK, double GalleyMinM2, double SanitaryM2, double SickbayBedsPerMan, double WelfareM2,
    double ServicesK, double PassageFactor, double DeckHeightM, double ProvisionsM3PerDay, double WaterLPerDay,
    double HotelFraction, double CpoFraction, double ToleranceDays)
{
    /// <summary>The numbers by their JSON key, for validation.</summary>
    public IEnumerable<(string Key, double Value)> Numbers() =>
    [
        ("sleep_rating_m2", SleepRatingM2), ("sleep_cpo_m2", SleepCpoM2), ("sleep_officer_m2", SleepOfficerM2),
        ("mess_seats_per_man", MessSeatsPerMan), ("galley_k", GalleyK), ("galley_min_m2", GalleyMinM2), ("sanitary_m2", SanitaryM2),
        ("sickbay_beds_per_man", SickbayBedsPerMan), ("welfare_m2", WelfareM2), ("services_k", ServicesK),
        ("passage_factor", PassageFactor), ("deck_height_m", DeckHeightM), ("provisions_m3_per_day", ProvisionsM3PerDay),
        ("water_l_per_day", WaterLPerDay), ("hotel_fraction", HotelFraction), ("cpo_fraction", CpoFraction),
        ("tolerance_days", ToleranceDays),
    ];

    /// <summary>The design's standard over this one, key by key.</summary>
    public CrewStandard Over(CrewStandardInput? s) => s is null ? this : new(s.Name ?? Name, s.SleepRatingM2 ?? SleepRatingM2,
        s.SleepCpoM2 ?? SleepCpoM2, s.SleepOfficerM2 ?? SleepOfficerM2, s.MessSeatsPerMan ?? MessSeatsPerMan, s.GalleyK ?? GalleyK,
        s.GalleyMinM2 ?? GalleyMinM2, s.SanitaryM2 ?? SanitaryM2, s.SickbayBedsPerMan ?? SickbayBedsPerMan, s.WelfareM2 ?? WelfareM2,
        s.ServicesK ?? ServicesK, s.PassageFactor ?? PassageFactor, s.DeckHeightM ?? DeckHeightM,
        s.ProvisionsM3PerDay ?? ProvisionsM3PerDay, s.WaterLPerDay ?? WaterLPerDay, s.HotelFraction ?? HotelFraction,
        s.CpoFraction ?? CpoFraction, s.ToleranceDays ?? ToleranceDays);
}

/// <summary>The design's crew settings, defaults filled in (EnduranceDays and OfficerFraction may still be left to the
/// designer).</summary>
public sealed record CrewSpec(double? EnduranceDays, bool Distiller, double BufferDays, double WaterLPerDay, double BerthRatio,
    double? OfficerFraction, CrewStandard Standard);

/// <summary>The ship's crew for the report: the complement, where it lives and the space it needs.</summary>
public sealed class CrewReport
{
    public long Complement { get; set; }
    public long QuarteredInSuperstructure { get; set; }
    public OrderedDictionary<string, long> SuperstructureQuarters { get; set; } = new(StringComparer.Ordinal);
    public long Officers { get; set; }
    public long Cpos { get; set; }
    public long Ratings { get; set; }
    public OrderedDictionary<string, long> Departments { get; set; } = new(StringComparer.Ordinal);
    public string Standard { get; set; } = "";
    public double EnduranceDays { get; set; }
    public long RangeDays { get; set; }
    public bool Distiller { get; set; }
    public double BerthRatio { get; set; }
    public double LivingM3 { get; set; }
    public double ProvisionsM3 { get; set; }
    public double WaterM3 { get; set; }
    public double WaterInDoubleBottomM3 { get; set; }
    public double DistillerM3PerDay { get; set; }
    public double SpaceNeededM3 { get; set; }
    public double SpaceUsableM3 { get; set; }
    public double SpaceFreeM3 { get; set; }
    public double SleepM2PerMan { get; set; }
    public double SleepStandardM2 { get; set; }
    public double HeadroomM { get; set; }
    public double DeckHeightM { get; set; }
    public long SickbayBeds { get; set; }
    public double ToleranceDays { get; set; }
    /// <summary>Where the complement stands at battle stations, by station.</summary>
    public OrderedDictionary<string, long>? BattleStations { get; set; }
}

/// <summary>The complement and the volume it needs (research/crew-space-model.md), and battle stations.</summary>
public static class Crew
{
    const double Usable = 0.65, CrewT = 0.12, ProvisionsTPerM3 = 0.6;

    static readonly Dictionary<string, CrewStandard> Standards = new(StringComparer.Ordinal)
    {
        ["H0"] = new("Sleep at station (MTB, PT boat)", 0.6, 0.8, 1.5, 0.0, 0.25, 1.0, 0.06, 0.0, 0.0, 0.0, 1.10, 1.9, 0.006, 8, 0.0, 0.0, 2),
        ["H1"] = new("Hammocks over mess tables", 1.1, 1.8, 5.0, 0.0, 0.30, 4.0, 0.10, 0.010, 0.0, 0.03, 1.15, 2.3, 0.007, 15, 0.05, 0.08, 30),
        ["H2"] = new("Tiered bunks and separate messdecks", 1.3, 2.5, 6.0, 0.33, 0.30, 5.0, 0.20, 0.010, 0.03, 0.05, 1.20, 2.4, 0.009, 60,
            0.06, 0.08, 60),
        ["H3"] = new("Cold-war bunks with lockers and lounges", 1.9, 3.5, 7.5, 0.30, 0.35, 6.0, 0.35, 0.012, 0.15, 0.08, 1.25, 2.6, 0.010,
            120, 0.07, 0.08, 120),
        ["H4"] = new("Modern small messes", 2.8, 5.0, 9.0, 0.30, 0.40, 8.0, 0.45, 0.015, 0.30, 0.10, 1.28, 2.8, 0.011, 180, 0.07, 0.08, 180),
        ["H5"] = new("Single cabins (merchant)", 5.0, 7.0, 10.0, 0.50, 0.40, 8.0, 0.60, 0.015, 0.50, 0.12, 1.30, 2.8, 0.011, 200, 0.08, 0.08,
            365),
    };

    /// <summary>The design's crew settings over the defaults (its standard over the style's default standard).</summary>
    public static CrewSpec Spec(Design design, string defaultStandard = "H2")
    {
        var c = design.Crew;
        var std = Standards[defaultStandard].Over(c?.Standard);
        return new CrewSpec(c?.EnduranceDays, c?.Distiller ?? true, c?.BufferDays ?? 5, c?.WaterLPerDay ?? std.WaterLPerDay,
            c?.BerthRatio ?? 1.0, c?.OfficerFraction, std);
    }

    public static List<string> Validate(Design design, string defaultStandard = "H2")
    {
        var s = Spec(design, defaultStandard);
        var errs = s.Standard.Numbers().Where(kv => !(kv.Value >= 0)).Select(kv => $"crew.standard.{kv.Key} must be a number, 0 or more").ToList();
        double hf = s.Standard.HotelFraction;
        if (!(0 <= hf && hf < 0.9))
            errs.Add("crew.standard.hotel_fraction must be 0..0.9");
        if (s.EnduranceDays is double e && !(e > 0))
            errs.Add("crew.endurance_days must be above 0");
        if (!(s.BerthRatio > 0))
            errs.Add("crew.berth_ratio must be above 0");
        return errs;
    }

    /// <summary>Crew of one gun mount, handling rooms included.</summary>
    public static double GunCrew(double calibreMm, double barrels) => barrels * (0.5 + 0.09 * calibreMm) + 0.02 * calibreMm;

    public static double TorpedoCrew(TurretType t) => t.IsFixedTube ? 0.5 * t.Barrels : 2 + 0.6 * t.Barrels;

    /// <summary>Deck, command, signals, control and damage control: k x standard displacement^0.5, tapering below 1,000 t.</summary>
    static double DeckCrew(double stdT, double k = 0.8) =>
        k * Math.Pow(Math.Max(stdT, 1.0), 0.5) * Math.Pow(Math.Min(1.0, stdT / 1000.0), 0.3);

    /// <summary>The complement by department (with the hotel staff), the total and the officers.</summary>
    static (OrderedDictionary<string, long> Departments, long Total, long Officers) Complement(Layout lay, Navarch.Result res,
        List<(string Name, long Men)> extra, double deckK, double? officerFraction, double hotelFraction)
    {
        double guns = 0.0, torps = 0.0;
        foreach (var m in lay.Mounts)
        {
            if (m.Kind == "torpedo")
                torps += TorpedoCrew(m.T);
            else
                guns += GunCrew(m.T.CalibreMm, m.T.Barrels);
        }
        foreach (var a in lay.Aa)
        {
            var cfg = Geometry.AaCfg[a.Type];
            guns += GunCrew(cfg.CalibreMm, cfg.Barrels);
        }
        var deps = new OrderedDictionary<string, long>(StringComparer.Ordinal)
        {
            ["engineering"] = res.PlantRated.Crew, ["weapons"] = (long)Math.Round(guns + torps),
            ["deck_and_command"] = (long)Math.Round(DeckCrew(res.Std, deckK)),
        };
        foreach (var (name, men) in extra)
            deps[name] = men;
        long ops = deps.Values.Sum();
        long total = (long)Math.Ceiling(ops / (1 - hotelFraction));
        deps["hotel"] = total - ops;
        double fo = officerFraction ?? (total < 30 ? 0.15 : 0.08);
        return (deps, total, Math.Max(1L, (long)Math.Round(fo * total)));
    }

    /// <summary>What the crew needs: living space, provisions, water tankage and the distiller, and comfort figures.</summary>
    sealed record Needs(double LivingM3, double ProvisionsM3, double WaterM3, double DistillerM3PerDay, double DistillerVolumeM3,
        double NetAreaM2, double SleepM2PerMan, double HeadroomM, long SickbayBeds, long Ratings, long Cpos);

    static Needs NeedsOf(CrewSpec c, double enduranceDays, long n, long officers, double hAvail)
    {
        var s = c.Standard;
        long nc = (long)Math.Round(s.CpoFraction * n);
        long nr = Math.Max(0, n - officers - nc);
        double b = c.BerthRatio;
        double tEnd = enduranceDays;
        double aSleep = nr * b * s.SleepRatingM2 + nc * s.SleepCpoM2 + officers * s.SleepOfficerM2;
        double aMess = s.MessSeatsPerMan * (nr + nc) * 1.1 + (s.MessSeatsPerMan != 0 ? 0.8 * officers * 1.6 : 0.0);
        double aGalley = Math.Max(s.GalleyMinM2, s.GalleyK * Math.Pow(n, 0.8));
        double aSan = Math.Max(1.0, s.SanitaryM2 * n);
        long beds = n >= 15 && tEnd > 3 ? (long)Math.Ceiling(s.SickbayBedsPerMan * n) : 0;
        double aMed = (beds != 0 ? 6 + 4 * beds : 0) + (n > 1000 && s.SickbayBedsPerMan >= 0.01 ? 40 : 0);
        double aWelfare = s.WelfareM2 * n;
        double aServ = s.ServicesK * Math.Pow(n, 0.85) * (tEnd > 7 ? 1 : 0.5);
        double aNet = aSleep + aMess + aGalley + aSan + aMed + aWelfare + aServ;
        double hEff = Math.Min(s.DeckHeightM, hAvail);
        double live = aNet * s.PassageFactor * hEff;
        double prov = n * tEnd * s.ProvisionsM3PerDay * 1.4;
        double wDay = c.WaterLPerDay;
        double water = n * wDay * (c.Distiller ? Math.Min(c.BufferDays, tEnd) : tEnd) / 1000.0;
        double qDist = c.Distiller ? 1.2 * n * wDay / 1000.0 : 0.0;
        return new Needs(live, prov, water, qDist, 0.06 * qDist, aNet, aSleep / Math.Max(1, n), hEff, beds, nr, nc);
    }

    static readonly string[] Taken = ["magazine", "boiler_room", "engine_room", "bunker", "hold", "cargo_tank", "fuel_tank", "steering"];

    /// <summary>The ship's volume and what of it is free for the crew, m3; the superstructure's by block.</summary>
    sealed record Space(double HullM3, double TakenM3, double SuperstructureM3, double FreeM3, double UsableM3, double HullUsableM3,
        List<(string Id, double M3)> BlocksM3);

    static Space SpaceOf(Layout lay, Design design, Navarch.Result res)
    {
        var hull = lay.Hull;
        double L = hull.L, B = hull.B;
        double cb = design.BlockCoefficient;
        double D = res.Depth, T = res.Draught;
        double db = Powerplant.DoubleBottom(D);
        var plan = lay.Geo.Plant;
        double low = (plan is { Armoured: true } ? plan.Top : D) - db;
        double hullV = L * B * (T * cb + Math.Max(0.0, D - T) * Geometry.Cwp(cb)) - db * L * B * Geometry.Cwp(cb) * 0.9;
        double raised = lay.Decks.Where(dk => dk.Kind == "deck").Sum(dk => Geometry.PolygonArea(dk.Points) * (dk.Top - dk.Base));
        double taken = 0.0;
        foreach (var c in lay.Compartments)
        {
            if (!Taken.Contains(c.Kind))
                continue;
            double h = c.Top is double top && c.Base is double bse ? top - bse : c.Kind is "hold" or "cargo_tank" ? D - db : low;
            taken += (c.X1 - c.X0) * 2 * c.HalfWidth * h;
        }
        var cit = lay.Geo.Citadel;
        if (plan is { Tds: > 0 } && cit is { } ct)
            taken += 2 * plan.Tds * (ct.X1 - ct.X0) * low;
        var rooms = new OrderedDictionary<string, double>(StringComparer.Ordinal);
        foreach (var b in lay.Blocks)
            if (b.Kind != "director" && b.Role is not ("hangar" or "director" or "casemate"))
                rooms[b.Id] = (b.Area ?? (b.X1 - b.X0) * b.W) * Layout.LevelH * 0.9;
        double sup = rooms.Values.Sum();
        double free = Math.Max(0.0, hullV - taken) + raised + sup;
        return new Space(hullV + raised, taken, sup, free, Usable * free, Usable * (Math.Max(0.0, hullV - taken) + raised),
            rooms.Select(kv => (kv.Key, kv.Value)).ToList());
    }

    /// <summary>n men over rooms by volume: whole men, the remainders to the largest fractions, (key, men) for the rooms
    /// that get any.</summary>
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

    /// <summary>Double-bottom tankage left for fresh water after the fuel, m3.</summary>
    static double TankRoom(Layout lay, Design design, Navarch.Result res)
    {
        string fuel = res.Plant.Tech.Fuel;
        double oil = fuel == "coal" ? 0.0 : res.Fuel * Powerplant.Fuels[fuel].Stowage;
        double tank = Powerplant.DoubleBottom(res.Depth) * lay.Hull.L * lay.Hull.B * design.BlockCoefficient * 0.6;
        return Math.Max(0.0, tank - oil);
    }

    /// <summary>Crew the laid-out ship: its complement, the volume the crew needs against the volume the ship has, and the
    /// weights of crew, provisions and water. Sets lay.Crew.</summary>
    public static void Apply(Layout lay, Design design, Navarch.Result res, Style style)
    {
        var c = Spec(design, style.CrewStandard);
        long rangeDays = (long)Math.Ceiling((design.RangeNm ?? 6000) / Math.Max(res.CruiseKn, 1.0) / 24.0 - 1e-9);
        double endurance = c.EnduranceDays ?? Math.Max(1L, rangeDays);
        var s = c.Standard;
        var (deps, n, officers) = Complement(lay, res, style.CrewExtra(design), style.CrewDeckK, c.OfficerFraction, s.HotelFraction);
        double hAvail = res.Depth - Powerplant.DoubleBottom(res.Depth);
        var nd = NeedsOf(c, endurance, n, officers, hAvail);
        var room = SpaceOf(lay, design, res);
        double waterTanks = TankRoom(lay, design, res);
        double waterFree = Math.Max(0.0, nd.WaterM3 - waterTanks);
        double need = nd.LivingM3 + nd.ProvisionsM3 + waterFree + nd.DistillerVolumeM3;
        if (need > room.UsableM3)
            lay.Fail("length", $"No room for the crew: {n} men need about {need:N0} m3 for quarters, provisions and " +
                               $"water, but the ship has about {room.UsableM3:N0} m3 to spare. Use a lower " +
                               "habitability standard, a shorter endurance, a distiller, or fewer men (guns, power).");
        if (endurance < rangeDays)
            lay.Warnings.Add($"Provisions for {endurance} days, but the fuel lasts {rangeDays} days at cruising speed.");
        double frac = need > 0 ? Math.Min(1.0, Math.Max(0.0, (need - room.HullUsableM3) / need)) : 0.0;
        long up = (long)Math.Round(n * frac);
        var upBlocks = Spread(up, room.BlocksM3);
        double xMid = lay.Geo.MachineryMid(lay.Hull.L);
        var byId = new Dictionary<string, Block>(StringComparer.Ordinal);
        foreach (var b in lay.Blocks)
            byId[b.Id] = b;
        foreach (var (bid, m) in upBlocks)
        {
            var b = byId[bid];
            lay.Weights.Add(new Weight($"Crew and effects ({bid})", "misc", m * CrewT, (b.X0 + b.X1) / 2, ZRel.Deck(b.Base + 1.3)));
        }
        lay.Weights.Add(new Weight("Crew and effects", "misc", (n - up) * CrewT, 0.0, ZRel.Deck(-1.5)));
        lay.Weights.Add(new Weight("Provisions", "misc", nd.ProvisionsM3 * ProvisionsTPerM3, 0.0, ZRel.Frac(0.4)));
        lay.Weights.Add(new Weight("Fresh water", "misc", nd.WaterM3, xMid, ZRel.Frac(0.05)));
        var quarters = new OrderedDictionary<string, long>(StringComparer.Ordinal);
        foreach (var (bid, m) in upBlocks)
            quarters[bid] = m;
        lay.Crew = new CrewReport
        {
            Complement = n, QuarteredInSuperstructure = up, SuperstructureQuarters = quarters, Officers = officers, Cpos = nd.Cpos,
            Ratings = nd.Ratings, Departments = deps, Standard = s.Name, EnduranceDays = endurance, RangeDays = rangeDays,
            Distiller = c.Distiller, BerthRatio = c.BerthRatio, LivingM3 = Math.Round(nd.LivingM3), ProvisionsM3 = Math.Round(nd.ProvisionsM3),
            WaterM3 = Math.Round(nd.WaterM3), WaterInDoubleBottomM3 = Math.Round(nd.WaterM3 - waterFree),
            DistillerM3PerDay = Math.Round(nd.DistillerM3PerDay, 1), SpaceNeededM3 = Math.Round(need), SpaceUsableM3 = Math.Round(room.UsableM3),
            SpaceFreeM3 = Math.Round(room.FreeM3), SleepM2PerMan = Math.Round(nd.SleepM2PerMan, 2), SleepStandardM2 = s.SleepRatingM2,
            HeadroomM = Math.Round(nd.HeadroomM, 2), DeckHeightM = s.DeckHeightM, SickbayBeds = nd.SickbayBeds, ToleranceDays = s.ToleranceDays,
        };
    }

    const double Handling = 0.4;
    static readonly (double A, double B) CommandK = (4, 0.2);
    const double AftControl = 0.25;
    const long DirectorK = 2, SteeringParty = 2;

    /// <summary>Where the complement stands at battle stations: (components {(kind, id): men}, rooms {id: men}, summary).</summary>
    static (List<((string Kind, string Id) Key, long Men)> Comps, OrderedDictionary<string, long> Rooms, OrderedDictionary<string, long> Summary)
        BattleStations(Layout lay, SubdivisionData sub)
    {
        var deps = lay.Crew?.Departments ?? new OrderedDictionary<string, long>();
        var onComp = new OrderedDictionary<(string, string), long>();
        var onRoom = new OrderedDictionary<string, long>(StringComparer.Ordinal);
        var summary = new OrderedDictionary<string, long>(StringComparer.Ordinal);
        if (deps.Count == 0)
            return ([], onRoom, summary);
        long Dep(string k) => deps.TryGetValue(k, out var v) ? v : 0;
        var barbettes = new HashSet<string>(lay.Mounts.Where(m => m.T.HasBarbette).Select(m => m.Id), StringComparer.Ordinal);

        void Put<K>(OrderedDictionary<K, long> where, IEnumerable<(K Key, long Men)> men, string station) where K : notnull
        {
            foreach (var (k, m) in men)
            {
                if (m == 0)
                    continue;
                where[k] = (where.TryGetValue(k, out var v) ? v : 0) + m;
                summary[station] = (summary.TryGetValue(station, out var sv) ? sv : 0) + m;
            }
        }

        static long Take(long n, long cap) => Math.Min(n, Math.Max(0, cap));

        var need = new List<((string, string) Key, double V)>();
        foreach (var m in lay.Mounts)
            need.Add(((m.Kind, m.Id), m.Kind == "torpedo" ? TorpedoCrew(m.T) : GunCrew(m.T.CalibreMm, m.T.Barrels)));
        foreach (var a in lay.Aa)
        {
            var cfg = Geometry.AaCfg[a.Type];
            need.Add((("aa", a.Id), GunCrew(cfg.CalibreMm, cfg.Barrels)));
        }
        foreach (var ((kind, k), men) in Spread(Dep("weapons"), need))
        {
            long below = kind != "aa" && barbettes.Contains(k) ? (long)Math.Round(Handling * men) : 0;
            Put(onComp, [((kind, k), men - below)], kind == "aa" ? "aa" : kind == "torpedo" ? "torpedoes" : "guns");
            Put(onComp, [(("barbette", $"{k} barbette"), below)], "handling");
        }
        long rest = deps.Where(kv => !(kv.Key is "weapons" or "engineering")).Sum(kv => kv.Value);
        var rooms = sub.Rooms.Where(r => r.Shared != true).ToList();
        var mach = rooms.Where(r => r.Kind is "boiler_room" or "engine_room").Select(r => (r.Id, r.VolumeM3)).ToList();
        if (mach.Count > 0)
            Put(onRoom, Spread(Dep("engineering"), mach), "machinery");
        else
            rest += Dep("engineering");
        var vol = new List<((string, string) Key, double V)>();
        var roles = new Dictionary<(string, string), string>();
        foreach (var b in lay.Blocks)
        {
            double area = Math.Abs(Geometry.PolygonCentroid(Geometry.BlockOutline(b)).Area);
            var key = ("superstructure", b.Id);
            int idx = vol.FindIndex(v => v.Key == key);
            double val = area * (b.TopZ - b.Base);
            if (idx >= 0)
                vol[idx] = (key, val);
            else
                vol.Add((key, val));
            roles[key] = b.Role;
        }
        string? Role((string, string) k) => roles.TryGetValue(k, out var r) ? r : null;
        var bridge = vol.Where(v => Role(v.Key) is "bridge" or "island").ToList();
        var aft = vol.Where(v => Role(v.Key) == "aft_control").ToList();
        if (bridge.Count == 0)
            bridge = vol.Where(v => Role(v.Key) != "director").ToList();
        if (bridge.Count > 0)
        {
            long n = Take((long)Math.Round(CommandK.A + CommandK.B * Dep("deck_and_command")), rest);
            long nAft = aft.Count > 0 ? (long)Math.Round(AftControl * n) : 0;
            Put(onComp, Spread(n - nAft, bridge), "command");
            Put(onComp, Spread(nAft, aft), "command");
            rest -= n;
        }
        foreach (var d in lay.Directors)
        {
            long n = Take(DirectorK + (long)Math.Round(d.Spec.RangefinderM), rest);
            Put(onComp, [(("superstructure", d.Id), n)], "directors");
            rest -= n;
        }
        var steer = rooms.Where(r => r.Kind == "steering").Select(r => r.Id).ToList();
        if (steer.Count > 0)
        {
            long n = Take(SteeringParty, rest);
            Put(onRoom, [(steer[0], n)], "steering");
            rest -= n;
        }
        long air = Take(Dep("air_group"), rest);
        if (air != 0)
        {
            var bays = lay.Compartments.Where(cc => cc.Kind == "hangar").Select(cc => (("hangar_bay", cc.Id), 1.0)).ToList();
            if (bays.Count == 0)
                bays = lay.Decks.Where(dk => dk.Kind == "flight_deck").Select(dk => (("flight_deck", dk.Id), 1.0)).ToList();
            bays = bays.GroupBy(b => b.Item1).Select(g => g.Last()).ToList();
            if (bays.Count > 0)
            {
                Put(onComp, Spread(air, bays), "air");
                rest -= air;
            }
        }
        var free = rooms.Where(r => r.Kind is "accommodation" or "stores").Select(r => (r.Id, r.VolumeM3)).ToList();
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
    public static Dictionary<(string Kind, string Id), long> AssignBattleCrew(Layout lay, SubdivisionData sub)
    {
        var (comps, roomsSt, summary) = BattleStations(lay, sub);
        var cells = sub.Cells.ToDictionary(c => c.Id, StringComparer.Ordinal);
        foreach (var r in sub.Rooms)
        {
            if (!roomsSt.TryGetValue(r.Id, out var men) || men == 0)
                continue;
            r.BattleCrew = men;
            var own = r.Cells.Where(cid => cells[cid].Room == r.Id).Select(cid => (cid, cells[cid].VolumeM3)).ToList();
            foreach (var (cid, m) in Spread(men, own))
                cells[cid].BattleCrew = m;
        }
        if (lay.Crew != null && summary.Count > 0)
            lay.Crew.BattleStations = summary;
        return comps.ToDictionary(t => t.Key, t => t.Men);
    }
}
