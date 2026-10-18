using Fleetwright.Sim.Ai;
using Fleetwright.Sim.Ai.Captains;
using Fleetwright.Sim.Core;
using Fleetwright.Sim.Missions;

namespace Fleetwright.Sim.Tests;

/// <summary>Doctrine (docs/battle/command.md 5.1a): read down the chain (ship, squadron, force, side, the neutral
/// default), presets as values from Content/Sim/doctrine.json, a scenario's sides and units, and Engage: a ship whose
/// doctrine is to fight takes up Fight when it meets the enemy, one whose doctrine is to carry on doesn't.</summary>
public class DoctrineTests
{
    static readonly WorldTime Start = WorldTime.FromUtc(1916, 5, 31, 14);
    static readonly SimSettings Settings = SimSettings.Load(Path.Combine(Fleet.Repo, "Content", "Sim", "settings.json"));
    static readonly string[] Designs = [Path.Combine(Fleet.Shipgen, "designs")];

    /// <summary>Ships of one class, one a side unless given, spawned east of each other <paramref name="apartM"/> apart.</summary>
    static (World W, AiSystem Ai) Make(int ships, double apartM, Func<int, byte>? side = null)
    {
        var w = new World(Start, 7, Settings);
        var ai = new AiSystem(w);
        w.Apply(Command.SetWeather(25_000));
        int c = w.Ships.AddClass(Fleet.Get("light_cruiser").Class!);
        var p = Earth.Position(56.0, 4.0);
        for (int i = 0; i < ships; i++)
        {
            var (la, lo) = Earth.LatLon(p);
            w.Apply(ShipSystem.Spawn(c, la, lo, 0, 12, side?.Invoke(i) ?? (byte)i));
            var f = Earth.Forward(p, 90);
            Earth.Advance(ref p, ref f, apartM);
        }
        return (w, ai);
    }

    [Fact]
    public void A_ship_takes_its_own_value_else_its_squadrons_its_forces_its_sides_and_the_default()
    {
        var (w, ai) = Make(4, 600, _ => 0);
        w.Apply(GroupSystem.Create(1));
        w.Apply(GroupSystem.Add(w.Groups.Of(1)!.GroupId, 2));
        w.Apply(GroupSystem.Create(3));
        int sq1 = w.Groups.Of(1)!.GroupId, sq2 = w.Groups.Of(3)!.GroupId;
        w.Apply(GroupSystem.Form(sq1));
        w.Apply(GroupSystem.Enter(w.Groups.Get(sq1)!.Parent, sq2));
        int force = w.Groups.Get(sq1)!.Parent;
        var d = ai.Doctrine;
        double Range(int ship) => d.Of(ship, 0, DoctrineField.RangeShare);

        Assert.Equal(Doctrine.Default(DoctrineField.RangeShare), Range(1));
        w.Apply(ai.Orders.SideDoctrine(0, DoctrineField.RangeShare, 0.5));
        Assert.Equal(0.5, Range(4));   // alone: its side's
        w.Apply(ai.Orders.Doctrine(Unit.Group(force), DoctrineField.RangeShare, 0.55));
        Assert.Equal((0.55, 0.55, 0.5), (Range(1), Range(3), Range(4)));
        w.Apply(ai.Orders.Doctrine(Unit.Group(sq1), DoctrineField.RangeShare, 0.7));
        Assert.Equal((0.7, 0.7, 0.55), (Range(1), Range(2), Range(3)));
        w.Apply(ai.Orders.Doctrine(Unit.Ship(2), DoctrineField.RangeShare, 0.8));
        Assert.Equal((0.7, 0.8), (Range(1), Range(2)));
        w.Apply(ai.Orders.Doctrine(Unit.Group(sq1), DoctrineField.RangeShare, double.NaN));   // back to the force's
        Assert.Equal((0.55, 0.8), (Range(1), Range(2)));
        Assert.Equal(Doctrine.Default(DoctrineField.RangeShare), d.Of(4, 1, DoctrineField.RangeShare));   // another side's: the default
    }

