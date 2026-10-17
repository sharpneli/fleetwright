namespace Fleetwright.Shipgen;

public sealed partial class Layout
{
    /// <summary>The middle's plan (BuildLayout): what stands between the end turret groups, as a row of items from
    /// forward aft, and the rules for how long each item is. The plant's segments (boiler rooms, engine rooms, bunkers;
    /// a magazine segment at either end for the wing turrets) are fixed lengths; between them stand the gaps the
    /// midships turrets ("T") and the echelon wing pairs ("W") go in, a funnel ("F") over each boiler room's share and
    /// the other segments ("E") as they are, with the abreast wing pairs ("W") at the row's ends, the first forward,
    /// the next aft, and so on. The core is the row without the abreast pairs: what the bridge and aft control close
    /// up on.</summary>
    sealed class MiddleRow
    {
        readonly MainGroups g;
        readonly double lb, la;

        /// <summary>The plant's segments fore to aft (Kind: boiler, engine, bunker or magazine; Len in metres).</summary>
        public readonly List<(string Kind, double Len)> Segs;
        /// <summary>The magazine segments' lengths, "fore" and "aft" (0: none).</summary>
        public readonly Dictionary<string, double> MagL;
        /// <summary>The funnels: how many, their width and length, the plan (Geo.FunnelPlan), and their tops above the
        /// main deck (before the deckhouse levels raise them).</summary>
        public readonly long NFun;
        public readonly double Fw, Fl, FunTop;
        public readonly FunnelPlan Fplan;
        /// <summary>The tangent of the beam arc's edge off the fore-and-aft line: how far fore or aft of a midships
        /// turret what stands beside it must be to keep out of its beam arc.</summary>
        public readonly double BeamTan;
        /// <summary>The row: each item's kind (T, W, F, E), its plant segment (null for a turret), its own length
        /// (null for a turret: its width depends on the wings' offset), and its gun (turrets only).</summary>
        public readonly List<string> Seq;
        public readonly List<int?> SegOf;
        public readonly List<double?> Widths0;
        public readonly List<Gun?> SeqGun;
        /// <summary>The row's items but the abreast wing pairs.</summary>
        public readonly List<int> Core;
        /// <summary>The engine segments before the first and after the last funnel, taken off the row: they run under
        /// the bridge and the aft end, and LeadL and TrailL are their lengths (with what of the first boiler room runs
        /// under the bridge).</summary>
        public readonly List<int> Lead, Trail;
        public readonly double LeadL, TrailL;
        /// <summary>The boiler room whose front runs under the bridge, if the core starts with one.</summary>
        public readonly int? UbSeg;
        /// <summary>The bridge's width, and the length aft of the row that aft control takes (or a metre).</summary>
        public readonly double W2, AftL;
        /// <summary>The half-widths of what stands at either end of the row: the bridge forward (-1), aft control aft
        /// (Seq.Count).</summary>
        readonly Dictionary<int, double> endsHalf;

        public MiddleRow(WarshipSite site, PlantPlan plant, MainGroups g, ControlPlan control, TorpedoPlan torp)
        {
            this.g = g;
            (lb, la) = (control.Lb, control.La);
            var (design, res, lay, B, depth) = (site.Design, site.Res, site.Lay, site.B, site.Depth);
            var (M, WA, WE) = (g.M, g.WA, g.WE);
            int nm = g.Nm;
            long nTower = control.NTower, ntp = torp.Mounts;
            bool tEdges = torp.Edges;

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
                var result = set.ToList();
                foreach (var c in cands)
                {
                    if (result.Count >= n)
                        break;
                    if (!result.Contains(c))
                    {
                        result.Add(c);
                        result.Sort();
                    }
                }
                return result;
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
            foreach (var gp in gaps)
            {
                gapKind[gp] = tGaps.Contains(gp) ? "T" : "W";
                var src = tGaps.Contains(gp) ? tLeft : wLeft;
                gapGun[gp] = src[0];
                src.RemoveAt(0);
            }
            var waFront = Enumerable.Range(0, WA.Count).Where(k => k % 2 == 0).Select(k => WA[k]).Reverse().ToList();
            var waBack = Enumerable.Range(0, WA.Count).Where(k => k % 2 == 1).Select(k => WA[k]).ToList();
            var groupedWings = waFront.Select((w, k) => ($"W{k + 1}", "fore", w))
                .Concat(waBack.Select((w, k) => ($"W{waFront.Count + WE.Count + k + 1}", "aft", w))).ToList();
            var magPlan = MagazinePlan(design, groupedWings);
            var magL = magPlan.ToDictionary(kv => kv.Key, kv => kv.Value.Count > 0
                ? Ordnance.ZoneLength(kv.Value.Select(v => v.N * v.V).Sum(), 2 * Ordnance.GroupHalfWidth(plant, 0.8 * B / 2, B, Armour.MagazineWingM(design, B)), plant) : 0.0);
            if (magL["fore"] > 0)
            {
                segs.Insert(0, ("magazine", magL["fore"]));
                gapKind = gapKind.ToDictionary(kv => kv.Key + 1, kv => kv.Value);
                gapGun = gapGun.ToDictionary(kv => kv.Key + 1, kv => kv.Value);
            }
            if (magL["aft"] > 0)
                segs.Add(("magazine", magL["aft"]));
            double funTop = LevelH * Math.Min(nTower, 4) + FunnelAbove;
            double belowDeck = depth - plant.InnerBottom - 1.0;
            if (res.Plant.Tech.Draught.System is "natural" or "forced_boost")
                funTop = Math.Max(funTop, StackNatural - belowDeck);
            var groups = segs.Where(s => s.Kind == "boiler").Select(s => s.Len).ToList();
            var (nfun, fw, fl) = PlanFunnels(lay, design, res, B, funTop, groups);
            var fplan = lay.Geo.FunnelPlan!;
            fw = Math.Min(fw, 0.3 * B);
            double beamTan = Math.Tan(double.DegreesToRadians(90.0 - Arcs.ArcBeam));
            double fMin = fl + (ntp != 0 && !tEdges ? 9.6 : 2.0);

            var seq = new List<string>();
            var segOf = new List<int?>();
            var widths0 = new List<double?>();
            var seqGun = new List<Gun?>();
            int gI = 0;
            var fCounts = fplan.Counts;
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
                underBridge = Math.Max(0.0, Math.Min(ubL - fItems.Count * fMin, BridgeOverBoilers * (lb + 1.5)));
                foreach (var i in fItems)
                    widths0[i] = Math.Max(fMin, (ubL - underBridge) / fItems.Count);
            }
            double leadL = lead.Select(si => segs[si].Len).Sum() + underBridge;
            double trailL = trail.Select(si => segs[si].Len).Sum();
            double w2 = Clamp(0.36 * B, 4.5, 12);
            var endsHalf = new Dictionary<int, double> { [-1] = w2 / 2, [seq.Count] = la != 0 ? 0.14 * B : 0.0 };

