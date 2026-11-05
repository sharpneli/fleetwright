namespace Fleetwright.Sim.Ships;

/// <summary>The mounts' ammunition trains (docs/battle/magazines.md V0): for each gun mount with a magazine, the stations its
/// charges pass, top first.
/// <list type="bullet">
/// <item>An enclosed mount (a gunhouse, <see cref="ShipClass.Enclosed"/>): its gunhouse, the working chamber in its
/// barbette (when it has one), its handling room, its magazine.</item>
/// <item>A casemate: the casemate, then its handling room if it has one, then the magazine.</item>
/// <item>A gun far from its magazine has an ammunition passage before the magazine, whatever houses it
/// (docs/battle/magazines.md "Ammunition passages").</item>
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
                for (int room = 0; room < c.rooms.Length; room++)
                    if (c.rooms[room].Kind == Shipgen.RoomKind.Passage && c.Links(c.rooms[room].Mounts).IndexOf(mr.Component) >= 0)
                    {
                        rows.Add(new StationRow(StationKind.Passage, -1, room));
                        break;
                    }
                rows.Add(new StationRow(StationKind.Magazine, -1, mr.Magazine));
            }
            c.trains[m] = new Slice(start, rows.Count - start);
        }
        c.stations = [.. rows];

        // the magazines a passage joins (research 07 §1, §2.4: Defence): one whose mounts it serves, one it runs over, and
        // those of any passage it meets
        var spans = new List<(double X0, double X1, HashSet<int> Mags)>();
        for (int room = 0; room < c.rooms.Length; room++)
        {
            ref readonly var p = ref c.rooms[room];
            if (p.Kind != Shipgen.RoomKind.Passage)
                continue;
            var mags = new HashSet<int>();
            foreach (int comp in c.Links(p.Mounts))
                if (c.MountOf(comp) is int m and >= 0 && c.mounts[m].Magazine >= 0)
                    mags.Add(c.mounts[m].Magazine);
            for (int mag = 0; mag < c.rooms.Length; mag++)
                if (c.rooms[mag].Kind == Shipgen.RoomKind.Magazine && Math.Min(p.X1, c.rooms[mag].X1) > Math.Max(p.X0, c.rooms[mag].X0))
                    mags.Add(mag);
            spans.Add((p.X0, p.X1, mags));
        }
        for (bool merged = true; merged;)
        {
            merged = false;
            for (int a = 0; a < spans.Count && !merged; a++)
                for (int b = a + 1; b < spans.Count && !merged; b++)
                    if (Math.Min(spans[a].X1, spans[b].X1) >= Math.Max(spans[a].X0, spans[b].X0))
                    {
                        spans[a].Mags.UnionWith(spans[b].Mags);
                        spans[a] = (Math.Min(spans[a].X0, spans[b].X0), Math.Max(spans[a].X1, spans[b].X1), spans[a].Mags);
                        spans.RemoveAt(b);
                        merged = true;
                    }
        }
        c.passageGroup = new int[c.rooms.Length];
        Array.Fill(c.passageGroup, -1);
        for (int g = 0; g < spans.Count; g++)
            foreach (int mag in spans[g].Mags)
                c.passageGroup[mag] = g;

        // what holds an enclosed mount's gunhouse down: its revolving weight over its barbette's bore (research 10 §2.2)
        c.liftKPa = new double[c.mounts.Length];
        c.boreM2 = new double[c.mounts.Length];
        for (int m = 0; m < c.mounts.Length; m++)
        {
            ref readonly var mr = ref c.mounts[m];
            if (mr.Barbette < 0 || !c.Enclosed(m))
                continue;
            foreach (ref readonly var b in c.barbettes.AsSpan())
                if (b.Component == mr.Barbette && b.R > 0)
                {
                    c.boreM2[m] = Math.PI * b.R * b.R;
                    c.liftKPa[m] = mr.RevolvingT * 1000 * 9.81 / c.boreM2[m] / 1000;
                }
        }
    }
}
