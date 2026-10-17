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

    /// <summary>The warship layout. The middle (machinery, funnels, midships and wing turrets, the secondaries beside
    /// them) gets the length its contents need plus midExtra (what an earlier layout found short there), the end turret
    /// groups close up on it, and any length to spare goes to the ends. plan: stop once the turret groups and the middle
    /// are placed (Deficit, the citadel and the shift range set), for sizing the hull.</summary>
    public static Layout BuildLayout(Design design, Navarch.Result res, double shift = 0.0, double midExtra = 0.0, bool plan = false)
    {
        var site = new WarshipSite(design, res);
        var (lay, hs, hull) = (site.Lay, site.Hs, site.Hull);
        double depth = site.Depth, L = site.L, B = site.B, deckHw = site.DeckHw;
        double beltMm = design.Armour?.BeltMm ?? 0;
        string deck = design.Deck ?? (L >= 150 ? "wood" : "steel");

        var mg = new MainGroups(design, lay);
        var (bats, F, A, M, WA, WE, gRef) = (mg.All, mg.F, mg.A, mg.M, mg.WA, mg.WE, mg.WingRef);
        int nStepF = mg.StepF, nStepA = mg.StepA, nf = mg.Nf, na = mg.Na, nm = mg.Nm, nw = mg.Nw;
        bool flushF = mg.FlushF, flushA = mg.FlushA;
        var Wy = mg.Wy;

        PlanMachinery(lay, design, res, hull);
        var plant = lay.Geo.Plant!;
        var control = ControlPlan.Of(site, mg);
        double lb = control.Lb, la = control.La, hood = control.Hood;
        long nb = control.Nb, nTower = control.NTower, naLvl = control.NaLvl;
        var raisedIn = design.Hull?.Raised ?? [];
        var secs = Batteries.SecondaryBatteries(design).Select((b, k) => (Sec: b, Prefix: Batteries.BatteryPrefix(k))).ToList();
        Armament.WarnUnpaired(lay, secs.Select(s => s.Sec));
        var torp = TorpedoPlan.Of(design, deckHw);
        long ntp = torp.Mounts;
        var (ttId, tt, tSweep, tEdges) = (torp.TypeId, torp.T, torp.Sweep, torp.Edges);

        // ---------------- the middle's plan ----------------
        var row = new MiddleRow(site, plant, mg, control, torp);
        var (segs, magL, fplan) = (row.Segs, row.MagL, row.Fplan);
        long nfun = row.NFun;
        double fw = row.Fw, fl = row.Fl, funTop = row.FunTop, beamTan = row.BeamTan, w2 = row.W2;
        double deckRatio = site.DeckRatio;
        var (seq, segOf, seqGun, core, lead, trail, ubSeg) = (row.Seq, row.SegOf, row.SeqGun, row.Core, row.Lead, row.Trail, row.UbSeg);

        double y0 = nw != 0 ? deckHw - gRef!.Reach - 0.6 : 0.0;
        double MReq = Math.Max(row.MiddleNeeds(row.PlanWidths(y0), row.CrossEnds(y0)), SecondarySpan(secs.Select(s => s.Sec))) + midExtra;

        CheckMainBeam(site, mg);
        var ends = EndsPlan.Of(site, mg, MReq, shift);
        var (fore, aft, midFwd, midAft) = (ends.Fore, ends.Aft, ends.MidFwd, ends.MidAft);
        if (plan)
        {
            // what the weights need of the layout: the citadel as the full layout makes it (end turrets to the middle)
            lay.Geo.Citadel = (aft.Count > 0 ? Math.Min(aft[0] - A[0].R - 2.0, midAft) : midAft,
                fore.Count > 0 ? Math.Max(fore[0] + F[0].R + 2.0, midFwd) : midFwd);
            lay.Geo.MachineryX = (midFwd + midAft) / 2;
            return lay;
        }

        // ---------------- middle: bridge, deckhouse, funnels, aft control ----------------
        var middle = MiddlePlaced.Of(site, mg, control, row, ends);
        var (fxs, fSegF, mids, wings, machPlaced, plantPlaced) = (middle.Fxs, middle.FSegF, middle.Mids, middle.Wings, middle.Mach, middle.Plant);
        double machC = middle.MachC;
        double bx1 = midFwd;
        double bx0 = bx1 - lb;

        // ---------------- raised stretches of hull (hull.raised) ----------------
        PlaceRaised(site, mg, control, ends, middle, fl);

        // ---------------- main turrets ----------------
        var turretTypes = new OrderedDictionary<string, TurretType>(StringComparer.Ordinal);
        var mounts = new List<Mount>();
        foreach (var g in bats)
            turretTypes[g.Tid] = g.T;
        PlaceEndTurrets(site, mg, ends, mounts);
        // riders: what level 1 is built under
        var riders = PlaceMiddleTurrets(site, mg, middle, mounts);

        // ---------------- superstructure, kept out of the guns' sweeps ----------------
        lay.EndMounts = mounts.Where(m => m.Kind == "main" && !(m.Wing || m.Midships)).ToList();
        var ctl = PlaceControl(site, control, row, ends);
        var blocks = ctl.Blocks;
        (bx0, bx1) = (ctl.Bx0, ctl.Bx1);
        double ax0 = ctl.Ax0;
        var funnels = PlaceFunnels(site, row, middle);

        // ---------------- secondaries on deck ----------------
        double dhW = 0.62 * B;
        bool first = true;
        foreach (var (sec, pre) in secs)
        {
            long nsec = sec.MountsPerSide;
            if ((sec.Mount ?? "deck") != "deck" || nsec == 0)
                continue;
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
            var deckSwept = mounts.Where(m => m.Kind == "main" && (m.Wing || m.Midships) && m.Base < LevelH).Select(m => m.Id)
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
                    var m = Armament.AddMount(lay, mounts, "secondary", tsId, ts, mid, sx, side * yS, SecBase(sx),
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
            first = false;
        }

        // ---------------- casemates ----------------
        PlaceCasemates(lay, mounts, turretTypes, blocks, secs, hull, depth);
        var housings = blocks.Where(b => b.Role == "casemate").ToList();
        riders.AddRange(housings.Select(b => (b.X0, b.X1, Math.Abs(b.Y) - b.W / 2)));

        // ---------------- level 1 ----------------
        (double X0, double X1)? dh = (bx0 - 3.0, bx1 + 1.0);
        dhW = 0.62 * B;
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

        // ---------------- torpedo mounts ----------------
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

        // ---------------- deckhouse levels ----------------
        AddDeckhouseLevels(lay, blocks, DeckhouseLevels(design), midAft, midFwd, dhBlocks, dhW, funnels.Select(f => f.Id).ToList());
        funTop = RaiseFunnels(lay, funnels, blocks, funTop);

        // ---------------- fire control ----------------
        FireControl.Place(lay, design, blocks);

        // ---------------- AA ----------------
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

        // ---------------- masts and boats ----------------
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

        // ---------------- citadel & compartments ----------------
        var mainMounts = mounts.Where(m => m.Kind == "main").ToList();
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

        // ---------------- dressing ----------------
        double frontEdge = fore.Count > 0 ? fore[0] + F[0].R : midFwd;
        FinishLayout(lay, design, hs, mounts, turretTypes, blocks, funnels, masts, aaOut, funTop, deck);
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
        return lay;
    }
}
