using Fleetwright.Sim.Combat;
using Fleetwright.Sim.Core;
using Fleetwright.Sim.Ships;

namespace Fleetwright.Sim.Tests;

/// <summary>A ship down by the head (the Åland card, 2026-10-26; <see cref="Ploughing"/>): her screws drive by their discs
/// under the sea, her deck under the sea presses her down going ahead and drags, and damage control asks for steerage
/// way once her forecastle is under.</summary>
public class PloughingTests
{
    static readonly SimSettings Settings = SimSettings.Load(Path.Combine(Fleet.Repo, "Content", "Sim", "settings.json"));

    /// <summary>The Väinämöinen-like trimmed by the head until her screws are clear of the sea, sunk so her forecastle is
    /// under (the card's 9.3° and 14.3 m forward).</summary>
    static (ShipClass C, FloodModel F, Attitude A) Down()
    {
        var c = Fleet.Scenario("vainamoinen");
        return (c, new FloodModel(c), new Attitude(1.5, 10, 0));
    }

    [Fact]
    public void A_disc_is_half_under_a_surface_through_its_centre()
    {
        Assert.Equal(0.5, Ploughing.DiscUnder(0), 9);
        Assert.Equal(1, Ploughing.DiscUnder(1), 9);
        Assert.Equal(0, Ploughing.DiscUnder(-1), 9);
        Assert.Equal(1, Ploughing.DiscUnder(0.5) + Ploughing.DiscUnder(-0.5), 9);
    }

    [Fact]
    public void Her_screws_out_of_the_water_drive_nothing()
    {
        var (c, f, a) = Down();
        Assert.Equal(1, Ploughing.Screws(c, Waterplane.Level(c)));
        Assert.Equal(0, Ploughing.Screws(c, HitQuery.WaterOf(c, a)));
        var k = new ShipCondition();
        k.Reset(c);
        (k.Damaged, k.Attitude) = (true, a);
        Assert.Equal(0, Fitness.Of(c, k, f.ReserveT).SpeedShare);
    }

    [Fact]
    public void Her_deck_under_the_sea_presses_her_bow_down_ahead_and_drags_either_way()
    {
        var (c, f, a) = Down();
        var ahead = Ploughing.Deck(c, f, a, true, Settings);
        var astern = Ploughing.Deck(c, f, a, false, Settings);
        Assert.True(ahead.DownM2 > 0, $"pressed {ahead.DownM2:0.0} m²");
        Assert.True(ahead.X > c.Hydrostatics.Lcf, $"at x {ahead.X:0.0} m, the centre of flotation {c.Hydrostatics.Lcf:0.0}");
        Assert.True(ahead.DragM2 > 0 && astern.DragM2 > 0);
        Assert.True(astern.DownM2 < 0, "astern the flow over her deck lifts it");
        Assert.Equal(Ploughing.Forces.None, Ploughing.Deck(c, f, Attitude.Upright, true, Settings));
    }

    [Fact]
    public void Damage_control_asks_for_steerage_way_with_her_forecastle_under()
    {
        var (c, f, a) = Down();
        var k = new ShipCondition();
        k.Reset(c);
        k.Readiness = Settings.Readiness.Single(r => r.Name == "closed up");
        k.DamageControl = Settings.DamageControl.Single(d => d.Name == "1916");
        (k.Damaged, k.Attitude) = (true, a);
        var r = RandomStream.FromSeed(1);
        Repairs.Step(c, f, k, 1, 1000, 13 * 0.514444, Settings, ref r);
        Assert.Equal(Settings.SafeMinKn, k.SafeSpeedKn);
    }
}
