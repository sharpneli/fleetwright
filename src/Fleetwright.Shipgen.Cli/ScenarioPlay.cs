using System.Diagnostics;
using Fleetwright.Sim;
using Fleetwright.Sim.Ai;
using Fleetwright.Sim.Combat;
using Fleetwright.Sim.Core;
using Fleetwright.Sim.Missions;
using Fleetwright.Sim.Ships;

namespace Fleetwright.Shipgen.Cli;

/// <summary><c>shipgen scenario X --play [--seeds N]</c> (docs/battle/command.md step 5.0): a scenario played to its end
/// with every side the AI's, headless, as fast as it goes. One seed prints its timeline and result; several print a line
/// each and the spread. The clock: guns are silent in the coarse tier until coarse fire (fighting.md 4.9), so while any
/// encounter is open the asked speed is the fine tier's (<paramref name="fine"/>, 1x: an encounter opening drops to it at
/// the event, by the interest rule for others' encounters), and <paramref name="fast"/> otherwise. The speed asked only
/// picks the tiers here; the run goes at full tilt either way.</summary>
static class ScenarioPlay
{
    public static int Run(string path, string settingsPath, string landPath, string designsPath, int threads, bool land,
        int seeds, double fast, double fine)
    {
        var settings = SimSettings.Load(settingsPath);
        var s = Scenario.Load(path);
        Console.WriteLine($"{s.Name}: {s.ShipCount} ships, {s.Sides.Length} sides, from {s.Start}" +
                          $"{(s.Until is { } u ? $" until {u}" : "")}; played by the AI on every side");
        var shoreTask = land ? Task.Run(() => Shore.Build(Land.Load(landPath))) : Task.FromResult<Shore>(null!);
        using var registry = new ShipClassRegistry(threads);
        var classes = ScenarioLoader.RequestClasses(s, registry, [designsPath]);
        registry.WaitAll();
        foreach (var (name, id) in classes)
            if (registry.ErrorOf(id) is { } err)
                Console.WriteLine($"  {name}: {err}");
        var shore = shoreTask.Result;

        var runs = new List<(ulong Seed, ScenarioEnd End, SideResult[] Result, double Wall)>();
        var all = new MainGuns();
        int crashed = 0;
        for (int i = 0; i < seeds; i++)
        {
            ulong seed = s.Seed + (ulong)i;
            var w = new World(s.Start, seed, settings, registry, land ? shore : null);
            var ai = new AiSystem(w);
            var start = ScenarioLoader.Apply(s, w, ai, classes);
            foreach (var x in start.Warnings)
                Console.WriteLine($"  {x}");
            var names = new Dictionary<int, string>(start.ShipNames);
            var store = w.Ships.Store;
            for (int slot = 0; slot < store.Slots; slot++)
                if (store.IsAlive(slot) && !names.ContainsKey(store[slot].Entity))
                    names[store[slot].Entity] = $"{store[slot].Handling.Class.Name} #{store[slot].Entity}";

            w.Apply(Command.SetPlayerSide(-1));   // watching: every side the AI's
            w.Apply(Command.SetInterest(InterestKind.Others, InterestAction.Slow, fine));   // an encounter opening drops to the fine tier at once
            var end = new ScenarioEnd(s, w, ai);
            var guns = new MainGuns();
            int seen = 0;
            var sw = Stopwatch.StartNew();
            try
            {
                while (!end.Check())
                {
                    double want = w.Encounters.Count > 0 ? fine : fast;
                    if (w.Speed != want)
                        w.Apply(Command.SetSpeed(want));
                    w.AdvanceTo(w.Time + 60_000);
                    guns.Read(w.CombatLog);
                    // a ship falling out for her main guns: why they're out, as she falls out
                    for (; seen < end.Timeline.Count; seen++)
                        if (end.Timeline[seen] is { Kind: PlayEvent.FellOut } l && (Fleetwright.Sim.Ai.Captains.CaptainReason)l.Count ==
                            Fleetwright.Sim.Ai.Captains.CaptainReason.OutGuns && w.Ships.Store.SlotOf(l.Entity) is int fs and >= 0)
                            guns.FellOut.Add(w.Ships.Store.Combat.Condition(fs));
                }
                for (int slot = 0; slot < w.Ships.Store.Slots; slot++)
                    if (w.Ships.Store.IsAlive(slot))
                        guns.End.Add(w.Ships.Store.Combat.Condition(slot));
            }
            catch (Exception x) when (x is ArgumentException or InvalidOperationException or IndexOutOfRangeException or NullReferenceException)
            {
                // the sim broke: a finding of the bench, told in full, and the other seeds still run
                Console.WriteLine($"  seed {seed}: THE SIM BROKE at {w.Time} (asked {w.Speed:G}x): {x}");
                crashed++;
                continue;
            }
            double wall = sw.Elapsed.TotalSeconds;
            var result = end.Result();
            runs.Add((seed, end, result, wall));
            if (seeds == 1)
            {
                PrintOne(s, end, result, names, wall, w, ai);
                guns.Print("  ");
            }
            else
                Console.WriteLine($"  seed {seed,6}: {Why(end.Reason),-7} at {Clock(s, end.EndMs)}  " +
                                  string.Join("   ", result.Select(r => $"{r.Name} lost {r.Lost}/{r.Ships} ({r.LostTonnes:0} t){(r.AimsMet ? ", aims met" : "")}")) +
                                  Causes(end) + $"   ({wall:F1} s)" + (end.Missed > 0 ? $"   {end.Missed} log lines missed" : ""));
            if (seeds > 1)
            {
                guns.Print("           ");
                if (end.Reason != EndReason.Done)
                    DoneLines(s, w, ai, "           ");
            }
            all.FellOut.Merge(guns.FellOut);
            all.End.Merge(guns.End);
            (all.HitBlackouts, all.NearBlackouts, all.ShipsBlacked) = (all.HitBlackouts + guns.HitBlackouts, all.NearBlackouts + guns.NearBlackouts,
                all.ShipsBlacked + guns.ShipsBlacked);
        }
        if (seeds > 1 && runs.Count > 0)
        {
            PrintSpread(s, runs.Select(r => (r.End, r.Result)).ToList());
            Console.WriteLine($"over {runs.Count} seeds:");
            all.Print("  ");
        }
        if (crashed > 0)
            Console.WriteLine($"the sim broke in {crashed} of {seeds} seeds");
        return crashed > 0 ? 1 : 0;
    }

