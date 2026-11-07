using Fleetwright.Sim.Combat;
using Fleetwright.Sim.Core;
using Fleetwright.Sim.Ships;

namespace Fleetwright.Sim;

/// <summary>A hit in three parts (docs/battle/workers.md W16b): what it takes from the world when it comes (<see cref="Capture"/>,
/// the sim thread), what it does to her condition (<see cref="Work"/>: her condition, the class, the settings and what it took,
/// nothing else, so it can be worked out anywhere), and the world's part after it (<see cref="Land"/>, the sim thread: the log,
/// her mounts and power, losses, blasts, her steps woken, the stores it set off).</summary>
public sealed partial class DamageSystem
{
    /// <summary>What a hit took from the world when it came: its draws' index, the moment, her men's experience, the sea her girder
    /// takes, her rudder, where she was (for the screens' ring) and the class's flooding model. Her turrets' bearings and her mounts'
    /// states as they stood are in the <see cref="HitScratch"/> it was taken into.</summary>
    struct HitInputs
    {
        public Hit Hit;
        public ulong Index;
        public long Now;
        public double Experience, Sea, Rudder;
        public int Entity, ClassIndex, Mounts;
        public Vec3d Position, Forward;
        public FloodModel Flood;
    }

    /// <summary>A hit's working space, one for each thread that works hits: the walk's path and its bender, and her turrets'
    /// bearings and mounts' states as the hit found them.</summary>
    sealed class HitScratch
    {
        public readonly HitPath Path = new();
        public readonly PathBender Bender = new();
        public double[] Bearings = new double[16];
        public MountState[] Mounts = new MountState[16];

        /// <summary>Room for a class's walk and mounts, made before her first hit.</summary>
        public void Reserve(ShipClass c)
        {
            Path.Reserve(c.Solids.Solids.Length);
            Bender.Reserve(c.Solids.Solids.Length);
            if (Bearings.Length < c.Mounts.Length)
                Bearings = new double[Math.Max(c.Mounts.Length, Bearings.Length * 2)];
        }

        public Span<MountState> MountsFor(int n)
        {
            if (Mounts.Length < n)
                Mounts = new MountState[Math.Max(n, Mounts.Length * 2)];
            return Mounts.AsSpan(0, n);
        }
    }

    /// <summary>What a hit did to her condition, for the world's part after it: the walk's outcome, the effects (with the race's),
    /// the race, her girder after it, the flags for the log, whether she was worked out again and with what change (for her mounts
    /// and power), whether it blew her up or broke her, and her record for the screens' ring.</summary>
    struct Worked
    {
        public TerminalResult T;
        public EffectsResult E;
        public RaceResult Race;
        public GirderReading Girder;
        public HitEffect Fx, Change;
        public bool Recomputed, Blew, Broke, Recorded;
        public Struck Record;
    }

    /// <summary>A hit lands, now: what it does to the ship, at once.</summary>
    public void Strike(World world, in Hit hit)
    {
        var store = ships.Store;
        int slot = store.SlotOf(hit.Ship);
        if (slot < 0 || Spared?.Invoke(hit) == true)
            return;
        var cls = store[slot].Handling.Class;
        var k = store.Combat.Condition(slot);
        touched = true;
        Ready(slot, k);
        Prepare(cls);
        var at = Capture(world, hit, slot, cls, scratch, (ulong)hits++);
        var w = Work(at, cls, k, scratch);
        Land(world, slot, k, at, w);
    }

    /// <summary>What a hit takes from the world when it comes, into a scratch (its mounts' states and her turrets' bearings).</summary>
    HitInputs Capture(World world, in Hit hit, int slot, ShipClass cls, HitScratch sx, ulong index)
    {
        var store = ships.Store;
        var ship = store[slot];
        long now = world.Time.Ms;
        var live = store.Combat.Mounts(slot);
        var mounts = sx.MountsFor(live.Length);
        live.CopyTo(mounts);
        sx.Reserve(cls);
        for (int m = 0; m < cls.Mounts.Length; m++)
            sx.Bearings[m] = CombatStore.BearingAt(mounts[m], cls.Mounts[m], now);
        return new HitInputs
        {
            Hit = hit, Index = index, Now = now, Experience = world.Gunnery.Experience(ship.Side), Sea = Sea(world, slot), Rudder = ship.Rudder,
            Entity = ship.Entity, ClassIndex = ship.ClassIndex, Mounts = live.Length, Position = ship.Position, Forward = ship.Forward, Flood = Flood(cls),
        };
    }

