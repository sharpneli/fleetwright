using System.Collections.Frozen;
using Fleetwright.Shipgen;

namespace Fleetwright.Sim.Ships;

/// <summary>A design compiled for the sim (docs/battle/plan.md, "The ships' data"): Shipgen's hitboxes and report as
/// int-indexed, read-only tables, shared by every ship of the class. Made once by <see cref="ShipClassCompiler"/> and
/// never written after; nothing in the sim reads the hitboxes once it's made.
/// <para>Each table is an array of immutable rows (Rows.cs) with a string id per row; lists inside rows are slices of
/// the pools (Links, Points, Numbers, Neighbours, ...). Indices are stable for one design and one shipgen version
/// (the layout hash says which). Every query is by index and allocates nothing; the id maps are for tools, saves and
/// debugging.</para></summary>
public sealed class ShipClass
{
    // Filled in by ShipClassCompiler, before the class escapes it; never written after.
    internal ComponentRow[] components = [];
    internal MountRow[] mounts = [];
    internal TurretTypeRow[] turretTypes = [];
    internal BarbetteRow[] barbettes = [];
    internal FunnelRow[] funnels = [];
    internal ShaftLineRow[] shaftLines = [];
    internal RudderRow[] rudders = [];
    internal DirectorRow[] directors = [];
    internal BatteryRow[] batteries = [];
    internal ShellTypeRow[] shellTypes = [];
    internal ShellLoad[] shellLoads = [];
    internal EyeRow[] eyes = [];
    internal TubeRow[] tubes = [];
    internal CellRow[] cells = [];
    internal RoomRow[] rooms = [];
    internal BulkheadRow[] bulkheads = [];
    internal DeckRow[] decks = [];
    internal TierRow[] tiers = [];
    internal SectionRow[] sections = [];
    internal SidePlateRow[] sidePlates = [];
    internal ArmourBulkheadRow[] armourBulkheads = [];
    internal ArmourDeckRow[] armourDecks = [];
    internal int[] links = [];
    internal Pt[] points = [];
    internal double[] numbers = [];
    internal Neighbour[] neighbours = [];
    internal ArmourLayer[] armourAbove = [];
    internal DeckPlate[] deckPlates = [];
    internal FunctionRow[] functions = [];
    internal int[] functionNodes = [], mountOfComponent = [];
    internal double[] powerKn = [], powerShp = [];
    internal string[] componentIds = [], turretTypeIds = [], cellIds = [], roomIds = [], bulkheadIds = [], deckIds = [], tierIds = [],
        sectionIds = [], sidePlateIds = [], armourBulkheadIds = [], materials = [], shellTypeIds = [];
    internal FrozenDictionary<string, int> componentIndex = null!, cellIndex = null!, roomIndex = null!, bulkheadIndex = null!,
        deckIndex = null!, tierIndex = null!, sectionIndex = null!;

    /// <summary>The design's name and id (not unique: an edited copy keeps its id).</summary>
    public string Name { get; internal init; } = "";
    public string? DesignId { get; internal init; }
    /// <summary>The design's hash: what the class registry keys on, and a save's or mission's reference.</summary>
    public ContentHash DesignHash { get; internal init; }
    /// <summary>The structure's hash (ids, kinds and links in index order): equal means the indices mean the same.</summary>
    public ContentHash LayoutHash { get; internal set; }
    /// <summary>The shipgen it was built by (<see cref="ShipDesign.Version"/>).</summary>
    public int ShipgenVersion { get; internal init; }

    /// <summary>The hull: its form as a query object (half-breadths, inside tests, ray crossings, deck tops), its
    /// length and beam, its heights, and the main deck's outline.</summary>
    public HullField Hull { get; internal init; } = null!;
    public double Length { get; internal init; }
    public double Beam { get; internal init; }
    public VerticalRow Vertical { get; internal init; }
    public Slice DeckOutline { get; internal init; }
    public HydrostaticsRow Hydrostatics { get; internal init; }
    public MotionFacts Motion { get; internal init; }
    /// <summary>The look's era: what the placeholders for the weapons and the fire load read until the designer gives
    /// a year.</summary>
    public Era Era { get; internal set; }
    /// <summary>The hull's own plating, beside the armour (fuzes arm on it, holes open in it).</summary>
    public PlatingRow Plating { get; internal set; }
    /// <summary>The parts and armour as solids for the tracer (ShipTracer), and the cells by section.</summary>
    public ShipSolids Solids { get; internal set; } = null!;

