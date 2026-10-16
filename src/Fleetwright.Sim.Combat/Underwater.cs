using Fleetwright.Sim.Core;
using Fleetwright.Sim.Ships;

namespace Fleetwright.Sim.Combat;

/// <summary>An underwater explosion against the hull (docs/battle/fighting.md 4.6: a torpedo's hit; a mine's later), its
/// terminal and effects in one, v0 (damage-model-research.md §3.4, 05-mechanics-and-games.md A6):
/// <list type="bullet">
/// <item><b>a dud</b> by the era's chance: nothing but the blow;</item>
/// <item><b>the reach and the hole:</b> the charge w (TNT's worth) wrecks <see cref="SimSettings.TorpedoReachK"/>·w^⅓
/// inboard and tears <see cref="SimSettings.TorpedoHoleK"/>·w^⅔ m² of the side; the cell behind the hull takes that hole,
/// and every other cell within the reach one of its own, smaller by its distance: the cells there flood;</item>
/// <item><b>the torpedo protection:</b> when the struck cell is a TDS cell at least as deep as the reach, its holding
/// bulkhead holds: only the TDS and the double bottom flood, and nothing inside it is wrecked;</item>
/// <item><b>what's in reach:</b> the spaces within half the reach are put out of action, the rest within it damaged; parts
/// within it (shafts, propellers, rudders, tubes) put out of action; a magazine within it may go (times the side's
/// propellant); this share of the battle crew in the cells it floods is lost;</item>
/// <item><b>the shock:</b> machinery spaces further off, within <see cref="SimSettings.TorpedoShockShare"/> reaches, may
/// trip (damaged);</item>
/// <item><b>the hull girder:</b> a small ship whose depth is little beside the explosion's height may break in two, the
/// more likely amidships (<see cref="SimSettings.TorpedoBreakShare"/>): how one torpedo usually sank a destroyer.</item>
/// </list>
/// Pure and allocation-free; the draws come from the stream it's given.</summary>
public static class Underwater
{
    public static EffectsResult Apply(ShipClass c, ShipCondition k, in Hit hit, SimSettings s, double propellant, ref RandomStream r, out Outcome outcome)
    {
        var res = new EffectsResult { Part = -1, MagazineRoom = -1 };
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
        if (first >= 0 && cells[first].Room >= 0)
            res.Part = c.RoomNode(cells[first].Room);
        int era = Math.Clamp((int)c.Era, 0, s.TorpedoDud.Length - 1);
        if (r.NextDouble() < s.TorpedoDud[era])
        {
            outcome = Outcome.Dud;
            return res;
        }

        double w = Math.Max(hit.Torpedo.ChargeKg, 0), reach = s.TorpedoReachK * Math.Cbrt(w), hole = s.TorpedoHoleK * Math.Pow(w, 2.0 / 3);
        bool held = first >= 0 && cells[first].TdsM >= reach;
        outcome = held ? Outcome.Held : Outcome.Exploded;

        // the cells within the reach flood: the struck one through the hole, the others through holes of their own
        int magazine = -1;
        for (int i = 0; i < cells.Length; i++)
        {
            double d = i == first ? 0 : Effects.BoxDistance(cells[i].Box, p);
            if (d > reach || held && !Outboard(c, i))
                continue;
            double a = i == first ? hole : hole * s.TorpedoInnerHoleShare * (1 - d / reach);
            if (a <= 0)
                continue;
            var b = cells[i].Box;
            k.Hole(i, a, new P3(Math.Clamp(p.X, b.X0, b.X1), Math.Clamp(p.Y, b.Y0, b.Y1), Math.Clamp(p.Z, b.Base, b.Top)));
            res.BreachM2 += a;
            res.CrewLost += cells[i].BattleCrew * s.TorpedoCasualtyShare * (1 - d / reach);
            foreach (int room in c.RoomsIn(i))
            {
                int node = c.RoomNode(room);
                var state = d <= reach / 2 ? PartState.Disabled : PartState.Damaged;
                Effects.Count(ref res, k, node, (state == PartState.Damaged ? k.Harm(node) : k.Worsen(node, state)) ? k.State[node] : PartState.Ok);
                if (magazine < 0 && c.Rooms[room].Kind == RoomKind.Magazine)
                    magazine = room;
            }
        }
        res.Magazine = magazine >= 0 && r.NextDouble() < s.TorpedoMagazineChance * propellant;
        (res.MagazineRoom, res.MagazineVia) = res.Magazine ? (magazine, MagazineVia.Underwater) : (-1, MagazineVia.None);

        // parts within the reach: what's under the stern, the tubes, whatever stands near (not inside a protection that held)
        if (!held)
        {
            var comps = c.Components;
            for (int i = 0; i < comps.Length; i++)
            {
                if (Effects.Distance(c, i, p) > reach)
                    continue;
                var state = Effects.Struck(comps[i].Kind);
                Effects.Count(ref res, k, i, k.Worsen(i, state) ? state : PartState.Ok);
            }
        }

        // the shock: machinery spaces further off may trip
        double shock = s.TorpedoShockShare * reach;
        var rooms = c.Rooms;
        for (int room = 0; room < rooms.Length; room++)
        {
            if (rooms[room].Kind is not (RoomKind.BoilerRoom or RoomKind.EngineRoom))
                continue;
            int node = c.RoomNode(room);
            if (k.State[node] != PartState.Ok || RoomDistance(c, room, p) > shock || r.NextDouble() >= s.TorpedoShockChance)
                continue;
            Effects.Count(ref res, k, node, k.Harm(node) ? k.State[node] : PartState.Ok);
        }

        // the hull girder: cut through when the explosion is high beside the hull's depth, amidships
        if (!held)
        {
            double depth = Math.Max(c.Vertical.Depth, 1), lo = s.TorpedoBreakShare[0], hi = s.TorpedoBreakShare[1];
            double u = (p.X - c.Hydrostatics.Lcf) / Math.Max(c.Length / 2, 1);
            double chance = Math.Clamp((2 * reach / depth - lo) / Math.Max(hi - lo, 1e-9), 0, 1) * Math.Clamp(2 - 2 * Math.Abs(u), 0, 1);
            res.Broken = r.NextDouble() < chance;
        }
        k.CrewLost += res.CrewLost;
        return res;
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

    /// <summary>How far a point is from a room: from the nearest of its cells.</summary>
    static double RoomDistance(ShipClass c, int room, P3 p)
    {
        double d = double.MaxValue;
        foreach (int cell in c.Links(c.Rooms[room].Cells))
            d = Math.Min(d, Effects.BoxDistance(c.Cells[cell].Box, p));
        return d;
    }
}
