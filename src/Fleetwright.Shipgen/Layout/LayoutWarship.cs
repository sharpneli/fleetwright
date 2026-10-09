namespace Fleetwright.Shipgen;

/// <summary>One main battery's turret, with the sizes the warship layout keeps clear for it. K: its index in "main".</summary>
public sealed class Gun
{
    public readonly PyDict Spec;
    public readonly int K;
    public readonly string Tid;
    public readonly PyDict T;
    public readonly string Cal;
    public readonly double R, Reach, Th, RR, Gap;
    public readonly PyDict Arm;
    public readonly bool Raised, Echelon, Cross;

    public Gun(PyDict spec, int k = 0)
    {
        Spec = spec;
        K = k;
        (Tid, T) = Geometry.BatteryType(spec);
        Cal = $"{Py.G(spec.F("calibre_mm"))} mm";
        R = T.F("r");
        Reach = Py.Max(R, Geometry.TurretReach(T, 0.0));
        Th = Geometry.TurretHeight(T);
        RR = Geometry.TurretReach(T) + 0.5;
        Gap = 2.0 + 0.5 * R;
        Arm = PyDict.Of(("armour_mm", spec.Get("armour_mm", 0L)));
        if (spec.B("material"))
            Arm["material"] = spec["material"];
        Raised = Py.Eq(Layout.StandsOn(spec, "amidships_stands_on"), "deckhouse");
        Echelon = Py.Truthy(spec.Get("echelon", false));
        Cross = Echelon && Py.Truthy(spec.Get("cross_deck", false));
    }
}

public sealed partial class Layout
{
    /// <summary>An end group: every battery's turrets there in list order, outermost first, and how many step up.</summary>
    static (List<Gun> Out, int Run) EndGroup(List<Gun> guns, string key)
    {
        var out_ = new List<Gun>();
        int run = 0;
        foreach (var g in guns)
        {
            long n = Py.ToLong(g.Spec.Get(key, 0L));
            var sf = g.Spec.Get("superfire", true);
            double k = sf is true ? n : sf is false ? 0 : ((PyDict)sf!).F(key, n);
            for (long i = 0; i < n; i++)
            {
                if (run == out_.Count && (out_.Count == 0 || i < k))
                    run += 1;
                out_.Add(g);
            }
        }
        return (out_, run);
    }

    /// <summary>How far an end group's tier `level` stands above the outermost turret.</summary>
    static double TierSteps(List<Gun> gs, int level) => Py.Sum(Enumerable.Range(0, level).Select(j => Geometry.SuperfireStep(gs[j].Th)));

    /// <summary>Each turret's distance inboard of its end group's outermost.</summary>
    static List<double> GroupOffsets(List<Gun> gs)
    {
        var out_ = new List<double>();
        double o = 0.0;
        for (int i = 0; i < gs.Count; i++)
        {
            if (i != 0)
                o += 1.1 * (gs[i - 1].R + gs[i].R) + 3.0;
            out_.Add(o);
        }
        return out_;
    }

