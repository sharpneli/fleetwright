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
            var sw = Stopwatch.StartNew();
            try
            {
                while (!end.Check())
                {
                    double want = w.Encounters.Count > 0 ? fine : fast;
                    if (w.Speed != want)
                        w.Apply(Command.SetSpeed(want));
                    w.AdvanceTo(w.Time + 60_000);
                }
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
                PrintOne(s, end, result, names, wall, w, ai);
            else
                Console.WriteLine($"  seed {seed,6}: {Why(end.Reason),-7} at {Clock(s, end.EndMs)}  " +
                                  string.Join("   ", result.Select(r => $"{r.Name} lost {r.Lost}/{r.Ships} ({r.LostTonnes:0} t){(r.AimsMet ? ", aims met" : "")}")) +
                                  $"   ({wall:F1} s)" + (end.Missed > 0 ? $"   {end.Missed} log lines missed" : ""));
        }
        if (seeds > 1 && runs.Count > 0)
            PrintSpread(s, runs.Select(r => (r.End, r.Result)).ToList());
        if (crashed > 0)
            Console.WriteLine($"the sim broke in {crashed} of {seeds} seeds");
        return crashed > 0 ? 1 : 0;
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
