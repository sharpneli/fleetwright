using Fleetwright.Sim;
using Fleetwright.Sim.Combat;
using Fleetwright.Sim.Ships;

namespace Fleetwright.Shipgen.Cli;

static partial class ScenarioPlay
{
    /// <summary><c>--ammo</c>: what the ships' chief batteries spent (docs/battle/fire-control.md "Shell consumption"):
    /// each ship's share of her outfit fired, rounds a gun a minute over her action and over the minutes her guns were
    /// firing, the ships left with under a third and run dry, and the salvos' pace and spotting.</summary>
    public static bool Ammo;

    /// <summary>One run's (or every seed's) chief-battery spending, sampled each game minute and from the salvo reports.</summary>
    sealed class AmmoTally
    {
        sealed class Ship
        {
            public int Side, Guns, Outfit, Left, FiredMinutes, FiringMinutes, FirstMin = -1, LastMin = -1;
            public ShipType Type;
        }

        readonly Dictionary<int, Ship> ships = [];
        readonly Dictionary<(int Shooter, int Battery), long> lastEffect = [];
        readonly Dictionary<(int Shooter, int Battery), (int Target, bool Held)> lastTarget = [];
        readonly Dictionary<int, Dictionary<int, long>> onTarget = [];   // a target's shooters (ships) and when each last fired at her
        readonly long[] byCrowd = new long[6], hitsByCrowd = new long[6];  // chief shells and hits by the ships firing at her: 1..5, 6+
        int minute;

        // every seed's, by side and type
        readonly SortedDictionary<(int Side, ShipType Type), (int Ships, long Guns, long Outfit, long Fired, long GunMinSpan, long GunMinFiring, int Third, int Dry)> rows = [];
        readonly SortedDictionary<(int Side, FirePhase Phase, bool Pb), long> shells = [];
        readonly List<double> effectGap = [];   // fire for effect: the time from one salvo to the next over the drill reload
        long spotSeen, spotUntold, spotUnseen;  // fire for effect's spots
        long shifts, salvos, rangingFirst, rangingAgain;   // targets shifted to, salvos; ranging shells on a new target, and after holding her

        public void Begin(World w)
        {
            ships.Clear();
            lastEffect.Clear();
            lastTarget.Clear();
            onTarget.Clear();
            minute = 0;
            var prevR = w.Gunnery.Reported;
            w.Gunnery.Reported = r =>
            {
                prevR?.Invoke(r);
                var store = w.Ships.Store;
                if (store.SlotOf(r.Shooter) is not (int ss and >= 0))
                    return;
                var cls = store[ss].Handling.Class;
                if (!cls.Batteries[r.Battery].Chief)
                    return;
                var key = (store[ss].Side, r.Phase, r.PointBlank);
                shells[key] = shells.GetValueOrDefault(key) + r.Shells;
                var bk = (r.Shooter, r.Battery);
                salvos++;
                // the ships whose chief guns fired at her within the last 90 s, this one counted
                if (!onTarget.TryGetValue(r.Target, out var who))
                    onTarget[r.Target] = who = [];
                who[r.Shooter] = r.FiredMs;
                int crowd = 0;
                foreach (long at in who.Values)
                    crowd += r.FiredMs - at < 90_000 ? 1 : 0;
                int ci = Math.Clamp(crowd, 1, 6) - 1;
                (byCrowd[ci], hitsByCrowd[ci]) = (byCrowd[ci] + r.Shells, hitsByCrowd[ci] + r.Hits);
                var (was, held) = lastTarget.TryGetValue(bk, out var lt) ? lt : (-1, false);
                if (was != r.Target)
                    (shifts, held) = (shifts + 1, false);
                held |= r.Phase == FirePhase.Effect;
                lastTarget[bk] = (r.Target, held);
                if (r.Phase == FirePhase.Ranging)
                {
                    if (was == r.Target && lt.Held)
                        rangingAgain += r.Shells;
                    else
                        rangingFirst += r.Shells;
                }
                if (r.Phase == FirePhase.Effect && !r.PointBlank)
                {
                    double reload = cls.Mounts[cls.Links(cls.Batteries[r.Battery].Mounts)[0]].ReloadS;
                    // a gun's cycle: alternating halves fire each gun every other salvo, so twice the gap; full salvos the gap
                    bool half = r.Shells < 0.75 * cls.Batteries[r.Battery].Guns;
                    if (lastEffect.TryGetValue(bk, out long last) && r.FiredMs - last < 4000 * reload)
                        effectGap.Add((r.FiredMs - last) / 1000.0 / reload * (half ? 2 : 1));
                    lastEffect[bk] = r.FiredMs;
                }
                else
                    lastEffect.Remove(bk);
            };
            var prevS = w.Gunnery.Spotted;
            w.Gunnery.Spotted = p =>
            {
                prevS?.Invoke(p);
                if (p.Phase != FirePhase.Effect || w.Ships.Store.SlotOf(p.Shooter) is not (int ss and >= 0) ||
                    !w.Ships.Store[ss].Handling.Class.Batteries[p.Battery].Chief)
                    return;
                if (!p.Seen)
                    spotUnseen++;
                else if (!p.Told)
                    spotUntold++;
                else
                    spotSeen++;
            };
        }

