using Fleetwright.Sim.Ai;
using Fleetwright.Sim.Ai.Admirals;
using Fleetwright.Sim.Core;
using Fleetwright.Sim.Missions;
using Xunit.Abstractions;

namespace Fleetwright.Sim.Tests;

/// <summary>The flotilla's attack (docs/battle/command.md 5.1b), in the flotilla scenario with the British line holding
/// its course (no turn-away): the boats gain a position ahead of the battleships' beam, run in together, fire their
/// spreads on one signal (in the water within two minutes: the tubes train first) and turn away together; with doctrine's "alone" they never attack
/// as a flotilla.</summary>
public class FlotillaAttackTests(ITestOutputHelper output)
{
    static readonly SimSettings Settings = SimSettings.Load(Path.Combine(Fleet.Repo, "Content", "Sim", "settings.json"));

    static (World W, AiSystem Ai, Scenario S) Load(TorpedoAttack attack, ulong? seed = null)
    {
        var s = Scenario.Load(Path.Combine(Fleet.Repo, "Content", "Scenarios", "flotilla.json"));
        var w = new World(s.Start, seed ?? s.Seed, Settings);
        var ai = new AiSystem(w);
        var classes = ScenarioLoader.RequestClasses(s, w.Ships.Registry, [Path.Combine(Fleet.Shipgen, "designs")]);
        ScenarioLoader.Apply(s, w, ai, classes);
        w.Apply(ai.Orders.SideDoctrine(0, DoctrineField.TurnAwayDeg, 0));
        w.Apply(ai.Orders.SideDoctrine(1, DoctrineField.TorpedoAttack, (double)attack));
        return (w, ai, s);
    }

    static int Flotilla(World w)
    {
        for (int k = 0; k < w.Groups.Count; k++)
            if (w.Groups[k].Name.Contains("Flotilla"))
                return w.Groups[k].GroupId;
        throw new InvalidOperationException();
    }

    [Fact]
    public void The_boats_gain_run_in_fire_together_and_turn_away()
    {
        // six boats against four battleships by day aren't worth an attack by their own judgement (docs/battle/command.md
        // 5.1g): ordered to, they make it, or about half the time they're beaten off first (docs/battle/bugs.md). Which seeds
        // get in moves with every change to the ships, so the test takes the first of a dozen where the signal is made, and
        // asks that two boats or more fire on it (their tubes trained out on the beam as they ran in, each track clear of
        // the boats steaming beside her) and the phases of it
        for (ulong seed = 1; seed <= 12; seed++)
        {
            var (w, ai, s) = Load(TorpedoAttack.Together, seed);
            int flotilla = Flotilla(w);
            w.Apply(ai.Orders.Attack(Unit.Group(flotilla)));
            var seen = new List<AttackPhase>();
            long signalMs = -1;
            long firedAtSignal = 0, firedMinuteOn = 0;
            for (int t = 10; t <= 30 * 60; t += 10)
            {
                w.AdvanceTo(s.Start + t * 1000L);
                var phase = ai.Admirals.AttackOf(flotilla).Phase;
                if (seen.Count == 0 || seen[^1] != phase)
                    seen.Add(phase);
                if (phase == AttackPhase.Fire && signalMs < 0)
                    (signalMs, firedAtSignal) = (w.Time.Ms, w.Torpedoes.Fired);
                if (signalMs >= 0 && w.Time.Ms - signalMs <= 120_000)
                    firedMinuteOn = w.Torpedoes.Fired;
            }
            output.WriteLine($"seed {seed}: phases {string.Join(" > ", seen)}; " + (signalMs < 0 ? "no signal" :
                $"at the signal {(signalMs - s.Start.Ms) / 60_000.0:F1} min in, {firedMinuteOn - firedAtSignal} torpedoes within two minutes (the tubes train first); {w.Torpedoes.Fired} in all"));
            if (signalMs < 0 || signalMs - s.Start.Ms > 25 * 60_000)
                continue;   // beaten off before the signal (or it's made too late to see it through)
            Assert.True(firedMinuteOn - firedAtSignal >= 2 * Settings.TorpedoSpread, "fewer than two boats fired on the signal");
            int gain = seen.IndexOf(AttackPhase.Gain), run = seen.IndexOf(AttackPhase.Run), fire = seen.IndexOf(AttackPhase.Fire), away = seen.IndexOf(AttackPhase.Away);
            Assert.True(gain >= 0 && gain < run && run < fire && fire < away, string.Join(" > ", seen));
            return;
        }
        Assert.Fail("in none of seeds 1-12 was the signal made");
    }

    [Fact]
    public void Alone_the_flotilla_never_attacks_as_one()
    {
        var (w, ai, s) = Load(TorpedoAttack.Alone);
        int flotilla = Flotilla(w);
        for (int t = 10; t <= 20 * 60; t += 10)
        {
            w.AdvanceTo(s.Start + t * 1000L);
            Assert.Equal(AttackPhase.None, ai.Admirals.AttackOf(flotilla).Phase);
        }
    }
}
