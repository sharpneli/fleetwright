using Fleetwright.Sim.Combat;
using Fleetwright.Sim.Core;

namespace Fleetwright.Sim;

/// <summary>Her gas in time (docs/battle/workers.md ET): an explosion's gas is a process on her, stepped by the world on a lattice
/// of its own (<see cref="SimSettings.GasSliceMs"/>) from her first explosion until it's done, what it did landing on her each time
/// (<see cref="GasNetwork.Land"/>), then the world's part here: her girder read, what she can do worked out again, her water's step
/// woken, the stores it set off, and how a magazine fire ended, counted when it's over. Every tier alike: a race takes its minutes at
/// any speed.</summary>
public sealed partial class DamageSystem
{
    const ushort GasEvent = 4;

    /// <summary>Her gas's next slice, on the lattice, if she has gas and none is due.</summary>
    void GasWake(World world, int slot, ShipCondition k)
    {
        if (k.Gas is null || k.GasNextMs >= 0 || k.Status != ShipStatus.Afloat)
            return;
        long d = settings.GasSliceMs;
        k.GasNextMs = (world.Time.Ms / d + 1) * d;
        k.GasGen++;
        world.Schedule(new WorldTime(k.GasNextMs), EventTier.Fine, ships.Store[slot].Entity, Id, GasEvent, ships.Store[slot].Generation, k.GasGen);
    }

    /// <summary>Her gas's slice: stepped to now and landed, and the next one's event while she has gas.</summary>
    void RunGas(World world, int slot, ShipCondition k, long gen)
    {
        if (gen != k.GasGen)
            return;
        k.GasNextMs = -1;
        GasTo(world, slot, k, world.Time.Ms);
        GasWake(world, slot, k);
    }

    /// <summary>A magazine alight in her gas from now (a hit's pieces or flash, her fire's heat): its stow into her run; what it does
    /// lands slice by slice. Whether it lit.</summary>
    bool Ignite(World world, int slot, ShipCondition k, int room, BlastCause cause, long now)
    {
        var net = GasNetwork.Of(k, settings, now);
        net.Arm(ships.Store.Combat.Mounts(slot));
        if (!net.AddStow(room, default))
            return false;
        if (net.MagazineCause < 0)
            net.MagazineCause = (int)cause;
        return true;
    }

    /// <summary>Her gas stepped to ms and what it did there landed on her, then the world's part: her girder read if her section
    /// lost something or her gas drives her (broken: lost, to her magazine if one burns in it), what she can do worked out again, the
    /// sea's way in woken, the stores it lit going off; done, how it ended.</summary>
    void GasTo(World world, int slot, ShipCondition k, long ms)
    {
        if (k.Gas is not { } net)
            return;
        if (k.Status != ShipStatus.Afloat)
        {
            EndGas(slot, k);
            return;
        }
        touched = true;
        k.Damaged = true;
        var cls = k.Class;
        var ship = ships.Store[slot];
        net.Arm(ships.Store.Combat.Mounts(slot));
        net.AdvanceTo(ms);
        var rng = stream.Derive(0x6A5u).Derive((ulong)ship.Entity).Derive((ulong)ms);
        var fx = net.Land(ms, ref rng);
        if (summing)
            Sum(ref gasSum, fx);
        if (fx.Magazine && net.MagazineCause < 0)
            net.MagazineCause = Math.Max(net.SourceCause, 0);   // its gas lit one: as what made the gas
        bool opened = fx.BreachM2 > 0 || fx.Blast.OpenM2 > 0 || fx.Magazine;
        if (opened)
            k.FloodDueMs = Math.Min(k.FloodDueMs, ms + settings.FloodMs);   // what it opened takes the sea from her next second
        double whip = net.TakeWhip();
        if (k.GirderStale || whip > 0)
        {
            var girder = HullGirder.Of(cls).Bear(k, whip, ms, Sea(world, slot), settings);
            net.Girder = girder;
            if (girder.Broken)
            {
                var gas = net.Report();
                if (gas.Magazines > 0)
                    Blew(world, slot, k, net.LitRoom(0), (BlastCause)Math.Max(net.MagazineCause, 0), net.SourceAt, Magazines.Result(gas, girder, fx));
                else
                    Lose(world, slot, k, LossKind.BrokeInTwo);
                return;
            }
            if (girder.EndOff)
                k.FloodDueMs = Math.Min(k.FloodDueMs, ms + settings.FloodMs);
        }
        if (fx.Changed || fx.Killed + fx.Wounded > 0)
        {
            var change = RecomputeOwn(k, world.Gunnery.Experience(ship.Side), ship.Rudder, ms, ref rng);
            RecomputeWorld(world, slot, k, change);
        }
        if (opened || fx.Fire || Repairs.Busy(cls, k) || Magazines.Busy(cls, Flood(cls), k, settings))
            Wake(world, slot, k);
        GoOff(world, slot, k);
        if (k.Gas == net && net.Done)
            EndGas(slot, k);
    }

