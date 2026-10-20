using Fleetwright.Sim.Combat;
using System.Diagnostics;
using Fleetwright.Sim;
using Fleetwright.Sim.Ai;
using Fleetwright.Sim.Core;
using Fleetwright.Sim.Missions;
using Fleetwright.Sim.Ships;

namespace Fleetwright.Shipgen.Cli;

/// <summary><c>shipgen scenario</c>: a scenario run headless at full tilt (docs/battle/plan.md step 2.7, the Jutland
/// test): how long its classes take to build (no art: that's the app's), the land and the placing, then game minutes
/// on the sim's own thread's work alone, with the ships on the fine tick and on rails, the speed it could run at, the
/// share of the sim thread a given speed would take, and what it allocated. With --replay, the command log is run again
/// on a fresh world and the hashes compared.</summary>
static class ScenarioRun
{
    public static int Run(string path, string settingsPath, string landPath, string designsPath, double minutes, int threads,
        int every, double atSpeed, bool land, bool replay, int[]? watch = null, double worldSpeed = 0, bool fireTrace = false)
    {
        var settings = SimSettings.Load(settingsPath);
        var total = Stopwatch.StartNew();
        var s = Scenario.Load(path);
        Console.WriteLine($"{s.Name}: {s.ShipCount} ships, {s.DesignNames().Count} designs, {s.Start}");

        // the land on a thread of its own, as the app does, while the classes build
        var shoreTask = land ? Task.Run(() => Shore.Build(Land.Load(landPath))) : Task.FromResult<Shore>(null!);

        var sw = Stopwatch.StartNew();
        using var registry = new ShipClassRegistry(threads);
        var classes = ScenarioLoader.RequestClasses(s, registry, [designsPath]);
        registry.WaitAll();
        double build = sw.Elapsed.TotalSeconds;
        int failed = classes.Values.Count(id => registry.Get(id) == null);
        Console.WriteLine($"classes: {classes.Count} built in {build:F2} s on {(threads > 0 ? threads : Math.Clamp(Environment.ProcessorCount / 2, 1, 4))} threads" +
                          (failed > 0 ? $", {failed} failed" : ""));
        foreach (var (name, id) in classes)
            if (registry.ErrorOf(id) is { } err)
                Console.WriteLine($"  {name}: {err}");

        sw.Restart();
        var shore = shoreTask.Result;
        double shoreWait = sw.Elapsed.TotalSeconds;
        sw.Restart();
        var w = new World(s.Start, s.Seed, settings, registry, land ? shore : null);
        var ai = new AiSystem(w);
        var start = ScenarioLoader.Apply(s, w, ai, classes);
        if (worldSpeed > 0)
            w.Apply(Command.SetSpeed(worldSpeed));   // the speed asked picks the tiers (coarse above 240x, snaps from 10,000x)
        double place = sw.Elapsed.TotalSeconds;
        Console.WriteLine($"land: {(land ? $"waited {shoreWait:F2} s more" : "none (a world of water)")}; placed {start.Ships} ships in " +
                          $"{start.Groups} groups in {place * 1000:F0} ms; ready {total.Elapsed.TotalSeconds:F2} s after the start");
        long tableBytes = 0;
        for (int i = 0; i < w.Shells.Ballistics.Count; i++)
            tableBytes += w.Shells.Ballistics[i].Bytes;
        Console.WriteLine($"ballistic tables: {w.Shells.Ballistics.Count}, {tableBytes / 1048576.0:F1} MB (built as the classes went to sea)");
        foreach (var x in start.Warnings)
            Console.WriteLine($"  {x}");

        // game minutes, each timed on this thread (the sim thread's work: no host, no snapshots); the probes from here
        var probes0 = new long[Probes.Capacity];
        var calls0 = new long[Probes.Capacity];
        Array.Copy(w.Probes.Ticks, probes0, Probes.Capacity);
        Array.Copy(w.Probes.Calls, calls0, Probes.Capacity);
        w.Probes.ResetMax();
        Console.WriteLine();
        Console.WriteLine("  game time   fine  rails   x real (this stretch)   allocated   gen0/1/2");
        var store = w.Ships.Store;
        var snap = new Snapshot();
        long alloc0 = GC.GetAllocatedBytesForCurrentThread();
        int g0 = GC.CollectionCount(0), g1 = GC.CollectionCount(1), g2 = GC.CollectionCount(2);
        double run = 0, worst = 0;
        var fight = new FightTally(w.CombatLog);
        var trace = fireTrace ? new FireTrace(start.ShipNames) : null;
        var stretch = Stopwatch.StartNew();
        long stretchMs = 0;
        for (int m = 1; m <= minutes; m++)
        {
            sw.Restart();
            if (trace != null)
                for (int k = 1; k <= 12; k++)
                {
                    w.AdvanceTo(w.Time + 5_000);
                    trace.Read(w);
                }
            else
                w.AdvanceTo(w.Time + 60_000);
            double t = sw.Elapsed.TotalSeconds;
            run += t;
            worst = Math.Max(worst, t);
            fight.Read();
            stretchMs += 60_000;
            if (m % every == 0 || m == (int)minutes)
            {
                double x = stretchMs / 1000.0 / stretch.Elapsed.TotalSeconds;
                Console.WriteLine($"  {w.Time.ToString()[11..19]}  {store.FineCount,5}  {store.Count - store.FineCount,5}   {x,12:F0}x            " +
                                  $"{(GC.GetAllocatedBytesForCurrentThread() - alloc0) / 1024.0,8:F0} KB   " +
                                  $"{GC.CollectionCount(0) - g0}/{GC.CollectionCount(1) - g1}/{GC.CollectionCount(2) - g2}");
                stretch.Restart();
                stretchMs = 0;
                if (fight.Any)
                    Console.WriteLine($"    fighting: {w.Gunnery.EngagedCount} batteries engaged, {w.Shells.InFlight} shells in flight, " +
                                      $"{fight.Shells} fired, {fight.Hits} hits");
                if (watch != null)
                    w.Fill(snap);
                foreach (int e in watch ?? [])
                {
                    int j = snap.Ships.IndexOf(e);
                    if (j >= 0)
                    {
                        var (sp, _) = snap.Ships.PoseAt(j, snap.Time.Ms);
                        var (sla, slo) = Earth.LatLon(sp);
                        Console.WriteLine($"    #{e} in the snapshot: {sla:F4}, {slo:F4} (pose of {snap.Ships.PoseMs[j] - snap.Time.Ms} ms)");
                    }
                    int k = store.SlotOf(e);
                    if (k < 0)
                    {
                        Console.WriteLine($"    #{e}: gone");
                        continue;
                    }
                    var sh = store[k];
                    var (la, lo) = Earth.LatLon(sh.Position);
                    var g = w.Groups.Of(e);
                    Console.WriteLine($"    #{e} {start.ShipNames.GetValueOrDefault(e)}: {la:F4}, {lo:F4}, heading {sh.HeadingDeg:F1}, " +
                                      $"{sh.Speed / Fleetwright.Sim.Combat.Handling.MsPerKnot:F1} kn, helm {sh.Helm} {sh.CourseOrderDeg:F0}° {sh.SpeedOrderKn:F1} kn, " +
                                      $"{sh.Tier.ToString().ToLowerInvariant()}, {g?.Name ?? "no group"}");
                }
            }
        }
        double game = minutes * 60;
        double speed = game / run;
        Console.WriteLine();
        Console.WriteLine($"{minutes:F0} game minutes in {run:F2} s: {speed:F0}x at full tilt, the slowest minute {60 / worst:F0}x; " +
                          $"{run / (game * 1000 / settings.ShipTickMs) * 1e6:F1} µs a fine tick");
        Console.WriteLine($"the world's state hash at {w.Time}: {w.ComputeHash():X16}");
        fight.Print(w, start.ShipNames);
        Console.WriteLine($"at {atSpeed:G}x the sim thread is {atSpeed / speed:P1} busy (the target: at most 25% at 10x, docs/battle/plan.md 2.7)");
        Console.WriteLine("where the time went (share of the run, longest call):");
        for (int i = 0; i < w.Probes.Count; i++)
        {
            double ms = Probes.Ms(w.Probes.Ticks[i] - probes0[i]);
            if (ms > 0)
                Console.WriteLine($"  {w.Probes.Names[i],-26} {ms / 1000 / run,7:P1}  {ms / 1000,7:F2} s  {(w.Probes.Calls[i] - calls0[i]) / game,9:F1} calls a game second, " +
                                  $"{ms * 1000 / Math.Max(w.Probes.Calls[i] - calls0[i], 1),7:F2} µs each   (longest {Probes.Ms(w.Probes.Max[i]):F2} ms)");
        }

        if (replay)
        {
            sw.Restart();
            var r = new World(s.Start, s.Seed, settings, registry, land ? shore : null);
            _ = new AiSystem(r);
            foreach (var c in w.CommandLog)
            {
                r.AdvanceTo(c.At);
                r.Apply(c.Command);
            }
            r.AdvanceTo(w.Time);
            bool same = r.ComputeHash() == w.ComputeHash();
            Console.WriteLine($"replay of {w.CommandLog.Count} commands: {(same ? "the same hash" : "A DIFFERENT HASH")} ({sw.Elapsed.TotalSeconds:F2} s)");
            if (!same)
                return 1;
        }
        return failed > 0 ? 1 : 0;
    }
}

