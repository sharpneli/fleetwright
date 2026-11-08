using Fleetwright.Shipgen;
using Fleetwright.Sim.Core;
using Fleetwright.Sim.Ships;

namespace Fleetwright.Sim.Combat;

/// <summary>How a magazine fire ended, as a report (docs/battle/explosions.md: read by no logic): contained (flooded, or the
/// vents kept up), a gunhouse burned out (the gas left through its trunk, the ship fights on), a local breach (her plating gave
/// and the girder held), the ship broken, or broken with two magazine groups alight.</summary>
public enum MagazineTier : byte
{
    Contained,
    BurnedOut,
    Breached,
    Severed,
    TwoGroups,
}

/// <summary>A magazine set off (<see cref="Magazines.SetOff"/>): its tier, the peak gauge pressure (kPa), the propellant burned
/// (kg; and by the hull's first giving and a second after, the fireball's), the charges that were there, the share of the
/// girder's worst section lost, the vents at the end (m²), the magazines alight, the gunhouses vented (and lifted), when the first
/// vented and when her hull first gave (s from the flash; NaN none), the men it killed and wounded, and the parts and spaces it
/// wrecked, put out of action and damaged.</summary>
public struct RaceResult
{
    public MagazineTier Tier;
    public double PeakKPa, BurnedKg, FastKg, Charges, VentM2, VentS, StructureS;
    public int Magazines, Vented, Lifted, Killed, Wounded, Destroyed, Disabled, Damaged;
    /// <summary>What gave, as flags: 1 a bulkhead, 2 a deck, 4 the sides, 8 the bottom, 16 the weather deck.</summary>
    public int Gave;
    /// <summary>Spaces the gas reached, and what it did there: what the flooding has from it.</summary>
    public GasResult Gas;
    /// <summary>What it left of her girder: broken, she's lost.</summary>
    public GirderReading Girder;
    public readonly bool Lost => Girder.Broken;
    /// <summary>The share of the worst section lost; its place along her, 0 the stern to 1 the bow; the moment there over what's left
    /// of its capacity (1 or more: broken); whether it took an end off her (forward or aft of the cut flooding).</summary>
    public readonly double D => Girder.D;
    public readonly double Station => Girder.Station;
    public readonly double GirderLoad => Girder.Load;
    public readonly bool EndLost => Girder.EndOff;
}

public static partial class Magazines
{
    /// <summary>A magazine set off in still air and its gas run to its end at once (a bench's, a test's: V3; docs/battle/explosions.md
    /// EX2): its stow lit and its gas through the ship (<see cref="GasNetwork.AddStow"/>), the same gas and plate as any explosion's,
    /// what it did landed on her as the game lands it (<see cref="GasNetwork.Land"/>), then what it left of her girder read
    /// (<see cref="HullGirder.Bear"/>). In the game a magazine lights in her live gas and lands slice by slice
    /// (docs/battle/workers.md ET3). alsoWhipEM: what else drove her at this moment (the shell that set it off), read with it.</summary>
    public static RaceResult SetOff(ShipClass c, ShipCondition k, int room, ReadOnlySpan<MountState> mounts, SimSettings s, long now,
        ref RandomStream r, double sea = 0, double alsoWhipEM = 0, int sliceMs = 0)
    {
        var res = new RaceResult { VentS = double.NaN, StructureS = double.NaN };
        if (room < 0 || k.MagazineSpent[room])
            return res;   // it has gone up already: nothing there to burn
        var net = GasNetwork.For(c);
        var fx = new EffectsResult { Part = -1, MagazineRoom = -1 };
        if (sliceMs > 0)
            return Sliced(c, k, room, mounts, s, now, ref r, sea, alsoWhipEM, sliceMs, net, ref fx, res);
        var gas = net.BurnOut(k, room, mounts, s, now, ref r, ref fx);
        if (gas.Magazines == 0)
        {
            k.MagazineSpent[room] = true;   // a room with no charge in it (a carrier's bombs, not in the model yet): nothing to go up, now or later
            return res;
        }
        return Result(gas, HullGirder.Of(c).Bear(k, net.TakeWhip() + alsoWhipEM, now, sea, s), fx);
    }

    /// <summary>The race a slice at a time (a bench's: docs/battle/workers.md ET4), her girder read each slice as the world reads it (what's
    /// gone of her section then, what her gas drives her now) to the first that breaks her; the worst reading.</summary>
    static RaceResult Sliced(ShipClass c, ShipCondition k, int room, ReadOnlySpan<MountState> mounts, SimSettings s, long now, ref RandomStream r, double sea,
        double alsoWhipEM, int sliceMs, GasNetwork net, ref EffectsResult fx, RaceResult none)
    {
        var girder = default(GirderReading);
        var hull = HullGirder.Of(c);
        var gas = net.BurnOut(k, room, mounts, s, now, ref r, ref fx, sliceMs, g =>
        {
            var reading = hull.Bear(k, g.TakeWhip() + alsoWhipEM, g.NowMs, sea, s);
            alsoWhipEM = 0;
            if (reading.Load > girder.Load || reading.Broken)
                girder = reading;
            return reading.Broken;
        });
        if (gas.Magazines == 0)
        {
            k.MagazineSpent[room] = true;
            return none;
        }
        return Result(gas, girder, fx);
    }

