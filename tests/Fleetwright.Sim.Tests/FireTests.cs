using Fleetwright.Shipgen;
using Fleetwright.Sim.Combat;
using Fleetwright.Sim.Core;
using Fleetwright.Sim.Ships;
using Xunit.Abstractions;

namespace Fleetwright.Sim.Tests;

/// <summary>Fire after it starts (docs/battle/fire.md B0; damage/17 §8.7): one space unfought grows, flashes over in
/// minutes and burns for hours on one door; closed up it starves; a thin load burns locally and dies; a shell hole lets a
/// starved fire flash; the step doesn't matter. The fire spaces and their loads come from the design's outfit, and a
/// fire lit on a ship burns as one.</summary>
public class FireTests(ITestOutputHelper output)
{
    static readonly SimSettings Settings = SimSettings.Load(Path.Combine(Fleet.Repo, "Content", "Sim", "settings.json"));

    static ShipClass Class(string name) => Fleet.Get(name).Class!;

    static FireSpaceRow Room(double area, double q, double growth, FireUse use = FireUse.Quarters) => new()
    {
        Kind = FireSpaceKind.Cell, Use = use, AreaM2 = area, HeightM = 2.6, LoadMJm2 = q, StrippedMJm2 = q, Growth = growth,
    };

    static double Aoh(int doors, double holes) => Settings.FireLeakAoh + doors * Settings.FireDoorAoh + Math.Pow(holes, 1.25);

    /// <summary>A fire from a seat of 0.1 MW left to burn: when it flashed over (min, NaN none), its peak (MW), how long
    /// it burned (h), and the phases it went through.</summary>
    static (double Fo, double Peak, double Hours, List<BurnPhase> Phases) Burn(FireSpaceRow row, double aoh, double dt = 5,
        Func<double, double>? aohAt = null)
    {
        var x = new BurnState { Phase = BurnPhase.Grow, Q = 0.1, Seat = 0.1, E0 = row.LoadMJm2 * row.AreaM2, E = row.LoadMJm2 * row.AreaM2 };
        double fo = double.NaN, peak = 0, t = 0;
        var phases = new List<BurnPhase> { BurnPhase.Grow };
        while (x.Burning && t < 8 * 3600)
        {
            if (Fires.Advance(row, ref x, aohAt?.Invoke(t) ?? aoh, row.LoadMJm2, dt, Settings) == BurnEvent.Flashover && double.IsNaN(fo))
                fo = (t + dt) / 60;
            t += dt;
            peak = Math.Max(peak, x.Q);
            if (phases[^1] != x.Phase)
                phases.Add(x.Phase);
        }
        return (fo, peak, t / 3600, phases);
    }

    [Fact]
    public void A_furnished_space_flashes_over_in_minutes_and_burns_for_hours_through_its_door()
    {
        var (fo, peak, hours, phases) = Burn(Room(40, 480, 1.3), Aoh(1, 0));
        output.WriteLine($"40 m² 1939 messdeck, one door: flashover {fo:0.0} min, {peak:0.00} MW, {hours:0.0} h, {string.Join("/", phases)}");
        Assert.InRange(fo, 3, 8);
        Assert.InRange(peak, 1.8, 2.6);   // ~2 MW whatever is inside: one door's air
        Assert.InRange(hours, 2, 4);
        Assert.Equal(BurnPhase.Out, phases[^1]);
    }

    [Fact]
    public void Closed_up_it_starves_and_a_shell_hole_lets_it_flash()
    {
        var closed = Burn(Room(40, 480, 1.3), Aoh(0, 0));
        output.WriteLine($"closed up: {string.Join("/", closed.Phases)}, {closed.Peak:0.00} MW, {closed.Hours * 60:0} min");
        Assert.True(double.IsNaN(closed.Fo));
        Assert.Contains(BurnPhase.Starved, closed.Phases);
        Assert.True(closed.Peak < 0.2);
        Assert.InRange(closed.Hours * 60, 20, 45);

        // starved, then holed at 10 minutes: backdraft
        var holed = Burn(Room(40, 480, 1.3), Aoh(0, 0), aohAt: t => t < 600 ? Aoh(0, 0) : Aoh(0, 1));
        output.WriteLine($"holed at 10 min: {string.Join("/", holed.Phases)}, flashover {holed.Fo:0.0} min, {holed.Peak:0.00} MW");
        Assert.Equal([BurnPhase.Grow, BurnPhase.Starved, BurnPhase.Developed], holed.Phases.Take(3));
        Assert.InRange(holed.Fo, 10, 10.5);
    }