/// <summary>--fire-trace: each battery's target, state and fire-discipline phase as they change (read every 5 game
/// seconds), with the range and the time since its engagement began, and its first salvo, first straddle and fire for
/// effect: how long a new target takes to come under fire.</summary>
sealed class FireTrace(IReadOnlyDictionary<int, string> names)
{
    readonly Dictionary<(int Shooter, int Battery), (int Target, BatteryState State, FirePhase Phase, int Salvos, long Since, bool Straddled)> was = [];

    public void Read(World w)
    {
        var store = w.Ships.Store;
        string Name(int e) => names.TryGetValue(e, out var n) ? n : $"#{e}";
        string Clock(long ms) => new WorldTime(ms).ToString()[11..19];
        long now = w.Time.Ms;
        for (int slot = 0; slot < store.Slots; slot++)
        {
            if (!store.IsAlive(slot))
                continue;
            var own = store[slot];
            var cls = own.Handling.Class;
            for (int b = 0; b < cls.Batteries.Length; b++)
            {
                var key = (own.Entity, b);
                int target = store.Combat.Target(slot, b);
                var t = target != 0 ? w.Gunnery.TallyOf(own.Entity, b) : null;
                bool had = was.TryGetValue(key, out var p);
                if (target == 0)
                {
                    if (had && p.Target != 0)
                        Console.WriteLine($"    {Clock(now)} {Name(own.Entity),-16} {cls.Batteries[b].Label,-10} ceases on {Name(p.Target)}");
                    was.Remove(key);
                    continue;
                }
                var st = t?.State ?? BatteryState.NoContact;
                var ph = t?.Phase ?? FirePhase.Opening;
                int salvos = t?.Salvos ?? 0;
                bool straddled = (t?.Straddles ?? 0) > 0;
                long since = had && p.Target == target ? p.Since : now;
                if (had && p.Target == target && p.State == st && p.Phase == ph && (p.Salvos > 0) == (salvos > 0) && p.Straddled == straddled)
                    continue;
                int ts = store.SlotOf(target);
                double r = ts >= 0 ? Earth.Distance(own.Position, store[ts].Position) : double.NaN;
                string what = !had || p.Target != target ? (had && p.Target != 0 ? $"SWITCHES from {Name(p.Target)} to" : "engages") : "on";
                string tag = had && p.Target == target && (p.Salvos > 0) != (salvos > 0) ? " first salvo" : "";
                tag += had && p.Target == target && straddled && !p.Straddled ? " first straddle" : "";
                Console.WriteLine($"    {Clock(now)} {Name(own.Entity),-16} {cls.Batteries[b].Label,-10} {what} {Name(target)} at {r / 1000:F1} km: " +
                                  $"{st} {ph}, {salvos} salvos{tag} (+{(now - since) / 1000} s)");
                was[key] = (target, st, ph, salvos, since, straddled);
            }
        }
    }
}

