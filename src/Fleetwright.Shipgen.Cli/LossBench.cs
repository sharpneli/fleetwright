using System.Globalization;
using Fleetwright.Sim;
using Fleetwright.Sim.Ai;
using Fleetwright.Sim.Ai.Admirals;
using Fleetwright.Sim.Ai.Captains;
using Fleetwright.Sim.Ai.Commanders;
using Fleetwright.Sim.Ai.Tasks;
using Fleetwright.Sim.Combat;
using Fleetwright.Sim.Core;
using Fleetwright.Sim.Missions;
using Fleetwright.Sim.Ships;

namespace Fleetwright.Shipgen.Cli;

/// <summary><c>shipgen losses SCENARIO... [--seeds N]</c> (docs/battle/loss-stock-take.md): how ships are lost. The scenario
/// played to its end in the fine tier with the AI on every side, as the balance panel plays it, every ship's state sampled
/// each game minute and every hit on her kept. Each ship lost gets a post-mortem of her last half hour: who hit her and from
/// how far, what she was doing, how she stood, what ended her. Then the losses sorted into causes and counted by side.
/// <c>--out DIR</c> keeps one line a loss as losses.csv and the post-mortems as postmortems.txt.</summary>
static class LossBench
{
    const double Cruise = 1000;   // the asked speed with no encounter open, as the balance panel's plays
    const double NearM = 10_000;  // the tonnage about her is counted within this
    const double SeenM = 30_000;   // the nearest enemy is looked for this far (the index's cubes)
    const long Min = 60_000;

    static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

    /// <param name="Spare">A counterfactual: hits that do nothing (<see cref="Sparing"/>).</param>
    /// <param name="Trace">Ships to follow cell by cell after their first hit (<c>seed:name</c>, comma separated), into DIR/trace.txt.</param>
    public sealed record Options(int Seeds, int Threads, string LandPath, string? Out, string? Spare = null, string? Trace = null);

    /// <summary>The counterfactuals: <c>home</c> shells on a ship going home; <c>boat-guns</c> a destroyer's or smaller craft's
    /// shells on anything larger; <c>screen</c> shells on a torpedo craft while her unit screens; <c>gap:X</c> all but one
    /// shell in X (the gunnery's gap to the record, error-sources.md); joined by + any of them. Torpedoes always count.</summary>
    static Func<Hit, bool>? Sparing(string? spare, World w, AiSystem ai, ulong seed)
    {
        if (spare == null)
            return null;
        if (spare.Contains('+'))
        {
            var parts = spare.Split('+').Select(x => Sparing(x, w, ai, seed)!).ToArray();
            return h => parts.Any(x => x(h));
        }
        var store = w.Ships.Store;
        static bool Shell(in Hit h) => h.Source is HitSource.Shell or HitSource.Coarse or HitSource.NearMiss;
        static bool Boat(ShipType t) => t is ShipType.Destroyer or ShipType.Small;
        if (spare == "home")
            return h => Shell(h) && ai.Captains.Of(h.Ship).Order == CaptainOrder.Home;
        if (spare == "boat-guns")
            return h => Shell(h) && store.SlotOf(h.Shooter) is int s and >= 0 && Boat(Sightings.TypeOf(store[s].Handling.Class)) && store.SlotOf(h.Ship) is int t and >= 0 && !Boat(Sightings.TypeOf(store[t].Handling.Class));
        if (spare == "screen")
            return h => Shell(h) && store.SlotOf(h.Ship) is int t and >= 0 && Boat(Sightings.TypeOf(store[t].Handling.Class)) && w.Groups.UnitOf(h.Ship) is { } top && ai.Tasks.Of(Unit.Group(top.GroupId)).Kind == TaskKind.Screen;
        if (spare.StartsWith("gap:", StringComparison.Ordinal))
        {
            double keep = 1 / double.Parse(spare[4..], Inv);
            var rng = new Random((int)seed);
            return h => Shell(h) && rng.NextDouble() >= keep;
        }
        throw new ArgumentException($"no counterfactual '{spare}' (home, boat-guns, screen, gap:X)");
    }

    /// <summary>A ship at a game minute: her way (kn), her fitness (<see cref="Fitness"/>), her GM, her captain's order and
    /// way of fighting, whether she'd fired in the last minute, whether she's dark, her unit's task and attack, the nearest
    /// enemy and enemy heavy ship, and the enemy's and her own side's afloat tonnage within <see cref="NearM"/>.</summary>
    readonly record struct Sample(long Ms, float Kn, float Speed, float Guns, float Water, float List, float Gm, float Power, Steering Steering, CaptainOrder Order,
        string Way, CaptainReason Reason, bool Firing, bool Dark, TaskKind Task, AttackPhase Attack, float EnemyM, float HeavyM, float EnemyT, float FriendT, bool InLine, float OwnHeavyM, float VisM, float HoleM2, float Pumps, float WaterT, float FireMW, Inflow Sea, float Men, byte Parties, byte Busy, float PumpTpm);

    /// <summary>The sea coming in now, t/min, by what could stop it: holes a party can reach and hasn't worked on (plug, patch,
    /// seam), holes of those sizes it can't reach (the water's over its reach, the space full, or afire), holes already worked
    /// on (what they leave), mat-sized and beyond (the dockyard's), and over a weather deck gone under.</summary>
    readonly record struct Inflow(float Reachable, float NoAccess, float Worked, float Mat, float Beyond, float Deck)
    {
        public float All => Reachable + NoAccess + Worked + Mat + Beyond + Deck;
    }

    /// <summary>What reached her: a shell, a torpedo, a mine, a collision, a near miss; the range at that minute, m.</summary>
    readonly record struct Struck(long Ms, int Shooter, HitSource Source, float CalibreMm, float RangeM, HitEffect Effect)
    {
        public bool Shell => Source is HitSource.Shell or HitSource.Coarse;
    }

    sealed class Ship(int entity, int side, string name, ShipClass cls)
    {
        public readonly int Entity = entity, Side = side;
        public readonly string Name = name, Class = cls.Name;
        public readonly ShipType Type = Sightings.TypeOf(cls);
        public readonly double Tonnes = cls.Motion.FullDisplacementT;
        public readonly List<Sample> Samples = [];
        public readonly List<Struck> Hits = [];
        public readonly List<(long Ms, int Shooter)> Salvos = [];
        public long FellMs = -1;
        public CaptainReason FellWhy;
        public Vec3d At;
    }

    /// <summary>A loss, as the post-mortem reads her. Times in minutes; NaN where there's nothing to read.</summary>
    sealed record Loss(string Scenario, ulong Seed, Ship Ship, string SideName, string Group, LossKind Kind, ShipFate Fate, double EndMin, double FirstHitMin, double FellMin,
        double OutMin, int Hits, int HitsAfterOut, int Hits30, double LastHitMin, int Torpedoes, double TorpedoShare, int NearMisses, int Heavy, int Medium, int Light,
        double Range30Km, double RangeAllKm, ShipType TopShooter, int Shooters30, Sample First, Sample At30, Sample At10, Sample Last, double Firing30, double GaveUpMin, double GoneMin, string Text);

