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
    /// <summary>A solid struck inside a space (Plate: Contents, Index: the room): put on the path by the terminal walk where
    /// the shell met it (docs/battle/shell-contents.md C1c), its skin or its core as one plate.</summary>
    Struck,
}

/// <summary>Which plate a crossing goes through; Index says which row or component it is.</summary>
public enum PlateKind : byte
{
    None,
    /// <summary>The belt or a strake (Index: ShipClass.SidePlates).</summary>
    SideArmour,
    /// <summary>An armour deck (Index: ShipClass.ArmourDecks).</summary>
    ArmourDeck,
    /// <summary>An armour deck's opening over an uptake, closed by an armoured grating (Index: ShipClass.ArmourDecks), its
    /// plate the deck's times <see cref="ShipClass.GratingShare"/> (armour.gratings; docs/battle/shells-armour-damage.md P7).</summary>
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
    /// <summary>The backing behind side armour, wood or cement (Index: ShipClass.SidePlates; docs/battle/shells-armour-damage.md
    /// P7).</summary>
    Backing,
    /// <summary>What stands in a space, struck (Index: the room; <see cref="CrossingKind.Struck"/>).</summary>
    Contents,
    /// <summary>What stands in a part, struck: a gun house's guns and gear, a barbette's trunk (Index: the component;
    /// docs/battle/shell-contents.md C4b).</summary>
    PartContents,
}

public static class PlateKinds
{
    /// <summary>Whether a plate is something struck inside a space or a part (docs/battle/shell-contents.md C1c, C4b).</summary>
    public static bool IsContents(this PlateKind k) => k is PlateKind.Contents or PlateKind.PartContents;
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
/// nothing once it's grown to the ship's size; one per thread.
/// <para>A path may bend where a plate turned the shell or it glanced off one (docs/battle/shells-armour-damage.md P5):
/// <see cref="Splice"/> keeps the crossings up to the bend and appends a new leg's. T stays the distance along the bent
/// path, and <see cref="At"/> follows its legs.</para></summary>
public sealed class HitPath
{
    /// <summary>The most legs a path takes: a bend past them isn't made.</summary>
    public const int MaxLegs = 8;

    Crossing[] items;
    int count;

    // the legs, once the path has bent: leg k from legT[k] at legAt[k] along legDir[k] (none: the straight segment)
    readonly double[] legT = new double[MaxLegs];
    readonly P3[] legAt = new P3[MaxLegs], legDir = new P3[MaxLegs];
    int legs;

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
        for (int k = 1; k < legs; k++)
            sb.AppendLine(string.Format(inv, "  bent at {0:0.00} m, on along ({1:0.000}, {2:0.000}, {3:0.000})", legT[k], legDir[k].X, legDir[k].Y,
                legDir[k].Z));
        foreach (ref readonly var x in Crossings)
        {
            string what = x.Kind switch
            {
                CrossingKind.PartIn or CrossingKind.PartOut => c.ComponentId(x.Index),
                CrossingKind.CellIn or CrossingKind.CellOut => c.CellId(x.Index),
                _ => "",
            };
            string plate = PlateName(c, x);
            if (x.Plate != PlateKind.None)
                plate += string.Format(inv, " {0:0.#} mm{1}", x.Mm, c.Material(x.Material) is { } m ? " " + m : "");
            sb.AppendLine(string.Format(inv, "  {0,7:0.00} m  {1,-8} {2,-28} {3,-44} ({4:0.00}, {5:0.00}, {6:0.00})", x.T, x.Kind, what, plate, x.Point.X,
                x.Point.Y, x.Point.Z));
        }
        return sb.ToString();
    }

