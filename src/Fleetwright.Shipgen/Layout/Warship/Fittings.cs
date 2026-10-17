namespace Fleetwright.Shipgen;

public sealed partial class Layout
{
    /// <summary>The AA mounts (BuildLayout), heavy then light: on the superstructure's roofs (pairs abreast, or one on
    /// the centreline), then along the deck edges, best nearest the machinery's middle and low; one right aft
    /// last.</summary>
    static List<AaMount> PlaceAaMounts(WarshipSite site, double machC, List<Block> blocks)
    {
        var (design, lay, L) = (site.Design, site.Lay, site.L);
        var aaOut = new List<AaMount>();

        // the AA candidates, best first, into cands
        void AaSlots(string kind, List<Slot> cands)
        {
            double rr = Geometry.AaCfg[kind].R;
            using var _s = Scratch<Slot>.Rent(out var slots);
            using var _k = Scratch<(double K, int I)>.Rent(out var scored);
            using var _r = Scratch<(double X, double Y, double Z0, bool Pair)>.Rent(out var spots);
            void Add(double s, Slot c)
            {
                scored.Add((s, slots.Count));
                slots.Add(c);
            }
            RoofSpots(blocks, 2 * rr, 2 * rr, spots);
            foreach (var (x, y, z0, pair) in spots)
            {
                long lvl = (long)Math.Round(z0 / LevelH);
                double pen = AaRoofPen[(int)Math.Min(lvl, AaRoofPen.Length - 1)];
                if (pair)
                    Add(Math.Abs(x - machC) / L + pen, new Slot(x, y, z0));
                else if (Math.Abs(y) < 1e-6)
                    Add(Math.Abs(x - machC) / L + pen + AaSinglePen, new Slot(x, 0.0, z0));
            }
            double xd = L / 2 - 0.06 * L;
            while (xd > -L / 2 + 2)
            {
                double yy = lay.DeckHalfWidth(xd) - rr - 0.5;
                if (yy > rr + 0.5)
                    Add(Math.Abs(xd - machC) / L + AaDeckPen, new Slot(xd, yy, lay.DeckZ(xd, rr)));
                xd -= 0.5;
            }
            KeyedSort.Sort(scored);
            foreach (var (_, i) in scored)
                cands.Add(slots[i]);
            double sx = -L / 2 + rr + 2.5;
            if (lay.DeckHalfWidth(sx) > rr + 0.6)
                cands.Add(new Slot(sx, 0.0, lay.DeckZ(sx, rr)));
        }

        void PlaceAa(string kind, long count)
        {
            using var _ = Scratch<Slot>.Rent(out var cands);
            AaSlots(kind, cands);
            Armament.PlaceAa(lay, aaOut, kind, count, cands);
        }

        PlaceAa("quad40", design.Aa?.Heavy ?? 0);
        PlaceAa("single20", design.Aa?.Light ?? 0);

        return aaOut;
    }

    /// <summary>The foremast at the bridge's after end (or through it when that spot is taken), over the funnels and
    /// clear of the tower's top; the mainmast on aft control. Their weights.</summary>
    static List<Mast> PlaceMasts(WarshipSite site, ControlPlan control, ControlPlaced ctl, double funTop)
    {
        var (lay, L, B) = (site.Lay, site.L, site.B);
        double lb = control.Lb, la = control.La, hood = control.Hood;
        long nTower = control.NTower;
        double bx0 = ctl.Bx0, ax0 = ctl.Ax0;
        double mastTop = funTop + 6.0;
        double fxM = lay.Clear(Footprint.Circle(bx0 - 1.2, 0, 0.7), mastTop) ? bx0 - 1.2 : bx0 + 0.3 * lb;
        var masts = new List<Mast> { new() { X = fxM, Yard = Math.Min(0.3 * B, 10), Tripod = L >= 150 } };
        if (LevelH * nTower + hood + 2.0 > mastTop)
            masts[0].Top = LevelH * nTower + hood + 2.0;
        if (la != 0)
            masts.Add(new Mast { X = ax0 + la * 0.5, Yard = Math.Min(0.22 * B, 8), Tripod = false });
        for (int k = 0; k < masts.Count; k++)
        {
            masts[k].Id = k == 0 ? "Foremast" : "Mainmast";
            MastWeight(lay, masts[k], masts[k].Top ?? mastTop, masts[k].Id!, lay.DeckZ(masts[k].X));
        }
        return masts;
    }

