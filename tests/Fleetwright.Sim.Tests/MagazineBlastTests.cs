using Fleetwright.Shipgen;
using Fleetwright.Sim.Combat;
using Fleetwright.Sim.Ships;
using Xunit.Abstractions;

namespace Fleetwright.Sim.Tests;

/// <summary>A magazine going up, as the screens draw it (docs/visuals/magazine-explosions.md X1): the correlations
/// against the research's table (research/vfx/magazine-explosion-vfx-research.md §2.2), the event on the cut's ships
/// against vidgen's <c>magazine.plan</c>, and the openings' sequence.</summary>
public class MagazineBlastTests(ITestOutputHelper output)
{
    static ShipClass Class(string name) => Fleet.Get(name).Class!;

    static IEnumerable<int> Magazines(ShipClass c) => Enumerable.Range(0, c.Rooms.Length).Where(r => c.Rooms[r].Kind == RoomKind.Magazine);

    [Theory]
    [InlineData(500, 46, 3.6, 22)]
    [InlineData(2_000, 73, 5.7, 34)]
    [InlineData(20_000, 157, 12.2, 73)]
    [InlineData(100_000, 269, 17.7, 125)]
    public void The_fireball_and_blast_follow_the_research_table(double kg, double d, double s, double lambda)
    {
        var (D, S) = MagazineBlast.Fireball(kg);
        Assert.Equal(d, D, 0.5);
        Assert.Equal(s, S, 0.1);
        Assert.Equal(lambda, MagazineBlast.BlastLength(kg), 0.6);
    }

    [Fact]
    public void Bismarcks_aft_magazine_comes_out_near_vidgens()
    {
        var c = Class("bismarck");
        foreach (int r in Magazines(c))
        {
            var e = MagazineBlast.Of(c, r);
            output.WriteLine($"{c.Rooms[r].X0,6:0.0}..{c.Rooms[r].X1,6:0.0}: {c.Rooms[r].Tonnes,6:0} t, M {e.MassKg / 1000:0.0} t, D {e.FireballM:0} m " +
                             $"{e.FireballS:0.0} s, λ {e.BlastLengthM:0} m, k {e.K:0.00}, mount {e.Mount}, {e.Openings.Length} openings, debris {e.Debris}");
        }
        // vidgen's Y (HANDOFF "Magazine explosions"): M 22 t, D 163 m for 12.6 s, λ 76 m; its propellant share is a fit
        // (23% for the 380/52), this the charge and shell rows (21%), so within 15%
        int y = Magazines(c).Where(r => c.Rooms[r].Tonnes > 100).MinBy(r => c.Rooms[r].X0);
        var aft = MagazineBlast.Of(c, y);
        // (from 0.75 since the magazines weigh the real shells, user 2026-10-16, docs/battle/shells-armour-damage.md S3: the
        // round's charge is still 0.6 of its shell (Batteries.AmmoMult), so a lighter shell took its propellant with it;
        // the ammunition's `charge` gives it as rounds x charge, and the band closes again)
        Assert.InRange(aft.MassKg, 0.75 * 22_000, 1.15 * 22_000);
        Assert.InRange(aft.FireballM, 140, 170);   // both follow M (from 150 and 70)
        Assert.InRange(aft.BlastLengthM, 65, 80);
        Assert.True(aft.Mount >= 0 && !double.IsNaN(aft.BarbetteR), "a turret over it, with its barbette");
    }

    [Fact]
    public void A_destroyers_magazine_is_small_and_every_class_with_one_builds()
    {
        foreach (var b in Fleet.Designs)
        {
            var c = b.Class!;
            foreach (int r in Magazines(c))
            {
                var e = MagazineBlast.Of(c, r);
                Assert.True(e.MassKg > 0 && double.IsFinite(e.FireballM), $"{c.Name} room {r}");
                Assert.InRange(e.DeckZ, 0.5, 20);
                Assert.True(e.Openings.Length >= 6, $"{c.Name} room {r}: {e.Openings.Length} openings");
            }
        }
        var dd = Class("destroyer");
        double most = Magazines(dd).Max(r => MagazineBlast.Of(dd, r).MassKg);
        output.WriteLine($"destroyer: M {most:0} kg, D {MagazineBlast.Fireball(most).D:0} m");
        Assert.InRange(most, 50, 2_000);
    }

    [Fact]
    public void The_openings_fail_in_the_prototypes_order_and_jet_until_the_pressure_is_spent()
    {
        var c = Class("bismarck");
        var e = MagazineBlast.Of(c, Magazines(c).MaxBy(r => c.Rooms[r].Tonnes));
        double Fail(OpeningKind k) => e.Openings.ToArray().Where(o => o.Kind == k).Min(o => o.FailS);
        Assert.True(Fail(OpeningKind.Port) < Fail(OpeningKind.Hood));
        Assert.True(Fail(OpeningKind.Hood) < Fail(OpeningKind.Hatch));
        Assert.True(Fail(OpeningKind.Hatch) < Fail(OpeningKind.Scuttle));
        Assert.True(Fail(OpeningKind.Scuttle) < Fail(OpeningKind.Barbette));
        Assert.True(Fail(OpeningKind.Barbette) < MagazineBlast.MainAtS);

        var port = e.Openings.ToArray().First(o => o.Kind == OpeningKind.Port);
        Assert.Equal(0, MagazineBlast.JetLength(port, port.FailS - 0.01));
        double peak = MagazineBlast.JetLength(port, MagazineBlast.MainAtS);
        output.WriteLine($"a port's jet at the main event: {peak:0} m (d {port.DiameterM:0.00} m)");
        Assert.InRange(peak, 30, 90);   // the prototype's 60-90 m port jets, Lion's "200 ft"
        Assert.Equal(0, MagazineBlast.JetLength(port, MagazineBlast.MainAtS + 3));
        var bar = e.Openings.ToArray().Single(o => o.Kind == OpeningKind.Barbette);
        Assert.Equal(120, MagazineBlast.JetLength(bar, MagazineBlast.MainAtS));   // capped by the column
    }
}
