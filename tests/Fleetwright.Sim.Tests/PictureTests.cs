using Fleetwright.Sim.Combat;
using Fleetwright.Sim.Core;
using Xunit.Abstractions;

namespace Fleetwright.Sim.Tests;

/// <summary>Each navy's picture (docs/battle/plan.md step 3.2d): looks in encounters find ships at the ranges the model
/// gives, contacts are tracked by their fixes and dead reckoned out of sight, made out up close, forgotten in time; the
/// same world whatever the cuts, without garbage.</summary>
public class PictureTests(ITestOutputHelper output)
{
    static readonly WorldTime Day = WorldTime.FromUtc(1916, 5, 31, 14);
    static readonly SimSettings Settings = SimSettings.Load(Path.Combine(Fleet.Repo, "Content", "Sim", "settings.json"));
    const double Kn = 15;

    static ShipState Ship(World w, int e) => w.Ships.Store[w.Ships.Store.SlotOf(e)];

    static void Run(World w, double s) => w.AdvanceTo(w.Time + (long)Math.Round(s * 1000));

    /// <summary>Ship 1 (side 1) and ship 2 (side 2), dreadnoughts, 60 km apart on a meridian closing head on, in a
    /// visibility; both kept on the fine tick by a rudder held amidships.</summary>
    static World Closing(WorldTime start, double visibilityM, ulong seed = 4, string design = "dreadnought")
    {
        var w = new World(start, seed, Settings);
        int c = w.Ships.AddClass(Fleet.Get(design).Class!);
        w.Apply(Command.SetWeather(visibilityM));
        w.Apply(ShipSystem.Spawn(c, 56, 4, 0, Kn, 1));
        var p = Earth.Position(56, 4);
        var f = Earth.Forward(p, 0);
        Earth.Advance(ref p, ref f, 60_000);
        var (la, lo) = Earth.LatLon(p);
        w.Apply(ShipSystem.Spawn(c, la, lo, 180, Kn, 2));
        w.Apply(ShipSystem.Rudder(1, 0));
        w.Apply(ShipSystem.Rudder(2, 0));
        return w;
    }

    static double Apart(World w) => Earth.Distance(Ship(w, 1).Position, Ship(w, 2).Position);

    /// <summary>Runs until side 1 holds ship 2; the range it was first seen at, km.</summary>
    static double FirstSeen(World w)
    {
        while (w.Sightings.Of(1)?.IndexOf(2) is null or < 0)
        {
            Run(w, 1);
            Assert.True(Apart(w) > 500, "never seen");
        }
        return Apart(w) / 1000;
    }

    [Fact]
    public void Ships_closing_are_seen_at_the_models_range_and_tracked()
    {
        var w = Closing(Day, 25_000);
        double first = FirstSeen(w);
        var pic = w.Sightings.Of(1)!;
        int i = pic.IndexOf(2);
        output.WriteLine($"first seen at {first:F1} km by {pic[i].Eye}, level {pic[i].Level}");
        // the model gives a dreadnought made out at ~24 km and found within a minute at ~18 km in 25 km
        Assert.InRange(first, 15, 27);
        Assert.True(pic[i].InSight);
        double err0 = Earth.Distance(pic[i].Position, Ship(w, 2).Position);

        // three minutes of fixes: the position and the course and speed close on the truth
        Run(w, 180);
        i = pic.IndexOf(2);
        var (p, v, cov) = pic.EstimateAt(i, w.Time.Ms, Settings);
        double err = Earth.Distance(p, Ship(w, 2).Position);
        double speedErr = Math.Abs(v.Length - Math.Abs(Ship(w, 2).Speed)) / Handling.MsPerKnot;
        double courseErr = Math.Abs(Angle(Earth.Heading(p, v) - Ship(w, 2).HeadingDeg));
        var (major, minor, _) = cov.Ellipse();
        output.WriteLine($"first fix {err0:F0} m off; after 3 min {err:F0} m off (ellipse {major:F0} x {minor:F0} m), speed {speedErr:F1} kn off, course {courseErr:F1}° off, level {pic[i].Level}");
        Assert.True(err < 2 * major + 200, $"{err:F0} m off, ellipse {major:F0} m");
        Assert.True(speedErr < 3 && courseErr < 15, $"{speedErr:F1} kn, {courseErr:F1}°");
        Assert.True(w.Sightings.Of(2)?.IndexOf(1) >= 0, "and the other side sees her");
    }