        /// <summary>Each game minute: every ship afloat's chief rounds left, and whether her chief guns are firing.</summary>
        public void Sample(World w)
        {
            var store = w.Ships.Store;
            for (int slot = 0; slot < store.Slots; slot++)
            {
                if (!store.IsAlive(slot))
                    continue;
                var st = store[slot];
                var cls = st.Handling.Class;
                int left = 0, outfit = 0, guns = 0;
                bool firing = false;
                for (int b = 0; b < cls.Batteries.Length; b++)
                {
                    if (!cls.Batteries[b].Chief)
                        continue;
                    guns += cls.Batteries[b].Guns;
                    foreach (int m in cls.Links(cls.Batteries[b].Mounts))
                        (left, outfit) = (left + Math.Min(store.Combat.Mount(slot, m).Rounds, cls.Mounts[m].Rounds), outfit + cls.Mounts[m].Rounds);
                    firing |= w.Gunnery.TallyOf(st.Entity, b) is { State: BatteryState.Firing };
                }
                if (outfit <= 0 || guns <= 0)
                    continue;
                if (!ships.TryGetValue(st.Entity, out var s))
                    ships[st.Entity] = s = new Ship { Side = st.Side, Guns = guns, Outfit = outfit, Left = outfit, Type = w.Types.Of(cls) };
                if (left < s.Left)
                {
                    s.FiredMinutes++;
                    s.FirstMin = s.FirstMin < 0 ? minute : s.FirstMin;
                    s.LastMin = minute;
                }
                s.Left = left;
                s.FiringMinutes += firing ? 1 : 0;
            }
            minute++;
        }

        /// <summary>The run's ships into the rows.</summary>
        public void End()
        {
            foreach (var s in ships.Values)
            {
                if (s.Type == ShipType.Merchant)
                    continue;
                int fired = s.Outfit - s.Left, span = s.FirstMin < 0 ? 0 : s.LastMin - s.FirstMin + 1;
                var k = (s.Side, s.Type);
                var r = rows.GetValueOrDefault(k);
                rows[k] = (r.Ships + 1, r.Guns + s.Guns, r.Outfit + s.Outfit, r.Fired + fired, r.GunMinSpan + (long)s.Guns * span,
                    r.GunMinFiring + (long)s.Guns * s.FiringMinutes, r.Third + (s.Left < s.Outfit / 3.0 ? 1 : 0), r.Dry + (s.Left < 0.05 * s.Outfit ? 1 : 0));
            }
        }

        public void Print(Fleetwright.Sim.Missions.Scenario s, int runs)
        {
            Console.WriteLine();
            Console.WriteLine($"the chief batteries' ammunition, {runs} run(s) (the record: about 0.25-0.5 rounds a gun a minute over an action, half the outfit after a day's fight):");
            Console.WriteLine($"  {"side",-12} {"type",-9} {"ships",5} {"outfit fired",12} {"rnd/gun/min",11} {"firing",7} {"<1/3 left",9} {"dry",5}");
            foreach (var ((side, type), r) in rows)
                Console.WriteLine($"  {Name(s, side),-12} {type,-9} {r.Ships,5} {100.0 * r.Fired / Math.Max(r.Outfit, 1),11:F0}% " +
                                  $"{(double)r.Fired / Math.Max(r.GunMinSpan, 1),11:F2} {(double)r.Fired / Math.Max(r.GunMinFiring, 1),7:F2} {r.Third,9} {r.Dry,5}");
            Console.WriteLine("    rnd/gun/min: over each ship's first to last minute she fired her chief guns; firing: over the minutes they were firing");
            Console.WriteLine("  shells by phase (chief batteries):");
            foreach (var ((side, phase, pb), n) in shells)
                Console.WriteLine($"    {Name(s, side),-12} {(pb ? "point blank" : phase.ToString()),-12} {n,8}");
            if (effectGap.Count > 0)
            {
                effectGap.Sort();
                double Q(double p) => effectGap[Math.Min((int)(p * effectGap.Count), effectGap.Count - 1)];
                Console.WriteLine($"  fire for effect, a gun's cycle over the drill reload: quartiles {Q(0.25):F2} {Q(0.5):F2} {Q(0.75):F2} ({effectGap.Count} gaps)");
            }
            Console.WriteLine($"  {salvos} salvos, {shifts} targets taken ({(double)salvos / Math.Max(shifts, 1):F1} salvos a target); ranging shells on a new target {rangingFirst}, " +
                              $"after holding her {rangingAgain}");
            long all = byCrowd.Sum();
            if (all > 0)
                Console.WriteLine("  chief shells by the ships firing on her (within 90 s): " + string.Join("  ", Enumerable.Range(0, 6).Select(i =>
                    $"{(i == 5 ? "6+" : (i + 1).ToString())}: {100.0 * byCrowd[i] / all:F0}% ({100.0 * hitsByCrowd[i] / Math.Max(byCrowd[i], 1):F1}% hit)")));
            long spots = spotSeen + spotUntold + spotUnseen;
            if (spots > 0)
                Console.WriteLine($"  fire for effect's spots: {100.0 * spotSeen / spots:F0}% seen and told, {100.0 * spotUntold / spots:F0}% seen but not told " +
                                  $"(whose splash), {100.0 * spotUnseen / spots:F0}% unseen ({spots})");
        }

        static string Name(Fleetwright.Sim.Missions.Scenario s, int side) => side >= 0 && side < s.Sides.Length ? s.Sides[side] : $"side {side}";
    }
}
