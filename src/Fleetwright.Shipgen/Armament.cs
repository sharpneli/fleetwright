namespace Fleetwright.Shipgen;

/// <summary>armament: style-neutral placement of guns, torpedo mounts and AA, and the mount records that hitboxes,
/// arcs and the renderer read.</summary>
public static class Armament
{
    /// <summary>Radius of the turret body and its ears (not the barrels): its footprint on deck.</summary>
    public static double BodyReach(PyDict t)
    {
        return Math.Max(t.F("r"), Geometry.TurretReach(t, 0.0));
    }

    /// <summary>Rest bearing of a side mount at x on `side` (+1 starboard), whose arc is +-half about its own beam.</summary>
    public static double StowBearing(double x, double side, double half) => 90.0 * side + (x >= 0 ? -1.0 : 1.0) * side * half;

    /// <summary>Axis-aligned box around a mount's barrels (as shown) trained to `bearing`.</summary>
    public static Footprint BarrelFootprint(PyDict t, double x, double y, double bearing)
    {
        double c = Math.Cos(double.DegreesToRadians(bearing)), s = Math.Sin(double.DegreesToRadians(bearing));
        double x0 = 0, y0 = 0, x1 = 0, y1 = 0;
        bool first = true;
        foreach (var poly in Geometry.TurretShapesOf(t).Barrels)
            foreach (var p in poly)
            {
                double px = x + p.X * c - p.Y * s, py = y + p.X * s + p.Y * c;
                if (first)
                {
                    (x0, y0, x1, y1) = (px, py, px, py);
                    first = false;
                    continue;
                }
                if (px < x0)
                    x0 = px;
                if (px > x1)
                    x1 = px;
                if (py < y0)
                    y0 = py;
                if (py > y1)
                    y1 = py;
            }
        if (first)
            throw new PyValueError("min() iterable argument is empty");
        return Footprint.Rect(x0, y0, x1, y1);
    }

    /// <summary>The heights (lo, hi) above the main deck that a mount's barrels take, about their axis.</summary>
    public static (double Lo, double Hi) BarrelBand(double bse, double top, PyDict t)
    {
        double axis = bse + 0.55 * (top - bse);
        double hw = t.F("barrel_w") / 2 + 0.1;
        return (axis - hw, axis + hw);
    }

    public static (string Id, PyDict T) GunType(BatteryInput gun) => Geometry.BatteryType(gun);

    /// <summary>Axis-aligned box around a fixed tube laid along `bearing`.</summary>
    static Footprint TubeFootprint(PyDict t, double x, double y, double bearing)
    {
        double a = double.DegreesToRadians(bearing);
        double hl = t.F("barrel_len") / 2 + 0.2, hw = ((t.F("barrels") - 1) * t.F("spacing") + t.F("barrel_w")) / 2 + 0.2;
        double ex = Math.Abs(hl * Math.Cos(a)) + Math.Abs(hw * Math.Sin(a)), ey = Math.Abs(hl * Math.Sin(a)) + Math.Abs(hw * Math.Cos(a));
        return Footprint.Rect(x - ex, y - ey, x + ex, y + ey);
    }

    /// <summary>One mount: footprint, weights and the mount record, for every style.</summary>
    public static PyDict AddMount(Layout lay, List<PyDict> mounts, string kind, string tId, PyDict t, string mid, double x, double y,
        double bse, object rest, long level = 0, double armourMm = 0.0, double depth = 10.0, double? top = null,
        double? footprintR = null, string label = "Mount", double deck = 0.0, (string Key, object? Value)[]? extra = null)
    {
        extra ??= [];
        double th = kind != "torpedo" || t.B("fixed_tube") ? Geometry.TurretHeight(t) : 1.1;
        double tp = top ?? bse + th;
        var m = PyDict.Of(("id", mid), ("kind", kind), ("type", tId), ("t", t), ("x", x), ("y", y), ("level", level),
            ("base", bse), ("top", tp), ("rest", rest), ("armour_mm", armourMm));
        foreach (var (k, v) in extra)
            m[k] = v;
        mounts.Add(m);
        if (t.B("fixed_tube"))
        {
            m["fixed"] = rest;
            lay.Occupy(TubeFootprint(t, x, y, Py.ToDouble(rest)), bse, tp, mid);
        }
        else
        {
            double r = footprintR ?? (kind != "torpedo" ? BodyReach(t) : t.F("barrel_len") / 2 + 0.3);
            lay.Occupy(Footprint.Circle(x, y, r), bse, tp, mid);
            if (m.B("side_mount"))
            {
                var fp = BarrelFootprint(t, x, y, Py.ToDouble(rest));
                var (lo, hi) = BarrelBand(bse, tp, t);
                lay.Occupy(fp, lo, hi, mid);
                lay.Overhangs.Add(fp);
            }
        }
        if (kind == "torpedo")
        {
            lay.Weights.Add(new Weight(mid, "armament", Batteries.TorpedoWeight(t.F("barrels"), t.B("fixed_tube")), x, ZRel.Deck(bse + 0.5)));
            return m;
        }
        var (tw, bw, aw) = Batteries.MountWeights(t, armourMm, depth, level, deck);
        lay.Weights.Add(new Weight($"{label} {mid}", "armament", tw, x, ZRel.Deck((bse + tp) / 2)));
        lay.Weights.Add(new Weight($"Magazine {mid}", "armament", aw, x, ZRel.Frac(0.25)));
        if (bw != 0 && bse > 0.5)
            lay.Weights.Add(new Weight($"Barbette {mid}", "armour", bw, x, ZRel.Frac(0.75)));
        return m;
    }

