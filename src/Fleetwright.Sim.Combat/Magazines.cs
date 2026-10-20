using Fleetwright.Sim.Core;
using Fleetwright.Sim.Ships;

namespace Fleetwright.Sim.Combat;

/// <summary>The magazines (docs/battle/magazines.md). V2, the ammunition trains: charges lit at a station of a mount's train
/// (<see cref="ShipClass.Train"/>) burn there, and the flame goes on up and down the train past each door that stands
/// open (research/simulation/damage-research/10-magazine-flash-chain.md §3). A flame that reaches the magazine sets it
/// off: until the venting race (V3) the ship is lost with it, as before.
/// <list type="bullet">
/// <item><b>The doors:</b> each station's door to the next one down (the gunhouse's floor and cages, the trunk's lower
/// doors, the handling room's scuttle to the magazine, a casemate's hoist and passage) stands open
/// <see cref="SimSettings.StationDoorOpen"/> of the time, a roll each, the flame's way down or up.</item>
/// <item><b>A station the flame reaches</b> burns the charges lying there (<see cref="SimSettings.StationCharges"/> a gun
/// [SRC: Brown, about 8 between the magazine and the guns of a twin turret in action, ~2 a station]): its men flash
/// (16 §4.6: a turret's train, a casemate's as a battery's) and the charges start a propellant seat (fire.md B1).</item>
/// </list>
/// Pure and allocation-free; the draws come from the stream it's given.</summary>
public static class Magazines
{
    /// <summary>Charges lit at a station of a mount's train: they burn (unless <paramref name="burning"/>, a fire that's
    /// already theirs), the flame runs on past the open doors; returns the magazine room it reached, or -1.</summary>
    public static int Light(ShipClass c, ShipCondition k, int mount, int station, bool burning, SimSettings s, ref RandomStream r,
        ref EffectsResult res)
    {
        var train = c.Train(mount);
        if (!burning)
            Burn(c, k, mount, train[station], s, ref res);
        int lo = station, hi = station;
        while (hi + 1 < train.Length && r.NextDouble() < s.StationDoorOpen[(int)train[hi].Kind])
            Burn(c, k, mount, train[++hi], s, ref res);
        while (lo > 0 && r.NextDouble() < s.StationDoorOpen[(int)train[lo - 1].Kind])
            Burn(c, k, mount, train[--lo], s, ref res);
        res.Stations += hi - lo + 1;
        res.Flash = true;
        return train[hi].Kind == StationKind.Magazine ? train[hi].Room : -1;
    }

    /// <summary>A station's charges burning: its men's flash and a propellant seat. A magazine reached is its caller's.</summary>
    static void Burn(ShipClass c, ShipCondition k, int mount, in StationRow st, SimSettings s, ref EffectsResult res)
    {
        if (st.Kind == StationKind.Magazine)
            return;
        ref readonly var mr = ref c.Mounts[mount];
        double mj = s.StationCharges * c.TurretTypes[mr.Type].Barrels * c.Charges[mr.Type].MassKg * s.FirePropellantMJkg;
        bool turret = st.Kind != StationKind.Casemate;
        if (st.Part >= 0)
        {
            Casualties.Flash(k, Casualties.Part(st.Part), turret, s);
            res.Fire |= Fires.Ignite(c, k, c.FireOfPart(st.Part), s.ReadyAmmoSeatMW, s, mj);
            return;
        }
        var cells = c.Links(c.Rooms[st.Room].Cells);
        foreach (int cell in cells)
            Casualties.Flash(k, Casualties.Cell(c, cell), turret, s);
        if (cells.Length > 0)
            res.Fire |= Fires.Ignite(c, k, cells[0], s.ReadyAmmoSeatMW, s, mj);   // a cell's fire space is its own index
    }
}