    /// <summary>Why ships' main guns are out (the user's question, 2026-10-20: "a lot of ships with disabled main guns"):
    /// their chief batteries' mounts not working, each by its first cause (the magazine, the gunhouse, the barbette, a jam,
    /// its guns), at the moment a ship falls out for her main guns and at the end of the run for every ship afloat.</summary>
    sealed class MainGuns
    {
        public readonly Tally FellOut = new(), End = new();
        /// <summary>The ship-wide blackouts (E4's cascade, E7's near misses), and the ships blacked out at least once.</summary>
        public int HitBlackouts, NearBlackouts, ShipsBlacked;
        readonly HashSet<int> blacked = [];
        long read;

        public void Read(CombatLog log)
        {
            for (long i = Math.Min(log.Written - read, log.Count) - 1; i >= 0; i--)
            {
                var e = log.Newest((int)i);
                if (e.Kind is not (CombatEvent.Hit or CombatEvent.NearMiss) || (e.Effect & HitEffect.Blackout) == 0)
                    continue;
                if (e.Kind == CombatEvent.NearMiss)
                    NearBlackouts++;
                else
                    HitBlackouts++;
                ShipsBlacked += blacked.Add(e.Target) ? 1 : 0;
            }
            read = log.Written;
        }

        public void Print(string indent)
        {
            Console.WriteLine($"{indent}main guns: {FellOut.Ships} fell out for them, " + FellOut.Text());
            Console.WriteLine($"{indent}           at the end, {End.Ships} ships afloat: " + End.Text());
            Console.WriteLine($"{indent}blackouts: {HitBlackouts + NearBlackouts} ship-wide ({HitBlackouts} by a hit's shock, {NearBlackouts} by near misses), " +
                              $"{ShipsBlacked} ships blacked out");
        }