            (Segs, MagL, NFun, Fw, Fl, FunTop, Fplan, BeamTan) = (segs, magL, nfun, fw, fl, funTop, fplan, beamTan);
            (Seq, SegOf, Widths0, SeqGun, Core, Lead, Trail) = (seq, segOf, widths0, seqGun, core, lead, trail);
            (LeadL, TrailL, UbSeg, W2, this.endsHalf) = (leadL, trailL, ubSeg, w2, endsHalf);
            AftL = la != 0 ? la + 1.5 : 1.0;
        }

        /// <summary>The half-width of item j (or of the bridge or aft control beyond the row's ends) when the wings
        /// stand y off the centreline.</summary>
        public double HalfOf(int j, double y)
        {
            if (endsHalf.TryGetValue(j, out var v))
                return v;
            if (Seq[j] == "W")
            {
                var gun = SeqGun[j]!;
                return g.Wy(gun, y) + gun.Reach;
            }
            return Seq[j] == "F" ? Fw / 2 : 0.0;
        }

        /// <summary>How far a wing turret at y stands from a neighbour half as wide: its barrels' sweep clear.</summary>
        static double WingGap(MainGroups g, Gun w, double y, double half)
        {
            y = g.Wy(w, y);
            if (half >= y - 0.2)
                return w.RR + 0.5;
            return Math.Max(0.5, Math.Sqrt(Math.Max(0.0, Math.Pow(w.Reach + 0.5, 2) - Math.Pow(y - half, 2))));
        }

        /// <summary>How far apart fore and aft an echelon pair stands, at y off the centreline.</summary>
        public double WingStagger(Gun w, double y)
        {
            double reach = w.Reach;
            y = g.Wy(w, y);
            if (reach - y > y - 0.3)
                return w.RR + reach + 0.5;
            double s = Math.Max(2 * reach + 1.0, Math.Sqrt(Math.Max(0.0, Math.Pow(2 * reach + 0.5, 2) - 4 * y * y)));
            if (w.Cross)
            {
                double c = double.DegreesToRadians(Arcs.ArcCross);
                s = Math.Max(s, (reach + 0.5 + 2 * y * Math.Sin(c)) / Math.Cos(c));
            }
            return s;
        }

        /// <summary>The room the wing turret item i needs on one side (step -1 forward, +1 aft): clear of its
        /// neighbours, of the end group beyond, and a cross-deck turret's fire across the deck.</summary>
        public double WingSide(int i, int step, double y)
        {
            var (F, A) = (g.F, g.A);
            var w = SeqGun[i]!;
            int j = i + step;
            double gp = WingGap(g, w, y, HalfOf(j, y));
            while (0 <= j && j < Seq.Count && (Seq[j] == "F" || Seq[j] == "E"))
                j += step;
            gp = Math.Max(gp, WingGap(g, w, y, HalfOf(j, y)));
            if ((step < 0 && g.Nf != 0) || (step > 0 && g.Na != 0))
            {
                var e = step < 0 ? F[^1] : A[^1];
                bool flush = step < 0 ? g.FlushF : g.FlushA;
                double beyond = (flush ? e.RR + 1.0 : e.R + e.Gap) + (step < 0 ? lb + 1.5 : (la != 0 ? la + 1.5 : 1.0));
                double yw = g.Wy(w, y);
                double need = e.Reach > yw - 0.3 || w.Cross ? w.RR + e.Reach + 0.5
                    : Math.Sqrt(Math.Max(0.0, Math.Pow(w.Reach + e.Reach + 0.5, 2) - yw * yw));
                gp = Math.Max(gp, need - beyond);
            }
            if (w.Cross)
            {
                int jj = i + step;
                double openL = 0.0;
                while (0 <= jj && jj < Seq.Count && Seq[jj] == "E")
                {
                    openL += Widths0[jj]!.Value;
                    jj += step;
                }
                if (0 <= jj && jj < Seq.Count && (HalfOf(jj, y) > 0 || Seq[jj] == "T"))
                    gp = Math.Max(gp, w.RR + 0.5 + (Seq[jj] == "T" ? SeqGun[jj]!.Reach : 0.0) - openL);
            }
            return gp;
        }

