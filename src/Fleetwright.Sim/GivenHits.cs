using Fleetwright.Shipgen;
using Fleetwright.Sim.Combat;
using Fleetwright.Sim.Core;
using Fleetwright.Sim.Ships;

namespace Fleetwright.Sim;

/// <summary>Given hits (docs/battle/fighting.md 4.3a, the bench; a port of research/simulation/damage-research/
/// gunfire_ref.py <c>mc()</c>): hits from one shell at a range, spread over the target as the falling shells see it
/// (uniformly over its outline across their line of flight, so side and deck share the hits by the danger space, and
/// parts by their size), each through the world's own damage system on a ship of the class. A run is K hits on one
/// ship (1: each hit on a fresh ship), then its flooding and fire run on for a while (<c>settleS</c>, 4.3b); the report
/// sums the hits' outcomes and the ships' state after. Not world state: a tool for <c>shipgen hits</c> and the damage
/// tests.</summary>
public static class GivenHits
{
    /// <summary>What the hits did: per hit, where they struck, their outcomes, fires, crew and holes, how often each
    /// part was put out of action (a node: component, then room); per ship after its K hits, its power and the share of
    /// its top speed that makes (slowed: under <see cref="SlowShare"/>), steering, main mounts and directors, and how
    /// many were lost, and how (by <see cref="LossKind"/>), the water they took, and how many were out of the fight (Out:
    /// lost, or past a fall-out rule; the matchup's toughness, docs/battle/command.md 5.1g).</summary>
    public sealed class Report
    {
        public int Hits, Ships;
        public readonly int[] Outcomes = new int[Enum.GetValues<Outcome>().Length];
        public readonly Dictionary<string, int> Struck = [];
        public int Fires, Magazines, Jams;
        /// <summary>E4: the hits a turret or barbette held (stopped, glanced off, burst on), what they did to its mount (by
        /// <see cref="TurretBlow"/>), the jams by cause (<see cref="JamCause"/>) and how many lasted the battle; the hits that
        /// stunned the struck structure's crew.</summary>
        public int TurretHeld, JamsForBattle, Stuns;
        public readonly int[] TurretBlows = new int[Enum.GetValues<TurretBlow>().Length];
        public readonly int[] JamCauses = new int[Enum.GetValues<JamCause>().Length];
        /// <summary>E4: the turret holds by where they struck (<see cref="TurretSpot"/>), and their margins' factor summed
        /// (<see cref="NonPen.MarginK"/>).</summary>
        public readonly int[] TurretSpots = new int[Enum.GetValues<TurretSpot>().Length];
        public double TurretMarginK;
        /// <summary>E4: the hits a belt held (stopped, glanced off, burst on), its leaks by regime
        /// (<see cref="LeakRegime"/>, None: held tight), the feeds they fouled.</summary>
        public int BeltHeld, Feeds;
        /// <summary>E4: the hits a belt, barbette, turret or conning tower held (the shock's), those whose shock put gear
        /// out, and the ship-wide blackouts.</summary>
        public int ShockHeld, Shocks, Blackouts;
        /// <summary>E5: the bursts whose gas loaded a closed space (not open, not outside), the spaces it reached, the
        /// boundaries it ruptured and the doors it blew, what the burst space's boundaries felt (MPa) and the openings left
        /// between spaces and to the sea or air (m²), summed.</summary>
        public int Confined, BlastSpaces, BlastRuptured, BlastDoors;
        public double BlastFelt, BlastOpenM2, BlastHoleM2;
        public readonly int[] Leaks = new int[Enum.GetValues<LeakRegime>().Length];
        /// <summary>E3: the hits whose plate threw spall off its back.</summary>
        public int Spalls;
        /// <summary>The hits that set a magazine off, by how (<see cref="MagazineVia"/>); and the ships lost to a magazine
        /// that no hit set off: a fire reached it while it burned on.</summary>
        public readonly int[] MagazinesBy = new int[Enum.GetValues<MagazineVia>().Length];
        public int MagazineByFire;
        public double CrewLost, BreachM2;
        /// <summary>E6: the men killed and wounded, summed; the hits that hurt nobody and that killed nobody; the hits by
        /// how many they killed (the last bin all of more).</summary>
        public double Killed, Wounded;
        public int Unhurt, KilledNone;
        /// <summary>The hits with a propellant flash (16 §2.2 leaves them out of the ships' groups), their killed and wounded.</summary>
        public int Flashes;
        public double FlashKilled, FlashWounded;
        public readonly int[] KilledBy = new int[401];
        public int[] PartsOut = [];