    /// <summary>n mounts in a line from x_start in direction step_dir, each superfiring over the one before it.</summary>
    /// <summary>stepped: how many step up, superfiring (all of them when null); flat: none do, all on the deck.</summary>
    public static void GunLine(Layout lay, List<PyDict> mounts, PyDict turretTypes, BatteryInput gun, long n, long? stepped, bool flat,
        string kind, IReadOnlyList<string> names, double xStart, double stepDir, double y, long rest, Func<double, double> deckH,
        bool raiseInner = false, double armourMm = 0.0, double depth = 10.0, string label = "Main",
        IReadOnlyCollection<string>? ignore = null, string? battery = null)
    {
        if (n == 0)
            return;
        var (tId, t) = GunType(gun);
        turretTypes[tId] = t;
        double r = t.F("r"), th = Geometry.TurretHeight(t);
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
            long stow = (long)Geometry.Normalize360(rest + 180);
            AddMount(lay, mounts, kind, tId, t, mid, x, y, bse, flush ? stow : rest, level, armourMm, depth,
                extra: battery != null ? [("battery", battery)] : null);
            if (flush)
                mounts[^1]["arc_role"] = "beam";
        }
    }

    /// <summary>per_side mounts on each side from candidates (x, y, base[, y_port]), in order of preference.</summary>
    public static long SidePairs(Layout lay, List<PyDict> mounts, PyDict turretTypes, string kind, string tId, PyDict t, long perSide,
        IEnumerable<object?[]> cands, string prefix, double? pitch = null, double armourMm = 25.0, double depth = 10.0,
        string label = "Secondary", IReadOnlyCollection<string>? ignore = null)
    {
        if (perSide == 0)
            return 0;
        turretTypes[tId] = t;
        double reach = kind != "torpedo" ? BodyReach(t) : t.F("barrel_len") / 2 + 0.3;
        double pt = pitch is double p && p != 0 ? p : 2.1 * reach + 1.0;
        bool guns = kind != "torpedo";
        double pitchStowed = Geometry.TurretReach(t) + reach + 0.4;
        var placed = new List<double>();
        foreach (var c in cands)
        {
            double x = Py.ToDouble(c[0]), y = Py.ToDouble(c[1]), bse = Py.ToDouble(c[2]);
            double yPort = c.Length > 3 ? Py.ToDouble(c[3]) : -y;
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
                    AddMount(lay, mounts, kind, tId, t, mid, x, yy, bse, StowBearing(x, side, 90.0), armourMm: armourMm, depth: depth,
                        extra: kind == "secondary" ? [("side_mount", true), ("battery", prefix)] : [("side_mount", true)]);
                else
                    AddMount(lay, mounts, kind, tId, t, mid, x, yy, bse, (long)(90 * side), armourMm: armourMm, depth: depth);
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

    /// <summary>One end line for place_batteries: (x_start, step_dir, y, rest, deck_h, ignore).</summary>
    public sealed record EndLine(double XStart, double StepDir, double Y, long Rest, Func<double, double> DeckH,
        IReadOnlyCollection<string> Ignore);

    /// <summary>Fit every secondary battery: end lines in the order they fill, side_slots(t) -&gt; starboard candidates.</summary>
    public static void PlaceBatteries(Layout lay, List<PyDict> mounts, PyDict turretTypes, Design design, IReadOnlyList<EndLine> endLines,
        Func<PyDict, IEnumerable<object?[]>> sideSlots, double depth = 10.0)
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
                cursor[i] += ln.StepDir * (2 * BodyReach(t) + (n - 1) * (2.2 * t.F("r") + 3.0) + 1.0);
                made += n;
            }
            if (nSide != 0)
                SidePairs(lay, mounts, turretTypes, "secondary", tId, t, nSide, sideSlots(t), prefix, armourMm: b.ArmourMm!.Value, depth: depth);
            foreach (var m in mounts.Skip(first))
                m["material"] = b.Material;
        }
    }

    public static (string Id, PyDict T) TorpedoType(TorpedoInput tp, bool fixed_ = false) => Geometry.MakeTorpedoType(tp.Tubes ?? 4, fixed_);

    /// <summary>Fixed torpedo tubes in port/starboard pairs, toed out by toe_deg from dead ahead.</summary>
    public static long FixedTubePairs(Layout lay, List<PyDict> mounts, PyDict turretTypes, TorpedoInput tp, IEnumerable<double> xs,
        Func<double, PyDict, double> yOfX, double bse = 0.2, double toeDeg = 8.0)
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

    const double AA_CELL = 8.0;
    static readonly Dictionary<string, double> AA_TUB_T = new() { ["quad40"] = 3.0, ["twin40"] = 1.5, ["single20"] = 0.3 };

    /// <summary>AA mounts in pairs from cands (x, y, base[, y_port]) in order of preference; y == 0 or y_port null is a
    /// single mount. ignore: ids to disregard, or a function of the slot's base giving them.</summary>
    public static long PlaceAa(Layout lay, List<PyDict> aaOut, string kind, long count, IReadOnlyList<object?[]> cands,
        double? spacing = null, Func<double, IReadOnlyCollection<string>>? ignore = null, Func<double, string>? layerOf = null)
    {
        double rr = Geometry.AA_CFG[kind].R;
        double sp = spacing ?? (kind == "quad40" ? 3.0 : 2.2);
        var grid = new Dictionary<long, List<Footprint>>();
        double rMax = 0.0;

        void File(Footprint fp)
        {
            long c = (long)Math.Floor(fp.X / AA_CELL);
            if (!grid.TryGetValue(c, out var l))
                grid[c] = l = [];
            l.Add(fp);
            rMax = Math.Max(rMax, fp.R);
        }

        foreach (var a in aaOut)
            File(Footprint.Circle(a.F("x"), a.F("y"), Geometry.AA_CFG[a.S("type")].R));

        IEnumerable<Footprint> Near(Footprint fp)
        {
            double reach = fp.R + rMax + sp;
            for (long c = (long)Math.Floor((fp.X - reach) / AA_CELL); c <= (long)Math.Floor((fp.X + reach) / AA_CELL); c++)
                if (grid.TryGetValue(c, out var l))
                    foreach (var o in l)
                        yield return o;
        }

        static bool Single(object?[] c) => Py.ToDouble(c[1]) == 0 || (c.Length > 3 && c[3] is null);

        List<(Footprint Fp, double Base, double Cx)>? Fits(object?[] c, IEnumerable<Footprint>? also = null)
        {
            double cx = Py.ToDouble(c[0]), cy = Py.ToDouble(c[1]), bse = Py.ToDouble(c[2]);
            var use = Single(c) ? new[] { (cx, cy) } : [(cx, cy), (cx, c.Length > 3 ? Py.ToDouble(c[3]) : -cy)];
            var fps = use.Select(u => Footprint.Circle(u.Item1, u.Item2, rr)).ToList();
            if (fps.Count == 2 && Layout.Overlap(fps[0], fps[1], sp))
                return null;
            var alsoL = also?.ToList() ?? [];
            foreach (var fp in fps)
                foreach (var o in Near(fp).Concat(alsoL))
                    if (Math.Abs(fp.X - o.X) < fp.R + o.R + sp && Layout.Overlap(fp, o, sp))
                        return null;
            var ign = ignore?.Invoke(bse) ?? [];
            if (!fps.All(fp => lay.FreeAt(fp, bse, bse + 2.0, 0.4, ign)) || !fps.All(fp => lay.Clear(fp, bse + 2.0)))
                return null;
            return fps.Select(fp => (fp, bse, cx)).ToList();
        }

        void Put(List<(Footprint Fp, double Base, double Cx)> ms)
        {
            foreach (var (fp, bse, cx) in ms)
            {
                File(fp);
                double y = fp.Y;
                string aid = $"AA{aaOut.Count + 1}";
                long d = y == 0 && cx < 0 ? 180 : y > 0 ? 90 : y < 0 ? -90 : 0;
                aaOut.Add(PyDict.Of(("id", aid), ("type", kind), ("x", fp.X), ("y", y), ("dir", d), ("base", bse),
                    ("layer", layerOf != null ? layerOf(bse) : "base")));
                lay.Occupy(fp, bse, bse + 2.0, aid);
                lay.Weights.Add(new Weight(aid, "armament", Batteries.AA_T[kind] + (bse > 0.5 ? AA_TUB_T[kind] : 0.0), fp.X,
                    ZRel.Deck(bse + 1.0)));
            }
        }

        long placed = 0;
        bool lonely = false;
        for (int i = 0; i < cands.Count; i++)
        {
            var c = cands[i];
            long left = count - placed;
            if (left <= 0)
                break;
            if (!Single(c) && left < 2)
                continue;
            if (Single(c) && left % 2 == 0 && lonely)
                continue;
            var got = Fits(c);
            if (got is null || got.Count == 0)
                continue;
            if (Single(c) && left % 2 == 0)
            {
                List<(Footprint, double, double)>? mate = null;
                for (int j = i + 1; j < cands.Count; j++)
                {
                    if (!Single(cands[j]))
                        continue;
                    var m = Fits(cands[j], got.Select(g => g.Fp));
                    if (m is { Count: > 0 })
                    {
                        mate = m;
                        break;
                    }
                }
                if (mate is null)
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