    // ------------------------------------------------------------------ the tables

    public ReadOnlySpan<ComponentRow> Components => components;
    public ReadOnlySpan<MountRow> Mounts => mounts;
    public ReadOnlySpan<TurretTypeRow> TurretTypes => turretTypes;
    public ReadOnlySpan<BarbetteRow> Barbettes => barbettes;
    public ReadOnlySpan<FunnelRow> Funnels => funnels;
    public ReadOnlySpan<ShaftLineRow> ShaftLines => shaftLines;
    public ReadOnlySpan<RudderRow> Rudders => rudders;
    public ReadOnlySpan<DirectorRow> Directors => directors;

    /// <summary>The gun batteries (docs/battle/fighting.md 4.0): every gun mount is in one.</summary>
    public ReadOnlySpan<BatteryRow> Batteries => batteries;
    /// <summary>The shells the batteries carry, each kind once.</summary>
    public ReadOnlySpan<ShellTypeRow> ShellTypes => shellTypes;

    /// <summary>The observation posts, highest first (docs/battle/plan.md step 3.2a).</summary>
    public ReadOnlySpan<EyeRow> Eyes => eyes;

    /// <summary>What the ship shows an observer (step 3.2a).</summary>
    public SilhouetteRow Silhouette { get; internal set; }
    public ReadOnlySpan<TubeRow> Tubes => tubes;
    public ReadOnlySpan<CellRow> Cells => cells;
    public ReadOnlySpan<RoomRow> Rooms => rooms;
    public ReadOnlySpan<BulkheadRow> Bulkheads => bulkheads;
    public ReadOnlySpan<DeckRow> Decks => decks;
    public ReadOnlySpan<TierRow> Tiers => tiers;
    public ReadOnlySpan<SectionRow> Sections => sections;
    public ReadOnlySpan<SidePlateRow> SidePlates => sidePlates;
    public ReadOnlySpan<ArmourBulkheadRow> ArmourBulkheads => armourBulkheads;
    public ReadOnlySpan<ArmourDeckRow> ArmourDecks => armourDecks;

    /// <summary>What the damage model asks of the ship's parts (docs/battle/fighting.md 4.3a): each function's needs, as
    /// nodes (<see cref="FunctionNodes"/>).</summary>
    public ReadOnlySpan<FunctionRow> Functions => functions;

    /// <summary>The parts damage reaches, as nodes: the components (0 to Components.Length - 1), then the rooms.</summary>
    public int NodeCount => components.Length + rooms.Length;
    public int RoomNode(int room) => components.Length + room;

    /// <summary>The rooms in a cell: its own and those sharing it (allocates nothing).</summary>
    public RoomsInCell RoomsIn(int cell) => new(this, cell);

    /// <summary>The mount a component is, or stands on (a barbette); -1 for none.</summary>
    public int MountOf(int component) => mountOfComponent[component];

    // ------------------------------------------------------------------ the pools

    public ReadOnlySpan<int> Links(Slice s) => links.AsSpan(s.Start, s.Count);
    public ReadOnlySpan<Pt> Points(Slice s) => points.AsSpan(s.Start, s.Count);
    public ReadOnlySpan<double> Numbers(Slice s) => numbers.AsSpan(s.Start, s.Count);
    public ReadOnlySpan<Neighbour> Neighbours(Slice s) => neighbours.AsSpan(s.Start, s.Count);
    public ReadOnlySpan<ArmourLayer> ArmourAbove(Slice s) => armourAbove.AsSpan(s.Start, s.Count);
    public ReadOnlySpan<DeckPlate> DeckPlates(Slice s) => deckPlates.AsSpan(s.Start, s.Count);
    public ReadOnlySpan<ShellLoad> ShellLoads(Slice s) => shellLoads.AsSpan(s.Start, s.Count);
    public ReadOnlySpan<int> FunctionNodes(Slice s) => functionNodes.AsSpan(s.Start, s.Count);

    // ------------------------------------------------------------------ ids

