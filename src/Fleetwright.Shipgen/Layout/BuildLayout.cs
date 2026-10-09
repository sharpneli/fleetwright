namespace Fleetwright.Shipgen;

public sealed partial class Layout
{
    /// <summary>layout.build_layout: the warship layout. See the Python for the commentary on each stage.</summary>
    public static Layout BuildLayout(PyDict design, Navarch.Result res, double shift = 0.0, double spread = 0.0)
    {
        double depth = res.Depth;
        var lay = new Layout(design);
        var hs = HullSpec(design);
        var hull = new Hull(hs);
        lay.Hull = hull;
        double L = hull.L, B = hull.B;
        var armour = design.Get("armour", new PyDict()) as PyDict ?? new PyDict();
        object? deck = design.Get("deck", L >= 150 ? "wood" : "steel");

        // ---------------- main battery groups ----------------
        var bats = Batteries.MainBatteries(design).Select((b, k) => (b, k)).Where(t => Batteries.BatteryTurrets(t.b) != 0)
            .Select(t => new Gun(t.b, t.k)).ToList();
        foreach (var g in bats)
            if (g.Spec.B("wing") && g.Spec.B("cross_deck") && !g.Echelon)
                lay.Warnings.Add($"main[{g.K}].cross_deck needs \"echelon\": true (an abreast pair blocks each other's " +
                                 "beam); the wing turrets fire on their own side only.");
        var (F, nStepF) = EndGroup(bats, "fore");
        var (A, nStepA) = EndGroup(bats, "aft");
        var M = bats.SelectMany(g => Enumerable.Repeat(g, (int)g.Spec.I("mid", 0))).ToList();
        var WA = bats.Where(g => !g.Echelon).SelectMany(g => Enumerable.Repeat(g, (int)g.Spec.I("wing", 0))).ToList();
        var WE = bats.Where(g => g.Echelon).SelectMany(g => Enumerable.Repeat(g, (int)g.Spec.I("wing", 0))).ToList();
        int nf = F.Count, na = A.Count, nm = M.Count, nw = WA.Count + WE.Count;
        bool flushF = nf > Math.Max(nStepF, 1), flushA = na > Math.Max(nStepA, 1);
        var gRef = nw > 0 ? WA.Concat(WE).First() : null;

        double Wy(Gun g, double y) => g.Reach == gRef!.Reach ? y : y + gRef.Reach - g.Reach;

        PlanMachinery(lay, design, res, hull);
        var plant = lay.Geo.Plant!;
        double lb = Clamp(0.05 * L + 2, 7, 18);
        double la = AftControl(design) ? 0.045 * L + 2 : 0.0;
        var raisedIn = (design.DOr("hull").Or("raised", null) as List<object?> ?? []).Cast<PyDict>().ToList();
        double fwdDeck = LEVEL_H * raisedIn.Where(q => RaisedCovers(q, "fore_group")).Select(q => q.F("decks")).DefaultIfEmpty(0.0).Max();
        double aftDeck = LEVEL_H * raisedIn.Where(q => RaisedCovers(q, "aft_group")).Select(q => q.F("decks")).DefaultIfEmpty(0.0).Max();
        int? fwdTier = nf != 0 ? Math.Min(nf, Math.Max(nStepF, 1)) - 1 : null;
        double? fwdRoof = fwdTier is int ft ? fwdDeck + 1.2 + TierSteps(F, ft) + F[ft].Th : null;
        long nbNeed = BridgeLevel(fwdRoof);
        int? aftTier = na != 0 ? Math.Min(na, Math.Max(nStepA, 1)) - 1 : null;
        long naLvl = BridgeLevel(aftTier is int atr ? aftDeck + 1.2 + TierSteps(A, atr) + A[atr].Th : null);
        long over = LevelsOverBridge(design);
        long nTower = TowerLevels(design, nbNeed + over);
        long nb = Math.Min(nTower, Math.Max(nbNeed, nTower - over));
        if (nb < nbNeed)
            lay.Warnings.Add(
                $"The bridge (level {nb}, its deck {LEVEL_H * (nb - 1):F1} m above the main deck) cannot see over " +
                $"turret {TurretName("ABC", fwdTier!.Value)}'s roof ({fwdRoof!.Value:F1} m): superstructure.tower_levels " +
                $"{nbNeed} or more lifts it clear.");
        lay.Geo.Bridge = PyDict.Of(("level", nb), ("floor", LEVEL_H * (nb - 1)), ("need", nbNeed), ("tower", nTower),
            ("turret_roof", fwdRoof));
        double hood = FireControl.Spec(design).D("main").F("directors") != 0 ? FireControl.HOOD_H : 0.0;
        var secs = Batteries.SecondaryBatteries(design).Select((b, k) =>
        {
            var c = b.Copy();
            c["prefix"] = Batteries.BatteryPrefix(k);
            return c;
        }).ToList();
        Armament.WarnUnpaired(lay, secs);
        var tp = design.Or("torpedoes", null) as PyDict ?? new PyDict();
        long ntp = Py.ToLong(tp.Get("mounts", 0L));
        string? ttId = null;
        PyDict? tt = null;
        if (ntp != 0)
            (ttId, tt) = Geometry.MakeTorpedoType(tp.Get("tubes", 4L));
        double tSweep = ntp != 0 ? tt!.F("barrel_len") / 2 + 0.3 : 0.0;
        bool tEdges = ntp != 0 && B / 2 - tt!.F("r") - 0.8 >= tSweep;

        // ---------------- the middle's plan ----------------
        var segs = PlanSegments(plant).Select(s => (Kind: s.Kind, Len: s.Len)).ToList();
        var segGroup = Enumerable.Range(0, segs.Count).ToList();
        int nGaps = nm + WE.Count;
        string mainKind = segs.Any(s => s.Kind == "boiler") ? "boiler" : "engine";

        List<int> Candidates() => Enumerable.Range(1, Math.Max(0, segs.Count - 1))
            .Where(i => segs[i - 1].Kind == mainKind || segs[i].Kind == mainKind).ToList();

        bool BetweenGroups(int i)
        {
            int a = i - 1, b = i;
            while (a >= 0 && segs[a].Kind == "bunker")
                a--;
            while (b < segs.Count && segs[b].Kind == "bunker")
                b++;
            return a >= 0 && b < segs.Count && segs[a].Kind == mainKind && segs[b].Kind == mainKind && segGroup[a] != segGroup[b];
        }

        List<int> SpreadOver(List<int> cands, int n)
        {
            var set = new SortedSet<int>(Enumerable.Range(0, n).Select(k => cands[Math.Min(cands.Count - 1, (int)(long)((k + 0.5) * cands.Count / n))]));
            var out_ = set.ToList();
            foreach (var c in cands)
            {
                if (out_.Count >= n)
                    break;
                if (!out_.Contains(c))
                {
                    out_.Add(c);
                    out_.Sort();
                }
            }
            return out_;
        }

        while (Candidates().Count < nGaps)
        {
            int i = Enumerable.Range(0, segs.Count).Where(i => segs[i].Kind == mainKind).MaxBy(i => segs[i].Len);
            var half = (mainKind, segs[i].Len / 2);
            segs.RemoveAt(i);
            segs.InsertRange(i, [half, half]);
            int sg = segGroup[i];
            segGroup.RemoveAt(i);
            segGroup.InsertRange(i, [sg, sg]);
        }
        var cands0 = Candidates();
        var pref = nGaps != 0 ? cands0.Where(BetweenGroups).ToList() : [];
        List<int> gaps;
        if (pref.Count >= nGaps)
            gaps = nGaps != 0 ? SpreadOver(pref, nGaps) : [];
        else
        {
            var rest = cands0.Where(c => !pref.Contains(c)).ToList();
            gaps = pref.Concat(SpreadOver(rest, nGaps - pref.Count)).ToList();
            gaps.Sort();
        }
        var gapKind = new Dictionary<int, string>();
        var gapGun = new Dictionary<int, Gun>();
        var tGaps = nm != 0 ? Enumerable.Range(0, nm).Select(k => gaps[(int)(long)Math.Round((k + 0.5) * gaps.Count / nm - 0.5)]).ToList() : [];
        var tLeft = M.ToList();
        var wLeft = WE.ToList();
        foreach (var g in gaps)
        {
            gapKind[g] = tGaps.Contains(g) ? "T" : "W";
            var src = tGaps.Contains(g) ? tLeft : wLeft;
            gapGun[g] = src[0];
            src.RemoveAt(0);
        }
        var waFront = Enumerable.Range(0, WA.Count).Where(k => k % 2 == 0).Select(k => WA[k]).Reverse().ToList();
        var waBack = Enumerable.Range(0, WA.Count).Where(k => k % 2 == 1).Select(k => WA[k]).ToList();
        var groupedWings = waFront.Select((g, k) => ($"W{k + 1}", "fore", g))
            .Concat(waBack.Select((g, k) => ($"W{waFront.Count + WE.Count + k + 1}", "aft", g))).ToList();
        var magPlan = MagazinePlan(design, groupedWings);
        var magL = magPlan.ToDictionary(kv => kv.Key, kv => kv.Value.Count > 0
            ? Ordnance.ZoneLength(kv.Value.Select(v => v.N * v.V).Sum(), plant.F("width"), plant) : 0.0);
        if (magL["fore"] > 0)
        {
            segs.Insert(0, ("magazine", magL["fore"]));
            gapKind = gapKind.ToDictionary(kv => kv.Key + 1, kv => kv.Value);
            gapGun = gapGun.ToDictionary(kv => kv.Key + 1, kv => kv.Value);
        }
        if (magL["aft"] > 0)
            segs.Add(("magazine", magL["aft"]));
        double funTop = LEVEL_H * Math.Min(nTower, 4) + FUNNEL_ABOVE;
        double belowDeck = depth - plant.F("inner_bottom") - 1.0;
        if (Py.In(res.Plant.D("tech").D("draught")["system"], "natural", "forced_boost"))
            funTop = Math.Max(funTop, STACK_NATURAL - belowDeck);
        var groups = segs.Where(s => s.Kind == "boiler").Select(s => s.Len).ToList();
        var (nfun, fw, fl) = PlanFunnels(lay, design, res, B, funTop, groups);
        var fplan = lay.Geo.FunnelPlan!;
        fw = Math.Min(fw, 0.3 * B);
        double beamTan = Math.Tan(double.DegreesToRadians(90.0 - Arcs.ARC_BEAM));
        double fMin = fl + (ntp != 0 && !tEdges ? 9.6 : 2.0);

        var seq = new List<string>();
        var segOf = new List<int?>();
        var widths0 = new List<double?>();
        var seqGun = new List<Gun?>();
        int gI = 0;
        var fCounts = fplan.L("counts").Select(c => Py.ToLong(c)).ToList();
        for (int si = 0; si < segs.Count; si++)
        {
            var (kind, segL) = segs[si];
            if (gapKind.TryGetValue(si, out var gk))
            {
                seq.Add(gk);
                segOf.Add(null);
                widths0.Add(null);
                seqGun.Add(gapGun[si]);
            }
            if (kind == "boiler")
            {
                long nG = fCounts[gI];
                gI++;
                for (long j = 0; j < nG; j++)
                {
                    seq.Add("F");
                    segOf.Add(si);
                    widths0.Add(Math.Max(segL / nG, fMin));
                    seqGun.Add(null);
                }
            }
            else
            {
                seq.Add("E");
                segOf.Add(si);
                widths0.Add(segL);
                seqGun.Add(null);
            }
        }
        if (groups.Count == 0 && nfun != 0)
        {
            var eItems = Enumerable.Range(0, seq.Count).Where(i => seq[i] == "E" && segs[segOf[i]!.Value].Kind == "engine").ToList();
            if (eItems.Count == 0)
                eItems = [0];
            for (long j = 0; j < nfun; j++)
            {
                seq.Insert(eItems[0], "F");
                segOf.Insert(eItems[0], null);
                widths0.Insert(eItems[0], fMin);
                seqGun.Insert(eItems[0], null);
            }
        }
        var lead = new List<int>();
        var trail = new List<int>();
        if (groups.Count > 0)
        {
            while (seq.Count > 0 && seq[0] == "E")
            {
                lead.Add(segOf[0]!.Value);
                segOf.RemoveAt(0);
                seq.RemoveAt(0);
                widths0.RemoveAt(0);
                seqGun.RemoveAt(0);
            }
            while (seq.Count > 0 && seq[^1] == "E")
            {
                trail.Insert(0, segOf[^1]!.Value);
                segOf.RemoveAt(segOf.Count - 1);
                seq.RemoveAt(seq.Count - 1);
                widths0.RemoveAt(widths0.Count - 1);
                seqGun.RemoveAt(seqGun.Count - 1);
            }
        }
        for (int k = 0; k < WA.Count; k++)
        {
            int at = k % 2 == 0 ? 0 : seq.Count;
            seq.Insert(at, "W");
            segOf.Insert(at, null);
            widths0.Insert(at, null);
            seqGun.Insert(at, WA[k]);
        }
        var core = Enumerable.Range(0, seq.Count).Where(i => !(seq[i] == "W" && !seqGun[i]!.Echelon)).ToList();
        double underBridge = 0.0;
        int? ubSeg = null;
        if (core.Count > 0 && seq[core[0]] == "F" && segOf[core[0]] is int us0)
        {
            ubSeg = us0;
            var fItems = Enumerable.Range(0, seq.Count).Where(i => seq[i] == "F" && segOf[i] == ubSeg).ToList();
            double ubL = segs[us0].Len;
            underBridge = Math.Max(0.0, Math.Min(ubL - fItems.Count * fMin, BRIDGE_OVER_BOILERS * (lb + 1.5)));
            foreach (var i in fItems)
                widths0[i] = Math.Max(fMin, (ubL - underBridge) / fItems.Count);
        }
        double leadL = lead.Select(si => segs[si].Len).Sum() + underBridge;
        double trailL = trail.Select(si => segs[si].Len).Sum();
        double w2 = Clamp(0.36 * B, 4.5, 12);
        var endsHalf = new Dictionary<int, double> { [-1] = w2 / 2, [seq.Count] = la != 0 ? 0.14 * B : 0.0 };

        double HalfOf(int j, double y)
        {
            if (endsHalf.TryGetValue(j, out var v))
                return v;
            if (seq[j] == "W")
            {
                var g = seqGun[j]!;
                return Wy(g, y) + g.Reach;
            }
            return seq[j] == "F" ? fw / 2 : 0.0;
        }

        double WingGap(Gun g, double y, double half)
        {
            y = Wy(g, y);
            if (half >= y - 0.2)
                return g.RR + 0.5;
            return Math.Max(0.5, Math.Sqrt(Math.Max(0.0, Math.Pow(g.Reach + 0.5, 2) - Math.Pow(y - half, 2))));
        }

        double WingStagger(Gun g, double y)
        {
            double reach = g.Reach;
            y = Wy(g, y);
            if (reach - y > y - 0.3)
                return g.RR + reach + 0.5;
            double s = Math.Max(2 * reach + 1.0, Math.Sqrt(Math.Max(0.0, Math.Pow(2 * reach + 0.5, 2) - 4 * y * y)));
            if (g.Cross)
            {
                double c = double.DegreesToRadians(Arcs.ARC_CROSS);
                s = Math.Max(s, (reach + 0.5 + 2 * y * Math.Sin(c)) / Math.Cos(c));
            }
            return s;
        }

        double WingSide(int i, int step, double y)
        {
            var w = seqGun[i]!;
            int j = i + step;
            double g = WingGap(w, y, HalfOf(j, y));
            while (0 <= j && j < seq.Count && (seq[j] == "F" || seq[j] == "E"))
                j += step;
            g = Math.Max(g, WingGap(w, y, HalfOf(j, y)));
            if ((step < 0 && nf != 0) || (step > 0 && na != 0))
            {
                var e = step < 0 ? F[^1] : A[^1];
                bool flush = step < 0 ? flushF : flushA;
                double beyond = (flush ? e.RR + 1.0 : e.R + e.Gap) + (step < 0 ? lb + 1.5 : (la != 0 ? la + 1.5 : 1.0));
                double yw = Wy(w, y);
                double need = e.Reach > yw - 0.3 || w.Cross ? w.RR + e.Reach + 0.5
                    : Math.Sqrt(Math.Max(0.0, Math.Pow(w.Reach + e.Reach + 0.5, 2) - yw * yw));
                g = Math.Max(g, need - beyond);
            }
            if (w.Cross)
            {
                int jj = i + step;
                double openL = 0.0;
                while (0 <= jj && jj < seq.Count && seq[jj] == "E")
                {
                    openL += widths0[jj]!.Value;
                    jj += step;
                }
                if (0 <= jj && jj < seq.Count && (HalfOf(jj, y) > 0 || seq[jj] == "T"))
                    g = Math.Max(g, w.RR + 0.5 + (seq[jj] == "T" ? seqGun[jj]!.Reach : 0.0) - openL);
            }
            return g;
        }

        (double Ef, double Ea) CrossEnds(double y)
        {
            var out_ = new List<double>();
            foreach (var (step, order) in new[] { (-1, Enumerable.Range(0, seq.Count)), (1, Enumerable.Range(0, seq.Count).Reverse()) })
            {
                int? k = order.Cast<int?>().FirstOrDefault(kk => seq[kk!.Value] != "E");
                if (k is null || seq[k.Value] != "W" || !seqGun[k.Value]!.Cross || HalfOf(step < 0 ? -1 : seq.Count, y) <= 0)
                {
                    out_.Add(0.0);
                    continue;
                }
                double openL = ((step < 0 ? Enumerable.Range(0, k.Value) : Enumerable.Range(k.Value + 1, seq.Count - k.Value - 1))
                    .Select(e => widths0[e]!.Value)).Sum();
                out_.Add(Math.Max(0.0, seqGun[k.Value]!.RR + 0.5 - WingSide(k.Value, step, y) - openL));
            }
            return (out_[0], out_[1]);
        }

        Dictionary<int, double> WingWidths(double y) =>
            Enumerable.Range(0, seq.Count).Where(i => seq[i] == "W").ToDictionary(i => i,
                i => WingSide(i, -1, y) + WingSide(i, 1, y) + (seqGun[i]!.Echelon ? WingStagger(seqGun[i]!, y) : 0.0));

        List<double> PlanWidths(double y)
        {
            var ww = WingWidths(y);
            var out_ = new List<double>();
            for (int i = 0; i < seq.Count; i++)
            {
                if (seq[i] == "W")
                    out_.Add(ww[i]);
                else if (seq[i] == "T")
                {
                    int stow = i + 1 < seq.Count && seq[i + 1] == "T" ? 0 : 180;
                    int j = stow == 180 ? i - 1 : i + 1;
                    double margin = Math.Max(seqGun[i]!.Reach + 1.0, HalfOf(j, y) / beamTan + 0.5);
                    out_.Add(margin + seqGun[i]!.RR + 1.5);
                }
                else
                    out_.Add(widths0[i]!.Value);
            }
            return out_;
        }

        double aftL = la != 0 ? la + 1.5 : 1.0;

        double MiddleNeeds(List<double> ws, (double Ef, double Ea) ext)
        {
            double before = core.Count > 0 ? ws.Take(core[0]).Sum() : 0.0;
            double cL = core.Select(i => ws[i]).Sum();
            var (ef, ea) = ext;
            return Math.Max(lb + 1.5 + ef + ws.Sum() + ea + aftL, Math.Max(leadL + cL + trailL + 2.0, Math.Max(lb + 1.5 + ef + before + cL + trailL + 1.0, leadL + 1.0 + ws.Sum() - before + ea + aftL)));
        }

        double y0 = nw != 0 ? B / 2 - gRef!.Reach - 0.6 : 0.0;
        double MReq = MiddleNeeds(PlanWidths(y0), CrossEnds(y0));

        var big = bats.Count > 0 ? bats.MaxBy(g => g.Reach) : null;
        if (big != null && big.Reach + 0.6 > B / 2)
            lay.Fail("beam", $"Main turrets{(bats.Count == 1 ? "" : $" of {big.Cal}")} are {2 * big.Reach:F1} m " +
                             $"across; the {B} m beam cannot carry them (needs about {2 * (big.Reach + 0.6):F1} m). " +
                             "Use fewer or smaller guns.");

        bool Fits(double x, Gun g)
        {
            if (g.Reach + 0.6 > B / 2)
                return true;
            return hull.HalfWidth(x) >= g.Reach + 0.6;
        }

        double bowPref, bowMin, stPref, stMin;
        if (nf != 0)
            (bowPref, bowMin) = (0.09 * L + 2.5 * F[0].R, 0.06 * L + 1.5 * F[0].R);
        else
            (bowPref, bowMin) = (0.14 * L, 0.06 * L);
        if (na != 0)
            (stPref, stMin) = (0.08 * L + 2.0 * A[0].R, 0.05 * L + 1.0 * A[0].R);
        else
            (stPref, stMin) = (0.12 * L, 0.05 * L);
        var offF = GroupOffsets(F);
        var offA = GroupOffsets(A);

        (List<double> Fore, List<double> Aft, double MidFwd, double MidAft) Arrangement(double bowC, double stC, double sh)
        {
            var fore = nf != 0 ? offF.Select(o => L / 2 - bowC - F[0].R + sh - o).ToList() : [];
            var aft = na != 0 ? offA.Select(o => -L / 2 + stC + A[0].R + sh + o).ToList() : [];
            int guard = 0;
            while (fore.Count > 0 && !Fits(fore[0], F[0]) && fore[0] > 0 && guard < 4000)
            {
                fore = fore.Select(x => x - 0.25).ToList();
                guard++;
            }
            guard = 0;
            while (aft.Count > 0 && !Fits(aft[0], A[0]) && aft[0] < 0 && guard < 4000)
            {
                aft = aft.Select(x => x + 0.25).ToList();
                guard++;
            }
            double midFwd = fore.Count > 0 ? (flushF ? fore[^1] - F[^1].RR - 1.0 : fore[^1] - F[^1].R - F[^1].Gap) : L / 2 - bowC + sh;
            double midAft = aft.Count > 0 ? (flushA ? aft[^1] + A[^1].RR + 1.0 : aft[^1] + A[^1].R + A[^1].Gap) : -L / 2 + stC + sh;
            return (fore, aft, midFwd, midAft);
        }

        var (fore, aft, midFwd, midAft) = Arrangement(bowPref, stPref, 0.0);
        double deficit = MReq - (midFwd - midAft);
        double bowCl = bowPref, stCl = stPref;
        if (deficit > 0)
        {
            lay.Short.Add("length");
            double sb = bowPref - bowMin, ss = stPref - stMin;
            double take = Math.Min(deficit, sb + ss);
            if (sb + ss > 0)
            {
                bowCl -= take * sb / (sb + ss);
                stCl -= take * ss / (sb + ss);
            }
            (fore, aft, midFwd, midAft) = Arrangement(bowCl, stCl, 0.0);
            deficit = MReq - (midFwd - midAft);
            if (deficit > 0.5)
                lay.Fail("length", "Not enough length amidships: the machinery, funnels and midships turrets need about " +
                                   $"{MReq:F0} m between the turret groups, but only {midFwd - midAft:F0} m is free. " +
                                   "Reduce speed, use a more compact plant, or remove a turret.");
        }
        else if (deficit < 0 && spread > 0)
        {
            bowCl -= spread * deficit * bowPref / (bowPref + stPref);
            stCl -= spread * deficit * stPref / (bowPref + stPref);
            (fore, aft, midFwd, midAft) = Arrangement(bowCl, stCl, 0.0);
        }

        bool ShiftOk(double sh)
        {
            var (f_, a_, mf, ma) = Arrangement(bowCl, stCl, sh);
            if (f_.Count > 0 && f_[0] != L / 2 - bowCl - F[0].R + sh)
                return false;
            if (a_.Count > 0 && a_[0] != -L / 2 + stCl + A[0].R + sh)
                return false;
            double frontEdge = f_.Count > 0 ? f_[0] + F[0].R : mf;
            double backEdge = a_.Count > 0 ? a_[0] - A[0].R : ma;
            return L / 2 - frontEdge >= bowMin - 1e-6 && backEdge + L / 2 >= stMin - 1e-6;
        }

        double lo = 0.0, hi = 0.0;
        while (lo > -0.3 * L && ShiftOk(lo - 0.25))
            lo -= 0.25;
        while (hi < 0.3 * L && ShiftOk(hi + 0.25))
            hi += 0.25;
        lay.ShiftRange = (lo, hi);
        shift = Clamp(shift, lo, hi);
        (fore, aft, midFwd, midAft) = Arrangement(bowCl, stCl, shift);
        lay.Geo.Shift = shift;

        // ---------------- middle: bridge, deckhouse, funnels, aft control ----------------
        double bx1 = midFwd;
        double bx0 = bx1 - lb;
        double fz0 = midAft + (la != 0 ? la + 1.5 : 1.0);
        double fz1 = bx0 - 1.5;
        double yW = nw != 0 ? Enumerable.Range(0, 21).Select(k => hull.HalfWidth(fz0 + (fz1 - fz0) * k / 20)).Min() - gRef!.Reach - 0.6 : 0.0;

        (double LoX, double HiX, double X) PlanFront(List<double> widths, double y)
        {
            double beforeCore = core.Count > 0 ? widths.Take(core[0]).Sum() : 0.0;
            double coreL = core.Select(i => widths[i]).Sum();
            var (ef, ea) = CrossEnds(y);
            double loX = Math.Max(fz0 + ea + widths.Sum(), midAft + 1.0 + trailL + coreL + beforeCore);
            double hiX = Math.Min(fz1 - ef, midFwd - 1.0 - leadL + beforeCore);
            return (loX, hiX, hiX >= loX ? (loX + hiX) / 2 : hiX);
        }

        double WingRoom(double y)
        {
            var widths = PlanWidths(y);
            double xx = PlanFront(widths, y).X, room = B - gRef!.Reach - 0.6;
            for (int i = 0; i < seq.Count; i++)
            {
                if (seq[i] == "W")
                {
                    var g = seqGun[i]!;
                    double x = xx - WingSide(i, -1, y);
                    double hw = B;
                    foreach (var xw in g.Echelon ? new[] { x, x - WingStagger(g, y) } : [x])
                        hw = Math.Min(hw, Math.Min(hull.HalfWidth(xw + -g.Reach), Math.Min(hull.HalfWidth(xw + 0.0), hull.HalfWidth(xw + g.Reach))));
                    double lim = hw - g.Reach - 0.6;
                    room = Math.Min(room, g.Reach == gRef.Reach ? lim : lim + g.Reach - gRef.Reach);
                }
                xx -= widths[i];
            }
            return room;
        }

        if (nw != 0)
        {
            double loY = yW, hiY = Math.Min(WingRoom(yW), B / 2 - gRef!.Reach - 0.6);
            for (int it = 0; it < 8; it++)
            {
                if (hiY - loY < 0.05)
                    break;
                double midY = (loY + hiY) / 2;
                (loY, hiY) = WingRoom(midY) >= midY ? (midY, hiY) : (loY, midY);
            }
            yW = loY;
        }
        foreach (var g in WA.Concat(WE).Distinct())
        {
            if (!g.Echelon && Wy(g, yW) < g.Reach + 0.25)
            {
                lay.Fail("beam", $"Wing turrets{(bats.Count == 1 ? "" : $" of {g.Cal}")} are {2 * g.Reach:F1} m " +
                                 "across: a pair cannot stand abreast on this beam (needs about " +
                                 $"{4 * g.Reach + 1.7:F1} m). Set \"echelon\": true or use smaller guns.");
                break;
            }
            else if (Wy(g, yW) <= 0.5)
            {
                lay.Fail("beam", $"Hull too narrow for wing turrets{(bats.Count == 1 ? "" : $" of {g.Cal}")} even in " +
                                 $"echelon: they need about {2 * g.Reach + 2.2:F1} m of beam amidships.");
                break;
            }
        }
        var widthsF = PlanWidths(yW);
        double beforeCoreF = core.Count > 0 ? widthsF.Take(core[0]).Sum() : 0.0;
        double coreLF = core.Select(i => widthsF[i]).Sum();
        var (loX, hiX, xx0) = PlanFront(widthsF, yW);
        if (hiX < loX - 0.5)
        {
            var what = new List<string>();
            if (nm != 0)
                what.Add($"{nm} midships turret(s)");
            if (nw != 0)
                what.Add($"{nw} wing turret pair(s)");
            lay.Fail("length", $"No room for the machinery, {nfun} funnel(s){(what.Count > 0 ? " and " + string.Join(" and ", what) : "")}" +
                               " between the end turret groups.");
        }
        double coreFront = xx0 - beforeCoreF;
        var fxs = new List<double>();
        var fSeg = new List<int?>();
        var mids = new List<(Gun G, double X, int Stow)>();
        var wings = new List<(Gun G, double Y, List<(double X, int Side)> Pair)>();
        var segSpan = new OrderedDictionary<int, double[]>();
        double xx = xx0;
        for (int i = 0; i < seq.Count; i++)
        {
            string it = seq[i];
            double w_ = widthsF[i];
            if (it == "F")
            {
                fxs.Add(xx - w_ / 2);
                fSeg.Add(segOf[i]);
            }
            else if (it == "W")
            {
                var g = seqGun[i]!;
                double x = xx - WingSide(i, -1, yW);
                wings.Add((g, Wy(g, yW), g.Echelon ? [(x, -1), (x - WingStagger(g, yW), 1)] : [(x, -1), (x, 1)]));
            }
            else if (it == "T")
            {
                int stow = i + 1 < seq.Count && seq[i + 1] == "T" ? 0 : 180;
                int j = stow == 180 ? i - 1 : i + 1;
                double margin = Math.Max(seqGun[i]!.Reach + 1.0, HalfOf(j, yW) / beamTan + 0.5);
                mids.Add((seqGun[i]!, stow == 180 ? xx - margin : xx - w_ + margin, stow));
            }
            if (segOf[i] is int so)
            {
                if (!segSpan.TryGetValue(so, out var spn))
                    segSpan[so] = spn = [xx, xx - w_];
                spn[1] = xx - w_;
            }
            xx -= w_;
        }
        var pos = new Dictionary<int, (double A, double B)>();
        foreach (var (si, ab) in segSpan)
            pos[si] = ((ab[0] + ab[1]) / 2 - segs[si].Len / 2, (ab[0] + ab[1]) / 2 + segs[si].Len / 2);
        if (ubSeg is int ub)
        {
            double back = segSpan[ub][1];
            pos[ub] = (back, back + segs[ub].Len);
        }
        double xp = (new[] { coreFront }.Concat(pos.Values.Select(v => v.B))).Max();
        foreach (var si in Enumerable.Reverse(lead))
        {
            pos[si] = (xp, xp + segs[si].Len);
            xp += segs[si].Len;
        }
        xp = (new[] { coreFront - coreLF }.Concat(pos.Values.Select(v => v.A))).Min();
        foreach (var si in trail)
        {
            pos[si] = (xp - segs[si].Len, xp);
            xp -= segs[si].Len;
        }
        var machPlaced = Enumerable.Range(0, segs.Count).Select(si => (Kind: segs[si].Kind, X0: pos[si].A, X1: pos[si].B)).ToList();
        int firstEngine = Enumerable.Range(0, segs.Count).Cast<int?>().FirstOrDefault(k => segs[k!.Value].Kind == "engine") ?? 0;
        var fSegF = fSeg.Select(si => si ?? firstEngine).ToList();
        var plantPlaced = machPlaced.Where(p => p.Kind != "magazine").ToList();
        double machC = (plantPlaced.Select(p => p.X0).Min() + plantPlaced.Select(p => p.X1).Max()) / 2;
        lay.Geo.Machinery = (plantPlaced.Select(p => p.X0).Min(), plantPlaced.Select(p => p.X1).Max());
        lay.Geo.MachineryX = machC;

        // ---------------- raised stretches of hull (hull.raised) ----------------
        double? fAft = fxs.Count > 0 ? fxs.Min() - fl / 2 : null, fFwd = fxs.Count > 0 ? fxs.Max() + fl / 2 : null;
        var edges = new Dictionary<string, (double? Aft, double? Fwd)>
        {
            ["bow"] = (null, L / 2),
            ["fore_group"] = (midFwd, fore.Count > 0 ? fore[0] + F[0].R + 1.0 : midFwd),
            ["bridge"] = (bx0 - 0.75, midFwd),
            ["funnels"] = (fxs.Count > 0 ? fAft - 0.75 : null, fxs.Count > 0 ? fFwd + 0.75 : null),
            ["aft_control"] = (midAft, la != 0 ? midAft + la + 0.75 : midAft),
            ["aft_group"] = (aft.Count > 0 ? aft[0] - A[0].R - 1.0 : midAft, midAft),
            ["stern"] = (-L / 2, null),
        };
        var spans = new List<(double, double, long)>();
        foreach (var q in raisedIn)
        {
            int a = Array.IndexOf(RAISED_ANCHORS, q.S("from")), b = Array.IndexOf(RAISED_ANCHORS, q.S("to"));
            int i = Math.Min(a, b), j = Math.Max(a, b);
            int i0 = i, j0 = j;
            while (i <= j && edges[RAISED_ANCHORS[i]].Fwd is null)
                i++;
            while (i <= j && edges[RAISED_ANCHORS[j]].Aft is null)
                j--;
            string Name(int k) => RAISED_ANCHORS[k].Replace('_', ' ');
            string what = $"the raised deck from the {q.S("from").Replace('_', ' ')} to the {q.S("to").Replace('_', ' ')}";
            var (x0_, x1_) = i <= j ? (edges[RAISED_ANCHORS[j]].Aft!.Value, edges[RAISED_ANCHORS[i]].Fwd!.Value) : (0.0, 0.0);
            if (i > j || x1_ - x0_ < 0.5)
            {
                lay.Warnings.Add($"hull.raised: nothing to raise {what} over; it is left out.");
                continue;
            }
            foreach (var (k, k0) in new[] { (i, i0), (j, j0) })
                if (k != k0)
                    lay.Warnings.Add($"hull.raised: no {Name(k0)} for {what}; it runs to the {Name(k)}.");
            spans.Add((x0_, x1_, q.I("decks")));
        }
        foreach (var (rid, x0_, x1_, lv, brk) in RaisedNames(RaisedProfile(spans, L), L))
            AddRaised(lay, design, rid, x0_, x1_, lv, brk);

        // ---------------- main turrets ----------------
        var turretTypes = new PyDict();
        var mounts = new List<PyDict>();
        foreach (var g in bats)
            turretTypes[g.Tid] = g.T;
        if (nf != 0 || na != 0)
        {
            var groupsT = new[] { ("A", fore, F, 0L), ("Y", aft, A, 180L) };
            var names = new Dictionary<string, string> { ["A"] = "ABC", ["Y"] = "YXW" };
            var stepped = new Dictionary<string, int> { ["A"] = nStepF, ["Y"] = nStepA };
            foreach (var (gname, xs, gs, rest) in groupsT)
            {
                double deck0 = xs.Count > 0 ? lay.DeckZ(xs[0], gs[0].Reach) : 0.0;
                double? prev = null;
                for (int i = 0; i < xs.Count; i++)
                {
                    double x = xs[i];
                    var g = gs[i];
                    bool flush = i >= Math.Max(stepped[gname], 1);
                    int level = flush ? 0 : i;
                    double dz = lay.DeckZ(x, g.Reach);
                    long rest_ = flush ? (gname == "A" ? 180 : 0) : rest;
                    double bse = (flush ? dz : deck0) + 1.2 + TierSteps(gs, level);
                    if (!flush && prev is double pv && pv > deck0 + 1.2 + TierSteps(gs, level - 1) + 1e-9)
                        bse = Math.Max(bse, pv + Geometry.SuperfireStep(gs[i - 1].Th));
                    bse = Math.Max(bse, dz + 1.2);
                    var probe = PyDict.Of(("kind", "main"), ("t", g.T), ("x", x), ("y", 0.0), ("rest", rest_), ("base", bse),
                        ("top", bse + g.Th));
                    if (flush)
                        probe["arc_role"] = "beam";
                    bse += RaisedLift(lay, probe);
                    prev = bse;
                    string mid = TurretName(names[gname], i);
                    var m = Armament.AddMount(lay, mounts, "main", g.Tid, g.T, mid, x, 0.0, bse, rest_, level: level,
                        armourMm: g.Arm["armour_mm"], depth: depth, footprintR: g.Reach, label: "Turret",
                        deck: lay.Raised.Count > 0 ? bse - 1.2 - TierSteps(gs, level) : dz,
                        extra: g.Arm.Has("material") ? [("material", g.Arm["material"])] : []);
                    if (flush)
                        m["arc_role"] = "beam";
                    lay.ReserveSweep(m);
                }
            }
        }

        // riders: what level 1 is built under
        var riders = new List<(double X0, double X1, double Hw)>();
        if (nm != 0 || nw != 0)
        {
            void MainMount(Gun g, string mid, double x, double y, long rest, params (string, object?)[] kw)
            {
                double reach = g.Reach;
                double dz = lay.DeckZ(x, reach);
                double bse = Math.Max(dz, g.Raised ? LEVEL_H : 0.0) + 1.2;
                var probe = PyDict.Of(("kind", "main"), ("t", g.T), ("x", x), ("y", y), ("rest", rest), ("base", bse), ("top", bse + g.Th));
                foreach (var (k, v) in kw)
                    probe[k] = v;
                bse += RaisedLift(lay, probe);
                var extra = new List<(string, object?)>();
                if (g.Arm.Has("material"))
                    extra.Add(("material", g.Arm["material"]));
                extra.AddRange(kw);
                lay.ReserveSweep(Armament.AddMount(lay, mounts, "main", g.Tid, g.T, mid, x, y, bse, rest,
                    armourMm: g.Arm["armour_mm"], depth: depth, footprintR: reach, label: "Turret",
                    deck: lay.Raised.Count > 0 ? Math.Max(0.0, bse - 1.2 - (g.Raised ? LEVEL_H : 0.0)) : dz, extra: [.. extra]));
                if (g.Raised && dz < LEVEL_H)
                    riders.Add((x - reach - DH_INSET, x + reach + DH_INSET, Math.Abs(y) + reach + DH_INSET));
            }

            for (int k = 0; k < mids.Count; k++)
            {
                var (g, x, stow) = mids[k];
                MainMount(g, TurretName("QPRS", k), x, 0.0, stow, ("arc_role", "beam"), ("midships", true));
            }
            for (int k = 0; k < wings.Count; k++)
            {
                var (g, yG, pair) = wings[k];
                foreach (var (x, side) in pair)
                {
                    bool fwd = g.Echelon ? x == pair[0].X : x >= machC;
                    var kw = new List<(string, object?)> { ("wing", true), ("battery", $"W{k + 1}"), ("magazine_end", x >= machC ? "fore" : "aft") };
                    if (g.Echelon)
                        kw.Add(("echelon", true));
                    if (g.Cross)
                        kw.Add(("cross_deck", true));
                    MainMount(g, $"W{k + 1}{(side > 0 ? "S" : "P")}", x, side * yG, fwd ? 0 : 180, [.. kw]);
                }
            }
        }

        // ---------------- superstructure, kept out of the guns' sweeps ----------------
        lay.EndMounts = mounts.Where(m => Py.Eq(m["kind"], "main") && !(m.B("wing") || m.B("midships"))).ToList();
        var blocks = new List<PyDict>();
        double towerTop = LEVEL_H * nTower + hood;
        while (!lay.Clear(Footprint.Rect(bx0, -w2 / 2, bx1, w2 / 2), towerTop) && bx0 > midAft)
        {
            bx1 -= 0.5;
            bx0 -= 0.5;
        }
        double ax0 = midAft;
        while (la != 0 && !lay.Clear(Footprint.Rect(ax0, -0.14 * B, ax0 + la, 0.14 * B), LEVEL_H * (naLvl + 1)) && ax0 + la < bx0)
            ax0 += 0.5;

        (double X0, double X1, double W) TowerFp(long k)
        {
            var (fwT, flT) = TowerTaper(k);
            if (flT == 1.0)
                return (bx0, bx1, w2 * fwT);
            double l = lb * flT;
            double x1t = bx1 - 0.25 * (lb - l);
            return (x1t - l, x1t, w2 * fwT);
        }

        PyDict? Stack(string bid, long k, double x0_, double x1_, double w_, PyDict? below, string role = "bridge", bool office = false)
        {
            var b_ = AddLevel(lay, blocks, bid, k, x0_, x1_, w_, support: below != null ? Geometry.Pts(below["points"]) : null,
                role: role, office: office);
            return b_ ?? below;
        }

        PyDict? belowB = null;
        for (long k = 2; k < nb; k++)
        {
            var (x0t, x1t, wt) = TowerFp(k);
            belowB = Stack($"Bridge base {k}", k, x0t, x1t, wt, belowB, office: true);
        }
        var towerFoot = blocks.Count > 0 ? blocks[0] : null;
        var (tx0, tx1, _) = nb > 2 ? TowerFp(nb - 1) : (bx0, bx1, w2);
        double tl = tx1 - tx0;
        belowB = Stack("Bridge", nb, tx0, tx1, w2, null);
        towerFoot ??= belowB;
        if (nTower > nb)
            belowB = Stack("Bridge upper", nb + 1, tx0 + 0.1 * tl, tx1 - 0.06 * tl, 0.78 * w2, belowB);
        if (armour.F("belt_mm", 0) > 0)
        {
            double ctR = Math.Min(Math.Max(0.1 * B, 1.25), Math.Min(4.0, 0.4 * w2));
            double ctX = Math.Max(bx1 - 0.42 * w2, bx0 + ctR);
            if (towerFoot != null)
            {
                var pts_ = Geometry.Pts(towerFoot["points"]);
                ctX = towerFoot.F("x1") - ctR - 0.3;
                while (ctX > towerFoot.F("x0") + ctR &&
                       Enumerable.Range(0, pts_.Count).Select(i => SegDist(ctX, 0.0, pts_[(i - 1 + pts_.Count) % pts_.Count].X,
                           pts_[(i - 1 + pts_.Count) % pts_.Count].Y, pts_[i].X, pts_[i].Y)).Min() < ctR + 0.3)
                    ctX -= 0.25;
            }
            lay.ConningTower = PyDict.Of(("x", ctX), ("y", 0.0), ("r", ctR), ("top", 2 * LEVEL_H));
            double mm = armour.F("belt_mm") / 1000;
            double area = 2 * Math.PI * ctR * 2 * LEVEL_H + 0.5 * Math.PI * Math.Pow(ctR, 2);
            lay.Weights.Add(new Weight("Conning tower", "armour", area * mm * 7.85, lay.ConningTower.F("x"), ZRel.Deck(LEVEL_H)));
        }
        for (long k = nb + 2; k <= nTower; k++)
        {
            double f = Math.Min(0.12, 0.03 * (k - nb - 2)), tw = Math.Max(3.0, 0.5 * w2 * Math.Pow(0.9, k - nb - 2));
            belowB = Stack($"Tower {k}", k, tx0 + (0.35 + f) * tl, Math.Max(tx0 + (0.35 + f) * tl + 3.0, tx1 - (0.2 + f) * tl), tw, belowB);
        }
        if (la != 0)
        {
            belowB = null;
            for (long k = 2; k < naLvl; k++)
                belowB = Stack($"Aft control base {k}", k, ax0, ax0 + la, 0.28 * B, belowB, "aft_control", true);
            belowB = Stack("Aft control", naLvl, ax0, ax0 + la, 0.28 * B, belowB, "aft_control");
            Stack("Aft control upper", naLvl + 1, ax0 + 0.25 * la, ax0 + 0.75 * la, 0.17 * B, belowB, "aft_control");
        }

        // funnels trunked aft toward the boundary with what lies aft of the boilers
        Footprint FunFp(double x) => Footprint.Rect(x - fl / 2, -fw / 2, x + fl / 2, fw / 2);
        double reachLim = fplan.F("reach") + fl / 2;

        bool InReach(int si, List<double> xs, double off)
        {
            var ends = new[] { machPlaced[si].X0, machPlaced[si].X1 };
            return ends.All(e => xs.Select(x => Math.Abs(e - x - off)).Min() <= Math.Max(reachLim, xs.Select(x => Math.Abs(e - x)).Min()) + 1e-6);
        }

        var bySeg = new OrderedDictionary<int, List<int>>();
        for (int i = 0; i < fSegF.Count; i++)
        {
            if (!bySeg.TryGetValue(fSegF[i], out var l))
                bySeg[fSegF[i]] = l = [];
            l.Add(i);
        }
        var fxFinal = fxs.ToList();
        foreach (var si in bySeg.Keys.OrderBy(s => machPlaced[s].X0).ToList())
        {
            var idx = bySeg[si];
            double want = 0.0;
            if (machPlaced[si].Kind == "boiler")
                want = Math.Min(0.0, machPlaced[si].X0 - (fxs[idx[0]] + fxs[idx[^1]]) / 2);
            double off = want;
            var xs = idx.Select(i => fxs[i]).ToList();
            double bound = (new[] { machPlaced[si].X0, machPlaced[si].X1 }.Select(e =>
                e - xs.Max() - Math.Max(reachLim, xs.Select(x => Math.Abs(e - x)).Min()))).Max() - 1e-6;
            while (off < Math.Min(0.0, bound))
                off = Math.Min(0.0, off + 0.25);
            while (off < 0.0 && !(InReach(si, idx.Select(i => fxs[i]).ToList(), off) && idx.All(i =>
                       lay.Clear(FunFp(fxs[i] + off), funTop) && lay.Free(FunFp(fxs[i] + off), 0.0)
                       && lay.DeckLevels(fxs[i] + off, fl / 2) == lay.DeckLevels(fxs[i], fl / 2))))
                off = Math.Min(0.0, off + 0.25);
            foreach (var i in idx)
            {
                fxFinal[i] = fxs[i] + off;
                lay.Occupy(FunFp(fxFinal[i]), lay.DeckZ(fxFinal[i], fl / 2), funTop, $"Funnel {i + 1}");
            }
        }

        var funnels = new List<PyDict>();
        for (int i = 0; i < fxFinal.Count; i++)
        {
            double fx = fxFinal[i];
            string fid = $"Funnel {i + 1}";
            var fp = FunFp(fx);
            if (!lay.Clear(fp, funTop) || !lay.Free(fp, 0.0, [fid]))
                lay.Fail("length", $"{fid} would stand in a turret's sweep or against the bridge: use fewer funnels " +
                                   "or midships turrets.");
            funnels.Add(PyDict.Of(("id", fid), ("x", fx), ("y", 0.0), ("l", fl), ("w", fw), ("pipes", fw > 4 ? 2L : 1L),
                ("seg", (long)fSegF[i])));
            if (lay.DeckZ(fx, fl / 2) != 0)
                funnels[^1]["z0"] = lay.DeckZ(fx, fl / 2);
            var seg = machPlaced[fSegF[i]];
            AddFunnelWeights(lay, funnels[^1], funTop, (seg.X0 + seg.X1) / 2, depth);
        }

        // ---------------- secondaries on deck ----------------
        double dhW = 0.62 * B;
        bool first = true;
        foreach (var sec in secs)
        {
            long nsec = Py.ToLong(sec["per_side"]);
            if (!Py.Eq(sec.Get("mount", "deck"), "deck") || nsec == 0)
                continue;
            string pre = sec.S("prefix"), cal = $"{sec.F("calibre_mm")} mm";
            bool raised = Py.Eq(StandsOn(sec), "deckhouse");
            var (tsId, ts) = Geometry.BatteryType(sec);
            turretTypes[tsId] = ts;
            double rs = ts.F("r");
            double rsReach = Math.Max(rs, Geometry.TurretReach(ts, 0.0));
            double ths = Geometry.TurretHeight(ts);

            double SecBase(double x) => raised ? Math.Max(lay.DeckZ(x, rsReach), LEVEL_H) : lay.DeckZ(x, rsReach);
            double xLo = midAft + rsReach + 0.5, xHi = midFwd - rsReach - 0.5;
            double inner = (blocks.Where(b => b.I("level") >= 2).Select(b => b.F("w") / 2).Append(fw / 2)
                .Concat(M.Select(g => g.Reach))).Max() + rsReach + 0.4;

            double OuterAt(double x) => Math.Min(hull.HalfWidth(x + -rsReach), Math.Min(hull.HalfWidth(x + 0.0), hull.HalfWidth(x + rsReach))) - rsReach - 0.6;

            double YAt(double x) => Math.Max(inner, inner + 0.55 * (OuterAt(x) - inner));
            var xsProbe = Enumerable.Range(0, 41).Select(k => xLo + (xHi - xLo) * k / 40).ToList();
            double xLo0 = xLo, xHi0 = xHi;
            if (xsProbe.Select(OuterAt).Max() < inner)
                lay.Fail("beam", $"Hull too narrow for {cal} secondary mounts: they need about " +
                                 $"{2 * (inner + rsReach + 0.6):F1} m of beam amidships.");
            else
            {
                var fitsX = xsProbe.Where(x => OuterAt(x) >= inner).ToList();
                (xLo, xHi) = (fitsX.Min(), fitsX.Max());
            }

            void ShortOf(string msg, Func<bool> fitWider)
            {
                lay.Fail("length", msg);
                if ((xLo, xHi) != (xLo0, xHi0) && fitWider())
                    lay.Short.Add("beam");
            }

            double pitchS = 2.1 * rsReach + 1.0;
            double pitchStowed = Geometry.TurretReach(ts) + rsReach + 0.4;

            bool PitchOk(double x, IEnumerable<double> others, double sp) =>
                others.All(o => Math.Abs(x - o) >= sp && ((x >= 0) != (o >= 0) || Math.Abs(x - o) >= pitchStowed));

            bool BarrelsOk(double x)
            {
                double b = SecBase(x);
                var band = Armament.BarrelBand(b, b + ths, ts);
                var fps = new[] { 1, -1 }.Select(s => Armament.BarrelFootprint(ts, x, s * YAt(x), Armament.StowBearing(x, s, 90.0))).ToList();
                return fps.All(fp => lay.FreeAt(fp, band.Lo, band.Hi, 0.2) && lay.Clear(fp, band.Hi));
            }

            if (xHi <= xLo)
                lay.Fail("length", "No room amidships for the secondary battery.");
            else if (first && nsec > 1 && (xHi - xLo) / (nsec - 1) < pitchS)
                ShortOf($"{nsec} secondary mounts per side do not fit in {xHi - xLo:F0} m amidships " +
                        $"(max {(long)((xHi - xLo) / pitchS) + 1}).", () => (xHi0 - xLo0) / (nsec - 1) >= pitchS);
            double step = Math.Min((xHi - xLo) / Math.Max(nsec - 1, 1), 2.2 * rs + 4.0);
            double c = (xLo + xHi) / 2;
            var sxs = Enumerable.Range(0, (int)nsec).Select(i => nsec > 1 ? c + (i - (nsec - 1) / 2.0) * step : c).ToList();
            bool spreadOk = Enumerable.Range(0, sxs.Count).All(i => PitchOk(sxs[i], sxs.Take(i), 0.0) && BarrelsOk(sxs[i]));
            if (nw != 0 || !first || !spreadOk)
            {
                bool SpotOk(double x)
                {
                    var fps = new[] { 1, -1 }.Select(s => Footprint.Circle(x, s * YAt(x), rsReach));
                    return fps.All(fp => lay.Free(fp, 0.4) && lay.Clear(fp, SecBase(x) + ths)) && BarrelsOk(x);
                }
                var spots = (Enumerable.Range(0, (int)(Math.Max(0.0, xHi - xLo) * 2) + 1).Select(k => xLo + 0.5 * k)
                    .Where(SpotOk)).OrderBy(x => Math.Abs(x - c)).ToList();
                foreach (var sp in new[] { 2.2 * rs + 4.0, pitchS })
                {
                    sxs = [];
                    foreach (var x in spots)
                        if (sxs.Count < nsec && PitchOk(x, sxs, sp))
                            sxs.Add(x);
                    if (sxs.Count == nsec)
                        break;
                }
                foreach (var sp in new[] { 2.2 * rs + 4.0, pitchS })
                {
                    if (sxs.Count == nsec)
                        break;
                    var cand = NearestSpaced(spots, (int)nsec, c, sp, Math.Max(sp, pitchStowed));
                    if (cand != null && cand.Count > 0 && Enumerable.Range(0, cand.Count).All(i => PitchOk(cand[i], cand.Take(i), sp)))
                        sxs = cand;
                }
                if (sxs.Count < nsec)
                {
                    string where = first ? (nw != 0 ? "beside the wing turrets" : "amidships") : "amidships beside the other secondaries";
                    var sxsCount = sxs.Count;
                    ShortOf($"Only {sxsCount} of {nsec} {(first ? "" : cal + " ")}secondary mounts per side fit {where}.",
                        () => NearestSpaced(Enumerable.Range(0, (int)(Math.Max(0.0, xHi0 - xLo0) * 2) + 1).Select(k => xLo0 + 0.5 * k)
                            .Where(SpotOk), (int)nsec, c, pitchS, Math.Max(pitchS, pitchStowed)) is { Count: > 0 });
                }
                sxs = sxs.Order().ToList();
            }
            for (int i = 0; i < sxs.Count; i++)
            {
                double sx = sxs[i];
                double yS = YAt(sx);
                if (!lay.Clear(Footprint.Circle(sx, yS, rsReach), SecBase(sx) + ths))
                    lay.Fail("length", $"Secondary mounts {pre}{i + 1} would stand in a main turret's sweep: use fewer " +
                                       "secondaries.");
                foreach (int side in new[] { 1, -1 })
                {
                    string mid = $"{pre}{i + 1}{(side > 0 ? "S" : "P")}";
                    Armament.AddMount(lay, mounts, "secondary", tsId, ts, mid, sx, side * yS, SecBase(sx),
                        Armament.StowBearing(sx, side, 90.0), armourMm: sec["armour_mm"], depth: depth, top: SecBase(sx) + ths,
                        footprintR: rsReach, deck: lay.DeckZ(sx, rsReach),
                        extra: [("material", sec.Get("material")), ("side_mount", true), ("battery", pre)]);
                }
            }
            if (raised && sxs.Count > 0)
            {
                double yS = sxs.Select(YAt).Max();
                riders.AddRange(sxs.Where(sx => lay.DeckZ(sx, rsReach) < LEVEL_H)
                    .Select(sx => (sx - rsReach - DH_INSET, sx + rsReach + DH_INSET, yS + rsReach + DH_INSET)));
            }
            first = false;
        }

        // ---------------- casemates ----------------
        PlaceCasemates(lay, mounts, turretTypes, blocks, secs, hull, depth);
        var housings = blocks.Where(b => Py.Eq(b["role"], "casemate")).ToList();
        riders.AddRange(housings.Select(b => (b.F("x0"), b.F("x1"), Math.Abs(b.F("y")) - b.F("w") / 2)));

        // ---------------- level 1 ----------------
        (double X0, double X1)? dh = (bx0 - 3.0, bx1 + 1.0);
        dhW = 0.62 * B;
        var pieces = new List<(string Id, double X0, double X1, double W, string Role)>();
        bool aftOn = false;
        if (riders.Count > 0)
        {
            double rx0 = riders.Select(r => r.X0).Min(), rx1 = riders.Select(r => r.X1).Max();
            double rw = 2 * riders.Select(r => r.Hw).Max();
            var through = funnels.Select(f => f.S("id")).Concat(housings.Select(h => h.S("id"))).ToList();

            bool DeckFree(double x0_, double x1_)
            {
                if (x1_ <= x0_)
                    return true;
                var fp = Footprint.Rect(x0_, -rw / 2, x1_, rw / 2);
                return lay.FreeAt(fp, 0.0, LEVEL_H, FP_MARGIN, through) && lay.Clear(fp, LEVEL_H);
            }

            if (DeckFree(rx1, dh.Value.X0))
            {
                (rx0, rx1, rw) = (Math.Min(rx0, dh.Value.X0), Math.Max(rx1, dh.Value.X1), Math.Max(rw, dhW));
                dh = null;
            }
            if (la != 0 && DeckFree(ax0 + la, rx0))
                (rx0, aftOn) = (Math.Min(rx0, ax0), true);
            double hwMax = Enumerable.Range(0, 21).Select(k => hull.HalfWidth(rx0 + (rx1 - rx0) * k / 20)).Max();
            pieces.Add(("Deckhouse", rx0, rx1, Math.Min(rw, 2 * (hwMax - DH_INSET)), "deckhouse"));
        }
        if (dh is { } dhv)
            pieces.Add((pieces.Count > 0 ? "Deckhouse-2" : "Deckhouse", dhv.X0, dhv.X1, dhW, "deckhouse"));
        if (la != 0 && !aftOn)
            pieces.Add(("Aft control base 1", ax0, ax0 + la, 0.28 * B, "aft_control"));

        Footprint DhRect(double x0_, double x1_)
        {
            double hw = Math.Min(hull.HalfWidth(x0_), hull.HalfWidth(x1_)) - 0.6;
            return Footprint.Rect(x0_, -hw, x1_, hw);
        }

        var raisedIds = lay.Raised.Select(s => s.S("id")).ToList();
        var cut = new List<(string Id, double X0, double X1, double W, string Role)>();
        foreach (var (pid, x0p, x1p, wp, role) in pieces)
        {
            var runs = new List<(double A, double B)> { (x0p, x1p) };
            foreach (var s_ in lay.Raised)
                runs = runs.SelectMany(r => new[] { (r.A, Math.Min(r.B, s_.F("x0"))), (Math.Max(r.A, s_.F("x1")), r.B) })
                    .Where(q => q.Item2 > q.Item1).ToList();
            runs = runs.Where(r => r.B - r.A >= DH_SLIVER || (r.A == x0p && r.B == x1p) || lay.Footprints.Any(o =>
                o.Base >= LEVEL_H - 0.01 && r.A <= (o.Fp.BBox.X0 + o.Fp.BBox.X1) / 2 && (o.Fp.BBox.X0 + o.Fp.BBox.X1) / 2 <= r.B
                && !raisedIds.Contains(o.Owner) && !lay.Overhangs.Contains(o.Fp))).ToList();
            runs.Reverse();
            cut.AddRange(runs.Select((r, k) => (pid + (k != 0 ? $" part {k + 1}" : ""), r.A, r.B, wp, role)));
        }
        var level1 = new List<PyDict>();
        foreach (var (pid, x0c, x1c, wc, role) in cut)
        {
            double x0_ = x0c, x1_ = x1c;
            while (!lay.Clear(DhRect(x0_, x1_), LEVEL_H) && x1_ - x0_ > 4)
            {
                if (lay.Clear(DhRect(x0_, (x0_ + x1_) / 2), LEVEL_H))
                    x1_ -= 0.5;
                else
                    x0_ += 0.5;
            }
            var keep = lay.Footprints.Where(o => o.Base >= LEVEL_H - 0.01 && !lay.Overhangs.Contains(o.Fp)
                                                 && x0_ <= (o.Fp.BBox.X0 + o.Fp.BBox.X1) / 2 && (o.Fp.BBox.X0 + o.Fp.BBox.X1) / 2 <= x1_)
                .Select(o => o.Fp.BBox).ToList();
            var b_ = AddLevel(lay, blocks, pid, 1, x0_, x1_, wc, keep: keep,
                ignore: funnels.Select(f => f.S("id")).Concat(housings.Select(h => h.S("id"))).Concat(raisedIds).ToList(),
                role: role, office: role == "aft_control");
            if (b_ != null)
            {
                var last = blocks[^1];
                blocks.RemoveAt(blocks.Count - 1);
                blocks.Insert(0, last);
                level1.Add(b_);
            }
        }
        var dhBlocks = level1.Where(b => Py.Eq(b["role"], "deckhouse")).ToList();
        var dhIds = dhBlocks.Select(b => b.S("id")).ToList();
        dhW = pieces.Where(p => p.Role == "deckhouse").Select(p => (double?)p.W).FirstOrDefault() ?? dhW;

        // ---------------- torpedo mounts ----------------
        if (ntp != 0)
        {
            turretTypes[ttId!] = tt;
            double sweep = tSweep;
            long placed = 0;
            if (tEdges)
            {
                if (ntp % 2 != 0)
                {
                    lay.Warnings.Add("Torpedo mounts at the deck edges go in pairs; rounded up to an even number.");
                    ntp += 1;
                }
                var xs = Enumerable.Range((int)-L, 2 * (int)L).Select(x => x * 0.5).OrderBy(x => Math.Abs(x - machC)).ToList();
                foreach (var x in xs)
                {
                    if (placed >= ntp)
                        break;
                    double y = hull.HalfWidth(x) - tt!.F("r") - 0.8;
                    if (y < sweep)
                        continue;
                    var fps = new[] { Footprint.Circle(x, y, sweep), Footprint.Circle(x, -y, sweep) };
                    double dz = lay.DeckZ(x, sweep);
                    if (fps.All(fp => lay.Free(fp, 0.3) && lay.Clear(fp, dz + 1.4)))
                    {
                        foreach (var (side, fp) in new[] { (1, fps[0]), (-1, fps[1]) })
                        {
                            string mid = $"T{placed / 2 + 1}{(side > 0 ? "S" : "P")}";
                            mounts.Add(PyDict.Of(("id", mid), ("kind", "torpedo"), ("type", ttId), ("t", tt), ("x", x), ("y", side * y),
                                ("level", 0L), ("base", dz + 0.3), ("top", dz + 1.4), ("rest", (long)(90 * side))));
                            lay.Occupy(fp, dz, dz + 1.4, mid);
                            lay.Weights.Add(new Weight(mid, "armament", Batteries.TorpedoWeight(tt!.F("barrels")), x, ZRel.Deck(dz + 1)));
                            placed++;
                        }
                    }
                }
            }
            if (placed < ntp)
            {
                var cands = Enumerable.Range(0, Math.Max(0, (int)((midFwd - midAft) * 2) + 1)).Select(k => midAft + 0.5 * k).OrderBy(x => Math.Abs(x - machC)).ToList();
                foreach (var x in cands)
                {
                    if (placed >= ntp)
                        break;
                    var fp = Footprint.Circle(x, 0, sweep);
                    double dz = lay.DeckZ(x, sweep);
                    if (lay.Free(fp, 0.3) && lay.Clear(fp, dz + 1.4) && hull.HalfWidth(x) > tt!.F("r") + 0.5)
                    {
                        string mid = $"T{placed + 1}";
                        mounts.Add(PyDict.Of(("id", mid), ("kind", "torpedo"), ("type", ttId), ("t", tt), ("x", x), ("y", 0.0),
                            ("level", 0L), ("base", dz + 0.3), ("top", dz + 1.4), ("rest", 90L)));
                        lay.Occupy(fp, dz, dz + 1.4, mid);
                        lay.Weights.Add(new Weight(mid, "armament", Batteries.TorpedoWeight(tt.F("barrels")), x, ZRel.Deck(dz + 1)));
                        placed++;
                    }
                }
            }
            if (placed < ntp && dhBlocks.Count > 0)
            {
                var cands = dhBlocks.SelectMany(b_ => Enumerable.Range(0, Math.Max(0, (int)((b_.F("x1") - b_.F("x0") - 2 * sweep) * 2) + 1))
                    .Select(k => b_.F("x0") + sweep + 0.5 * k)).OrderBy(x => Math.Abs(x - machC)).ToList();
                foreach (var x in cands)
                {
                    if (placed >= ntp)
                        break;
                    var fp = Footprint.Circle(x, 0, sweep);
                    if (lay.Free(fp, 0.3, dhIds) && lay.Clear(fp, LEVEL_H + 1.4))
                    {
                        string mid = $"T{placed + 1}";
                        mounts.Add(PyDict.Of(("id", mid), ("kind", "torpedo"), ("type", ttId), ("t", tt), ("x", x), ("y", 0.0),
                            ("level", 1L), ("base", LEVEL_H + 0.3), ("top", LEVEL_H + 1.4), ("rest", 90L)));
                        lay.Occupy(fp, LEVEL_H, LEVEL_H + 1.4, mid);
                        lay.Weights.Add(new Weight(mid, "armament", Batteries.TorpedoWeight(tt!.F("barrels")), x, ZRel.Deck(LEVEL_H + 1)));
                        placed++;
                    }
                }
            }
            if (placed < ntp)
                lay.Fail("length", $"Only {placed} of {ntp} torpedo mounts fit on deck.");
        }

        // ---------------- deckhouse levels ----------------
        AddDeckhouseLevels(lay, blocks, DeckhouseLevels(design), midAft, midFwd, dhBlocks, dhW, funnels.Select(f => f.S("id")).ToList());
        funTop = RaiseFunnels(lay, funnels, blocks, funTop);

        // ---------------- fire control ----------------
        FireControl.Place(lay, design, blocks);

        // ---------------- AA ----------------
        var aaOut = new List<PyDict>();
        var aaReq = design.Get("aa", new PyDict()) as PyDict ?? new PyDict();

        List<object?[]> AaSlots(string kind)
        {
            double rr = Geometry.AA_CFG[kind].R;
            var scored = new List<(double S, object?[] C)>();
            foreach (var (x, y, z0, pair) in RoofSpots(blocks, 2 * rr, 2 * rr))
            {
                long lvl = (long)Math.Round(z0 / LEVEL_H);
                double pen = AA_ROOF_PEN[(int)Math.Min(lvl, AA_ROOF_PEN.Length - 1)];
                if (pair)
                    scored.Add((Math.Abs(x - machC) / L + pen, [x, y, z0]));
                else if (Math.Abs(y) < 1e-6)
                    scored.Add((Math.Abs(x - machC) / L + pen + AA_SINGLE_PEN, [x, 0.0, z0]));
            }
            double xd = L / 2 - 0.06 * L;
            while (xd > -L / 2 + 2)
            {
                double yy = hull.HalfWidth(xd) - rr - 0.5;
                if (yy > rr + 0.5)
                    scored.Add((Math.Abs(xd - machC) / L + AA_DECK_PEN, [xd, yy, lay.DeckZ(xd, rr)]));
                xd -= 0.5;
            }
            var cands = scored.OrderBy(s => s.S).ToList().Select(s => s.C).ToList();
            double sx = -L / 2 + rr + 2.5;
            if (hull.HalfWidth(sx) > rr + 0.6)
                cands.Add([sx, 0.0, lay.DeckZ(sx, rr)]);
            return cands;
        }

        void PlaceAa(string kind, long count) =>
            Armament.PlaceAa(lay, aaOut, kind, count, AaSlots(kind), layerOf: bse => bse > LEVEL_H + 0.01 ? "upper" : "base");

        PlaceAa("quad40", Py.ToLong(aaReq.Get("heavy", 0L)));
        PlaceAa("single20", Py.ToLong(aaReq.Get("light", 0L)));

        // ---------------- masts and boats ----------------
        double mastTop = funTop + 6.0;
        double fxM = lay.Clear(Footprint.Circle(bx0 - 1.2, 0, 0.7), mastTop) ? bx0 - 1.2 : bx0 + 0.3 * lb;
        var masts = new List<PyDict> { PyDict.Of(("x", fxM), ("yard", Math.Min(0.3 * B, 10)), ("tripod", L >= 150)) };
        if (LEVEL_H * nTower + hood + 2.0 > mastTop)
            masts[0]["top"] = LEVEL_H * nTower + hood + 2.0;
        if (la != 0)
            masts.Add(PyDict.Of(("x", ax0 + la * 0.5), ("yard", Math.Min(0.22 * B, 8)), ("tripod", false)));
        for (int k = 0; k < masts.Count; k++)
            MastWeight(lay, masts[k], masts[k].F("top", mastTop), k == 0 ? "Foremast" : "Mainmast", lay.DeckZ(masts[k].F("x")));
        var boats = new List<object?>();
        double bl_ = Clamp(0.03 * L, 4, 8);
        foreach (var x in Enumerable.Range(-6, 13).Select(k => machC + k * 2.0))
        {
            if (boats.Count >= 2)
                break;
            double y = riders.Count > 0 ? dhW / 2 - 0.35 * bl_ - 0.6 : B / 2 - 0.35 * bl_ - 1.0;
            var (lo_, hi_) = lay.DeckLevels(x, bl_ / 2);
            double z = riders.Count > 0 ? Math.Max(LEVEL_H, hi_ * LEVEL_H) : LEVEL_H + hi_ * LEVEL_H;
            var fps = new[] { 1, -1 }.Select(s => Footprint.Rect(x - bl_ / 2, s * y - 0.15 * bl_, x + bl_ / 2, s * y + 0.15 * bl_)).ToList();
            if (lo_ == hi_ && y > fw / 2 + 0.3 * bl_ && fps.All(fp => lay.Free(fp, 0.3, dhIds) && lay.Clear(fp, z + 1.5)))
            {
                foreach (var (s, fp) in new[] { (1, fps[0]), (-1, fps[1]) })
                {
                    boats.Add(PyDict.Of(("x", x), ("y", s * y), ("l", bl_), ("w", 0.3 * bl_)));
                    lay.Occupy(fp, z, z + 1.5, $"Boat{boats.Count}");
                }
            }
        }

        // ---------------- citadel & compartments ----------------
        var mainMounts = mounts.Where(m => Py.Eq(m["kind"], "main")).ToList();
        var block = (machPlaced.Select(p => p.X0).Min(), machPlaced.Select(p => p.X1).Max());
        (double, double) cit = mainMounts.Count > 0
            ? (Math.Min(mainMounts.Select(m => m.F("x") - m.D("t").F("r")).Min() - 2.0, block.Item1),
               Math.Max(mainMounts.Select(m => m.F("x") + m.D("t").F("r")).Max() + 2.0, block.Item2))
            : block;
        SetCitadel(lay, cit.Item1, cit.Item2);
        double innerHw = 0.8 * B / 2;
        var magX = machPlaced.Where(p => p.Kind == "magazine").Select(p => (p.X0, p.X1)).ToList();
        var magGroups = new Dictionary<string, (double X0, double X1)>();
        if (magL["fore"] > 0)
            magGroups["fore"] = magX[0];
        if (magL["aft"] > 0)
            magGroups["aft"] = magX[^1];
        AddMagazines(lay, mounts, innerHw, magGroups);
        var toPlant = new Dictionary<long, long>();
        int kk2 = 0;
        for (int i = 0; i < machPlaced.Count; i++)
            if (machPlaced[i].Kind != "magazine")
                toPlant[i] = kk2++;
        foreach (var f in lay.FunnelsPlanned)
            f["seg"] = f.Get("seg") is object sg && toPlant.TryGetValue(Py.ToLong(sg), out var tp2) ? tp2 : null;
        AddMachineryRooms(lay, plantPlaced, innerHw, depth);
        AddSteering(lay);

        // ---------------- renderer spec ----------------
        double frontEdge = fore.Count > 0 ? fore[0] + F[0].R : midFwd;
        var extra2 = new List<(string, object?)> { ("boats", boats), ("bollards", new List<object?> { L / 2 - 0.05 * L, -L / 2 + 0.06 * L }) };
        if (L / 2 - frontEdge > 0.08 * L + 6)
        {
            extra2.Add(("chain_x", frontEdge + 0.35 * (L / 2 - frontEdge)));
            extra2.Add(("hawse_back", 0.03 * L + 1.0));
        }
        if (L >= 150 && fore.Count > 0 && (L / 2 - frontEdge) > 12)
            extra2.Add(("breakwater_x", frontEdge + 3.0));
        return FinishLayout(lay, design, hs, mounts, turretTypes, blocks, funnels, masts, aaOut, funTop, deck, [.. extra2]);
    }
}
