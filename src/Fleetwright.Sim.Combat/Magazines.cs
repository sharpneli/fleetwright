using Fleetwright.Shipgen;
using Fleetwright.Sim.Core;
using Fleetwright.Sim.Ships;

namespace Fleetwright.Sim.Combat;

/// <summary>How the men are getting to a magazine's valves (research 11).</summary>
public enum ValveStage : byte
{
    /// <summary>On their way, or not ordered yet.</summary>
    Walking,
    /// <summary>Getting through the smoke over it (Seydlitz).</summary>
    Through,
    /// <summary>Driven back by the smoke, until it clears (Liberté).</summary>
    DrivenBack,
}

/// <summary>What a step of the valves did: the water let in, m³, and what met the men there: valves opened, smoke got
/// through, driven back by it.</summary>
public struct ValveStep
{
    public double AddedM3;
    public int Opened, Through, DrivenBack;
}

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

    /// <summary>A flash discipline read between the rows (<see cref="SimSettings.FlashDisciplines"/>, safest first): at a
    /// share of the way from the first (0) to the last (1) (a side's <see cref="SimSettings.Stupidity"/>).</summary>
    public static FlashDiscipline At(SimSettings s, double share)
    {
        var rows = s.FlashDisciplines;
        double at = Math.Clamp(share, 0, 1) * (rows.Length - 1);
        int i = Math.Min((int)at, rows.Length - 2);
        double t = at - i;
        ref readonly var a = ref rows[i];
        ref readonly var b = ref rows[i + 1];
        double Mix(double x, double y) => x + (y - x) * t;
        var doors = new double[a.DoorOpen.Length];
        for (int d = 0; d < doors.Length; d++)
            doors[d] = Mix(a.DoorOpen[d], b.DoorOpen[d]);
        return new FlashDiscipline
        {
            Name = "stupidity", DoorOpen = doors, StationCharges = Mix(a.StationCharges, b.StationCharges), LidsOff = Mix(a.LidsOff, b.LidsOff),
            HoistShare = Mix(a.HoistShare, b.HoistShare), StackShare = Mix(a.StackShare, b.StackShare),
        };
    }

    /// <summary>The rounds a discipline stacks at a station of a mount's train, out of the magazine: its share of her rounds,
    /// shared over the places in her train there's room for them (the working chamber, the handling room, a passage; research
    /// 10 §3.2: the BCF's "direct train of cordite", Bulwark's passages).</summary>
    public static double Stacked(ShipClass c, int mount, StationKind at, in FlashDiscipline flash)
    {
        if (!(flash.StackShare > 0) || at is not (StationKind.Chamber or StationKind.Handling or StationKind.Passage))
            return 0;
        int places = 0;
        foreach (ref readonly var st in c.Train(mount))
            if (st.Kind is StationKind.Chamber or StationKind.Handling or StationKind.Passage)
                places++;
        return flash.StackShare * Math.Max(c.Mounts[mount].Rounds, 0) / places;
    }

    /// <summary>How likely a burst, its spall or its gas at a station of a mount's train lights the charges lying there
    /// (docs/battle/magazines.md V8): each lights with <see cref="SimSettings.StationLightChance"/> by the station's kind,
    /// and there are the discipline's charges a gun times the mount's guns and any stack there, so 1 - (1 - q)^n: a twin on
    /// drill 0.75, on safe 0.5, on rate first 0.94 before its stack.</summary>
    public static double LightChance(ShipClass c, ShipCondition k, int mount, int station, SimSettings s)
    {
        var kind = c.Train(mount)[station].Kind;
        if (kind == StationKind.Magazine)
            return s.MagazineFlashChance;
        var flash = Discipline(k, s);
        double n = flash.StationCharges * c.TurretTypes[c.Mounts[mount].Type].Barrels + Stacked(c, mount, kind, flash);
        return 1 - Math.Pow(1 - s.StationLightChance[(int)kind], n);
    }

    /// <summary>Charges lit at a station of a mount's train: they burn (unless <paramref name="burning"/>, a fire that's
    /// already theirs), the flame runs on past the open doors; returns the magazine room it reached, or -1. A flash in
    /// the train that didn't reach it orders its magazine flooded (Harvey's; V6), the order and the walk to the valves
    /// about <see cref="SimSettings.MagazineOrderS"/> on, unless her doctrine never floods one. It doesn't hang on the
    /// train's men: the gunner, her damage control or the bridge order it too (research 11 §3.4). What can stop it is at
    /// the valves (<see cref="Valves"/>).</summary>
    public static int Light(ShipClass c, ShipCondition k, int mount, int station, bool burning, SimSettings s, ref RandomStream r,
        ref EffectsResult res, long now = 0)
    {
        var train = c.Train(mount);
        var flash = Discipline(k, s);
        if (!burning)
            Burn(c, k, mount, train[station], flash, s, ref res);
        int lo = station, hi = station;
        while (hi + 1 < train.Length && r.NextDouble() < DoorOpen(flash, train[hi].Kind, train[hi + 1].Kind))
            Burn(c, k, mount, train[++hi], flash, s, ref res);
        while (lo > 0 && r.NextDouble() < DoorOpen(flash, train[lo - 1].Kind, train[lo].Kind))
            Burn(c, k, mount, train[--lo], flash, s, ref res);
        res.Stations += hi - lo + 1;
        res.Flash = true;
        if (train[hi].Kind == StationKind.Magazine)
            return train[hi].Room;
        if (k.DamageControl.FloodMagazineS >= 0 || k.DamageControl.Name is null)
        {
            double walk = Math.Clamp(s.MagazineOrderS * Math.Exp(s.MagazineOrderSigma * r.NextGaussian()), s.MagazineOrderRangeS[0], s.MagazineOrderRangeS[1]);
            Order(k, c.Mounts[mount].Magazine, now + (long)(walk * 1000));
        }
        return -1;
    }

    /// <summary>How often the door below a station stands open to the flame. The door into a magazine is the magazine's own,
    /// whatever's above it: a wing turret sharing a magazine has no handling room, and her working chamber's flash doors
    /// aren't what stands between the flame and her charges (a casemate's hoist and passage have theirs in series).</summary>
    static double DoorOpen(in FlashDiscipline flash, StationKind above, StationKind below)
    {
        if (below != StationKind.Magazine)
            return flash.DoorOpen[(int)above];
        double own = flash.DoorOpen[(int)StationKind.Handling];
        return above == StationKind.Casemate ? flash.DoorOpen[(int)StationKind.Casemate] * own : own;   // her hoist, then the door
    }

    /// <summary>The share of a burst in a space that finds the charges of a station there: all of a handling room's; of a
    /// space a passage runs through, the passage's width in its (Ordnance.PassageHalfWidthM). Other stations: 1.</summary>
    public static double Exposed(ShipClass c, int room)
    {
        if (room < 0 || c.Rooms[room].Kind != RoomKind.Passage)
            return 1;
        double width = 0;
        var cells = c.Links(c.Rooms[room].Cells);
        foreach (int cell in cells)
            width += c.Cells[cell].Box.Y1 - c.Cells[cell].Box.Y0;
        return cells.Length == 0 ? 0 : Math.Min(1, 2 * Fleetwright.Shipgen.Ordnance.PassageHalfWidthM * cells.Length / width);
    }

    /// <summary>A magazine's flooding ordered, its valves to open at <paramref name="atMs"/> (the earlier order stands).</summary>
    public static void Order(ShipCondition k, int room, long atMs)
    {
        if (room >= 0 && !k.MagazineFlood[room] && k.MagazineValve[room] == ValveStage.Walking &&
            (k.MagazineOrderMs[room] == 0 || atMs < k.MagazineOrderMs[room]))
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

    /// <summary>The magazines' valves (V6; research 10 §4, 11; one set of valves with damage control's counterflooding, DC4).
    /// The men reach an ordered magazine's valves at the order's time (by hand, <see cref="SimSettings.CounterfloodHandS"/>
    /// later, when she isn't conned). What fails is there, not the order (research 11 §3.4): with the space over the magazine
    /// in smoke (<see cref="Fires.ValvesCut"/>; Liberté, Glatton) they get through with
    /// <see cref="SimSettings.MagazineThroughChance"/>, in <see cref="SimSettings.MagazineThroughS"/> (Seydlitz), or are
    /// driven back until it clears. Open, it fills against the falling head (<see cref="Fill"/>). Her sprinklers, where her
    /// outfit has them, wet the ordered magazine's stow in <see cref="SimSettings.MagazineSprinkleS"/> while its section's
    /// main has water. Its water is let in on purpose, as damage control's tanks are
    /// (<see cref="ShipCondition.Counterflooded"/>): the pumps leave it.</summary>
    public static ValveStep Valves(ShipClass c, FloodModel f, ShipCondition k, double dt, long now, SimSettings s, ref RandomStream r)
    {
        var step = default(ValveStep);
        for (int m = 0; m < c.Rooms.Length; m++)
        {
            if (k.MagazineOrderMs[m] == 0 && !k.MagazineFlood[m])
                continue;
            long arrive = k.MagazineOrderMs[m] + (k.Conned ? 0 : (long)(s.CounterfloodHandS * 1000));
            if (!k.MagazineFlood[m] && now >= arrive)
                Reach(c, k, m, now, s, ref r, ref step);
            var cells = c.Links(c.Rooms[m].Cells);
            if (c.MagazineSprinklers && cells.Length > 0 && k.MagazineWet[m] < s.MagazineSprinkleShare &&
                Fires.SectionWater(c, k, c.FireSpaces[cells[0]].Section, now, s) > 0)
                k.MagazineWet[m] = Math.Min(s.MagazineSprinkleShare, k.MagazineWet[m] + dt / s.MagazineSprinkleS);
            if (k.MagazineFlood[m])
                step.AddedM3 += Fill(c, f, k, cells, dt, now, s);
        }
        return step;
    }

    /// <summary>The men at a magazine's valves, by how they came.</summary>
    static void Reach(ShipClass c, ShipCondition k, int m, long now, SimSettings s, ref RandomStream r, ref ValveStep step)
    {
        bool cut = Fires.ValvesCut(c, k, m);
        switch (k.MagazineValve[m])
        {
            case ValveStage.Through:   // through the smoke at last: whatever it's doing now, they're there
                k.MagazineFlood[m] = true;
                step.Opened++;
                break;
            case ValveStage.DrivenBack:   // back when it clears
                if (!cut)
                    (k.MagazineValve[m], k.MagazineOrderMs[m]) = (ValveStage.Walking, now + (long)(s.MagazineOrderS * 1000));
                break;
            default:
                if (!cut)
                {
                    k.MagazineFlood[m] = true;
                    step.Opened++;
                }
                else if (r.NextDouble() < s.MagazineThroughChance)
                {
                    double through = s.MagazineThroughS[0] + r.NextDouble() * (s.MagazineThroughS[1] - s.MagazineThroughS[0]);
                    (k.MagazineValve[m], k.MagazineOrderMs[m]) = (ValveStage.Through, now + (long)(through * 1000));
                    step.Through++;
                }
                else
                {
                    k.MagazineValve[m] = ValveStage.DrivenBack;
                    step.DrivenBack++;
                }
                break;
        }
    }

    /// <summary>A magazine's water: one flood, sized to fill it in <see cref="SimSettings.FireMagazineFloodS"/> at her own
    /// waterline (the USN's 1917 rule; research 11 §2), through a falling head: the flow as the root of the depth left under
    /// the sea, normalised so the whole fill takes that time (full about 2.2 times half). What stands over the waterline, and
    /// a magazine all above it, fills from its section's main at the rule's rate. By hand, at
    /// <see cref="SimSettings.CounterfloodHandShare"/> of that. Shared over its cells by the room left in them. The water let
    /// in, m³.</summary>
    static double Fill(ShipClass c, FloodModel f, ShipCondition k, ReadOnlySpan<int> cells, double dt, long now, SimSettings s)
    {
        double held = 0, cap = 0, floor = double.MaxValue, top = double.MinValue;
        foreach (int cell in cells)
        {
            (held, cap) = (held + k.WaterM3[cell], cap + f.Capacity[cell]);
            (floor, top) = (Math.Min(floor, c.Cells[cell].Box.Base), Math.Max(top, c.Cells[cell].Box.Top));
        }
        double room = cap - held;
        if (room <= 0)
            return 0;
        double depth = c.Vertical.Waterline - floor, height = top - floor;
        double rate = cap * dt / s.FireMagazineFloodS * (k.Conned ? 1 : s.CounterfloodHandShare);
        double risen = depth > 0 ? held / cap * height / depth : 1;   // how far up the depth under the sea the water stands
        bool sea = risen < 1;
        if (sea)
        {
            double a = Math.Min(height / depth, 1);
            rate *= 2 * (1 - Math.Sqrt(1 - a)) / a * Math.Sqrt(1 - risen);
        }
        double added = 0;
        foreach (int cell in cells)
        {
            double left = f.Capacity[cell] - k.WaterM3[cell];
            k.Counterflooded[cell] = true;   // let in on purpose, as damage control's tanks: the pumps leave it
            if (left <= 0)
                continue;
            double w = Math.Min(left, rate * left / room * (sea ? 1 : Fires.SectionWater(c, k, c.FireSpaces[cell].Section, now, s)));
            (k.WaterM3[cell], added) = (k.WaterM3[cell] + w, added + w);
        }
        return added;
    }

    /// <summary>A station's charges burning: its men's flash and a propellant seat. A magazine reached is its caller's.</summary>
    static void Burn(ShipClass c, ShipCondition k, int mount, in StationRow st, in FlashDiscipline flash, SimSettings s, ref EffectsResult res)
    {
        if (st.Kind == StationKind.Magazine)
            return;
        ref readonly var mr = ref c.Mounts[mount];
        double mj = (flash.StationCharges * c.TurretTypes[mr.Type].Barrels + Stacked(c, mount, st.Kind, flash)) * c.Charges[mr.Type].MassKg * s.FirePropellantMJkg;
        bool turret = st.Kind != StationKind.Casemate;
        if (st.Part >= 0)
        {
            Casualties.Flash(k, Casualties.Part(st.Part), turret, s);
            res.Fire |= Fires.Ignite(c, k, c.FireOfPart(st.Part), s.ReadyAmmoSeatMW, s, mj);
            return;
        }
        var cells = c.Links(c.Rooms[st.Room].Cells);
        if (st.Kind == StationKind.Passage)
        {
            // it runs through other spaces: the charges burn where they lie, somewhere along it, not all along it [EST]
            if (cells.Length > 0)
            {
                int at = cells[cells.Length / 2];
                Casualties.Flash(k, Casualties.Cell(c, at), false, s);
                res.Fire |= Fires.Ignite(c, k, at, s.ReadyAmmoSeatMW, s, mj);
            }
            return;
        }
        foreach (int cell in cells)
            Casualties.Flash(k, Casualties.Cell(c, cell), turret, s);
        if (cells.Length > 0)
            res.Fire |= Fires.Ignite(c, k, cells[0], s.ReadyAmmoSeatMW, s, mj);   // a cell's fire space is its own index
    }
}
