using Fleetwright.Shipgen;
using Fleetwright.Sim.Combat;
using Fleetwright.Sim.Core;
using Fleetwright.Sim.Ships;
using Xunit.Abstractions;

namespace Fleetwright.Sim.Tests;

/// <summary>B2 of docs/battle/fire.md: what a fire does. Its heat harms what's in its space by a dose of time (a local
/// fire hardly), a flashover catches the room's men, smoke takes the machinery under a fire (fans running far sooner than
/// secured) and steam with it, a magazine cooks off on its boundary in tens of minutes beside a fire and hours under one
/// unless she floods it first (and smoke over it keeps her from that), and a fire shows when it's developed or in the
/// open, a light at night.</summary>
public class FireEffectsTests(ITestOutputHelper output)
{
    static readonly SimSettings Settings = SimSettings.Load(Path.Combine(Fleet.Repo, "Content", "Sim", "settings.json"));

    /// <summary>No spread, so one space's fire stays one.</summary>
    static readonly SimSettings Still = Settings with { FireOpenPathPerMin = 0, FireSteelPerMin = [0, 0, 0], FireDeckPerMin = 0, FireReflashPerMin = 0 };

    static ShipClass Class(string name) => Fleet.Get(name).Class!;

    static ShipCondition Fresh(ShipClass c, string readiness = "prepared")
    {
        var k = new ShipCondition();
        k.Reset(c);
        k.Readiness = Settings.Readiness.Single(x => x.Name == readiness);
        return k;
    }

    /// <summary>A space set burning developed (or another phase) with fuel for hours.</summary>
    static void Burn(ShipClass c, ShipCondition k, int space, BurnPhase phase = BurnPhase.Developed, double q = 2)
    {
        double area = Math.Max(c.FireSpaces[space].AreaM2, 1);
        k.Fire[space] = new BurnState { Phase = phase, Q = q, E = 1e7, E0 = 1e7, Involved = area, InvolvedMax = area, Seat = 0.1 };
    }

    static IEnumerable<FireLink> Links(ShipClass c, int space) => c.FireLinks(c.FireSpaces[space].Links).ToArray();

    static RoomKind KindOf(ShipClass c, int cell) => c.Cells[cell].Room >= 0 ? c.Rooms[c.Cells[cell].Room].Kind : RoomKind.Stores;

    static double Median(List<double> xs)
    {
        xs.Sort();
        return xs.Count == 0 ? double.NaN : xs[xs.Count / 2];
    }

    [Fact]
    public void A_developed_fire_harms_its_rooms_by_the_dose_and_a_local_one_hardly()
    {
        var c = Class("dreadnought");
        var f = new FloodModel(c);
        int cell = Enumerable.Range(0, c.Cells.Length).First(i => c.FireSpaces[i].Use == FireUse.Quarters && c.FireSpaces[i].AreaM2 > 20);
        int node = c.RoomNode(c.Cells[cell].Room);
        var k = Fresh(c);
        var r = RandomStream.FromSeed(3);
        Fires.Ignite(c, k, cell, 0.3, Still);
        double flashed = double.NaN, damaged = double.NaN, disabled = double.NaN;
        for (int t = 1; t <= 40 * 60; t++)
        {
            var st = Fires.Step(c, f, k, 1, Still, ref r, t * 1000L);
            if (st.Flashed > 0)
                flashed = t / 60.0;
            if (double.IsNaN(damaged) && k.State[node] >= PartState.Damaged)
                damaged = t / 60.0;
            if (double.IsNaN(disabled) && k.State[node] >= PartState.Disabled)
                disabled = t / 60.0;
        }
        output.WriteLine($"{c.CellId(cell)} ({c.FireSpaces[cell].AreaM2:0} m²): flashover {flashed:0.0} min, its room damaged {damaged:0.0}, " +
                         $"out of action {disabled:0.0}");
        Assert.InRange(damaged - flashed, 1, 10);   // minutes over 232 °C
        Assert.InRange(disabled - flashed, 8, 30);

        // a local fire for its twenty minutes: nothing worse
        var local = Fresh(c);
        Burn(c, local, cell, BurnPhase.Local, 0.1);
        for (int t = 1; t <= 25 * 60; t++)
            Fires.Step(c, f, local, 1, Still, ref r, t * 1000L);
        Assert.Equal(PartState.Ok, local.State[node]);
    }

