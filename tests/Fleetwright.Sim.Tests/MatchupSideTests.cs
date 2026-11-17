using Fleetwright.Sim.Ai.Knowledge;
using Fleetwright.Sim.Combat;
using Fleetwright.Sim.Core;
using Xunit.Abstractions;

namespace Fleetwright.Sim.Tests;

/// <summary>The matchup's sides from the world (docs/battle/command.md 5.1g, piece 2): own ships exactly, enemies as the
/// picture takes them (a class mistaken misleads; a ship made out to her type is what her side has near her size), and what's seen of their
/// condition.</summary>
public class MatchupSideTests(ITestOutputHelper output)
{
    static readonly SimSettings Settings = SimSettings.Load(Path.Combine(Fleet.Repo, "Content", "Sim", "settings.json"));
    static readonly WorldTime Start = WorldTime.FromUtc(1916, 5, 31, 14);

    /// <summary>A world with own ships (side 0) and enemy ships (side 1) of the named classes, and their class ids.</summary>
    static (World W, Dictionary<string, int> Ids) Make(string[] own, string[] enemy)
    {
        var w = new World(Start, 1, Settings);
        var ids = MatchupTests.Classes.Value.ToDictionary(kv => kv.Key, kv => w.Ships.AddClass(kv.Value));
        double lon = 4.0;
        foreach (string n in own)
            w.Apply(ShipSystem.Spawn(ids[n], 56.0, lon += 0.01, 0, 15, side: 0));
        foreach (string n in enemy)
            w.Apply(ShipSystem.Spawn(ids[n], 56.3, lon += 0.01, 180, 15, side: 1));
        return (w, ids);
    }

    static int Slot(World w, int entity) => w.Ships.Store.SlotOf(entity);

