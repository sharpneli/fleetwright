namespace Fleetwright.Shipgen;

/// <summary>A place a mount (or a pair of mounts) could stand: X, Y and its base above the main deck; a pair's port
/// mount at YPort (default -Y). A slot on the centreline, or a Lone one, takes a single mount.</summary>
public readonly record struct Slot(double X, double Y, double Base, double? YPort = null, bool Lone = false)
{
    public bool Single => Y == 0 || Lone;
    public double PortY => YPort ?? -Y;
}

/// <summary>Style-neutral placement of guns, torpedo mounts and AA, and the mounts that hitboxes, arcs and the renderer
/// read.</summary>
public static class Armament
{
    /// <summary>Radius of the turret body and its ears (not the barrels): its footprint on deck.</summary>
    public static double BodyReach(TurretType t) => Math.Max(t.R, Geometry.TurretReach(t, 0.0));

    /// <summary>Rest bearing of a side mount at x on `side` (+1 starboard), whose arc is +-half about its own beam.</summary>
    public static double StowBearing(double x, double side, double half) => 90.0 * side + (x >= 0 ? -1.0 : 1.0) * side * half;

    /// <summary>Axis-aligned box around a mount's barrels (as shown) trained to `bearing`.</summary>
    public static Footprint BarrelFootprint(TurretType t, double x, double y, double bearing)
    {
        double c = Math.Cos(double.DegreesToRadians(bearing)), s = Math.Sin(double.DegreesToRadians(bearing));
        var pts = Geometry.TurretShapesOf(t).Barrels.SelectMany(poly => poly)
            .Select(p => new Pt(x + p.X * c - p.Y * s, y + p.X * s + p.Y * c)).ToList();
        var (x0, y0, x1, y1) = Geometry.Bounds(pts);
        return Footprint.Rect(x0, y0, x1, y1);
    }

    /// <summary>The heights (lo, hi) above the main deck that a mount's barrels take, about their axis.</summary>
    public static (double Lo, double Hi) BarrelBand(double bse, double top, TurretType t)
    {
        double axis = bse + 0.55 * (top - bse);
        double hw = t.BarrelW / 2 + 0.1;
        return (axis - hw, axis + hw);
    }

    public static (string Id, TurretType T) GunType(BatteryInput gun) => Geometry.BatteryType(gun);

    /// <summary>Axis-aligned box around a fixed tube laid along `bearing`.</summary>
    static Footprint TubeFootprint(TurretType t, double x, double y, double bearing)
    {
        double a = double.DegreesToRadians(bearing);
        double hl = t.BarrelLen / 2 + 0.2, hw = ((t.Barrels - 1) * t.Spacing + t.BarrelW) / 2 + 0.2;
        double ex = Math.Abs(hl * Math.Cos(a)) + Math.Abs(hw * Math.Sin(a)), ey = Math.Abs(hl * Math.Sin(a)) + Math.Abs(hw * Math.Cos(a));
        return Footprint.Rect(x - ex, y - ey, x + ex, y + ey);
    }

    /// <summary>One mount: footprint, weights and the mount record, for every style. A side mount also claims the space
    /// its barrels take at rest.</summary>
    public static Mount AddMount(Layout lay, List<Mount> mounts, string kind, string tId, TurretType t, string mid, double x, double y,
        double bse, double rest, long level = 0, double armourMm = 0.0, double depth = 10.0, double? top = null,
        double? footprintR = null, string label = "Mount", double deck = 0.0, bool sideMount = false)
    {
        double th = kind != "torpedo" || t.IsFixedTube ? Geometry.TurretHeight(t) : 1.1;
        double tp = top ?? bse + th;
        var m = new Mount
        {
            Id = mid, Kind = kind, Type = tId, T = t, X = x, Y = y, Level = level, Base = bse, Top = tp, Rest = rest, ArmourMm = armourMm,
            SideMount = sideMount,
        };
        mounts.Add(m);
        if (t.IsFixedTube)
        {
            m.Fixed = rest;
            lay.Occupy(TubeFootprint(t, x, y, rest), bse, tp, mid);
        }
        else
        {
            double r = footprintR ?? (kind != "torpedo" ? BodyReach(t) : t.BarrelLen / 2 + 0.3);
            lay.Occupy(Footprint.Circle(x, y, r), bse, tp, mid);
            if (sideMount)
            {
                var fp = BarrelFootprint(t, x, y, rest);
                var (lo, hi) = BarrelBand(bse, tp, t);
                lay.Occupy(fp, lo, hi, mid);
                lay.Overhangs.Add(fp);
            }
        }
        if (kind == "torpedo")
        {
            lay.Weights.Add(new Weight(mid, "armament", Batteries.TorpedoWeight(t.Barrels, t.IsFixedTube), x, ZRel.Deck(bse + 0.5)));
            return m;
        }
        var (tw, bw, aw) = Batteries.MountWeights(t, armourMm, depth, level, deck);
        lay.Weights.Add(new Weight($"{label} {mid}", "armament", tw, x, ZRel.Deck((bse + tp) / 2)));
        lay.Weights.Add(new Weight($"Magazine {mid}", "armament", aw, x, ZRel.Frac(0.25)));
        if (bw != 0 && bse > 0.5)
            lay.Weights.Add(new Weight($"Barbette {mid}", "armour", bw, x, ZRel.Frac(0.75)));
        return m;
    }