    [Fact]
    public void A_flashover_catches_the_rooms_men()
    {
        var c = Class("dreadnought");
        var f = new FloodModel(c);
        int cell = Enumerable.Range(0, c.Cells.Length)
            .Where(i => c.FireSpaces[i].Use is FireUse.Quarters or FireUse.Cabins or FireUse.Offices or FireUse.Stores && c.FireSpaces[i].AreaM2 > 10 && c.Cells[i].BattleCrew >= 3)
            .OrderByDescending(i => c.Cells[i].BattleCrew).First();
        int men = c.Cells[cell].BattleCrew;
        double share = Fires.Room(c.FireSpaces[cell], Settings) / c.FireSpaces[cell].AreaM2;
        double killed = 0, hurt = 0;
        int runs = 0;
        for (ulong seed = 0; seed < 20; seed++)
        {
            var k = Fresh(c);
            var r = RandomStream.FromSeed(seed);
            Fires.Ignite(c, k, cell, 0.3, Still);
            for (int t = 1; t <= 20 * 60 && k.Fire[cell].Phase != BurnPhase.Developed; t++)
                Fires.Step(c, f, k, 1, Still, ref r, t * 1000L);
            if (k.Fire[cell].Phase != BurnPhase.Developed)
                continue;
            runs++;
            (killed, hurt) = (killed + k.Killed, hurt + k.Killed + k.Wounded);
        }
        output.WriteLine($"{c.CellId(cell)} ({KindOf(c, cell)}, {men} men, the room {share:P0} of it): flashed in {runs} of 20; " +
                         $"killed {killed / runs:0.0}, casualties {hurt / runs:0.0} a fire (expected {men * share * Settings.FireFlashCaught[0]:0.0}, " +
                         $"{men * share * Settings.FireFlashCaught[1]:0.0})");
        Assert.True(runs > 10);
        Assert.InRange(hurt / runs, 0.5 * men * share * Settings.FireFlashCaught[1], 1.5 * men * share * Settings.FireFlashCaught[1] + 1);
        Assert.True(killed <= hurt);
    }

    /// <summary>A boiler room with a space over it.</summary>
    static (int Cell, int Over) Machinery(ShipClass c)
    {
        foreach (int i in Enumerable.Range(0, c.Cells.Length).Where(i => KindOf(c, i) == RoomKind.BoilerRoom))
            foreach (var l in Links(c, i))
                if (l.Way == FireWay.Up)
                    return (i, l.Space);
        throw new InvalidOperationException("no boiler room with a space over it");
    }

    [Fact]
    public void Smoke_takes_the_machinery_under_a_fire_far_sooner_with_the_fans_running()
    {
        var c = Class("dreadnought");
        var f = new FloodModel(c);
        var (cell, over) = Machinery(c);
        int node = c.RoomNode(c.Cells[cell].Room);
        foreach (var (fans, lo, hi) in new[] { (true, 0.85, 1.0), (false, 0.15, 0.65) })
        {
            int lost = 0;
            var at = new List<double>();
            for (ulong seed = 0; seed < 40; seed++)
            {
                var k = Fresh(c);
                k.Readiness = k.Readiness with { FansSecured = !fans };
                var r = RandomStream.FromSeed(seed);
                Burn(c, k, over);
                for (int t = 10; t <= 30 * 60; t += 10)
                    if (Fires.Step(c, f, k, 10, Still, ref r, t * 1000L).Abandoned > 0)
                    {
                        at.Add(t / 60.0);
                        lost++;
                        break;
                    }
            }
            output.WriteLine($"fans {(fans ? "running" : "secured")}: the boiler room under a developed fire abandoned in 30 min in {lost} of 40, " +
                             $"median {Median(at):0.0} min");
            Assert.InRange(lost / 40.0, lo, hi);
        }

        // abandoned, she loses its steam; it's manned again after the smoke clears
        var s = Fresh(c);
        var rs = RandomStream.FromSeed(1);
        Burn(c, s, over);
        long now = 0;
        while (!s.Smoked[c.Cells[cell].Room])
            Fires.Step(c, f, s, 10, Still, ref rs, now += 10_000);
        Functions.Evaluate(s, Settings.DraughtFloor);
        output.WriteLine($"abandoned at {now / 60_000.0:0.0} min: power {s.PowerCap:P0}");
        Assert.True(s.PowerCap < 1);
        Assert.True(s.Has(node, PartFlags.Down));
        Fires.Douse(c, s, over, Still);
        long outAt = now;
        for (; now < outAt + 30 * 60_000; now += 10_000)
        {
            Fires.Step(c, f, s, 10, Still, ref rs, now);
            if (s.NextClearMs >= 0 && s.NextClearMs <= now)
                s.Clear(now);
            if (!s.Has(node, PartFlags.Down))
                break;
        }
        output.WriteLine($"manned again {(now - outAt) / 60_000.0:0.0} min after the fire over it was out");
        Assert.InRange((now - outAt) / 1000.0, Settings.FireSmokeClearS - 20, Settings.FireSmokeClearS + 20);
        Functions.Evaluate(s, Settings.DraughtFloor);
        Assert.Equal(1, s.PowerCap, 9);
    }