    /// <summary>A seed's ships and its losses.</summary>
    sealed record Played(string Scenario, ulong Seed, List<Ship> Ships, List<Loss> Losses, EndReason Reason, double Hours, long Missed, Tally Tally);

    /// <summary>The whole fight beside the losses: where the fire went (by the shooter's type and battery, his target's type,
    /// whether she was still fighting, and the true range when fired), where the ships were (ship-minutes by type, what she
    /// was doing and the nearest enemy heavy ship), the range between the fighting capital ships by the quarter hour, and
    /// what the heavy hits did to magazines.</summary>
    sealed class Tally
    {
        public readonly Dictionary<string, (double Shells, double Hits)> Fire = [], FireBy = [];
        public readonly Dictionary<string, double> Minutes = [];
        public readonly Dictionary<int, List<float>> LineRange = [];
        public int HeavyHits, MagazineHits, TrainsLit;
        public int[] RacesByHit = [], RacesByFire = [];
    }

    public static int Run(SimSettings settings, string root, IReadOnlyList<string> names, Options o)
    {
        string dir = Path.Combine(root, "..", "Content", "Scenarios");
        var scenarios = names.ToDictionary(n => n, n => Scenario.Load(Path.Combine(dir, n + ".json"), [dir]));
        var shoreTask = Task.Run(() => Shore.Build(Land.Load(o.LandPath)));
        using var registry = new ShipClassRegistry(0);
        var classes = scenarios.ToDictionary(kv => kv.Key, kv => ScenarioLoader.RequestClasses(kv.Value, registry, [Path.Combine(root, "designs")]));
        registry.WaitAll();
        var shore = shoreTask.Result;
        var jobs = (from n in names from i in Enumerable.Range(0, o.Seeds) select (Name: n, Seed: scenarios[n].Seed + (ulong)i)).ToList();
        int threads = o.Threads > 0 ? o.Threads : Environment.ProcessorCount;
        Console.WriteLine($"how ships are lost: {string.Join(", ", names)}, {o.Seeds} seeds each, on {threads} threads" + (o.Spare != null ? $"; spared: {o.Spare}" : ""));
        var trace = o.Trace?.Split(',').Select(x => x.Split(':', 2)).Select(x => (ulong.Parse(x[0], Inv), x[1])).ToList();
        var runs = new Played[jobs.Count];
        Parallel.ForEach(Enumerable.Range(0, jobs.Count), new ParallelOptions { MaxDegreeOfParallelism = threads }, i =>
        {
            var j = jobs[i];
            var sw = System.Diagnostics.Stopwatch.StartNew();
            runs[i] = Play(scenarios[j.Name], j.Name, j.Seed, settings, registry, classes[j.Name], shore, o.Spare, trace);
            Console.WriteLine($"  {j.Name} seed {j.Seed}: {runs[i].Losses.Count} lost of {runs[i].Ships.Count}, ended {ScenarioPlay.Why(runs[i].Reason)} after {runs[i].Hours:F1} h ({sw.Elapsed.TotalSeconds:0} s)");
        });
        if (runs.Sum(r => r.Missed) is > 0 and var missed)
            Console.WriteLine($"  ({missed} combat log lines went by unread: the ring too small for a minute of a fight; hits undercounted)");
        foreach (var n in names)
            Summary(scenarios[n], n, runs.Where(r => r.Scenario == n).ToList());
        if (o.Out != null)
        {
            Directory.CreateDirectory(o.Out);
            WriteCsv(Path.Combine(o.Out, "losses.csv"), runs);
            File.WriteAllLines(Path.Combine(o.Out, "postmortems.txt"), runs.SelectMany(r => r.Losses).Select(l => l.Text));
            File.WriteAllLines(Path.Combine(o.Out, "fire.txt"), runs.SelectMany(r => r.Tally.FireBy.Select(kv => string.Format(Inv, "{0}	{1}	{2}	{3:0}	{4:0}", r.Scenario, r.Seed, kv.Key, kv.Value.Shells, kv.Value.Hits))));
            if (TraceText.Length > 0)
                File.WriteAllText(Path.Combine(o.Out, "trace.txt"), TraceText.ToString());
            Console.WriteLine($"kept in {o.Out}");
        }
        return 0;
    }

    static readonly System.Text.StringBuilder TraceText = new();