    /// <summary>n mounts in a line from xStart in direction stepDir, each superfiring over the one before it. stepped:
    /// how many step up (all of them when null); flat: none do, all on the deck.</summary>
    public static void GunLine(Layout lay, List<Mount> mounts, OrderedDictionary<string, TurretType> turretTypes, BatteryInput gun, long n,
        long? stepped, bool flat, string kind, IReadOnlyList<string> names, double xStart, double stepDir, double y, double rest,
        Func<double, double> deckH, bool raiseInner = false, double armourMm = 0.0, double depth = 10.0, string label = "Main",
        IReadOnlyCollection<string>? ignore = null, string? battery = null)
    {
        if (n == 0)
            return;
        var (tId, t) = GunType(gun);
        turretTypes[tId] = t;
        double r = t.R, th = Geometry.TurretHeight(t);
        double reach = BodyReach(t);
        double s = 2.2 * r + 3.0;
        long k = Math.Max(stepped ?? n, 1);
        for (int i = 0; i < n; i++)
        {
            double x = xStart + stepDir * (reach + i * s);
            bool flush = i >= k && !flat;
            long level = flush || flat ? 0 : (raiseInner ? k - 1 - i : i);
            double bse = deckH(x) + level * Geometry.SuperfireStep(th);
            string mid = i < names.Count ? names[i] : $"{names[0]}{i + 1}";
            if (!lay.Free(Footprint.Circle(x, y, reach), 0.4, ignore))
            {
                lay.Fail("length", $"{label} mount {mid} ({gun.CalibreMm} mm) does not fit at {x:F0} m: " +
                                   "the deck is taken there. Use fewer or smaller guns.");
                continue;
            }
            if (Math.Abs(y) + reach > lay.Hull.HalfWidth(x) + 0.2 && !lay.OnDeck(x, y))
            {
                lay.Fail("beam", $"{label} mount {mid} ({gun.CalibreMm} mm) is too wide for the hull at {x:F0} m.");
                continue;
            }
            double stow = Geometry.Normalize360(rest + 180);
            var m = AddMount(lay, mounts, kind, tId, t, mid, x, y, bse, flush ? stow : rest, level, armourMm, depth);
            m.Battery = battery;
            if (flush)
                m.ArcRole = "beam";
        }
    }