    /// <summary>A magazine cell with a space beside it through a bulkhead, and a space over it that burns (not its handling
    /// room).</summary>
    static (int Cell, int Room, int Beside, int Over) Magazine(ShipClass c)
    {
        foreach (int i in Enumerable.Range(0, c.Cells.Length).Where(i => KindOf(c, i) == RoomKind.Magazine))
        {
            int beside = Links(c, i).Where(l => l.Way == FireWay.Side && l.Space < c.Cells.Length && KindOf(c, l.Space) != RoomKind.Magazine)
                .Select(l => l.Space).DefaultIfEmpty(-1).First();
            int over = Links(c, i).Where(l => l.Way == FireWay.Up && c.FireSpaces[l.Space].Use != FireUse.None).Select(l => l.Space).DefaultIfEmpty(-1).First();
            if (beside >= 0 && over >= 0)
                return (i, c.Cells[i].Room, beside, over);
        }
        throw new InvalidOperationException("no magazine with a space beside and over it");
    }

    /// <summary>Minutes until a magazine went up with a developed fire on it (NaN: not in the time), stepping 10 s.</summary>
    static double CookOff(ShipClass c, FloodModel f, ShipCondition k, int fire, double hours, ulong seed)
    {
        var r = RandomStream.FromSeed(seed);
        Burn(c, k, fire);
        for (int t = 10; t <= hours * 3600; t += 10)
            if (Fires.Step(c, f, k, 10, Still, ref r, t * 1000L).Magazine)
                return t / 60.0;
        return double.NaN;
    }

    [Fact]
    public void A_magazine_cooks_off_in_tens_of_minutes_beside_a_fire_and_hours_under_one()
    {
        var c = Class("dreadnought");
        var f = new FloodModel(c);
        var (cell, room, beside, over) = Magazine(c);
        var side = Enumerable.Range(0, 40).Select(s => CookOff(c, f, Fresh(c), beside, 3, (ulong)s)).ToList();
        var above = Enumerable.Range(0, 40).Select(s => CookOff(c, f, Fresh(c), over, 8, (ulong)s)).ToList();
        double ms = Median(side.Select(x => double.IsNaN(x) ? 999 : x).ToList()), ma = Median(above.Select(x => double.IsNaN(x) ? 9999 : x).ToList());
        output.WriteLine($"{c.CellId(cell)}: a fire beside it ({c.CellId(beside)}) sets it off in a median {ms:0} min " +
                         $"(10%: {side.Order().ElementAt(4):0}), one over it ({over}) {ma / 60:0.0} h");
        Assert.InRange(ms, 20, 80);   // "tens of minutes"
        Assert.InRange(ma / 60, 1.5, 6);   // hours: its floor the fire's cool layer
        Assert.Equal(RoomKind.Magazine, c.Rooms[room].Kind);
    }

