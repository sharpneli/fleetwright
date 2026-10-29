using Fleetwright.Sim.Ai;
using Fleetwright.Sim.Combat;
using Fleetwright.Sim.Core;
using Xunit;
using Xunit.Abstractions;

namespace Fleetwright.Sim.Tests;

/// <summary>The player's hand at night (docs/battle/night.md Q9): a darkened unit shows nothing and holds its fire until
/// it's fired upon; beams kept on by hand against doctrine; star shell by hand on a point of the sea, left alone by the
/// captain's doctrine, and from a squadron by its nearest ship that carries it.</summary>
public class NightControlTests(ITestOutputHelper output)
{
    static readonly SimSettings S = SimSettings.Load(Path.Combine(Fleet.Repo, "Content", "Sim", "settings.json")) with { Smoke = false };
    const double Lat = 56, Lon = 4;

    /// <summary>A light cruiser (side 1, entity 1) steaming north at 10 kn and a destroyer (side 2, entity 2) stopped 1.9 km
    /// to the east of her, under a full moon high over the North Sea (seen there, not made out by the guns' glasses: on a
    /// moonless night she'd be seen only within a kilometre, docs/battle/night.md Q1b): the cruiser fires on what she can't make out, the
    /// destroyer holds hers and keeps dark; both sides' star shell off.</summary>
    static (World W, AiSystem Ai) Night()
    {
        var w = new World(WorldTime.FromUtc(1916, 9, 12, 23, 30), 7, S);
        var ai = new AiSystem(w);
        int cl = w.Ships.AddClass(Fleet.Get("light_cruiser").Class!);
        int dd = w.Ships.AddClass(Fleet.Get("destroyer").Class!);
        w.Apply(Command.SetWeather(20_000));
        w.Apply(ShipSystem.Spawn(cl, Lat, Lon, 0, 10, 1));
        w.Apply(ShipSystem.Spawn(dd, Lat, Lon + 0.03, 180, 0, 2));
        foreach (int side in new[] { 1, 2 })
            w.Apply(ai.Orders.SideDoctrine(side, DoctrineField.StarShell, (double)StarShellUse.Never));
        w.Apply(ai.Orders.SideDoctrine(1, DoctrineField.Unknowns, (double)Unknowns.Fire));
        w.Apply(ai.Orders.SideDoctrine(2, DoctrineField.Unknowns, (double)Unknowns.Hold));
        w.Apply(ai.Orders.SideDoctrine(2, DoctrineField.Searchlights, (double)SearchlightUse.Never));
        return (w, ai);
    }

    static bool Engaged(World w, int e)
    {
        var store = w.Ships.Store;
        int slot = store.SlotOf(e);
        var cls = store[slot].Handling.Class;
        for (int b = 0; b < cls.Batteries.Length; b++)
            if (store.Combat.Target(slot, b) != 0)
                return true;
        return false;
    }

    /// <summary>Darkened, the cruiser neither fires, lights nor challenges in 90 s, where undarkened she engages; once the
    /// destroyer opens on her, she fires back, her lights still out.</summary>
    [Fact]
    public void A_darkened_ship_shows_nothing_and_fires_only_once_fired_upon()
    {
        (bool Fired, SearchlightOrder Lights, bool Challenged) Watch(World w, int seconds)
        {
            var (fired, lights, challenged) = (false, SearchlightOrder.Off, false);
            for (int s = 0; s < seconds; s++)
            {
                w.AdvanceTo(w.Time + 1_000);
                fired |= Engaged(w, 1);
                var o = w.Searchlights.OrderOf(1, w.Time.Ms).Order;
                lights = o > lights ? o : lights;
                challenged |= w.Challenges.ChallengeOf(1).Target != 0;
            }
            return (fired, lights, challenged);
        }
        var (open, oai) = Night();
        var plain = Watch(open, 90);
        var (w, ai) = Night();
        w.Apply(ai.Orders.SideDoctrine(1, DoctrineField.Darkened, 1));
        var dark = Watch(w, 90);
        bool holding = (((NightFlags)Snap(w).Ships.Night[Snap(w).Ships.IndexOf(1)]) & NightFlags.Holding) != 0;
        w.Apply(ai.Orders.Engage(Unit.Ship(2), 1));   // the destroyer opens on her
        var back = Watch(w, 60);
        output.WriteLine($"undarkened: fired {plain.Fired}, lights {plain.Lights}; darkened: fired {dark.Fired}, lights {dark.Lights}, " +
                         $"challenged {dark.Challenged}, holding {holding}; fired upon: fired {back.Fired}, lights {back.Lights}");
        Assert.True(plain.Fired);
        Assert.False(dark.Fired);
        Assert.Equal(SearchlightOrder.Off, dark.Lights);
        Assert.True(holding);
        Assert.True(back.Fired);
        Assert.Equal(SearchlightOrder.Off, back.Lights);

        // challenging by doctrine: none while darkened
        var (c, cai) = Night();
        c.Apply(cai.Orders.SideDoctrine(1, DoctrineField.Unknowns, (double)Unknowns.Challenge));
        c.Apply(cai.Orders.SideDoctrine(1, DoctrineField.ChallengeM, 2_500));
        c.Apply(cai.Orders.SideDoctrine(1, DoctrineField.Darkened, 1));
        Assert.False(Watch(c, 60).Challenged);
    }

