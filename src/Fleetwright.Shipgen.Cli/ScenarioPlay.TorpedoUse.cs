using Fleetwright.Sim;
using Fleetwright.Sim.Ai;
using Fleetwright.Sim.Ai.Captains;
using Fleetwright.Sim.Combat;
using Fleetwright.Sim.Core;

namespace Fleetwright.Shipgen.Cli;

static partial class ScenarioPlay
{
    /// <summary><c>--torpedo-use</c> (docs/battle/torpedo-use.md TU0): every spread fired, by why it was fired (the captain's
    /// path, and for an attack who ordered it), what she fired at against what she is, whether the matchup has her
    /// torpedoes as the better weapon against the target, and whether such a target was near when she fired at another;
    /// the hits each spread made. TU_CSV=FILE appends a line a spread, for the panel's totals.</summary>
    public static bool TorpedoUse;

    sealed class TorpedoUseTally
    {
        sealed class Spread
        {
            public string Scenario = "", Why = "", Task = "";
            public ulong Seed;
            public ShipType Shooter, Target;
            public double TargetT, RangeM, Chance, Better;
            public bool Night, BetterNear;
            public int Count, Hits, Others, Aimed;
        }

        readonly List<Spread> spreads = [];
        readonly Dictionary<(ulong, int, int), Spread> last = [];
        CombatLog? log;
        long read, missed;
        ulong seed;
        public string Scenario = "";

        public void Hook(World w, AiSystem ai, ulong seed)
        {
            this.seed = seed;
            (log, read) = (w.CombatLog, w.CombatLog.Written);
            var prev = w.Torpedoes.Launched;
            w.Torpedoes.Launched = world =>
            {
                prev?.Invoke(world);
                ref readonly var e = ref world.CombatLog.Newest(0);
                if (e.Kind == CombatEvent.Torpedoes)
                    Fired(world, ai, e);
            };
        }

        void Fired(World w, AiSystem ai, in CombatEntry e)
        {
            var store = w.Ships.Store;
            if (store.SlotOf(e.Shooter) is not (int ss and >= 0) || store.SlotOf(e.Target) is not (int ts and >= 0))
                return;
            var sh = store[ss];
            var tg = store[ts];
            var (why, chance) = ai.Captains.LaunchOf(e.Shooter);
            string by = why.ToString();
            if (why == TorpedoWhy.Signal && w.Groups.Of(e.Shooter) is { } g)
                by = ai.Admirals.AttackOrderedOf(g.GroupId) ? $"Signal: ordered ({ai.Admirals.Of(g.GroupId).Whose})" : "Signal: by doctrine";
            else if (why == TorpedoWhy.OrderedAttack)
                by = $"OrderedAttack ({ai.Captains.Of(e.Shooter).Whose})";
            string task = "alone";
            if (w.Groups.Of(e.Shooter) is { } sq)
            {
                var k = ai.Tasks.Of(Unit.Group(sq.GroupId)).Kind;
                var top = w.Groups.UnitOf(e.Shooter);
                var tk = top != null && top.GroupId != sq.GroupId ? ai.Tasks.Of(Unit.Group(top.GroupId)).Kind : k;
                task = k == tk ? k.ToString() : $"{k} in {tk}";
            }
            var pic = w.Sightings.Of(sh.Side);
            var profile = ai.Judge.Matchups.Profile(w, store.Combat.Loadout(ss));
            double better = pic != null && pic.IndexOf(e.Target) is int ci and >= 0 ? ai.Judge.AttackerShare(w, pic[ci], profile, sh.Side, theyAttack: false) : double.NaN;
            bool near = false;
            if (pic != null && !(better >= 0.5))
                for (int i = 0; i < pic.Count && !near; i++)
                    near = pic[i].Foe && !pic[i].Out && pic[i].Entity != e.Target && (pic[i].Position - sh.Position).Length <= w.Settings.FightCompareM &&
                           ai.Judge.AttackerShare(w, pic[i], profile, sh.Side, theyAttack: false) >= 0.5;
            var sp = new Spread
            {
                Scenario = Scenario, Seed = seed, Why = by, Task = task, Shooter = w.Types.Of(sh.Handling.Class), Target = w.Types.Of(tg.Handling.Class),
                TargetT = tg.Handling.Class.Motion.FullDisplacementT, RangeM = (tg.Position - sh.Position).Length, Chance = chance, Better = better,
                Night = Light.SunElevationDeg(sh.Position, w.Time) < -6, BetterNear = near, Count = e.Count, Aimed = e.Target,
            };
            spreads.Add(sp);
            last[(seed, e.Shooter, e.Battery)] = sp;
        }

