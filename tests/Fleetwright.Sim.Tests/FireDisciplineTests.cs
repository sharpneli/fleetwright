using Fleetwright.Sim.Combat;
using Fleetwright.Sim.Core;
using Fleetwright.Sim.Ships;
using Xunit.Abstractions;

namespace Fleetwright.Sim.Tests;

/// <summary>Fire discipline (docs/battle/fire-control.md F6): the spots' bracket in correction space, and a battery that
/// ranges with partial salvos, each waiting for the last one's fall, before it fires for effect.</summary>
public class FireDisciplineTests(ITestOutputHelper output)
{
    static readonly WorldTime Start = WorldTime.FromUtc(1916, 5, 31, 14);
    static readonly SimSettings Settings = SimSettings.Load(Path.Combine(Fleet.Repo, "Content", "Sim", "settings.json")) with { HitsDoDamage = false };
    const double Lat = 56.0, Lon = 4.0;

    [Fact]
    public void The_bracket_takes_spots_in_any_order()
    {
        // the RN's 400-200-100 is the bracket's halving: over at 0 steps down 366, a short there splits it
        var b = FireControl.Bracket.Open(366);
        Assert.Equal(0, b.Aim(0, 0));
        b.Spot(FireControl.Sign.Over, 0, 0, 0, 100, 92);
        Assert.Equal(-366, b.Aim(0, 0));
        b.Spot(FireControl.Sign.Short, -366, 0, 0, 100, 92);
        Assert.Equal(-183, b.Aim(0, 0));
        // an older salvo's spot that says what's already known changes nothing
        b.Spot(FireControl.Sign.Over, 0, 0, 0, 100, 92);
        Assert.Equal(-183, b.Aim(0, 0));
        b.Spot(FireControl.Sign.Short, -183, 0, 0, 100, 92);
        Assert.Equal(-91.5, b.Aim(0, 0));
        // never narrower than the floor: a short at the middle pushes the high bound out
        b.Spot(FireControl.Sign.Short, -91.5, 0, 0, 100, 92);
        Assert.Equal(92, b.HiAt(0, 0) - b.LoAt(0, 0), 6);
        // a spot against a bound drops it: open below the over, a step beyond
        b.Spot(FireControl.Sign.Over, -500, 0, 0, 100, 92);
        Assert.False(b.Closed);
        Assert.Equal(-866, b.Aim(0, 0));
        // a straddle sets it to the pattern round the salvo; then it widens at the drift
        b.Spot(FireControl.Sign.Straddle, -700, 0, 0, 200, 92);
        Assert.Equal((-800.0, -600.0), (b.LoAt(0, 0), b.HiAt(0, 0)));
        Assert.Equal((-830.0, -570.0), (b.LoAt(60_000, 0.5), b.HiAt(60_000, 0.5)));
        Assert.Equal(-700, b.Aim(60_000, 0.5));
    }

