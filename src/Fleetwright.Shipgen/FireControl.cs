namespace Fleetwright.Shipgen;

/// <summary>One battery's fire control: how many directors, the rangefinder's base, the hood's armour, and the radar's
/// weight.</summary>
public sealed record DirectorSpec(long Directors, double RangefinderM, double ArmourMm, double RadarT);

/// <summary>A placed director: its battery, where it stands (base and top above the main deck, and its eye), its
/// settings, its weight, and (secondary and AA directors) the unit of a pair it belongs to.</summary>
public sealed record Director(string Id, string Battery, double X, double Y, double Base, double Top, double Eye, DirectorSpec Spec,
    double WeightT, long? Unit);

/// <summary>firecontrol: directors, the plotting rooms that turn their readings into gun orders, and the search radar.</summary>
public static class FireControl
{
    static readonly string[] BatteryNames = ["main", "secondary", "aa"];
    static readonly string[] Fields = ["directors", "rangefinder_m", "armour_mm", "radar_t", "fit"];
    static readonly Dictionary<string, string> Label = new() { ["main"] = "Main director", ["secondary"] = "Secondary director", ["aa"] = "AA director" };
    public const double HoodH = 2.2;
    const double EyeH = 1.5, HoodPlateT = 0.047, RfTK = 0.03;
    static readonly (double A, double B) GearT = (1.0, 1.2);
    const double MainSpread = 0.25, Refraction = 1.17;

    /// <summary>The design's fire control for a battery, every field (0 where it gives none).</summary>
    public static DirectorSpec Spec(Design design, string battery)
    {
        var d = design.FireControl?.Of(battery);
        return new DirectorSpec((long)(d?.Directors ?? 0), d?.RangefinderM ?? 0, d?.ArmourMm ?? 0, d?.RadarT ?? 0);
    }

    public static List<string> Validate(Design design)
    {
        if (design.FireControl is not { } fc)
            return [];
        var errs = fc.Extra.KeysOrEmpty().Select(k => $"fire_control.{k}: not a battery ({string.Join(", ", BatteryNames)}), search_radar_t, " +
            "aft_rangefinder_m or conning_tower_rangefinder_m").ToList();
        foreach (var (k, v) in new[] { ("aft_rangefinder_m", fc.AftRangefinderM), ("conning_tower_rangefinder_m", fc.ConningTowerRangefinderM) })
            if (v is double d && !(d >= 0 && double.IsFinite(d)))
                errs.Add($"fire_control.{k} must be a number, 0 or more");
        foreach (var b in BatteryNames)
        {
            if (fc.Of(b) is not { } d)
                continue;
            errs.AddRange(d.Extra.KeysOrEmpty().Select(k => $"fire_control.{b}.{k}: not a director setting ({string.Join(", ", Fields)})"));
            var given = new[] { d.Directors, d.RangefinderM, d.ArmourMm, d.RadarT };
            errs.AddRange(Fields.Take(given.Length).Where((_, i) => given[i] < 0).Select(k => $"fire_control.{b}.{k} must be a number, 0 or more"));
            if (d.Directors is double n && n != Math.Floor(n))
                errs.Add($"fire_control.{b}.directors: use a whole number");
        }
        if (fc.SearchRadarT < 0)
            errs.Add("fire_control.search_radar_t must be a number, 0 or more");
        return errs;
    }

    /// <summary>Half a director's hood's diagonal (its rangefinder's arms reach out past it, as Geometry.DirectorParts draws
    /// the hood): the radius a top needs to hold it.</summary>
    public static double HalfDiagonal(DirectorSpec d)
    {
        var (l, w) = Size(d);
        return double.Hypot(l, Math.Min(w, l * 1.15)) / 2;
    }

    /// <summary>How many of a battery's directors stand in masts' tops (masts[i].director): no more than it has; a top
    /// asking for more stands empty.</summary>
    static long InTops(Design design, string battery) =>
        Math.Min(design.Masts?.Count(m => m?.Director == battery && m.Kind != "none") ?? 0, Spec(design, battery).Directors);

    /// <summary>A director's footprint: (fore-and-aft, athwartships) m.</summary>
    static (double L, double W) Size(DirectorSpec d)
    {
        double bse = d.RangefinderM;
        return (0.25 * bse + 1.8, Math.Max(bse + 1.0, 1.8));
    }

