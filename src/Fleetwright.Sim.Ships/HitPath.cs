namespace Fleetwright.Sim.Ships;

/// <summary>What a crossing on a <see cref="HitPath"/> is.</summary>
public enum CrossingKind : byte
{
    /// <summary>A plate on its own (Plate says which): armour, a grating, a deck or bulkhead between two cells.</summary>
    Plate,
    /// <summary>Into a part and out of it (Index: the component), through its skin (Plate, possibly None: a barbette's
    /// open top).</summary>
    PartIn,
    PartOut,
    /// <summary>Into the hull and out of it, through its plating (Plate: HullSide, HullDeck or HullEnd).</summary>
    HullIn,
    HullOut,
    /// <summary>Into a watertight cell and out of it (Index: the cell).</summary>
    CellIn,
    CellOut,
    /// <summary>Into the sea and out of it: through the waterplane, or out through the hull's skin under it.</summary>
    WaterIn,
    WaterOut,
}

/// <summary>Which plate a crossing goes through; Index says which row or component it is.</summary>
public enum PlateKind : byte
{
    None,
    /// <summary>The belt or a strake (Index: ShipClass.SidePlates).</summary>
    SideArmour,
    /// <summary>An armour deck (Index: ShipClass.ArmourDecks).</summary>
    ArmourDeck,
    /// <summary>An armour deck's opening over an uptake, closed by an armoured grating as thick as the deck (Index:
    /// ShipClass.ArmourDecks). A placeholder until the designer gives gratings their own thickness.</summary>
    Grating,
    /// <summary>An armoured transverse bulkhead (Index: ShipClass.ArmourBulkheads).</summary>
    ArmourBulkhead,
    /// <summary>An inclined plate, a deck's slope (Index: ShipClass.Inclined).</summary>
    Inclined,
    /// <summary>A barbette's wall (Index: the component).</summary>
    Barbette,
    /// <summary>A turret's face, side, rear or roof (Index: the component).</summary>
    TurretFace,
    TurretSide,
    TurretRear,
    TurretRoof,
    /// <summary>Another armoured part's skin: a conning tower, a casemate, an armoured director (Index: the component).</summary>
    PartArmour,
    /// <summary>A part's plating, unarmoured: a superstructure block, a funnel (Index: the component).</summary>
    PartPlating,
    /// <summary>The hull's shell plating at its side or end, or its deck (Index: -1; Mm from ShipClass.Plating).</summary>
    HullSide,
    HullEnd,
    HullDeck,
    /// <summary>A deck or bulkhead between two cells, its structural plate (Index: ShipClass.Decks or Bulkheads).</summary>
    Deck,
    Bulkhead,
}

/// <summary>One crossing along a segment: where (T, metres from the segment's start, and the point, ship-local), what
/// (Kind, Plate, Index), and the plate's thickness, material and unit normal (outward from the hull or the part,
/// upward for a deck, toward the nearer end or side for a bulkhead; NaN where there's no surface).</summary>
public readonly record struct Crossing
{
    public double T { get; init; }
    public P3 Point { get; init; }
    public CrossingKind Kind { get; init; }
    public PlateKind Plate { get; init; }
    public int Index { get; init; }
    public double Mm { get; init; }
    public int Material { get; init; }
    public P3 Normal { get; init; }
}

/// <summary>What a segment crossed through a ship, in order from its start (docs/battle/fighting.md 4.1): the contract
/// between the hit query and the terminal model. A fixed buffer, reused query after query, so tracing allocates
/// nothing once it's grown to the ship's size; one per thread.</summary>
public sealed class HitPath
{
    Crossing[] items;
    int count;

    // the tracer's scratch: the parts the segment may meet, the stretch of the walk inside each one's box, and whether
    // the walk is inside it
    internal int[] parts = [];
    internal double[] partIn = [], partOut = [];
    internal bool[] partState = [];
    internal double[] partBearing = [];

    public HitPath(int capacity = 512) => items = new Crossing[capacity];

    /// <summary>The segment, ship-local: from Start to End, Length long.</summary>
    public P3 Start { get; private set; }
    public P3 End { get; private set; }
    public double Length { get; private set; }

    public ReadOnlySpan<Crossing> Crossings => items.AsSpan(0, count);
    public int Count => count;
    /// <summary>More crossings than the buffer holds: the last ones were dropped.</summary>
    public bool Overflowed { get; private set; }

    /// <summary>Did the segment meet the ship: its hull or a part?</summary>
    public bool HitShip
    {
        get
        {
            foreach (ref readonly var x in Crossings)
                if (x.Kind is CrossingKind.HullIn or CrossingKind.PartIn or CrossingKind.HullOut or CrossingKind.PartOut)
                    return true;
            return false;
        }
    }