    public string ComponentId(int i) => componentIds[i];
    public string TurretTypeId(int i) => turretTypeIds[i];
    public string CellId(int i) => cellIds[i];
    public string RoomId(int i) => roomIds[i];
    public string BulkheadId(int i) => bulkheadIds[i];
    public string DeckId(int i) => deckIds[i];
    public string TierId(int i) => tierIds[i];
    public string SectionId(int i) => sectionIds[i];
    public string SidePlateId(int i) => sidePlateIds[i];
    public string ArmourBulkheadId(int i) => armourBulkheadIds[i];
    /// <summary>A shell type's id, shared across classes: the gun, the kind, the era and the quality ("305/45 AP
    /// great_war rn1916"), so equal ids are equal rows.</summary>
    public string ShellTypeId(int i) => shellTypeIds[i];
    /// <summary>A material's name (armour and plating: "Krupp cemented", "STS", "steel"), or null for -1.</summary>
    public string? Material(int i) => i < 0 ? null : materials[i];
    public int MaterialCount => materials.Length;

    /// <summary>The index of an id, or -1.</summary>
    public int ComponentIndex(string id) => componentIndex.GetValueOrDefault(id, -1);
    public int CellIndex(string id) => cellIndex.GetValueOrDefault(id, -1);
    public int RoomIndex(string id) => roomIndex.GetValueOrDefault(id, -1);
    public int BulkheadIndex(string id) => bulkheadIndex.GetValueOrDefault(id, -1);
    public int DeckIndex(string id) => deckIndex.GetValueOrDefault(id, -1);
    public int TierIndex(string id) => tierIndex.GetValueOrDefault(id, -1);
    public int SectionIndex(string id) => sectionIndex.GetValueOrDefault(id, -1);

    // ------------------------------------------------------------------ moving

    /// <summary>The power the hull needs at a speed, shp: shipgen's resistance model at full load (Motion.PowerShp at
    /// MaxSpeedKn, give or take the report's rounding), linear between its points, and flat beyond its last.</summary>
    public double PowerAt(double kn)
    {
        if (kn <= 0)
            return 0;
        int n = powerKn.Length;
        if (kn >= powerKn[n - 1])
            return powerShp[n - 1];
        int i = (int)(kn / PowerStepKn);
        double f = (kn - powerKn[i]) / (powerKn[i + 1] - powerKn[i]);
        return powerShp[i] + (powerShp[i + 1] - powerShp[i]) * f;
    }

    /// <summary>The speed a power drives the hull at, knots: PowerAt's inverse (the curve rises).</summary>
    public double SpeedAt(double shp)
    {
        if (shp <= 0)
            return 0;
        int n = powerShp.Length;
        if (shp >= powerShp[n - 1])
            return powerKn[n - 1];
        int i = Array.BinarySearch(powerShp, shp);
        if (i >= 0)
            return powerKn[i];
        i = ~i - 1;
        double f = (shp - powerShp[i]) / (powerShp[i + 1] - powerShp[i]);
        return powerKn[i] + (powerKn[i + 1] - powerKn[i]) * f;
    }

    /// <summary>The curve's points: speeds every PowerStepKn from 0 to past the overload's speed.</summary>
    public ReadOnlySpan<double> PowerCurveKn => powerKn;
    public ReadOnlySpan<double> PowerCurveShp => powerShp;
    public const double PowerStepKn = 0.5;

    public override string ToString() => $"{Name} ({DesignHash.Short})";
}

/// <summary>The rooms in a cell (<see cref="ShipClass.RoomsIn"/>): its own room, then those sharing it.</summary>
public readonly ref struct RoomsInCell(ShipClass c, int cell)
{
    public Enumerator GetEnumerator() => new(c, cell);

    public ref struct Enumerator(ShipClass c, int cell)
    {
        int i = -2;
        public int Current { get; private set; }

        public bool MoveNext()
        {
            ref readonly var row = ref c.Cells[cell];
            while (true)
            {
                i++;
                if (i == -1)
                {
                    if (row.Room < 0)
                        continue;
                    Current = row.Room;
                    return true;
                }
                var also = c.Links(row.Also);
                if (i >= also.Length)
                    return false;
                Current = also[i];
                return true;
            }
        }
    }
}