    static Played Play(Scenario s, string name, ulong seed, SimSettings settings, ShipClassRegistry registry, Dictionary<string, int> classes, Shore shore, string? spare, List<(ulong Seed, string Name)>? trace)
    {
        var (w, ai, start) = ScenarioPlay.Begin(s, seed, settings, registry, classes, shore, Workers.Inline, 1);
        w.Damage.Spared = Sparing(spare, w, ai, seed);
        var end = new ScenarioEnd(s, w, ai);
        var ships = new Dictionary<int, Ship>();
        var index = new CubeIndex();
        var store = w.Ships.Store;
        long read = 0, missed = 0;
        var tally = new Tally();
        // a battery's calibre and whether it's the chief, kept by the shooter's class
        w.Gunnery.Reported = r =>
        {
            int ss = store.SlotOf(r.Shooter), ts = store.SlotOf(r.Target);
            if (ss < 0 || ts < 0)
                return;
            var by = store[ss].Handling.Class;
            bool chief = by.Batteries[r.Battery].Chief;
            string k = $"{Sightings.TypeOf(by),-9} {(chief ? "chief" : "other"),-5} at {Sightings.TypeOf(store[ts].Handling.Class),-9} {(ai.Captains.Of(r.Target).Order == CaptainOrder.Home ? "going home" : "fighting"),-10} {Band(r.TrueRangeM / 1000)}";
            var (a, b) = tally.Fire.GetValueOrDefault(k);
            tally.Fire[k] = (a + r.Shells, b + r.Hits);
            var (order, _, _) = ai.Captains.Of(r.Shooter);
            var (attacking, finishing, away) = ai.Captains.FightOf(r.Shooter);
            string doing = order == CaptainOrder.Fight ? attacking ? "Fight attacking" : finishing ? "Fight finishing" : away ? "Fight away" : "Fight band" : order.ToString();
            var line = w.Groups.UnitOf(r.Shooter);
            if (line != null)
                doing += $" / task {ai.Tasks.Of(Unit.Group(line.GroupId)).Kind} / admiral {ai.Admirals.Of(line.GroupId).Item1}" + (ai.Admirals.AttackOf(line.GroupId).Phase is var ph and not AttackPhase.None ? $" / attack {ph}" : "");
            string d = k + " | " + doing;
            var (a2, b2) = tally.FireBy.GetValueOrDefault(d);
            tally.FireBy[d] = (a2 + r.Shells, b2 + r.Hits);
        };

        void Sample()
        {
            index.Clear(store.Slots, SeenM);
            for (int slot = 0; slot < store.Slots; slot++)
            {
                if (!store.IsAlive(slot))
                    continue;
                var x = store[slot];
                if (!ships.TryGetValue(x.Entity, out var ship))
                    ships[x.Entity] = ship = new Ship(x.Entity, x.Side, start.ShipNames.GetValueOrDefault(x.Entity) ?? $"{x.Handling.Class.Name} #{x.Entity}", x.Handling.Class);
                ship.At = x.Position;
                if (Aims.Afloat(store, slot))
                    index.Add(slot, x.Position);
            }
            for (int slot = 0; slot < store.Slots; slot++)
                if (store.IsAlive(slot))
                {
                    var x = Read(w, ai, slot, index);
                    ships[store[slot].Entity].Samples.Add(x);
                    if (!Aims.Afloat(store, slot) || x.HeavyM > SeenM)
                        continue;
                    var type = ships[store[slot].Entity].Type;
                    string k = $"{type,-9} {Doing(x),-44} {Band(x.HeavyM / 1000)}";
                    tally.Minutes[k] = tally.Minutes.GetValueOrDefault(k) + 1;
                    if (type == ShipType.Capital && x.Order != CaptainOrder.Home)
                    {
                        int quarter = (int)((w.Time.Ms - s.Start.Ms) / (15 * Min));
                        if (!tally.LineRange.TryGetValue(quarter, out var l))
                            tally.LineRange[quarter] = l = [];
                        l.Add(x.HeavyM);
                    }
                }
        }

        void ReadLog()
        {
            var log = w.CombatLog;
            long fresh = log.Written - read;
            missed += Math.Max(0, fresh - log.Count);
            for (long i = Math.Min(fresh, log.Count) - 1; i >= 0; i--)
            {
                ref readonly var e = ref log.Newest((int)i);
                if (e.Kind is not (CombatEvent.Fired or CombatEvent.Hit or CombatEvent.NearMiss) || !ships.TryGetValue(e.Target, out var her))
                    continue;
                if (e.Kind == CombatEvent.Fired)
                    her.Salvos.Add((e.Ms, e.Shooter));
                else
                {
                    her.Hits.Add(new Struck(e.Ms, e.Shooter, e.Source, e.CalibreMm, ships.TryGetValue(e.Shooter, out var by) ? (float)(by.At - her.At).Length : float.NaN, e.Effect));
                    tally.HeavyHits += e.CalibreMm >= 200 && e.Source is HitSource.Shell or HitSource.Coarse ? 1 : 0;
                    tally.MagazineHits += (e.Effect & HitEffect.Magazine) != 0 ? 1 : 0;
                }
            }
            read = log.Written;
        }

        Sample();
        var traced = (trace ?? []).Where(x => x.Seed == seed).Select(x => x.Name).ToHashSet();
        var traces = new Dictionary<int, System.Text.StringBuilder>();
        ScenarioPlay.PlayToEnd(w, end, Cruise, 1, () =>
        {
            Sample();
            ReadLog();
            if (traced.Count > 0 && (w.Time.Ms - s.Start.Ms) / Min % 10 == 0)
                for (int slot = 0; slot < store.Slots; slot++)
                    if (store.IsAlive(slot) && ships[store[slot].Entity] is var ship && traced.Contains(ship.Name) && ship.Hits.Count > 0)
                    {
                        if (!traces.TryGetValue(ship.Entity, out var text))
                            traces[ship.Entity] = text = new System.Text.StringBuilder($"--- {name} seed {seed}: {ship.Name} ({ship.Class}, {ship.Tonnes:0} t)\n");
                        TraceShip(w, slot, s.Start.Ms, text);
                    }
        });
        if (traces.Count > 0)
            lock (TraceText)
                foreach (var t in traces.Values)
                    TraceText.Append(t);
        (tally.TrainsLit, tally.RacesByHit, tally.RacesByFire) = (w.Damage.TrainsLit, w.Damage.MagazineCounts.ByHit, w.Damage.MagazineCounts.ByFire);

        foreach (var l in end.Timeline)
            if (l.Kind == PlayEvent.FellOut && ships.TryGetValue(l.Entity, out var f) && f.FellMs < 0)
                (f.FellMs, f.FellWhy) = (l.Ms, (CaptainReason)l.Count);
        var lostAt = new Dictionary<int, (long Ms, LossKind Kind, long GaveUp, long Gone)>();
        foreach (var l in end.Timeline)
            if (l.Kind is PlayEvent.Lost or PlayEvent.GaveUp)
            {
                var (ms, kind, up, gone) = lostAt.GetValueOrDefault(l.Entity, (long.MaxValue, LossKind.None, -1L, -1L));
                lostAt[l.Entity] = (Math.Min(ms, l.Ms), l.Kind == PlayEvent.Lost ? (LossKind)l.Count : kind, l.Kind == PlayEvent.GaveUp && up < 0 ? l.Ms : up, l.Kind == PlayEvent.Lost ? l.Ms : gone);
            }
        var losses = new List<Loss>();
        foreach (var ship in ships.Values.OrderBy(x => x.Entity))
        {
            var fate = end.Fate(ship.Entity);
            if (fate != ShipFate.Afloat && ship.Side < s.Sides.Length)
                losses.Add(PostMortem(s, name, seed, ship, fate, lostAt.GetValueOrDefault(ship.Entity, (end.EndMs, LossKind.None, -1L, -1L)), ships));
        }
        return new Played(name, seed, [.. ships.Values], losses, end.Reason, (end.EndMs - s.Start.Ms) / 3_600_000.0, missed, tally);
    }

