namespace Fleetwright.Shipgen;

/// <summary>One main battery's turret, with the sizes the warship layout keeps clear for it. K: its index in "main".</summary>
public sealed class Gun
{
    public readonly BatteryInput Spec;
    public readonly int K;
    public readonly string Tid;
    public readonly TurretType T;
    public readonly string Cal;
    public readonly double R, Reach, Th, RR, Gap;
    public readonly double ArmourMm;
    public readonly string? Material;
    public readonly bool Raised, Echelon, Cross;

    public Gun(BatteryInput spec, int k = 0)
    {
        Spec = spec;
        K = k;
        (Tid, T) = Geometry.BatteryType(spec);
        Cal = $"{spec.CalibreMm} mm";
        R = T.R;
        Reach = Math.Max(R, Geometry.TurretReach(T, 0.0));
        Th = Geometry.TurretHeight(T);
        RR = Geometry.TurretReach(T) + 0.5;
        Gap = 2.0 + 0.5 * R;
        ArmourMm = spec.ArmourMm ?? 0;
        Material = string.IsNullOrEmpty(spec.Material) ? null : spec.Material;
        Raised = spec.AmidshipsStandsOn == "deckhouse";
        Echelon = spec.Echelon ?? false;
        Cross = Echelon && (spec.CrossDeck ?? false);
    }
}

public sealed partial class Layout
{
    /// <summary>An end group: every battery's turrets there in list order, outermost first, and how many step up.</summary>
    static (List<Gun> Out, int Run) EndGroup(List<Gun> guns, string key)
    {
        var result = new List<Gun>();
        int run = 0;
        foreach (var g in guns)
        {
            long n = (key == "fore" ? g.Spec.Fore : g.Spec.Aft) ?? 0;
            var sf = g.Spec.Superfire;
            double k = sf is null or { All: true } ? n : sf.All is false ? 0 : (key == "fore" ? sf.Fore : sf.Aft) ?? n;
            for (long i = 0; i < n; i++)
            {
                if (run == result.Count && (result.Count == 0 || i < k))
                    run += 1;
                result.Add(g);
            }
        }
        return (result, run);
    }

    /// <summary>How far an end group's tier `level` stands above the outermost turret.</summary>
    static double TierSteps(List<Gun> gs, int level) => Enumerable.Range(0, level).Select(j => Geometry.SuperfireStep(gs[j].Th)).Sum();

    /// <summary>Each turret's distance inboard of its end group's outermost.</summary>
    static List<double> GroupOffsets(List<Gun> gs)
    {
        var result = new List<double>();
        double o = 0.0;
        for (int i = 0; i < gs.Count; i++)
        {
            if (i != 0)
                o += 1.1 * (gs[i - 1].R + gs[i].R) + 3.0;
            result.Add(o);
        }
        return result;
    }