/// <summary>What the combat log said over a run, by shooter, battery and target: salvos, shells, straddles, hits, the
/// first salvo and the first hit (the log is a ring: it's read every game minute, so nothing is lost below its size).</summary>
sealed class FightTally(CombatLog log)
{
    sealed class Line
    {
        public int Salvos, Shells, Straddles, Hits;
        public long FirstMs = long.MaxValue, FirstHitMs = long.MaxValue;
    }

    readonly SortedDictionary<(int Shooter, int Battery, int Target), Line> lines = [];
    readonly List<CombatEntry> losses = [];
    /// <summary>The near misses that did something (E7), by target: how many, and of them those that holed her, put gear
    /// out, blacked her out and harmed a part.</summary>
    record struct Near(int Count, int Holed, int Shock, int Blackout, int Parts);
    readonly SortedDictionary<int, Near> near = [];
    /// <summary>Damage control's events by ship (docs/battle/damage-control.md DC8): boundaries that gave way, the steering
    /// it went to last, parts repaired, rooms brought back, sides whose counterflood tanks ran out, the lowest speed asked.</summary>
    record struct Dc(int Gave, Steering Steering, int Repaired, int Restored, int CounterOut, int SafeKn);
    readonly SortedDictionary<int, Dc> dc = [];
    long read;