        /// <summary>The killed a hit at a share of the hits (by count, 0-1) kills at most.</summary>
        public int KilledAt(double share)
        {
            int n = 0, want = (int)Math.Ceiling(share * Hits);
            for (int i = 0; i < KilledBy.Length; i++)
                if ((n += KilledBy[i]) >= want)
                    return i;
            return KilledBy.Length - 1;
        }

        /// <summary>16 §2.1's classes of hit by what it did: burst inside (A and B), passed through without bursting (C), a
        /// dud (D), on armour that held or a burst outside it (E); their hits, killed, wounded and hits killing nobody.</summary>
        public static readonly string[] HitClasses = ["burst inside", "passed through", "dud", "on armour"];
        public readonly int[] ClassHits = new int[4], ClassNone = new int[4];
        public readonly double[] ClassKilled = new double[4], ClassWounded = new double[4];

        public static int ClassOf(Outcome o) => o switch
        {
            Outcome.BurstInside or Outcome.BurstInPlate or Outcome.LowOrder or Outcome.BrokeUp or Outcome.Holed => 0,
            Outcome.PassedThrough or Outcome.BurstBeyond => 1,
            Outcome.Dud => 2,
            _ => 3,
        };

        /// <summary>A hit's killed and wounded tallied, by its outcome's class too.</summary>
        public void Men(in EffectsResult fx, Outcome o)
        {
            int h = ClassOf(o);
            (ClassHits[h], ClassNone[h], ClassKilled[h], ClassWounded[h]) = (ClassHits[h] + 1, ClassNone[h] + (fx.Killed == 0 ? 1 : 0), ClassKilled[h] + fx.Killed,
                ClassWounded[h] + fx.Wounded);
            (Killed, Wounded) = (Killed + fx.Killed, Wounded + fx.Wounded);
            if (fx.Flash)
                (Flashes, FlashKilled, FlashWounded) = (Flashes + 1, FlashKilled + fx.Killed, FlashWounded + fx.Wounded);
            Unhurt += fx.Killed + fx.Wounded == 0 ? 1 : 0;
            KilledNone += fx.Killed == 0 ? 1 : 0;
            KilledBy[Math.Min(fx.Killed, KilledBy.Length - 1)]++;
        }
        public double PowerCap, SpeedShare, MainWorking, MainMounts, DirectorsWorking, Directors;
        public int Crippled, Slowed, SteeringLost, Lost, MainSilenced, Out;
        public readonly int[] LostBy = new int[Enum.GetValues<LossKind>().Length];
        public double WaterT, ListDeg;
        public double FallDeg, StrikeMs;
        /// <summary>The hits that ran under the water to the hull: how many, and their runs and striking speeds summed.</summary>
        public int Underwater;
        public double UnderwaterRunM, UnderwaterStrikeMs;
        /// <summary>What the plates did to the shells (docs/battle/shells-armour-damage.md P2): caps knocked off, shells
        /// shattered on face-hardened plate, bodies broken just over a plate's limit; and the hits that met a plate over 9"
        /// (229 mm), and of those the ones that got through it and burst inside (Jutland's British 1 in 13).</summary>
        public int Decapped, Shattered, BrokeNearLimit, Heavy, HeavyBurst;
        /// <summary>The hits a plate stopped within its spall band (P4, E3's), and their severities summed.</summary>
        public int Spalled;
        public double SpallS;
        /// <summary>P5: the hits that glanced off a plate, and those a plate turned enough to bend their path, with their
        /// turns summed (degrees).</summary>
        public int Glanced, Bent;
        public double TurnDeg;
        /// <summary>The bursts inside after a fuze's delay (full or low-order): how many, and how far behind the crossing
        /// that started the fuze they went off, summed (P3; Baden's 15" burst 11.6 m behind the belt).</summary>
        public int Delayed;
        public double DelayedM;

        public double Share(Outcome o) => Hits > 0 ? (double)Outcomes[(int)o] / Hits : 0;
    }

    /// <summary>n hits of a shell at a range on a class, the shell coming from a bearing off the target's bow
    /// (degrees clockwise: 90 from its starboard beam), in runs of perShip hits on one ship.</summary>
    public const double SlowShare = 0.85;