        public sealed class Tally
        {
            public static readonly string[] Names = ["magazine flooded by order", "magazine flooded by the sea", "magazine spent (its race)",
                "magazine put out by a hit", "gunhouse destroyed", "gunhouse out of action", "barbette out", "jammed", "guns out",
                "down: a crew stunned", "down: a hit's shock round it", "down: a hit's shock through the ship", "down: a near miss round it",
                "down: a near miss through the ship", "down: her own guns' breakers", "down: feed", "down: smoke", "down: unknown"];
            public readonly int[] By = new int[Names.Length];
            public int Ships, Mounts, Barrels, Out, OutBarrels;

            public void Merge(Tally o)
            {
                for (int i = 0; i < By.Length; i++)
                    By[i] += o.By[i];
                (Ships, Mounts, Barrels, Out, OutBarrels) = (Ships + o.Ships, Mounts + o.Mounts, Barrels + o.Barrels, Out + o.Out, OutBarrels + o.OutBarrels);
            }

            public void Add(ShipCondition k)
            {
                var c = k.Class;
                Ships++;
                foreach (ref readonly var b in c.Batteries)
                {
                    if (!b.Chief)
                        continue;
                    foreach (int m in c.Links(b.Mounts))
                    {
                        ref readonly var mr = ref c.Mounts[m];
                        int n = c.TurretTypes[mr.Type].Barrels;
                        (Mounts, Barrels) = (Mounts + 1, Barrels + n);
                        if (k.MountWorks[m])
                            continue;
                        (Out, OutBarrels) = (Out + 1, OutBarrels + n);
                        By[Cause(c, k, m)]++;
                    }
                }
            }

            static int Cause(ShipClass c, ShipCondition k, int m)
            {
                ref readonly var mr = ref c.Mounts[m];
                if (mr.Magazine >= 0 && c.RoomNode(mr.Magazine) is int mag && k.Health(mag) <= 0)
                    return k.MagazineFlood[mr.Magazine] ? 0 : k.Drowned[mag] ? 1 : k.State[mag] == PartState.Destroyed ? 2 : 3;
                if (k.Health(mr.Component) <= 0)
                    return k.Has(mr.Component, PartFlags.Down) ? Down(k.DownWhy[mr.Component]) : k.State[mr.Component] == PartState.Destroyed ? 4 : 5;
                if (mr.Barbette >= 0 && k.Health(mr.Barbette) <= 0)
                    return k.Has(mr.Barbette, PartFlags.Down) ? Down(k.DownWhy[mr.Barbette]) : 6;
                return k.Has(mr.Component, PartFlags.Jammed) ? 7 : 8;
            }

            static int Down(DownCause why) => why == DownCause.None ? 17 : 8 + (int)why;

            public string Text()
            {
                if (Mounts == 0)
                    return "no main mounts";
                var parts = Enumerable.Range(0, By.Length).Where(i => By[i] > 0).OrderByDescending(i => By[i]).Select(i => $"{Names[i]} {By[i]}");
                return $"main mounts out {Out} of {Mounts} ({OutBarrels} of {Barrels} guns)" + (Out > 0 ? ": " + string.Join(", ", parts) : "");
            }
        }
    }

    /// <summary>A seed's losses by how they went (a magazine, capsized, broke in two, foundered), for its one line.</summary>
    static string Causes(ScenarioEnd end)
    {
        var lost = end.Timeline.Where(l => l.Kind == PlayEvent.Lost).GroupBy(l => (LossKind)l.Count).OrderBy(g => g.Key)
            .Select(g => $"{g.Key switch { LossKind.Magazine => "magazine", LossKind.Capsized => "capsized", LossKind.BrokeInTwo => "broke in two", _ => "foundered" }} {g.Count()}")
            .ToList();
        return lost.Count > 0 ? "   (" + string.Join(", ", lost) + ")" : "";
    }