    [Fact]
    public void A_thin_load_burns_locally_and_dies()
    {
        var (fo, peak, hours, phases) = Burn(Room(40, 250, 0.7), Aoh(1, 0));
        output.WriteLine($"40 m² 1943 berthing: {string.Join("/", phases)}, {peak:0.00} MW, {hours * 60:0} min");
        Assert.True(double.IsNaN(fo));
        Assert.Contains(BurnPhase.Local, phases);
        Assert.True(peak < 1);
        Assert.InRange(hours * 60, 10, 30);
    }

    [Fact]
    public void Holes_burn_it_hotter_and_shorter()
    {
        var (_, door, doorH, _) = Burn(Room(40, 480, 1.3), Aoh(1, 0));
        var (_, holed, holedH, _) = Burn(Room(40, 480, 1.3), Aoh(1, 3));
        output.WriteLine($"door {door:0.0} MW {doorH:0.0} h; door + 3 m² {holed:0.0} MW {holedH:0.0} h");
        Assert.InRange(holed, 5, 7);
        Assert.InRange(holedH, 0.9, 1.5);
        Assert.True(holedH < doorH / 2);
    }

    [Fact]
    public void The_step_does_not_matter()
    {
        foreach (var (row, aoh) in new[] { (Room(40, 480, 1.3), Aoh(1, 0)), (Room(90, 930, 2), Aoh(1, 4)), (Room(300, 450, 1), Aoh(1, 0)) })
        {
            var a = Burn(row, aoh, 1);
            var b = Burn(row, aoh, 15);
            output.WriteLine($"{row.AreaM2} m² {row.LoadMJm2} MJ/m²: 1 s {a.Fo:0.00} min {a.Hours:0.00} h, 15 s {b.Fo:0.00} min {b.Hours:0.00} h");
            Assert.InRange(b.Fo - a.Fo, -0.1, 0.5);
            Assert.InRange(b.Hours / a.Hours, 0.97, 1.03);
        }
    }

    [Fact]
    public void A_big_space_goes_up_a_room_at_a_time()
    {
        var row = Room(300, 450, 1);
        var x = new BurnState { Phase = BurnPhase.Grow, Q = 0.3, Seat = 0.3, E0 = 450 * 300, E = 450 * 300 };
        double flashed = double.NaN, all = double.NaN;
        for (double t = 0; t < 3600 && double.IsNaN(all); t += 5)
        {
            if (Fires.Advance(row, ref x, Aoh(1, 0), 450, 5, Settings) == BurnEvent.Flashover)
            {
                flashed = t;
                Assert.Equal(90, x.Involved, 6);   // one messdeck's room
            }
            if (x.Involved >= 300)
                all = t;
        }
        output.WriteLine($"flashover {flashed / 60:0.0} min, all 300 m² by {all / 60:0.0} min");
        Assert.InRange((all - flashed) / 60, 4, 10);   // two more rooms and a part, 2 min each
    }

    [Fact]
    public void Every_cell_part_and_section_has_its_fire_space_with_the_outfits_loads()
    {
        foreach (var name in new[] { "dreadnought", "destroyer", "bismarck" })
        {
            var c = Class(name);
            var spaces = c.FireSpaces;
            for (int i = 0; i < c.Cells.Length; i++)
                Assert.Equal((FireSpaceKind.Cell, i), (spaces[i].Kind, spaces[i].Index));
            for (int s = 0; s < c.Sections.Length; s++)
            {
                var d = spaces[c.FireOfDeck(s)];
                Assert.Equal((FireSpaceKind.Deck, s, FireUse.Deck), (d.Kind, d.Index, d.Use));
                Assert.True(d.AreaM2 > 0, $"{name}: section {s}'s deck has no area");
            }
            int parts = 0;
            for (int p = 0; p < c.Components.Length; p++)
                if (c.FireOfPart(p) is int f and >= 0)
                {
                    parts++;
                    Assert.Equal((FireSpaceKind.Part, p), (spaces[f].Kind, spaces[f].Index));
                }
            var quarters = spaces.ToArray().Where(x => x.Use == FireUse.Quarters).ToList();
            var none = spaces.ToArray().Where(x => x.Kind == FireSpaceKind.Cell && x.Use == FireUse.None).ToList();
            output.WriteLine($"{name}: {spaces.Length} spaces ({c.Cells.Length} cells, {parts} parts, {c.Sections.Length} decks); " +
                             $"quarters {quarters.Count} at {quarters[0].LoadMJm2:0} MJ/m², {none.Count} cells that don't burn");
            Assert.NotEmpty(quarters);
            Assert.All(spaces.ToArray(), x => Assert.True(x.Use == FireUse.None ? x.LoadMJm2 == 0 : x.LoadMJm2 > 0));
            foreach (int r in Enumerable.Range(0, c.Rooms.Length).Where(r => c.Rooms[r].Kind is RoomKind.Magazine or RoomKind.FuelTank or RoomKind.Bunker))
                foreach (int cell in c.Links(c.Rooms[r].Cells))
                    if (c.Cells[cell].Room == r)
                        Assert.Equal(FireUse.None, spaces[cell].Use);
        }
    }