    public static Report Run(ShipClass target, ShellTypeRow shell, double maxElevationDeg, double rangeM, double aspectDeg, int n, int perShip, ulong seed,
        SimSettings settings, Action<int, HitPath, TerminalResult, EffectsResult>? each = null, double settleS = 0,
        FallOutRule? fallOut = null)
    {
        var table = new BallisticTable(BallisticKey.Of(shell, maxElevationDeg));
        double e = table.ElevationFor(rangeM, 10);
        if (double.IsNaN(e))
            throw new ArgumentException($"{rangeM:0} m is beyond the gun's reach");
        var fall = table.Fall(e, 10);
        var rep = new Report { FallDeg = fall.FallDeg, StrikeMs = fall.StrikeMs, PartsOut = new int[target.NodeCount] };

        // the shell's flight in the ship's frame, and two axes across it
        double b = double.DegreesToRadians(aspectDeg), f = double.DegreesToRadians(fall.FallDeg);
        var dir = new P3(-Math.Cos(b) * Math.Cos(f), -Math.Sin(b) * Math.Cos(f), -Math.Sin(f));
        var u = Normalize(Cross(dir, new P3(0, 0, 1)));
        var v = Cross(u, dir);
        var box = target.Solids.Bounds;
        double u0 = double.MaxValue, u1 = double.MinValue, v0 = double.MaxValue, v1 = double.MinValue;
        foreach (double x in (ReadOnlySpan<double>)[box.X0, box.X1])
            foreach (double y in (ReadOnlySpan<double>)[box.Y0, box.Y1])
                foreach (double z in (ReadOnlySpan<double>)[box.Base, box.Top])
                {
                    var p = new P3(x, y, z);
                    (u0, u1, v0, v1) = (Math.Min(u0, Dot(p, u)), Math.Max(u1, Dot(p, u)), Math.Min(v0, Dot(p, v)), Math.Max(v1, Dot(p, v)));
                }
        double far = 2 * target.Solids.Radius + 50;

        var w = new World(WorldTime.FromUtc(1916, 5, 31, 14), seed, settings);
        int cls = w.Ships.AddClass(target);
        var path = new HitPath();
        var rng = RandomStream.Named(seed, "given hits");
        double reach = WaterRun.ReachM(shell, fall.StrikeMs, settings);
        int entity = 0;
        while (rep.Hits < n)
        {
            if (entity != 0)
                w.Apply(ShipSystem.Drop(entity));
            w.Apply(ShipSystem.Spawn(cls, 56, 4, 0, 15));
            entity = w.Ships.NewestEntity;
            int slot = w.Ships.Store.SlotOf(entity);
            rep.Ships++;
            bool hitMagazine = false;
            for (int k = 0; k < perShip && rep.Hits < n; k++)
            {
                // a point across the line of flight, until one meets the ship, its waterline where it floats now
                var water = HitQuery.WaterOf(target, w.Ships.Store.Combat.Condition(slot).Attitude);
                P3 at;
                double run;
                while (true)
                {
                    double a = rng.NextDouble(u0, u1), c = rng.NextDouble(v0, v1);
                    var mid = new P3(u.X * a + v.X * c, u.Y * a + v.Y * c, u.Z * a + v.Z * c);
                    ShipTracer.Trace(target, Add(mid, dir, -far), Add(mid, dir, far), [], water, path);
                    int first = First(path);
                    if (first < 0)
                        continue;
                    var x = path.Crossings[first];
                    // the sea first: a miss, unless it meets the ship within the shell system's reach under the water
                    run = WaterRun.RunM(path, first);
                    if (run > reach)
                        continue;
                    at = x.Point;
                    string where = Where(target, x);
                    rep.Struck[where] = rep.Struck.GetValueOrDefault(where) + 1;
                    break;
                }
                var hit = new Hit
                {
                    Ship = entity, Ms = w.Time.Ms, Point = at, Direction = dir, SpeedMs = fall.StrikeMs, WaterRunM = run, Source = HitSource.Shell, Shell = shell,
                    ShellType = -1,
                };
                var k0 = w.Ships.Store.Combat.Condition(slot);
                var before = (PartState[])k0.State.Clone();
                w.Damage.Strike(w, hit);
                var t = w.Damage.LastTerminal;
                var fx = w.Damage.LastEffects;
                each?.Invoke(rep.Hits, w.Damage.LastPath, t, fx);
                rep.Hits++;
                rep.Outcomes[(int)t.Outcome]++;
                (rep.Decapped, rep.Shattered, rep.BrokeNearLimit) =
                    (rep.Decapped + (t.Decapped ? 1 : 0), rep.Shattered + (t.Shattered ? 1 : 0), rep.BrokeNearLimit + (t.BrokeNearLimit ? 1 : 0));
                if (!double.IsNaN(t.Spall))
                    (rep.Spalled, rep.SpallS) = (rep.Spalled + 1, rep.SpallS + t.Spall);
                if (t.Glance >= 0)
                    rep.Glanced++;
                if (t.TurnDeg > 0)
                    (rep.Bent, rep.TurnDeg) = (rep.Bent + 1, rep.TurnDeg + t.TurnDeg);
                double heavyT = HeavyPlateT(w.Damage.LastPath, t.StopT);
                if (!double.IsNaN(heavyT))
                    (rep.Heavy, rep.HeavyBurst) = (rep.Heavy + 1, rep.HeavyBurst + (t.Burst == BurstKind.Inside && t.BurstT > heavyT ? 1 : 0));
                if (t.Burst == BurstKind.Inside && t.Outcome is Outcome.BurstInside or Outcome.LowOrder && t.Plate >= 0)
                    (rep.Delayed, rep.DelayedM) = (rep.Delayed + 1, rep.DelayedM + t.BurstT - w.Damage.LastPath.Crossings[t.Plate].T);
                if (t.Underwater)
                    (rep.Underwater, rep.UnderwaterRunM, rep.UnderwaterStrikeMs) = (rep.Underwater + 1, rep.UnderwaterRunM + run, rep.UnderwaterStrikeMs + t.StrikeMs);
                rep.Fires += fx.Fire ? 1 : 0;
                rep.Magazines += fx.Magazine ? 1 : 0;
                rep.Spalls += fx.Spall ? 1 : 0;
                rep.MagazinesBy[(int)fx.MagazineVia] += fx.Magazine ? 1 : 0;
                hitMagazine |= fx.Magazine;
                rep.Jams += fx.Jammed ? 1 : 0;
                rep.Stuns += fx.Stunned ? 1 : 0;
                if (fx.Blast.Spaces > 0 && !fx.Blast.Open)
                {
                    (rep.Confined, rep.BlastSpaces, rep.BlastRuptured, rep.BlastDoors) =
                        (rep.Confined + 1, rep.BlastSpaces + fx.Blast.Spaces, rep.BlastRuptured + fx.Blast.Ruptured, rep.BlastDoors + fx.Blast.Doors);
                    (rep.BlastFelt, rep.BlastOpenM2, rep.BlastHoleM2) =
                        (rep.BlastFelt + fx.Blast.FeltMPa, rep.BlastOpenM2 + fx.Blast.OpenM2 + fx.Blast.LeakM2, rep.BlastHoleM2 + fx.Blast.HoleM2);
                }
                int held = t.Glance >= 0 ? t.Glance : t.Outcome is Outcome.Defeated or Outcome.BurstOutside ? t.Plate : -1;
                var hp = held >= 0 ? w.Damage.LastPath.Crossings[held].Plate : PlateKind.None;
                bool shaken = hp is PlateKind.SideArmour or PlateKind.Barbette or >= PlateKind.TurretFace and <= PlateKind.TurretRoof ||
                              hp == PlateKind.PartArmour && target.Components[w.Damage.LastPath.Crossings[held].Index].Kind == ComponentKind.ConningTower;
                if (shaken)
                    (rep.ShockHeld, rep.Shocks, rep.Blackouts) = (rep.ShockHeld + 1, rep.Shocks + (fx.Shock ? 1 : 0), rep.Blackouts + (fx.Blackout ? 1 : 0));
                if (held >= 0 && w.Damage.LastPath.Crossings[held].Plate == PlateKind.SideArmour)
                {
                    rep.BeltHeld++;
                    rep.Leaks[(int)fx.Leak]++;
                    rep.Feeds += fx.Feed ? 1 : 0;
                }
                if (held >= 0 && w.Damage.LastPath.Crossings[held].Plate is PlateKind.Barbette or >= PlateKind.TurretFace and <= PlateKind.TurretRoof &&
                    target.MountOf(w.Damage.LastPath.Crossings[held].Index) >= 0)
                {
                    rep.TurretHeld++;
                    ref readonly var hx = ref w.Damage.LastPath.Crossings[held];
                    rep.TurretSpots[(int)NonPen.Spot(hx.Plate, hx.Point.Z)]++;
                    rep.TurretMarginK += NonPen.MarginK(NonPen.Margin(t.PlateU));
                    rep.TurretBlows[(int)fx.Turret.Blow]++;
                    if (fx.Turret.Blow == TurretBlow.Jam)
                        (rep.JamCauses[(int)fx.Turret.Cause], rep.JamsForBattle) = (rep.JamCauses[(int)fx.Turret.Cause] + 1, rep.JamsForBattle + (fx.Turret.Ms == NonPen.Battle ? 1 : 0));
                }
                rep.CrewLost += fx.CrewLost;
                rep.Men(fx, t.Outcome);
                rep.BreachM2 += fx.BreachM2;
                for (int i = 0; i < before.Length; i++)
                    if (before[i] < PartState.Disabled && k0.State[i] >= PartState.Disabled)
                        rep.PartsOut[i]++;
            }
            After(rep, target, w, entity, slot, settleS, fallOut, hitMagazine);
        }
        return rep;
    }

