namespace Fleetwright.Shipgen;

public sealed partial class Layout
{
    /// <summary>The warship layout. The middle (machinery, funnels, midships and wing turrets, the secondaries beside
    /// them) gets the length its contents need plus midExtra (what an earlier layout found short there), the end turret
    /// groups close up on it, and any length to spare goes to the ends. plan: stop once the turret groups and the middle
    /// are placed (Deficit, the citadel and the shift range set), for sizing the hull.
    /// The stages run in this order (Layout/Warship/): each takes what it reads as parameters and returns what later
    /// stages read; all of them write the layout (footprints, weights, warnings, failures), so their order matters.</summary>
    public static Layout BuildLayout(Design design, Navarch.Result res, double shift = 0.0, double midExtra = 0.0, bool plan = false)
    {
        var site = new WarshipSite(design, res);
        var lay = site.Lay;

        // ---------------- what the layout is planned from ----------------
        var mg = new MainGroups(design, lay);
        PlanMachinery(lay, design, res, site.Hull);
        var control = ControlPlan.Of(site, mg);
        var secs = Batteries.SecondaryBatteries(design).Select((b, k) => (Sec: b, Prefix: Batteries.BatteryPrefix(k))).ToList();
        Armament.WarnUnpaired(lay, secs.Select(s => s.Sec));
        var torp = TorpedoPlan.Of(design, site.DeckHw);

        // ---------------- the middle's plan, the end groups closed up on it ----------------
        var row = new MiddleRow(site, lay.Geo.Plant!, mg, control, torp);
        double y0 = mg.Nw != 0 ? site.DeckHw - mg.WingRef!.Reach - 0.6 : 0.0;
        double mReq = Math.Max(row.MiddleNeeds(row.PlanWidths(y0), row.CrossEnds(y0)), SecondarySpan(secs.Select(s => s.Sec))) + midExtra;
        CheckMainBeam(site, mg);
        var ends = EndsPlan.Of(site, mg, mReq, shift);
        if (plan)
        {
            // what the weights need of the layout: the citadel as the full layout makes it (end turrets to the middle)
            var (fore, aft, midFwd, midAft) = (ends.Fore, ends.Aft, ends.MidFwd, ends.MidAft);
            lay.Geo.Citadel = (aft.Count > 0 ? Math.Min(aft[0] - mg.A[0].R - 2.0, midAft) : midAft,
                fore.Count > 0 ? Math.Max(fore[0] + mg.F[0].R + 2.0, midFwd) : midFwd);
            lay.Geo.MachineryX = (midFwd + midAft) / 2;
            return lay;
        }

        // ---------------- the middle placed, the raised stretches over it ----------------
        var middle = MiddlePlaced.Of(site, mg, control, row, ends);
        PlaceRaised(site, mg, control, ends, middle, row.Fl);

        // ---------------- main turrets ----------------
        var turretTypes = new OrderedDictionary<string, TurretType>(StringComparer.Ordinal);
        var mounts = new List<Mount>();
        foreach (var g in mg.All)
            turretTypes[g.Tid] = g.T;
        PlaceEndTurrets(site, mg, ends, mounts);
        // riders: what level 1 is built under
        var riders = PlaceMiddleTurrets(site, mg, middle, mounts);

        // ---------------- superstructure, kept out of the guns' sweeps ----------------
        lay.EndMounts = mounts.Where(m => m.Kind == ComponentKind.Main && !(m.Wing || m.Midships)).ToList();
        var ctl = PlaceControl(site, control, row, ends);
        var blocks = ctl.Blocks;
        var funnels = PlaceFunnels(site, row, middle);

        // ---------------- secondaries on deck ----------------
        bool first = true;
        foreach (var (sec, pre) in secs)
            if (PlaceDeckBattery(site, mg, row, ends, sec, pre, first, turretTypes, mounts, riders, blocks, funnels))
                first = false;

        // ---------------- casemates ----------------
        PlaceCasemates(lay, mounts, turretTypes, blocks, secs, site.Hull, site.Depth);
        var housings = blocks.Where(b => b.Role == "casemate").ToList();
        riders.AddRange(housings.Select(b => (b.X0, b.X1, Math.Abs(b.Y) - b.W / 2)));

        // ---------------- level 1, and torpedo mounts ----------------
        var level1 = PlaceLevel1(site, control, ctl, riders, funnels, housings, mounts, blocks);
        PlaceTorpedoMounts(site, torp, ends, middle.MachC, level1, turretTypes, mounts);

        // ---------------- deckhouse levels ----------------
        AddDeckhouseLevels(lay, blocks, DeckhouseLevels(design), ends.MidAft, ends.MidFwd, level1.Deckhouse, level1.DeckhouseW,
            funnels.Select(f => f.Id).ToList());
        double funTop = RaiseFunnels(lay, funnels, blocks, row.FunTop);

        // ---------------- fire control ----------------
        FireControl.Place(lay, design, blocks);

        // ---------------- AA ----------------
        var aaOut = PlaceAaMounts(site, middle.MachC, blocks);

        // ---------------- masts and boats ----------------
        var masts = PlaceMasts(site, control, ctl, funTop);
        var boats = PlaceBoats(site, row, middle.MachC, riders, level1);

        // ---------------- citadel & compartments ----------------
        PlaceCitadel(site, row, middle, mounts);

        // ---------------- dressing ----------------
        string deck = design.Deck ?? (site.L >= 150 ? "wood" : "steel");
        FinishLayout(lay, design, site.Hs, mounts, turretTypes, blocks, funnels, masts, aaOut, funTop, deck);
        Dress(site, mg, ends, boats);
        return lay;
    }
}