    /// <summary>Two boats a side abreast the machinery's middle: on the deckhouse roof beside the funnels (or on the
    /// deck at its edge without riders), where the deck under them is level and free.</summary>
    static List<Boat> PlaceBoats(WarshipSite site, MiddleRow row, double machC, List<(double X0, double X1, double Hw)> riders, Level1Placed level1)
    {
        var (lay, L, deckHw) = (site.Lay, site.L, site.DeckHw);
        double fw = row.Fw;
        var (dhW, dhIds) = (level1.DeckhouseW, level1.DeckhouseIds);
        var boats = new List<Boat>();
        double bl_ = Clamp(0.03 * L, 4, 8);
        foreach (var x in Enumerable.Range(-6, 13).Select(k => machC + k * 2.0))
        {
            if (boats.Count >= 2)
                break;
            double y = riders.Count > 0 ? dhW / 2 - 0.35 * bl_ - 0.6 : deckHw - 0.35 * bl_ - 1.0;
            var (lo_, hi_) = lay.DeckLevels(x, bl_ / 2);
            double z = riders.Count > 0 ? Math.Max(LevelH, hi_ * LevelH) : LevelH + hi_ * LevelH;
            var fps = new[] { 1, -1 }.Select(s => Footprint.Rect(x - bl_ / 2, s * y - 0.15 * bl_, x + bl_ / 2, s * y + 0.15 * bl_)).ToList();
            if (lo_ == hi_ && y > fw / 2 + 0.3 * bl_ && fps.All(fp => lay.Free(fp, 0.3, dhIds) && lay.Clear(fp, z + 1.5)))
            {
                foreach (var (s, fp) in new[] { (1, fps[0]), (-1, fps[1]) })
                {
                    boats.Add(new Boat(x, s * y, bl_, 0.3 * bl_));
                    lay.Occupy(fp, z, z + 1.5, $"Boat{boats.Count}");
                }
            }
        }

        return boats;
    }

    /// <summary>The citadel from the end turrets' barbettes over the machinery and magazines, the magazines and the
    /// machinery rooms in it (the funnels' segments renumbered to the plant's, without the magazines), and the steering
    /// gear.</summary>
    static void PlaceCitadel(WarshipSite site, MiddleRow row, MiddlePlaced middle, List<Mount> mounts)
    {
        var (design, lay, B, depth) = (site.Design, site.Lay, site.B, site.Depth);
        var (machPlaced, plantPlaced, magL) = (middle.Mach, middle.Plant, row.MagL);
        var mainMounts = mounts.Where(m => m.Kind == ComponentKind.Main).ToList();
        var block = (machPlaced.Select(p => p.X0).Min(), machPlaced.Select(p => p.X1).Max());
        (double, double) cit = mainMounts.Count > 0
            ? (Math.Min(mainMounts.Select(m => m.X - m.T.R).Min() - 2.0, block.Item1),
               Math.Max(mainMounts.Select(m => m.X + m.T.R).Max() + 2.0, block.Item2))
            : block;
        SetCitadel(lay, cit.Item1, cit.Item2);
        double innerHw = 0.8 * B / 2;
        var magX = machPlaced.Where(p => p.Kind == "magazine").Select(p => (p.X0, p.X1)).ToList();
        var magGroups = new Dictionary<string, (double X0, double X1)>();
        if (magL["fore"] > 0)
            magGroups["fore"] = magX[0];
        if (magL["aft"] > 0)
            magGroups["aft"] = magX[^1];
        AddMagazines(lay, mounts, innerHw, magGroups, machPlaced.Select(p => (p.X0, p.X1)).ToList(), Armour.MagazineWingM(design, B));
        var toPlant = new Dictionary<long, long>();
        int kk2 = 0;
        for (int i = 0; i < machPlaced.Count; i++)
            if (machPlaced[i].Kind != "magazine")
                toPlant[i] = kk2++;
        foreach (var f in lay.FunnelsPlanned)
            f.Seg = f.Seg is long sg && toPlant.TryGetValue(sg, out var tp2) ? tp2 : null;
        AddMachineryRooms(lay, plantPlaced, innerHw, depth);
        AddSteering(lay);

    }

    /// <summary>The dressing the sprites draw beyond the hitboxes: the boats, bollards, and the cable gear and
    /// breakwater on a forecastle long enough ahead of the fore group.</summary>
    static void Dress(WarshipSite site, MainGroups mg, EndsPlan ends, List<Boat> boats)
    {
        var (lay, L) = (site.Lay, site.L);
        var F = mg.F;
        var (fore, midFwd) = (ends.Fore, ends.MidFwd);
        double frontEdge = fore.Count > 0 ? fore[0] + F[0].R : midFwd;
        var spec = lay.Dressing;
        spec.Boats = boats;
        spec.Bollards = [L / 2 - 0.05 * L, -L / 2 + 0.06 * L];
        if (L / 2 - frontEdge > 0.08 * L + 6)
        {
            spec.ChainX = frontEdge + 0.35 * (L / 2 - frontEdge);
            spec.HawseBack = 0.03 * L + 1.0;
        }
        if (L >= 150 && fore.Count > 0 && (L / 2 - frontEdge) > 12)
            spec.BreakwaterX = frontEdge + 3.0;
    }
}