    [Fact]
    public void A_look_now_sees_a_ship_spawned_in_sight_at_once()
    {
        // ship 1 steaming a while (on rails, if the tier puts it there), then an enemy spawned 15 km off: without the
        // look nobody has seen anybody at that moment; with it, both sides have, the clock not moved
        var w = new World(Day, 4, Settings);
        int c = w.Ships.AddClass(Fleet.Get("dreadnought").Class!);
        w.Apply(Command.SetWeather(25_000));
        w.Apply(ShipSystem.Spawn(c, 56, 4, 0, Kn, 1));
        Run(w, 600);
        var p = Ship(w, 1).Position;
        var f = Earth.Forward(p, 90);
        Earth.Advance(ref p, ref f, 15_000);
        var (la, lo) = Earth.LatLon(p);
        w.Apply(ShipSystem.Spawn(c, la, lo, 180, Kn, 2));
        output.WriteLine($"ship 1 on rails: {w.Ships.Store.OnRails(w.Ships.Store.SlotOf(1))}");
        Assert.True(w.Sightings.Of(1)?.IndexOf(2) is null or < 0);
        var t = w.Time;
        w.Apply(ShipSystem.LookNow());
        Assert.Equal(t, w.Time);
        Assert.True(w.Sightings.Of(1)?.IndexOf(2) >= 0, "side 1 sees the new ship");
        Assert.True(w.Sightings.Of(2)?.IndexOf(1) >= 0, "and the new ship sees side 1");
        Assert.Equal(1, w.Encounters.Count);
    }

    [Fact]
    public void Up_close_she_is_named_and_out_of_sight_dead_reckoned_and_forgotten()
    {
        var w = Closing(Day, 25_000);
        FirstSeen(w);
        var pic = w.Sightings.Of(1)!;
        while (Apart(w) > 4_000)
            Run(w, 5);
        Run(w, 10);
        int i = pic.IndexOf(2);
        Assert.Equal(ContactLevel.Name, pic[i].Level);
        Assert.Equal(Ship(w, 2).ClassIndex, pic[i].TakenFor);
        Assert.Equal(ShipType.Capital, pic[i].Type);

        // they pass and open: out of sight past the model's range, then dead reckoned
        while (pic[pic.IndexOf(2)].InSight)
            Run(w, 10);
        double lost = Apart(w) / 1000;
        i = pic.IndexOf(2);
        long seen = pic[i].SeenMs;
        Run(w, 600);
        var (p, _, cov) = pic.EstimateAt(pic.IndexOf(2), w.Time.Ms, Settings);
        double err = Earth.Distance(p, Ship(w, 2).Position);
        output.WriteLine($"lost at {lost:F1} km; 10 min on, dead reckoned {err:F0} m off, ellipse {cov.Ellipse().Major:F0} m");
        Assert.InRange(lost, 15, 33);   // 30.3 km since the director went up into the foretop (fire-control.md F4b); F4d re-fits
        Assert.True(err < 3_000, $"{err:F0} m");
        Assert.Equal(seen, pic[pic.IndexOf(2)].SeenMs);

        // the encounter closes and the looks stop; six hours out of sight it's forgotten (at the hourly contact scan)
        w.AdvanceTo(new WorldTime(seen + Settings.SightForgetMs - 60_000));
        Assert.True(pic.IndexOf(2) >= 0);
        w.AdvanceTo(new WorldTime(seen + Settings.SightForgetMs + Settings.ContactScanMs));
        Assert.True(pic.IndexOf(2) < 0, "forgotten");
    }

    [Fact]
    public void At_night_only_a_few_kilometres()
    {
        var w = Closing(WorldTime.FromUtc(1916, 1, 15, 23), 25_000);   // a winter midnight
        Assert.True(Light.SunElevationDeg(Ship(w, 1).Position, w.Time) < -30);
        double first = FirstSeen(w);
        output.WriteLine($"first seen at night at {first:F1} km");
        Assert.InRange(first, 1, 7);
    }