    static Sample Read(World w, AiSystem ai, int slot, CubeIndex index)
    {
        var store = w.Ships.Store;
        var x = store[slot];
        var cls = x.Handling.Class;
        var k = store.Combat.Condition(slot);
        var fit = Fitness.Of(cls, k, w.Damage.Flood(cls).ReserveT);
        double enemyM = double.PositiveInfinity, heavyM = double.PositiveInfinity, ownHeavyM = double.PositiveInfinity, enemyT = 0, friendT = 0;
        int found = index.Near(x.Position);
        foreach (int o in index.Found(found))
        {
            if (o == slot)
                continue;
            var y = store[o];
            double d = (y.Position - x.Position).Length;
            double t = y.Handling.Class.Motion.FullDisplacementT;
            if (y.Side == x.Side)
            {
                friendT += d <= NearM ? t : 0;
                if (Sightings.TypeOf(y.Handling.Class) is ShipType.Capital or ShipType.Cruiser)
                    ownHeavyM = Math.Min(ownHeavyM, d);
                continue;
            }
            if (!w.Relations.Hostile(x.Side, y.Side))
                continue;
            enemyM = Math.Min(enemyM, d);
            enemyT += d <= NearM ? t : 0;
            if (Sightings.TypeOf(y.Handling.Class) is ShipType.Capital or ShipType.Cruiser)
                heavyM = Math.Min(heavyM, d);
        }
        var (order, _, _) = ai.Captains.Of(x.Entity);
        var (attacking, finishing, away) = ai.Captains.FightOf(x.Entity);
        var task = TaskKind.None;
        var attack = AttackPhase.None;
        var top = w.Groups.UnitOf(x.Entity);
        if (top != null)
        {
            task = ai.Tasks.Of(Unit.Group(top.GroupId)).Kind;
            attack = ai.Admirals.AttackOf(top.GroupId).Phase;
        }
        return new Sample(w.Time.Ms, (float)(x.Speed / Handling.MsPerKnot), (float)fit.SpeedShare, (float)fit.MainGunsShare, (float)fit.WaterShare, (float)fit.ListDeg, (float)k.GmM, (float)k.PowerCap,
            k.Steering, order, attacking ? "attacking" : finishing ? "finishing" : away ? "away" : "band", ai.Captains.ReasonOf(x.Entity), w.Time.Ms - store.Combat.LastFired(slot) < Min,
            k.Dark, task, attack, (float)enemyM, (float)heavyM, (float)enemyT, (float)friendT, top != null, (float)ownHeavyM, (float)w.Weather.At(x.Position, w.Time).VisibilityM,
            (float)Holes(k), (float)k.PumpShare, (float)k.WaterT, (float)k.FireShowMW, Sea(w, slot), (float)Math.Max(w.Damage.Flood(cls).Complement - k.CrewLost, 0), (byte)k.PartyCount,
            (byte)Busy(k), (float)(cls.Drainage.PumpsTph * k.PumpShare / 60));
    }

    static int Busy(ShipCondition k)
    {
        int n = 0;
        for (int p = 0; p < k.PartyCount; p++)
            n += k.Parties[p].Job != RepairJob.None || k.Parties[p].Target >= 0 ? 1 : 0;
        return n;
    }

    /// <summary>Each cell's inflow shared out by its holes' effective area (a cell's holes differ in height; a share by area is
    /// good enough to say what the water's coming through).</summary>
    static Inflow Sea(World w, int slot)
    {
        var store = w.Ships.Store;
        var cls = store[slot].Handling.Class;
        var k = store.Combat.Condition(slot);
        if (k.CellInflow.Length == 0)
            return default;
        var f = w.Damage.Flood(cls);
        double reach = 0, none = 0, worked = 0, mat = 0, beyond = 0, deck = 0;
        for (int i = 0; i < cls.Cells.Length; i++)
        {
            double q = k.CellInflow[i] * FloodModel.SeaT * 60;
            if (q <= 0)
                continue;
            if (k.BreachM2[i] <= 0)
            {
                deck += q;
                continue;
            }
            double all = 0;
            for (int c = 0; c < ShipCondition.HoleClasses; c++)
                all += k.HoleM2(i * ShipCondition.HoleClasses + c);
            for (int c = 0; c < ShipCondition.HoleClasses; c++)
            {
                int h = i * ShipCondition.HoleClasses + c;
                double share = all > 0 ? k.HoleM2(h) / all : 0, part = q * share;
                if (part <= 0)
                    continue;
                if ((HoleClass)c == HoleClass.Mat)
                    mat += part;
                else if ((HoleClass)c == HoleClass.Beyond)
                    beyond += part;
                else
                {
                    double open = k.HoleM2(h) > 0 ? k.HoleOpenM2[h] / k.HoleM2(h) : 0;
                    worked += part * (1 - open);
                    if (Repairs.Reachable(cls, f, k, i, k.HoleZ[h], w.Settings))
                        reach += part * open;
                    else
                        none += part * open;
                }
            }
        }
        return new Inflow((float)reach, (float)none, (float)worked, (float)mat, (float)beyond, (float)deck);
    }

    /// <summary>A ship cell by cell: her state, her parties, the water by cell with what comes in and how big its holes are,
    /// and the biggest flows across the graph.</summary>
    static void TraceShip(World w, int slot, long t0, System.Text.StringBuilder o)
    {
        var store = w.Ships.Store;
        var x = store[slot];
        var cls = x.Handling.Class;
        var k = store.Combat.Condition(slot);
        var f = w.Damage.Flood(cls);
        var water = HitQuery.WaterOf(cls, k.Attitude);
        o.AppendLine(Inv, $"  +{(w.Time.Ms - t0) / Min} min: {k.WaterT:0} t of reserve {f.ReserveT:0} t, list {k.Attitude.HeelDeg:0.0}, trim {k.Attitude.TrimDeg:0.0}, sinkage {k.Attitude.SinkageM:0.00} m, GM {k.GmM:0.00}, {x.Speed / Handling.MsPerKnot:0.0} kn; power {k.PowerCap:0.00}, plant out {k.PlantOut}, no steam {k.NoSteam}, dark {k.Dark}; pumps {k.PumpShare:0.00} of {cls.Drainage.PumpsTph:0} t/h, portable rigged {k.PumpsRigged} of {cls.Drainage.PortablePumps} at {cls.Drainage.PortableTph:0} t/h; men out {k.CrewLost:0} of {f.Complement:0}; given up {k.GivenUp}");
        for (int p = 0; p < k.PartyCount; p++)
        {
            ref readonly var q = ref k.Parties[p];
            string what = q.Target >= 0 ? $"fire {q.Target}" : q.Job == RepairJob.None ? "free" : $"{q.Job} at {q.JobAt}";
            o.AppendLine(Inv, $"    party {p}: {q.Men:0} men, skill {q.Skill:0.0}, {what}, done {q.Done:0} man-min, arrives {(q.ArriveMs - w.Time.Ms) / 1000} s");
        }
        for (int i = 0; i < cls.Cells.Length; i++)
        {
            if (k.WaterM3[i] < 0.5 && k.CellInflow[i] <= 0 && k.BreachM2[i] <= 0)
                continue;
            var b = cls.Cells[i].Box;
            var kinds = new List<RoomKind>();
            foreach (int r in cls.RoomsIn(i))
                kinds.Add(cls.Rooms[r].Kind);
            string rooms = string.Join("+", kinds);
            o.Append(Inv, $"    cell {i,3} {rooms,-26} x {b.X0:0}..{b.X1:0} z {b.Base:0.0}..{b.Top:0.0} sea {water.At(f.Cx[i], f.Cy[i]):0.0}: {k.WaterM3[i]:0}/{f.Capacity[i]:0} m3, in {k.CellInflow[i] * 61.5:0.0} t/min");
            for (int c = 0; c < ShipCondition.HoleClasses; c++)
            {
                int h = i * ShipCondition.HoleClasses + c;
                if (k.HoleM2(h) > 0)
                    o.Append(Inv, $"; {(HoleClass)c} open {k.HoleOpenM2[h]:0.000} m2 x{k.HoleOpenN[h]} done {k.HoleDoneM2[h]:0.000} left {k.HoleLeft[h]:0.00} at z {k.HoleZ[h]:0.0} reach {Repairs.Reachable(cls, f, k, i, k.HoleZ[h], w.Settings)}");
            }
            o.AppendLine();
        }
        var flows = new List<(double T, string Text)>();
        foreach (ref readonly var e in f.Edges.AsSpan())
        {
            double v = k.JoinFlow[e.Pool] * 61.5;
            if (Math.Abs(v) < 0.2)
                continue;
            int from = v > 0 ? e.A : e.B, to = v > 0 ? e.B : e.A;
            flows.Add((Math.Abs(v), string.Format(Inv, "{0} -> {1} {2:0.0} t/min ({3}, open {4:0.000}, seam {5:0.000} x{6:0.0}, closure {7:0.000}, {8})", from, to, Math.Abs(v), e.Join, k.JoinOpenM2[e.Pool], k.JoinSeamM2[e.Pool], k.JoinWork[e.Pool], k.JoinClosureM2[e.Pool], k.JoinMarks[e.Pool])));
        }
        foreach (var (_, text) in flows.OrderByDescending(y => y.T).Take(12))
            o.AppendLine("    flow " + text);
    }

