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
    /// <summary>A magazine set off (V3; docs/battle/explosions.md EX2): its stow lit and its gas through the ship
    /// (<see cref="GasNetwork.Burn"/>), the same gas and plate as any explosion's; what it left of her girder read
    /// (<see cref="HullGirder.Bear"/>). What it leaves when she lives: the magazines alight spent (their mounts out of ammunition), the
    /// vented gunhouses and barbettes destroyed and burning, the trains' men flashed (a contained one, the magazine's), the men and
    /// gear in the spaces its gas reached, the holes the network tore. alsoWhipEM: what else drove her at this moment (the shell that
    /// set it off), read with it: one moment, one reading of her girder. Allocation-free.</summary>
    public static RaceResult SetOff(ShipClass c, ShipCondition k, int room, ReadOnlySpan<MountState> mounts, SimSettings s, long now,
        ref RandomStream r, double sea = 0, double alsoWhipEM = 0)
    {
        var res = new RaceResult { VentS = double.NaN, StructureS = double.NaN };
        if (room < 0 || k.MagazineSpent[room])
            return res;   // it has gone up already: nothing there to burn
        var net = GasNetwork.For(c);
        var gas = net.Burn(k, room, mounts, s, ref r);
        if (gas.Magazines == 0)
        {
            k.MagazineSpent[room] = true;   // a room with no charge in it (a carrier's bombs, not in the model yet): nothing to go up, now or later
            return res;
        }
        var girder = HullGirder.Of(c).Bear(k, gas.WhipEM + alsoWhipEM, now, sea, s);
        res.Gas = gas;
        (res.PeakKPa, res.BurnedKg, res.FastKg, res.Charges, res.VentM2, res.VentS, res.StructureS) = (gas.PeakKPa, gas.BurnedKg, gas.FastKg, gas.Charges, gas.VentM2, gas.VentS, gas.HullS);
        (res.Magazines, res.Vented, res.Lifted) = (gas.Magazines, gas.Vented, gas.Lifted);
        res.Gave = (gas.Bulkheads > 0 ? 1 : 0) | (gas.Decks > 0 ? 2 : 0) | (gas.Sides > 0 ? 4 : 0) | (gas.Bottoms > 0 ? 8 : 0) | (gas.WeatherDecks > 0 ? 16 : 0);
        res.Girder = girder;
        bool breached = girder.EndOff || gas.Sides + gas.WeatherDecks + gas.Bottoms + gas.Decks > 0;
        bool contained = !girder.Broken && !breached && gas.Vented == 0;
        res.Tier = girder.Broken ? (gas.Magazines > 1 ? MagazineTier.TwoGroups : MagazineTier.Severed)
            : breached ? MagazineTier.Breached : contained ? MagazineTier.Contained : MagazineTier.BurnedOut;
        Aftermath(c, k, net, contained, ref res, s, now, ref r);
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

    /// <summary>The pressure the lit charges of a stow burn at, Pa: above the room's by what it takes to drive their gas out through
    /// the gaps between them, a packed bed's inertial drop (Ergun), ΔP = K·G²/ρ, with G the kg a m² a second through the bed and ρ = P/RT.
    /// As G goes with r(P), P·(P − room) = C·r(P)², where C is K·RT·(G/r)²: one root above the room's, found by Newton's from the last
    /// (it moves little in a step). C 0: the room's.</summary>
    internal static double BedPa(double roomPa, double c, in PropellantRow pr, double guess)
    {
        if (!(c > 0))
            return roomPa;
        double p = Math.Max(guess, roomPa);
        for (int i = 0; i < 3; i++)
        {
            double r = Rate(p, pr), rhs = 4 * c * r * r, root = Math.Sqrt(roomPa * roomPa + rhs);
            double alpha = p / 1e6 >= pr.KneeMPa ? pr.AlphaHi : pr.AlphaLo;
            p = Math.Max(p - (p - (roomPa + root) / 2) / Math.Max(1 - rhs * alpha / (2 * p * root), 0.05), roomPa);
        }
        return p;
    }

    /// <summary>What the fire left (a lost ship's too: her men in the trains): the magazines alight spent, the gunhouses it threw and
    /// those it vented destroyed and burning with their barbettes, the trains' men flashed (a contained fire, the magazine's own), and
    /// the men and gear in the spaces the gas reached.</summary>
    static void Aftermath(ShipClass c, ShipCondition k, GasNetwork net, bool contained, ref RaceResult res, SimSettings s, long now, ref RandomStream r)
    {
        Casualties.Begin(k, now);
        var fx = new EffectsResult { Part = -1, MagazineRoom = -1 };
        for (int i = 0; i < net.LitCount; i++)
        {
            int rm = net.LitRoom(i);
            Wreck(k, c.RoomNode(rm), ref fx);
            if (contained)
                foreach (int cell in c.Links(c.Rooms[rm].Cells))
                    Casualties.Flash(k, Casualties.Cell(c, cell), true, s);
            else
                Casualties.Train(c, k, rm, s);
        }
        for (int m = 0; m < c.Mounts.Length; m++)
        {
            ref readonly var mr = ref c.Mounts[m];
            if (net.LiftedMount(m))
                Wreck(k, mr.Component, ref fx);
            if (!net.VentedMount(m) || !c.Enclosed(m))
                continue;
            Wreck(k, mr.Component, ref fx);
            if (mr.Barbette >= 0)
                Wreck(k, mr.Barbette, ref fx);
            Fires.Ignite(c, k, c.FireOfPart(mr.Component), s.ReadyAmmoSeatMW, s);
        }
        Effects.GasFelt(c, k, net, -1, s, ref fx);
        Casualties.Draw(c, k, now, s, ref fx, ref r);
        (res.Killed, res.Wounded, res.Destroyed, res.Disabled, res.Damaged) = (fx.Killed, fx.Wounded, fx.Destroyed, fx.Disabled, fx.Damaged);
        k.Version++;
    }

    static void Wreck(ShipCondition k, int node, ref EffectsResult fx) =>
        Effects.Count(ref fx, k, node, k.Worsen(node, PartState.Destroyed) ? PartState.Destroyed : PartState.Ok);
}
