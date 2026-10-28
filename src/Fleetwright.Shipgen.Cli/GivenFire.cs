using Fleetwright.Sim;
using Fleetwright.Sim.Combat;
using Fleetwright.Sim.Core;
using Fleetwright.Sim.Ships;

namespace Fleetwright.Shipgen.Cli;

/// <summary>Ships under a given fire (the history benches': <c>damage-history</c>, docs/battle/shells-armour-damage.md
/// E8, and <c>crew-history</c>, docs/battle/crew.md N0): runs of a ship of a case's kind at her speed, in one world, each
/// hit by the case's shells at random times over its minutes, each from a range and a target angle drawn from the case's
/// bands (the battle's, not one beam-on shot), through the world's damage system; the world's clock runs on between them
/// and for the case's hours, so her fires, flooding, parties and walking wounded go on as in a battle.</summary>
static class GivenFire
{
    /// <summary>A share of a case's hits: a shooter's battery's shell, by kind (or its first), or a loadout's; at the
    /// case's ranges, or its own (a secondary that can't reach the main's).</summary>
    public sealed record Fall(string Shooter, int Battery, string? Kind, double Share, string? Loadout = null, double RangeHi = 0);

    /// <summary>The fire: the target, her speed, the hits (n over the minutes, ranges and target angles drawn from the
    /// bands), her readiness and doctrine, the hours run; her side's men's experience and her work-up (docs/battle/crew.md
    /// decision 6).</summary>
    public sealed record Barrage(string Target, double Kn, Fall[] Falls, int Hits, double Minutes, (double Lo, double Hi) RangeM,
        (double Lo, double Hi) AspectDeg, string Readiness, string Doctrine, double Hours, double Experience = Sim.Core.Experience.Trained, double WorkUp = 1);

    /// <summary>A hit just struck: the run, the ship's slot and condition, when (ms after the start), where it struck, and
    /// her mounts working before it. The damage system's Last* hold what it did.</summary>
    public readonly record struct Struck(int Run, int Slot, long Ms, string Where, ShipCondition K, bool[] MountsBefore);

    static readonly double[] RangeSteps = [0, 0.25, 0.5, 0.75, 1];
    static readonly double[] AspectSteps = [0, 0.25, 0.5, 0.75, 1];

    /// <summary>The fire on <paramref name="runs"/> ships: <paramref name="spawned"/> with each run's entity as she's
    /// placed, <paramref name="hit"/> after each hit, <paramref name="minute"/> at the end of each minute (from 1) once the
    /// world has reached it.</summary>
    public static void Run(Barrage b, string name, ShipClass target, Func<string, ShipClass> cls, SimSettings settings, int runs,
        Action<int, int> spawned, Action<World, Struck> hit, Action<World, int> minute)
    {
        var s = settings with { ReadinessDefault = b.Readiness, DamageControlDefault = b.Doctrine };
        if (!s.Readiness.Any(x => x.Name == b.Readiness) || !s.DamageControl.Any(x => x.Name == b.Doctrine))
            throw new ArgumentException($"{name}: no readiness \"{b.Readiness}\" or doctrine \"{b.Doctrine}\"");
        // each fall's aims over the case's ranges and target angles
        var aims = b.Falls.Select(f =>
        {
            var sc = cls(f.Shooter);
            var (shell, _) = HitsBench.ShellOf(sc, f.Battery, f.Kind, f.Loadout);
            double maxElev = sc.Mounts[sc.Links(sc.Batteries[f.Battery].Mounts)[0]].MaxElevationDeg;
            double hi = f.RangeHi > 0 ? f.RangeHi : b.RangeM.Hi, lo = Math.Min(b.RangeM.Lo, hi);
            return (from r in RangeSteps
                    from a in AspectSteps
                    select new GivenHits.Aim(target, shell, maxElev, lo + r * (hi - lo),
                        b.AspectDeg.Lo + a * (b.AspectDeg.Hi - b.AspectDeg.Lo), s)).ToArray();
        }).ToArray();
        double total = b.Falls.Sum(f => f.Share);
        var start = WorldTime.FromUtc(1916, 5, 31, 14);
        var w = new World(start, 1, s);
        int ci = w.Ships.AddClass(target);
        if (b.Experience != Sim.Core.Experience.Trained)
            w.Apply(GunnerySystem.SetExperience(0, b.Experience));
        var rng = RandomStream.Named(1, "damage history");
        var entities = new int[runs];
        var due = new List<(long Ms, int Run, int Fall)>();
        for (int i = 0; i < runs; i++)
        {
            w.Apply(ShipSystem.Spawn(ci, 50 + 0.05 * (i % 20), -20 - 0.1 * (i / 20), 0, b.Kn));   // the open Atlantic, 5 km apart
            entities[i] = w.Ships.NewestEntity;
            if (b.WorkUp != 1)
                w.Apply(ShipSystem.WorkUp(entities[i], b.WorkUp));
            spawned(i, entities[i]);
            for (int h = 0; h < b.Hits; h++)
            {
                double u = rng.NextDouble() * total, acc = 0;
                int a = 0;
                for (; a < b.Falls.Length - 1; a++)
                    if ((acc += b.Falls[a].Share) > u)
                        break;
                due.Add(((long)(rng.NextDouble() * b.Minutes * 60_000), i, a));
            }
        }
        due.Sort();
        var path = new HitPath();
        int next = 0;
        var store = w.Ships.Store;
        for (int m = 1; m <= Math.Max(b.Hours * 60, b.Minutes); m++)
        {
            long end = m * 60_000L;
            for (; next < due.Count && due[next].Ms < end; next++)
            {
                var (ms, ri, fi) = due[next];
                w.AdvanceTo(start + ms);
                int slot = store.SlotOf(entities[ri]);
                if (slot < 0 || store.Combat.Condition(slot).Status != ShipStatus.Afloat)
                    continue;
                var k = store.Combat.Condition(slot);
                var mounts = (bool[])k.MountWorks.Clone();
                var aim = aims[fi][rng.NextInt(aims[fi].Length)];
                w.Damage.Strike(w, aim.Next(w, entities[ri], slot, path, ref rng, out string where));
                hit(w, new Struck(ri, slot, ms, where, k, mounts));
            }
            w.AdvanceTo(start + end);
            minute(w, m);
        }
    }
}