    /// <summary>The holes the sea sees, m² (<see cref="ShipCondition.BreachM2"/>: damage control's work counted).</summary>
    static double Holes(ShipCondition k)
    {
        double sum = 0;
        foreach (double a in k.BreachM2)
            sum += a;
        return sum;
    }

    static Sample AtOrBefore(List<Sample> xs, long ms)
    {
        if (xs.Count == 0)
            return default;
        int lo = 0, hi = xs.Count - 1;
        while (lo < hi)
        {
            int mid = (lo + hi + 1) / 2;
            if (xs[mid].Ms <= ms)
                lo = mid;
            else
                hi = mid - 1;
        }
        return xs[lo];
    }

    static Loss PostMortem(Scenario s, string name, ulong seed, Ship ship, ShipFate fate, (long Ms, LossKind Kind, long GaveUp, long Gone) lost, Dictionary<int, Ship> ships)
    {
        long end = lost.Ms, gaveUp = lost.GaveUp, gone = lost.Gone, t0 = s.Start.Ms, from = end - 30 * Min;
        double M(long ms) => (ms - t0) / (double)Min;
        var hits = ship.Hits.Where(h => h.Ms <= end).ToList();
        var shells = hits.Where(h => h.Shell).ToList();
        var last = shells.Where(h => h.Ms >= from).ToList();
        var torps = hits.Where(h => h.Source == HitSource.Torpedo).ToList();
        int near = hits.Count(h => h.Source == HitSource.NearMiss);
        long first = shells.Concat(torps).Select(h => h.Ms).DefaultIfEmpty(-1).Min();
        // out of the fight: fallen out, or under half her speed by the sim's own reckoning (a fall-out's told at most once)
        long outMs = ship.FellMs >= 0 && ship.FellMs <= end ? ship.FellMs : -1;
        foreach (var x in ship.Samples)
            if (x.Ms <= end && x.Speed < 0.5 && (outMs < 0 || x.Ms < outMs))
            {
                outMs = x.Ms;
                break;
            }
        int afterOut = outMs < 0 ? 0 : shells.Count(h => h.Ms > outMs);
        double lastHit = shells.Concat(torps).Select(h => (end - h.Ms) / (double)Min).DefaultIfEmpty(double.NaN).Min();

        // the water that came in on each torpedo: from just before it to a few minutes on, against her water at the end
        var final = AtOrBefore(ship.Samples, end);
        double torpWater = 0;
        long doneTo = long.MinValue;
        foreach (var t in torps.OrderBy(h => h.Ms))
        {
            long a = Math.Max(t.Ms - Min, doneTo), b = t.Ms + 3 * Min;
            torpWater += Math.Max(AtOrBefore(ship.Samples, b).Water - AtOrBefore(ship.Samples, a).Water, 0);
            doneTo = b;
        }
        double torpShare = final.Water > 0.01 ? Math.Min(torpWater / final.Water, 1) : torps.Count > 0 ? 1 : 0;

        double Median(IEnumerable<double> xs)
        {
            var l = xs.Where(double.IsFinite).OrderBy(x => x).ToList();
            return l.Count == 0 ? double.NaN : l[l.Count / 2] / 1000;
        }
        double r30 = Median(last.Select(h => (double)h.RangeM)), rAll = Median(shells.Select(h => (double)h.RangeM));
        var top = last.Count > 0 ? last : shells;
        var byType = top.GroupBy(h => ships.TryGetValue(h.Shooter, out var by) ? by.Type : ShipType.Unknown).OrderByDescending(g => g.Count()).FirstOrDefault()?.Key ?? ShipType.Unknown;
        int shooters = ship.Salvos.Where(x => x.Ms >= from && x.Ms <= end).Select(x => x.Shooter).Distinct().Count();
        var window = ship.Samples.Where(x => x.Ms >= from && x.Ms <= end).ToList();
        double firing = window.Count > 0 ? (double)window.Count(x => x.Firing) / window.Count : 0;
        var atFirst = first >= 0 ? AtOrBefore(ship.Samples, first) : final;

        string group = Group(lost.Kind, torpShare, torps.Count, shells.Count, outMs, end, afterOut, lastHit, r30, atFirst, first);
        var loss = new Loss(name, seed, ship, s.Sides[ship.Side], group, lost.Kind, fate, M(end), first >= 0 ? M(first) : double.NaN, ship.FellMs >= 0 ? M(ship.FellMs) : double.NaN,
            outMs >= 0 ? M(outMs) : double.NaN, shells.Count, afterOut, last.Count, lastHit, torps.Count, torpShare, near, shells.Count(h => h.CalibreMm >= 200),
            shells.Count(h => h.CalibreMm is >= 120 and < 200), shells.Count(h => h.CalibreMm < 120), r30, rAll, byType, shooters, atFirst, AtOrBefore(ship.Samples, end - 30 * Min),
            AtOrBefore(ship.Samples, end - 10 * Min), final, firing, gaveUp >= 0 ? M(gaveUp) : double.NaN, gone >= 0 ? M(gone) : double.NaN, "");
        return loss with { Text = Report(loss, ship, end, ships, t0) };
    }

    /// <summary>The starting groups (docs/battle/loss-stock-take.md "What to build first"), first match, to be revised by what
    /// turns up. A cripple's been out of the fight at least <c>5 min</c>; the flood's when nothing's struck her for 20.</summary>
    static string Group(LossKind kind, double torpShare, int torps, int shells, long outMs, long end, int afterOut, double lastHit, double r30, in Sample first, long firstMs)
    {
        if (kind == LossKind.Magazine)
            return "Magazine";
        if (torps > 0 && (torpShare >= 0.5 || shells == 0))
            return "Torpedoed";
        if (lastHit >= 20 || double.IsNaN(lastHit))
            return "Flooding ran away";
        if (outMs >= 0 && end - outMs >= 5 * Min)
            return afterOut > 0 ? "Cripple finished off" : "Flooding ran away";
        if (r30 < 3)
            return "Point-blank brawl";
        if (first.EnemyT >= 3 * (first.FriendT + 1))
            return "Fed in piecemeal";
        return end - firstMs >= 10 * Min ? "Stayed too long" : "Fair fight";
    }

