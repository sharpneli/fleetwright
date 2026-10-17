namespace Fleetwright.Shipgen;

public sealed partial class Layout
{
    /// <summary>Level 1 placed (BuildLayout): its deckhouse blocks, their ids, and the deckhouse's width (what the
    /// levels above it and the boats go by).</summary>
    sealed record Level1Placed(List<Block> Deckhouse, List<string> DeckhouseIds, double DeckhouseW);

    /// <summary>Level 1: one deckhouse under the riders (what stands on the deckhouse over the deck), joined to the
    /// one under the bridge and to aft control's base when the deck between is free, cut where something stands on the
    /// deck near the centreline or a raised stretch runs, and notched in to what stands beside it; the blocks go first in
    /// the list. Funnels and casemate housings run through it.</summary>
    static Level1Placed PlaceLevel1(WarshipSite site, ControlPlan control, ControlPlaced ctl, List<(double X0, double X1, double Hw)> riders,
        List<Funnel> funnels, List<Block> housings, List<Mount> mounts, List<Block> blocks)
    {
        var (lay, B) = (site.Lay, site.B);
        double la = control.La;
        double bx0 = ctl.Bx0, bx1 = ctl.Bx1, ax0 = ctl.Ax0;
        (double X0, double X1)? dh = (bx0 - 3.0, bx1 + 1.0);
        double dhW = 0.62 * B;
        var pieces = new List<(string Id, double X0, double X1, double W, string Role)>();
        bool aftOn = false;
        if (riders.Count > 0)
        {
            double rx0 = riders.Select(r => r.X0).Min(), rx1 = riders.Select(r => r.X1).Max();
            double rw = 2 * riders.Select(r => r.Hw).Max();
            var through = funnels.Select(f => f.Id).Concat(housings.Select(h => h.Id)).ToList();

            bool DeckFree(double x0_, double x1_)
            {
                if (x1_ <= x0_)
                    return true;
                var fp = Footprint.Rect(x0_, -rw / 2, x1_, rw / 2);
                return lay.FreeAt(fp, 0.0, LevelH, FpMargin, through) && lay.Clear(fp, LevelH);
            }

            if (DeckFree(rx1, dh.Value.X0))
            {
                (rx0, rx1, rw) = (Math.Min(rx0, dh.Value.X0), Math.Max(rx1, dh.Value.X1), Math.Max(rw, dhW));
                dh = null;
            }
            if (la != 0 && DeckFree(ax0 + la, rx0))
                (rx0, aftOn) = (Math.Min(rx0, ax0), true);
            double hwMax = Enumerable.Range(0, 21).Select(k => lay.DeckHalfWidth(rx0 + (rx1 - rx0) * k / 20)).Max();
            pieces.Add(("Deckhouse", rx0, rx1, Math.Min(rw, 2 * (hwMax - DhInset)), "deckhouse"));
        }
        if (dh is { } dhv)
            pieces.Add((pieces.Count > 0 ? "Deckhouse-2" : "Deckhouse", dhv.X0, dhv.X1, dhW, "deckhouse"));
        if (la != 0 && !aftOn)
            pieces.Add(("Aft control base 1", ax0, ax0 + la, 0.28 * B, "aft_control"));

        Footprint DhRect(double x0_, double x1_)
        {
            double hw = Math.Min(lay.DeckHalfWidth(x0_), lay.DeckHalfWidth(x1_)) - 0.6;
            return Footprint.Rect(x0_, -hw, x1_, hw);
        }

        var raisedIds = lay.Raised.Select(s => s.Id).ToList();
        // what stands on the deck at level 1's height (wing turrets and secondaries on the deck beside the riders):
        // level 1 is cut round it where it stands near the centreline and notched in to it at the side. Funnels and
        // casemate housings (and their guns) run through level 1, raised decks cut it above, and the end turrets trim its
        // ends.
        var notIn = funnels.Select(f => f.Id).Concat(housings.Select(h => h.Id)).Concat(raisedIds)
            .Concat(lay.EndMounts.Select(m => m.Id)).Concat(mounts.Where(m => m.Casemate).Select(m => m.Id))
            .Concat(blocks.Select(b => b.Id)).ToHashSet();
        var splits = new List<(double X0, double X1)>();
        var yields = new List<(double N0, double N1, double H)>();
        foreach (var o in lay.Footprints)
        {
            if (o.Base >= LevelH - 0.01 || o.Top <= 0.01 || notIn.Contains(o.Owner))
                continue;
            var (fx0, fy0, fx1, fy1) = o.Fp.BBox;
            double h = (fy0 > 0 ? fy0 : fy1 < 0 ? -fy1 : 0.0) - FpMargin;
            if (h < DhMinW / 2)
                splits.Add((fx0 - FpMargin, fx1 + FpMargin));
            else
                yields.Add((fx0 - FpMargin, fx1 + FpMargin, h));
        }
        // the barrel sweeps of the wing and midships turrets on the deck the same way, where they pass under the roof
        var deckTurrets = mounts.Where(m => m.Kind == "main" && (m.Wing || m.Midships) && m.Base < LevelH)
            .Select(m => m.Id).ToHashSet();
        foreach (var sw in lay.Sweeps.Where(sw => deckTurrets.Contains(sw.Owner) && sw.Axis < LevelH))
            foreach (var (sx0, sy0, sx1, sy1) in sw.Boxes)
            {
                double h = (sy0 > 0 ? sy0 : sy1 < 0 ? -sy1 : 0.0) - FpMargin;
                if (h < DhMinW / 2)
                    splits.Add((sx0 - FpMargin, sx1 + FpMargin));
                else
                    yields.Add((sx0 - FpMargin, sx1 + FpMargin, h));
            }
        var cut = new List<(string Id, double X0, double X1, double W, string Role)>();
        foreach (var (pid, x0p, x1p, wp, role) in pieces)
        {
            var runs = new List<(double A, double B)> { (x0p, x1p) };
            foreach (var (sx0, sx1) in lay.Raised.Select(s_ => (s_.X0, s_.X1)).Concat(splits))
                runs = runs.SelectMany(r => new[] { (r.A, Math.Min(r.B, sx0)), (Math.Max(r.A, sx1), r.B) })
                    .Where(q => q.Item2 > q.Item1).ToList();
            runs = runs.Where(r => r.B - r.A >= DhSliver || (r.A == x0p && r.B == x1p) || lay.Footprints.Any(o =>
                o.Base >= LevelH - 0.01 && r.A <= (o.Fp.BBox.X0 + o.Fp.BBox.X1) / 2 && (o.Fp.BBox.X0 + o.Fp.BBox.X1) / 2 <= r.B
                && !raisedIds.Contains(o.Owner) && !lay.Overhangs.Contains(o.Fp))).ToList();
            runs.Reverse();
            cut.AddRange(runs.Select((r, k) => (pid + (k != 0 ? $" part {k + 1}" : ""), r.A, r.B, wp, role)));
        }
        var level1 = new List<Block>();
        foreach (var (pid, x0c, x1c, wc, role) in cut)
        {
            double x0_ = x0c, x1_ = x1c;
            while (!lay.Clear(DhRect(x0_, x1_), LevelH, deckTurrets) && x1_ - x0_ > 4)
            {
                if (lay.Clear(DhRect(x0_, (x0_ + x1_) / 2), LevelH, deckTurrets))
                    x1_ -= 0.5;
                else
                    x0_ += 0.5;
            }
            var onRoof = lay.Footprints.Where(o => o.Base >= LevelH - 0.01 && !lay.Overhangs.Contains(o.Fp)
                                                 && x0_ <= (o.Fp.BBox.X0 + o.Fp.BBox.X1) / 2 && (o.Fp.BBox.X0 + o.Fp.BBox.X1) / 2 <= x1_)
                .Select(o => o.Fp).ToList();
            var keep = onRoof.Select(fp => fp.BBox).ToList();
            // a notch never cuts from under what stands on the roof, its 45-degree shoulders included
            var roofPts = onRoof.SelectMany(fp => fp.Kind == 'c'
                ? Enumerable.Range(0, 32).Select(k => new Pt(fp.X + fp.R * Math.Cos(k * Math.PI / 16), fp.Y + fp.R * Math.Sin(k * Math.PI / 16)))
                : fp.Points()).ToList();
            var mine = yields.Where(nt => nt.N1 > x0_ && nt.N0 < x1_).Select(nt => (nt.N0, nt.N1, H: roofPts.Select(p =>
                Math.Abs(p.Y) + FpMargin - Math.Max(0.0, Math.Max(nt.N0 - p.X, p.X - nt.N1))).Append(nt.H).Max())).ToList();
            var b_ = AddLevel(lay, blocks, pid, 1, x0_, x1_, wc, keep: keep,
                ignore: funnels.Select(f => f.Id).Concat(housings.Select(h => h.Id)).Concat(raisedIds).ToList(),
                yields: mine, role: role, office: role == "aft_control");
            if (b_ != null)
            {
                var last = blocks[^1];
                blocks.RemoveAt(blocks.Count - 1);
                blocks.Insert(0, last);
                level1.Add(b_);
            }
        }
        var dhBlocks = level1.Where(b => b.Role == "deckhouse").ToList();
        var dhIds = dhBlocks.Select(b => b.Id).ToList();
        dhW = pieces.Where(p => p.Role == "deckhouse").Select(p => (double?)p.W).FirstOrDefault() ?? dhW;

        return new Level1Placed(dhBlocks, dhIds, dhW);
    }

