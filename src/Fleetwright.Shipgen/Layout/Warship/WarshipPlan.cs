namespace Fleetwright.Shipgen;

// The warship layout's first stages (BuildLayout): what the rest of the layout is planned from.
public sealed partial class Layout
{
    /// <summary>What every stage of the warship layout builds on: the design, the hull and the main deck's width. Lay
    /// is what the layout fills in (footprints, weights, warnings, failures), written by every stage in turn.</summary>
    sealed class WarshipSite
    {
        public readonly Design Design;
        public readonly Navarch.Result Res;
        public readonly Layout Lay;
        public readonly HullSpec Hs;
        public readonly Hull Hull;
        public readonly double L, B, Depth;
        /// <summary>The main deck's half-width amidships: the beam's, unless the sides lean in above the widest
        /// point.</summary>
        public readonly double DeckHw;

        public WarshipSite(Design design, Navarch.Result res)
        {
            (Design, Res) = (design, res);
            Depth = res.Depth;
            Lay = new Layout(design);
            Hs = HullSpecOf(design);
            Hull = new Hull(Hs);
            Lay.Hull = Hull;
            Lay.Topside = Topside.Of(design, res.Freeboard);
            (L, B) = (Hull.L, Hull.B);
            DeckHw = Lay.Topside.Plain ? B / 2 : B / 2 * Lay.Topside.Ratio(0.0, 0.0);
        }
    }

    /// <summary>The main battery's turrets in their groups: the end groups (outermost first, and how many of each step
    /// up), the midships turrets (on the centreline in the middle) and the wing turrets (abeam in the middle: pairs
    /// abreast, or echelon pairs staggered fore and aft).</summary>
    sealed class MainGroups
    {
        /// <summary>Every battery with turrets, in the design's order.</summary>
        public readonly List<Gun> All;
        /// <summary>The fore and aft end groups, outermost first; StepF and StepA of each superfire.</summary>
        public readonly List<Gun> F, A;
        public readonly int StepF, StepA;
        /// <summary>The midships turrets (M), and the wing turrets abreast (WA) and in echelon (WE), an entry a
        /// pair.</summary>
        public readonly List<Gun> M, WA, WE;
        /// <summary>An end group with more turrets than superfire: the rest stand flush, on the deck.</summary>
        public readonly bool FlushF, FlushA;
        /// <summary>The first wing turret: the wings' offset from the centreline is reckoned by its reach.</summary>
        public readonly Gun? WingRef;

        public int Nf => F.Count;
        public int Na => A.Count;
        public int Nm => M.Count;
        public int Nw => WA.Count + WE.Count;

        /// <summary>The groups, with a warning for each cross-deck battery that isn't in echelon.</summary>
        public MainGroups(Design design, Layout lay)
        {
            All = Batteries.MainBatteries(design).Select((b, k) => (b, k)).Where(t => t.b.Turrets != 0)
                .Select(t => new Gun(t.b, t.k)).ToList();
            foreach (var g in All)
                if (g.Spec.Wing > 0 && g.Spec.CrossDeck == true && !g.Echelon)
                    lay.Warnings.Add($"main[{g.K}].cross_deck needs \"echelon\": true (an abreast pair blocks each other's " +
                                     "beam); the wing turrets fire on their own side only.");
            (F, StepF) = EndGroup(All, "fore");
            (A, StepA) = EndGroup(All, "aft");
            M = All.SelectMany(g => Enumerable.Repeat(g, g.Spec.Mid ?? 0)).ToList();
            WA = All.Where(g => !g.Echelon).SelectMany(g => Enumerable.Repeat(g, g.Spec.Wing ?? 0)).ToList();
            WE = All.Where(g => g.Echelon).SelectMany(g => Enumerable.Repeat(g, g.Spec.Wing ?? 0)).ToList();
            FlushF = Nf > Math.Max(StepF, 1);
            FlushA = Na > Math.Max(StepA, 1);
            WingRef = Nw > 0 ? WA.Concat(WE).First() : null;
        }