    static Snapshot Snap(World w)
    {
        var s = new Snapshot();
        w.Fill(s, truth: true);
        return s;
    }

    /// <summary>With doctrine's searchlights never, the player's beams light the destroyer she engages, and stay on her once
    /// her guns hold; back to doctrine, they go out. A ship whose guns never took her sweeps about her instead.</summary>
    [Fact]
    public void Beams_kept_on_by_hand_against_doctrine()
    {
        var (w, ai) = Night();
        w.Apply(ai.Orders.SideDoctrine(1, DoctrineField.Searchlights, (double)SearchlightUse.Never));
        w.Apply(ai.Orders.Beams(Unit.Ship(1), true));
        w.AdvanceTo(w.Time + 60_000);
        var (lit, on, exposed) = w.Searchlights.OrderOf(1, w.Time.Ms);
        var snap = Snap(w);
        int k = snap.Ships.IndexOf(1);
        w.Apply(ai.Orders.HoldFire(Unit.Ship(1)));
        w.AdvanceTo(w.Time + 20_000);
        var kept = w.Searchlights.OrderOf(1, w.Time.Ms);
        w.Apply(ai.Orders.Beams(Unit.Ship(1), false));
        w.AdvanceTo(w.Time + 5_000);
        var after = w.Searchlights.OrderOf(1, w.Time.Ms).Order;

        var (h, hai) = Night();
        h.Apply(hai.Orders.SideDoctrine(1, DoctrineField.Searchlights, (double)SearchlightUse.Never));
        h.Apply(hai.Orders.HoldFire(Unit.Ship(1)));
        h.Apply(hai.Orders.Beams(Unit.Ship(1), true));
        h.AdvanceTo(h.Time + 60_000);
        var sweeping = h.Searchlights.OrderOf(1, h.Time.Ms).Order;
        output.WriteLine($"beams by hand: {lit} on {on} (out {exposed}), the snapshot's {snap.Ships.BeamOn[k]}, flags {(NightFlags)snap.Ships.Night[k]}; " +
                         $"guns holding: {kept.Order} on {kept.Target}; back to doctrine: {after}; never engaged: {sweeping}");
        Assert.Equal(SearchlightOrder.Illuminate, lit);
        Assert.Equal(2, on);
        Assert.True(exposed);
        Assert.Equal(2, snap.Ships.BeamOn[k]);
        Assert.True((((NightFlags)snap.Ships.Night[k]) & NightFlags.Beams) != 0);
        Assert.Equal((SearchlightOrder.Illuminate, 2), (kept.Order, kept.Target));
        Assert.Equal(SearchlightOrder.Off, after);
        Assert.Equal(SearchlightOrder.Search, sweeping);
    }

    /// <summary>The cruiser alone on the night sea, no encounter: the player's beams still burn, sweeping about her bow, and
    /// go out when handed back to doctrine.</summary>
    [Fact]
    public void Beams_by_hand_burn_out_of_a_fight()
    {
        var w = new World(WorldTime.FromUtc(1917, 4, 21, 0), 7, S);
        var ai = new AiSystem(w);
        w.Apply(Command.SetWeather(20_000));
        w.Apply(ShipSystem.Spawn(w.Ships.AddClass(Fleet.Get("light_cruiser").Class!), Lat, Lon, 0, 10, 1));
        w.AdvanceTo(w.Time + 5_000);
        w.Apply(ai.Orders.Beams(Unit.Ship(1), true));
        w.AdvanceTo(w.Time + 30_000);
        var (on, _, exposed) = w.Searchlights.OrderOf(1, w.Time.Ms);
        w.Apply(ai.Orders.Beams(Unit.Ship(1), false));
        w.AdvanceTo(w.Time + 5_000);
        var after = w.Searchlights.OrderOf(1, w.Time.Ms).Order;
        output.WriteLine($"alone, beams by hand: {on} (out {exposed}); back to doctrine: {after}");
        Assert.Equal(SearchlightOrder.Search, on);
        Assert.True(exposed);
        Assert.Equal(SearchlightOrder.Off, after);
    }