    static void PrintOne(Scenario s, ScenarioEnd end, SideResult[] result, Dictionary<int, string> names, double wall, World w, AiSystem ai)
    {
        string Name(int e) => names.TryGetValue(e, out var n) ? n : $"#{e}";
        string SideName(int side) => side >= 0 && side < s.Sides.Length ? s.Sides[side] : "?";
        string UnitName(int id) => id < 0 ? end.GroupName(-id) : Name(id);
        Console.WriteLine();
        Console.WriteLine("the timeline:");
        foreach (var l in end.Timeline.OrderBy(l => l.Ms).ThenBy(l => l.Kind))
            Console.WriteLine($"  {Clock(s, l.Ms)}  " + l.Kind switch
            {
                PlayEvent.Met => "the sides meet (the first encounter opens)",
                PlayEvent.Sighted => $"{SideName(l.Side)} sights {Name(l.Entity)}",
                PlayEvent.Fired => $"{SideName(l.Side)} opens fire: {Name(l.Entity)} at {Name(l.Other)}",
                PlayEvent.Hit => $"{SideName(l.Side)}'s first hit: {Name(l.Entity)} on {Name(l.Other)}",
                PlayEvent.Torpedoes => $"{SideName(l.Side)}'s first torpedoes: {Name(l.Entity)}, {l.Count} at {Name(l.Other)}",
                PlayEvent.Lost => $"LOST: {SideName(l.Side)}'s {Name(l.Entity)}, " + (LossKind)l.Count switch
                {
                    LossKind.Magazine => "her magazine set off",
                    LossKind.Capsized => "capsized",
                    LossKind.BrokeInTwo => "broke in two",
                    _ => "foundered",
                } + (l.Other != 0 ? $" (last hit by {Name(l.Other)}{(end.SideOf(l.Other) == l.Side ? ", own fire" : "")})" : ""),
                PlayEvent.FellOut => $"{SideName(l.Side)}'s {Name(l.Entity)} falls out: " + (Fleetwright.Sim.Ai.Captains.CaptainReason)l.Count switch
                {
                    Fleetwright.Sim.Ai.Captains.CaptainReason.OutSpeed => "too slow",
                    Fleetwright.Sim.Ai.Captains.CaptainReason.OutGuns => "her main guns out",
                    Fleetwright.Sim.Ai.Captains.CaptainReason.OutList => "listing",
                    Fleetwright.Sim.Ai.Captains.CaptainReason.OutWater => "flooding",
                    Fleetwright.Sim.Ai.Captains.CaptainReason.OutAmmunition => "her magazines empty",
                    _ => "her steering gone",
                },
                PlayEvent.Deployed => $"{SideName(l.Side)}'s {end.GroupName(l.Entity)} deploys into line of battle",
                PlayEvent.Task => $"{SideName(l.Side)}'s {(l.Entity < 0 ? end.GroupName(-l.Entity) : Name(l.Entity))}: " +
                                  $"{((Fleetwright.Sim.Ai.Tasks.TaskKind)(l.Other / 256)).ToString().ToLowerInvariant()} " +
                                  (Fleetwright.Sim.Ai.Tasks.TaskState)l.Count switch
                                  {
                                      Fleetwright.Sim.Ai.Tasks.TaskState.InContact => "in contact",
                                      Fleetwright.Sim.Ai.Tasks.TaskState.Done => "done",
                                      _ => "can't",
                                  } + ((Fleetwright.Sim.Ai.Tasks.TaskReason)(l.Other % 256) is var why && why is not (Fleetwright.Sim.Ai.Tasks.TaskReason.None or Fleetwright.Sim.Ai.Tasks.TaskReason.InContact)
                                      ? $" ({why.ToString().ToLowerInvariant()})" : ""),
                PlayEvent.Phase => $"{SideName(l.Side)}'s commander: {Words(((Fleetwright.Sim.Ai.Commanders.CommanderPhase)l.Count).ToString())}" +
                                   ((Fleetwright.Sim.Ai.Commanders.CommanderReason)l.Other is var r && r != Fleetwright.Sim.Ai.Commanders.CommanderReason.None ? $" ({Words(r.ToString())})" : ""),
                PlayEvent.Ordered => $"{SideName(l.Side)}'s commander: {UnitName(l.Entity)} to " +
                                     $"{Words(((Fleetwright.Sim.Ai.Tasks.TaskKind)(l.Other / 256)).ToString())}" +
                                     (l.Count != 0 ? $" {UnitName(l.Count)}" : "") +
                                     $" ({Words(((Fleetwright.Sim.Ai.Commanders.CommanderWhy)(l.Other % 256)).ToString())})",
                PlayEvent.End => $"the end: {Why((EndReason)l.Count)}",
                _ => l.Kind.ToString(),
            });
        Console.WriteLine();
        Console.WriteLine($"the result ({Why(end.Reason)} at {Clock(s, end.EndMs)}, played in {wall:F1} s):");
        foreach (var r in result)
            Console.WriteLine($"  {r.Name,-12} {r.Ships,4} ships {r.Tonnes,9:0} t   lost {r.Lost,3} ({r.LostTonnes,7:0} t)   damaged afloat {r.Damaged,3}   " +
                              $"afloat {r.Afloat,3}   hits made {r.Hits,4}   torpedoes {r.Torpedoes,3}   aims {(r.AimsMet ? "met" : "not met")}");
        int own = end.Timeline.Count(l => l.Kind == PlayEvent.Lost && l.Other != 0 && end.SideOf(l.Other) == l.Side);
        if (own > 0)
            Console.WriteLine($"  {own} of the losses were last hit by their own side (shells and torpedoes hit whatever is in their way)");
        if (end.Missed > 0)
            Console.WriteLine($"  ({end.Missed} combat log lines went by unread: the ring is too small for a minute of this fight)");
        // every side's top units at the end (docs/battle/command.md 5.1d: nothing idle)
        for (int side = 0; side < s.Sides.Length; side++)
        {
            var (tasked, home, orders, idle, fallen) = ai.Commanders.Census(side);
            Console.WriteLine($"  {s.Sides[side],-12} at the end: {tasked} units on a task, {home} home, {orders} on their first orders, {idle} idle; {fallen} fallen out");
        }
        DoneLines(s, w, ai, "  ");
        // nobody played: nothing is held by the player, nor ever asked back (the AI's own detachments are Tasked)
        int held = 0, onTask = 0;
        for (int g = 0; g < w.Groups.Count; g++)
            for (int i = 0; i < w.Groups[g].Members.Count; i++)
                if (w.Groups[g].DetachmentAt(i) is Detachment.Nudged or Detachment.Pinned)
                    held++;
                else if (w.Groups[g].DetachmentAt(i) == Detachment.Tasked)
                    onTask++;
        Console.WriteLine($"  at the end {held} units held by the player, {onTask} out of their lines on tasks; {ai.Captains.Notices + ai.Admirals.Notices} notices raised");
        // what the chain of command cost (docs/battle/command.md 5.1d: the commanders cost little)
        double game = (end.EndMs - s.Start.Ms) / 1000.0;
        Console.WriteLine("the AI's layers (share of the run, calls a game second, each, longest):");
        for (int i = 0; i < w.Probes.Count; i++)
            if (w.Probes.Names[i].StartsWith("AI: ", StringComparison.Ordinal) && w.Probes.Calls[i] > 0)
            {
                double ms = Probes.Ms(w.Probes.Ticks[i]);
                Console.WriteLine($"  {w.Probes.Names[i],-24} {ms / 1000 / wall,7:P2}  {w.Probes.Calls[i] / game,7:F3}  {ms * 1000 / w.Probes.Calls[i],9:F1} µs   ({Probes.Ms(w.Probes.Max[i]):F2} ms)");
            }
    }