    /// <summary>Where along the path the shell first met armour over 9" (229 mm) before it stopped; NaN for none.</summary>
    static double HeavyPlateT(HitPath path, double stopT)
    {
        foreach (ref readonly var x in path.Crossings)
        {
            if (x.T > stopT)
                break;
            if (x.Plate is >= PlateKind.SideArmour and <= PlateKind.PartArmour && x.Mm > 228.6)
                return x.T;
        }
        return double.NaN;
    }

    /// <summary>Torpedo hits (docs/battle/fighting.md 4.6): n of a torpedo on a class, at its set depth on its side, along
    /// the waterline (at a share of its length from the stern, 0-1, or anywhere on it: NaN), to starboard, in runs of
    /// perShip hits on one ship, each through the world's own damage system.</summary>
    public static Report Torpedoes(ShipClass target, TorpedoRow torpedo, double depthM, double atShare, int n, int perShip, ulong seed, SimSettings settings,
        double settleS = 0, FallOutRule? fallOut = null)
    {
        var rep = new Report { PartsOut = new int[target.NodeCount] };
        var w = new World(WorldTime.FromUtc(1916, 5, 31, 14), seed, settings);
        int cls = w.Ships.AddClass(target);
        var rng = RandomStream.Named(seed, "given torpedoes");
        double x0 = double.MaxValue, x1 = double.MinValue;
        foreach (var p in target.Solids.Waterline)
            (x0, x1) = (Math.Min(x0, p.X), Math.Max(x1, p.X));
        double z = target.Vertical.Waterline - depthM;
        int entity = 0;
        while (rep.Hits < n)
        {
            if (entity != 0)
                w.Apply(ShipSystem.Drop(entity));
            w.Apply(ShipSystem.Spawn(cls, 56, 4, 0, 15));
            entity = w.Ships.NewestEntity;
            int slot = w.Ships.Store.SlotOf(entity);
            rep.Ships++;
            bool hitMagazine = false;
            for (int k = 0; k < perShip && rep.Hits < n; k++)
            {
                int cell;
                double x, y;
                do
                {
                    x = x0 + (double.IsNaN(atShare) ? rng.NextDouble() : atShare) * (x1 - x0);
                    (cell, y) = FloodModel.SideCell(target, x, z, true);
                }
                while (cell < 0 && double.IsNaN(atShare));
                if (cell < 0)
                    throw new ArgumentException($"no hull at {x:0.0} m, {depthM:0.0} m down on {target.Name}");
                var k0 = w.Ships.Store.Combat.Condition(slot);
                var before = (PartState[])k0.State.Clone();
                w.Damage.Strike(w, new Hit
                {
                    Ship = entity, Ms = w.Time.Ms, Point = new P3(x, y, z), Direction = new P3(0, -1, 0), SpeedMs = 20, Source = HitSource.Torpedo,
                    Torpedo = torpedo, ShellType = -1,
                });
                var fx = w.Damage.LastEffects;
                rep.Hits++;
                rep.Outcomes[(int)w.Damage.LastTerminal.Outcome]++;
                rep.Magazines += fx.Magazine ? 1 : 0;
                rep.Spalls += fx.Spall ? 1 : 0;
                rep.MagazinesBy[(int)fx.MagazineVia] += fx.Magazine ? 1 : 0;
                hitMagazine |= fx.Magazine;
                rep.CrewLost += fx.CrewLost;
                rep.BreachM2 += fx.BreachM2;
                string where = fx.Part >= target.Components.Length ? target.Rooms[fx.Part - target.Components.Length].Kind.ToString() : "-";
                rep.Struck[where] = rep.Struck.GetValueOrDefault(where) + 1;
                for (int i = 0; i < before.Length; i++)
                    if (before[i] < PartState.Disabled && k0.State[i] >= PartState.Disabled)
                        rep.PartsOut[i]++;
            }
            After(rep, target, w, entity, slot, settleS, fallOut, hitMagazine);
        }
        return rep;
    }