    public bool Any => lines.Count > 0;
    public int Shells => lines.Values.Sum(l => l.Shells);
    public int Hits => lines.Values.Sum(l => l.Hits);

    public void Read()
    {
        long n = Math.Min(log.Written - read, log.Count);
        for (long i = n - 1; i >= 0; i--)
        {
            var e = log.Newest((int)i);
            if (e.Kind == CombatEvent.DamageControl)
            {
                var d = dc.GetValueOrDefault(e.Target, new Dc(0, Steering.Free, 0, 0, 0, int.MaxValue));
                d = (DcEvent)e.Count switch
                {
                    DcEvent.Gave => d with { Gave = d.Gave + e.Battery },
                    DcEvent.Steering => d with { Steering = (Steering)e.Battery },
                    DcEvent.Repaired => d with { Repaired = d.Repaired + 1 },
                    DcEvent.Restored => d with { Restored = d.Restored + 1 },
                    DcEvent.CounterfloodOut => d with { CounterOut = d.CounterOut + 1 },
                    _ => d with { SafeKn = Math.Min(d.SafeKn, e.Battery) },
                };
                dc[e.Target] = d;
                continue;
            }
            int battery = e.Source == HitSource.Torpedo ? -1 : e.Battery;   // a ship's torpedoes on one line, its launchers together
            if (!lines.TryGetValue((e.Shooter, battery, e.Target), out var l))
                lines[(e.Shooter, battery, e.Target)] = l = new Line();
            switch (e.Kind)
            {
                case CombatEvent.Torpedoes:
                    (l.Salvos, l.Shells, l.FirstMs) = (l.Salvos + 1, l.Shells + e.Count, Math.Min(l.FirstMs, e.Ms));
                    break;
                case CombatEvent.Fired:
                    (l.Salvos, l.Shells, l.FirstMs) = (l.Salvos + 1, l.Shells + e.Count, Math.Min(l.FirstMs, e.Ms));
                    break;
                case CombatEvent.Hit:
                    (l.Hits, l.FirstHitMs) = (l.Hits + 1, Math.Min(l.FirstHitMs, e.Ms));
                    break;
                case CombatEvent.Straddle:
                    l.Straddles++;
                    break;
                case CombatEvent.NearMiss:
                    // a near miss that did something (E7): what it did, by its target
                    var nm = near.GetValueOrDefault(e.Target);
                    nm.Count++;
                    nm.Holed += (e.Effect & HitEffect.Holed) != 0 ? 1 : 0;
                    nm.Shock += (e.Effect & HitEffect.Shock) != 0 ? 1 : 0;
                    nm.Blackout += (e.Effect & HitEffect.Blackout) != 0 ? 1 : 0;
                    nm.Parts += (e.Effect & (HitEffect.Destroyed | HitEffect.Disabled | HitEffect.Damaged)) != 0 ? 1 : 0;
                    near[e.Target] = nm;
                    break;
                case CombatEvent.Lost:
                    losses.Add(e);
                    break;
            }
        }
        read = log.Written;
    }