    static string Doing(in Sample x)
    {
        string her = x.Order == CaptainOrder.Fight ? $"Fight {x.Way}" : x.Order.ToString();
        if (x.Reason != CaptainReason.None)
            her += $" ({x.Reason})";
        return her + (x.InLine ? $", task {x.Task}" + (x.Attack != AttackPhase.None ? $" attack {x.Attack}" : "") : ", out of her line");
    }

    static string Stood(in Sample x) => string.Format(Inv, "{0,4:0.0} kn, speed {1,3:0}%, guns {2,3:0}%, water {3,3:0}% ({9:0} t, holes {10:0.0} m², pumps {11:0}%), list {4,4:0.0}, GM {5,5:0.00}{6}{7}{8}{12}", x.Kn, 100 * x.Speed, 100 * x.Guns, 100 * x.Water, x.List, x.Gm,
        x.Steering != Steering.Free ? $", steering {x.Steering}" : "", x.Dark ? ", dark" : "", x.Firing ? ", firing" : "", x.WaterT, x.HoleM2, 100 * x.Pumps, x.FireMW > 1 ? $", fire {x.FireMW:0} MW" : "");

    static string Km(double m) => double.IsFinite(m) ? ((m) / 1000).ToString("0.0", Inv) + " km" : "-";

    static string Report(Loss l, Ship ship, long end, Dictionary<int, Ship> ships, long t0)
    {
        var o = new System.Text.StringBuilder();
        o.AppendLine(Inv, $"--- {l.Scenario} seed {l.Seed}: {ship.Name} ({ship.Class}, {ship.Type}, {ship.Tonnes:0} t, {l.SideName}): {l.Kind} / {l.Fate} at +{l.EndMin:0} min: {l.Group}");
        o.AppendLine(Inv, $"    first struck +{l.FirstHitMin:0}; out of the fight +{l.OutMin:0}{(ship.FellMs >= 0 ? $" ({ship.FellWhy})" : "")}; shell hits {l.Hits} (heavy {l.Heavy}, medium {l.Medium}, light {l.Light}), {l.HitsAfterOut} after she was out, {l.Hits30} in the last 30 min; torpedoes {l.Torpedoes} (water {100 * l.TorpedoShare:0}%), near misses {l.NearMisses}; last struck {l.LastHitMin:0.0} min before the end");
        o.AppendLine(Inv, $"    when first struck: {Doing(l.First)}; {Stood(l.First)}; enemy {Km(l.First.EnemyM)}, heavy {Km(l.First.HeavyM)}, own heavy {Km(l.First.OwnHeavyM)}, visibility {Km(l.First.VisM)}; within 10 km enemy {l.First.EnemyT:0} t, friends {l.First.FriendT:0} t");
        if (l.GaveUpMin < l.GoneMin)
            o.AppendLine(Inv, $"    given up +{l.GaveUpMin:0}, went under +{l.GoneMin:0}");
        for (int back = 30; back >= 0; back -= 5)
        {
            long a = end - back * Min, b = a + 5 * Min;
            var x = AtOrBefore(ship.Samples, a);
            if (x.Ms == 0 || x.Ms < a - 2 * Min)
                continue;
            var near = ship.Hits.Where(h => h.Ms > a && h.Ms <= Math.Min(b, end) && h.Shell).ToList();
            var ts = ship.Hits.Count(h => h.Ms > a && h.Ms <= Math.Min(b, end) && h.Source == HitSource.Torpedo);
            string by = string.Join(", ", near.GroupBy(h => ships.TryGetValue(h.Shooter, out var s) ? s.Type : ShipType.Unknown).OrderByDescending(g => g.Count())
                .Select(g => string.Format(Inv, "{0} {1} at {2:0.0} km", g.Count(), g.Key, g.Select(h => h.RangeM / 1000).Order().ElementAt(g.Count() / 2))));
            string struck = near.Count + ts > 0 ? $" | next 5 min: {by}{(ts > 0 ? $" +{ts} torpedoes" : "")}" : "";
            o.AppendLine(Inv, $"    T-{back,2}: {Doing(x),-52} {Stood(x)}; enemy {Km(x.EnemyM)}, heavy {Km(x.HeavyM)}{struck}");
        }
        return o.ToString();
    }