    /// <summary>A ship after its hits: its flooding and fire run on for a while, then what it can still do into the
    /// report.</summary>
    static void After(Report rep, ShipClass target, World w, int entity, int slot, double settleS, FallOutRule? fallOut, bool hitMagazine)
    {
        // the ship after its hits, and its flooding and fire run on
        if (settleS > 0)
            w.Damage.Settle(w, entity, settleS);
        var kk = w.Ships.Store.Combat.Condition(slot);
        rep.WaterT += kk.WaterT;
        rep.LostBy[(int)kk.Loss]++;
        rep.MagazineByFire += kk.Loss == LossKind.Magazine && !hitMagazine ? 1 : 0;
        int main = 0, mainOk = 0, dirs = 0;
        for (int m = 0; m < target.Mounts.Length; m++)
            if (target.Mounts[m].Kind == ComponentKind.Main)
            {
                main++;
                mainOk += kk.MountWorks[m] ? 1 : 0;
            }
        foreach (bool d in kk.DirectorWorks)
            dirs += d ? 1 : 0;
        rep.PowerCap += kk.PowerCap;
        double top = target.Motion.MaxSpeedKn, speed = target.SpeedAt(target.PowerAt(top) * kk.PowerCap) / top;
        rep.SpeedShare += speed;
        rep.MainWorking += mainOk;
        rep.MainMounts += main;
        rep.DirectorsWorking += dirs;
        rep.Directors += kk.DirectorWorks.Length;
        bool lost = kk.Status != ShipStatus.Afloat, slowed = speed < SlowShare, steer = kk.Steering != Steering.Free;
        rep.Lost += lost ? 1 : 0;
        rep.Slowed += slowed ? 1 : 0;
        rep.SteeringLost += steer ? 1 : 0;
        rep.MainSilenced += main > 0 && mainOk == 0 ? 1 : 0;
        rep.Crippled += lost || slowed || steer || main > 0 && mainOk == 0 ? 1 : 0;
        rep.ListDeg += Math.Abs(kk.Attitude.HeelDeg);
        rep.Out += lost || fallOut is { } rule && rule.Of(Fitness.Of(target, kk, w.Damage.Flood(target).ReserveT)) != FallOutCause.None ? 1 : 0;
    }