    /// <summary>The path in words, a line a crossing (for tools: it allocates).</summary>
    public string Text(ShipClass c)
    {
        var sb = new System.Text.StringBuilder();
        var inv = System.Globalization.CultureInfo.InvariantCulture;
        sb.AppendLine(string.Format(inv, "  from ({0:0.00}, {1:0.00}, {2:0.00}) to ({3:0.00}, {4:0.00}, {5:0.00}), {6:0.0} m{7}", Start.X, Start.Y, Start.Z,
            End.X, End.Y, End.Z, Length, Overflowed ? " (OVERFLOWED: crossings dropped)" : ""));
        foreach (ref readonly var x in Crossings)
        {
            string what = x.Kind switch
            {
                CrossingKind.PartIn or CrossingKind.PartOut => c.ComponentId(x.Index),
                CrossingKind.CellIn or CrossingKind.CellOut => c.CellId(x.Index),
                _ => "",
            };
            string plate = x.Plate switch
            {
                PlateKind.None => "",
                PlateKind.SideArmour => c.SidePlateId(x.Index),
                PlateKind.ArmourDeck => $"armour deck on {c.DeckId(c.ArmourDecks[x.Index].Deck)}",
                PlateKind.Grating => $"grating in the armour deck on {c.DeckId(c.ArmourDecks[x.Index].Deck)}",
                PlateKind.ArmourBulkhead => c.ArmourBulkheadId(x.Index),
                PlateKind.Inclined => c.InclinedId(x.Index),
                PlateKind.Deck => $"deck {c.DeckId(x.Index)}",
                PlateKind.Bulkhead => $"bulkhead {c.BulkheadId(x.Index)}",
                PlateKind.HullSide => "hull side",
                PlateKind.HullEnd => "hull end",
                PlateKind.HullDeck => "hull deck",
                _ => x.Plate.ToString(),
            };
            if (x.Plate != PlateKind.None)
                plate += string.Format(inv, " {0:0.#} mm{1}", x.Mm, c.Material(x.Material) is { } m ? " " + m : "");
            sb.AppendLine(string.Format(inv, "  {0,7:0.00} m  {1,-8} {2,-28} {3,-44} ({4:0.00}, {5:0.00}, {6:0.00})", x.T, x.Kind, what, plate, x.Point.X,
                x.Point.Y, x.Point.Z));
        }
        return sb.ToString();
    }

    /// <summary>The point at T along the segment.</summary>
    public P3 At(double t)
    {
        double f = Length > 0 ? t / Length : 0;
        return new P3(Start.X + (End.X - Start.X) * f, Start.Y + (End.Y - Start.Y) * f, Start.Z + (End.Z - Start.Z) * f);
    }

    /// <summary>Empty, for the segment start to end (a query that didn't come near the ship).</summary>
    public void Clear(P3 start, P3 end)
    {
        double dx = end.X - start.X, dy = end.Y - start.Y, dz = end.Z - start.Z;
        Begin(start, end, Math.Sqrt(dx * dx + dy * dy + dz * dz));
    }

    internal void Begin(P3 start, P3 end, double length)
    {
        (Start, End, Length, count, Overflowed) = (start, end, length, 0, false);
    }

    internal void Add(in Crossing x)
    {
        if (count == items.Length)
        {
            Overflowed = true;
            return;
        }
        items[count++] = x;
    }

    /// <summary>In order along the segment. The tracer walks it from its end, so the crossings come in nearly backward:
    /// they're reversed, then sorted (stably, so crossings at one point come out in the reverse of the order they
    /// were added in).</summary>
    internal void Sort()
    {
        items.AsSpan(0, count).Reverse();
        for (int i = 1; i < count; i++)
        {
            var x = items[i];
            int j = i - 1;
            while (j >= 0 && items[j].T > x.T)
            {
                items[j + 1] = items[j];
                j--;
            }
            items[j + 1] = x;
        }
    }

    /// <summary>Room for a trace through a class with this many solids (the tracer grows it to each class it meets; a
    /// caller can make the room ahead, when the class first comes along).</summary>
    public void Reserve(int parts)
    {
        if (this.parts.Length >= parts)
            return;
        this.parts = new int[parts];
        partIn = new double[parts];
        partOut = new double[parts];
        partState = new bool[parts];
        partBearing = new double[parts];
    }
}

/// <summary>The sea's surface in the ship's frame: z = Z + DzDx·x + DzDy·y (heights over the main deck). Level at the
/// waterline until the ship sinks, trims or heels (step 4.3).</summary>
public readonly record struct Waterplane(double Z, double DzDx, double DzDy)
{
    public static Waterplane Level(ShipClass c) => new(c.Vertical.Waterline, 0, 0);

    public double At(double x, double y) => Z + DzDx * x + DzDy * y;
}