    /// <summary>The casemate batteries: single guns at the hull side, in two tiers (lower in the hull side, upper in
    /// housings on the main deck).</summary>
    static void PlaceCasemates(Layout lay, List<Mount> mounts, OrderedDictionary<string, TurretType> turretTypes, List<Block> blocks,
        List<(BatteryInput Sec, string Prefix)> secs, Hull hull, double depth)
    {
        var bats = new List<((BatteryInput Sec, string Prefix) Sec, long N, string TId, TurretType T, bool Upper)>();
        foreach (var sec in secs)
        {
            long n = sec.Sec.MountsPerSide;
            if (sec.Sec.Mount == "casemate" && n != 0)
            {
                var (tId, t) = Geometry.BatteryType(sec.Sec, "casemate");
                turretTypes[tId] = t;
                bats.Add((sec, n, tId, t, sec.Sec.Tier == "upper"));
            }
        }
        if (bats.Count == 0)
            return;
        bats = bats.OrderBy(bt => bt.Upper ? 1 : 0).ToList();
        double B = hull.B;
        // the side where a casemate's gun ports are: half a deck below the main deck, or above it in a raised stretch;
        // a housing on the main deck keeps inside the deck's edge
        Planform lowSide = lay.Side(-LevelH / 2), highSide = lay.Side(LevelH / 2), deckEdge = lay.Side(0.0);
        var barbettes = mounts.Where(m => m.Kind == "main" && m.T.HasBarbette).ToList();

        bool SideOk(Planform side, double x, double rc)
        {
            double hw = side.HalfWidth(x);
            if (hw < CasemateBeam * B / 2 || hw - 2 * rc < 0.5)
                return false;
            var boxes = new[] { Footprint.Rect(x - rc, hw - 2 * rc, x + rc, hw), Footprint.Rect(x - rc, -hw, x + rc, -hw + 2 * rc) };
            return !boxes.Any(bx => barbettes.Any(m => Overlap(bx, Footprint.Circle(m.X, m.Y, Geometry.BarbetteR(m.T)), 0.3)));
        }

        (double Yo, double D) Housing(double x0, double x1, double rc)
        {
            double yo = Enumerable.Range(0, 9).Select(k => deckEdge.HalfWidth(x0 + (x1 - x0) * k / 8)).Min() - 0.3;
            return (yo, 1.6 * rc);
        }

        bool InRaised(double x, double rc) => lay.DeckLevels(x, 1.05 * rc).Lo >= 1;

        bool UpperOk(double x, double rc)
        {
            if (deckEdge.HalfWidth(x) < CasemateBeam * B / 2)
                return false;
            if (InRaised(x, rc))
                return SideOk(highSide, x, rc);
            double x0 = x - 1.05 * rc, x1 = x + 1.05 * rc;
            var (yo, d) = Housing(x0, x1, rc);
            if (yo - d < 0.5)
                return false;
            foreach (int side in new[] { 1, -1 })
            {
                var fp = side > 0 ? Footprint.Rect(x0, yo - d, x1, yo) : Footprint.Rect(x0, -yo, x1, -yo + d);
                if (lay.Footprints.Any(o => o.Owner != "Deckhouse" && o.Base < LevelH - 0.01 && o.Top > 0.01 && Overlap(fp, o.Fp, 0.3)))
                    return false;
                if (!lay.Clear(fp, LevelH))
                    return false;
            }
            return true;
        }

        var xs = Enumerable.Range((int)-hull.L, 2 * (int)hull.L + 1).Select(k => 0.5 * k).ToList();
        var elig = xs.Where(x => lowSide.HalfWidth(x) >= CasemateBeam * B / 2).ToList();
        double c = (elig.Count > 0 ? (elig.Min() + elig.Max()) / 2 : 0.0) + lay.Geo.Shift;
        xs = xs.OrderBy(x => Math.Abs(x - c)).ToList();
        var okCache = new Dictionary<(double, double, bool), bool>();
        double rUp = bats.Where(b => b.Upper).Select(b => b.T.R).DefaultIfEmpty(0.0).Max();

        bool Cached(double x, double rc, bool upper)
        {
            var key = (x, rc, upper);
            if (!okCache.TryGetValue(key, out var v))
                okCache[key] = v = upper ? UpperOk(x, rc) : SideOk(lowSide, x, rc);
            return v;
        }

        List<List<double>> Attempt(string mode)
        {
            double Half(double r, bool upper)
            {
                if (mode == "pref")
                    return 1.1 * r + 2.0;
                if (mode == "stagger" && !upper)
                    return Math.Max(1.05 * r + 0.5, 1.05 * rUp + Geometry.CasemateShield * r + 0.3);
                return 1.05 * r + 0.5;
            }
            var taken = new Dictionary<bool, List<(double X, double H)>> { [false] = [], [true] = [] };
            var shields = new List<(double X, double R)>();
            var result = new List<List<double>>();
            foreach (var (sec, n, tId, t, upper) in bats)
            {
                double rc = t.R;
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
                    shields.AddRange(got.Select(x => (x, Geometry.CasemateShield * rc)));
                result.Add(got.Order().ToList());
            }
            return result;
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
            double mean = (every.Select(e => e.X).Min() + every.Select(e => e.X).Max()) / 2;
            var ds = Enumerable.Range(-40, 81).Select(k => 0.5 * k).OrderBy(d => (Math.Abs(mean + d - c), d)).ToList();
            foreach (var d in ds)
            {
                if (Math.Abs(mean + d - c) >= Math.Abs(mean - c))
                    break;
                bool ok_ = true;
                foreach (var (x, bt) in every)
                    if (!Cached(x + d, bt.T.R, bt.Upper))
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
                lay.Fail("length", $"Only {got.Count} of {n} {sec.Sec.CalibreMm} mm {(upper ? "upper " : "")}casemates " +
                                   $"per side fit {(upper ? "on deck" : "in the hull sides")}. " +
                                   "Use fewer or smaller guns.");
            var arm = sec.Sec.ArmourMm!.Value;
            double rc = t.R;
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
                    yo = (upper ? highSide : lowSide).HalfWidth(x);
                var (bse, top) = upper ? (0.0, LevelH) : (-LevelH, 0.0);
                foreach (int side in new[] { 1, -1 })
                {
                    string mid = $"{sec.Prefix}{i + 1}{(side > 0 ? "S" : "P")}";
                    var m = Armament.AddMount(lay, mounts, "secondary", tId, t, mid, x, side * yo, bse,
                        Armament.StowBearing(x, side, Arcs.ArcCasemate), armourMm: arm, depth: depth, top: top,
                        footprintR: Geometry.CasemateShield * rc);
                    (m.Battery, m.Casemate, m.Material) = (sec.Prefix, true, sec.Sec.Material);
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
                (m[1], m[2], m[3]) = (Math.Max(m[1], g[1]), Math.Min(m[2], g[2]), Math.Max(m[3], g[3]));
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