    /// <summary>perSide mounts on each side from candidate slots, in order of preference.</summary>
    public static long SidePairs(Layout lay, List<Mount> mounts, OrderedDictionary<string, TurretType> turretTypes, string kind, string tId,
        TurretType t, long perSide, IEnumerable<Slot> cands, string prefix, double? pitch = null, double armourMm = 25.0,
        double depth = 10.0, string label = "Secondary", IReadOnlyCollection<string>? ignore = null)
    {
        if (perSide == 0)
            return 0;
        turretTypes[tId] = t;
        double reach = kind != "torpedo" ? BodyReach(t) : t.BarrelLen / 2 + 0.3;
        double pt = pitch is double p && p != 0 ? p : 2.1 * reach + 1.0;
        bool guns = kind != "torpedo";
        double pitchStowed = Geometry.TurretReach(t) + reach + 0.4;
        var placed = new List<double>();
        foreach (var c in cands)
        {
            double x = c.X, y = c.Y, bse = c.Base, yPort = c.PortY;
            if (placed.Count >= perSide)
                break;
            if (placed.Any(px => Math.Abs(x - px) < pt || (guns && (x >= 0) == (px >= 0) && Math.Abs(x - px) < pitchStowed)))
                continue;
            var fps = new[] { Footprint.Circle(x, y, reach), Footprint.Circle(x, yPort, reach) };
            if (Layout.Overlap(fps[0], fps[1], 0.4) || !fps.All(fp => lay.Free(fp, 0.4, ignore)))
                continue;
            if (guns)
            {
                var band = BarrelBand(bse, bse + Geometry.TurretHeight(t), t);
                if (!new[] { (1.0, y), (-1.0, yPort) }.All(sy =>
                        lay.FreeAt(BarrelFootprint(t, x, sy.Item2, StowBearing(x, sy.Item1, 90.0)), band.Lo, band.Hi, 0.2, ignore)))
                    continue;
            }
            int k = placed.Count + 1;
            foreach (var (side, yy) in new[] { (1, y), (-1, yPort) })
            {
                string mid = $"{prefix}{k}{(side > 0 ? "S" : "P")}";
                if (guns)
                {
                    var m = AddMount(lay, mounts, kind, tId, t, mid, x, yy, bse, StowBearing(x, side, 90.0), armourMm: armourMm,
                        depth: depth, sideMount: true);
                    if (kind == "secondary")
                        m.Battery = prefix;
                }
                else
                    AddMount(lay, mounts, kind, tId, t, mid, x, yy, bse, 90 * side, armourMm: armourMm, depth: depth);
            }
            placed.Add(x);
        }
        if (placed.Count < perSide)
            lay.Fail("length", $"Only {placed.Count} of {perSide} {label.ToLowerInvariant()} mounts per side fit.");
        return placed.Count;
    }

    /// <summary>The secondary armament as a list of batteries (carriers, merchants), each with "where".</summary>
    public static List<BatteryInput> BatteriesOf(Design design) =>
        Batteries.SecondaryBatteries(design).Where(b => b.MountCount != 0).Select(b => b with { Where = b.Where ?? "sides" }).ToList();

    /// <summary>Warn about an odd count on a style that mounts its secondaries in pairs.</summary>
    public static void WarnUnpaired(Layout lay, IEnumerable<BatteryInput> bats)
    {
        foreach (var b in bats)
            if (b.MountCount % 2 != 0)
                lay.Warnings.Add($"secondary count {b.MountCount}: secondaries go in pairs on this style, so " +
                                 $"{b.MountsPerSide} per side ({2 * b.MountsPerSide} mounts).");
    }

    /// <summary>One end line for PlaceBatteries: where it starts and which way it runs, its y, the mounts' rest bearing,
    /// the deck height under it, and what it may overlap.</summary>
    public sealed record EndLine(double XStart, double StepDir, double Y, double Rest, Func<double, double> DeckH,
        IReadOnlyCollection<string> Ignore);