    /// <summary>One director's weights, t: the hood, the rangefinder and its gear, the armour and the radar.</summary>
    static (double Hood, double Gear, double Rangefinder, double Armour, double Radar) Weights(DirectorSpec d)
    {
        double bse = d.RangefinderM;
        var (l, w) = Size(d);
        double area = 2 * (l + w) * HoodH + l * w;
        return (area * HoodPlateT, GearT.A + GearT.B * bse, RfTK * Math.Pow(bse, 2), area * d.ArmourMm / 1000.0 * Weight.Steel, d.RadarT);
    }

    public static double HorizonKm(double eyeM) => 3.57 * Math.Sqrt(Refraction * Math.Max(0.0, eyeM));

    static readonly string[] ControlRoles = ["bridge", "director", "aft_control"];

    /// <summary>Control positions in a funnel's smoke: sets lay.smoke and warns about each.</summary>
    public static void AssignSmoke(Layout lay, Navarch.Result res)
    {
        lay.Smoke = new OrderedDictionary<string, List<string>>(StringComparer.Ordinal);
        if (lay.Funnels.Count == 0)
            return;
        double reach = Powerplant.SmokeReach(res.Plant, res.PowerShp);
        foreach (var b in lay.Blocks)
        {
            if (!ControlRoles.Contains(b.Role) || b.Office)
                continue;
            var hit = SmokeFrom(lay.Funnels, lay.FunTop, reach, b.X1, b.TopZ, b.Y, b.W);
            if (hit.Count > 0)
            {
                lay.Smoke[b.Id] = hit;
                lay.Warnings.Add($"{b.Id} stands in the smoke of {string.Join(", ", hit)}: poor visibility from it.");
            }
        }
    }

    /// <summary>Ids of the funnels whose smoke blinds a control position.</summary>
    static List<string> SmokeFrom(IEnumerable<Funnel> funnels, double funTop, double reach, double x1, double top, double y, double w)
    {
        var hit = new List<string>();
        foreach (var f in funnels)
        {
            double d = (f.X - f.L / 2) - x1;
            if (0 <= d && d < reach && top < funTop + 0.3 * d && Math.Abs(f.Y - y) < w / 2 + f.W)
                hit.Add(f.Id);
        }
        return hit;
    }