    /// <summary>Her gas over (done, or she's lost): a magazine fire in it counted by how it ended (by her girder's last reading), and
    /// the network back to its pool.</summary>
    void EndGas(int slot, ShipCondition k)
    {
        if (k.Gas is not { } net)
            return;
        var gas = net.Report();
        if (gas.Magazines > 0)
        {
            var tier = Magazines.Tier(gas, net.Girder);
            (net.MagazineCause == (int)BlastCause.Fire ? MagazineCounts.ByFire : MagazineCounts.ByHit)[(int)tier]++;
            LogRace(slot, tier);
            LastRace = Magazines.Result(gas, net.Girder, default);
        }
        LastGas = gas;
        net.Release();
        (k.Gas, k.GasNextMs) = (null, -1);
        k.GasGen++;
    }

    /// <summary>What the last gas to end did, as a result (tools and benches: not world state).</summary>
    public GasResult LastGas { get; private set; }

    /// <summary>A bench's (every hit at one moment, each read for what it did): a hit, then her gas stepped on slice by slice to its end
    /// as the world would step it, without the world's clock moving, through the same landings. <see cref="LastEffects"/> is what the
    /// hit did with what its gas did after (its men, parts, openings, a magazine it lit; Blast its gas's result), and
    /// <see cref="LastRace"/> how a magazine fire in it ended. A bench whose world runs its clock between hits needs none of this.</summary>
    public void StrikeOut(World world, in Hit hit)
    {
        Strike(world, hit);
        var fx = LastEffects;
        (summing, gasSum) = (true, new EffectsResult { Part = -1, MagazineRoom = -1 });
        LastRace = default;
        try
        {
            var gas = RunGasOut(world, hit.Ship);
            if (gas.Steps > 0 || gas.Magazines > 0)
                fx.Blast = gas with { Spaces = Math.Max(gas.Spaces, fx.Blast.Spaces), Open = fx.Blast.Open, HoleM2 = gas.HoleM2 };
        }
        finally
        {
            summing = false;
        }
        Sum(ref fx, gasSum);
        if (gasSum.Magazine && !fx.Magazine)
            (fx.Magazine, fx.MagazineRoom, fx.MagazineVia) = (true, gasSum.MagazineRoom, gasSum.MagazineVia);
        LastEffects = fx;
    }

    bool summing;
    EffectsResult gasSum;

    /// <summary>A landing's toll added to a hit's.</summary>
    static void Sum(ref EffectsResult into, in EffectsResult x)
    {
        (into.Killed, into.Wounded, into.CrewLost) = (into.Killed + x.Killed, into.Wounded + x.Wounded, into.CrewLost + x.CrewLost);
        (into.Destroyed, into.Disabled, into.Damaged) = (into.Destroyed + x.Destroyed, into.Disabled + x.Disabled, into.Damaged + x.Damaged);
        (into.BreachM2, into.Fire, into.Stations) = (into.BreachM2 + x.BreachM2, into.Fire || x.Fire, into.Stations + x.Stations);
        if (x.Magazine && !into.Magazine)
            (into.Magazine, into.MagazineRoom, into.MagazineVia) = (true, x.MagazineRoom, x.MagazineVia);
    }

    /// <summary>A tool's: a ship's gas stepped on slice by slice from now until it's done, as the world's events would step it, without
    /// the world's clock moving. What it did, as a result.</summary>
    public GasResult RunGasOut(World world, int entity)
    {
        int slot = ships.Store.SlotOf(entity);
        if (slot < 0)
            return default;
        var k = ships.Store.Combat.Condition(slot);
        k.GasGen++;   // the world's own slices for it are cancelled: these take their place
        k.GasNextMs = -1;
        SettleGas(world, slot, k, long.MaxValue);
        return LastGas;
    }

    /// <summary>Her gas stepped on its lattice from where it is to until (or its end), each slice landed.</summary>
    void SettleGas(World world, int slot, ShipCondition k, long until)
    {
        long d = settings.GasSliceMs;
        while (k.Gas is { } net && k.Status == ShipStatus.Afloat)
        {
            long next = (net.NowMs / d + 1) * d;
            if (next > until)
                break;
            GasTo(world, slot, k, next);
        }
        if (k.Gas is not null && k.Status != ShipStatus.Afloat)
            EndGas(slot, k);
    }
}