    /// <summary>What the "done" end waits on (command.md "The end"): each side's phase, and its ships afloat not yet home,
    /// those in an encounter (which keep it from done), stopped, and the farthest from home.</summary>
    static void DoneLines(Scenario s, World w, AiSystem ai, string indent)
    {
        var store = w.Ships.Store;
        for (int side = 0; side < s.Sides.Length; side++)
        {
            var (phase, reason) = ai.Commanders.PhaseOf(side);
            int afloat = 0, away = 0, inContact = 0, stopped = 0;
            double farthest = 0;
            for (int slot = 0; slot < store.Slots; slot++)
            {
                if (!store.IsAlive(slot) || store[slot].Side != side || !Fleetwright.Sim.Ai.Commanders.Aims.Afloat(store, slot))
                    continue;
                afloat++;
                double d = ai.HomeOf(side) is { } at ? Earth.Distance(store[slot].Position, at) : double.PositiveInfinity;
                if (d <= w.Settings.HomeRadiusM)
                    continue;
                away++;
                farthest = Math.Max(farthest, d);
                if (w.Encounters.Of(slot, store[slot].Entity) != 0)
                    inContact++;
                if (store[slot].Speed < 0.5)
                    stopped++;
            }
            Console.WriteLine($"{indent}{s.Sides[side],-12} done? {Words(phase.ToString())}" + (reason != 0 ? $" ({Words(reason.ToString())})" : "") +
                              $"; {afloat - away} of {afloat} afloat home, {away} not ({inContact} in an encounter, {stopped} stopped" +
                              (away > 0 ? $", the farthest {farthest / 1000:F0} km)" : ")"));
        }
    }