    [Fact]
    public void A_battery_ranges_with_partial_salvos_each_after_the_last_fall_then_fires_for_effect()
    {
        // Bismarck's starboard 150 mm (director, ~11 s reload) on a destroyer at 11 km: a flight of ~20 s, so firing at
        // the loading rate would put two salvos in the air before the first one's spot
        var bismarck = Fleet.Get("bismarck").Class!;
        int bat = -1;
        for (int k = 0; k < bismarck.Batteries.Length; k++)
            if (bismarck.Batteries[k].Side == Side.Starboard && bismarck.Batteries[k].CalibreMm > 120)
                bat = k;
        var reload = bismarck.Mounts[bismarck.Links(bismarck.Batteries[bat].Mounts)[0]].ReloadS;
        int ranging = 0, effect = 0, partial = 0;
        double shortestRanging = double.MaxValue, effectGaps = 0;
        for (int seed = 1; seed <= 6; seed++)
        {
            var w = new World(Start, (ulong)seed, Settings);
            int a = w.Ships.AddClass(bismarck), d = w.Ships.AddClass(Fleet.Get("destroyer").Class!);
            var p = Earth.Position(Lat, Lon);
            var f = Earth.Forward(p, 90);
            Earth.Advance(ref p, ref f, 11_000);
            var (tl, to) = Earth.LatLon(p);
            w.Apply(ShipSystem.Spawn(a, Lat, Lon, 0, 15, side: 0));
            w.Apply(ShipSystem.Spawn(d, tl, to, 0, 15, side: 1));
            var salvos = new List<GunnerySystem.SalvoReport>();
            w.Gunnery.Reported = salvos.Add;
            w.Apply(GunnerySystem.Engage(1, bat, 2));
            w.AdvanceTo(Start + 12 * 60_000);
            int guns = bismarck.Batteries[bat].Guns;
            for (int i = 0; i < salvos.Count; i++)
            {
                var s = salvos[i];
                if (s.Phase == FirePhase.Ranging)
                {
                    ranging++;
                    partial += s.Shells < guns ? 1 : 0;
                    Assert.True(s.Shells >= Math.Min(Settings.FireDisciplines[0].GunsMin, guns), $"a ranging salvo of {s.Shells}");
                    if (i > 0 && salvos[i - 1].Phase == FirePhase.Ranging)
                        shortestRanging = Math.Min(shortestRanging, (s.FiredMs - salvos[i - 1].FiredMs) / 1000.0);
                }
                else if (i > 0 && salvos[i - 1].Phase == FirePhase.Effect)
                {
                    effect++;
                    effectGaps += (s.FiredMs - salvos[i - 1].FiredMs) / 1000.0;
                }
            }
        }
        double effectGap = effectGaps / Math.Max(effect, 1);
        output.WriteLine($"{ranging} ranging salvos ({partial} partial), the closest {shortestRanging:F0} s apart; {effect} in fire for effect, " +
                         $"{effectGap:F1} s apart on average; the reload {reload:F1} s");
        Assert.True(ranging >= 6 && effect >= 30, "it ranged, then fired for effect");
        Assert.True(partial * 2 >= ranging, "ranging salvos are partial");
        // each ranging salvo waited for the last one's fall and spot: its flight and the spot's delay, well over a reload
        Assert.True(shortestRanging > reload + Settings.SpotDelayMs / 1000.0, $"ranging salvos {shortestRanging:F0} s apart");
        Assert.True(effectGap < 2 * reload, $"fire for effect at the loading rate: {effectGap:F1} s");
    }

    [Fact]
    public void The_german_ladder_brackets_in_one_flight_and_the_halves_alternate()
    {
        // Seydlitz (five twin turrets) on Lion at 16.5 km: three salvos stepped 400 yd apart go before the first falls;
        // firing for effect, three turrets and two in turn, half a reload apart
        var seydlitz = Fleet.Get("seydlitz").Class!;
        var w = new World(Start, 3, Settings);
        int a = w.Ships.AddClass(seydlitz), l = w.Ships.AddClass(Fleet.Get("lion").Class!);
        var p = Earth.Position(Lat, Lon);
        var f = Earth.Forward(p, 90);
        Earth.Advance(ref p, ref f, 16_500);
        var (tl, to) = Earth.LatLon(p);
        w.Apply(ShipSystem.Spawn(a, Lat, Lon, 0, 20, side: 0));
        w.Apply(ShipSystem.Spawn(l, tl, to, 0, 20, side: 1));
        int german = Array.FindIndex(Settings.FireDisciplines, d => d.Name == "german");
        w.Apply(GunnerySystem.SetDiscipline(0, german));
        var salvos = new List<GunnerySystem.SalvoReport>();
        w.Gunnery.Reported = salvos.Add;
        w.Apply(GunnerySystem.Engage(1, 0, 2));
        w.AdvanceTo(Start + 10 * 60_000);
        var dis = Settings.FireDisciplines[german];
        var first = salvos.OrderBy(s => s.FiredMs).Take(dis.InFlight).ToArray();
        output.WriteLine(string.Join("; ", first.Select(s => $"{(s.FiredMs - first[0].FiredMs) / 1000.0:F0} s step {s.LadderM:+0;-0} {s.Shells} guns")));
        Assert.Equal([-dis.LadderStepM, 0, dis.LadderStepM], first.Select(s => s.LadderM).Order().ToArray());
        Assert.True(first.All(s => s.Phase == FirePhase.Ranging && s.Shells >= dis.GunsMin && s.Shells < seydlitz.Batteries[0].Guns));
        // the first two go together, before the first one's fall (its flight is ~30 s)
        Assert.True(first[1].FiredMs - first[0].FiredMs < 10_000);

        var effect = salvos.Where(s => s.Phase == FirePhase.Effect).OrderBy(s => s.FiredMs).Take(12).ToArray();
        output.WriteLine("fire for effect: " + string.Join(", ", effect.Select(s => $"{s.Shells}")));
        Assert.True(effect.Length >= 8);
        int alternating = 0;
        for (int i = 1; i < effect.Length; i++)
            alternating += effect[i].Shells != effect[i - 1].Shells ? 1 : 0;
        Assert.True(alternating * 4 >= 3 * (effect.Length - 1), "the halves alternate");
    }
}
