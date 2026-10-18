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
    internal FcUnitRow[] fcUnits = [];
    internal InstrumentRow[] instruments = [];
    internal ChargeRow[] charges = [];
    internal Design design = null!;
    internal EyeRow[] eyes = [];
    internal TubeRow[] tubes = [];
    internal LauncherRow[] launchers = [];
    internal TorpedoRow[] torpedoes = [];
    internal CellRow[] cells = [];
    internal RoomRow[] rooms = [];
    internal BulkheadRow[] bulkheads = [];
    internal DeckRow[] decks = [];
    internal TierRow[] tiers = [];
    internal SectionRow[] sections = [];
    internal FireSpaceRow[] fireSpaces = [];
    internal int[] fireOfPart = [], fireOfDeck = [];
    internal SidePlateRow[] sidePlates = [];
    internal ArmourBulkheadRow[] armourBulkheads = [];
    internal ArmourDeckRow[] armourDecks = [];
    internal InclinedRow[] inclined = [];
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
        sectionIds = [], sidePlateIds = [], armourBulkheadIds = [], inclinedIds = [], materials = [];
    internal MaterialRow[] materialRows = [];
    internal FrozenDictionary<string, int> componentIndex = null!, cellIndex = null!, roomIndex = null!, bulkheadIndex = null!,
        deckIndex = null!, tierIndex = null!, sectionIndex = null!;

    /// <summary>The design's name and id (not unique: an edited copy keeps its id).</summary>
    public string Name { get; internal init; } = "";
    public string? DesignId { get; internal init; }
    /// <summary>The design's hash: what the class registry keys on, and a save's or mission's reference.</summary>
    public ContentHash DesignHash { get; internal init; }
    /// <summary>The structure's hash (ids, kinds and links in index order): equal means the indices mean the same.</summary>
    public ContentHash LayoutHash { get; internal set; }
    /// <summary>Whether the main bulkheads have doors below the damage-control deck (the hitboxes' doors_below): the
    /// open-door path for flooding, for damage control to read (protection-plan.md H7).</summary>
    public bool DoorsBelow { get; internal set; }
    /// <summary>How the hull's shell plating and its internals are joined (docs/battle/shells-armour-damage.md E0): the
    /// blast's boundaries (riveted ×0.85), the belt's seep and the seams' leak read them.</summary>
    public Joint ShellJoint { get; internal init; }
    public Joint InternalsJoint { get; internal init; }
    /// <summary>How the fittings stand shock (E0): the armour shock, the near misses and the service networks read it.</summary>
    public ShockGrade ShockGrade { get; internal init; }
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
    /// <summary>The look's era: what the placeholders for the weapons read until the designer gives
    /// a year.</summary>
    public Era Era { get; internal set; }

    /// <summary>The uptakes' armoured gratings in the armour decks, as a share of the deck's plate
    /// (docs/battle/shells-armour-damage.md P7).</summary>
    public double GratingShare { get; internal set; } = Shipgen.Armour.GratingShareDefault;
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
    /// <summary>The fire-control units (docs/battle/fire-control.md F0): the control positions, one target each.</summary>
    public ReadOnlySpan<FcUnitRow> FcUnits => fcUnits;
    /// <summary>The fire-control units' instruments.</summary>
    public ReadOnlySpan<InstrumentRow> Instruments => instruments;
    /// <summary>The shells the design's batteries carry (its default loadout, docs/battle/shells-armour-damage.md
    /// decision 6). A ship may carry another (<see cref="Load"/>): the sim reads hers (CombatStore.Loadout), tools and
    /// what's judged of a class unseen read this.</summary>
    public Loadout Loadout { get; internal set; } = null!;

    /// <summary>A loadout of this class: its design with the batteries' shells given (Ammunition.WithLoadout), resolved by
    /// shipgen with each gun's velocity the design's, compiled as the class's own is. The design's guns, magazines and
    /// weights stay as they are (the sim's hydrostatics stay on the design's weight; flagged in the plan). Null, with
    /// the errors, when it doesn't apply; <paramref name="warnings"/> gets shipgen's (one gun, two outfits).</summary>
    public Loadout? Load(LoadoutSpec spec, List<string> errors, List<string>? warnings = null)
    {
        var loaded = Ammunition.WithLoadout(design, spec.Batteries, spec.Blocks, errors);
        if (loaded is null)
            return null;
        var shells = Ammunition.Resolve(loaded, warnings ?? [], gun: design);
        try
        {
            return Loadout.Compile(this, shells, spec.Name, ContentHash.OfDesign(loaded));
        }
        catch (ShipCompileException x)
        {
            errors.Add(x.Message);
            return null;
        }
    }
    /// <summary>The charge each turret type's guns fire (docs/visuals/muzzle-flashes.md: the flash and the smoke), by
    /// turret type; a torpedo mount's is empty.</summary>
    public ReadOnlySpan<ChargeRow> Charges => charges;

    /// <summary>The observation posts, highest first (docs/battle/plan.md step 3.2a).</summary>
    public ReadOnlySpan<EyeRow> Eyes => eyes;

    /// <summary>What the ship shows an observer (step 3.2a).</summary>
    public SilhouetteRow Silhouette { get; internal set; }
    public ReadOnlySpan<TubeRow> Tubes => tubes;

    /// <summary>The torpedo mounts and submerged tubes as one table (docs/battle/fighting.md 4.6), and the torpedoes they
    /// fire, each kind once.</summary>
    public ReadOnlySpan<LauncherRow> Launchers => launchers;
    public ReadOnlySpan<TorpedoRow> Torpedoes => torpedoes;

    /// <summary>The launcher a mount or tube is (its index in Launchers), or -1.</summary>
    public int LauncherOf(LauncherKind kind, int index)
    {
        for (int i = 0; i < launchers.Length; i++)
            if (launchers[i].Kind == kind && launchers[i].Index == index)
                return i;
        return -1;
    }
    public ReadOnlySpan<CellRow> Cells => cells;
    public ReadOnlySpan<RoomRow> Rooms => rooms;
    public ReadOnlySpan<BulkheadRow> Bulkheads => bulkheads;
    public ReadOnlySpan<DeckRow> Decks => decks;
    public ReadOnlySpan<TierRow> Tiers => tiers;
    public ReadOnlySpan<SectionRow> Sections => sections;
    public ReadOnlySpan<SidePlateRow> SidePlates => sidePlates;
    public ReadOnlySpan<ArmourBulkheadRow> ArmourBulkheads => armourBulkheads;
    public ReadOnlySpan<ArmourDeckRow> ArmourDecks => armourDecks;
    /// <summary>The inclined plates: the decks' slopes (protection-plan.md H6).</summary>
    public ReadOnlySpan<InclinedRow> Inclined => inclined;

    /// <summary>The places a fire burns (docs/battle/fire.md): the cells first, each at its own index, then the parts that
    /// burn, then a strip of weather deck a section.</summary>
    public ReadOnlySpan<FireSpaceRow> FireSpaces => fireSpaces;
    /// <summary>A component's fire space, or -1 when it has none (a funnel, a mast).</summary>
    public int FireOfPart(int component) => fireOfPart[component];
    /// <summary>A section's weather-deck fire space.</summary>
    public int FireOfDeck(int section) => fireOfDeck[section];

    /// <summary>The section a station along the hull (x, m) lies in; past either end, the end's.</summary>
    public int SectionAt(double x)
    {
        for (int i = 0; i < sections.Length; i++)
            if (x < sections[i].X1 || i == sections.Length - 1)
                return i;
        return 0;
    }

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

    /// <summary>The Neighbours pool's length: every edge of the cell graph, from both its cells.</summary>
    public int NeighbourCount => neighbours.Length;

    /// <summary>One entry of the Neighbours pool, by its index there.</summary>
    public ref readonly Neighbour Neighbour(int poolIndex) => ref neighbours[poolIndex];
    public ReadOnlySpan<ArmourLayer> ArmourAbove(Slice s) => armourAbove.AsSpan(s.Start, s.Count);
    public ReadOnlySpan<DeckPlate> DeckPlates(Slice s) => deckPlates.AsSpan(s.Start, s.Count);
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
    public string InclinedId(int i) => inclinedIds[i];
    /// <summary>A material's name (armour and plating: "Krupp cemented", "STS", "steel"), or null for -1.</summary>
    public string? Material(int i) => i < 0 ? null : materials[i];
    public int MaterialCount => materials.Length;
    /// <summary>A material's numbers (its block's, from report.materials).</summary>
    public ref readonly MaterialRow MaterialOf(int i) => ref i < 0 ? ref MaterialRow.None : ref materialRows[i];
    /// <summary>A plate of a material, tMm thick, as a share of its thickness of the formula's plate against a shell of a
    /// calibre (<see cref="MaterialRow.Against"/>); 1 for -1, a plate with no material named.</summary>
    public double MaterialQ(int i, double tMm, double calibreMm) => i < 0 ? 1 : materialRows[i].Against(tMm, calibreMm);

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
