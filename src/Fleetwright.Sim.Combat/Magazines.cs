using Fleetwright.Sim.Core;
using Fleetwright.Sim.Ships;

namespace Fleetwright.Sim.Combat;

/// <summary>The magazines (docs/battle/magazines.md). V2, the ammunition trains: charges lit at a station of a mount's train
/// (<see cref="ShipClass.Train"/>) burn there, and the flame goes on up and down the train past each door that stands
/// open (research/simulation/damage-research/10-magazine-flash-chain.md §3). A flame that reaches the magazine sets it
/// off, and the venting race (V3, <see cref="SetOff"/>) decides what's left.
/// <list type="bullet">
/// <item><b>The doors:</b> each station's door to the next one down (the gunhouse's floor and cages, the trunk's lower
/// doors, the handling room's scuttle to the magazine, a casemate's hoist and passage) stands open
/// the side's flash discipline's share of the time (<see cref="Discipline"/>), a roll each, the flame's way down or
/// up.</item>
/// <item><b>A station the flame reaches</b> burns the charges lying there (its discipline's charges a gun
/// [SRC: Brown, about 8 between the magazine and the guns of a twin turret in action, ~2 a station]): its men flash
/// (16 §4.6: a turret's train, a casemate's as a battery's) and the charges start a propellant seat (fire.md B1).</item>
/// </list>
/// Pure and allocation-free; the draws come from the stream it's given.</summary>
public static partial class Magazines
{
    /// <summary>A ship's flash discipline (V5): her side's doctrine's, once her side's orders are read; before, the
    /// default doctrine's (a bench's ship, a test's).</summary>
    public static FlashDiscipline Discipline(ShipCondition k, SimSettings s) => k.Flash.Name != null ? k.Flash : Of(s, s.DamageControlDefault);

    /// <summary>A doctrine's flash discipline, by the doctrine's name (none, or a doctrine without one: drill).</summary>
    public static FlashDiscipline Of(SimSettings s, string? doctrine)
    {
        string flash = "drill";
        foreach (ref readonly var d in s.DamageControl.AsSpan())
            if (d.Name == doctrine && d.Flash != null)
                flash = d.Flash;
        foreach (ref readonly var f in s.FlashDisciplines.AsSpan())
            if (f.Name == flash)
                return f;
        throw new ArgumentException($"no flash discipline \"{flash}\"");
    }

    /// <summary>How likely a burst, its spall or its gas at a station of a mount's train lights the charges lying there
    /// (docs/battle/magazines.md V8): each lights with <see cref="SimSettings.StationLightChance"/> by the station's kind,
    /// and there are the discipline's charges a gun times the mount's guns, so 1 - (1 - q)^n: a twin on drill 0.75, on
    /// safe 0.5, on rate first 0.94.</summary>
    public static double LightChance(ShipClass c, ShipCondition k, int mount, int station, SimSettings s)
    {
        var kind = c.Train(mount)[station].Kind;
        if (kind == StationKind.Magazine)
            return s.MagazineFlashChance;
        double n = Discipline(k, s).StationCharges * c.TurretTypes[c.Mounts[mount].Type].Barrels;
        return 1 - Math.Pow(1 - s.StationLightChance[(int)kind], n);
    }

    /// <summary>Charges lit at a station of a mount's train: they burn (unless <paramref name="burning"/>, a fire that's
    /// already theirs), the flame runs on past the open doors; returns the magazine room it reached, or -1. A flash in
    /// the train that didn't reach it orders its magazine flooded (Harvey's; V6), the order and the walk to the valves
    /// <see cref="SimSettings.MagazineOrderS"/> on, unless her doctrine never floods one.</summary>
    public static int Light(ShipClass c, ShipCondition k, int mount, int station, bool burning, SimSettings s, ref RandomStream r,
        ref EffectsResult res, long now = 0)
    {
        var train = c.Train(mount);
        var flash = Discipline(k, s);
        if (!burning)
            Burn(c, k, mount, train[station], flash, s, ref res);
        int lo = station, hi = station;
        while (hi + 1 < train.Length && r.NextDouble() < flash.DoorOpen[(int)train[hi].Kind])
            Burn(c, k, mount, train[++hi], flash, s, ref res);
        while (lo > 0 && r.NextDouble() < flash.DoorOpen[(int)train[lo - 1].Kind])
            Burn(c, k, mount, train[--lo], flash, s, ref res);
        res.Stations += hi - lo + 1;
        res.Flash = true;
        if (train[hi].Kind == StationKind.Magazine)
            return train[hi].Room;
        if (k.DamageControl.FloodMagazineS >= 0 || k.DamageControl.Name is null)
            Order(k, c.Mounts[mount].Magazine, now + (long)(s.MagazineOrderS * 1000));
        return -1;
    }