    /// <summary>What a hit does to her condition, from that, the class, the settings and what it took (pure: no world, no shared
    /// scratch; the thread's own gas network).</summary>
    Worked Work(in HitInputs at, ShipClass cls, ShipCondition k, HitScratch sx)
    {
        ref readonly var hit = ref at.Hit;
        long now = at.Now;
        bool near = hit.Source == HitSource.NearMiss, own = hit.Source == HitSource.Store;
        k.Damaged = true;
        k.Experience = at.Experience;   // her men's nerve (docs/battle/crew.md N3b)
        if (!near && !own)
            k.Hits++;
        (k.LastShooter, k.LastBattery) = (hit.Shooter, hit.Battery);

        var rng = stream.Derive(at.Index);
        ReadOnlySpan<MountState> mounts = sx.Mounts.AsSpan(0, at.Mounts);
        var net = GasNetwork.For(cls);
        net.Arm(mounts);   // a magazine the hit's gas lights stows what's left (EX6a)
        var e = WhatItDoes(hit, cls, k, now, sx, out var t, ref rng);
        // a magazine set off: its gas through her and what it leaves of her girder decide (docs/battle/explosions.md)
        var race = default(RaceResult);
        if (e.Magazine && k.Status == ShipStatus.Afloat)
        {
            if (e.Raced)
            {
                // its own gas lit it (EX6a): its race ran there; its draws the hit's own, not the next hit's (workers.md: keyed streams)
                var rr = stream.Derive(at.Index).Derive(0x8ACEDu);
                race = Magazines.Raced(cls, k, e.Blast, settings, now, ref rr, at.Sea);
            }
            else
                race = Magazines.SetOff(cls, k, e.MagazineRoom, mounts, settings, now, ref rng, at.Sea, e.Blast.WhipEM);
            if (race.Magazines > 0)
            {
                if (!race.Lost)
                    k.FloodDueMs = Math.Min(k.FloodDueMs, now + settings.FloodMs);   // what it opened takes the sea from her next second
                Join(ref e, race);
            }
        }
        net.Disarm();
        // what's left of her girder, and what's on it (docs/battle/explosions.md: after every explosion, one reading a moment)
        var girder = race.Girder;
        if (race.Magazines == 0 && k.Status == ShipStatus.Afloat && (!near || e.BreachM2 > 0 || e.BubbleWhip > 0))
            girder = HullGirder.Of(cls).Bear(k, e.Blast.WhipEM, now, at.Sea, settings, e.BubbleWhip);
        // its section's fire main and the fire parties there (docs/battle/fire.md B4)
        if (!near && k.Status == ShipStatus.Afloat)
            Fires.Hit(cls, at.Flood, k, cls.SectionAt(hit.Point.X), now, settings, ref rng);

        var w = new Worked { T = t, Race = race, Girder = girder, Fx = Flags(e) };
        // men hit or shaken change what her stations can do (docs/battle/crew.md N2, N3b)
        if ((e.Changed || e.Killed + e.Wounded > 0 || k.Stirred || girder.EndOff) && k.Status == ShipStatus.Afloat)
        {
            (w.Recomputed, w.Change) = (true, RecomputeOwn(k, at.Experience, at.Rudder, now, ref rng));
            w.Fx |= w.Change;
        }
        w.Blew = e.Magazine && race.Lost && k.Status == ShipStatus.Afloat;
        w.Broke = girder.Broken && !w.Blew && k.Status == ShipStatus.Afloat;
        if (w.Blew)
            w.Fx |= HitEffect.Magazine;
        if (w.Broke)
            w.Fx |= HitEffect.Broken;
        if (hit.Source is HitSource.Shell or HitSource.Coarse)
            (w.Recorded, w.Record) = (true, RecordOf(at, cls, t, e, sx.Path));
        w.E = e;
        return w;
    }