    /// <summary>A magazine fire's outcome as a report (read by no logic): what its gas did, what's left of her girder, and what it did to
    /// her men and parts.</summary>
    public static RaceResult Result(in GasResult gas, in GirderReading girder, in EffectsResult fx)
    {
        var res = new RaceResult { Gas = gas, Girder = girder, Tier = Tier(gas, girder) };
        (res.PeakKPa, res.BurnedKg, res.FastKg, res.Charges, res.VentM2, res.VentS, res.StructureS) = (gas.PeakKPa, gas.BurnedKg, gas.FastKg, gas.Charges, gas.VentM2, gas.VentS, gas.HullS);
        (res.Magazines, res.Vented, res.Lifted) = (gas.Magazines, gas.Vented, gas.Lifted);
        res.Gave = (gas.Bulkheads > 0 ? 1 : 0) | (gas.Decks > 0 ? 2 : 0) | (gas.Sides > 0 ? 4 : 0) | (gas.Bottoms > 0 ? 8 : 0) | (gas.WeatherDecks > 0 ? 16 : 0);
        (res.Killed, res.Wounded, res.Destroyed, res.Disabled, res.Damaged) = (fx.Killed, fx.Wounded, fx.Destroyed, fx.Disabled, fx.Damaged);
        return res;
    }

    /// <summary>How a magazine fire ended: broken (two groups alight or one), a breach (her plating gave, or an end came off), contained
    /// (nothing vented), else burned out through a gunhouse.</summary>
    public static MagazineTier Tier(in GasResult gas, in GirderReading girder)
    {
        bool breached = girder.EndOff || gas.Sides + gas.WeatherDecks + gas.Bottoms + gas.Decks > 0;
        return girder.Broken ? (gas.Magazines > 1 ? MagazineTier.TwoGroups : MagazineTier.Severed)
            : breached ? MagazineTier.Breached : gas.Vented == 0 ? MagazineTier.Contained : MagazineTier.BurnedOut;
    }

    /// <summary>A magazine's stow: the charges lying open (in the scuttles, the discipline's charges a station a gun, its
    /// stack, and the share with their lids off; all of them in bags alone), those still in their packages, and their kg.
    /// rounds: what each mount has left.</summary>
    internal static void Stow(ShipClass c, int room, ReadOnlySpan<int> rounds, in FlashDiscipline flash, out double nOpen, out double nCased, out double massSum)
    {
        (nOpen, nCased, massSum) = (0, 0, 0);
        for (int m = 0; m < c.Mounts.Length; m++)
        {
            ref readonly var mr = ref c.Mounts[m];
            if (mr.Magazine != room || mr.Battery < 0)
                continue;
            var ch = c.Charges[mr.Type];
            int left = rounds[m];
            double all = left == int.MaxValue ? Math.Max(mr.Rounds, 0) : Math.Max(left, 0);
            double open = ch.Packaging == Packaging.Bag ? all
                : Math.Min(all, flash.StationCharges * c.TurretTypes[mr.Type].Barrels + flash.StackShare * Math.Max(mr.Rounds, 0) + flash.LidsOff * all);
            (nOpen, nCased, massSum) = (nOpen + open, nCased + all - open, massSum + all * ch.MassKg);
        }
    }

    /// <summary>The share of a magazine's stow under water: its water over its spaces' room for it.</summary>
    internal static double Flooded(ShipClass c, ShipCondition k, int room)
    {
        double water = 0, space = 0;
        foreach (int cell in c.Links(c.Rooms[room].Cells))
        {
            ref readonly var x = ref c.Cells[cell];
            water += k.WaterM3[cell];
            space += x.VolumeM3 * Math.Clamp(x.Permeability, 0.05, 1);
        }
        return space > 0 ? Math.Clamp(water / space, 0, 1) : 0;
    }

    /// <summary>The linear burning rate, m/s, at absolute pressure p (Pa): two branches meeting at the knee.</summary>
    internal static double Rate(double p, in PropellantRow pr)
    {
        double pm = Math.Max(p, 0.02e6) / 1e6;
        if (pm >= pr.KneeMPa)
            return pr.BetaMmS * Math.Pow(pm, pr.AlphaHi) / 1000;
        return pr.BetaMmS * Math.Pow(pr.KneeMPa, pr.AlphaHi) * Math.Pow(pm / pr.KneeMPa, pr.AlphaLo) / 1000;
    }
}
