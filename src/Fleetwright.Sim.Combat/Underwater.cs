using Fleetwright.Shipgen;
using Fleetwright.Sim.Core;
using Fleetwright.Sim.Ships;

namespace Fleetwright.Sim.Combat;

/// <summary>A torpedo's hit (docs/battle/fighting.md 4.6; a mine's later): its warhead going off at her plate under the sea, a burst
/// outside her on the one path every explosion takes (<see cref="Effects.Outside"/>; docs/battle/explosions.md EX5, "One outside"). What's
/// a torpedo's own is only whether it goes off (a dud by the torpedo's own chance: nothing but the blow) and whether a protection held:
/// <see cref="Outcome.Held"/> when its near field opened no way from a space outside a holding bulkhead into one inside it, read from
/// what opened, not decided before. Pure and allocation-free; the draws come from the stream it's given.</summary>
public static class Underwater
{
    public static EffectsResult Apply(ShipClass c, ShipCondition k, in Hit hit, SimSettings s, ref RandomStream r, out Outcome outcome)
    {
        var p = hit.Point;
        var cells = c.Cells;
        int first = -1;
        double nearest = double.MaxValue;
        for (int i = 0; i < cells.Length; i++)
        {
            double d = Effects.BoxDistance(cells[i].Box, p);
            if (d < nearest)
                (first, nearest) = (i, d);
        }
        int told = first >= 0 && cells[first].Room >= 0 ? c.RoomNode(cells[first].Room) : -1;
        if (first < 0 || r.NextDouble() < hit.Torpedo.DudShare)   // the torpedo's own (it was the struck ship's era's until 2026-10-19)
        {
            outcome = Outcome.Dud;
            return new EffectsResult { Part = told, MagazineRoom = -1 };
        }
        var res = Effects.Outside(c, k, hit.Torpedo.Warhead, p, hit.Direction, hit.SpeedMs, 1, hit.Ms, s, ref r);
        res.Part = told >= 0 ? told : res.Part;
        var net = GasNetwork.Of(k, s, hit.Ms);
        outcome = Outboard(c, first) && !Inside(c, net) ? Outcome.Held : Outcome.Exploded;
        return res;
    }

    /// <summary>Whether the explosion opened a way from a space outside a holding bulkhead into one inside it.</summary>
    static bool Inside(ShipClass c, GasNetwork net)
    {
        foreach (int i in net.Walked())
        {
            if (!Outboard(c, i))
                continue;
            var nb = c.Cells[i].Neighbours;
            for (int j = nb.Start; j < nb.End; j++)
                if (!Outboard(c, c.Neighbour(j).Cell) && net.Opened(j))
                    return true;
        }
        return false;
    }

    /// <summary>Whether a cell lies outside a holding bulkhead: a TDS cell, or the double bottom.</summary>
    static bool Outboard(ShipClass c, int cell)
    {
        if (c.Cells[cell].TdsM > 0)
            return true;
        foreach (int room in c.RoomsIn(cell))
            if (c.Rooms[room].Kind is RoomKind.Tds or RoomKind.DoubleBottom)
                return true;
        return false;
    }
}