    [Fact]
    public void The_outfit_is_the_navys_in_the_plants_year_and_a_design_can_give_its_own()
    {
        Assert.Equal("Victorian (1890)", Outfits.ByNavyAndYear("portsmouth", 1894).Name);
        Assert.Equal("Great War (1914)", Outfits.ByNavyAndYear("portsmouth", 1916).Name);
        Assert.Equal("Post-Savo (1943)", Outfits.ByNavyAndYear("brooklyn", 1944).Name);
        Assert.Equal("Interwar (1939)", Outfits.ByNavyAndYear("kure", 1944).Name);

        var d = Fleet.Get("dreadnought").Ship!.Design;
        var mine = d with { Outfit = new OutfitInput { Fitted = new FireLoadsInput { Quarters = 1000 }, PaintMjM2 = 0 } };
        var o = Outfits.Of(mine);
        Assert.Equal(1000, o.Fitted!.Quarters);
        Assert.Equal(Outfits.Default(mine).Value.Fitted!.Cabins, o.Fitted.Cabins);   // the rest the template's
        Assert.Equal(0, o.PaintMjM2);
        Assert.NotEmpty(Outfits.Validate(d with { Outfit = new OutfitInput { Growth = -1 } }));
        Assert.Empty(Outfits.Validate(d));
    }

    [Fact]
    public void A_fire_lit_on_a_ship_grows_flashes_and_shows_on_its_cell()
    {
        var c = Class("dreadnought");
        var k = new ShipCondition();
        k.Reset(c);
        var f = new FloodModel(c);
        var quiet = Settings with { FireHarmS = [], FireMagazineS = [1e12, 1e12, 1e12, 1e12, 1e12], FireOpenPathPerMin = 0,
            FireSteelPerMin = [0, 0, 0], FireDeckPerMin = 0, FireReflashPerMin = 0 };
        k.Readiness = Settings.Readiness[1];   // prepared: a door a cell
        int cell = Enumerable.Range(0, c.Cells.Length).First(i => c.FireSpaces[i].Use == FireUse.Quarters && c.FireSpaces[i].AreaM2 > 20);
        int bunker = Enumerable.Range(0, c.Cells.Length).FirstOrDefault(i => c.FireSpaces[i].Use == FireUse.None, -1);
        Assert.True(bunker < 0 || !Fires.Ignite(c, k, bunker, 0.3, quiet), "a space with nothing to burn doesn't light");
        Assert.True(Fires.Ignite(c, k, cell, 0.3, quiet));
        Assert.False(Fires.Ignite(c, k, cell, 0.3, quiet), "lit already: its seat adds to it");
        Assert.True(k.CellFire[cell]);
        Assert.Equal(1, k.Fires);
        var r = RandomStream.FromSeed(7);
        int flashed = 0;
        for (int s = 0; s < 20 * 60; s++)
            flashed += Fires.Step(c, f, k, 1, quiet, ref r).Flashed;
        output.WriteLine($"{c.CellId(cell)} ({c.FireSpaces[cell].AreaM2:0} m², {c.FireSpaces[cell].LoadMJm2:0} MJ/m²): {k.Fire[cell].Phase}, " +
                         $"{k.Fire[cell].Q:0.00} MW, {k.Fire[cell].Involved:0} m² after 20 min");
        Assert.Equal(1, flashed);
        Assert.Equal(BurnPhase.Developed, k.Fire[cell].Phase);
        Assert.True(k.CellFire[cell]);

        // a part burns as a space of its own, shown by its flag
        int part = Enumerable.Range(0, c.Components.Length).First(p => c.Components[p].Kind == ComponentKind.Superstructure && c.FireOfPart(p) >= 0);
        Assert.True(Fires.Ignite(c, k, c.FireOfPart(part), 0.3, quiet));
        Assert.True((k.Flags[part] & PartFlags.Burning) != 0);
        Assert.Equal(2, k.Fires);
    }
}