    /// <summary>Stand the design's directors on the superstructure's roofs as blocks of their own, with their weights.</summary>
    public static void Place(Layout lay, Design design, List<Block> blocks)
    {
        double L = lay.Hull.L;
        // the planned funnels' highest top (LINQ's Max: leading NaNs skipped), 0 with none
        double funTop = 0.0;
        for (int i = 0; i < lay.FunnelsPlanned.Count; i++)
        {
            double t = lay.FunnelsPlanned[i].Top;
            if (i == 0 || double.IsNaN(funTop) || t > funTop)
                funTop = t;
        }
        using var _s = Scratch<(double X, double Y, double Z0, bool Pair)>.Rent(out var spots);
        using var _m = Scratch<((double, double) K, int I)>.Rent(out var mainKeys);
        using var _k = Scratch<((double, int, int, double) K, int I)>.Rent(out var keys);
        foreach (var bat in BatteryNames)
        {
            var d = Spec(design, bat);
            long n = d.Directors - InTops(design, bat);   // those in tops stand there once the masts are up (PlaceInTops)
            if (n <= 0)
                continue;
            var (l, w) = Size(d);
            double hl = l / 2, hw = w / 2;
            var wt = Weights(d);
            var mine = new List<Director>();

            bool Ok(double x, double y, double z0)
            {
                var fp = Footprint.Rect(x - hl, y - hw, x + hl, y + hw);
                return lay.FreeAt(fp, z0, z0 + HoodH, 0.2) && lay.Clear(fp, z0 + HoodH);
            }

            void Put(double x, double y, double z0, bool pair = false, long? unit = null)
            {
                int k = mine.Count;
                string bid = bat == "main" ? Label[bat] + (k == 0 ? "" : $" {k + 1}")
                    : $"{Label[bat]} {(unit is null ? "None" : unit.ToString())}" + (pair ? (y > 0 ? "S" : "P") : "");
                mine.Add(PutDirector(lay, blocks, bat, d, bid, x, y, z0, unit));
            }

            bool Smoky(double x, double y, double z0) =>
                AnySmoke(lay.FunnelsPlanned, funTop, lay.Geo.SmokeReach, x + hl, z0 + Layout.LevelH, y, w);

            bool Spaced(double x, double spread)
            {
                foreach (var m in mine)
                    if (Math.Abs(x - m.X) < Math.Max(mine.Count < 2 ? spread : 0.0, l + 0.4))
                        return false;
                return true;
            }

            for (int src = 0; src < 2 && mine.Count < n; src++)
            {
                spots.Clear();
                Layout.RoofSpots(src == 0 ? blocks : RaisedRoofs(lay), l, w, spots);
                if (bat == "main")
                {
                    // the single spots, highest and then furthest forward first
                    mainKeys.Clear();
                    for (int i = 0; i < spots.Count; i++)
                        if (!spots[i].Pair)
                            mainKeys.Add(((-spots[i].Z0, -spots[i].X), i));
                    KeyedSort.Sort(mainKeys);
                    for (int s = 0; s < 2; s++)
                    {
                        double spread = s == 0 ? MainSpread * L : 0.0;
                        foreach (var (_, i) in mainKeys)
                        {
                            var (x, y, z0, _) = spots[i];
                            if (mine.Count >= n)
                                break;
                            if (!Spaced(x, spread))
                                continue;
                            if (Ok(x, y, z0))
                                Put(x, y, z0);
                        }
                    }
                }
                else
                {
                    // highest first, out of the smoke, pairs before singles, nearest amidships
                    keys.Clear();
                    for (int i = 0; i < spots.Count; i++)
                    {
                        var s = spots[i];
                        keys.Add(((-s.Z0, Smoky(s.X, s.Y, s.Z0) ? 1 : 0, !s.Pair ? 1 : 0, Math.Abs(s.X)), i));
                    }
                    KeyedSort.Sort(keys);
                    foreach (var (_, i) in keys)
                    {
                        var (x, y, z0, pair) = spots[i];
                        long left = n - mine.Count;
                        if (left <= 0)
                            break;
                        if (pair && left < 2)
                            continue;
                        if (Ok(x, y, z0) && (!pair || Ok(x, -y, z0)))
                        {
                            long unit = DistinctUnits(mine) + 1;
                            Put(x, y, z0, pair, unit);
                            if (pair)
                                Put(x, -y, z0, pair, unit);
                        }
                    }
                }
            }
            if (mine.Count < n)   // a little more ship may give one a roof; the sizer only tries a few steps
                lay.Fail("beam", $"Only {mine.Count} of {n} {(bat == "aa" ? "AA" : bat)} directors find a roof to stand on " +
                                 $"({w:F1} m across with the rangefinder).");
        }
        if (design.MainBatteries.Any(b => b.Turrets != 0) && Spec(design, "main").Directors == 0)
            lay.Warnings.Add("The main battery has no director: each turret fires under local control.");
    }

    /// <summary>A director as a block of its own at (x, y) on z0 above the main deck, its weights aloft and its plotting
    /// room's below, and its record.</summary>
    static Director PutDirector(Layout lay, List<Block> blocks, string bat, DirectorSpec d, string bid, double x, double y, double z0, long? unit)
    {
        var (l, w) = Size(d);
        double hl = l / 2;
        var wt = Weights(d);
        var pts = Geometry.DirectorParts(x, y, l, w, d.RangefinderM).Outline;
        var b = Layout.AddBlock(lay, blocks, bid, x - hl, x + hl, w, 1, 0.0, 0.0, y: y, z0: z0, kind: "director", tPerM2: 0.0,
            points: pts, role: "director");
        b.Director = new BlockDirector(bat, d.RangefinderM, d.RadarT > 0, (long)Math.Round(z0 / Layout.LevelH));
        double aloft = wt.Hood + wt.Gear + wt.Rangefinder + wt.Armour + wt.Radar;
        lay.Weights.Add(new Weight(bid, "fire_control", aloft, x, ZRel.Deck(z0 + 0.5 * HoodH)));
        var rec = new Director(bid, bat, x, y, z0, z0 + HoodH, z0 + EyeH, d, aloft, unit);
        lay.Directors.Add(rec);
        return rec;
    }