        /// <summary>A wing turret's offset from the centreline when the reference's is y: a smaller turret stands
        /// further out, its outer edge in line.</summary>
        public double Wy(Gun g, double y) => g.Reach == WingRef!.Reach ? y : y + WingRef.Reach - g.Reach;
    }

    /// <summary>The bridge tower and aft control: their lengths (La 0: no aft control), how many levels each stands
    /// (the bridge high enough to see over the fore group's superfiring turret), and the main directors' hood on
    /// top.</summary>
    sealed record ControlPlan(double Lb, double La, long Nb, long NTower, long NaLvl, double Hood)
    {
        /// <summary>The plan, recorded in Geo.Bridge, with a warning when the tower is too low for the bridge to see
        /// over the fore group.</summary>
        public static ControlPlan Of(WarshipSite site, MainGroups g)
        {
            var (design, lay, L) = (site.Design, site.Lay, site.L);
            var (F, A) = (g.F, g.A);
            int nf = g.Nf, na = g.Na;
            double lb = Clamp(0.05 * L + 2, 7, 18);
            double la = AftControl(design) ? 0.045 * L + 2 : 0.0;
            var raisedIn = design.Hull?.Raised ?? [];
            double fwdDeck = LevelH * raisedIn.Where(q => RaisedCovers(q, "fore_group")).Select(q => (double)(q.Decks ?? 0)).DefaultIfEmpty(0.0).Max();
            double aftDeck = LevelH * raisedIn.Where(q => RaisedCovers(q, "aft_group")).Select(q => (double)(q.Decks ?? 0)).DefaultIfEmpty(0.0).Max();
            int? fwdTier = nf != 0 ? Math.Min(nf, Math.Max(g.StepF, 1)) - 1 : null;
            double? fwdRoof = fwdTier is int ft ? fwdDeck + 1.2 + TierSteps(F, ft) + F[ft].Th : null;
            long nbNeed = BridgeLevel(fwdRoof);
            int? aftTier = na != 0 ? Math.Min(na, Math.Max(g.StepA, 1)) - 1 : null;
            long naLvl = BridgeLevel(aftTier is int atr ? aftDeck + 1.2 + TierSteps(A, atr) + A[atr].Th : null);
            long over = LevelsOverBridge(design);
            long nTower = TowerLevels(design, nbNeed + over);
            long nb = Math.Min(nTower, Math.Max(nbNeed, nTower - over));
            if (nb < nbNeed)
                lay.Warnings.Add(
                    $"The bridge (level {nb}, its deck {LevelH * (nb - 1):F1} m above the main deck) cannot see over " +
                    $"turret {TurretName("ABC", fwdTier!.Value)}'s roof ({fwdRoof!.Value:F1} m): superstructure.tower_levels " +
                    $"{nbNeed} or more lifts it clear.");
            lay.Geo.Bridge = new BridgePlan(nb, LevelH * (nb - 1), nbNeed, nTower, fwdRoof);
            double hood = FireControl.Spec(design, "main").Directors != 0 ? FireControl.HoodH : 0.0;
            return new ControlPlan(lb, la, nb, nTower, naLvl, hood);
        }
    }

    /// <summary>The deck torpedo mounts asked for: how many, their type, the circle their tubes sweep, and whether
    /// they go in pairs at the deck edges (the deck wide enough) or on the centreline.</summary>
    sealed record TorpedoPlan(long Mounts, string? TypeId, TurretType? T, double Sweep, bool Edges)
    {
        public static TorpedoPlan Of(Design design, double deckHw)
        {
            long ntp = design.Torpedoes?.Mounts ?? 0;
            string? ttId = null;
            TurretType? tt = null;
            if (ntp != 0)
                (ttId, tt) = Geometry.MakeTorpedoType(design.Torpedoes!.Tubes ?? 4);
            double tSweep = ntp != 0 ? tt!.BarrelLen / 2 + 0.3 : 0.0;
            bool tEdges = ntp != 0 && deckHw - tt!.R - 0.8 >= tSweep;
            return new TorpedoPlan(ntp, ttId, tt, tSweep, tEdges);
        }
    }
}