    static void Summary(Scenario s, string name, List<Played> runs)
    {
        int n = runs.Count;
        var all = runs.SelectMany(r => r.Losses).ToList();
        Console.WriteLine();
        Console.WriteLine($"{s.Name}, {n} seeds: lost a seed by side and type (of the ships she has), tonnage in brackets");
        for (int side = 0; side < s.Sides.Length; side++)
        {
            var mine = all.Where(l => l.Ship.Side == side).ToList();
            var cols = runs[0].Ships.Where(x => x.Side == side).GroupBy(x => x.Type).OrderBy(g => g.Key)
                .Select(g => string.Format(Inv, "{0} {1:0.0}/{2}", g.Key, (double)mine.Count(l => l.Ship.Type == g.Key) / n, g.Count()));
            Console.WriteLine(string.Format(Inv, "  {0,-14} {1,5:0.0} lost ({2:0} t): {3}", s.Sides[side], (double)mine.Count / n, mine.Sum(l => l.Ship.Tonnes) / n, string.Join(", ", cols)));
        }

        Console.WriteLine("  how they went: " + string.Join(", ", all.GroupBy(l => l.Kind).OrderByDescending(g => g.Count()).Select(g => $"{g.Key} {100.0 * g.Count() / Math.Max(all.Count, 1):0}%"))
            + "; fates: " + string.Join(", ", all.GroupBy(l => l.Fate).OrderByDescending(g => g.Count()).Select(g => $"{g.Key} {g.Count()}")));

        // the magazines (docs/battle/magazines.md): how far a flash got, a seed
        string Races(Func<Tally, int[]> by) => string.Join(", ", Enum.GetValues<MagazineTier>().Select(t => string.Format(Inv, "{0} {1:0.0}", t, runs.Average(r => by(r.Tally)[(int)t]))));
        Console.WriteLine(string.Format(Inv, "  magazines, a seed: {0:0} heavy hits, {1:0.0} hits lit charges in a train; set off by a hit: {2}; by a fire: {3}",
            runs.Average(r => r.Tally.HeavyHits), runs.Average(r => r.Tally.TrainsLit), Races(t => t.RacesByHit), Races(t => t.RacesByFire)));

        Console.WriteLine("  causes (a seed; share; of them capital/cruiser/destroyer-small; the medians: shell hits, of those after she was out, minutes first struck to the end, out to the end, the last half hour's range):");
        foreach (var side in Enumerable.Range(-1, s.Sides.Length + 1))
        {
            var part = side < 0 ? all : all.Where(l => l.Ship.Side == side).ToList();
            Console.WriteLine($"    {(side < 0 ? "every side" : s.Sides[side])}:");
            foreach (var g in part.GroupBy(l => l.Group).OrderByDescending(g => g.Count()))
                Console.WriteLine(string.Format(Inv, "      {0,-22}{1,6:0.0} {2,4:0}%  {3,3}/{4,3}/{5,3}  hits {6,4:0} ({7,3:0} after)  {8,5:0} min  out {9,5:0} min  at {10,5:0.0} km", g.Key, (double)g.Count() / n, 100.0 * g.Count() / Math.Max(part.Count, 1),
                    g.Count(l => l.Ship.Type == ShipType.Capital), g.Count(l => l.Ship.Type == ShipType.Cruiser), g.Count(l => l.Ship.Type is ShipType.Destroyer or ShipType.Small),
                    Med(g.Select(l => (double)l.Hits)), Med(g.Select(l => (double)l.HitsAfterOut)), Med(g.Select(l => l.EndMin - l.FirstHitMin)), Med(g.Select(l => l.EndMin - l.OutMin)), Med(g.Select(l => l.Range30Km))));
        }

        // the slow deaths: halfway from 20 min after her last hit to the end, what's coming in and who's about it
        var slow = all.Where(l => l.LastHitMin >= 40).Select(l =>
        {
            double endMs = s.Start.Ms + l.EndMin * Min;
            var x = AtOrBefore(l.Ship.Samples, (long)(endMs - (l.LastHitMin - 20) * Min / 2));
            return (Loss: l, At: x);
        }).ToList();
        if (slow.Count > 0)
        {
            Console.WriteLine($"  the slow deaths (no hit in 40 min or more, a seed {(double)slow.Count / n:0.0}): the medians halfway through, sea in by what could stop it, t/min:");
            foreach (var g in slow.GroupBy(y => y.Loss.Ship.Type).OrderBy(g => g.Key))
            {
                Console.WriteLine(string.Format(Inv, "    {0,-10}{1,5:0.0}  {2,6:0} min after the hit; enemy {3,5:0.0} km; water {4,6:0} t, holes {5,5:0.0} m2; sea in {6,6:0.0} (reachable {7:0.0}, no access {8:0.0}, worked {9:0.0}, mat {10:0.0}, beyond {11:0.0}, deck {12:0.0}); pumps {13:0.0} t/min; parties {14:0}, busy {15:0}; men {16:0}; {17,4:0} kn",
                    g.Key, (double)g.Count() / n, Med(g.Select(y => y.Loss.LastHitMin)), Med(g.Select(y => (double)y.At.EnemyM / 1000)), Med(g.Select(y => (double)y.At.WaterT)), Med(g.Select(y => (double)y.At.HoleM2)),
                    Med(g.Select(y => (double)y.At.Sea.All)), Med(g.Select(y => (double)y.At.Sea.Reachable)), Med(g.Select(y => (double)y.At.Sea.NoAccess)), Med(g.Select(y => (double)y.At.Sea.Worked)), Med(g.Select(y => (double)y.At.Sea.Mat)), Med(g.Select(y => (double)y.At.Sea.Beyond)), Med(g.Select(y => (double)y.At.Sea.Deck)),
                    Med(g.Select(y => (double)y.At.PumpTpm)), Med(g.Select(y => (double)y.At.Parties)), Med(g.Select(y => (double)y.At.Busy)), Med(g.Select(y => (double)y.At.Men)), Med(g.Select(y => (double)y.At.Kn))));
                double tot = g.Sum(y => (double)y.At.Sea.All);
                Console.WriteLine(string.Format(Inv, "      all the sea coming in, the shares: reachable {0:0}%, no access {1:0}%, worked {2:0}%, mat {3:0}%, beyond {4:0}%, deck {5:0}%; ships with none coming in {6:0}%; pumps could beat it in {7:0}%",
                    100 * g.Sum(y => (double)y.At.Sea.Reachable) / Math.Max(tot, 1e-9), 100 * g.Sum(y => (double)y.At.Sea.NoAccess) / Math.Max(tot, 1e-9), 100 * g.Sum(y => (double)y.At.Sea.Worked) / Math.Max(tot, 1e-9), 100 * g.Sum(y => (double)y.At.Sea.Mat) / Math.Max(tot, 1e-9), 100 * g.Sum(y => (double)y.At.Sea.Beyond) / Math.Max(tot, 1e-9), 100 * g.Sum(y => (double)y.At.Sea.Deck) / Math.Max(tot, 1e-9),
                    100.0 * g.Count(y => y.At.Sea.All < 0.01) / g.Count(), 100.0 * g.Count(y => y.At.PumpTpm >= y.At.Sea.All) / g.Count()));
            }
        }

        Console.WriteLine("  causes against how she went (a seed):");
        foreach (var g in all.GroupBy(l => l.Group).OrderByDescending(g => g.Count()))
            Console.WriteLine($"    {g.Key,-22}" + string.Join(", ", g.GroupBy(l => l.Kind).OrderByDescending(k => k.Count()).Select(k => string.Format(Inv, "{0} {1:0.0}", k.Key, (double)k.Count() / n))));

        // falling out against being lost: what becomes of ships once they're out
        var ships = runs.SelectMany(r => r.Ships.Select(x => (Ship: x, Lost: r.Losses.Any(l => l.Ship == x)))).ToList();
        Console.WriteLine("  falling out and being lost (ships a seed; lost of them):");
        foreach (var g in ships.GroupBy(x => (x.Ship.Type, Fell: x.Ship.FellMs >= 0)).OrderBy(g => g.Key.Type).ThenBy(g => g.Key.Fell))
            Console.WriteLine(string.Format(Inv, "    {0,-10}{1,-14}{2,7:0.0}{3,7:0.0} ({4:0}%)", g.Key.Type, g.Key.Fell ? "fell out" : "never fell out", (double)g.Count() / n, (double)g.Count(x => x.Lost) / n, 100.0 * g.Count(x => x.Lost) / g.Count()));
        Console.WriteLine("  why they fell out (ships a seed; lost of them):");
        foreach (var g in ships.Where(x => x.Ship.FellMs >= 0).GroupBy(x => x.Ship.FellWhy).OrderByDescending(g => g.Count()))
            Console.WriteLine(string.Format(Inv, "    {0,-16}{1,7:0.0}{2,7:0.0} ({3:0}%)", g.Key, (double)g.Count() / n, (double)g.Count(x => x.Lost) / n, 100.0 * g.Count(x => x.Lost) / g.Count()));

        Console.WriteLine("  what the lost were doing when first struck (a seed; top 15):");
        foreach (var g in all.GroupBy(l => $"{l.Ship.Type} {Doing(l.First)}").OrderByDescending(g => g.Count()).Take(15))
            Console.WriteLine(string.Format(Inv, "    {0,-70}{1,6:0.0}", g.Key, (double)g.Count() / n));

        var fire = runs.SelectMany(r => r.Tally.Fire).GroupBy(kv => kv.Key).Select(g => (g.Key, Shells: g.Sum(kv => kv.Value.Shells) / n, Hits: g.Sum(kv => kv.Value.Hits) / n)).ToList();
        double firedAll = fire.Sum(x => x.Shells), hitAll = fire.Sum(x => x.Hits);
        Console.WriteLine(string.Format(Inv, "  where the fire went, a seed: {0:0} shells, {1:0} hits; {2} heavy hits, {3} hits that reached a magazine, all seeds. By range:", firedAll, hitAll, runs.Sum(r => r.Tally.HeavyHits), runs.Sum(r => r.Tally.MagazineHits)));
        foreach (var g in fire.GroupBy(x => x.Key[^8..].Trim()).OrderBy(g => g.Key))
            Console.WriteLine(string.Format(Inv, "    {0,-10}{1,8:0} shells{2,7:0} hits {3,5:0.0}%  ({4:0}% of the hits)", g.Key, g.Sum(x => x.Shells), g.Sum(x => x.Hits), 100 * g.Sum(x => x.Hits) / Math.Max(g.Sum(x => x.Shells), 1), 100 * g.Sum(x => x.Hits) / Math.Max(hitAll, 1)));
        Console.WriteLine("    the top rows (shells, hits, hit %):");
        foreach (var x in fire.OrderByDescending(x => x.Hits).Take(30))
            Console.WriteLine(string.Format(Inv, "    {0,-55}{1,8:0}{2,7:0} {3,5:0.0}%", x.Key, x.Shells, x.Hits, 100 * x.Hits / Math.Max(x.Shells, 1)));
        Console.WriteLine("  the fighting capital ships' nearest enemy heavy ship, by the quarter hour from the start (median km; ship-minutes a seed):");
        foreach (var g in runs.SelectMany(r => r.Tally.LineRange).GroupBy(kv => kv.Key).OrderBy(g => g.Key))
        {
            var l = g.SelectMany(kv => kv.Value).Order().ToList();
            Console.WriteLine(string.Format(Inv, "    +{0,3} min {1,6:0.0} km  ({2,5:0})", g.Key * 15, l[l.Count / 2] / 1000, (double)l.Count / n));
        }
        Console.WriteLine("  ship-minutes a seed with an enemy heavy ship within 30 km, top rows:");
        foreach (var g in runs.SelectMany(r => r.Tally.Minutes).GroupBy(kv => kv.Key).OrderByDescending(g => g.Sum(kv => kv.Value)).Take(40))
            Console.WriteLine(string.Format(Inv, "    {0,-78}{1,7:0}", g.Key, g.Sum(kv => kv.Value) / n));

        Console.WriteLine("  who struck her most in her last half hour, and from how far (a seed):");
        foreach (var g in all.GroupBy(l => $"{l.Ship.Type} by {l.TopShooter} at {Band(l.Range30Km)}").OrderByDescending(g => g.Count()).Take(15))
            Console.WriteLine(string.Format(Inv, "    {0,-50}{1,6:0.0}", g.Key, (double)g.Count() / n));
    }