    /// <summary>A worked hit into the world: the race counted, her mounts and power as she now is, what the screens and the log
    /// show, a blast or a loss, her steps woken, and the stores it set off going off after it.</summary>
    void Land(World world, int slot, ShipCondition k, in HitInputs at, in Worked w)
    {
        ref readonly var hit = ref at.Hit;
        var cls = k.Class;
        long now = world.Time.Ms;
        bool near = hit.Source == HitSource.NearMiss, own = hit.Source == HitSource.Store;
        if (own)
            ships.Store.Combat.Spent(slot, hit.Store);   // her launcher's warheads gone with it
        if (w.Race.Magazines > 0)
        {
            MagazineCounts.ByHit[(int)w.Race.Tier]++;
            LogRace(slot, w.Race.Tier);
        }
        trainsLit += w.E.Stations > 0 ? 1 : 0;
        LastRace = w.Race;
        if (w.Recomputed)
        {
            RecomputeWorld(world, slot, k, w.Change);
            Remind(world, slot, k);
        }
        (LastTerminal, LastEffects, LastEffect) = (w.T, w.E, w.Fx);
        if (w.Recorded)
        {
            struck[struckNext] = w.Record;
            struckNext = (struckNext + 1) % struck.Length;
            struckCount = Math.Min(struckCount + 1, struck.Length);
        }
        var e = w.E;
        if (!near || w.Fx != HitEffect.None || e.Killed + e.Wounded > 0)   // a near miss that did nothing isn't news
            log.Add(new CombatEntry(now, near ? CombatEvent.NearMiss : own ? CombatEvent.Store : CombatEvent.Hit, hit.Shooter, hit.Battery, hit.Ship,
                own ? hit.Store : hit.ShellType, e.Part, w.T.Outcome, w.Fx, hit.Shell.Kind,
                (float)(hit.Source == HitSource.Torpedo ? hit.Torpedo.CalibreMm : hit.Shell.CalibreMm), hit.Source));
        if (w.Blew)
            Blew(world, slot, k, e.MagazineRoom, hit.Source switch { HitSource.Torpedo => BlastCause.Torpedo, HitSource.Store => BlastCause.Store, _ => BlastCause.Shell },
                hit.Point, w.Race);
        else if (w.Broke)
            Lose(world, slot, k, LossKind.BrokeInTwo);
        else if (k.Status == ShipStatus.Afloat && (e.BreachM2 > 0 || w.Girder.EndOff || e.Fire || Repairs.Busy(cls, k) || Magazines.Busy(cls, Flood(cls), k, settings)))
            Wake(world, slot, k);   // water or fire: its steps begin (or come sooner)
        GoOff(world, slot, k);
    }

    /// <summary>What a hit does, by what it is: a shell's walk through her and its burst, or an explosion in the water beside her.</summary>
    EffectsResult WhatItDoes(in Hit hit, ShipClass cls, ShipCondition k, long now, HitScratch sx, out TerminalResult t, ref RandomStream rng)
    {
        if (hit.Source == HitSource.Torpedo)
        {
            // an underwater explosion against the hull (4.6): no path of its own
            var e = Underwater.Apply(cls, k, hit, settings, ref rng, out var outcome);
            t = new TerminalResult { Outcome = outcome, Plate = -1 };
            return e;
        }
        if (hit.Source == HitSource.Store)
        {
            // a store of hers going off where it is (EX6c): a burst at rest, no path; her launcher's warheads gone with it (the
            // launcher's own tubes and spares in the world's part, Land)
            t = new TerminalResult { Outcome = Outcome.Exploded, Burst = BurstKind.Inside, Plate = -1, Glance = -1, Spall = double.NaN, PlateU = double.NaN };
            var e = Effects.Detonate(cls, k, hit.Store, now, settings, ref rng);
            k.StoreLit[hit.Store] = false;
            k.Warheads[hit.Store] = 0;
            return e;
        }
        if (hit.Source == HitSource.NearMiss)
        {
            // a burst in the water outside the hull (E7): no path either
            t = new TerminalResult { Outcome = hit.Force < 1 ? Outcome.LowOrder : Outcome.BurstOutside, Burst = BurstKind.Outside, Plate = -1, Glance = -1,
                Spall = double.NaN, PlateU = double.NaN, BurstMs = hit.SpeedMs };
            return NearMiss.Apply(cls, k, hit, settings, ref rng);
        }
        // its own path: from just outside the point along the flight, through the whole ship
        int n = cls.Mounts.Length;
        var d = hit.Direction;
        double far = 2 * cls.Solids.Radius + 10;
        var p0 = new P3(hit.Point.X - d.X, hit.Point.Y - d.Y, hit.Point.Z - d.Z);
        var p1 = new P3(hit.Point.X + d.X * far, hit.Point.Y + d.Y * far, hit.Point.Z + d.Z * far);
        var water = HitQuery.WaterOf(cls, k.Attitude);
        ShipTracer.Trace(cls, p0, p1, sx.Bearings.AsSpan(0, n), water, sx.Path);
        sx.Bender.Set(sx.Bearings, n, water);
        t = Terminal.Resolve(cls, hit, sx.Path, sx.Bender, lotStream.Derive((ulong)hit.Ship), settings, ref rng, k, Trace);
        return Effects.Apply(cls, k, hit, sx.Path, t, settings, ref rng);
    }

