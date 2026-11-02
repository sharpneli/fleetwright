using System.Diagnostics;
using Fleetwright.Sim;
using Fleetwright.Sim.Ai;
using Fleetwright.Sim.Ai.Admirals;
using Fleetwright.Sim.Ai.Captains;
using Fleetwright.Sim.Combat;
using Fleetwright.Sim.Core;
using Fleetwright.Sim.Missions;
using Fleetwright.Sim.Ships;

namespace Fleetwright.Shipgen.Cli;

/// <summary><c>shipgen scenario X --play [--seeds N]</c> (docs/battle/command.md step 5.0): a scenario played to its end
/// with every side the AI's, headless, as fast as it goes. One seed prints its timeline and result; several print a line
/// each and the spread. The clock: while any encounter is open the asked speed is <paramref name="fine"/> (1x, the fine
/// tier, by default; <c>--tier coarse</c> asks <see cref="CoarseParity.CoarseSpeed"/>: an encounter opening drops to it at
/// the event, by the interest rule for others' encounters), and <paramref name="fast"/> otherwise. The speed asked only
/// picks the tiers here; the run goes at full tilt either way.</summary>
static class ScenarioPlay
{
    /// <summary>The fire table's "steady": under this rate of turn, °/s (BL1m).</summary>
    const double SteadyYawDegS = 0.2;