    /// <summary>The casemate batteries: single guns at the hull side, in two tiers (lower in the hull side, upper in
    /// housings on the main deck).</summary>
    static void PlaceCasemates(Layout lay, List<PyDict> mounts, PyDict turretTypes, List<PyDict> blocks, List<PyDict> secs, Hull hull,
        double depth)
    {
        var bats = new List<(PyDict Sec, long N, string TId, PyDict T, bool Upper)>();
        foreach (var sec in secs)
        {
            long n = Py.ToLong(sec["per_side"]);
            if (Py.Eq(sec.Get("mount"), "casemate") && n != 0)
            {
                var (tId, t) = Geometry.BatteryType(sec, "casemate");
                turretTypes[tId] = t;
                bats.Add((sec, n, tId, t, Py.Eq(sec.Get("tier", "lower"), "upper")));
            }
        }
        if (bats.Count == 0)
            return;
        bats = Py.Sorted(bats, bt => bt.Upper ? 1 : 0);
        double B = hull.B;
        var barbettes = mounts.Where(m => Py.Eq(m["kind"], "main") && Geometry.HasBarbette(m.D("t"))).ToList();

        bool LowerOk(double x, double rc)
        {
            double hw = hull.HalfWidth(x);
            if (hw < CASEMATE_BEAM * B / 2 || hw - 2 * rc < 0.5)
                return false;
            var boxes = new[] { Footprint.Rect(x - rc, hw - 2 * rc, x + rc, hw), Footprint.Rect(x - rc, -hw, x + rc, -hw + 2 * rc) };
            return !boxes.Any(bx => barbettes.Any(m => Overlap(bx, Footprint.Circle(m.F("x"), m.F("y"), 0.95 * m.D("t").F("r")), 0.3)));
        }

        (double Yo, double D) Housing(double x0, double x1, double rc)
        {
            double yo = Py.Min(Enumerable.Range(0, 9).Select(k => hull.HalfWidth(x0 + (x1 - x0) * k / 8))) - 0.3;
            return (yo, 1.6 * rc);
        }

        bool InRaised(double x, double rc) => lay.DeckLevels(x, 1.05 * rc).Lo >= 1;

        bool UpperOk(double x, double rc)
        {
            if (hull.HalfWidth(x) < CASEMATE_BEAM * B / 2)
                return false;
            if (InRaised(x, rc))
                return LowerOk(x, rc);
            double x0 = x - 1.05 * rc, x1 = x + 1.05 * rc;
            var (yo, d) = Housing(x0, x1, rc);
            if (yo - d < 0.5)
                return false;
            foreach (int side in new[] { 1, -1 })
            {
                var fp = side > 0 ? Footprint.Rect(x0, yo - d, x1, yo) : Footprint.Rect(x0, -yo, x1, -yo + d);
                if (lay.Footprints.Any(o => o.Owner != "Deckhouse" && o.Base < LEVEL_H - 0.01 && o.Top > 0.01 && Overlap(fp, o.Fp, 0.3)))
                    return false;
                if (!lay.Clear(fp, LEVEL_H))
                    return false;
            }
            return true;
        }

        var xs = Py.Range(Py.Int(-hull.L), Py.Int(hull.L) + 1).Select(k => 0.5 * k).ToList();
        var elig = xs.Where(x => hull.HalfWidth(x) >= CASEMATE_BEAM * B / 2).ToList();
        double c = (elig.Count > 0 ? (Py.Min(elig) + Py.Max(elig)) / 2 : 0.0) + lay.Geo.Shift;
        xs = Py.Sorted(xs, x => Math.Abs(x - c));
        var okCache = new Dictionary<(double, double, bool), bool>();
        double rUp = Py.Max(bats.Where(b => b.Upper).Select(b => b.T.F("r")), 0.0);

        bool Cached(double x, double rc, bool upper)
        {
            var key = (x, rc, upper);
            if (!okCache.TryGetValue(key, out var v))
                okCache[key] = v = upper ? UpperOk(x, rc) : LowerOk(x, rc);
            return v;
        }

        List<List<double>> Attempt(string mode)
        {
            double Half(double r, bool upper)
            {
                if (mode == "pref")
                    return 1.1 * r + 2.0;
                if (mode == "stagger" && !upper)
                    return Py.Max(1.05 * r + 0.5, 1.05 * rUp + Geometry.CASEMATE_SHIELD * r + 0.3);
                return 1.05 * r + 0.5;
            }
            var taken = new Dictionary<bool, List<(double X, double H)>> { [false] = [], [true] = [] };
            var shields = new List<(double X, double R)>();
            var out_ = new List<List<double>>();
            foreach (var (sec, n, tId, t, upper) in bats)
            {
                double rc = t.F("r");
                double h = Half(rc, upper);
                var got = new List<double>();
                foreach (var x in xs)
                {
                    if (got.Count >= n)
                        break;
                    if (!taken[upper].All(o => Math.Abs(x - o.X) >= h + o.H))
                        continue;
                    if (upper && !shields.All(s => Math.Abs(x - s.X) >= 1.05 * rc + s.R + 0.3))
                        continue;
                    if (Cached(x, rc, upper))
                    {
                        got.Add(x);
                        taken[upper].Add((x, h));
                    }
                }
                if (!upper)
                    shields.AddRange(got.Select(x => (x, Geometry.CASEMATE_SHIELD * rc)));
                out_.Add(Py.Sorted(got));
            }
            return out_;
        }

        List<List<double>>? best = null;
        List<List<double>> placed = null!;
        bool brokeOut = false;
        foreach (var mode in rUp != 0 ? new[] { "pref", "stagger", "min" } : ["pref", "min"])
        {
            placed = Attempt(mode);
            if (placed.Zip(bats).All(t => t.First.Count >= t.Second.N))
            {
                brokeOut = true;
                break;
            }
            if (best is null || placed.Sum(g => g.Count) > best.Sum(g => g.Count))
                best = placed;
        }
        if (!brokeOut)
            placed = best!;
        var every = placed.Zip(bats).SelectMany(t => t.First.Select(x => (X: x, Bt: t.Second))).ToList();
        if (every.Count > 0)
        {
            double mean = (Py.Min(every.Select(e => e.X)) + Py.Max(every.Select(e => e.X))) / 2;
            var ds = Py.Sorted(Enumerable.Range(-40, 81).Select(k => 0.5 * k), d => (Math.Abs(mean + d - c), d));
            foreach (var d in ds)
            {
                if (Math.Abs(mean + d - c) >= Math.Abs(mean - c))
                    break;
                bool ok_ = true;
                foreach (var (x, bt) in every)
                    if (!Cached(x + d, bt.T.F("r"), bt.Upper))
                    {
                        ok_ = false;
                        break;
                    }
                if (ok_)
                {
                    placed = placed.Select(got => got.Select(x => x + d).ToList()).ToList();
                    break;
                }
            }
        }
        var galleries = new List<double[]>();
        var lowerX = placed.Zip(bats).Where(t => !t.Second.Upper).SelectMany(t => t.First).ToList();
        foreach (var (got, (sec, n, tId, t, upper)) in placed.Zip(bats))
        {
            if (got.Count < n)
                lay.Fail("length", $"Only {got.Count} of {n} {Py.G(sec.F("calibre_mm"))} mm {(upper ? "upper " : "")}casemates " +
                                   $"per side fit {(upper ? "on deck" : "in the hull sides")}. " +
                                   "Use fewer or smaller guns.");
            var arm = sec["armour_mm"];
            double rc = t.F("r");
            for (int i = 0; i < got.Count; i++)
            {
                double x = got[i];
                double yo;
                if (upper && !InRaised(x, rc))
                {
                    var (hy, hd) = Housing(x - 1.05 * rc, x + 1.05 * rc, rc);
                    yo = hy;
                    galleries.Add([x - 1.05 * rc, x + 1.05 * rc, hy, hd]);
                }
                else
                    yo = hull.HalfWidth(x);
                var (bse, top) = upper ? (0.0, LEVEL_H) : (-LEVEL_H, 0.0);
                foreach (int side in new[] { 1, -1 })
                {
                    string mid = $"{sec.S("prefix")}{i + 1}{(side > 0 ? "S" : "P")}";
                    Armament.AddMount(lay, mounts, "secondary", tId, t, mid, x, side * yo, bse,
                        Armament.StowBearing(x, side, Arcs.ARC_CASEMATE), armourMm: arm, depth: depth, top: top,
                        footprintR: Geometry.CASEMATE_SHIELD * rc,
                        extra: [("battery", sec["prefix"]), ("casemate", true), ("material", sec.Get("material"))]);
                }
            }
        }
        galleries.Sort((p, q) =>
        {
            for (int i = 0; i < 4; i++)
            {
                int cc = p[i].CompareTo(q[i]);
                if (cc != 0)
                    return cc;
            }
            return 0;
        });
        var merged = new List<double[]>();
        foreach (var g in galleries)
        {
            if (merged.Count > 0 && g[0] - merged[^1][1] < 1.5 && !lowerX.Any(x => merged[^1][1] <= x && x <= g[0]))
            {
                var m = merged[^1];
                (m[1], m[2], m[3]) = (Py.Max(m[1], g[1]), Py.Min(m[2], g[2]), Py.Max(m[3], g[3]));
            }
            else
                merged.Add([.. g]);
        }
        for (int k = 0; k < merged.Count; k++)
        {
            var (x0, x1, yo, d) = (merged[k][0], merged[k][1], merged[k][2], merged[k][3]);
            foreach (int side in new[] { 1, -1 })
                AddBlock(lay, blocks, $"Casemate housing {k + 1}{(side > 0 ? "S" : "P")}", x0, x1, d, 1, 0.3, 0.3,
                    y: side * (yo - d / 2), role: "casemate");
        }
    }
}