    [Fact]
    public void The_shipped_presets_read_and_a_bad_one_says_why()
    {
        var p = DoctrinePresets.Load(Path.Combine(Fleet.Repo, "Content", "Sim", "doctrine.json"));
        var d = p[DoctrinePresets.DefaultName].ToDictionary(x => x.Field, x => x.Value);
        Assert.Equal((double)Engage.Fight, d[DoctrineField.Engage]);
        Assert.Equal((double)TorpedoUse.AtWill, d[DoctrineField.Torpedoes]);
        Assert.Contains(DoctrineField.FallOutSpeedShare, d.Keys);
        Assert.Contains("no field", Assert.Throws<ArgumentException>(() => DoctrinePresets.Parse("""{ "default": { "Bravery": 1 } }""")).Message);
        Assert.Contains("isn't a value", Assert.Throws<ArgumentException>(() => DoctrinePresets.Parse("""{ "default": { "Engage": "maybe" } }""")).Message);
        Assert.Contains("no \"default\"", Assert.Throws<ArgumentException>(() => DoctrinePresets.Parse("""{ "british": {} }""")).Message);
    }

    const string Small = """
        { "scenario": 1, "start": "1916-05-31T14:00", "seed": 7, "sides": ["Britain", "Germany"] XTRA,
          "units": [
            { "name": "Light", "side": 0, "at": [56.5, 3.0], "course": 90, "kn": 15, "doctrine": { "Torpedoes": "on order" },
              "ships": [ { "design": "light_cruiser", "count": 2 } ] },
            { "name": "Lone", "side": 1, "at": [56.6, 4.5], "course": 270, "kn": 12, "doctrine": { "RangeShare": 0.4 },
              "ships": [ { "design": "light_cruiser" } ] } ] }
        """;

    static (World W, AiSystem Ai) Load(string extra)
    {
        var s = Scenario.Parse(Small.Replace("XTRA", extra));
        var w = new World(s.Start, s.Seed, Settings);
        var ai = new AiSystem(w);
        ScenarioLoader.Apply(s, w, ai, ScenarioLoader.RequestClasses(s, w.Ships.Registry, Designs));
        return (w, ai);
    }

    [Fact]
    public void A_scenario_sets_each_sides_readiness_for_fire()
    {
        var (w, _) = Load(""", "readiness": { "Germany": "closed up" }""");
        Assert.Equal(w.Settings.FireReadinessDefault, w.Damage.Readiness(0).Name);
        Assert.Equal("closed up", w.Damage.Readiness(1).Name);
        Assert.Contains("no \"open\"", Assert.Throws<ScenarioException>(() => Load(""", "readiness": { "Germany": "open" }""")).Message);
    }

    [Fact]
    public void A_scenario_gives_each_side_its_preset_and_values_and_each_unit_its_own()
    {
        var (w, ai) = Load(""", "doctrine": { "Germany": { "preset": "default", "Engage": "carry on" } }, "home": { "Germany": [54.0, 8.0] }""");
        var d = ai.Doctrine;
        Assert.Equal((double)Engage.Fight, d.Of(1, 0, DoctrineField.Engage));   // Britain: the default preset
        Assert.Equal((double)Engage.CarryOn, d.Of(3, 1, DoctrineField.Engage));   // Germany: carry on over it
        Assert.Equal((double)TorpedoUse.OnOrder, d.Of(2, 0, DoctrineField.Torpedoes));   // the Light squadron's own
        Assert.Equal(0.4, d.Of(3, 1, DoctrineField.RangeShare));   // the lone ship's own
        Assert.Null(ai.HomeOf(0));
        var (lat, lon) = Earth.LatLon(ai.HomeOf(1)!.Value);
        Assert.Equal((54.0, 8.0), (Math.Round(lat, 6), Math.Round(lon, 6)));

        Assert.Contains("no preset \"bold\"", Assert.Throws<ScenarioException>(() => Load(""", "doctrine": { "Germany": "bold" }""")).Message);
        Assert.Contains("fight: gone", Assert.Throws<ScenarioException>(() => Scenario.Parse(Small.Replace("XTRA", """, "fight": ["Germany"]"""))).Message);
        Assert.Contains("no field", Assert.Throws<ScenarioException>(() => Scenario.Parse(Small.Replace("\"RangeShare\": 0.4", "\"Range\": 0.4").Replace("XTRA", ""))).Message);
    }

