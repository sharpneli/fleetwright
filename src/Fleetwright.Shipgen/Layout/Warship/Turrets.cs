namespace Fleetwright.Shipgen;

public sealed partial class Layout
{
    /// <summary>The end groups' turrets (BuildLayout): A, B, C forward and Y, X, W aft, outermost first, each tier
    /// superfiring over the one outboard of it, the rest flush on the deck and bearing on the beam; each lifted over
    /// a raised stretch under it, and its sweep reserved.</summary>
    static void PlaceEndTurrets(WarshipSite site, MainGroups mg, EndsPlan ends, List<Mount> mounts)
    {
        var (lay, depth) = (site.Lay, site.Depth);
        var (F, A) = (mg.F, mg.A);
        int nf = mg.Nf, na = mg.Na, nStepF = mg.StepF, nStepA = mg.StepA;
        var (fore, aft) = (ends.Fore, ends.Aft);
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
                    double rest_ = flush ? (gname == "A" ? 180 : 0) : rest;
                    double bse = (flush ? dz : deck0) + 1.2 + TierSteps(gs, level);
                    if (!flush && prev is double pv && pv > deck0 + 1.2 + TierSteps(gs, level - 1) + 1e-9)
                        bse = Math.Max(bse, pv + Geometry.SuperfireStep(gs[i - 1].Th));
                    bse = Math.Max(bse, dz + 1.2);
                    var probe = new Mount
                    {
                        Id = "", Kind = ComponentKind.Main, Type = g.Tid, T = g.T, X = x, Rest = rest_, Base = bse, Top = bse + g.Th,
                        ArcRole = flush ? "beam" : null,
                    };
                    bse += RaisedLift(lay, probe);
                    prev = bse;
                    string mid = TurretName(names[gname], i);
                    var m = Armament.AddMount(lay, mounts, ComponentKind.Main, g.Tid, g.T, mid, x, 0.0, bse, rest_, level: level,
                        armourMm: g.ArmourMm, depth: depth, footprintR: g.Reach, label: "Turret", material: g.Material,
                        deck: lay.Raised.Count > 0 ? bse - 1.2 - TierSteps(gs, level) : dz);
                    m.Material = g.Material;
                    if (flush)
                        m.ArcRole = "beam";
                    lay.ReserveSweep(m);
                }
            }
        }

    }

    /// <summary>The midships turrets (Q, P, R, S) and the wing pairs (W1, W2, ...; S and P) where the middle placed
    /// them, their sweeps reserved. Returns the riders: what level 1 must be built under (a turret on the deckhouse
    /// over the deck).</summary>
    static List<(double X0, double X1, double Hw)> PlaceMiddleTurrets(WarshipSite site, MainGroups mg, MiddlePlaced middle, List<Mount> mounts)
    {
        var (lay, depth) = (site.Lay, site.Depth);
        int nm = mg.Nm, nw = mg.Nw;
        var (mids, wings, machC) = (middle.Mids, middle.Wings, middle.MachC);
        var riders = new List<(double X0, double X1, double Hw)>();
        if (nm != 0 || nw != 0)
        {
            // flags: what the mount is (midships, wing, ...), set on the probe the raised decks are tested with and on the mount
            void MainMount(Gun g, string mid, double x, double y, double rest, Action<Mount> flags)
            {
                double reach = g.Reach;
                double dz = lay.DeckZ(x, reach);
                double bse = Math.Max(dz, g.Raised ? LevelH : 0.0) + 1.2;
                var probe = new Mount { Id = "", Kind = ComponentKind.Main, Type = g.Tid, T = g.T, X = x, Y = y, Rest = rest, Base = bse, Top = bse + g.Th };
                flags(probe);
                bse += RaisedLift(lay, probe);
                var m = Armament.AddMount(lay, mounts, ComponentKind.Main, g.Tid, g.T, mid, x, y, bse, rest,
                    armourMm: g.ArmourMm, depth: depth, footprintR: reach, label: "Turret", material: g.Material,
                    deck: lay.Raised.Count > 0 ? Math.Max(0.0, bse - 1.2 - (g.Raised ? LevelH : 0.0)) : dz);
                m.Material = g.Material;
                flags(m);
                lay.ReserveSweep(m);
                if (g.Raised && dz < LevelH)
                    riders.Add((x - reach - DhInset, x + reach + DhInset, Math.Abs(y) + reach + DhInset));
            }

            for (int k = 0; k < mids.Count; k++)
            {
                var (g, x, stow) = mids[k];
                MainMount(g, TurretName("QPRS", k), x, 0.0, stow, m => (m.ArcRole, m.Midships) = ("beam", true));
            }
            for (int k = 0; k < wings.Count; k++)
            {
                var (g, yG, pair) = wings[k];
                foreach (var (x, side) in pair)
                {
                    bool fwd = g.Echelon ? x == pair[0].X : x >= machC;
                    string battery = $"W{k + 1}", end = x >= machC ? "fore" : "aft";
                    MainMount(g, $"W{k + 1}{(side > 0 ? "S" : "P")}", x, side * yG, fwd ? 0 : 180,
                        m => (m.Wing, m.Battery, m.MagazineEnd, m.Echelon, m.CrossDeck) = (true, battery, end, g.Echelon, g.Cross));
                }
            }
        }

        return riders;
    }
}
