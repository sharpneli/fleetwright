namespace Fleetwright.Sim.Ships;

/// <summary>The mounts' ammunition trains (docs/battle/magazines.md V0): for each gun mount with a magazine, the stations its
/// charges pass, top first.
/// <list type="bullet">
/// <item>An enclosed mount (a gunhouse, <see cref="ShipClass.Enclosed"/>): its gunhouse, the working chamber in its
/// barbette (when it has one), its handling room, its magazine.</item>
/// <item>A casemate: the casemate, then its handling room if it has one, then the magazine (the battery's own space stands
/// for the passages until shipgen emits them).</item>
/// <item>A shield or an open mount: no station on deck (its hoist ends in a scuttle and the gun vents its flash to the
/// air: decision 4); its handling room if it has one, then the magazine.</item>
/// </list></summary>
static class Trains
{
    internal static void Build(ShipClass c)
    {
        var rows = new List<StationRow>();
        c.trains = new Slice[c.mounts.Length];
        for (int m = 0; m < c.mounts.Length; m++)
        {
            ref readonly var mr = ref c.mounts[m];
            int start = rows.Count;
            if (mr.Battery >= 0 && mr.Magazine >= 0)
            {
                if (c.Enclosed(m))
                {
                    rows.Add(new StationRow(StationKind.Gunhouse, mr.Component, -1));
                    if (mr.Barbette >= 0)
                        rows.Add(new StationRow(StationKind.Chamber, mr.Barbette, -1));
                }
                else if (mr.Casemate)
                    rows.Add(new StationRow(StationKind.Casemate, mr.Component, -1));
                for (int room = 0; room < c.rooms.Length; room++)
                    if (c.rooms[room].Kind == Shipgen.RoomKind.Handling && c.rooms[room].Mount == mr.Component)
                    {
                        rows.Add(new StationRow(StationKind.Handling, -1, room));
                        break;
                    }
                rows.Add(new StationRow(StationKind.Magazine, -1, mr.Magazine));
            }
            c.trains[m] = new Slice(start, rows.Count - start);
        }
        c.stations = [.. rows];
    }
}
