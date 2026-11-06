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
/// vented and when her hull first gave (s from the flash; NaN none), and the men it killed and wounded.</summary>
public struct RaceResult
{
    public MagazineTier Tier;
    public double PeakKPa, BurnedKg, FastKg, Charges, D, VentM2, VentS, StructureS;
    public int Magazines, Vented, Lifted, Killed, Wounded;
    /// <summary>What gave, as flags: 1 a bulkhead, 2 a deck, 4 the sides, 8 the bottom.</summary>
    public int Gave;
    /// <summary>The worst section's place along her, 0 the stern to 1 the bow; the moment there over what's left of its capacity
    /// (1 or more: broken); whether it took an end off her (forward or aft of the cut flooding).</summary>
    public double Station, GirderLoad;
    public bool EndLost;
    /// <summary>Spaces the gas reached, and what it did there: what the flooding has from it.</summary>
    public GasResult Gas;
    public readonly bool Lost => Tier >= MagazineTier.Severed;
}

public static partial class Magazines
{
    /// <summary>A magazine set off (V3; docs/battle/explosions.md EX2): its stow lit and its gas through the ship
    /// (<see cref="GasNetwork.Burn"/>), the same gas and plate as any explosion's; what it left of her girder read
    /// (<see cref="HullGirder.Bear"/>). What it leaves when she lives: the magazines alight spent (their mounts out of ammunition), the
    /// vented gunhouses and barbettes destroyed and burning, the trains' men flashed (a contained one, the magazine's), the men and
    /// gear in the spaces its gas reached, the holes the network tore. Allocation-free.</summary>
    public static RaceResult SetOff(ShipClass c, ShipCondition k, int room, ReadOnlySpan<MountState> mounts, SimSettings s, long now,
        ref RandomStream r, double sea = 0)
    {
        var res = new RaceResult { VentS = double.NaN, StructureS = double.NaN };
        if (room < 0)
            return res with { Tier = MagazineTier.Severed };
        if (k.MagazineSpent[room])
            return res;   // it has gone up already: nothing there to burn
        var net = GasNetwork.For(c);
        var gas = net.Burn(k, room, mounts, s, ref r);
        if (gas.Magazines == 0)
            return res with { Tier = MagazineTier.Severed };   // nothing known to burn: as before
        var girder = HullGirder.Of(c).Bear(k, gas.WhipEM, now, sea, s);
        res.Gas = gas;
        (res.PeakKPa, res.BurnedKg, res.FastKg, res.Charges, res.VentM2, res.VentS, res.StructureS) = (gas.PeakKPa, gas.BurnedKg, gas.FastKg, gas.Charges, gas.VentM2, gas.VentS, gas.HullS);
        (res.Magazines, res.Vented, res.Lifted) = (gas.Magazines, gas.Vented, gas.Lifted);
        res.Gave = (gas.Bulkheads > 0 ? 1 : 0) | (gas.Decks > 0 ? 2 : 0) | (gas.Sides > 0 ? 4 : 0) | (gas.Bottoms > 0 ? 8 : 0);
        (res.D, res.Station, res.GirderLoad, res.EndLost) = (girder.D, girder.Station, girder.Load, girder.EndOff);
        bool hullGave = gas.Sides + gas.WeatherDecks + gas.Bottoms + gas.Decks > 0;
        res.Tier = girder.Broken ? (gas.Magazines > 1 ? MagazineTier.TwoGroups : MagazineTier.Severed)
            : hullGave || girder.EndOff ? MagazineTier.Breached : gas.Vented > 0 ? MagazineTier.BurnedOut : MagazineTier.Contained;
        Aftermath(c, k, net, ref res, s, now, ref r);
        return res;
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

    /// <summary>What the fire left (a lost ship's too: her men in the trains): the magazines alight spent, the gunhouses it threw and
    /// those it vented destroyed and burning with their barbettes, the trains' men flashed (a contained fire, the magazine's own), and
    /// the men and gear in the spaces the gas reached.</summary>
    static void Aftermath(ShipClass c, ShipCondition k, GasNetwork net, ref RaceResult res, SimSettings s, long now, ref RandomStream r)
    {
        Casualties.Begin(k, now);
        for (int i = 0; i < net.LitCount; i++)
        {
            int rm = net.LitRoom(i);
            k.Worsen(c.RoomNode(rm), PartState.Destroyed);
            if (res.Tier == MagazineTier.Contained)
                foreach (int cell in c.Links(c.Rooms[rm].Cells))
                    Casualties.Flash(k, Casualties.Cell(c, cell), true, s);
            else
                Casualties.Train(c, k, rm, s);
        }
        for (int m = 0; m < c.Mounts.Length; m++)
        {
            ref readonly var mr = ref c.Mounts[m];
            if (net.LiftedMount(m))
                k.Worsen(mr.Component, PartState.Destroyed);
            if (!net.VentedMount(m) || !c.Enclosed(m))
                continue;
            k.Worsen(mr.Component, PartState.Destroyed);
            if (mr.Barbette >= 0)
                k.Worsen(mr.Barbette, PartState.Destroyed);
            Fires.Ignite(c, k, c.FireOfPart(mr.Component), s.ReadyAmmoSeatMW, s);
        }
        var fx = new EffectsResult { Part = -1, MagazineRoom = -1 };
        foreach (int cell in net.Reached())
            Casualties.Gas(k, Casualties.Cell(c, cell), net.FeltMPa(cell), s);
        Effects.Felt(c, k, net, s, ref fx);
        Casualties.Draw(c, k, now, s, ref fx, ref r);
        (res.Killed, res.Wounded) = (fx.Killed, fx.Wounded);
        k.Version++;
    }
}
