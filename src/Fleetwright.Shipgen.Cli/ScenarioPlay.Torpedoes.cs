using Fleetwright.Sim;
using Fleetwright.Sim.Combat;
using Fleetwright.Sim.Core;

namespace Fleetwright.Shipgen.Cli;

static partial class ScenarioPlay
{
    /// <summary>Every spread fired in play (the fire table; docs/battle/balance.md P11): its range and the setting it ran on as
    /// the torpedo system solves it on the truth at the launch, the target's speed and length, by day or night; the hits
    /// it made (a torpedo hit is put to its shooter's last spread from that launcher: on its target, or on another ship). Beside each, the
    /// matchup's hit chance a torpedo (<see cref="Matchup"/>'s, duds aside) for the same shot, so its forecast reads against
    /// play.</summary>
    sealed class TorpedoTally
    {
        sealed class Spread
        {
            public bool Night;
            public int Shooter, Launcher, Target, Count, Hits, Others;
            public double RangeM, RunS, SettingKn, FastKn, SlowKn, TargetKn, LengthM, Forecast;
        }

        readonly List<Spread> spreads = [];
        readonly Dictionary<(ulong, int, int), Spread> last = [];

        public void Fired(World w, ulong seed, in CombatEntry e)
        {
            var store = w.Ships.Store;
            if (store.SlotOf(e.Shooter) is not (int ss and >= 0) || store.SlotOf(e.Target) is not (int ts and >= 0))
                return;
            var sh = store[ss];
            var tg = store[ts];
            var cls = sh.Handling.Class;
            ref readonly var row = ref cls.Torpedoes[cls.Launchers[e.Battery].Torpedo];
            var v = tg.Forward * tg.Speed;
            var from = sh.Frame.ToWorld(cls.Launchers[e.Battery].X, cls.Launchers[e.Battery].Y, 0);
            double range = (tg.Position - sh.Position).Length, runS = double.NaN, kn = double.NaN;
            if (TorpedoAim.Setting(row, from, tg.Position, v, out double speed, out _, out _, out double run))
                (runS, kn) = (run / speed, speed / Handling.MsPerKnot);
            var s = w.Settings;
            double length = tg.Handling.Class.Length, tkn = tg.Speed / Handling.MsPerKnot;
            // the matchup's chance a torpedo for this shot (Matchup.Torpedoes, duds aside): an attack's by day may run slow
            double forecast = Matchup.TorpedoHit(row, range, tg.Speed, length, s, 1, slow: true);
            var sp = new Spread
            {
                Night = Light.SunElevationDeg(sh.Position, w.Time) < -6, Shooter = e.Shooter, Launcher = e.Battery, Target = e.Target, Count = e.Count,
                RangeM = range, RunS = runS, SettingKn = kn, FastKn = row.FastKn, SlowKn = row.SlowKn, TargetKn = tkn, LengthM = length, Forecast = forecast,
            };
            spreads.Add(sp);
            last[(seed, e.Shooter, e.Battery)] = sp;
        }

        public void Hit(ulong seed, in CombatEntry e)
        {
            // put to the launcher's last spread: its target, or another ship it ran into (a line's next astern)
            if (last.TryGetValue((seed, e.Shooter, e.Battery), out var sp))
            {
                if (e.Target == sp.Target)
                    sp.Hits++;
                else
                    sp.Others++;
            }
        }

        /// <summary>Every spread as a CSV line (P11_SPREADS=FILE, appended): the fit's data.</summary>
        void Csv()
        {
            if (Environment.GetEnvironmentVariable("P11_SPREADS") is not { Length: > 0 } path)
                return;
            bool fresh = !File.Exists(path);
            using var f = new StreamWriter(path, append: true);
            if (fresh)
                f.WriteLine("scenario,night,count,hits,others,range_m,run_s,setting_kn,fast_kn,slow_kn,target_kn,length_m,forecast");
            foreach (var x in spreads)
                f.WriteLine(string.Join(",", Scenario, x.Night ? 1 : 0, x.Count, x.Hits, x.Others, Math.Round(x.RangeM), Math.Round(x.RunS, 1), Math.Round(x.SettingKn, 1),
                    x.FastKn, x.SlowKn, Math.Round(x.TargetKn, 1), Math.Round(x.LengthM, 1), Math.Round(x.Forecast, 4)).Replace("NaN", ""));
        }

        public string Scenario = "";

        public void Print()
        {
            if (spreads.Count == 0)
                return;
            Csv();
            Console.WriteLine("every torpedo fired (P11), by day or night and the run the setting gives (its range over its speed, on the truth at the launch): " +
                              "torpedoes, hits, hit %; the matchup's forecast a torpedo for the same shots (duds aside); the median range, setting and target speed");
            double[] edges = [60, 120, 240, 480];
            string[] names = ["under 1 min", "1-2 min", "2-4 min", "4-8 min", "8 min and over"];
            foreach (bool night in new[] { false, true })
            {
                var ss = spreads.Where(x => x.Night == night).ToList();
                if (ss.Count == 0)
                    continue;
                Console.WriteLine($"  {(night ? "night" : "day")}:");
                for (int b = 0; b <= edges.Length; b++)
                {
                    var bs = ss.Where(x => double.IsFinite(x.RunS) && edges.Count(e => x.RunS >= e) == b).ToList();
                    Line(names[b], bs);
                }
                Line("out of reach on the truth", ss.Where(x => !double.IsFinite(x.RunS)).ToList());
                Line("all", ss);
                foreach (var g in ss.GroupBy(x => x.FastKn).OrderBy(g => g.Key))
                    Line($"a {g.Key:0} kn torpedo", g.ToList());
            }

            static void Line(string name, List<Spread> bs)
            {
                if (bs.Count == 0)
                    return;
                int n = bs.Sum(x => x.Count), h = bs.Sum(x => x.Hits), o = bs.Sum(x => x.Others);
                double fc = bs.Sum(x => x.Count * x.Forecast) / Math.Max(n, 1);
                static double Med(IEnumerable<double> xs)
                {
                    var l = xs.Where(double.IsFinite).OrderBy(x => x).ToList();
                    return l.Count == 0 ? double.NaN : l[l.Count / 2];
                }
                Console.WriteLine($"    {name,-26} {n,6} {h,5} {100.0 * h / Math.Max(n, 1),6:0.0}% (+{o} others)  forecast {100 * fc,5:0.0}%   range {Med(bs.Select(x => x.RangeM)) / 1000,5:0.0} km, " +
                                  $"set {Med(bs.Select(x => x.SettingKn)),4:0} kn, target {Med(bs.Select(x => x.TargetKn)),4:0} kn");
            }
        }
    }
}
