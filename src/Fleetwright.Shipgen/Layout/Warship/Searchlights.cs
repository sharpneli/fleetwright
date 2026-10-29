namespace Fleetwright.Shipgen;

public sealed partial class Layout
{
    /// <summary>A place a searchlight could stand: its middle (and its mirror image across the centreline, for a pair),
    /// its base above the main deck, and what carries it (a block's, funnel's or mast's id; null the deck).</summary>
    readonly record struct LightSite(double X, double Y, double Z, bool Pair, string? On);

    /// <summary>The searchlights (docs/battle/night.md Q3): the design's count and mirror for her size
    /// (SearchlightTemplates), sited where the era's ships carried them. The sites come in groups, each best first: the
    /// bridge's highest roof (its wings), platforms on the funnels' sides (the searchlight towers), after control's roof,
    /// the masts' tops (on the top's roof, ahead of the pole) and the other roofs; the deck on the centreline only where
    /// none of them has room. The groups take turns, so the lights spread fore and aft, pairs abreast while two or more are still wanted. Each light keeps
    /// clear of everything placed at its height, of the gun sweeps under it and of the masts. Its weight is booked as
    /// equipment, its men in the crew book; the drawing reads them from the hitboxes.</summary>
    static void PlaceSearchlights(WarshipSite site, List<Block> blocks, List<Funnel> funnels, List<Mast> masts)
    {
        var (lay, design) = (site.Lay, site.Design);
        var spec = SearchlightTemplates.Of(design);
        int want = SearchlightTemplates.CountFor(spec, site.L, site.B);
        if (want == 0)
            return;
        double d = SearchlightTemplates.MirrorM(spec, site.L, site.B);
        double r = SearchlightTemplates.HousingR(d), h = SearchlightTemplates.HousingH(d);
        double wt = SearchlightTemplates.WeightT(d);

        List<LightSite> Roofs(IEnumerable<Block> on)
        {
            var sites = new List<(double Z, int Pair, double Off, LightSite S)>();
            using var _ = Scratch<(double X, double Y, double Z0, bool Pair)>.Rent(out var spots);
            foreach (var b in on)
            {
                if (b.Kind == "director")
                    continue;
                spots.Clear();
                RoofSpots([b], 2 * r, 2 * r, spots);
                double mid = (b.X0 + b.X1) / 2;
                foreach (var (x, y, z0, pair) in spots)
                    sites.Add((-z0, pair ? 0 : 1, Math.Abs(x - mid), new LightSite(x, y, z0, pair, b.Id)));
            }
            return sites.OrderBy(s => s.Z).ThenBy(s => s.Pair).ThenBy(s => s.Off).Select(s => s.S).ToList();
        }

        var groups = new List<List<LightSite>>
        {
            Roofs(blocks.Where(b => b.Role is "bridge" or "island")),
        };
        var onFunnels = new List<LightSite>();
        foreach (double share in (ReadOnlySpan<double>)[0.55, 0.7])   // higher only where lower's taken
            foreach (var f in funnels)
            {
                double foot = f.Z0 ?? lay.DeckZ(f.X, f.L / 2);
                double z = foot + share * (lay.FunTop - foot), off = f.W / 2 + r + 0.05;
                onFunnels.Add(Math.Abs(f.Y) < 1e-6 ? new LightSite(f.X, off, z, true, f.Id)
                    : new LightSite(f.X, f.Y + Math.Sign(f.Y) * off, z, false, f.Id));
            }
        groups.Add(onFunnels);
        groups.Add(Roofs(blocks.Where(b => b.Role == "aft_control")));
        var onMasts = new List<LightSite>();
        foreach (var m in masts)
        {
            if (m.Platform is not { } p || m.Id is null)
                continue;
            double roofR = p.TierR(p.Tiers - 1);
            bool through = (m.Top ?? 0.0) > p.Roof + 0.01;   // the pole runs on up through the top: the light stands ahead of it
            if (through && m.R + 2 * r + 0.1 > roofR + 0.3 * r)
                continue;
            onMasts.Add(new LightSite(through ? m.X + m.R + r + 0.1 : m.X, m.Y ?? 0.0, p.Roof, false, m.Id));
        }
        groups.Add(onMasts);
        groups.Add(Roofs(blocks.Where(b => b.Role is not ("bridge" or "island" or "aft_control"))));
        var onDeck = new List<LightSite>();
        for (int k = 0; k < 40; k++)
        {
            double x = (k % 2 == 0 ? -1 : 1) * (k / 2) * 2.0;
            if (Math.Abs(x) < 0.35 * site.L)
                onDeck.Add(new LightSite(x, 0.0, lay.DeckZ(x, r), false, null));
        }

        bool Fits(double x, double y, double z, string? on)
        {
            var fp = Footprint.Circle(x, y, r);
            IReadOnlyCollection<string>? ignore = on is not null && funnels.Any(f => f.Id == on) ? [on] : null;
            if (!lay.FreeAt(fp, z, z + h, 0.3, ignore) || !lay.Clear(fp, z + h))
                return false;
            if (on is null && Math.Abs(y) + r > lay.DeckHalfWidth(x) - 0.3)
                return false;
            foreach (var m in masts)
            {
                if (m.Id == on || (m.Top ?? double.PositiveInfinity) <= z)
                    continue;
                double my = m.Y ?? 0.0;
                if (Math.Pow(x - m.X, 2) + Math.Pow(y - my, 2) < Math.Pow(m.R + r + 0.3, 2))
                    return false;
                if (m.Tripod && x > m.X - 5.5 - r && x < m.X + 0.5 + r && Math.Abs(y - my) < 4.0 + r)   // its legs run aft
                    return false;
            }
            return true;
        }

        void Put(double x, double y, double z, string? on)
        {
            string id = $"Searchlight {lay.Searchlights.Count + 1}";
            lay.Searchlights.Add(new Searchlight(id, x, y, z, d, r, h, on));
            lay.Occupy(Footprint.Circle(x, y, r), z, z + h, id);
            lay.Weights.Add(new Weight(id, "misc", wt, x, ZRel.Deck(z + h / 2)));
        }

        // a pair where two or more are wanted, else one on the centreline (or, failing any, a pair's starboard light)
        bool Take(LightSite s, bool halves)
        {
            int left = want - lay.Searchlights.Count;
            if (s.Pair && left >= 2)
            {
                if (!Fits(s.X, s.Y, s.Z, s.On) || !Fits(s.X, -s.Y, s.Z, s.On))
                    return false;
                Put(s.X, s.Y, s.Z, s.On);
                Put(s.X, -s.Y, s.Z, s.On);
                return true;
            }
            if (s.Pair && !halves || !Fits(s.X, s.Y, s.Z, s.On))
                return false;
            Put(s.X, s.Y, s.Z, s.On);
            return true;
        }

        foreach (bool halves in (ReadOnlySpan<bool>)[false, true])
        {
            var next = new int[groups.Count];
            bool any = true;
            while (any && lay.Searchlights.Count < want)
            {
                any = false;
                for (int g = 0; g < groups.Count && lay.Searchlights.Count < want; g++)
                {
                    var list = groups[g];
                    while (next[g] < list.Count && !Take(list[next[g]], halves))
                        next[g]++;
                    if (next[g] < list.Count)
                    {
                        next[g]++;
                        any = true;
                    }
                }
            }
        }
        foreach (var s in onDeck)   // the deck only where nothing else has room
            if (lay.Searchlights.Count < want)
                Take(s, true);
        if (lay.Searchlights.Count < want)
            lay.Warnings.Add($"Searchlights: room for {lay.Searchlights.Count} of {want}.");
    }
}