    static void PrintSpread(Scenario s, List<(ScenarioEnd End, SideResult[] Result)> runs)
    {
        Console.WriteLine();
        Console.WriteLine($"over {runs.Count} seeds:");
        foreach (var g in runs.GroupBy(r => r.End.Reason).OrderBy(g => g.Key))
            Console.WriteLine($"  ended {Why(g.Key),-7} {g.Count(),3} times, after {Hours(g.Average(r => r.End.EndMs - s.Start.Ms))} on average");
        for (int side = 0; side < s.Sides.Length; side++)
        {
            var lost = runs.Select(r => r.Result[side].Lost).ToList();
            var tonnes = runs.Select(r => r.Result[side].LostTonnes).ToList();
            Console.WriteLine($"  {s.Sides[side],-12} lost {lost.Average():F1} of {runs[0].Result[side].Ships} ships (from {lost.Min()} to {lost.Max()}), " +
                              $"{tonnes.Average():0} t; made {runs.Average(r => r.Result[side].Hits):F0} hits, fired {runs.Average(r => r.Result[side].Torpedoes):F1} torpedoes; aims met {runs.Count(r => r.Result[side].AimsMet)} times");
        }
    }

    /// <summary>An enum's name in words: "NoContact" as "no contact".</summary>
    static string Words(string name) => string.Concat(name.Select((ch, i) => char.IsUpper(ch) && i > 0 ? " " + char.ToLowerInvariant(ch) : char.ToLowerInvariant(ch).ToString()));

    static string Why(EndReason r) => r switch
    {
        EndReason.Time => "time",
        EndReason.Decided => "decided",
        EndReason.Quiet => "quiet",
        EndReason.Done => "done",
        _ => "not over",
    };

    static string Clock(Scenario s, long ms) => $"{new WorldTime(ms).ToString()[11..16]} (+{Hours(ms - s.Start.Ms)})";

    static string Hours(double ms) => $"{(int)(ms / 3_600_000)}:{(int)(ms / 60_000 % 60):00}";
}