    /// <summary>The directors the masts' tops carry (masts[i].director, fire-control.md F4b): one of the battery's on the
    /// top's roof, over the mast, named for it; a secondary or AA one a unit of its own.</summary>
    public static void PlaceInTops(Layout lay, Design design, List<Block> blocks, List<Mast> masts)
    {
        var placed = new Dictionary<string, long>();
        foreach (var m in masts)
        {
            if (m.Director is not { } bat || m.Platform is not { } top)
                continue;
            var d = Spec(design, bat);
            if (placed.GetValueOrDefault(bat) >= d.Directors)
                continue;   // the battery has fewer directors than tops asking for one: this top stands empty
            placed[bat] = placed.GetValueOrDefault(bat) + 1;
            long? unit = bat == "main" ? null : lay.Directors.Where(x => x.Battery == bat).Select(x => x.Unit ?? 0).DefaultIfEmpty(0).Max() + 1;
            PutDirector(lay, blocks, bat, d, $"{Label[bat]} ({m.Id} top)", m.X, m.Y ?? 0.0, top.Roof, unit);
        }
    }

    /// <summary>Does any funnel's smoke blind a control position? (SmokeFrom(...).Count > 0)</summary>
    static bool AnySmoke(List<Funnel> funnels, double funTop, double reach, double x1, double top, double y, double w)
    {
        foreach (var f in funnels)
        {
            double d = (f.X - f.L / 2) - x1;
            if (0 <= d && d < reach && top < funTop + 0.3 * d && Math.Abs(f.Y - y) < w / 2 + f.W)
                return true;
        }
        return false;
    }

    /// <summary>How many fire-control units (distinct Unit values, none counting as one) the directors make.</summary>
    static long DistinctUnits(List<Director> ds)
    {
        long count = 0;
        for (int a = 0; a < ds.Count; a++)
        {
            bool seen = false;
            for (int b = 0; b < a && !seen; b++)
                seen = ds[b].Unit == ds[a].Unit;
            if (!seen)
                count++;
        }
        return count;
    }

    /// <summary>The raised stretches' decks as roofs for roof_spots.</summary>
    static List<Block> RaisedRoofs(Layout lay) =>
        lay.Decks.Where(dk => dk.Kind == "deck").Select(dk => new Block
        {
            Id = dk.Id, Kind = "raised", Role = "deckhouse", X0 = dk.Points.Min(p => p.X), X1 = dk.Points.Max(p => p.X),
            W = dk.Points.Max(p => p.Y) - dk.Points.Min(p => p.Y), Y = 0.0, Level = (long)Math.Round(dk.Top / Geometry.DeckPitch),
            Points = dk.Points,
        }).ToList();

    /// <summary>A gunhouse rangefinder's weight, t, on each mount with one (fire-control-designer.md §3.4): the instrument
    /// with its mounting, three times the bare instrument's (its battery's optics' mass_kg_m2 times the base squared, else
    /// 27), and its hood as a box of 6 mm plate (B + 0.6 wide, 1.6 deep, 1.9 high), at the gunhouse's roof.</summary>
    public static void TurretRangefinders(Layout lay, Design design, List<Mount> mounts)
    {
        foreach (var m in mounts)
        {
            if (m.T.RangefinderM is not double b)
                continue;
            string bat = m.Kind == ComponentKind.Main ? "main" : "secondary";
            double massK = FireControlTemplates.FitOf(design.FireControl?.Of(bat)).Select(n => FireControlTemplates.Resolve(design, n)?.Optics?.MassKgM2)
                .FirstOrDefault(k => k is > 0) ?? 27.0;
            double instrument = massK * b * b / 1000.0, w = b + 0.6, d = 1.6, h = 1.9;
            double hood = (2 * w * h + 2 * d * h + w * d) * 0.006 * Weight.Steel;
            lay.Weights.Add(new Weight($"Rangefinder ({m.Id})", "fire_control", 3 * instrument + hood, m.X, ZRel.Deck(m.Top)));
        }
    }