        /// <summary>The room a cross-deck wing turret at either end of the row needs beyond it (fore, aft), for its
        /// fire across the deck past the bridge or aft control.</summary>
        public (double Ef, double Ea) CrossEnds(double y)
        {
            var result = new List<double>();
            foreach (var (step, order) in new[] { (-1, Enumerable.Range(0, Seq.Count)), (1, Enumerable.Range(0, Seq.Count).Reverse()) })
            {
                int? k = order.Cast<int?>().FirstOrDefault(kk => Seq[kk!.Value] != "E");
                if (k is null || Seq[k.Value] != "W" || !SeqGun[k.Value]!.Cross || HalfOf(step < 0 ? -1 : Seq.Count, y) <= 0)
                {
                    result.Add(0.0);
                    continue;
                }
                double openL = ((step < 0 ? Enumerable.Range(0, k.Value) : Enumerable.Range(k.Value + 1, Seq.Count - k.Value - 1))
                    .Select(e => Widths0[e]!.Value)).Sum();
                result.Add(Math.Max(0.0, SeqGun[k.Value]!.RR + 0.5 - WingSide(k.Value, step, y) - openL));
            }
            return (result[0], result[1]);
        }

        Dictionary<int, double> WingWidths(double y) =>
            Enumerable.Range(0, Seq.Count).Where(i => Seq[i] == "W").ToDictionary(i => i,
                i => WingSide(i, -1, y) + WingSide(i, 1, y) + (SeqGun[i]!.Echelon ? WingStagger(SeqGun[i]!, y) : 0.0));

        /// <summary>Every item's length along the row with the wings y off the centreline.</summary>
        public List<double> PlanWidths(double y)
        {
            var ww = WingWidths(y);
            var result = new List<double>();
            for (int i = 0; i < Seq.Count; i++)
            {
                if (Seq[i] == "W")
                    result.Add(ww[i]);
                else if (Seq[i] == "T")
                {
                    int stow = i + 1 < Seq.Count && Seq[i + 1] == "T" ? 0 : 180;
                    int j = stow == 180 ? i - 1 : i + 1;
                    double margin = Math.Max(SeqGun[i]!.Reach + 1.0, HalfOf(j, y) / BeamTan + 0.5);
                    result.Add(margin + SeqGun[i]!.RR + 1.5);
                }
                else
                    result.Add(Widths0[i]!.Value);
            }
            return result;
        }

        /// <summary>The length between the end groups the middle needs, its items ws long and the cross-deck turrets'
        /// ends ext beyond: the bridge, the row and aft control, or the core with the lead and trail under them.</summary>
        public double MiddleNeeds(List<double> ws, (double Ef, double Ea) ext)
        {
            double before = Core.Count > 0 ? ws.Take(Core[0]).Sum() : 0.0;
            double cL = Core.Select(i => ws[i]).Sum();
            var (ef, ea) = ext;
            return Math.Max(lb + 1.5 + ef + ws.Sum() + ea + AftL, Math.Max(LeadL + cL + TrailL + 2.0, Math.Max(lb + 1.5 + ef + before + cL + TrailL + 1.0, LeadL + 1.0 + ws.Sum() - before + ea + AftL)));
        }

        /// <summary>Where the row's front goes (X) with its items widths long and the wings y off the centreline:
        /// between LoX (the row clear of aft control, the trail clear of the aft group) and HiX (clear of the bridge,
        /// the lead clear of the fore group), fz0 to fz1 the stretch between aft control and the bridge.</summary>
        public (double LoX, double HiX, double X) PlanFront(List<double> widths, double y, double fz0, double fz1, double midAft, double midFwd)
        {
            double beforeCore = Core.Count > 0 ? widths.Take(Core[0]).Sum() : 0.0;
            double coreL = Core.Select(i => widths[i]).Sum();
            var (ef, ea) = CrossEnds(y);
            double loX = Math.Max(fz0 + ea + widths.Sum(), midAft + 1.0 + TrailL + coreL + beforeCore);
            double hiX = Math.Min(fz1 - ef, midFwd - 1.0 - LeadL + beforeCore);
            return (loX, hiX, hiX >= loX ? (loX + hiX) / 2 : hiX);
        }
    }
}