    /// <summary>Fit every secondary battery: end lines in the order they fill, sideSlots(t) -&gt; starboard slots.</summary>
    public static void PlaceBatteries(Layout lay, List<Mount> mounts, OrderedDictionary<string, TurretType> turretTypes, Design design,
        IReadOnlyList<EndLine> endLines, Func<TurretType, IEnumerable<Slot>> sideSlots, double depth = 10.0)
    {
        var cursor = endLines.Select(l => l.XStart).ToList();
        var bats = BatteriesOf(design);
        for (int k = 0; k < bats.Count; k++)
        {
            var b = bats[k];
            int first = mounts.Count;
            string prefix = Batteries.BatteryPrefix(k);
            var (tId, t) = GunType(b);
            turretTypes[tId] = t;
            long count = b.MountCount;
            long nEnd = b.Where == "ends" ? count : count % 2;
            long nSide = b.Where == "ends" ? 0 : count / 2;
            if (nEnd != 0 && endLines.Count == 0)
            {
                lay.Fail(null, $"No end positions for {nEnd} {b.CalibreMm} mm mount(s): give them in pairs.");
                nEnd = 0;
            }
            long made = 0;
            for (int i = 0; i < endLines.Count; i++)
            {
                var ln = endLines[i];
                long n = nEnd / endLines.Count + (i < nEnd % endLines.Count ? 1 : 0);
                if (n == 0)
                    continue;
                GunLine(lay, mounts, turretTypes, b, n, null, true, "secondary",
                    Enumerable.Range(0, (int)n).Select(j => $"{prefix}{made + j + 1}").ToList(), cursor[i], ln.StepDir, ln.Y, ln.Rest,
                    ln.DeckH, armourMm: b.ArmourMm!.Value, depth: depth, label: "Secondary", ignore: ln.Ignore, battery: prefix);
                cursor[i] += ln.StepDir * (2 * BodyReach(t) + (n - 1) * (2.2 * t.R + 3.0) + 1.0);
                made += n;
            }
            if (nSide != 0)
                SidePairs(lay, mounts, turretTypes, "secondary", tId, t, nSide, sideSlots(t), prefix, armourMm: b.ArmourMm!.Value, depth: depth);
            foreach (var m in mounts.Skip(first))
                m.Material = b.Material;
        }
    }

    public static (string Id, TurretType T) TorpedoType(TorpedoInput tp, bool fixed_ = false) => Geometry.MakeTorpedoType(tp.Tubes ?? 4, fixed_);

    /// <summary>Fixed torpedo tubes in port/starboard pairs, toed out by toeDeg from dead ahead.</summary>
    public static long FixedTubePairs(Layout lay, List<Mount> mounts, OrderedDictionary<string, TurretType> turretTypes, TorpedoInput tp,
        IEnumerable<double> xs, Func<double, TurretType, double> yOfX, double bse = 0.2, double toeDeg = 8.0)
    {
        var (tId, t) = TorpedoType(tp, true);
        turretTypes[tId] = t;
        long want = ((tp.Mounts ?? 0) + 1) / 2;
        long placed = 0;
        foreach (var x in xs)
        {
            if (placed >= want)
                break;
            double y = yOfX(x, t);
            var fps = new[] { TubeFootprint(t, x, y, toeDeg), TubeFootprint(t, x, -y, -toeDeg) };
            if (y <= 0 || !fps.All(fp => lay.Free(fp, 0.2)))
                continue;
            placed++;
            foreach (int side in new[] { 1, -1 })
                AddMount(lay, mounts, "torpedo", tId, t, $"T{placed}{(side > 0 ? "S" : "P")}", x, side * y, bse, side * toeDeg);
        }
        if (placed < want)
            lay.Fail("length", $"Only {placed} of {want} pairs of torpedo tubes fit along the sides.");
        return placed;
    }

    const double AaCell = 8.0;
    static readonly Dictionary<string, double> AaTubT = new() { ["quad40"] = 3.0, ["twin40"] = 1.5, ["single20"] = 0.3 };