    /// <summary>A German ship of 1916 (her star shell off by doctrine) told to light a point of the sea 4 km east: one flare
    /// bursts over it, a few hundred metres up, her captain leaving the task be until it's fired; then the task's done. An
    /// order called off before its shell goes ends at once.</summary>
    [Fact]
    public void Star_shell_by_hand_over_a_point_of_the_sea()
    {
        var w = new World(WorldTime.FromUtc(1916, 1, 15, 23), 7, S);
        var ai = new AiSystem(w);
        int c = w.Ships.AddClass(Fleet.Scenario("nassau_1916"));
        int d = w.Ships.AddClass(Fleet.Get("destroyer").Class!);
        w.Apply(Command.SetWeather(20_000));
        w.Apply(ShipSystem.Spawn(c, Lat, Lon, 0, 0, 1));
        w.Apply(ShipSystem.Spawn(d, Lat - 0.03, Lon, 0, 0, 2));   // a contact to keep her in an encounter, astern
        w.Apply(ai.Orders.SideDoctrine(1, DoctrineField.StarShell, (double)StarShellUse.Never));
        w.Apply(ai.Orders.SideDoctrine(1, DoctrineField.Unknowns, (double)Unknowns.Hold));
        w.Apply(ai.Orders.SideDoctrine(2, DoctrineField.Unknowns, (double)Unknowns.Hold));
        double lon = Lon + 4_000 / (Earth.Radius * Math.Cos(Lat * Math.PI / 180)) * 180 / Math.PI;
        var point = Earth.Position(Lat, lon);
        w.Apply(ai.Orders.StarShellAt(Unit.Ship(1), Lat, lon));
        bool busy = w.StarShells.Busy(1, out var lights);
        var snap = Snap(w);
        int k = snap.Ships.IndexOf(1);
        var held = (NightFlags)snap.Ships.Night[k];
        double nearest = double.MaxValue, height = 0;
        int most = 0;
        long doneMs = 0;
        for (int s = 0; s < 150; s++)
        {
            w.AdvanceTo(w.Time + 1_000);
            most = Math.Max(most, w.StarShells.FlareCount);
            for (int i = 0; i < w.StarShells.FlareCount; i++)
            {
                var (at, _) = w.StarShells.FlareAt(i, w.Time.Ms);
                double off = Earth.Distance(at.Normalized() * Earth.Radius, point);
                if (off < nearest)
                    (nearest, height) = (off, at.Length - Earth.Radius);
            }
            if (doneMs == 0 && !w.StarShells.Busy(1, out _))
                doneMs = s + 1;
        }

        w.Apply(ai.Orders.StarShellAt(Unit.Ship(1), Lat, lon));
        w.Apply(ai.Orders.StarShell(Unit.Ship(1), 0));
        bool after = w.StarShells.Busy(1, out _);
        output.WriteLine($"pending: busy {busy}, the snapshot's {snap.Ships.StarOn[k]}, flags {held}; the task done after {doneMs} s, " +
                         $"flares up at most {most}; the nearest {nearest:F0} m off the point at {height:F0} m; " +
                         $"the task's point {Earth.Distance(lights, point):F0} m off it; called off: {!after}");
        Assert.True(busy);
        Assert.Equal(-1, snap.Ships.StarOn[k]);
        Assert.True((held & NightFlags.StarHeld) != 0);
        Assert.InRange(doneMs, 1, 60);
        Assert.Equal(1, most);   // (the list keeps a spent flare until the next tick: a second shell would show as 2)
        Assert.InRange(nearest, 0, 800);
        Assert.InRange(height, 50, 600);
        Assert.False(after);
    }

    /// <summary>Star shell on a contact ordered to a squadron of two German ships 3 km apart: the one nearer the destroyer
    /// takes it, the other none.</summary>
    [Fact]
    public void A_squadrons_star_shell_goes_from_its_nearest_ship()
    {
        var w = new World(WorldTime.FromUtc(1916, 1, 15, 23), 7, S);
        var ai = new AiSystem(w);
        int c = w.Ships.AddClass(Fleet.Scenario("nassau_1916"));
        int d = w.Ships.AddClass(Fleet.Get("destroyer").Class!);
        w.Apply(Command.SetWeather(20_000));
        w.Apply(ShipSystem.Spawn(c, Lat, Lon, 0, 0, 1));
        w.Apply(ShipSystem.Spawn(c, Lat - 0.027, Lon, 0, 0, 1));
        w.Apply(ShipSystem.Spawn(d, Lat, Lon + 0.014, 180, 0, 2));
        w.Apply(GroupSystem.Create(1, Formation.Column, 3_000));
        int g = w.Groups[w.Groups.Count - 1].GroupId;
        w.Apply(GroupSystem.Add(g, 2));
        foreach (int side in new[] { 1, 2 })
            w.Apply(ai.Orders.SideDoctrine(side, DoctrineField.Unknowns, (double)Unknowns.Hold));
        w.Apply(ai.Orders.SideDoctrine(1, DoctrineField.StarShell, (double)StarShellUse.Never));
        w.AdvanceTo(w.Time + 30_000);
        Assert.True(w.Sightings.Of(1)!.IndexOf(3) >= 0, "the squadron holds the destroyer");
        w.Apply(ai.Orders.StarShell(Unit.Group(g), 3));
        int first = w.StarShells.TaskOf(1).Target, second = 0;
        for (int s = 0; s < 10; s++)
        {
            w.AdvanceTo(w.Time + 1_000);
            second |= w.StarShells.TaskOf(2).Target;
        }
        output.WriteLine($"the near ship's task {first}, the far one's {second}");
        Assert.Equal(3, first);
        Assert.Equal(0, second);
    }
}
