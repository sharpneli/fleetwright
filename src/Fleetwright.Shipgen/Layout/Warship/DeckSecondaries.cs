namespace Fleetwright.Shipgen;

public sealed partial class Layout
{
    /// <summary>The length the deck secondaries need amidships, each battery's mounts at their pitch in a row with
    /// room for the first and last; the batteries one after another (the layout may well fit them closer).</summary>
    static double SecondarySpan(IEnumerable<BatteryInput> secs)
    {
        double span = 0.0;
        foreach (var sec in secs)
        {
            long n = sec.MountsPerSide;
            if ((sec.Mount ?? "deck") != "deck" || n == 0)
                continue;
            var ts = Geometry.BatteryType(sec).T;
            double reach = Math.Max(ts.R, Geometry.TurretReach(ts, 0.0)), pitch = 2.1 * reach + 1.0;
            span += span == 0.0 ? (n - 1) * pitch + 2 * (reach + 0.5) : n * pitch;
        }
        return span;
    }

    /// <summary>A secondary battery on deck (BuildLayout), mounts in pairs abreast amidships: outboard of the
    /// superstructure and funnels beside them, kept apart from the turrets and from what stands on the other level
    /// (deck or deckhouse), barrels clear when stowed, spread evenly or (beside wing turrets, after the first battery,
    /// or when the even spread doesn't fit) at the free spots nearest the middle. A failure, and Short or NeedMid for the
    /// sizer, for what doesn't fit; a battery on the deckhouse adds its riders. First: the first battery on deck.
    /// Returns false for a battery not on deck, or of no mounts.</summary>
    static bool PlaceDeckBattery(WarshipSite site, MainGroups mg, MiddleRow row, EndsPlan ends, BatteryInput sec, string pre, bool first,
        OrderedDictionary<string, TurretType> turretTypes, List<Mount> mounts, List<(double X0, double X1, double Hw)> riders,
        List<Block> blocks, List<Funnel> funnels)
    {
        var (lay, depth, deckRatio) = (site.Lay, site.Depth, site.DeckRatio);
        var M = mg.M;
        int nw = mg.Nw;
        double fw = row.Fw;
        var (midFwd, midAft) = (ends.MidFwd, ends.MidAft);
        long nsec = sec.MountsPerSide;
        if ((sec.Mount ?? "deck") != "deck" || nsec == 0)
            return false;
        string cal = $"{sec.CalibreMm} mm";
        bool raised = sec.StandsOn == "deckhouse";
        var (tsId, ts) = Geometry.BatteryType(sec);
        turretTypes[tsId] = ts;
        double rs = ts.R;
        double rsReach = Math.Max(rs, Geometry.TurretReach(ts, 0.0));
        double ths = Geometry.TurretHeight(ts);

        double SecBase(double x) => raised ? Math.Max(lay.DeckZ(x, rsReach), LevelH) : lay.DeckZ(x, rsReach);

        // batteries on the deck and on the deckhouse kept apart: a deck mount stays out of the level 1 that what
        // rides on it needs, and a raised mount's level 1 out of the deck mounts and turrets standing on the deck
        // (level 1 is notched in to what stands beside it, with 45-degree shoulders: the same rule)
        var onDeck = mounts.Where(m => m.Base < LevelH && !m.Casemate && !lay.EndMounts.Contains(m))
            .Select(m => (m.X, Y: Math.Abs(m.Y), R: Math.Max(m.T.R, Geometry.TurretReach(m.T, 0.0)))).ToList();
        // (and out of the barrel sweeps of the turrets on the deck that pass under its roof)
        var deckSwept = mounts.Where(m => m.Kind == ComponentKind.Main && (m.Wing || m.Midships) && m.Base < LevelH).Select(m => m.Id)
            .ToHashSet();
        var sweptBoxes = lay.Sweeps.Where(sw => deckSwept.Contains(sw.Owner) && sw.Axis < LevelH).SelectMany(sw => sw.Boxes)
            .ToList();
        static bool Apart((double X0, double X1, double Hw) r, double mx, double my, double mr) =>
            my - mr - FpMargin + Math.Max(0.0, Math.Max(r.X0 - (mx + mr), mx - mr - r.X1)) >= r.Hw;

        bool RiderOk(double x)
        {
            var zone = (X0: x - rsReach - DhInset, X1: x + rsReach + DhInset, Hw: YAt(x) + rsReach + DhInset);
            return onDeck.All(d => Apart(zone, d.X, d.Y, d.R)) && sweptBoxes.All(b =>
                (b.Y0 > 0 ? b.Y0 : b.Y1 < 0 ? -b.Y1 : 0.0) - FpMargin + Math.Max(0.0, Math.Max(zone.X0 - b.X1, b.X0 - zone.X1))
                >= zone.Hw);
        }

        bool ApartOk(double x) => raised
            ? lay.DeckZ(x, rsReach) >= LevelH || RiderOk(x)
            : riders.All(r => Apart(r, x, YAt(x), rsReach));
        double xLo = midAft + rsReach + 0.5, xHi = midFwd - rsReach - 0.5;
        // what a mount must stand outboard of where it stands: the superstructure over level 1 and the funnels beside
        // it, not the widest anywhere (a gun abreast the after funnel needn't clear the bridge, and a beam asked
        // of the bridge's width, a share of the beam, would never be enough), and the midships turrets' reach
        double innerMain = M.Select(g => g.Reach).Append(0.0).Max();
        double InnerAt(double x)
        {
            double hw = innerMain;
            foreach (var b in blocks)
                if (b.Level >= 2 && b.X0 - rsReach - 0.4 < x && x < b.X1 + rsReach + 0.4)
                    hw = Math.Max(hw, Math.Abs(b.Y) + b.W / 2);
            foreach (var f in funnels)
                if (Math.Abs(f.X - x) < f.L / 2 + rsReach + 0.4)
                    hw = Math.Max(hw, f.W / 2);
            return hw + rsReach + 0.4;
        }

        double OuterAt(double x) => Math.Min(Math.Min(lay.MountHalfWidth(x + -rsReach), Math.Min(lay.MountHalfWidth(x + 0.0), lay.MountHalfWidth(x + rsReach))) - rsReach - 0.6,
            lay.BarbetteLimit(x, BarbetteOf(ts)));

        bool Room(double x) => OuterAt(x) >= InnerAt(x);
        // where across the deck: past the widest block anywhere, a share of the way to the edge, where the deck is
        // wide enough for that; on a narrower deck, as far out as it goes (a spot with Room stays clear of what's
        // beside it)
        double clearAll = blocks.Where(b => b.Level >= 2).Select(b => b.W / 2).Append(fw / 2).Append(innerMain).Max()
                          + rsReach + 0.4;
        double YAt(double x)
        {
            double outer = OuterAt(x);
            return Math.Min(outer, clearAll + 0.55 * (outer - clearAll));
        }
        var xsProbe = Enumerable.Range(0, 41).Select(k => xLo + (xHi - xLo) * k / 40).ToList();
        double xLo0 = xLo, xHi0 = xHi;
        if (!xsProbe.Any(Room))
        {
            double inner = xsProbe.Min(InnerAt);   // where they'd need the least
            lay.NeedB = Math.Max(lay.NeedB, 2 * (inner + rsReach + 0.6) / deckRatio);
            lay.Fail("beam", $"Hull too narrow for {cal} secondary mounts: they need about " +
                             $"{2 * (inner + rsReach + 0.6):F1} m of beam amidships.");
        }
        else
        {
            var fitsX = xsProbe.Where(Room).ToList();
            (xLo, xHi) = (fitsX.Min(), fitsX.Max());
        }

        // what the battery came up short of, counted once (below)
        double secShort = 0.0;

        void ShortOf(string msg, double metres)
        {
            lay.Fail("length", msg);
            secShort = Math.Max(secShort, Math.Max(0.5, metres));
        }

        double pitchS = 2.1 * rsReach + 1.0;
        double pitchStowed = Geometry.TurretReach(ts) + rsReach + 0.4;

        bool PitchOk(double x, IEnumerable<double> others, double sp) =>
            others.All(o => Math.Abs(x - o) >= sp && ((x >= 0) != (o >= 0) || Math.Abs(x - o) >= pitchStowed));

        bool BarrelsOk(double x)
        {
            double b = SecBase(x);
            var band = Armament.BarrelBand(b, b + ths, ts);
            var fpS = Armament.BarrelFootprint(ts, x, YAt(x), Armament.StowBearing(x, 1, 90.0));
            var fpP = Armament.BarrelFootprint(ts, x, -YAt(x), Armament.StowBearing(x, -1, 90.0));
            return lay.FreeAt(fpS, band.Lo, band.Hi, 0.2) && lay.Clear(fpS, band.Hi) && lay.FreeAt(fpP, band.Lo, band.Hi, 0.2)
                   && lay.Clear(fpP, band.Hi);
        }

        if (xHi <= xLo)
            lay.FailMid((nsec - 1) * pitchS + 1.0 - (xHi - xLo), "No room amidships for the secondary battery.");
        else if (first && nsec > 1 && (xHi - xLo) / (nsec - 1) < pitchS)
            ShortOf($"{nsec} secondary mounts per side do not fit in {xHi - xLo:F0} m amidships " +
                    $"(max {(long)((xHi - xLo) / pitchS) + 1}).", (nsec - 1) * pitchS - (xHi - xLo));
        double step = Math.Min((xHi - xLo) / Math.Max(nsec - 1, 1), 2.2 * rs + 4.0);
        double c = (xLo + xHi) / 2;
        var sxs = Enumerable.Range(0, (int)nsec).Select(i => nsec > 1 ? c + (i - (nsec - 1) / 2.0) * step : c).ToList();
        bool spreadOk = Enumerable.Range(0, sxs.Count).All(i => Room(sxs[i]) && PitchOk(sxs[i], sxs.Take(i), 0.0) && BarrelsOk(sxs[i])
                                                                && ApartOk(sxs[i]));
        if (nw != 0 || !first || !spreadOk)
        {
            bool SpotOk(double x)
            {
                if (!Room(x))
                    return false;   // abreast something too wide here: the deck beside it is too narrow
                for (int s = 1; s >= -1; s -= 2)
                {
                    var fp = Footprint.Circle(x, s * YAt(x), rsReach);
                    if (!lay.Free(fp, 0.4) || !lay.Clear(fp, SecBase(x) + ths))
                        return false;
                }
                return BarrelsOk(x) && ApartOk(x);
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
                    (nsec - sxsCount) * pitchS);
            }
            sxs = sxs.Order().ToList();
        }
        for (int i = 0; i < sxs.Count; i++)
        {
            double sx = sxs[i];
            double yS = YAt(sx);
            if (!lay.Clear(Footprint.Circle(sx, yS, rsReach), SecBase(sx) + ths))
                lay.FailMid(pitchS, $"Secondary mounts {pre}{i + 1} would stand in a main turret's sweep: use fewer " +
                                   "secondaries.");
            foreach (int side in new[] { 1, -1 })
            {
                string mid = $"{pre}{i + 1}{(side > 0 ? "S" : "P")}";
                var m = Armament.AddMount(lay, mounts, ComponentKind.Secondary, tsId, ts, mid, sx, side * yS, SecBase(sx),
                    Armament.StowBearing(sx, side, 90.0), armourMm: sec.ArmourMm!.Value, depth: depth, top: SecBase(sx) + ths,
                    footprintR: rsReach, deck: lay.DeckZ(sx, rsReach), sideMount: true);
                (m.Material, m.Battery) = (sec.Material, pre);
            }
        }
        if (raised && sxs.Count > 0)
        {
            double yS = sxs.Select(YAt).Max();
            riders.AddRange(sxs.Where(sx => lay.DeckZ(sx, rsReach) < LevelH)
                .Select(sx => (sx - rsReach - DhInset, sx + rsReach + DhInset, yS + rsReach + DhInset)));
        }
        // short where the deck beside the superstructure is too narrow for them, with the middle long enough: a wider
        // hull would fit them, a longer middle wouldn't
        // (mounts all on one side of amidships stow their barrels the same way: further apart)
        double pitchEff = xHi < 0 || xLo > 0 ? Math.Max(pitchS, pitchStowed) : pitchS;
        if (secShort > 0 && (nsec - 1) * pitchEff > xHi - xLo && (nsec - 1) * pitchEff <= xHi0 - xLo0)
            lay.Short.Add("beam");
        else if (secShort > 0)
            lay.NeedMid += secShort;
        return true;
    }
}
