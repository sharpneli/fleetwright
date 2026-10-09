namespace Fleetwright.Shipgen;

/// <summary>One battery's fire control: how many directors, the rangefinder's base, the hood's armour, and the radar's
/// and the plotting room computer's weight.</summary>
public sealed record DirectorSpec(long Directors, double RangefinderM, double ArmourMm, double RadarT, double ComputerT);

/// <summary>A placed director: its battery, where it stands (base and top above the main deck, and its eye), its
/// settings, its weight, and (secondary and AA directors) the unit of a pair it belongs to.</summary>
public sealed record Director(string Id, string Battery, double X, double Y, double Base, double Top, double Eye, DirectorSpec Spec,
    double WeightT, long? Unit);

/// <summary>firecontrol: directors, the plotting rooms that turn their readings into gun orders, and the search radar.</summary>
public static class FireControl
{
    static readonly string[] BatteryNames = ["main", "secondary", "aa"];
    static readonly string[] Fields = ["directors", "rangefinder_m", "armour_mm", "radar_t", "computer_t"];
    static readonly Dictionary<string, string> Label = new() { ["main"] = "Main director", ["secondary"] = "Secondary director", ["aa"] = "AA director" };
    public const double HoodH = 2.2;
    const double EyeH = 1.5, HoodPlateT = 0.047, RfTK = 0.03;
    static readonly (double A, double B) GearT = (1.0, 1.2);
    const double ComputerZ = 0.3, MainSpread = 0.25, Refraction = 1.17;

    /// <summary>The design's fire control for a battery, every field (0 where it gives none).</summary>
    public static DirectorSpec Spec(Design design, string battery)
    {
        var d = design.FireControl?.Of(battery);
        return new DirectorSpec((long)(d?.Directors ?? 0), d?.RangefinderM ?? 0, d?.ArmourMm ?? 0, d?.RadarT ?? 0, d?.ComputerT ?? 0);
    }

    public static List<string> Validate(Design design)
    {
        if (design.FireControl is not { } fc)
            return [];
        var errs = fc.Extra.KeysOrEmpty().Select(k => $"fire_control.{k}: not a battery ({string.Join(", ", BatteryNames)}) or search_radar_t")
            .ToList();
        foreach (var b in BatteryNames)
        {
            if (fc.Of(b) is not { } d)
                continue;
            errs.AddRange(d.Extra.KeysOrEmpty().Select(k => $"fire_control.{b}.{k}: not a director setting ({string.Join(", ", Fields)})"));
            var given = new[] { d.Directors, d.RangefinderM, d.ArmourMm, d.RadarT, d.ComputerT };
            errs.AddRange(Fields.Where((_, i) => given[i] < 0).Select(k => $"fire_control.{b}.{k} must be a number, 0 or more"));
            if (d.Directors is double n && n != Math.Floor(n))
                errs.Add($"fire_control.{b}.directors: use a whole number");
        }
        if (fc.SearchRadarT < 0)
            errs.Add("fire_control.search_radar_t must be a number, 0 or more");
        return errs;
    }

    /// <summary>A director's footprint: (fore-and-aft, athwartships) m.</summary>
    static (double L, double W) Size(DirectorSpec d)
    {
        double bse = d.RangefinderM;
        return (0.25 * bse + 1.8, Math.Max(bse + 1.0, 1.8));
    }

