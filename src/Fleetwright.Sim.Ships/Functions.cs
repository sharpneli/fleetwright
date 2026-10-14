namespace Fleetwright.Sim.Ships;

/// <summary>What a function of the ship is (docs/battle/fighting.md 4.3a, FUNCTIONS): what the damage model asks of its
/// parts. Index says which: a mount, a director, an eye, a shaft line, a boiler room (a room), a rudder, a submerged
/// tube; the command function has none.</summary>
public enum FunctionKind : byte
{
    /// <summary>A mount fires (Index: Mounts).</summary>
    Mount,
    /// <summary>A director controls its battery's fire (Index: Directors).</summary>
    Director,
    /// <summary>An observation post sees (Index: Eyes).</summary>
    Eye,
    /// <summary>A shaft line turns its propeller (Index: ShaftLines).</summary>
    ShaftLine,
    /// <summary>A boiler room raises steam (Index: Rooms).</summary>
    Boilers,
    /// <summary>A rudder answers the helm (Index: Rudders).</summary>
    Rudder,
    /// <summary>The ship is conned: a bridge or the conning tower (recorded now; what its loss costs comes with the
    /// signals, docs/battle/plan.md 5.1).</summary>
    Command,
    /// <summary>A submerged tube fires (Index: Tubes): it and its torpedo room work (4.6).</summary>
    Tube,
}

/// <summary>A function's needs (docs/research/simulation/damage-research/06-service-networks.md §4, Model A): every node
/// in All, and at least one in Any (none: no such need). A node is a part of the ship that damage reaches: a component
/// (its index) or a room (<see cref="ShipClass.RoomNode"/>). Data, evaluated by one generic pass, so the service
/// networks (fighting.md 4.5) come in as more rows and more kinds of node, and nothing that reads a function
/// changes.</summary>
public readonly record struct FunctionRow(FunctionKind Kind, int Index, Slice All, Slice Any);

/// <summary>Builds a class's function table from its own rows and links (the links damage needs: mounts to their
/// barbettes and magazines, funnels to their boiler rooms, shaft lines to their engine rooms, rudders to the steering
/// room, eyes and directors to their parts).</summary>
static class FunctionTable
{
    internal static void Build(ShipClass c)
    {
        var rows = new List<FunctionRow>();
        var nodes = new List<int>();
        int Room(int r) => c.RoomNode(r);

        Slice Add(ReadOnlySpan<int> list)
        {
            int start = nodes.Count;
            foreach (int n in list)
                if (n >= 0)
                    nodes.Add(n);
            return new Slice(start, nodes.Count - start);
        }

        for (int m = 0; m < c.mounts.Length; m++)
        {
            ref readonly var row = ref c.mounts[m];
            rows.Add(new FunctionRow(FunctionKind.Mount, m,
                Add([row.Component, row.Barbette, row.Magazine >= 0 ? Room(row.Magazine) : -1]), Slice.Empty));
        }
        for (int d = 0; d < c.directors.Length; d++)
            rows.Add(new FunctionRow(FunctionKind.Director, d, Add([c.directors[d].Component]), Slice.Empty));
        for (int e = 0; e < c.eyes.Length; e++)
            rows.Add(new FunctionRow(FunctionKind.Eye, e, Add([c.eyes[e].Component]), Slice.Empty));
        for (int s = 0; s < c.shaftLines.Length; s++)
        {
            ref readonly var row = ref c.shaftLines[s];
            rows.Add(new FunctionRow(FunctionKind.ShaftLine, s, Add([row.EngineRoom >= 0 ? Room(row.EngineRoom) : -1, row.Shaft, row.Propeller]),
                Slice.Empty));
        }
        // a boiler room raises steam while it works and one of the funnels it draws through stands
        var funnelsOf = new List<int>();
        for (int r = 0; r < c.rooms.Length; r++)
        {
            if (c.rooms[r].Kind != RoomKind.BoilerRoom)
                continue;
            funnelsOf.Clear();
            foreach (var f in c.funnels)
                if (c.Links(f.BoilerRooms).Contains(r))
                    funnelsOf.Add(f.Component);
            rows.Add(new FunctionRow(FunctionKind.Boilers, r, Add([Room(r)]), Add(funnelsOf.ToArray())));
        }
        for (int r = 0; r < c.rudders.Length; r++)
        {
            ref readonly var row = ref c.rudders[r];
            rows.Add(new FunctionRow(FunctionKind.Rudder, r, Add([row.Component, row.Steering >= 0 ? Room(row.Steering) : -1]), Slice.Empty));
        }
        for (int t = 0; t < c.tubes.Length; t++)
        {
            ref readonly var row = ref c.tubes[t];
            rows.Add(new FunctionRow(FunctionKind.Tube, t, Add([row.Component, row.Room >= 0 ? Room(row.Room) : -1]), Slice.Empty));
        }
        var conning = new List<int>();
        for (int i = 0; i < c.components.Length; i++)
            if (c.components[i].Kind == ComponentKind.ConningTower || c.components[i].Role == ComponentRole.Bridge)
                conning.Add(i);
        if (conning.Count > 0)
            rows.Add(new FunctionRow(FunctionKind.Command, -1, Slice.Empty, Add(conning.ToArray())));

        c.functions = [.. rows];
        c.functionNodes = [.. nodes];

        // a component's mount, and a barbette's
        c.mountOfComponent = new int[c.components.Length];
        Array.Fill(c.mountOfComponent, -1);
        for (int m = 0; m < c.mounts.Length; m++)
        {
            c.mountOfComponent[c.mounts[m].Component] = m;
            if (c.mounts[m].Barbette >= 0)
                c.mountOfComponent[c.mounts[m].Barbette] = m;
        }
    }
}