        /// <summary>The torpedo hits since the last look, each put to its launcher's last spread (a game minute's lines).</summary>
        public void Sample()
        {
            if (log == null)
                return;
            missed += Math.Max(log.Written - read - log.Count, 0);
            long fresh = Math.Min(log.Written - read, log.Count);
            for (long i = fresh - 1; i >= 0; i--)
            {
                ref readonly var e = ref log.Newest((int)i);
                if (e.Kind == CombatEvent.Hit && e.Source == HitSource.Torpedo && last.TryGetValue((seed, e.Shooter, e.Battery), out var sp))
                {
                    if (e.Target == sp.Aimed)
                        sp.Hits++;
                    else
                        sp.Others++;
                }
            }
            read = log.Written;
        }

        public void Print()
        {
            Console.WriteLine();
            Console.WriteLine($"torpedo use (docs/battle/torpedo-use.md TU0), every seed: {spreads.Count} spreads, {spreads.Sum(x => x.Count)} torpedoes" +
                              (missed > 0 ? $" ({missed} log lines missed: hits among them uncounted)" : ""));
            Console.WriteLine("  better: the matchup has her torpedoes the better weapon against the target (share of spreads); near: fired at a target they " +
                              "weren't, with one they were in sight within the fight's reach");
            Table("by why it was fired and what at", x => $"{x.Why,-34} at {x.Target,-9}");
            Table("by the shooter's task and what at", x => $"{x.Task,-34} at {x.Target,-9}");
            Table("by day or night, the shooter and what at", x => $"{(x.Night ? "night" : "day"),-5} {x.Shooter,-9} {"",-19} at {x.Target,-9}");
            Csv();
        }

        void Table(string title, Func<Spread, string> key)
        {
            Console.WriteLine($"  {title}:");
            Console.WriteLine($"    {"",-47} {"spreads",7} {"torps",6} {"hits",5} {"hit %",6} {"+oth",5} {"chance",6} {"km",5} {"better",6} {"near",5}");
            foreach (var g in spreads.GroupBy(key).OrderBy(g => g.Key, StringComparer.Ordinal))
            {
                int n = g.Sum(x => x.Count), h = g.Sum(x => x.Hits);
                Console.WriteLine($"    {g.Key,-47} {g.Count(),7} {n,6} {h,5} {100.0 * h / Math.Max(n, 1),5:0.0}% {g.Sum(x => x.Others),5} " +
                                  $"{Med(g.Select(x => x.Chance)),6:0.00} {Med(g.Select(x => x.RangeM)) / 1000,5:0.0} " +
                                  $"{100.0 * g.Count(x => x.Better >= 0.5) / g.Count(),5:0}% {100.0 * g.Count(x => x.BetterNear) / g.Count(),4:0}%");
            }
        }

        static double Med(IEnumerable<double> xs)
        {
            var l = xs.Where(double.IsFinite).OrderBy(x => x).ToList();
            return l.Count == 0 ? double.NaN : l[l.Count / 2];
        }

        void Csv()
        {
            if (Environment.GetEnvironmentVariable("TU_CSV") is not { Length: > 0 } path)
                return;
            bool fresh = !File.Exists(path);
            using var f = new StreamWriter(path, append: true);
            if (fresh)
                f.WriteLine("scenario,seed,why,task,night,shooter,target,target_t,range_m,chance,better,better_near,count,hits,others");
            foreach (var x in spreads)
                f.WriteLine(string.Join(",", x.Scenario, x.Seed, x.Why, x.Task, x.Night ? 1 : 0, x.Shooter, x.Target, Math.Round(x.TargetT), Math.Round(x.RangeM),
                    Math.Round(x.Chance, 3), Math.Round(x.Better, 2), x.BetterNear ? 1 : 0, x.Count, x.Hits, x.Others).Replace("NaN", ""));
        }
    }
}
