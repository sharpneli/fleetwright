namespace Fleetwright.Shipgen;

public sealed partial class Layout
{
    /// <summary>The bridge tower and aft control placed (BuildLayout): the blocks so far (the tower's levels, aft
    /// control's), the tower's foot fore and aft (Bx0, Bx1: moved aft from the middle's front until it is clear of the
    /// fore group's sweeps) and aft control's after end (Ax0).</summary>
    sealed record ControlPlaced(List<Block> Blocks, double Bx0, double Bx1, double Ax0);

    /// <summary>The bridge tower (its base levels, the bridge, the upper bridge and the tower over it, tapering), the
    /// conning tower at its foot (armoured as the belt; none without one) and aft control, each kept clear of the
    /// turrets' sweeps.</summary>
    static ControlPlaced PlaceControl(WarshipSite site, ControlPlan control, MiddleRow row, EndsPlan ends)
    {
        var (lay, B) = (site.Lay, site.B);
        double beltMm = site.Design.Armour?.BeltMm ?? 0;
        double lb = control.Lb, la = control.La, hood = control.Hood, w2 = row.W2;
        long nb = control.Nb, nTower = control.NTower, naLvl = control.NaLvl;
        double midAft = ends.MidAft;
        double bx1 = ends.MidFwd;
        double bx0 = bx1 - lb;
        var blocks = new List<Block>();
        double towerTop = LevelH * nTower + hood;
        while (!lay.Clear(Footprint.Rect(bx0, -w2 / 2, bx1, w2 / 2), towerTop) && bx0 > midAft)
        {
            bx1 -= 0.5;
            bx0 -= 0.5;
        }
        double ax0 = midAft;
        while (la != 0 && !lay.Clear(Footprint.Rect(ax0, -0.14 * B, ax0 + la, 0.14 * B), LevelH * (naLvl + 1)) && ax0 + la < bx0)
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

        Block? Stack(string bid, long k, double x0_, double x1_, double w_, Block? below, string role = "bridge", bool office = false)
        {
            var b_ = AddLevel(lay, blocks, bid, k, x0_, x1_, w_, support: below?.Points,
                role: role, office: office);
            return b_ ?? below;
        }

        Block? belowB = null;
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
        if (beltMm > 0)
        {
            double ctR = Math.Min(Math.Max(0.1 * B, 1.25), Math.Min(4.0, 0.4 * w2));
            double ctX = Math.Max(bx1 - 0.42 * w2, bx0 + ctR);
            if (towerFoot != null)
            {
                var pts_ = towerFoot.Points!;
                ctX = towerFoot.X1 - ctR - 0.3;
                while (ctX > towerFoot.X0 + ctR &&
                       Enumerable.Range(0, pts_.Count).Select(i => SegDist(ctX, 0.0, pts_[(i - 1 + pts_.Count) % pts_.Count].X,
                           pts_[(i - 1 + pts_.Count) % pts_.Count].Y, pts_[i].X, pts_[i].Y)).Min() < ctR + 0.3)
                    ctX -= 0.25;
            }
            lay.ConningTower = new ConningTower(ctX, 0.0, ctR, 2 * LevelH);
            double mm = beltMm / 1000;
            double area = 2 * Math.PI * ctR * 2 * LevelH + 0.5 * Math.PI * Math.Pow(ctR, 2);
            lay.Weights.Add(new Weight("Conning tower", "armour", area * mm * 7.85, lay.ConningTower.X, ZRel.Deck(LevelH)));
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

        return new ControlPlaced(blocks, bx0, bx1, ax0);
    }

    /// <summary>The funnels: each over its boiler room, trunked aft toward the room's after end as far as the plan's
    /// reach allows and what stands there leaves room, and occupying the deck to their tops (a failure, and NeedMid,
    /// for one in a turret's sweep or against the bridge); their weights.</summary>
    static List<Funnel> PlaceFunnels(WarshipSite site, MiddleRow row, MiddlePlaced middle)
    {
        var (lay, depth) = (site.Lay, site.Depth);
        var (fxs, fSegF, machPlaced) = (middle.Fxs, middle.FSegF, middle.Mach);
        double fl = row.Fl, fw = row.Fw, funTop = row.FunTop;
        var fplan = row.Fplan;
        // funnels trunked aft toward the boundary with what lies aft of the boilers
        Footprint FunFp(double x) => Footprint.Rect(x - fl / 2, -fw / 2, x + fl / 2, fw / 2);
        double reachLim = fplan.Reach + fl / 2;

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

        var funnels = new List<Funnel>();
        for (int i = 0; i < fxFinal.Count; i++)
        {
            double fx = fxFinal[i];
            string fid = $"Funnel {i + 1}";
            var fp = FunFp(fx);
            if (!lay.Clear(fp, funTop) || !lay.Free(fp, 0.0, [fid]))
                lay.FailMid(fl + 1.0, $"{fid} would stand in a turret's sweep or against the bridge: use fewer funnels " +
                                   "or midships turrets.");
            funnels.Add(new Funnel { Id = fid, X = fx, Y = 0.0, L = fl, W = fw, Pipes = fw > 4 ? 2 : 1, Seg = fSegF[i] });
            if (lay.DeckZ(fx, fl / 2) != 0)
                funnels[^1].Z0 = lay.DeckZ(fx, fl / 2);
            var seg = machPlaced[fSegF[i]];
            AddFunnelWeights(lay, funnels[^1], funTop, (seg.X0 + seg.X1) / 2, depth);
        }

        return funnels;
    }
}