    static string Band(double km) => double.IsNaN(km) ? "no shells" : (km < 2 ? " 0-2 km" : km < 4 ? " 2-4 km" : km < 6 ? " 4-6 km" : km < 10 ? "6-10 km" : km < 15 ? "10-15km" : "15+ km ").PadLeft(8);

    static double Med(IEnumerable<double> xs)
    {
        var l = xs.Where(double.IsFinite).OrderBy(x => x).ToList();
        return l.Count == 0 ? double.NaN : l[l.Count / 2];
    }

    static void WriteCsv(string path, IEnumerable<Played> runs)
    {
        using var f = new StreamWriter(path);
        f.WriteLine("scenario,seed,side,name,class,type,tonnes,group,kind,fate,end_min,first_hit_min,fell_min,fell_why,out_min,hits,hits_after_out,hits_30,last_hit_min,torpedoes,torpedo_share,near_misses,heavy,medium,light," +
                    "range30_km,range_all_km,top_shooter,shooters30,firing30,first_order,first_way,first_task,first_attack,first_enemy_m,first_heavy_m,first_enemy_t,first_friend_t,first_own_heavy_m,first_vis_m,gave_up_min,gone_min," +
                    "t30_kn,t30_speed,t30_guns,t30_water,t30_list,t10_kn,t10_speed,t10_guns,t10_water,t10_list,end_kn,end_speed,end_guns,end_water,end_list,end_gm,end_power,end_dark,end_water_t,end_holes,end_pumps,t10_water_t,t10_holes,t30_water_t,t30_holes");
        static string N(double v) => double.IsFinite(v) ? v.ToString("0.###", Inv) : "";
        foreach (var r in runs)
            foreach (var l in r.Losses)
                f.WriteLine(string.Join(",", l.Scenario, l.Seed, l.SideName, $"\"{l.Ship.Name}\"", $"\"{l.Ship.Class}\"", l.Ship.Type, N(l.Ship.Tonnes), l.Group, l.Kind, l.Fate, N(l.EndMin), N(l.FirstHitMin), N(l.FellMin),
                    l.Ship.FellWhy, N(l.OutMin), l.Hits, l.HitsAfterOut, l.Hits30, N(l.LastHitMin), l.Torpedoes, N(l.TorpedoShare), l.NearMisses, l.Heavy, l.Medium, l.Light, N(l.Range30Km), N(l.RangeAllKm), l.TopShooter, l.Shooters30, N(l.Firing30),
                    l.First.Order, l.First.Way, l.First.Task, l.First.Attack, N(l.First.EnemyM), N(l.First.HeavyM), N(l.First.EnemyT), N(l.First.FriendT), N(l.First.OwnHeavyM), N(l.First.VisM), N(l.GaveUpMin), N(l.GoneMin),
                    N(l.At30.Kn), N(l.At30.Speed), N(l.At30.Guns), N(l.At30.Water), N(l.At30.List), N(l.At10.Kn), N(l.At10.Speed), N(l.At10.Guns), N(l.At10.Water), N(l.At10.List),
                    N(l.Last.Kn), N(l.Last.Speed), N(l.Last.Guns), N(l.Last.Water), N(l.Last.List), N(l.Last.Gm), N(l.Last.Power), l.Last.Dark, N(l.Last.WaterT), N(l.Last.HoleM2), N(l.Last.Pumps), N(l.At10.WaterT), N(l.At10.HoleM2), N(l.At30.WaterT), N(l.At30.HoleM2)));
    }
}