    /// <summary>A magazine's flooding ordered, its valves to open at <paramref name="atMs"/> (the earlier order stands).</summary>
    public static void Order(ShipCondition k, int room, long atMs)
    {
        if (room >= 0 && !k.MagazineFlood[room] && (k.MagazineOrderMs[room] == 0 || atMs < k.MagazineOrderMs[room]))
            k.MagazineOrderMs[room] = Math.Max(atMs, 1);
    }

    /// <summary>Whether any magazine's flooding or sprinkling is under way: her steps go on for it.</summary>
    public static bool Busy(ShipClass c, FloodModel f, ShipCondition k, SimSettings s)
    {
        for (int m = 0; m < c.Rooms.Length; m++)
        {
            if (k.MagazineOrderMs[m] == 0 && !k.MagazineFlood[m])
                continue;
            if (c.MagazineSprinklers && k.MagazineWet[m] < s.MagazineSprinkleShare)
                return true;
            foreach (int cell in c.Links(c.Rooms[m].Cells))
                if (k.WaterM3[cell] < 0.999 * f.Capacity[cell])
                    return true;
        }
        return false;
    }

    /// <summary>The magazines' valves (V6; research 10 §4; one set of valves with damage control's counterflooding, DC4):
    /// an ordered magazine's valves open at the order's time, and it fills (the sea's water under the waterline, else its
    /// section's main) in <see cref="SimSettings.FireMagazineFloodS"/> while she's conned; by hand, at
    /// <see cref="SimSettings.CounterfloodHandShare"/> of that after <see cref="SimSettings.CounterfloodHandS"/>, when
    /// she isn't. Her sprinklers, where her outfit has them, wet the ordered magazine's stow in
    /// <see cref="SimSettings.MagazineSprinkleS"/> while its section's main has water. Its water is let in on purpose, as
    /// damage control's tanks are (<see cref="ShipCondition.Counterflooded"/>): the pumps leave it. The water it let in, m³.</summary>
    public static double Valves(ShipClass c, FloodModel f, ShipCondition k, double dt, long now, SimSettings s)
    {
        double added = 0;
        for (int m = 0; m < c.Rooms.Length; m++)
        {
            if (k.MagazineOrderMs[m] == 0 && !k.MagazineFlood[m])
                continue;
            long open = k.MagazineOrderMs[m] + (k.Conned ? 0 : (long)(s.CounterfloodHandS * 1000));
            if (!k.MagazineFlood[m] && now >= open)
                k.MagazineFlood[m] = true;
            var cells = c.Links(c.Rooms[m].Cells);
            if (c.MagazineSprinklers && cells.Length > 0 && k.MagazineWet[m] < s.MagazineSprinkleShare &&
                Fires.SectionWater(c, k, c.FireSpaces[cells[0]].Section, now, s) > 0)
                k.MagazineWet[m] = Math.Min(s.MagazineSprinkleShare, k.MagazineWet[m] + dt / s.MagazineSprinkleS);
            if (!k.MagazineFlood[m])
                continue;
            double hand = k.Conned ? 1 : s.CounterfloodHandShare;
            foreach (int cell in cells)
            {
                double source = c.Cells[cell].BelowWaterline ? 1 : Fires.SectionWater(c, k, c.FireSpaces[cell].Section, now, s);
                double w = Math.Min(f.Capacity[cell] - k.WaterM3[cell], f.Capacity[cell] * source * hand * dt / s.FireMagazineFloodS);
                if (w > 0)
                    (k.WaterM3[cell], added) = (k.WaterM3[cell] + w, added + w);
                k.Counterflooded[cell] = true;   // let in on purpose, as damage control's tanks: the pumps leave it
            }
        }
        return added;
    }

    /// <summary>A station's charges burning: its men's flash and a propellant seat. A magazine reached is its caller's.</summary>
    static void Burn(ShipClass c, ShipCondition k, int mount, in StationRow st, in FlashDiscipline flash, SimSettings s, ref EffectsResult res)
    {
        if (st.Kind == StationKind.Magazine)
            return;
        ref readonly var mr = ref c.Mounts[mount];
        double mj = flash.StationCharges * c.TurretTypes[mr.Type].Barrels * c.Charges[mr.Type].MassKg * s.FirePropellantMJkg;
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