    /// <summary>One director's weights, t: the hood, the rangefinder and its gear, the armour, the radar and the plotting
    /// room's computer.</summary>
    static (double Hood, double Gear, double Rangefinder, double Armour, double Radar, double Computer) Weights(DirectorSpec d)
    {
        double bse = d.RangefinderM;
        var (l, w) = Size(d);
        double area = 2 * (l + w) * HoodH + l * w;
        return (area * HoodPlateT, GearT.A + GearT.B * bse, RfTK * Math.Pow(bse, 2), area * d.ArmourMm / 1000.0 * 7.85, d.RadarT,
            d.ComputerT);
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
        foreach (var bat in BatteryNames)
        {
            var d = Spec(design, bat);
            long n = d.Directors;
            if (n == 0)
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
                var pts = Geometry.DirectorParts(x, y, l, w, d.RangefinderM).Outline;
                var b = Layout.AddBlock(lay, blocks, bid, x - hl, x + hl, w, 1, 0.0, 0.0, y: y, z0: z0, kind: "director", tPerM2: 0.0,
                    points: pts, role: "director");
                b.Director = new BlockDirector(bat, d.RangefinderM, d.RadarT > 0, (long)Math.Round(z0 / Layout.LevelH));
                double aloft = wt.Hood + wt.Gear + wt.Rangefinder + wt.Armour + wt.Radar;
                lay.Weights.Add(new Weight(bid, "fire_control", aloft, x, ZRel.Deck(z0 + 0.5 * HoodH)));
                if (wt.Computer != 0)
                    lay.Weights.Add(new Weight($"Plotting room ({bid})", "fire_control", wt.Computer, x, ZRel.Frac(ComputerZ)));
                var rec = new Director(bid, bat, x, y, z0, z0 + HoodH, z0 + EyeH, d, aloft + wt.Computer, unit);
                lay.Directors.Add(rec);
                mine.Add(rec);
            }

            bool Smoky(double x, double y, double z0) =>
                SmokeFrom(lay.FunnelsPlanned, lay.FunnelsPlanned.Select(f => f.Top).DefaultIfEmpty(0.0).Max(), lay.Geo.SmokeReach,
                    x + hl, z0 + Layout.LevelH, y, w).Count > 0;

            foreach (var spots in new Func<List<(double X, double Y, double Z0, bool Pair)>>[]
                     { () => Layout.RoofSpots(blocks, l, w), () => Layout.RoofSpots(RaisedRoofs(lay), l, w) })
            {
                if (mine.Count >= n)
                    break;
                var sp = spots();
                if (bat == "main")
                {
                    var cands = sp.Where(s => !s.Pair).Select(s => (s.X, s.Y, s.Z0)).OrderBy(s => (-s.Z0, -s.X)).ToList();
                    foreach (var spread in new[] { MainSpread * L, 0.0 })
                        foreach (var (x, y, z0) in cands)
                        {
                            if (mine.Count >= n)
                                break;
                            if (mine.Any(m => Math.Abs(x - m.X) < Math.Max(mine.Count < 2 ? spread : 0.0, l + 0.4)))
                                continue;
                            if (Ok(x, y, z0))
                                Put(x, y, z0);
                        }
                }
                else
                {
                    foreach (var (x, y, z0, pair) in sp.OrderBy(s => (-s.Z0, Smoky(s.X, s.Y, s.Z0) ? 1 : 0, !s.Pair ? 1 : 0, Math.Abs(s.X))).ToList())
                    {
                        long left = n - mine.Count;
                        if (left <= 0)
                            break;
                        if (pair && left < 2)
                            continue;
                        var pts = pair ? new[] { (x, y), (x, -y) } : [(x, y)];
                        if (pts.All(p => Ok(p.Item1, p.Item2, z0)))
                        {
                            long unit = mine.Select(m => m.Unit).Distinct().Count() + 1;
                            foreach (var (px, py) in pts)
                                Put(px, py, z0, pair, unit);
                        }
                    }
                }
            }
            if (mine.Count < n)
                lay.Fail("beam", $"Only {mine.Count} of {n} {(bat == "aa" ? "AA" : bat)} directors find a roof to stand on " +
                                 $"({w:F1} m across with the rangefinder).");
        }
        if (design.MainBatteries.Any(b => b.Turrets != 0) && Spec(design, "main").Directors == 0)
            lay.Warnings.Add("The main battery has no director: each turret fires under local control.");
    }

    /// <summary>The raised stretches' decks as roofs for roof_spots.</summary>
    static List<Block> RaisedRoofs(Layout lay) =>
        lay.Decks.Where(dk => dk.Kind == "deck").Select(dk => new Block
        {
            Id = dk.Id, Kind = "raised", Role = "deckhouse", X0 = dk.Points.Min(p => p.X), X1 = dk.Points.Max(p => p.X),
            W = dk.Points.Max(p => p.Y) - dk.Points.Min(p => p.Y), Y = 0.0, Level = (long)Math.Round(dk.Top / Geometry.DeckPitch),
            Points = dk.Points,
        }).ToList();

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