    double Judge(MatchupSide a, MatchupSide b)
    {
        var r = Matchup.Judge(a.Entries, b.Entries, 0.6, 0.6, Settings);
        output.WriteLine($"{string.Join(",", a.Entries.ToArray().Select(x => $"{x.Profile.Class.Name}x{x.Count:0.##}"))} against " +
                         $"{string.Join(",", b.Entries.ToArray().Select(x => $"{x.Profile.Class.Name}x{x.Count:0.##}"))}: {r.Confidence:0.###}");
        return r.Confidence;
    }

    [Fact]
    public void Own_ships_count_exactly_and_empty_magazines_count_for_nothing()
    {
        var (w, _) = Make(["iron_duke"], ["iron_duke"]);
        var m = new Matchups();
        MatchupSide a = new(), b = new();
        a.AddOwn(w, m, Slot(w, 1));
        b.AddOwn(w, m, Slot(w, 2));
        Assert.Equal(1, a.Entries[0].MainRounds);
        Assert.InRange(Judge(a, b), 0.999, 1.001);
        // her main mounts' rounds fired
        var cls = w.Ships.Store[Slot(w, 1)].Handling.Class;
        for (int i = 0; i < cls.Mounts.Length; i++)
            if (cls.Mounts[i].Battery >= 0 && cls.Batteries[cls.Mounts[i].Battery].Kind == Ships.Battery.Main)
                w.Ships.Store.Combat.Mount(Slot(w, 1), i).Rounds = 0;
        a.Clear();
        a.AddOwn(w, m, Slot(w, 1));
        Assert.Equal(0, a.Entries[0].MainRounds);
        Assert.True(Judge(a, b) < 0.2);
    }

    [Fact]
    public void A_mistaken_class_misleads()
    {
        var (w, ids) = Make(["arethusa"], ["v25"]);
        var m = new Matchups();
        MatchupSide a = new(), b = new();
        a.AddOwn(w, m, Slot(w, 1));
        b.AddContact(w, m, 0, new Contact { Entity = 2, Level = ContactLevel.Class, TakenFor = ids["v25"], Side = 1, Type = ShipType.Destroyer });
        double right = Judge(a, b);
        b.Clear();
        b.AddContact(w, m, 0, new Contact { Entity = 2, Level = ContactLevel.Class, TakenFor = ids["iron_duke"], Side = 1, Type = ShipType.Capital });
        double wrong = Judge(a, b);
        Assert.True(right > 2, $"taken rightly {right:0.##}");
        Assert.True(wrong < 0.5, $"taken for a battleship {wrong:0.##}");
    }

    [Fact]
    public void A_ship_made_out_to_her_type_is_what_her_side_has_near_her_size()
    {
        // docs/battle/allocation.md AL7b: her side's classes weighed by how near their size is to her estimated size
        var (w, ids) = Make(["arethusa"], ["iron_duke", "iron_duke", "v25", "v25", "derfflinger"]);
        var m = new Matchups();
        MatchupSide b = new();
        double Size(string n) => ShipTypes.SideAreaM2(w.Ships.Registry.Get(ids[n])!);
        double Share(string prefix) => b.Entries.ToArray().Where(e => e.Profile.Class.Name.StartsWith(prefix)).Sum(e => e.Count);
        // judged an Iron Duke's size, a third either way: two Iron Dukes and a Derfflinger, the boats hardly
        b.AddContact(w, m, 0, new Contact { Entity = 2, Level = ContactLevel.Type, TakenFor = -1, Side = 1, SizeM2 = Size("iron_duke"), SizeSigma = 0.35, Armed = true });
        output.WriteLine(string.Join(", ", b.Entries.ToArray().Select(e => $"{e.Profile.Class.Name} ({e.Profile.SideAreaM2:F0} m²) {e.Count:0.###}")));
        Assert.Equal(1, b.Entries.ToArray().Sum(e => e.Count), 6);
        Assert.True(Share("Iron Duke") + Share("Derfflinger") > 0.98);
        Assert.True(Share("Iron Duke") > Share("Derfflinger"));
        double loose = Share("Iron Duke");
        // made out better: nearer her own
        b.Clear();
        b.AddContact(w, m, 0, new Contact { Entity = 2, Level = ContactLevel.Type, TakenFor = -1, Side = 1, SizeM2 = Size("iron_duke"), SizeSigma = 0.05, Armed = true });
        Assert.True(Share("Iron Duke") > loose);
        // a boat's size: the V25s; two contacts of it are two ships, whole: one entry
        b.Clear();
        b.AddContact(w, m, 0, new Contact { Entity = 4, Level = ContactLevel.Type, TakenFor = -1, Side = 1, SizeM2 = Size("v25"), SizeSigma = 0.35, Armed = true });
        Assert.True(Share("V25") > 0.97);
        b.AddContact(w, m, 0, new Contact { Entity = 5, Level = ContactLevel.Type, TakenFor = -1, Side = 1, SizeM2 = Size("v25"), SizeSigma = 0.35, Armed = true });
        Assert.InRange(Share("V25"), 1.94, 2);
    }

    [Fact]
    public void A_silent_stopped_cripple_is_one_to_finish()
    {
        var (w, ids) = Make(["arethusa"], ["arethusa"]);
        var m = new Matchups();
        MatchupSide a = new(), b = new();
        a.AddOwn(w, m, Slot(w, 1));
        var c = new Contact { Entity = 2, Level = ContactLevel.Class, TakenFor = ids["arethusa"], Side = 1, Type = ShipType.Cruiser };
        b.AddContact(w, m, 0, c);
        double whole = Judge(a, b);
        b.Clear();
        b.AddContact(w, m, 0, c with { Seen = SeenFlags.Silent | SeenFlags.Stopped | SeenFlags.Low });
        double cripple = Judge(a, b);
        Assert.InRange(whole, 0.9, 1.1);
        Assert.True(cripple > 10, $"the cripple {cripple:0.#}");
        // and one seen going down isn't there at all
        b.Clear();
        b.AddContact(w, m, 0, c with { Seen = SeenFlags.GoingDown });
        Assert.Equal(0, b.Count);
    }
}