    public void Print(World w, IReadOnlyDictionary<int, string> names)
    {
        if (!Any)
            return;
        string Name(int e) => names.TryGetValue(e, out var n) ? n : $"#{e}";
        Console.WriteLine("the fight (shooter, battery: target, salvos, shells, straddled salvos, hits, first salvo and hit):");
        foreach (var ((shooter, battery, target), l) in lines)
        {
            int slot = w.Ships.Store.SlotOf(shooter);
            string gun = battery < 0 ? "torpedoes" : slot >= 0 && battery < w.Ships.Store[slot].Handling.Class.Batteries.Length
                ? w.Ships.Store[slot].Handling.Class.Batteries[battery].Label
                : $"battery {battery}";
            string Clock(long ms) => ms == long.MaxValue ? "-" : new WorldTime(ms).ToString()[11..19];
            Console.WriteLine($"  {Name(shooter),-16} {gun,-12} on {Name(target),-16} {l.Salvos,4} salvos {l.Shells,5} shells {l.Straddles,4} straddles " +
                              $"{l.Hits,4} hits ({100.0 * l.Hits / Math.Max(l.Shells, 1),4:F1}%)   first {Clock(l.FirstMs)}, hit {Clock(l.FirstHitMs)}");
        }
        foreach (var (target, n) in near)
            Console.WriteLine($"  near misses on {Name(target),-16} {n.Count,4}: holed {n.Holed}, gear out {n.Shock}, blacked out {n.Blackout}, parts harmed {n.Parts}");
        foreach (var (ship, d) in dc)
            Console.WriteLine($"  damage control on {Name(ship),-16} boundaries gave way {d.Gave}, parts repaired {d.Repaired}, rooms brought back {d.Restored}" +
                              (d.Steering != Steering.Free ? $", steering by {(d.Steering == Steering.Hand ? "hand" : "engines")}" : "") +
                              (d.CounterOut > 0 ? ", counterflood tanks ran out" : "") + (d.SafeKn < int.MaxValue ? $", asked for {d.SafeKn} kn" : ""));
        foreach (var e in losses)
            Console.WriteLine($"  LOST: {Name(e.Target)} at {new WorldTime(e.Ms).ToString()[11..19]}, " + (LossKind)e.Count switch
            {
                LossKind.Magazine => "her magazine set off",
                LossKind.Capsized => "capsized",
                LossKind.BrokeInTwo => "broke in two",
                _ => "foundered",
            } + $" (last hit by {Name(e.Shooter)}'s battery {e.Battery})");

        // what the hits did (docs/battle/fighting.md 4.3a): the ships still afloat that were hit
        var store = w.Ships.Store;
        bool header = false;
        for (int slot = 0; slot < store.Slots; slot++)
        {
            if (!store.IsAlive(slot) || store.Combat.Condition(slot) is not { Damaged: true } k)
                continue;
            if (!header)
                Console.WriteLine("the damage (hits taken, power and the speed it makes, mounts and directors working, steering, crew lost, fires, holes, water, list):");
            header = true;
            var c = store[slot].Handling.Class;
            double top = c.Motion.MaxSpeedKn, kn = c.SpeedAt(c.PowerAt(top) * k.PowerCap);
            int dirs = k.DirectorWorks.Count(x => x);
            Console.WriteLine($"  {Name(store[slot].Entity),-16} {k.Hits,4} hits   power {100 * k.PowerCap,3:0}% ({kn,4:0.0} of {top:0.0} kn)   mounts {k.MountsWorking,2} of " +
                              $"{k.MountWorks.Length,-2}  directors {dirs} of {k.DirectorWorks.Length}   {k.Steering,-7}  crew {k.CrewLost,5:0}   fires {k.Fires,2}   holes {k.BreachTotalM2:0.00} m²" +
                              $"   water {k.WaterT,5:0} t   list {k.Attitude.HeelDeg,5:0.0}°" + (k.Status != ShipStatus.Afloat ? $"   GOING DOWN ({k.Loss})" : ""));
        }
    }
}
