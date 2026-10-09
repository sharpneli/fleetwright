namespace Fleetwright.Shipgen;

/// <summary>firecontrol: directors, the plotting rooms that turn their readings into gun orders, and the search radar.</summary>
public static class FireControl
{
    static readonly string[] BATTERIES = ["main", "secondary", "aa"];
    static readonly string[] FIELDS = ["directors", "rangefinder_m", "armour_mm", "radar_t", "computer_t"];
    static readonly Dictionary<string, string> LABEL = new() { ["main"] = "Main director", ["secondary"] = "Secondary director", ["aa"] = "AA director" };
    public const double HOOD_H = 2.2;
    const double EYE_H = 1.5, HOOD_PLATE_T = 0.047, RF_T_K = 0.03;
    static readonly (double A, double B) GEAR_T = (1.0, 1.2);
    const double COMPUTER_Z = 0.3, MAIN_SPREAD = 0.25, REFRACTION = 1.17;

    /// <summary>The design's fire control, every battery with every field (0 where it gives none).</summary>
    public static PyDict Spec(PyDict design)
    {
        var fc = design.DOr("fire_control");
        var out_ = new PyDict();
        foreach (var b in BATTERIES)
        {
            var bd = fc.DOr(b);
            out_[b] = PyDict.Of(FIELDS.Select(k => (k, bd.Get(k, 0L))).ToArray());
        }
        out_["search_radar_t"] = fc.Get("search_radar_t", 0.0);
        return out_;
    }

    public static List<string> Validate(PyDict design)
    {
        var fcV = design.Get("fire_control");
        if (fcV is null)
            return [];
        if (fcV is not PyDict fc)
            return ["fire_control: use {\"main\": {...}, \"secondary\": {...}, \"aa\": {...}, \"search_radar_t\": t}"];
        var errs = fc.Keys.Where(k => !BATTERIES.Contains(k) && k != "search_radar_t")
            .Select(k => $"fire_control.{k}: not a battery ({string.Join(", ", BATTERIES)}) or search_radar_t").ToList();
        foreach (var b in BATTERIES)
        {
            var v = fc.Get(b);
            if (v is null)
                continue;
            if (v is not PyDict vd)
            {
                errs.Add($"fire_control.{b}: use {{{string.Join(", ", FIELDS)}}}");
                continue;
            }
            errs.AddRange(vd.Keys.Where(k => !FIELDS.Contains(k))
                .Select(k => $"fire_control.{b}.{k}: not a director setting ({string.Join(", ", FIELDS)})"));
            errs.AddRange(FIELDS.Where(k => vd.Has(k) && !(Py.IsNumber(vd[k]) && Py.ToDouble(vd[k]) >= 0))
                .Select(k => $"fire_control.{b}.{k} must be a number, 0 or more"));
            if (!Py.IsInt(vd.Get("directors", 0L)))
                errs.Add($"fire_control.{b}.directors: use a whole number");
        }
        var s = fc.Get("search_radar_t", 0L);
        if (!(Py.IsNumber(s) && Py.ToDouble(s) >= 0))
            errs.Add("fire_control.search_radar_t must be a number, 0 or more");
        return errs;
    }

    /// <summary>A director's footprint: (fore-and-aft, athwartships) m.</summary>
    static (double L, double W) Size(PyDict d)
    {
        double bse = d.F("rangefinder_m");
        return (0.25 * bse + 1.8, Py.Max(bse + 1.0, 1.8));
    }

    /// <summary>One director's weights, t.</summary>
    static PyDict Weights(PyDict d)
    {
        double bse = d.F("rangefinder_m");
        var (l, w) = Size(d);
        double area = 2 * (l + w) * HOOD_H + l * w;
        return PyDict.Of(("hood", area * HOOD_PLATE_T), ("gear", GEAR_T.A + GEAR_T.B * bse), ("rangefinder", RF_T_K * Py.Pow(bse, 2)),
            ("armour", area * d.F("armour_mm") / 1000.0 * 7.85), ("radar", d["radar_t"]), ("computer", d["computer_t"]));
    }

    public static double HorizonKm(double eyeM) => 3.57 * Math.Sqrt(REFRACTION * Py.Max(0.0, eyeM));

    static readonly string[] CONTROL_ROLES = ["bridge", "director", "aft_control"];

