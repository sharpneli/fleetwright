using Fleetwright.Sim.Core;
using Fleetwright.Sim.Ships;

namespace Fleetwright.Sim.Combat;

/// <summary>What a fire step did: whether a part or space got worse (what the ship can do must be worked out again),
/// fires started and put out, the crew it cost, and whether it set off a magazine.</summary>
public struct FireStep
{
    public bool Changed, Magazine;
    /// <summary>The magazine set off (a room of the class), when <see cref="Magazine"/>; else -1.</summary>
    public int MagazineRoom;
    public int Spread, Out;
    public double CrewLost;
}

/// <summary>FIRE v0 (docs/battle/fighting.md 4.3b; damage-model-research.md §8.5): a burning space or part is damaged
/// over time and costs crew; a burning space spreads to a neighbour with a chance a minute (not into one half flooded);
/// the crew puts a fire out with a chance a minute, less as they're lost; water drowns one; a fire in a magazine, or next
/// to one, may cook it off (times the side's propellant). That's all for now: spread by vents, fire load by space and
/// era, and cook-off timers are fighting.md 4.5's. Pure and allocation-free; the draws come from the stream it's
/// given.</summary>
public static class Fires
{
    public static FireStep Step(ShipClass c, FloodModel f, ShipCondition k, double dt, SimSettings s, double propellant, ref RandomStream r)
    {
        var res = new FireStep { MagazineRoom = -1 };
        double crew = Math.Clamp(1 - k.CrewLost / f.Complement, 0, 1);
        double harm = Chance(s.FireHarmPerMin, dt), spread = Chance(s.FireSpreadPerMin, dt), put = Chance(s.FireOutPerMin * crew, dt);
        double magIn = Chance(s.FireMagazineInPerMin * propellant, dt), magNext = Chance(s.FireMagazineNextPerMin * propellant, dt);
        var cells = c.Cells;
        for (int i = 0; i < cells.Length; i++)
        {
            if (!k.CellFire[i])
                continue;
            if (Drowned(f, k, i, s) || r.NextDouble() < put)
            {
                k.CellFire[i] = false;
                res.Out++;
                continue;
            }
            res.CrewLost += s.FireCrewPerMin * dt / 60;
            if (r.NextDouble() < harm)
                foreach (int room in c.RoomsIn(i))
                    res.Changed |= k.Harm(c.RoomNode(room));
            if (!res.Magazine && Magazine(c, f, k, i, s) is int mi and >= 0 && r.NextDouble() < magIn)
                (res.Magazine, res.MagazineRoom) = (true, mi);
            foreach (var n in c.Neighbours(cells[i].Neighbours))
            {
                if (!res.Magazine && Magazine(c, f, k, n.Cell, s) is int mn and >= 0 && r.NextDouble() < magNext)
                    (res.Magazine, res.MagazineRoom) = (true, mn);
                if (!k.CellFire[n.Cell] && !Drowned(f, k, n.Cell, s) && r.NextDouble() < spread)
                {
                    k.CellFire[n.Cell] = true;
                    res.Spread++;
                }
            }
        }
        for (int i = 0; i < k.Flags.Length; i++)
        {
            if ((k.Flags[i] & PartFlags.Burning) == 0)
                continue;
            if (r.NextDouble() < put)
            {
                k.Flags[i] &= ~PartFlags.Burning;
                res.Out++;
                continue;
            }
            res.CrewLost += s.FireCrewPerMin * dt / 60;
            if (r.NextDouble() < harm)
                res.Changed |= k.Harm(i);
        }
        k.CrewLost += res.CrewLost;
        return res;
    }

    /// <summary>The chance of something that happens with a chance a minute, over dt seconds.</summary>
    static double Chance(double perMin, double dt) => perMin <= 0 ? 0 : 1 - Math.Pow(1 - Math.Min(perMin, 0.999999), dt / 60);

    static bool Drowned(FloodModel f, ShipCondition k, int cell, SimSettings s) => k.WaterM3[cell] >= s.FireDrownShare * f.Capacity[cell];

    /// <summary>The magazine (a room) a cell holds, dry enough to go up; else -1.</summary>
    static int Magazine(ShipClass c, FloodModel f, ShipCondition k, int cell, SimSettings s)
    {
        if (Drowned(f, k, cell, s))
            return -1;
        foreach (int room in c.RoomsIn(cell))
            if (c.Rooms[room].Kind == RoomKind.Magazine)
                return room;
        return -1;
    }
}