    /// <summary>Two lone light cruisers 12 km apart: the one whose doctrine is to fight takes up Fight on meeting, the
    /// one whose doctrine is to carry on keeps its course (both fire at will).</summary>
    [Fact]
    public void Engage_fight_takes_up_Fight_on_meeting_and_carry_on_does_not()
    {
        var (w, ai) = Make(2, 12_000);
        w.Apply(ai.Orders.SideDoctrine(0, DoctrineField.Engage, (double)Engage.Fight));
        w.AdvanceTo(w.Time + 3 * 60_000);
        Assert.Equal(CaptainOrder.Fight, ai.Captains.Of(1).Order);
        Assert.NotEqual(CaptainOrder.Fight, ai.Captains.Of(2).Order);
        Assert.Equal(0.0, w.Ships.Store[w.Ships.Store.SlotOf(2)].CourseOrderDeg, 6);   // its course as spawned
    }

    [Fact]
    public void Aggression_scales_the_fields_it_lists_as_they_are_read()
    {
        var (w, ai) = Make(2, 600, _ => 0);
        w.Apply(GroupSystem.Create(1));
        var d = ai.Doctrine;
        w.Apply(ai.Orders.SideDoctrine(0, DoctrineField.OutgunnedRatio, 2));
        w.Apply(ai.Orders.SideDoctrine(0, DoctrineField.RangeShare, 0.6));
        w.Apply(ai.Orders.SideDoctrine(0, DoctrineField.Aggression, 1));   // the side bold
        Assert.Equal(3, d.Of(2, 0, DoctrineField.OutgunnedRatio), 9);   // 2 x (1 + 0.5)
        Assert.Equal(0.42, d.Of(2, 0, DoctrineField.RangeShare), 9);    // nearer: 0.6 x (1 - 0.3)
        Assert.Equal(30, d.Of(2, 0, DoctrineField.EvadeDeg), 9);        // not listed: as written
        Assert.Equal(2, d.Of(2, 0, DoctrineField.BreakOffShare) / 1.5, 9);   // neutral "never" stays past any loss

        w.Apply(ai.Orders.Doctrine(Unit.Group(w.Groups.Of(1)!.GroupId), DoctrineField.Aggression, -1));   // a timid squadron
        Assert.Equal(1, d.Of(1, 0, DoctrineField.OutgunnedRatio), 9);   // its ship: 2 x (1 - 0.5)
        Assert.Equal(3, d.Of(2, 0, DoctrineField.OutgunnedRatio), 9);   // a ship of no squadron: the side's
        Assert.Equal(1, d.OfGroup(null, 0, DoctrineField.Aggression), 9);
        Assert.Equal(1, d.SideValue(0, DoctrineField.Aggression), 9);

        Assert.Throws<ArgumentException>(() => new Doctrine(w.Groups, new Dictionary<string, double> { ["Bravery"] = 1 }));
    }

    [Fact]
    public void Doctrine_replays_and_is_in_the_hash()
    {
        var (w, ai) = Make(2, 12_000);
        ulong none = w.ComputeHash();
        w.Apply(ai.Orders.SideDoctrine(0, DoctrineField.Engage, (double)Engage.Fight));
        w.Apply(ai.Orders.Doctrine(Unit.Ship(2), DoctrineField.RangeShare, 0.45));
        Assert.NotEqual(none, w.ComputeHash());
        w.AdvanceTo(w.Time + 5 * 60_000);

        var r = new World(Start, 7, Settings, w.Ships.Registry);
        _ = new AiSystem(r);
        foreach (var c in w.CommandLog)
        {
            r.AdvanceTo(c.At);
            r.Apply(c.Command);
        }
        r.AdvanceTo(w.Time);
        Assert.Equal(w.ComputeHash(), r.ComputeHash());
    }
}