    /// <summary>Control positions in a funnel's smoke: sets lay.smoke and warns about each.</summary>
    public static void AssignSmoke(Layout lay, Navarch.Result res)
    {
        lay.Smoke = new OrderedDictionary<string, List<string>>(StringComparer.Ordinal);
        if (lay.Funnels.Count == 0)
            return;
        double reach = Powerplant.SmokeReach(res.Plant, res.PowerShp);
        foreach (var b in lay.Blocks)
        {
            if (!CONTROL_ROLES.Any(r => Py.Eq(b["role"], r)) || b.B("office"))
                continue;
            var hit = SmokeFrom(lay.Funnels, lay.FunTop, reach, b.F("x1"), Layout.BlockTop(b), b.F("y"), b.F("w"));
            if (hit.Count > 0)
            {
                lay.Smoke[b.S("id")] = hit;
                lay.Warnings.Add($"{b.S("id")} stands in the smoke of {string.Join(", ", hit)}: poor visibility from it.");
            }
        }
    }

    /// <summary>Ids of the funnels whose smoke blinds a control position.</summary>
    static List<string> SmokeFrom(IEnumerable<PyDict> funnels, double funTop, double reach, double x1, double top, double y, double w)
    {
        var hit = new List<string>();
        foreach (var f in funnels)
        {
            double d = (f.F("x") - f.F("l") / 2) - x1;
            if (0 <= d && d < reach && top < funTop + 0.3 * d && Math.Abs(f.F("y") - y) < w / 2 + f.F("w"))
                hit.Add(f.S("id"));
        }
        return hit;
    }

    /// <summary>Stand the design's directors on the superstructure's roofs as blocks of their own, with their weights.</summary>
    public static void Place(Layout lay, PyDict design, List<PyDict> blocks)
    {
        var fc = Spec(design);
        double L = lay.Hull.L;
        foreach (var bat in BATTERIES)
        {
            var d = fc.D(bat);
            long n = Py.ToLong(d["directors"]);
            if (n == 0)
                continue;
            var (l, w) = Size(d);
            double hl = l / 2, hw = w / 2;
            var wt = Weights(d);
            var mine = new List<PyDict>();

            bool Ok(double x, double y, double z0)
            {
                var fp = Footprint.Rect(x - hl, y - hw, x + hl, y + hw);
                return lay.FreeAt(fp, z0, z0 + HOOD_H, 0.2) && lay.Clear(fp, z0 + HOOD_H);
            }

            void Put(double x, double y, double z0, bool pair = false, long? unit = null)
            {
                int k = mine.Count;
                string bid = bat == "main" ? LABEL[bat] + (k == 0 ? "" : $" {k + 1}")
                    : $"{LABEL[bat]} {(unit is null ? "None" : unit.ToString())}" + (pair ? (y > 0 ? "S" : "P") : "");
                var pts = Geometry.DirectorParts(x, y, l, w, d.F("rangefinder_m")).Outline;
                var b = Layout.AddBlock(lay, blocks, bid, x - hl, x + hl, w, 1, 0.0, 0.0, y: y, z0: z0, kind: "director", tPerM2: 0.0,
                    points: pts, role: "director");
                b["director"] = PyDict.Of(("battery", bat), ("rangefinder_m", d["rangefinder_m"]), ("radar", d.F("radar_t") > 0),
                    ("on", Py.Round(z0 / Layout.LEVEL_H)));
                lay.Weights.Add(new Weight(bid, "fire_control", Py.ToDouble(Py.SumObj(wt.Where(kk => kk != "computer").Values)), x,
                    ZRel.Deck(z0 + 0.5 * HOOD_H)));
                if (Py.Truthy(wt["computer"]))
                    lay.Weights.Add(new Weight($"Plotting room ({bid})", "fire_control", wt.F("computer"), x, ZRel.Frac(COMPUTER_Z)));
                var rec = PyDict.Of(("id", bid), ("battery", bat), ("x", x), ("y", y), ("base", z0), ("top", z0 + HOOD_H), ("eye", z0 + EYE_H));
                rec.Update(d);
                rec["weight_t"] = Py.SumObj(wt.Values);
                rec["unit"] = unit;
                lay.Directors.Add(rec);
                mine.Add(rec);
            }

            bool Smoky(double x, double y, double z0) =>
                SmokeFrom(lay.FunnelsPlanned, Py.Max(lay.FunnelsPlanned.Select(f => f.F("top", 0.0)), 0.0), lay.Geo.SmokeReach,
                    x + hl, z0 + Layout.LEVEL_H, y, w).Count > 0;

            foreach (var spots in new Func<List<(double X, double Y, double Z0, bool Pair)>>[]
                     { () => Layout.RoofSpots(blocks, l, w), () => Layout.RoofSpots(RaisedRoofs(lay), l, w) })
            {
                if (mine.Count >= n)
                    break;
                var sp = spots();
                if (bat == "main")
                {
                    var cands = Py.Sorted(sp.Where(s => !s.Pair).Select(s => (s.X, s.Y, s.Z0)), s => (-s.Z0, -s.X));
                    foreach (var spread in new[] { MAIN_SPREAD * L, 0.0 })
                        foreach (var (x, y, z0) in cands)
                        {
                            if (mine.Count >= n)
                                break;
                            if (mine.Any(m => Math.Abs(x - m.F("x")) < Py.Max(mine.Count < 2 ? spread : 0.0, l + 0.4)))
                                continue;
                            if (Ok(x, y, z0))
                                Put(x, y, z0);
                        }
                }
                else
                {
                    foreach (var (x, y, z0, pair) in Py.Sorted(sp, s => (-s.Z0, Smoky(s.X, s.Y, s.Z0) ? 1 : 0, !s.Pair ? 1 : 0, Math.Abs(s.X))))
                    {
                        long left = n - mine.Count;
                        if (left <= 0)
                            break;
                        if (pair && left < 2)
                            continue;
                        var pts = pair ? new[] { (x, y), (x, -y) } : [(x, y)];
                        if (pts.All(p => Ok(p.Item1, p.Item2, z0)))
                        {
                            long unit = mine.Select(m => m["unit"]).Distinct(new PyEq()).Count() + 1;
                            foreach (var (px, py) in pts)
                                Put(px, py, z0, pair, unit);
                        }
                    }
                }
            }
            if (mine.Count < n)
                lay.Fail("beam", $"Only {mine.Count} of {n} {(bat == "aa" ? "AA" : bat)} directors find a roof to stand on " +
                                 $"({Py.F(w, 1)} m across with the rangefinder).");
        }
        if (Batteries.MainBatteries(design).Any(b => Batteries.BatteryTurrets(b) != 0) && Py.ToDouble(fc.D("main")["directors"]) == 0)
            lay.Warnings.Add("The main battery has no director: each turret fires under local control.");
    }