    /// <summary>How far past her far side a shell that bursts beyond her goes off, m [EST]: the walk stops at her side.</summary>
    const double BeyondM = 5;

    /// <summary>A shell's hit for the screens' ring (<see cref="Struck"/>): where it burst and after how long, where it left the
    /// ship if it flew on (the last time the path left her hull or a part, on the leg it was on then; a glance off her side that
    /// never got in leaves at the plate), its filler's W and what its burst tore open to the outside.</summary>
    static Struck RecordOf(in HitInputs at, ShipClass cls, in TerminalResult t, in EffectsResult e, HitPath path)
    {
        ref readonly var hit = ref at.Hit;
        var shell = hit.Shell;
        var shows = HitShows.None;
        if (t.Underwater)
            shows |= HitShows.Underwater;
        if (e.Fire)
            shows |= HitShows.Fire;
        if (e.Blast.Open)
            shows |= HitShows.Open;
        if (e.Spall)
            shows |= HitShows.Spall;
        // the path starts a metre short of the hit, along the flight
        P3 burstAt = default;
        double burstS = 0;
        if (t.Bursts && double.IsFinite(t.BurstT))
        {
            burstAt = path.At(t.BurstT);
            burstS = Math.Max(t.BurstT - 1, 0) / Math.Max(0.5 * (t.StrikeMs + t.BurstMs), 30);
        }
        P3 exitAt = default, exitDir = default;
        double exitMs = 0;
        if (t.Outcome is Outcome.PassedThrough or Outcome.Ricochet or Outcome.BurstBeyond)
        {
            var xs = path.Crossings;
            double from = t.Glance >= 0 && t.Glance < xs.Length ? xs[t.Glance].T : 0, exitT = double.NaN;
            for (int i = xs.Length - 1; i >= 0 && double.IsNaN(exitT); i--)
                if (xs[i].T >= from && xs[i].Kind is CrossingKind.HullOut or CrossingKind.PartOut)
                    exitT = xs[i].T;
            if (double.IsNaN(exitT) && t.Glance >= 0)
                exitT = from;
            if (!double.IsNaN(exitT))
            {
                exitAt = path.At(exitT);
                exitDir = path.LegOf(path.Leg(exitT)).Direction;
                if (t.Outcome == Outcome.BurstBeyond)
                {
                    // the walk keeps no point past her far side: it bursts a few metres out [EST]
                    burstAt = new P3(exitAt.X + exitDir.X * BeyondM, exitAt.Y + exitDir.Y * BeyondM, exitAt.Z + exitDir.Z * BeyondM);
                    burstS = Math.Max(exitT - 1 + BeyondM, 0) / Math.Max(t.StrikeMs, 30);
                    (exitAt, exitDir) = (default, default);
                }
                else
                {
                    // its speed out [EST]: the plates' toll isn't kept; a glance leaves at about half the speed it met the plate
                    exitMs = t.Outcome == Outcome.Ricochet ? 0.5 * (double.IsFinite(t.PlateMs) && t.PlateMs > 0 ? t.PlateMs : t.StrikeMs) : 0.85 * t.StrikeMs;
                    if (exitAt.Z > cls.Vertical.Waterline)
                        shows |= HitShows.Exit;
                }
            }
        }
        double w = shell.FillerKg * shell.FillerRe * (t.Outcome == Outcome.LowOrder ? Math.Min(t.Force, 1) : 1);
        // its pieces of 50 g and more (Mott: N(>m) = N₀ e^(−√m / M_k)), when it burst
        double pieces = 0;
        if ((t.Bursts || t.Outcome == Outcome.BurstBeyond) && shell.Fragments is { } f)
        {
            var fb = f.Of(t.Outcome == Outcome.LowOrder);
            pieces = fb.MottK > 0 ? fb.MottN0 * Math.Exp(-Math.Sqrt(0.05) / fb.MottK) : 0;
        }
        return new Struck(hit.Ms, at.Entity, at.ClassIndex, hit.Shooter, at.Position, at.Forward, hit.Point, hit.Direction,
            t.Outcome, t.Burst, burstAt, burstS, exitAt, exitDir, exitMs, shell.CalibreMm, w, shell.FillerRe, e.Blast.HoleM2, pieces, shell.Kind, shows);
    }
}