    public static int Run(string path, string settingsPath, string landPath, string designsPath, int threads, bool land,
        int seeds, double fast, double fine, bool? smoke = null, int workerCount = -1, ulong shuffle = 0, double spinUs = 250, bool fireTable = false,
        int[]? watch = null, int every = 10)
    {
        using var salvoLog = SalvoLog is { } logPath ? new StreamWriter(logPath) : null;
        salvoLog?.WriteLine("F,seed,t,shooter,battery,target,cal,range,shells,fallX,fallY,aimErr,corr,laid,phase,straddle,hits,pb,yaw,targetYaw");
        salvoLog?.WriteLine("S,seed,t,shooter,battery,call,lo,hi,next,phase,drift,possigma,tracker");
        // where the hits come from (docs/battle/balance.md BL1g): shells and hits by calibre, range and point blank, every seed
        var fire = new SortedDictionary<(bool Night, bool Small, int Cal, int Range, bool Pb), (long Shells, long Hits)>();
        // and inside 4 km, why the shooter was there: his captain's order and way of fighting (BL5)
        var close = new SortedDictionary<string, (long Shells, long Hits)>(StringComparer.Ordinal);
        var against = new SortedDictionary<string, (long Shells, long Hits)>(StringComparer.Ordinal);
        // the fire at ships going home (BL2's cripples, after BL5): who fires, from how near, closing or not; and each
        // cripple's hits taken going home and whether she got there
        var homeWho = new SortedDictionary<string, (long Shells, long Hits)>(StringComparer.Ordinal);
        var homeShips = new Dictionary<(ulong Seed, int Entity), (long Hits, bool Lost)>();
        // how ships came to fall out (balance.md's first principles): every hit on a ship still in the fight, and at her fall-out
        // what she was doing when they began, who hit her and from how far
        var fightHits = new Dictionary<(ulong Seed, int Entity), List<FightHit>>();
        var fellWhy = new SortedDictionary<string, (int Ships, long Hits, double Minutes)>(StringComparer.Ordinal);
        var fellBy = new SortedDictionary<string, (int Ships, long Hits, double Minutes)>(StringComparer.Ordinal);
        var noGunfire = new MainGuns.Tally();
        var attacks = new AttackTrace();
        int noGunfireHits = 0, noGunfireShips = 0;
        // ship-minutes inside 1 km of an enemy, by side and what she was doing (BL5: who closes in, and why)
        var inside = new SortedDictionary<string, long>(StringComparer.Ordinal);
        // the heavy guns' salvos laid by the sights (not point blank) by her rate of turn as she fired, and those fired under
        // helm by what she was doing (BL1m: where the turns while firing come from)
        var helm = new SortedDictionary<string, (long Shells, long Hits)>(StringComparer.Ordinal);
        var helmWhy = new SortedDictionary<string, (long Shells, long Hits)>(StringComparer.Ordinal);
        int[] calBands = [120, 200, 300], rangeBands = [2, 4, 6, 10, 15];
        string[] rangeNames = ["0-2 km", "2-4 km", "4-6 km", "6-10 km", "10-15 km", "15 km and over"];
        var settings = SimSettings.Load(settingsPath);
        if (smoke is bool on)
            settings = settings with { Smoke = on };   // docs/battle/smoke.md: --smoke or --no-smoke
        var s = Scenario.Load(path, [Path.Combine(designsPath, "..", "..", "Content", "Scenarios")]);   // a setup's "from" among the shipped
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
        // the sim's workers (docs/battle/workers.md): the host's, not the world's; --shuffle runs every job's chunks
        // in a seeded order on one thread, so a job that depends on its chunks' order shows in the hash
        int n = shuffle != 0 ? 0 : workerCount >= 0 ? workerCount : Workers.HeadlessCount;
        using var workers = new Workers(n, spinUs, shuffle);
        Console.WriteLine($"  {n} sim workers" + (shuffle != 0 ? $", chunks shuffled by {shuffle}" : "") + (n > 0 ? $", spinning {spinUs:G} µs" : ""));

        var runs = new List<(ulong Seed, ScenarioEnd End, SideResult[] Result, double Wall)>();
        var all = new MainGuns();
        int crashed = 0;
        for (int i = 0; i < seeds; i++)
        {
            ulong seed = s.Seed + (ulong)i;
            var (w, ai, start) = Begin(s, seed, settings, registry, classes, land ? shore : null, workers, fine);
            if (fireTable)
                w.Gunnery.Reported = r =>
                {
                    int ss = w.Ships.Store.SlotOf(r.Shooter);
                    if (ss < 0)
                        return;
                    double cal = w.Ships.Store[ss].Handling.Class.Batteries[r.Battery].CalibreMm;
                    // by day or night (the sun under 6°: civil twilight's end), at a small ship (under 2,000 t) or a larger
                    int ts = w.Ships.Store.SlotOf(r.Target);
                    bool night = Light.SunElevationDeg(w.Ships.Store[ss].Position, w.Time) < -6;
                    bool small = ts >= 0 && w.Ships.Store[ts].Handling.Class.Motion.FullDisplacementT < 2000;
                    var key = (night, small, calBands.Count(b => cal >= b), rangeBands.Count(b => r.TrueRangeM >= b * 1000), r.PointBlank);
                    var (sh, hi) = fire.GetValueOrDefault(key);
                    fire[key] = (sh + r.Shells, hi + r.Hits);
                    if (cal >= 200 && !r.PointBlank)
                    {
                        double yaw = Math.Abs(r.YawDegS);
                        string band = yaw < SteadyYawDegS ? "steady" : yaw < 0.5 ? "0.2-0.5 deg/s" : yaw < 1 ? "0.5-1 deg/s" : "over 1 deg/s";
                        var (hs, hh) = helm.GetValueOrDefault(band);
                        helm[band] = (hs + r.Shells, hh + r.Hits);
                        if (yaw >= SteadyYawDegS)
                        {
                            var (order, _, whose) = ai.Captains.Of(r.Shooter);
                            string why = $"{band}: {order} ({whose})";
                            if (w.Groups.UnitOf(r.Shooter) is { } top)
                            {
                                var (adm, _, _) = ai.Admirals.Of(top.GroupId);
                                why += $" / admiral {adm} / {(w.Groups.GuideShip(top) == r.Shooter ? "the guide" : "in the line")}";
                            }
                            var (ws, wh) = helmWhy.GetValueOrDefault(why);
                            helmWhy[why] = (ws + r.Shells, wh + r.Hits);
                        }
                    }
                    if (r.TrueRangeM < 4000)
                    {
                        var (order, _, whose) = ai.Captains.Of(r.Shooter);
                        var (att, fin, away) = ai.Captains.FightOf(r.Shooter);
                        string why = $"{(night ? "night" : "day")} {order}{(order == Fleetwright.Sim.Ai.Captains.CaptainOrder.Fight ? att ? " attacking" : fin ? " finishing" : away ? " away" : " in the band" : "")} ({whose})";
                        // in her line: what her admiral and his task have the line doing
                        if (w.Groups.UnitOf(r.Shooter) is { } top)
                        {
                            var task = ai.Tasks.Of(Fleetwright.Sim.Ai.Unit.Group(top.GroupId));
                            var (adm, _, _) = ai.Admirals.Of(top.GroupId);
                            var (phase, _, _) = ai.Admirals.AttackOf(top.GroupId);
                            why += $" / admiral {adm} / task {task.Kind} {task.Status}" + (phase != default ? $" / attack {phase}" : "");
                        }
                        var (cs, ch) = close.GetValueOrDefault(why);
                        close[why] = (cs + r.Shells, ch + r.Hits);
                        // the range against the shooter's band (0.6 of her chief guns' reach, the default doctrine's), the
                        // visibility there, and what her target's captain was doing (BL5: who closed)
                        var sc = w.Ships.Store[ss].Handling.Class;
                        int chief = -1;
                        for (int bi = 0; bi < sc.Batteries.Length && chief < 0; bi++)
                            chief = sc.Batteries[bi].Chief ? bi : -1;
                        double bandM = chief >= 0 ? 0.6 * w.Gunnery.ReachM(w.Ships.Store.Combat.Loadout(ss), chief) : double.NaN;
                        double vis = w.Weather.At(w.Ships.Store[ss].Position, w.Time).VisibilityM;
                        var (torder, _, _) = ai.Captains.Of(r.Target);
                        var (tatt, _, taway) = ai.Captains.FightOf(r.Target);
                        string tw = torder == Fleetwright.Sim.Ai.Captains.CaptainOrder.Fight ? tatt ? "attacking" : taway ? "away" : "fighting" : torder.ToString();
                        if (w.Groups.UnitOf(r.Target) is { } ttop)
                        {
                            var (tadm, _, _) = ai.Admirals.Of(ttop.GroupId);
                            var (tph, _, _) = ai.Admirals.AttackOf(ttop.GroupId);
                            tw += $" / her admiral {tadm}" + (tph != default ? $" {tph}" : "");
                        }
                        string k2 = $"{(night ? "night" : "day")} range/band {(r.TrueRangeM / bandM < 0.33 ? "<1/3" : r.TrueRangeM / bandM < 0.66 ? "1/3-2/3" : ">2/3")} vis {(vis < 6000 ? "<6 km" : vis < 12000 ? "6-12 km" : ">12 km")} / target {tw}";
                        var (as2, ah2) = against.GetValueOrDefault(k2);
                        against[k2] = (as2 + r.Shells, ah2 + r.Hits);
                    }
                    if (ts >= 0 && r.Hits > 0)
                        attacks.Hit(w, ai, seed, ts, r.Hits, night, ss, r.Battery);
                    if (ts >= 0 && r.Hits > 0 && ai.Captains.Of(r.Target).Order != Fleetwright.Sim.Ai.Captains.CaptainOrder.Home)
                    {
                        var her = w.Ships.Store[ts];
                        var (ho, _, _) = ai.Captains.Of(r.Target);
                        var (hatt, hfin, haway) = ai.Captains.FightOf(r.Target);
                        string way = ho == Fleetwright.Sim.Ai.Captains.CaptainOrder.Fight ? hatt ? "Fight attacking" : hfin ? "Fight finishing" : haway ? "Fight away" : "Fight in the band" : ho.ToString();
                        if (w.Groups.UnitOf(r.Target) is { } htop)
                            way += $" / admiral {ai.Admirals.Of(htop.GroupId).Item1} / task {ai.Tasks.Of(Fleetwright.Sim.Ai.Unit.Group(htop.GroupId)).Kind}";
                        bool answering = w.Time.Ms - w.Ships.Store.Combat.LastFired(ts) < 60_000;
                        var hk = (seed, r.Target);
                        if (!fightHits.TryGetValue(hk, out var list))
                            fightHits[hk] = list = [];
                        list.Add(new FightHit(r.FiredMs, Sightings.TypeOf(w.Ships.Store[ss].Handling.Class),
                            w.Ships.Store[ss].Handling.Class.Motion.FullDisplacementT / Math.Max(her.Handling.Class.Motion.FullDisplacementT, 1),
                            r.TrueRangeM, r.Hits, way, answering, night));
                    }
                    if (ts >= 0 && ai.Captains.Of(r.Target).Order == Fleetwright.Sim.Ai.Captains.CaptainOrder.Home)
                    {
                        var (so, _, _) = ai.Captains.Of(r.Shooter);
                        var (satt, sfin, saway) = ai.Captains.FightOf(r.Shooter);
                        string sw2 = so == Fleetwright.Sim.Ai.Captains.CaptainOrder.Fight ? satt ? "Fight attacking" : sfin ? "Fight finishing" : saway ? "Fight away" : "Fight in the band" : so.ToString();
                        if (w.Groups.UnitOf(r.Shooter) is { } stop)
                            sw2 += $" / admiral {ai.Admirals.Of(stop.GroupId).Item1} / task {ai.Tasks.Of(Fleetwright.Sim.Ai.Unit.Group(stop.GroupId)).Kind}";
                        var sh0 = w.Ships.Store[ss];
                        var th0 = w.Ships.Store[ts];
                        var u = th0.Position - sh0.Position;
                        double rate = Fleetwright.Sim.Core.Vec3d.Dot(th0.Forward * th0.Speed - sh0.Forward * sh0.Speed, u * (1 / Math.Max(u.Length, 1)));
                        // was there a better target for her: an enemy in sight of her side, still fighting (not going home),
                        // within this battery's reach
                        double reach = w.Gunnery.ReachM(w.Ships.Store.Combat.Loadout(ss), r.Battery);
                        bool better = false;
                        if (w.Sightings.Of(sh0.Side) is { } pic)
                            for (int ci = 0; ci < pic.Count && !better; ci++)
                            {
                                ref readonly var ct = ref pic[ci];
                                if (!ct.InSight || ct.Entity == r.Target || ct.Side == sh0.Side || !ct.Foe || w.Ships.Store.SlotOf(ct.Entity) < 0)
                                    continue;
                                better = ai.Captains.Of(ct.Entity).Order != Fleetwright.Sim.Ai.Captains.CaptainOrder.Home &&
                                         Earth.Distance(sh0.Position, ct.Position) < reach;
                            }
                        // what the shooter's side sees of her: her turrets seen silent, her guns quiet
                        string seenOf = "unseen";
                        if (w.Sightings.Of(sh0.Side) is { } pic2 && pic2.IndexOf(r.Target) is int ti and >= 0)
                        {
                            ref readonly var tc = ref pic2[ti];
                            double q = Math.Max(Math.Clamp((w.Ships.Store.NowMs - tc.QuietMs) / (1000 * settings.CrippleQuietS), 0, 1), tc.MountsQuiet);
                            seenOf = q < 0.25 ? "fighting" : q < 0.75 ? "half silent" : "silent";
                        }
                        string k3 = $"{(better ? "a fighting enemy in reach" : "no other in reach      ")} seen {seenOf,-11} {(night ? "night" : "day")} {rangeNames[rangeBands.Count(b => r.TrueRangeM >= b * 1000)],-8} {(rate < -1 ? "closing" : rate > 1 ? "opening" : "holding"),-7} " +
                                    $"{(th0.Speed < 0.6 * th0.Handling.Class.Motion.MaxSpeedKn * Fleetwright.Sim.Combat.Handling.MsPerKnot ? "she's slow" : "she's quick"),-10} / {sw2}";
                        var (hs3, hh3) = homeWho.GetValueOrDefault(k3);
                        homeWho[k3] = (hs3 + r.Shells, hh3 + r.Hits);
                        var hk = (seed, r.Target);
                        homeShips[hk] = (homeShips.GetValueOrDefault(hk).Hits + r.Hits, false);
                    }
                };
            if (salvoLog != null)
                LogSalvos(w, seed, salvoLog);
            foreach (var x in start.Warnings)
                Console.WriteLine($"  {x}");
            var names = new Dictionary<int, string>(start.ShipNames);
            var store = w.Ships.Store;
            for (int slot = 0; slot < store.Slots; slot++)
                if (store.IsAlive(slot) && !names.ContainsKey(store[slot].Entity))
                    names[store[slot].Entity] = $"{store[slot].Handling.Class.Name} #{store[slot].Entity}";

            var end = new ScenarioEnd(s, w, ai);
            var guns = new MainGuns();
            int seen = 0, seenFell = 0;
            var probes0 = (long[])w.Probes.Ticks.Clone();
            var calls0 = (long[])w.Probes.Calls.Clone();
            w.Probes.ResetMax();
            long alloc0 = GC.GetAllocatedBytesForCurrentThread() + workers.AllocatedBytes;
            int g0 = GC.CollectionCount(0), g1 = GC.CollectionCount(1), g2 = GC.CollectionCount(2);
            var sw = Stopwatch.StartNew();
            var hours = new List<double>();   // the wall clock at each game hour played: the speed hour by hour
            long hour0 = w.Time.Ms;
            try
            {
                PlayToEnd(w, end, fast, fine, () =>
                {
                    // --watch: the sides' aims and the ships named, every so many game minutes
                    if (watch != null && (w.Time.Ms - hour0) / 60_000 % every == 0)
                    {
                        Console.WriteLine($"  {w.Time.ToString()[11..16]} at {w.Speed:G}x asked");
                        ScenarioRun.Sides(w, ai, s);
                        foreach (int e in watch)
                            ScenarioRun.Ship(w, start, e);
                    }
                    if (fireTable)
                    {
                        Inside(w, ai, s, inside);
                        attacks.Sample(w, ai, seed);
                    }
                    if ((w.Time.Ms - hour0) / 3_600_000 > hours.Count)
                        hours.Add(sw.Elapsed.TotalSeconds);
                    guns.Read(w);
                    // a ship falling out for her main guns: why they're out, as she falls out
                    for (; seen < end.Timeline.Count; seen++)
                        if (end.Timeline[seen] is { Kind: PlayEvent.FellOut } l && (Fleetwright.Sim.Ai.Captains.CaptainReason)l.Count ==
                            Fleetwright.Sim.Ai.Captains.CaptainReason.OutGuns && w.Ships.Store.SlotOf(l.Entity) is int fs and >= 0)
                            guns.FellOut.Add(w.Ships.Store.Combat.Condition(fs));
                    if (fireTable)
                        for (; seenFell < end.Timeline.Count; seenFell++)
                            if (end.Timeline[seenFell] is { Kind: PlayEvent.FellOut } fl && w.Ships.Store.SlotOf(fl.Entity) is int fo and >= 0)
                            {
                                attacks.FellOut(w, ai, seed, fo);
                                if (fightHits.GetValueOrDefault((seed, fl.Entity)) is not { Count: > 0 })
                                {
                                    // no shell landed on her while she fought: what the damage model has on her
                                    var fc0 = w.Ships.Store.Combat.Condition(fo);
                                    noGunfire.Add(fc0);
                                    (noGunfireShips, noGunfireHits) = (noGunfireShips + 1, noGunfireHits + fc0.Hits);
                                }
                                FellOutWhy(w.Ships.Store[fo].Handling.Class, (Fleetwright.Sim.Ai.Captains.CaptainReason)fl.Count, fl.Ms,
                                    fightHits.GetValueOrDefault((seed, fl.Entity)), fellWhy, fellBy);
                            }
                });
                for (int slot = 0; slot < w.Ships.Store.Slots; slot++)
                    if (w.Ships.Store.IsAlive(slot))
                        guns.End.Add(w.Ships.Store.Combat.Condition(slot));
                foreach (var k in homeShips.Keys.Where(k => k.Seed == seed).ToList())
                    homeShips[k] = (homeShips[k].Hits, w.Ships.Store.SlotOf(k.Entity) < 0);
            }
            catch (Exception x) when (x is ArgumentException or InvalidOperationException or IndexOutOfRangeException or NullReferenceException)
            {
                // the sim broke: a finding of the bench, told in full, and the other seeds still run
                Console.WriteLine($"  seed {seed}: THE SIM BROKE at {w.Time} (asked {w.Speed:G}x): {x}");
                crashed++;
                continue;
            }
            double wall = sw.Elapsed.TotalSeconds;
            var cost = new PlayCost(probes0, calls0, GC.GetAllocatedBytesForCurrentThread() + workers.AllocatedBytes - alloc0,
                GC.CollectionCount(0) - g0, GC.CollectionCount(1) - g1, GC.CollectionCount(2) - g2);
            var result = end.Result();
            runs.Add((seed, end, result, wall));
            if (seeds == 1)
            {
                PrintOne(s, end, result, names, wall, w, ai, cost);
                if (hours.Count > 0)
                    Console.WriteLine("  the speed hour by hour: " + string.Join(" ", hours.Select((t, h) =>
                        $"{3600 / (t - (h > 0 ? hours[h - 1] : 0)):0}x")));
                guns.Print("  ");
                Console.WriteLine($"  the state at the end: {w.ComputeHash():x16}");
            }
            else
                Console.WriteLine($"  seed {seed,6}: {Why(end.Reason),-7} at {Clock(s, end.EndMs)}  " +
                                  string.Join("   ", result.Select(r => $"{r.Name} lost {r.Lost}/{r.Ships} ({r.LostTonnes:0} t){(r.AimsMet ? ", aims met" : "")}, score {r.Score:0.00}")) +
                                  Causes(end) + $"   {w.ComputeHash():x16}   ({wall:F1} s)" + (end.Missed > 0 ? $"   {end.Missed} log lines missed" : ""));
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
            (all.Shells, all.AtWrecks) = (all.Shells + guns.Shells, all.AtWrecks + guns.AtWrecks);
        }
        if (seeds > 1 && runs.Count > 0)
        {
            PrintSpread(s, runs.Select(r => (r.End, r.Result)).ToList());
            Console.WriteLine($"over {runs.Count} seeds:");
            all.Print("  ");
        }
        if (fireTable && fire.Count > 0)
        {
            string[] cals = ["under 120 mm", "120-200 mm", "200-300 mm", "300 mm and over"];
            string[] ranges = ["0-2 km", "2-4 km", "4-6 km", "6-10 km", "10-15 km", "15 km and over"];
            Console.WriteLine();
            Console.WriteLine($"where the hits came from, every seed (shells, hits, hit %; pb: laid point blank; at a ship under 2,000 t or larger):");
            foreach (var ((night, small, cal, range, pb), (sh, hi)) in fire)
                Console.WriteLine($"  {(night ? "night" : "day"),-6} {(small ? "small" : "large"),-6} {cals[cal],-16} {ranges[range],-15} {(pb ? "pb" : "  ")}  {sh,8} {hi,6} {100.0 * hi / Math.Max(sh, 1),6:F1}%");
            Console.WriteLine("inside 4 km, by the shooter's captain's order and his way of fighting (shells, hits):");
            foreach (var (why, (sh, hi)) in close.OrderByDescending(kv => kv.Value.Hits))
                Console.WriteLine($"  {why,-100} {sh,8} {hi,6}");
            long all0 = helm.Values.Sum(x => x.Shells);
            Console.WriteLine("the heavy guns' (200 mm and over) salvos laid by the sights, by her rate of turn as she fired (shells, share, hits, hit %):");
            foreach (var (band, (sh, hi)) in helm)
                Console.WriteLine($"  {band,-16} {sh,8} {100.0 * sh / Math.Max(all0, 1),5:F1}% {hi,6} {100.0 * hi / Math.Max(sh, 1),6:F1}%");
            Console.WriteLine("  under helm, by what she was doing (shells, hits):");
            foreach (var (why, (sh, hi)) in helmWhy.OrderByDescending(kv => kv.Value.Shells).Take(20))
                Console.WriteLine($"    {why,-98} {sh,8} {hi,6}");
            Console.WriteLine("ship-minutes inside 1 km of an enemy, by side and what she was doing:");
            foreach (var (why, count) in inside.OrderByDescending(kv => kv.Value).Take(30))
                Console.WriteLine($"  {why,-110} {count,6}");
            Console.WriteLine("inside 4 km, by the range against the shooter's band, the visibility and her target's way (shells, hits):");
            foreach (var (why, (sh, hi)) in against.OrderByDescending(kv => kv.Value.Hits).Take(25))
                Console.WriteLine($"  {why,-100} {sh,8} {hi,6}");
            attacks.Print();
            if (fellWhy.Count > 0)
            {
                Console.WriteLine("how ships came to fall out: her type, who landed most of the hits before she fell out (by their " +
                                  "displacement against hers) and from how far (ships, hits before, minutes from her first hit):");
                foreach (var (why, (ns, hi, mins)) in fellBy.OrderByDescending(kv => kv.Value.Ships))
                    Console.WriteLine($"  {why,-90} {ns,5} {hi,7} {mins / Math.Max(ns, 1),7:F1}");
                if (noGunfireShips > 0)
                    Console.WriteLine($"  the {noGunfireShips} with no shell landed while fighting: {noGunfireHits} hits on them by the damage model's count " +
                                      $"(torpedoes, mines, collisions); main mounts out: {noGunfire.Text()}");
                Console.WriteLine("  and what she was doing when the hits began, whether she was firing then, and why she fell out:");
                foreach (var (why, (ns, hi, mins)) in fellWhy.OrderByDescending(kv => kv.Value.Ships).Take(40))
                    Console.WriteLine($"    {why,-120} {ns,5} {hi,7} {mins / Math.Max(ns, 1),7:F1}");
            }
            if (homeShips.Count > 0)
            {
                var hl = homeShips.Values.Select(v => v.Hits).OrderBy(h => h).ToList();
                Console.WriteLine($"ships going home under fire: {homeShips.Count} ({homeShips.Values.Count(v => v.Lost)} lost by the end), {hl.Sum()} hits on them going home " +
                                  $"(median {hl[hl.Count / 2]}, the top quarter from {hl[3 * hl.Count / 4]}, the most {hl[^1]}); by range, closing, her speed and the shooter (shells, hits):");
                foreach (var yes in new[] { true, false })
                {
                    var part = homeWho.Where(kv => kv.Key.StartsWith("a fighting", StringComparison.Ordinal) == yes).ToList();
                    Console.WriteLine($"  {(yes ? "with a fighting enemy in the battery's reach" : "with none other in reach")}: {part.Sum(kv => kv.Value.Shells)} shells, {part.Sum(kv => kv.Value.Hits)} hits");
                }
                static string Who(string k) => k[..(k.IndexOf(" day ", StringComparison.Ordinal) is int di and > 0 ? di : k.IndexOf(" night ", StringComparison.Ordinal))];
                foreach (var g in homeWho.GroupBy(kv => Who(kv.Key))
                             .OrderByDescending(g => g.Sum(kv => kv.Value.Hits)))
                    Console.WriteLine($"    {g.Key,-50} {g.Sum(kv => kv.Value.Shells),8} {g.Sum(kv => kv.Value.Hits),6}");
                foreach (var (why, (sh, hi)) in homeWho.OrderByDescending(kv => kv.Value.Hits).Take(30))
                    Console.WriteLine($"  {why,-130} {sh,8} {hi,6}");
            }
        }
        if (crashed > 0)
            Console.WriteLine($"the sim broke in {crashed} of {seeds} seeds");
        return crashed > 0 ? 1 : 0;
    }

    /// <summary>The flotillas' torpedo attacks as they went (the fire table; balance.md "How ships come to fall out"): each
    /// attack from its gain to its end, sampled every 5 s of game time (its phase, its guide's range to the target, to the
    /// enemy's nearest heavy ship and his nearest torpedo craft), the hits and fall-outs among its boats by phase, the range
    /// at the signal and how it ended; and every hit on a torpedo craft by what her unit was doing, with her distance to her
    /// own side's nearest heavy ship and to the enemy's.</summary>
    sealed class AttackTrace
    {
        sealed class Run
        {
            public long StartMs, LastMs;
            public int Boats;
            public bool Night;
            public AttackPhase Last;
            public double MinTarget = double.PositiveInfinity, MinHeavy = double.PositiveInfinity, MinBoat = double.PositiveInfinity, FireM = double.NaN;
            public double GainS, RunS, AwayS;
            public readonly int[] Hits = new int[5], Fell = new int[5];
            public string End = "";
        }

        readonly Dictionary<(ulong, int), Run> active = [];
        readonly List<Run> done = [];
        readonly SortedDictionary<string, (long Hits, List<double> Own, List<double> Heavy, List<double> Boat)> onBoats = new(StringComparer.Ordinal);
        long lastMs;
        readonly HashSet<int> seenGroups = [];

        static bool Heavy(ShipType t) => t is ShipType.Capital or ShipType.Cruiser;
        static bool Boat(ShipType t) => t is ShipType.Destroyer or ShipType.Small;

        /// <summary>The nearest live ship of a kind to a point, on her side (own) or the other's, m.</summary>
        static double Nearest(ShipStore store, Vec3d p, int side, bool own, Func<ShipType, bool> kind)
        {
            double best = double.PositiveInfinity;
            for (int k = 0; k < store.Slots; k++)
                if (store.IsAlive(k) && (store[k].Side == side) == own && kind(Sightings.TypeOf(store[k].Handling.Class)))
                    best = Math.Min(best, (store[k].Position - p).Length);
            return best;
        }

        public void Sample(World w, AiSystem ai, ulong seed)
        {
            if (w.Time.Ms - lastMs < 5_000)
                return;
            lastMs = w.Time.Ms;
            var store = w.Ships.Store;
            seenGroups.Clear();
            for (int slot = 0; slot < store.Slots; slot++)
            {
                if (!store.IsAlive(slot) || !Boat(Sightings.TypeOf(store[slot].Handling.Class)) || w.Groups.UnitOf(store[slot].Entity) is not { } g ||
                    !seenGroups.Add(g.GroupId))
                    continue;
                var (phase, target, _) = ai.Admirals.AttackOf(g.GroupId);
                var key = (seed, g.GroupId);
                active.TryGetValue(key, out var run);
                if (phase == AttackPhase.None)
                {
                    if (run != null)
                        Close(key, run, ai.Admirals.Of(g.GroupId).Item1 == CaptainOrder.Run ? "beaten off (run)" :
                            run.Last == AttackPhase.Away ? "fired and away" : $"stopped in {run.Last}");
                    continue;
                }
                if (run == null)
                    active[key] = run = new Run { StartMs = w.Time.Ms, Boats = g.Line.Count, Night = Light.SunElevationDeg(store[slot].Position, w.Time) < -6 };
                int gs = store.SlotOf(w.Groups.GuideShip(g));
                if (gs < 0)
                    continue;
                var guide = store[gs];
                double dt = (w.Time.Ms - Math.Max(run.LastMs, run.StartMs)) / 1000.0;
                switch (run.Last)
                {
                    case AttackPhase.Gain: run.GainS += dt; break;
                    case AttackPhase.Run: run.RunS += dt; break;
                    case AttackPhase.Away: run.AwayS += dt; break;
                }
                if (store.SlotOf(target) is int tslot and >= 0)
                {
                    double rt = (store[tslot].Position - guide.Position).Length;
                    run.MinTarget = Math.Min(run.MinTarget, rt);
                    if (phase == AttackPhase.Fire && double.IsNaN(run.FireM))
                        run.FireM = rt;
                }
                if (phase is AttackPhase.Gain or AttackPhase.Run)
                {
                    run.MinHeavy = Math.Min(run.MinHeavy, Nearest(store, guide.Position, guide.Side, false, Heavy));
                    run.MinBoat = Math.Min(run.MinBoat, Nearest(store, guide.Position, guide.Side, false, Boat));
                }
                (run.Last, run.LastMs) = (phase, w.Time.Ms);
            }
        }

        void Close((ulong, int) key, Run run, string end)
        {
            run.End = end;
            done.Add(run);
            active.Remove(key);
        }

        public void Hit(World w, AiSystem ai, ulong seed, int slot, int hits, bool night, int shooter, int battery)
        {
            var store = w.Ships.Store;
            var her = store[slot];
            if (!Boat(Sightings.TypeOf(her.Handling.Class)))
                return;
            string way = "alone";
            if (w.Groups.UnitOf(her.Entity) is { } g)
            {
                var (adm, _, _) = ai.Admirals.Of(g.GroupId);
                int phase = (int)ai.Admirals.AttackOf(g.GroupId).Phase;
                way = $"admiral {adm} / task {ai.Tasks.Of(Fleetwright.Sim.Ai.Unit.Group(g.GroupId)).Kind}" +
                      (phase != 0 ? $" / attack {(AttackPhase)phase}" : "");
                // whom her unit's guide fights, and what landed the hits
                int foe = ai.Captains.FightEnemyOf(w.Groups.GuideShip(g));
                if (foe != 0 && store.SlotOf(foe) is int fs and >= 0)
                    way += $" / its guide fights a {Sightings.TypeOf(store[fs].Handling.Class)}";
                var sc = store[shooter].Handling.Class;
                way += $" / hit by a {Sightings.TypeOf(sc)}'s {(sc.Batteries[battery].Chief ? "chief" : "secondary")} guns";
                if (active.TryGetValue((seed, g.GroupId), out var run))
                    run.Hits[phase] += hits;
            }
            if (ai.Captains.Of(her.Entity).Order == CaptainOrder.Home)
                way = "going home";
            string k = $"{(night ? "night" : "day")} {way}";
            if (!onBoats.TryGetValue(k, out var e))
                onBoats[k] = e = (0, [], [], []);
            onBoats[k] = (e.Hits + hits, e.Own, e.Heavy, e.Boat);
            e.Own.Add(Nearest(store, her.Position, her.Side, true, Heavy));
            e.Heavy.Add(Nearest(store, her.Position, her.Side, false, Heavy));
            e.Boat.Add(Nearest(store, her.Position, her.Side, false, Boat));
        }

        public void FellOut(World w, AiSystem ai, ulong seed, int slot)
        {
            // she's left her line as she falls out: her group's top unit
            if (w.Groups.Of(w.Ships.Store[slot].Entity) is { } own && w.Groups.TopOf(own) is var g && active.TryGetValue((seed, g.GroupId), out var run))
                run.Fell[(int)ai.Admirals.AttackOf(g.GroupId).Phase]++;
        }

        static string Km(IEnumerable<double> xs)
        {
            var l = xs.Where(double.IsFinite).OrderBy(x => x).ToList();
            return l.Count == 0 ? "   -" : $"{l[l.Count / 2] / 1000,4:F1}";
        }

        static double Med(IEnumerable<double> xs)
        {
            var l = xs.OrderBy(x => x).ToList();
            return l.Count == 0 ? double.NaN : l[l.Count / 2];
        }

        public void Print()
        {
            foreach (var (k, r) in active.ToList())
                Close(k, r, $"still on in {r.Last} at the end");
            Console.WriteLine("the flotillas' torpedo attacks, every seed (attacks, boats; how they ended; the medians of the seconds gaining and " +
                              "running in, the guide's nearest to the target, and to an enemy heavy ship and an enemy boat while gaining and running, " +
                              "the range at the signal; the hits on its boats and the boats fallen out, gaining/running/firing/away):");
            foreach (var night in new[] { false, true })
            {
                var rs = done.Where(r => r.Night == night).ToList();
                if (rs.Count == 0)
                    continue;
                Console.WriteLine($"  {(night ? "night" : "day")}: {rs.Count} attacks, {rs.Sum(r => r.Boats)} boats; " +
                                  string.Join(", ", rs.GroupBy(r => r.End).OrderByDescending(g => g.Count()).Select(g => $"{g.Key} {g.Count()}")));
                Console.WriteLine($"    gaining {Med(rs.Select(r => r.GainS)),5:0} s, running {Med(rs.Select(r => r.RunS)),5:0} s; nearest the target {Km(rs.Select(r => r.MinTarget))}, " +
                                  $"an enemy heavy ship {Km(rs.Select(r => r.MinHeavy))}, an enemy boat {Km(rs.Select(r => r.MinBoat))} km; the signal at {Km(rs.Select(r => r.FireM))} km " +
                                  $"({rs.Count(r => double.IsFinite(r.FireM))} made it)");
                Console.WriteLine($"    hits {string.Join("/", Enumerable.Range(1, 4).Select(i => rs.Sum(r => r.Hits[i])))}, " +
                                  $"fallen out {string.Join("/", Enumerable.Range(1, 4).Select(i => rs.Sum(r => r.Fell[i])))}");
            }
            Console.WriteLine("hits on torpedo craft, by what her unit was doing (hits; the medians of her distance to her own side's nearest heavy ship, " +
                              "to the enemy's nearest heavy ship and to his nearest boat, km):");
            foreach (var (k, e) in onBoats.OrderByDescending(kv => kv.Value.Hits).Take(25))
                Console.WriteLine($"  {k,-80} {e.Hits,6}  own {Km(e.Own)}  enemy heavy {Km(e.Heavy)}  enemy boat {Km(e.Boat)}");
        }
    }

    /// <summary>A hit on a ship still in the fight (the fire table's fall-out breakdown): when, the shooter's type and her
    /// displacement over the target's, the range, the hits, what the target was doing, whether she'd fired in the last
    /// minute, by night.</summary>
    readonly record struct FightHit(long Ms, ShipType Shooter, double Heavier, double RangeM, int Hits, string Way, bool Answering, bool Night);

    /// <summary>A ship fallen out, into the fall-out breakdown: who landed most of her hits (by type and weight against
    /// hers), the hits' median range, and what she was doing when they began.</summary>
    static void FellOutWhy(ShipClass cls, Fleetwright.Sim.Ai.Captains.CaptainReason reason, long ms, List<FightHit>? hits,
        SortedDictionary<string, (int Ships, long Hits, double Minutes)> why, SortedDictionary<string, (int Ships, long Hits, double Minutes)> by)
    {
        string her = Sightings.TypeOf(cls).ToString();
        if (hits == null || hits.Count == 0)
        {
            Add(why, $"{her} {reason}: no gunfire hits (torpedoes, a collision, or the sea)", 0, 0);
            Add(by, $"{her}: no gunfire hits", 0, 0);
            return;
        }
        static string Weight(double h) => h >= 2 ? "heavier" : h > 0.5 ? "her match" : "lighter";
        var top = hits.GroupBy(x => (x.Shooter, Weight(x.Heavier))).OrderByDescending(g => g.Sum(x => x.Hits)).First().Key;
        var ranges = hits.SelectMany(x => Enumerable.Repeat(x.RangeM, x.Hits)).OrderBy(x => x).ToList();
        double r = ranges[ranges.Count / 2] / 1000;
        string band = r < 2 ? "0-2 km" : r < 4 ? "2-4 km" : r < 6 ? "4-6 km" : r < 10 ? "6-10 km" : r < 15 ? "10-15 km" : "15 km and over";
        int n = hits.Sum(x => x.Hits);
        double minutes = (ms - hits[0].Ms) / 60_000.0;
        var first = hits[0];
        Add(by, $"{her} by {top.Item2} {top.Shooter} at {band}", n, minutes);
        Add(why, $"{her} {(first.Night ? "night" : "day")} {first.Way} {(first.Answering ? "(firing)" : "(not firing)")} -> {reason}", n, minutes);

        static void Add(SortedDictionary<string, (int Ships, long Hits, double Minutes)> d, string k, long h, double m)
        {
            var (a, b, c) = d.GetValueOrDefault(k);
            d[k] = (a + 1, b + h, c + m);
        }
    }

    /// <summary>A seed's world with the scenario in it and the AI on every side (the player's side none), an encounter
    /// opening dropping the asked speed to <paramref name="encounterSpeed"/> at the event (the interest rule for others'
    /// encounters): 1x the fine tier, <see cref="CoarseParity.CoarseSpeed"/> the coarse one.</summary>
    public static (World World, AiSystem Ai, ScenarioStart Start) Begin(Scenario s, ulong seed, SimSettings settings, ShipClassRegistry registry,
        IReadOnlyDictionary<string, int> classes, Shore? shore, Workers workers, double encounterSpeed)
    {
        var w = new World(s.Start, seed, settings, registry, shore) { Workers = workers };
        var ai = new AiSystem(w);
        var start = ScenarioLoader.Apply(s, w, ai, classes);
        w.Apply(Command.SetPlayerSide(-1));   // watching: every side the AI's
        w.Apply(Command.SetInterest(InterestKind.Others, InterestAction.Slow, encounterSpeed));
        return (w, ai, start);
    }

    /// <summary>Plays a world to its scenario's end a game minute at a time: the asked speed <paramref name="encounterSpeed"/>
    /// while any encounter is open, <paramref name="fast"/> otherwise (the speed only picks the tiers; the run goes at full
    /// tilt), and <paramref name="minute"/> told after each minute.</summary>
    public static void PlayToEnd(World w, ScenarioEnd end, double fast, double encounterSpeed, Action? minute = null)
    {
        while (!end.Check())
        {
            double want = w.Encounters.Count > 0 ? encounterSpeed : fast;
            if (w.Speed != want)
                w.Apply(Command.SetSpeed(want));
            w.AdvanceTo(w.Time + 60_000);
            minute?.Invoke();
            if (Hashes)
                Console.WriteLine($"  hashes {w.Time}: " + string.Join(" ", w.HashParts().Select(p => $"{p.Part}={p.Hash:x16}")));
        }
    }

    /// <summary>Every game minute, each part of the state hash (<c>--hashes</c>): where two runs first differ.</summary>
    public static bool Hashes;

    /// <summary>A CSV of every salvo and its spot (<c>--salvo-log FILE</c>): where the falls stood against the target
    /// salvo by salvo, the correction and the call it drew, to see whether the spotting walks the fall onto her.</summary>
    public static string? SalvoLog;

    static void LogSalvos(World w, ulong seed, TextWriter log)
    {
        var prev = w.Gunnery.Reported;
        // its spot, a line of its own six seconds on: S, the salvo's time, whose, the call, the correction it leaves
        w.Gunnery.Spotted = p => log.WriteLine(string.Join(",", "S", seed, p.FiredMs / 1000.0, p.Shooter, p.Battery, p.Seen ? p.Sign.ToString() : "unseen",
            Math.Round(p.Lo), Math.Round(p.Hi), Math.Round(p.Next), p.Phase,
            Math.Round(p.DriftMs, 2), Math.Round(p.PosSigmaM), (TrackerKind)p.Tracker));
        w.Gunnery.Reported = r =>
        {
            prev?.Invoke(r);
            var store = w.Ships.Store;
            int ss = store.SlotOf(r.Shooter), ts = store.SlotOf(r.Target);
            if (ss < 0 || ts < 0)
                return;
            log.WriteLine(string.Join(",", "F", seed, r.FiredMs / 1000.0, r.Shooter, r.Battery, r.Target, store[ss].Handling.Class.Batteries[r.Battery].CalibreMm,
                Math.Round(r.TrueRangeM), r.Shells, Math.Round(r.MeanX), Math.Round(r.MeanY), Math.Round(r.AimErrM), Math.Round(r.CorrR), Math.Round(r.LaidErrM),
                r.Phase, r.Straddle ? 1 : 0, r.Hits, r.PointBlank ? 1 : 0, Math.Round(r.YawDegS, 2),
                Math.Round(double.RadiansToDegrees(store[ts].YawRate), 2)));
        };
    }

    /// <summary>Why ships' main guns are out (the user's question, 2026-10-20: "a lot of ships with disabled main guns"):
    /// their chief batteries' mounts not working, each by its first cause (the magazine, the gunhouse, the barbette, a jam,
    /// its guns), at the moment a ship falls out for her main guns and at the end of the run for every ship afloat.</summary>
    sealed class MainGuns
    {
        public readonly Tally FellOut = new(), End = new();
        /// <summary>The ship-wide blackouts (E4's cascade, E7's near misses), and the ships blacked out at least once.</summary>
        public int HitBlackouts, NearBlackouts, ShipsBlacked;
        /// <summary>The shells fired, and those at a ship her shooter's side saw as mostly done for then (docs/battle/crew.md
        /// N5b: <c>Contact.DoneFor</c> at <see cref="SimSettings.DoneForCease"/> or more, as of the log's reading).</summary>
        public long Shells, AtWrecks;
        readonly HashSet<int> blacked = [];
        long read;

        public void Read(World w)
        {
            var log = w.CombatLog;
            for (long i = Math.Min(log.Written - read, log.Count) - 1; i >= 0; i--)
            {
                var e = log.Newest((int)i);
                if (e.Kind == CombatEvent.Fired)
                {
                    Shells += e.Count;
                    if (w.Ships.Store.SlotOf(e.Shooter) is int ss and >= 0 && w.Sightings.Of(w.Ships.Store[ss].Side) is { } pic &&
                        pic.IndexOf(e.Target) is int ci and >= 0 && pic[ci].DoneFor(w.Time.Ms, w.Settings) >= w.Settings.DoneForCease)
                        AtWrecks += e.Count;
                    continue;
                }
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
            Console.WriteLine($"{indent}shells: {Shells} fired, {AtWrecks} at ships seen done for");
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

    /// <summary>Each ship afloat inside 1 km of an enemy this minute, tallied by her side, day or night and what she's doing:
    /// her captain's order and way of fighting and reason, her line's admiral's order and attack phase, her unit's task
    /// (docs/battle/balance.md BL5). Enemies found through a grid of 2 km cells, so linear in ships (a tool's, built each
    /// minute).</summary>
    static void Inside(World w, AiSystem ai, Scenario s, SortedDictionary<string, long> inside)
    {
        var store = w.Ships.Store;
        var cells = new Dictionary<(int, int), List<int>>();
        (int, int) Cell(Vec3d p)
        {
            var (la, lo) = Earth.LatLon(p);
            return ((int)Math.Floor(la / 0.018), (int)Math.Floor(lo * Math.Cos(double.DegreesToRadians(la)) / 0.018));
        }
        for (int slot = 0; slot < store.Slots; slot++)
            if (store.IsAlive(slot) && Fleetwright.Sim.Ai.Commanders.Aims.Afloat(store, slot))
            {
                var c = Cell(store[slot].Position);
                if (!cells.TryGetValue(c, out var l))
                    cells[c] = l = [];
                l.Add(slot);
            }
        foreach (var l in cells.Values)
            foreach (int slot in l)
            {
                var me = store[slot];
                var (ci, cj) = Cell(me.Position);
                int nearest = -1;
                double nd = 1000;
                for (int di = -1; di <= 1; di++)
                    for (int dj = -1; dj <= 1; dj++)
                        if (cells.TryGetValue((ci + di, cj + dj), out var o))
                            foreach (int other in o)
                                if (store[other].Side != me.Side && Earth.Distance(me.Position, store[other].Position) is var d && d < nd)
                                    (nearest, nd) = (other, d);
                if (nearest < 0)
                    continue;
                // who's closing: her own way toward him, his toward her (m/s along the line between them)
                var en = store[nearest];
                var u = (en.Position - me.Position) * (1 / Math.Max((en.Position - me.Position).Length, 1));
                double mine = Vec3d.Dot(me.Forward * me.Speed, u), his = -Vec3d.Dot(en.Forward * en.Speed, u);
                string closing = mine > 1 && his > 1 ? "both closing" : mine > 1 ? "she closes" : his > 1 ? "he closes" : "neither";
                bool night = Light.SunElevationDeg(me.Position, w.Time) < -6;
                var (order, _, whose) = ai.Captains.Of(me.Entity);
                var (att, fin, away) = ai.Captains.FightOf(me.Entity);
                string why = $"{s.Sides[me.Side]} {(night ? "night" : "day")} [{closing}]: {order}" +
                             (order == Fleetwright.Sim.Ai.Captains.CaptainOrder.Fight ? att ? " attacking" : fin ? " finishing" : away ? " away" : " in the band" : "") +
                             $" ({whose}, {ai.Captains.ReasonOf(me.Entity)})";
                if (w.Groups.UnitOf(me.Entity) is { } top)
                {
                    var task = ai.Tasks.Of(Fleetwright.Sim.Ai.Unit.Group(top.GroupId));
                    var (adm, _, _) = ai.Admirals.Of(top.GroupId);
                    var (phase, _, _) = ai.Admirals.AttackOf(top.GroupId);
                    why += $" / admiral {adm}" + (phase != default ? $" {phase}" : "") + $" / task {task.Kind}";
                }
                inside[why] = inside.GetValueOrDefault(why) + 1;
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

    /// <summary>The play loop's cost: the probes before it, and what it allocated and collected (docs/profiling.md).</summary>
    sealed record PlayCost(long[] Ticks0, long[] Calls0, long Allocated, int Gen0, int Gen1, int Gen2);

    static void PrintOne(Scenario s, ScenarioEnd end, SideResult[] result, Dictionary<int, string> names, double wall, World w, AiSystem ai, PlayCost cost)
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
                PlayEvent.GaveUp => $"{SideName(l.Side)}'s {Name(l.Entity)}: " + ((Fleetwright.Sim.Combat.GivenUp)l.Count switch
                {
                    var g when (g & Fleetwright.Sim.Combat.GivenUp.Scuttled) != 0 => "SCUTTLED, her men ordered off",
                    var g when (g & Fleetwright.Sim.Combat.GivenUp.ColoursDown) != 0 && (g & Fleetwright.Sim.Combat.GivenUp.Abandoned) != 0 => "struck, her men ordered off",
                    var g when (g & Fleetwright.Sim.Combat.GivenUp.ColoursDown) != 0 => "STRIKES HER COLOURS",
                    _ => "ABANDONED",
                }),
                _ => l.Kind.ToString(),
            });
        Console.WriteLine();
        Console.WriteLine($"the result ({Why(end.Reason)} at {Clock(s, end.EndMs)}, played in {wall:F1} s):");
        foreach (var r in result)
            Console.WriteLine($"  {r.Name,-12} {r.Ships,4} ships {r.Tonnes,9:0} t   lost {r.Lost,3} ({r.LostTonnes,7:0} t)   damaged afloat {r.Damaged,3}   " +
                              $"afloat {r.Afloat,3}   hits made {r.Hits,4}   torpedoes {r.Torpedoes,3}   aims {(r.AimsMet ? "met" : "not met")}, score {r.Score:0.00}" +
                              (r.Taken + r.Struck + r.Scuttled + r.Abandoned > 0 ? $"   of the lost: taken {r.Taken}, struck {r.Struck}, scuttled {r.Scuttled}, abandoned {r.Abandoned}" : "") +
                              $"   men lost {r.Killed}, saved {r.Saved}");
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
        // and where the play's time went: every probe, the systems' events and the parts timed inside them (docs/profiling.md)
        Console.WriteLine($"where the time went ({cost.Allocated / 1048576.0:F1} MB allocated, gen0/1/2 {cost.Gen0}/{cost.Gen1}/{cost.Gen2}; " +
                          "share of the run, calls a game second, each, longest):");
        for (int i = 0; i < w.Probes.Count; i++)
        {
            long calls = w.Probes.Calls[i] - cost.Calls0[i];
            if (calls <= 0)
                continue;
            double ms = Probes.Ms(w.Probes.Ticks[i] - cost.Ticks0[i]);
            Console.WriteLine($"  {w.Probes.Names[i],-26} {ms / 1000 / wall,7:P2}  {calls / game,9:F3}  {ms * 1000 / calls,9:F1} µs   ({Probes.Ms(w.Probes.Max[i]):F2} ms)");
        }
    }

    /// <summary>What the "done" end waits on (command.md "The end"): each side's phase, and its ships afloat not yet home,
    /// those in an encounter (which keep it from done, unless dead in the water), stopped, and the farthest from her side's
    /// home.</summary>
    static void DoneLines(Scenario s, World w, AiSystem ai, string indent)
    {
        var store = w.Ships.Store;
        for (int side = 0; side < s.Sides.Length; side++)
        {
            var (phase, reason) = ai.Commanders.PhaseOf(side);
            int afloat = 0, away = 0, inContact = 0, stopped = 0, dead = 0;
            double farthest = 0;
            for (int slot = 0; slot < store.Slots; slot++)
            {
                if (!store.IsAlive(slot) || store[slot].Side != side || !Fleetwright.Sim.Ai.Commanders.Aims.Afloat(store, slot))
                    continue;
                afloat++;
                if (ai.Homes.There(side, store[slot].Entity, store[slot].Position, w.Settings.HomeRadiusM))
                    continue;   // home as the judge has it: her side's, or the zone she's at
                double d = ai.HomeOf(side) is { } at ? Earth.Distance(store[slot].Position, at) : double.PositiveInfinity;
                away++;
                if (store.PowerCapOf(slot) <= 0)
                    dead++;
                farthest = Math.Max(farthest, d);
                if (w.Encounters.Of(slot, store[slot].Entity) != 0)
                    inContact++;
                if (store[slot].Speed < 0.5)
                    stopped++;
            }
            Console.WriteLine($"{indent}{s.Sides[side],-12} done? {Words(phase.ToString())}" + (reason != 0 ? $" ({Words(reason.ToString())})" : "") +
                              $"; {afloat - away} of {afloat} afloat home, {away} not ({inContact} in an encounter, {stopped} stopped, {dead} dead in the water" +
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
                              $"{tonnes.Average():0} t; made {runs.Average(r => r.Result[side].Hits):F0} hits, fired {runs.Average(r => r.Result[side].Torpedoes):F1} torpedoes; aims met {runs.Count(r => r.Result[side].AimsMet)} times, score {runs.Average(r => r.Result[side].Score):0.00}");
        }
    }

    /// <summary>An enum's name in words: "NoContact" as "no contact".</summary>
    static string Words(string name) => string.Concat(name.Select((ch, i) => char.IsUpper(ch) && i > 0 ? " " + char.ToLowerInvariant(ch) : char.ToLowerInvariant(ch).ToString()));

    public static string Why(EndReason r) => r switch
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