    [Fact]
    public void Her_crew_flood_a_threatened_magazine_unless_smoke_over_it_cuts_the_controls_off()
    {
        var c = Class("dreadnought");
        var f = new FloodModel(c);
        var (cell, room, beside, over) = Magazine(c);
        // the doctrine floods; nobody fights the fire itself
        var doc = Settings.DamageControl.Single(d => d.Name == "1916") with { WatchS = -1, PartyShare = 0, TurnTo = 0 };
        int up = 0, flooded = 0;
        double water = 0;
        for (ulong seed = 0; seed < 20; seed++)
        {
            var k = Fresh(c);
            k.DamageControl = doc;
            bool blew = !double.IsNaN(CookOff(c, f, k, beside, 1, seed));
            up += blew ? 1 : 0;
            flooded += k.MagazineFlood[room] ? 1 : 0;
            water += k.WaterM3[cell] / f.Capacity[cell] / 20;
        }
        output.WriteLine($"flooded {doc.FloodMagazineS / 60:0} min after the fire beside it: flooded in {flooded} of 20, gone up in {up}, " +
                         $"{water:P0} full after the hour ({(c.Cells[cell].BelowWaterline ? "sea valves" : "the main")})");
        Assert.Equal(20, flooded);
        Assert.True(up <= 1);
        Assert.True(water > 0.9);

        // smoke over it: the controls are cut off, nobody floods it, and it goes up
        int blocked = 0, gone = 0;
        for (ulong seed = 0; seed < 20; seed++)
        {
            var k = Fresh(c);
            k.DamageControl = doc;
            Burn(c, k, over, BurnPhase.Starved, 0.05);
            k.Fire[over].PhaseS = -1e9;   // smouldering for good
            bool blew = !double.IsNaN(CookOff(c, f, k, beside, 3, seed));
            blocked += k.MagazineFlood[room] ? 0 : 1;
            gone += blew ? 1 : 0;
        }
        output.WriteLine($"smoke over it: not flooded in {blocked} of 20, gone up in {gone}");
        Assert.Equal(20, blocked);
        Assert.True(gone >= 15);
    }

    [Fact]
    public void A_fire_shows_when_developed_or_in_the_open_and_glows_at_night()
    {
        var c = Class("dreadnought");
        var f = new FloodModel(c);
        int cell = Enumerable.Range(0, c.Cells.Length).First(i => c.FireSpaces[i].Use == FireUse.Quarters && c.FireSpaces[i].AreaM2 > 20);
        int deck = c.FireOfDeck(c.Sections.Length / 2);
        var k = Fresh(c, "closed up");   // doors shut: it stays starved
        var r = RandomStream.FromSeed(5);
        Burn(c, k, cell, BurnPhase.Starved, 0.05);
        k.Fire[cell].PhaseS = -1e9;
        Fires.Step(c, f, k, 1, Still, ref r);
        Assert.Equal(0, k.FireShowMW);   // a starved fire inside shows nothing
        Burn(c, k, cell);
        Fires.Step(c, f, k, 1, Still, ref r);
        double inside = k.FireShowMW;
        Burn(c, k, deck, BurnPhase.Local, 0.5);
        Fires.Step(c, f, k, 1, Still, ref r);
        output.WriteLine($"developed inside {inside:0.00} MW shows; with a deck fire {k.FireShowMW:0.00} MW");
        Assert.True(inside > 0);
        Assert.True(k.FireShowMW > inside);

        // at night, a 2 MW fire as a lamp through 20 km of visibility, from a 30 m masthead
        double Margin(double km, double sun) => SightModel.GlowMargin(30, 2, 10, km * 1000, 20_000, sun, Settings);
        double reach = Enumerable.Range(1, 60).Where(km => Margin(km, -20) >= 1).DefaultIfEmpty(0).Max();
        output.WriteLine($"a 2 MW fire at night made out to {reach} km; margin at 10 km {Margin(10, -20):0}, at sunset {Margin(10, -0.5):0.00}, by day {Margin(10, 10)}");
        Assert.InRange(reach, 12, 35);
        Assert.Equal(0, Margin(10, 10));
        Assert.True(Margin(10, -0.5) < Margin(10, -20));
    }
}