    /// <summary>The control positions' rangefinders (fire-control.md F4b): a hood on after control's highest roof and on the
    /// conning tower's, each a block of its own (role rangefinder: a director's shape, the hood and the tube's ends, without a
    /// director's gear), weighed as a turret's (three times the bare instrument, a 6 mm plate box), where it finds room.</summary>
    public static void PlaceRangefinders(Layout lay, Design design, List<Block> blocks)
    {
        void Hood(string id, double baseM, double x, double y, double z0)
        {
            double l = 1.6, w = baseM + 0.6;
            var fp = Footprint.Rect(x - l / 2, y - w / 2, x + l / 2, y + w / 2);
            if (!lay.FreeAt(fp, z0, z0 + HoodH, 0.2))
            {
                lay.Warnings.Add($"{id}: no room on its roof; left off.");
                return;
            }
            var pts = Geometry.DirectorParts(x, y, l, w, baseM).Outline;
            var b = Layout.AddBlock(lay, blocks, id, x - l / 2, x + l / 2, w, 1, 0.0, 0.0, y: y, z0: z0, kind: "director", tPerM2: 0.0, points: pts,
                role: "rangefinder");
            b.Director = new BlockDirector("main", baseM, false, (long)Math.Round(z0 / Layout.LevelH));
            double massK = FireControlTemplates.FitOf(design.FireControl?.Of("main")).Select(n => FireControlTemplates.Resolve(design, n)?.Optics?.MassKgM2)
                .FirstOrDefault(k => k is > 0) ?? 27.0;
            double instrument = massK * baseM * baseM / 1000.0, hd = 1.6, hh = 1.9;
            double hood = (2 * w * hh + 2 * hd * hh + w * hd) * 0.006 * Weight.Steel;
            lay.Weights.Add(new Weight(id, "fire_control", 3 * instrument + hood, x, ZRel.Deck(z0 + 0.5 * HoodH)));
        }
        if (design.FireControl?.AftRangefinderM is > 0 and var aft)
        {
            Block? top = null;
            foreach (var b in blocks)
                if (b.Role == "aft_control" && (top is null || b.TopZ > top.TopZ))
                    top = b;
            if (top is null)
                lay.Warnings.Add("Aft rangefinder: no after control to stand on; left off.");
            else
                Hood("Aft rangefinder", aft, (top.X0 + top.X1) / 2, top.Y, top.TopZ);
        }
        if (design.FireControl?.ConningTowerRangefinderM is > 0 and var ct)
        {
            if (lay.ConningTower is not { } c)
                lay.Warnings.Add("Conning tower rangefinder: no conning tower; left off.");
            else
                Hood("Conning tower rangefinder", ct, c.X, c.Y, c.Top);
        }
    }

    /// <summary>A battery's computer block (its fit's), or null.</summary>
    public static ComputerTechInput? ComputerOf(Design design, string battery) =>
        FireControlTemplates.FitOf(design.FireControl?.Of(battery)).Select(n => FireControlTemplates.Resolve(design, n)?.Computer)
            .FirstOrDefault(c => c is not null);