    sealed class PyEq : IEqualityComparer<object?>
    {
        public new bool Equals(object? a, object? b) => Py.Eq(a, b);
        public int GetHashCode(object? o) => o is null ? 0 : Py.ToDouble(o).GetHashCode();
    }

    /// <summary>The raised stretches' decks as roofs for roof_spots.</summary>
    static List<PyDict> RaisedRoofs(Layout lay)
    {
        var out_ = new List<PyDict>();
        foreach (var dk in lay.Decks)
        {
            if (!Py.Eq(dk["kind"], "deck"))
                continue;
            var pts = Geometry.Pts(dk["points"]);
            out_.Add(PyDict.Of(("id", dk["id"]), ("x0", Py.Min(pts.Select(p => p.X))), ("x1", Py.Max(pts.Select(p => p.X))),
                ("w", Py.Max(pts.Select(p => p.Y)) - Py.Min(pts.Select(p => p.Y))), ("y", 0.0), ("z0", 0.0),
                ("level", Py.Round(dk.F("top") / Geometry.DECK_PITCH)), ("points", pts)));
        }
        return out_;
    }

    /// <summary>The search radar's weight on the foremast's top, or 1 m over the highest roof on a ship without masts.</summary>
    public static void SearchRadar(Layout lay, PyDict design, List<PyDict> blocks, List<PyDict> masts, double funTop)
    {
        double t = Py.ToDouble(Spec(design)["search_radar_t"]);
        if (t == 0)
            return;
        if (masts.Count > 0)
        {
            var m = masts[0];
            lay.Weights.Add(new Weight("Search radar", "fire_control", t, m.F("x"), ZRel.Deck(m.F("top", funTop + 6.0))));
            return;
        }
        var top = Py.MaxByOrDefault(blocks, Layout.BlockTop);
        lay.Weights.Add(new Weight("Search radar", "fire_control", t, top != null ? (top.F("x0") + top.F("x1")) / 2 : 0.0,
            ZRel.Deck((top != null ? Layout.BlockTop(top) : 0.0) + 1.0)));
    }

    /// <summary>The directors for the report.</summary>
    public static List<object?> Report(Layout lay, double deckM)
    {
        var out_ = new List<object?>();
        foreach (var d in lay.Directors)
        {
            double eye = deckM + d.F("eye");
            out_.Add(PyDict.Of(("id", d["id"]), ("battery", d["battery"]), ("x", Py.Round(d.F("x"), 2)), ("y", Py.Round(d.F("y"), 2)),
                ("eye_height_m", Py.Round(eye, 2)), ("horizon_km", Py.Round(HorizonKm(eye), 1)), ("rangefinder_m", d["rangefinder_m"]),
                ("armour_mm", d["armour_mm"]), ("radar_t", d["radar_t"]), ("weight_t", Py.RoundObj(d["weight_t"], 1))));
        }
        return out_;
    }
}