    /// <summary>A crossing's plate in words: the belt row, the armour deck it's on, the turret's face, and so on ("" for
    /// none).</summary>
    public static string PlateName(ShipClass c, in Crossing x) =>
        x.Plate switch
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
            PlateKind.Backing => $"backing of {c.SidePlateId(x.Index)}",
            PlateKind.Contents => $"{c.RoomId(x.Index)}: {c.ContentsOf(x.Index).Name}",
            PlateKind.PartContents => $"{c.ComponentId(x.Index)}: what's in it",
            _ => x.Plate.ToString(),
        };

    /// <summary>The point at T along the path (along its legs once it's bent).</summary>
    public P3 At(double t)
    {
        if (legs == 0)
        {
            double f = Length > 0 ? t / Length : 0;
            return new P3(Start.X + (End.X - Start.X) * f, Start.Y + (End.Y - Start.Y) * f, Start.Z + (End.Z - Start.Z) * f);
        }
        int k = Leg(t);
        double d = t - legT[k];
        return new P3(legAt[k].X + legDir[k].X * d, legAt[k].Y + legDir[k].Y * d, legAt[k].Z + legDir[k].Z * d);
    }

    /// <summary>The legs: 1 for a straight path.</summary>
    public int Legs => Math.Max(legs, 1);

    /// <summary>Leg k: where along the path it starts, its point there and its direction (unit).</summary>
    public (double T, P3 At, P3 Direction) LegOf(int k)
    {
        if (legs > 0)
            return (legT[k], legAt[k], legDir[k]);
        double l = Length > 0 ? Length : 1;
        return (0, Start, new P3((End.X - Start.X) / l, (End.Y - Start.Y) / l, (End.Z - Start.Z) / l));
    }

    /// <summary>The leg T is on.</summary>
    public int Leg(double t)
    {
        int k = 0;
        while (k + 1 < legs && legT[k + 1] <= t)
            k++;
        return k;
    }

    /// <summary>Crossings this close along a path, m, are at one point (the tracer's bisection leaves them a hair apart).</summary>
    public const double SameM = 1e-3;

    /// <summary>Bend the path at T: keep its first <paramref name="keep"/> crossings, then the crossings of the leg (a
    /// straight trace through the bend's point, which is <paramref name="atT"/> along it) past that point. The cell the
    /// shell is in there is the leg's: if it isn't the one the kept crossings leave it in, the change is made at the bend.
    /// False, and the path unchanged, when it has all its legs.</summary>
    public bool Splice(int keep, double t, HitPath leg, double atT)
    {
        if (legs >= MaxLegs)
            return false;
        if (legs == 0)
        {
            var (_, a, d) = LegOf(0);
            (legT[0], legAt[0], legDir[0], legs) = (0, a, d, 1);
        }
        var (_, _, dir) = leg.LegOf(0);
        var at = leg.At(atT);
        (legT[legs], legAt[legs], legDir[legs]) = (t, at, dir);
        legs++;
        count = Math.Min(keep, count);
        var xs = leg.Crossings;
        int from = 0;
        while (from < xs.Length && xs[from].T <= atT + SameM)
            from++;
        int was = OpenCell(Crossings), now = OpenCell(xs[..from]);
        if (was != now)
        {
            if (was >= 0)
                Add(new Crossing { T = t, Point = at, Kind = CrossingKind.CellOut, Index = was, Material = -1 });
            if (now >= 0)
                Add(new Crossing { T = t, Point = at, Kind = CrossingKind.CellIn, Index = now, Material = -1 });
        }
        foreach (ref readonly var x in xs[from..])
            Add(x with { T = t + x.T - atT });
        Overflowed |= leg.Overflowed;
        (End, Length) = (leg.End, t + leg.Length - atT);
        return true;
    }

    /// <summary>The cell the crossings leave the shell in (-1: none).</summary>
    static int OpenCell(ReadOnlySpan<Crossing> xs)
    {
        int cell = -1;
        foreach (ref readonly var x in xs)
            if (x.Kind == CrossingKind.CellIn)
                cell = x.Index;
            else if (x.Kind == CrossingKind.CellOut && x.Index == cell)
                cell = -1;
        return cell;
    }

    /// <summary>This path made a copy of another, its legs and crossings (tools keeping a hit's path: it may allocate).</summary>
    public void CopyFrom(HitPath p)
    {
        if (items.Length < p.count)
            items = new Crossing[p.items.Length];
        p.items.AsSpan(0, p.count).CopyTo(items);
        (Start, End, Length, count, Overflowed, legs) = (p.Start, p.End, p.Length, p.count, p.Overflowed, p.legs);
        p.legT.AsSpan(0, legs).CopyTo(legT);
        p.legAt.AsSpan(0, legs).CopyTo(legAt);
        p.legDir.AsSpan(0, legs).CopyTo(legDir);
    }

    /// <summary>Empty, for the segment start to end (a query that didn't come near the ship).</summary>
    public void Clear(P3 start, P3 end)
    {
        double dx = end.X - start.X, dy = end.Y - start.Y, dz = end.Z - start.Z;
        Begin(start, end, Math.Sqrt(dx * dx + dy * dy + dz * dz));
    }

    internal void Begin(P3 start, P3 end, double length)
    {
        (Start, End, Length, count, Overflowed, legs) = (start, end, length, 0, false, 0);
    }

    /// <summary>A crossing put in at an index, those from it on moved one along (the terminal walk's struck contents: it
    /// keeps the order by T). False, and the path unchanged, when it's full.</summary>
    public bool Insert(int at, in Crossing x)
    {
        if (count == items.Length)
        {
            Overflowed = true;
            return false;
        }
        items.AsSpan(at, count - at).CopyTo(items.AsSpan(at + 1));
        items[at] = x;
        count++;
        return true;
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