    /// <summary>The deck torpedo mounts: in pairs at the deck edges nearest the machinery's middle (when the plan
    /// says the deck is wide enough; an odd count rounded up), else on the centreline in the middle, else on the
    /// deckhouse roof; a failure, and NeedMid, for those that don't fit.</summary>
    static void PlaceTorpedoMounts(WarshipSite site, TorpedoPlan torp, EndsPlan ends, double machC, Level1Placed level1,
        OrderedDictionary<string, TurretType> turretTypes, List<Mount> mounts)
    {
        var (lay, L) = (site.Lay, site.L);
        long ntp = torp.Mounts;
        var (ttId, tt, tSweep, tEdges) = (torp.TypeId, torp.T, torp.Sweep, torp.Edges);
        var (midFwd, midAft) = (ends.MidFwd, ends.MidAft);
        var (dhBlocks, dhIds) = (level1.Deckhouse, level1.DeckhouseIds);
        if (ntp != 0)
        {
            turretTypes[ttId!] = tt!;
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
                    double y = lay.DeckHalfWidth(x) - tt!.R - 0.8;
                    if (y < sweep)
                        continue;
                    var fps = new[] { Footprint.Circle(x, y, sweep), Footprint.Circle(x, -y, sweep) };
                    double dz = lay.DeckZ(x, sweep);
                    if (fps.All(fp => lay.Free(fp, 0.3) && lay.Clear(fp, dz + 1.4)))
                    {
                        foreach (var (side, fp) in new[] { (1, fps[0]), (-1, fps[1]) })
                        {
                            string mid = $"T{placed / 2 + 1}{(side > 0 ? "S" : "P")}";
                            mounts.Add(new Mount
                            {
                                Id = mid, Kind = "torpedo", Type = ttId!, T = tt!, X = x, Y = side * y, Base = dz + 0.3, Top = dz + 1.4,
                                Rest = 90 * side,
                            });
                            lay.Occupy(fp, dz, dz + 1.4, mid);
                            lay.Weights.Add(new Weight(mid, "armament", Batteries.TorpedoWeight(tt!.Barrels), x, ZRel.Deck(dz + 1)));
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
                    if (lay.Free(fp, 0.3) && lay.Clear(fp, dz + 1.4) && lay.DeckHalfWidth(x) > tt!.R + 0.5)
                    {
                        string mid = $"T{placed + 1}";
                        mounts.Add(new Mount { Id = mid, Kind = "torpedo", Type = ttId!, T = tt, X = x, Base = dz + 0.3, Top = dz + 1.4, Rest = 90 });
                        lay.Occupy(fp, dz, dz + 1.4, mid);
                        lay.Weights.Add(new Weight(mid, "armament", Batteries.TorpedoWeight(tt.Barrels), x, ZRel.Deck(dz + 1)));
                        placed++;
                    }
                }
            }
            if (placed < ntp && dhBlocks.Count > 0)
            {
                var cands = dhBlocks.SelectMany(b_ => Enumerable.Range(0, Math.Max(0, (int)((b_.X1 - b_.X0 - 2 * sweep) * 2) + 1))
                    .Select(k => b_.X0 + sweep + 0.5 * k)).OrderBy(x => Math.Abs(x - machC)).ToList();
                foreach (var x in cands)
                {
                    if (placed >= ntp)
                        break;
                    var fp = Footprint.Circle(x, 0, sweep);
                    if (lay.Free(fp, 0.3, dhIds) && lay.Clear(fp, LevelH + 1.4))
                    {
                        string mid = $"T{placed + 1}";
                        mounts.Add(new Mount
                        {
                            Id = mid, Kind = "torpedo", Type = ttId!, T = tt!, X = x, Level = 1, Base = LevelH + 0.3, Top = LevelH + 1.4, Rest = 90,
                        });
                        lay.Occupy(fp, LevelH, LevelH + 1.4, mid);
                        lay.Weights.Add(new Weight(mid, "armament", Batteries.TorpedoWeight(tt!.Barrels), x, ZRel.Deck(LevelH + 1)));
                        placed++;
                    }
                }
            }
            if (placed < ntp)
                lay.FailMid((ntp - placed) * (sweep + 0.3), $"Only {placed} of {ntp} torpedo mounts fit on deck.");
        }
    }
}