    /// <summary>The first crossing into the ship: its hull or a part.</summary>
    static int First(HitPath path)
    {
        var xs = path.Crossings;
        for (int i = 0; i < xs.Length; i++)
            if (xs[i].Kind is CrossingKind.HullIn or CrossingKind.PartIn)
                return i;
        return -1;
    }

    /// <summary>Where a hit struck, in a word: the side armour, the hull's side or deck, a turret, a barbette, the
    /// conning tower, superstructure, a funnel, and so on.</summary>
    static string Where(ShipClass c, in Crossing x)
    {
        if (x.Kind == CrossingKind.PartIn)
            return c.Components[x.Index].Kind switch
            {
                ComponentKind.Main or ComponentKind.Secondary or ComponentKind.Torpedo => "mount",
                ComponentKind.Superstructure => c.Components[x.Index].Role is ComponentRole.Director ? "director" : "superstructure",
                var k => k.ToString().ToLowerInvariant(),
            };
        return x.Plate switch
        {
            PlateKind.SideArmour => "side armour",
            PlateKind.HullDeck => "hull deck",
            PlateKind.HullEnd => "hull end",
            _ => "hull side",
        };
    }

    static P3 Add(P3 a, P3 d, double s) => new(a.X + d.X * s, a.Y + d.Y * s, a.Z + d.Z * s);
    static double Dot(P3 a, P3 b) => a.X * b.X + a.Y * b.Y + a.Z * b.Z;
    static P3 Cross(P3 a, P3 b) => new(a.Y * b.Z - a.Z * b.Y, a.Z * b.X - a.X * b.Z, a.X * b.Y - a.Y * b.X);

    static P3 Normalize(P3 a)
    {
        double l = Math.Sqrt(Dot(a, a));
        return new P3(a.X / l, a.Y / l, a.Z / l);
    }
}