    /// <summary>AA mounts in pairs from slots in order of preference (a single slot takes one). ignore: ids to
    /// disregard, as a function of the slot's base; layerOf: the drawing layer for a base.</summary>
    public static long PlaceAa(Layout lay, List<AaMount> aaOut, string kind, long count, IReadOnlyList<Slot> cands,
        double? spacing = null, Func<double, IReadOnlyCollection<string>>? ignore = null, Func<double, string>? layerOf = null)
    {
        double rr = Geometry.AaCfg[kind].R;
        double sp = spacing ?? (kind == "quad40" ? 3.0 : 2.2);
        var grid = new Dictionary<long, List<Footprint>>();
        double rMax = 0.0;

        void File(Footprint fp)
        {
            long c = (long)Math.Floor(fp.X / AaCell);
            if (!grid.TryGetValue(c, out var l))
                grid[c] = l = [];
            l.Add(fp);
            rMax = Math.Max(rMax, fp.R);
        }

        foreach (var a in aaOut)
            File(Footprint.Circle(a.X, a.Y, Geometry.AaCfg[a.Type].R));

        bool Hits(Footprint fp, Footprint o) => Math.Abs(fp.X - o.X) < fp.R + o.R + sp && Layout.Overlap(fp, o, sp);

        // does the slot's mount (or pair) fit, clear of the mounts placed and of `also`? Appends them to `into` if so
        bool Fits(Slot c, List<(Footprint Fp, double Base, double Cx)> into, List<(Footprint Fp, double Base, double Cx)>? also = null)
        {
            var f0 = Footprint.Circle(c.X, c.Y, rr);
            var f1 = c.Single ? null : Footprint.Circle(c.X, c.PortY, rr);
            if (f1 != null && Layout.Overlap(f0, f1, sp))
                return false;
            for (int k = 0; k < (f1 is null ? 1 : 2); k++)
            {
                var fp = k == 0 ? f0 : f1!;
                double reach = fp.R + rMax + sp;
                for (long g = (long)Math.Floor((fp.X - reach) / AaCell); g <= (long)Math.Floor((fp.X + reach) / AaCell); g++)
                    if (grid.TryGetValue(g, out var l))
                        foreach (var o in l)
                            if (Hits(fp, o))
                                return false;
                if (also != null)
                    foreach (var o in also)
                        if (Hits(fp, o.Fp))
                            return false;
            }
            var ign = ignore?.Invoke(c.Base) ?? [];
            if (!lay.FreeAt(f0, c.Base, c.Base + 2.0, 0.4, ign) || f1 != null && !lay.FreeAt(f1, c.Base, c.Base + 2.0, 0.4, ign)
                || !lay.Clear(f0, c.Base + 2.0) || f1 != null && !lay.Clear(f1, c.Base + 2.0))
                return false;
            into.Add((f0, c.Base, c.X));
            if (f1 != null)
                into.Add((f1, c.Base, c.X));
            return true;
        }

        void Put(List<(Footprint Fp, double Base, double Cx)> ms)
        {
            foreach (var (fp, bse, cx) in ms)
            {
                File(fp);
                double y = fp.Y;
                string aid = $"AA{aaOut.Count + 1}";
                long d = y == 0 && cx < 0 ? 180 : y > 0 ? 90 : y < 0 ? -90 : 0;
                aaOut.Add(new AaMount { Id = aid, Type = kind, X = fp.X, Y = y, Dir = d, Base = bse, Layer = layerOf != null ? layerOf(bse) : "base" });
                lay.Occupy(fp, bse, bse + 2.0, aid);
                lay.Weights.Add(new Weight(aid, "armament", Batteries.AaT[kind] + (bse > 0.5 ? AaTubT[kind] : 0.0), fp.X,
                    ZRel.Deck(bse + 1.0)));
            }
        }

        long placed = 0;
        bool lonely = false;
        using var _g = Scratch<(Footprint Fp, double Base, double Cx)>.Rent(out var got);
        using var _m = Scratch<(Footprint Fp, double Base, double Cx)>.Rent(out var mate);
        for (int i = 0; i < cands.Count; i++)
        {
            var c = cands[i];
            long left = count - placed;
            if (left <= 0)
                break;
            if (!c.Single && left < 2)
                continue;
            if (c.Single && left % 2 == 0 && lonely)
                continue;
            got.Clear();
            if (!Fits(c, got))
                continue;
            if (c.Single && left % 2 == 0)
            {
                mate.Clear();
                bool found = false;
                for (int j = i + 1; j < cands.Count && !found; j++)
                    found = cands[j].Single && Fits(cands[j], mate, got);
                if (!found)
                {
                    lonely = true;
                    continue;
                }
                got.AddRange(mate);
            }
            Put(got);
            lonely = false;
            placed += got.Count;
        }
        if (placed < count)
            lay.Fail("length", $"Only {placed} of {count} {(kind == "quad40" ? "heavy" : "light")} AA mounts fit.");
        return placed;
    }

    /// <summary>The main battery as two lines, fore and aft: how many turrets in each and how many of them step up.
    /// Null when it has no end turrets.</summary>
    public static (BatteryInput Gun, (int Count, int Stepped) Fore, (int Count, int Stepped) Aft)? GunGroups(Design design)
    {
        var main = design.MainBatteries.FirstOrDefault();
        if (main is null || (main.Fore ?? 0) + (main.Aft ?? 0) == 0)
            return null;
        var (sf, sa) = Layout.SteppedCounts(main);
        return (main, (main.Fore ?? 0, sf), (main.Aft ?? 0, sa));
    }
}
