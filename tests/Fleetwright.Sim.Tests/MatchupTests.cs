using Fleetwright.Sim.Combat;
using Fleetwright.Sim.Core;
using Fleetwright.Sim.Ships;
using Fleetwright.Shipgen;
using Xunit.Abstractions;

namespace Fleetwright.Sim.Tests;

/// <summary>The matchup (docs/battle/command.md 5.1g): who can fight whom, on the 1914 classes the scenarios use. The
/// cases the user named (a light cruiser hunting a destroyer and shunning a battleship; destroyers and light cruisers
/// with torpedoes challenging one), the even ones, the saturation, the square law, ammunition, and no garbage.</summary>
public class MatchupTests(ITestOutputHelper output)
{
    static readonly SimSettings Settings = SimSettings.Load(Path.Combine(Fleet.Repo, "Content", "Sim", "settings.json"));
    const double Share = 0.6;

    static readonly Lazy<Dictionary<string, MatchupProfile>> profiles = new(() =>
    {
        var tables = new Dictionary<BallisticKey, BallisticTable>();
        BallisticTable Table(BallisticKey k) => tables.TryGetValue(k, out var t) ? t : tables[k] = new BallisticTable(k);
        var d = new Dictionary<string, MatchupProfile>();
        foreach (string name in new[] { "arethusa", "v25", "iron_duke", "derfflinger", "lion" })
        {
            // the scenarios' own designs, else a shipped one (as the CLI resolves a name)
            string path = Path.Combine(Fleet.Repo, "Content", "Scenarios", "Designs", name + ".json");
            var design = Design.Load(File.Exists(path) ? path : Path.Combine(Fleet.Shipgen, "designs", name + ".json"));
            d[name] = new MatchupProfile(ShipClassCompiler.Compile(ShipDesign.Build(design)), Table, Settings);
        }
        return d;
    });

    static MatchupEntry E(string name, int n = 1, bool torpedoes = true, double mainRounds = 1) =>
        new(profiles.Value[name], n, MainRounds: mainRounds, TorpedoesLeft: torpedoes ? 1 : 0);

    double Judge(MatchupEntry[] a, MatchupEntry[] b, double sightM = double.PositiveInfinity)
    {
        var r = Matchup.Judge(a, b, Share, Share, Settings, sightM);
        output.WriteLine($"{string.Join(",", a.Select(x => $"{x.Profile.Class.Name}x{x.Count}"))} against {string.Join(",", b.Select(x => $"{x.Profile.Class.Name}x{x.Count}"))}: " +
                         $"{r.Confidence:0.###} ({r.Terms.Style}, lines at {r.Terms.RangeM:0} m)");
        return r.Confidence;
    }

    [Fact]
    public void A_light_cruiser_hunts_a_destroyer_and_the_answer_is_the_same_both_ways()
    {
        double lc = Judge([E("arethusa")], [E("v25")]), dd = Judge([E("v25")], [E("arethusa")]);
        Assert.True(lc > 2, $"the light cruiser {lc:0.##}");
        Assert.True(dd < 0.5, $"the destroyer {dd:0.##}");
        Assert.InRange(lc * dd, 0.9, 1.1);
    }

    [Fact]
    public void A_light_cruiser_without_torpedoes_shuns_a_battleship()
    {
        Assert.True(Judge([E("arethusa", torpedoes: false)], [E("iron_duke")]) < 0.2);
        // a squadron of them with their torpedoes can take her on
        Assert.True(Judge([E("arethusa", 4)], [E("iron_duke")]) > 1);
        Assert.True(Judge([E("arethusa", 4, torpedoes: false)], [E("iron_duke")]) < 1);
    }

    [Fact]
    public void Destroyers_saturate_a_battleships_defence()
    {
        double last = 0;
        foreach (int n in new[] { 1, 2, 4, 8, 16 })
        {
            double c = Judge([E("v25", n)], [E("iron_duke")]);
            Assert.True(c > last, $"{n} destroyers {c:0.###}, fewer {last:0.###}");
            last = c;
        }
        Assert.True(Judge([E("v25")], [E("iron_duke")]) < 0.2);
        Assert.True(Judge([E("v25", 8)], [E("iron_duke")]) > 1);
        // by night the battleship's guns open only within sight: the lone boat does better
        Assert.True(Judge([E("v25")], [E("iron_duke")], sightM: 2000) > Judge([E("v25")], [E("iron_duke")]));
    }

    [Fact]
    public void Even_matches_are_even_and_numbers_count_as_their_square()
    {
        Assert.InRange(Judge([E("iron_duke")], [E("iron_duke")]), 0.999, 1.001);
        Assert.InRange(Judge([E("iron_duke"), E("v25", 4)], [E("iron_duke"), E("v25", 4)]), 0.999, 1.001);
        Assert.InRange(Judge([E("lion")], [E("derfflinger")]), 0.5, 2);
        Assert.InRange(Judge([E("iron_duke")], [E("derfflinger")]), 0.5, 2);
        // two against one: about four times as confident (Lanchester's square law), a little less as the losses saturate
        Assert.InRange(Judge([E("iron_duke", 2)], [E("iron_duke")]), 2.5, 4.5);
    }

    [Fact]
    public void A_battleship_with_empty_main_magazines_is_no_match_for_another()
    {
        Assert.True(Judge([E("iron_duke", mainRounds: 0)], [E("iron_duke")]) < 0.2);
    }

    [Fact]
    public void Hit_and_out_chances_follow_the_fine_models_benches()
    {
        var id = profiles.Value["iron_duke"];
        var derf = profiles.Value["derfflinger"];
        var main = id.Guns.First(g => g.Kind == Battery.Main);
        // shipgen duel: Iron Duke's 13.5" on Derfflinger, 13.0% at 10 km and 4.7% at 14 km
        Assert.InRange(Matchup.HitChance(main, derf, 10_000, Settings), 0.08, 0.2);
        Assert.InRange(Matchup.HitChance(main, derf, 14_000, Settings), 0.03, 0.08);
        // shipgen hits: a 6" HE hit puts the V25 out 7.2% of the time, a 13.5" AP hit Derfflinger 0.85% at 14 km
        var six = profiles.Value["arethusa"].Guns[0];
        Assert.InRange(Matchup.OutChance(six, E("v25"), 6000, Settings), 0.05, 0.1);
        Assert.InRange(Matchup.OutChance(main, E("derfflinger"), 14_000, Settings), 0.004, 0.017);
    }

    [Fact]
    public void A_judgement_allocates_nothing()
    {
        MatchupEntry[] a = [E("iron_duke", 4), E("arethusa", 4), E("v25", 8)], b = [E("derfflinger", 3), E("v25", 12)];
        Matchup.Judge(a, b, Share, Share, Settings);
        long before = GC.GetAllocatedBytesForCurrentThread();
        for (int i = 0; i < 100; i++)
            Matchup.Judge(a, b, Share, Share, Settings);
        Assert.Equal(0, GC.GetAllocatedBytesForCurrentThread() - before);
    }
}