    [Fact]
    public void The_same_looks_whatever_the_cuts_and_on_replay()
    {
        var a = Closing(Day, 25_000, 7);
        var b = Closing(Day, 25_000, 7);
        Run(a, 3600);
        while (b.Time < a.Time)
            b.AdvanceTo(new WorldTime(Math.Min(b.Time.Ms + 7_003, a.Time.Ms)));   // cut at odd moments
        Assert.Equal(a.ComputeHash(), b.ComputeHash());
        var r = new World(Day, 7, Settings);
        r.Ships.AddClass(Fleet.Get("dreadnought").Class!);
        foreach (var sc in a.CommandLog)
        {
            r.AdvanceTo(sc.At);
            r.Apply(sc.Command);
        }
        r.AdvanceTo(a.Time);
        Assert.Equal(a.ComputeHash(), r.ComputeHash());
    }

    /// <summary>The player's snapshot (docs/battle/plan.md step 3.2e): their own ships and groups, the enemy's only as
    /// contacts from their navy's picture (where it reckons them); with the truth, every ship.</summary>
    [Fact]
    public void The_players_snapshot_holds_the_enemy_only_as_contacts()
    {
        var w = Closing(Day, 25_000);
        w.Apply(Command.SetPlayerSide(1));
        w.Apply(GroupSystem.Create(2, Formation.Column, 600));   // the enemy's "group" of one: not to be named to the player
        var s = new Snapshot();
        w.Fill(s);
        Assert.Equal(1, s.Ships.Count);
        Assert.Equal(1, s.Ships.Entity[0]);
        Assert.Equal(0, s.Groups.Count);
        Assert.Equal(0, s.Contacts.Count);
        FirstSeen(w);
        w.Fill(s);
        Assert.Equal(1, s.Ships.Count);
        Assert.Equal(1, s.Contacts.Count);
        Assert.Equal(2, s.Contacts.Entity[0]);
        Assert.True(s.Contacts.InSight[0]);
        Assert.True(Earth.Distance(s.Contacts.Position[0], Ship(w, 2).Position) < 1000);
        Assert.True(s.Contacts.MajorM[0] >= s.Contacts.MinorM[0]);
        Assert.True(s.Ships.Encounter[0] > 0);

        w.Fill(s, truth: true);
        Assert.Equal(2, s.Ships.Count);
        Assert.Equal(1, s.Groups.Count);
        Assert.Equal(1, s.Contacts.Count);   // the picture still, to hold against the truth
        var none = new World(Day, 1, Settings);
        none.Fill(s);
        Assert.Equal(0, s.Contacts.Count);
    }

    /// <summary>Two lines of 100 abreast 25 km apart: every ship of each side held by the other in a minute, the looks
    /// allocating nothing once the pictures have grown.</summary>
    [Fact]
    public void Two_hundred_ships_look_at_each_other_without_garbage()
    {
        var w = new World(Day, 5, Settings);
        int c = w.Ships.AddClass(Fleet.Get("dreadnought").Class!);
        w.Apply(Command.SetWeather(30_000));
        for (int i = 0; i < 100; i++)
        {
            foreach (var (lat, hdg, s) in new[] { (56.0, 0.0, (byte)1), (56.22, 180.0, (byte)2) })
            {
                var p = Earth.Position(lat, 3);
                var f = Earth.Forward(p, 90);
                Earth.Advance(ref p, ref f, i * 400);
                var (la, lo) = Earth.LatLon(p);
                w.Apply(ShipSystem.Spawn(c, la, lo, hdg, 0, s));
                w.Apply(ShipSystem.Rudder(w.Ships.NewestEntity, 0));
            }
        }
        Run(w, 300);
        Assert.Equal(100, w.Sightings.Of(1)!.Count);
        Assert.Equal(100, w.Sightings.Of(2)!.Count);
        var sw = new System.Diagnostics.Stopwatch();   // made before the count: it's an object
        long before = GC.GetAllocatedBytesForCurrentThread();
        sw.Start();
        Run(w, 600);
        sw.Stop();
        long allocated = GC.GetAllocatedBytesForCurrentThread() - before;
        var (calls, total) = Probe(w, "Sighting");
        output.WriteLine($"10 minutes of 200 ships: {allocated} bytes allocated, {sw.Elapsed.TotalSeconds:F2} s; a look {total / Math.Max(calls, 1) * 1e6:F0} µs");
        Assert.Equal(0, allocated);
    }

    static (long Calls, double Seconds) Probe(World w, string name)
    {
        for (int i = 0; i < w.Probes.Count; i++)
            if (w.Probes.Names[i] == name)
                return (w.Probes.Calls[i], Probes.Ms(w.Probes.Ticks[i]) / 1000);
        return (0, 0);
    }

    static double Angle(double d) => d - 360 * Math.Round(d / 360);
}
