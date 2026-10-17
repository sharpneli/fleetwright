namespace Fleetwright.Shipgen;

public sealed partial class Layout
{
    /// <summary>The middle placed (BuildLayout): the funnels' x and their plant segments, the midships turrets (x,
    /// and the way they stow: 0 forward, 180 aft), the wing pairs (their offset from the centreline and each turret's
    /// x and side), the plant's and magazines' segments along the hull (Mach; Plant without the magazines) and the
    /// machinery's middle (MachC).</summary>
    sealed record MiddlePlaced(List<double> Fxs, List<int> FSegF, List<(Gun G, double X, int Stow)> Mids,
        List<(Gun G, double Y, List<(double X, int Side)> Pair)> Wings, List<(string Kind, double X0, double X1)> Mach,
        List<(string Kind, double X0, double X1)> Plant, double MachC)
    {
        /// <summary>The row placed between the bridge and aft control: the wings as far out as the deck allows (or
        /// a failure when it's too narrow for them), the row's front where the room for it is, a failure and NeedMid
        /// when the middle is too short; then each item and segment where the row puts it (Geo.Machinery and
        /// Geo.MachineryX).</summary>
        public static MiddlePlaced Of(WarshipSite site, MainGroups mg, ControlPlan control, MiddleRow row, EndsPlan ends)
        {
            var (lay, B) = (site.Lay, site.B);
            var (bats, WA, WE, gRef) = (mg.All, mg.WA, mg.WE, mg.WingRef);
            int nm = mg.Nm, nw = mg.Nw;
            var Wy = mg.Wy;
            double lb = control.Lb, la = control.La;
            var (midFwd, midAft) = (ends.MidFwd, ends.MidAft);
            var (segs, seq, segOf, seqGun, core, lead, trail, ubSeg) = (row.Segs, row.Seq, row.SegOf, row.SeqGun, row.Core, row.Lead, row.Trail, row.UbSeg);
            long nfun = row.NFun;
            double beamTan = row.BeamTan;

            double bx1 = midFwd;
            double bx0 = bx1 - lb;
            double fz0 = midAft + (la != 0 ? la + 1.5 : 1.0);
            double fz1 = bx0 - 1.5;
            double yW = nw != 0 ? Enumerable.Range(0, 21).Select(k => fz0 + (fz1 - fz0) * k / 20)
                .Min(x => Math.Min(lay.MountHalfWidth(x) - gRef!.Reach - 0.6, lay.BarbetteLimit(x, BarbetteOf(gRef.T)))) : 0.0;

            (double LoX, double HiX, double X) PlanFront(List<double> widths, double y) => row.PlanFront(widths, y, fz0, fz1, midAft, midFwd);

            double WingRoom(double y)
            {
                var widths = row.PlanWidths(y);
                double xx = PlanFront(widths, y).X, room = B - gRef!.Reach - 0.6;
                for (int i = 0; i < seq.Count; i++)
                {
                    if (seq[i] == "W")
                    {
                        var g = seqGun[i]!;
                        double x = xx - row.WingSide(i, -1, y);
                        double hw = B;
                        foreach (var xw in g.Echelon ? new[] { x, x - row.WingStagger(g, y) } : [x])
                            hw = Math.Min(hw, Math.Min(lay.MountHalfWidth(xw + -g.Reach), Math.Min(lay.MountHalfWidth(xw + 0.0), lay.MountHalfWidth(xw + g.Reach))));
                        double lim = hw - g.Reach - 0.6;
                        foreach (var xw in g.Echelon ? new[] { x, x - row.WingStagger(g, y) } : [x])
                            lim = Math.Min(lim, lay.BarbetteLimit(xw, BarbetteOf(g.T)));
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
            var widthsF = row.PlanWidths(yW);
            double beforeCoreF = core.Count > 0 ? widthsF.Take(core[0]).Sum() : 0.0;
            double coreLF = core.Select(i => widthsF[i]).Sum();
            var (loX, hiX, xx0) = PlanFront(widthsF, yW);
            if (hiX < loX - 0.5)
            {
                lay.NeedMid += loX - hiX;
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
                    double x = xx - row.WingSide(i, -1, yW);
                    wings.Add((g, Wy(g, yW), g.Echelon ? [(x, -1), (x - row.WingStagger(g, yW), 1)] : [(x, -1), (x, 1)]));
                }
                else if (it == "T")
                {
                    int stow = i + 1 < seq.Count && seq[i + 1] == "T" ? 0 : 180;
                    int j = stow == 180 ? i - 1 : i + 1;
                    double margin = Math.Max(seqGun[i]!.Reach + 1.0, row.HalfOf(j, yW) / beamTan + 0.5);
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

            return new MiddlePlaced(fxs, fSegF, mids, wings, machPlaced, plantPlaced, machC);
        }
    }

    /// <summary>The raised stretches of hull (hull.raised) between the anchors the design names, the edges of the
    /// parts placed so far (a warning for an anchor the ship hasn't, or a stretch with nothing to raise).</summary>
    static void PlaceRaised(WarshipSite site, MainGroups mg, ControlPlan control, EndsPlan ends, MiddlePlaced placed, double fl)
    {
        var (design, lay, L) = (site.Design, site.Lay, site.L);
        var (F, A) = (mg.F, mg.A);
        double la = control.La;
        var (fore, aft, midFwd, midAft) = (ends.Fore, ends.Aft, ends.MidFwd, ends.MidAft);
        var fxs = placed.Fxs;
        double bx0 = midFwd - control.Lb;
        var raisedIn = design.Hull?.Raised ?? [];
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
            int a = Array.IndexOf(RaisedAnchors, q.From), b = Array.IndexOf(RaisedAnchors, q.To);
            int i = Math.Min(a, b), j = Math.Max(a, b);
            int i0 = i, j0 = j;
            while (i <= j && edges[RaisedAnchors[i]].Fwd is null)
                i++;
            while (i <= j && edges[RaisedAnchors[j]].Aft is null)
                j--;
            string Name(int k) => RaisedAnchors[k].Replace('_', ' ');
            string what = $"the raised deck from the {q.From!.Replace('_', ' ')} to the {q.To!.Replace('_', ' ')}";
            var (x0_, x1_) = i <= j ? (edges[RaisedAnchors[j]].Aft!.Value, edges[RaisedAnchors[i]].Fwd!.Value) : (0.0, 0.0);
            if (i > j || x1_ - x0_ < 0.5)
            {
                lay.Warnings.Add($"hull.raised: nothing to raise {what} over; it is left out.");
                continue;
            }
            foreach (var (k, k0) in new[] { (i, i0), (j, j0) })
                if (k != k0)
                    lay.Warnings.Add($"hull.raised: no {Name(k0)} for {what}; it runs to the {Name(k)}.");
            spans.Add((x0_, x1_, q.Decks ?? 1));
        }
        foreach (var (rid, x0_, x1_, lv, brk) in RaisedNames(RaisedProfile(spans, L), L))
            AddRaised(lay, design, rid, x0_, x1_, lv, brk);

    }
}