    /// <summary>The transmitting stations (fire-control.md F4c): one a battery with directors, holding its computer (its
    /// block's mass, crew and power), in the deck space just under the machinery's roof, the protective deck (two levels
    /// down without a plant plan): 15 m² and 3 m² a man [EST], 0.4 of the beam wide, and at least a section long, so its
    /// ends can be bulkheads of its own. Each takes the free stretch nearest the conning tower (else the bridge) that no
    /// room it gives way to fills (magazines, handling rooms, machinery, bunkers, the stations before it) between the
    /// vitals' ends; with no room there it stands beside the station before it (else under the tower) and shares that
    /// cell, staying under the armour. A ship without vitals (no magazines or
    /// machinery spaces) looks within the middle 0.6 of its length. Stations no bulkhead falls between share a compartment,
    /// as the subdivision makes them. A room of its own, so damage can find it; its weight at its middle.</summary>
    public static void PlaceStations(Layout lay, Design design)
    {
        double x = lay.ConningTower?.X ?? lay.Blocks.Where(b => b.Role == "bridge").Select(b => (b.X0 + b.X1) / 2).DefaultIfEmpty(0.0).First();
        double hw = Math.Max(0.2 * lay.Hull.B, 1.5), top = -2 * Layout.LevelH, bse = -3 * Layout.LevelH;
        if (lay.Geo.Plant is { } plan)
        {
            // under the protective deck (research 02 §4: "below the waterline and inside the armored belt"; the deck space
            // under it straddles the waterline on most ships: the spaces wholly below are the magazines' and machinery's
            // sections, a short magazine taking its whole section)
            double D = plan.Decks[0];
            top = plan.Top - D;
            bse = top - Layout.LevelH;
            foreach (var z in plan.Decks)
                if (z - D < top - 1.8 && z - D > bse)
                    bse = z - D;
            bse = Math.Max(bse, plan.InnerBottom - D);
        }
        int yields = Subdivision.RoomPriority(RoomKind.TransmittingStation);
        var taken = new List<(double X0, double X1)>();
        double vital0 = double.PositiveInfinity, vital1 = double.NegativeInfinity;
        foreach (var c in lay.Compartments)
        {
            if (Subdivision.RoomPriority(c.Kind) < yields)
                continue;
            if (c.Kind is RoomKind.Magazine or RoomKind.BoilerRoom or RoomKind.EngineRoom)
                (vital0, vital1) = (Math.Min(vital0, c.X0), Math.Max(vital1, c.X1));
            double cb = c.Base ?? double.NegativeInfinity, ct = c.Top ?? 0.0;
            if (Math.Min(ct, top) - Math.Max(cb, bse) >= 0.25 * (top - bse) && Math.Abs(c.Y ?? 0.0) - c.HalfWidth < hw)
                taken.Add((c.X0, c.X1));
        }
        double half = 0.3 * lay.Hull.L;
        double? last = null;
        foreach (var bat in BatteryNames)
        {
            if (Spec(design, bat).Directors <= 0 || ComputerOf(design, bat) is not { } k)
                continue;
            double area = 15 + 3 * (k.Crew ?? 0), l = Math.Max(area / (2 * hw), Math.Max(3.0, Subdivision.MinSectionOf(lay.Hull.L)));
            double mid = (vital0 <= vital1 ? Spot(taken, vital0, vital1, l, x) : Spot(taken, -half, half, l, x)) ?? last ?? x;
            double x0 = mid - l / 2, x1 = mid + l / 2;
            taken.Add((x0, x1));
            last = mid;
            string id = bat == "main" ? "Transmitting station" : $"Transmitting station ({(bat == "aa" ? "AA" : bat)})";
            lay.Compartments.Add(new Compartment { Id = id, Kind = RoomKind.TransmittingStation, X0 = x0, X1 = x1, HalfWidth = hw, Base = bse, Top = top,
                Battery = bat, Men = (long)Math.Round(k.Crew ?? 0) });
            if (k.MassT is > 0)
                lay.Weights.Add(new Weight(id, "fire_control", k.MassT.Value, (x0 + x1) / 2, ZRel.Deck((bse + top) / 2)));
        }
    }

    /// <summary>The middle of the stretch l long within lo..hi that none of taken overlaps, nearest x; null if none.</summary>
    static double? Spot(List<(double X0, double X1)> taken, double lo, double hi, double l, double x)
    {
        double? best = null;
        double at = lo;
        foreach (var (t0, t1) in taken.Where(t => t.X1 > lo && t.X0 < hi).OrderBy(t => t.X0).Append((hi, hi)))
        {
            if (Math.Min(t0, hi) - at >= l)
            {
                double c = Math.Clamp(x, at + l / 2, Math.Min(t0, hi) - l / 2);
                if (best is not { } b || Math.Abs(c - x) < Math.Abs(b - x))
                    best = c;
            }
            at = Math.Max(at, t1);
        }
        return best;
    }

    /// <summary>The search radar's weight on the foremast's top, or 1 m over the highest roof on a ship without masts.</summary>
    public static void SearchRadar(Layout lay, Design design, List<Block> blocks, List<Mast> masts, double funTop)
    {
        double t = design.FireControl?.SearchRadarT ?? 0.0;
        if (t == 0)
            return;
        if (masts.Count > 0)
        {
            var m = masts[0];
            lay.Weights.Add(new Weight("Search radar", "fire_control", t, m.X, ZRel.Deck(m.Top ?? funTop + 6.0)));
            return;
        }
        var top = blocks.MaxBy(b => b.TopZ);
        lay.Weights.Add(new Weight("Search radar", "fire_control", t, top != null ? (top.X0 + top.X1) / 2 : 0.0,
            ZRel.Deck((top?.TopZ ?? 0.0) + 1.0)));
    }

    /// <summary>The directors for the report.</summary>
    public static List<DirectorReport> Report(Layout lay, double deckM) =>
        lay.Directors.Select(d =>
        {
            double eye = deckM + d.Eye;
            return new DirectorReport(d.Id, d.Battery, Math.Round(d.X, 2), Math.Round(d.Y, 2), Math.Round(eye, 2), Math.Round(HorizonKm(eye), 1),
                d.Spec.RangefinderM, d.Spec.ArmourMm, d.Spec.RadarT, Math.Round(d.WeightT, 1));
        }).ToList();
}
