using Fleetwright.Shipgen;
using Fleetwright.Sim.Core;
using Fleetwright.Sim.Ships;

namespace Fleetwright.Sim.Combat;

/// <summary>What an immune zone's shells are aimed at (docs/designer-ui/immune-zone.md): her armour, the main belt (its
/// strakes and zones, on the side or inside it), the main battery's barbettes, the main armour deck; each plate's shells
/// all down the lines through its aim points (its centre; each barbette's; five along the deck), the main battery's barbettes where they stand clear of the hull, the main armour deck (Armour.MainDeck's, with its slopes),
/// each beaten by a shell that comes through it whole; or her vitals, the magazines and the machinery (boiler and engine
/// rooms), reached by a shell that gets into them whole (decision 2), each from its own shells.</summary>
public enum ZoneTarget : byte
{
    Belt,
    Barbettes,
    Deck,
    Magazines,
    Machinery,
    /// <summary>A plate picked in the designer (<see cref="PlatePick"/>): the user, 2026-11-14, "clicking on a plate ... and
    /// it'll run the simulation for that plate (center of it)".</summary>
    Plate,
}

/// <summary>The parts of her armour a plate can be picked from, as the Protection tab lists them.</summary>
public enum PlatePart : byte
{
    Belt,
    UpperBelt,
    EndFore,
    EndAft,
    BeltZone,
    Bulkheads,
    /// <summary>An armour deck, by its deck number (<see cref="PlatePick.Deck"/>: 0 the main deck, 1 the second, ...).</summary>
    Deck,
    Slopes,
    Barbettes,
    Turrets,
    ConningTower,
}

/// <summary>A plate to test (<see cref="ZoneTarget.Plate"/>): a part, and for a deck its number.</summary>
public readonly record struct PlatePick(PlatePart Part, int Deck = 0)
{
    /// <summary>Whether it lies as a deck: beaten from far off, by falling shells, rather than close in.</summary>
    public bool Lies => Part is PlatePart.Deck or PlatePart.Slopes;
}

/// <summary>One cell of an immune zone, a target, a range and a target angle: the shells aimed at the target (lines that,
/// unstopped, would meet it) and what came of them. A plate: those through it whole. The vitals: those in whole through
/// the side or through the deck (by the heaviest plate each beat), or in only as pieces.</summary>
public struct ZoneCell
{
    public int Shots, Through, Side, Deck, Broken;
    /// <summary>The shells it has been filled to (each shell's index: the same shells at every range).</summary>
    public int Tried;
    /// <summary>Lines drawn, and the shells for which no line met the target (hidden from there: below the sea's reach for
    /// a flat fall, or behind the rest of her).</summary>
    public int Drawn, Unseen;
    /// <summary>The lines traced through her (the rest were sure misses), for the cost.</summary>
    public int Traced;

    /// <summary>Whether no shell from here could meet the target at all: out of sight, as good as immune.</summary>
    public readonly bool Hidden => Shots == 0 && Unseen > 0;

    public readonly double ThroughShare => Shots > 0 ? (double)Through / Shots : 0;
    public readonly double SideShare => Shots > 0 ? (double)Side / Shots : 0;
    public readonly double DeckShare => Shots > 0 ? (double)Deck / Shots : 0;
    public readonly double BrokenShare => Shots > 0 ? (double)Broken / Shots : 0;

    /// <summary>The share that got through or in: a plate's beaten, the vitals' reached either way.</summary>
    public readonly double InShare => Shots > 0 ? (double)(Through + Side + Deck) / Shots : 0;
}

/// <summary>A plate that let shells through (or into the vitals): its kind and index (as the path's crossings name it), and
/// its thickness where they met it.</summary>
public readonly record struct ZonePlate(PlateKind Kind, int Index, double Mm);

/// <summary>An immune zone (docs/designer-ui/immune-zone.md): a shell fired at a class at each range out to its gun's
/// reach and each target angle, aimed at a target (<see cref="ZoneTarget"/>), each shell walked through her by
/// <see cref="Terminal.Resolve"/> on an intact ship, upright on her waterline, with the walk's own rolls. A plate is
/// beaten by a shell through it whole (not broken, not glanced off): the belt's and the barbettes' inner edge is the range
/// out to which half the shells at them get through, the deck's outer edge the range from which half do. The vitals are
/// reached by a shell that enters one of their spaces whole (its filler in it), or bursts inside one; broken up on the way
/// it counts apart; each put down to the side or the deck by the heaviest plate it beat (within 45° of level a deck).
/// <para>Every range of a target and an angle fires the same shells (the same aim across the outline, lots and rolls), so
/// a share falls or rises with range as the physics makes it, not by each cell's own draws; each shell comes from its own
/// substream, so the zone is the same on any number of threads, and filled in passes (<see cref="Fill(int, ReadOnlySpan{ZoneTarget}, int, CancellationToken)"/>)
/// it's the same as filled at once.</para></summary>
public sealed class ImmuneZone
{
    /// <summary>The target angles, degrees off her bow the shells come from (one side: she's near enough symmetric).</summary>
    public static readonly double[] Angles = [0, 15, 30, 45, 60, 75, 90, 105, 120, 135, 150, 165, 180];

    /// <summary>The armour targets, the zone's own view; the vitals are asked for apart.</summary>
    public static readonly ZoneTarget[] Armour = [ZoneTarget.Belt, ZoneTarget.Barbettes, ZoneTarget.Deck];

    public static readonly ZoneTarget[] All = [ZoneTarget.Belt, ZoneTarget.Barbettes, ZoneTarget.Deck, ZoneTarget.Magazines, ZoneTarget.Machinery];

    public const int Targets = 6;

    /// <summary>The ranges' step, m (the first range is one step).</summary>
    public const double RangeStepM = 1000;

    /// <summary>The muzzle's height over the sea for the flight, m (GivenHits's).</summary>
    public const double MuzzleM = 10;

    /// <summary>Half the shells at the target getting through: an edge (decision 3).</summary>
    public const double Edge = 0.5;

    /// <summary>The most lines drawn for a shell before it's given up as unseen (the target hidden from it behind the sea
    /// or the rest of her).</summary>
    const int DrawsPerShot = 24;

    /// <summary>A cell whose first this many shells all went unseen is hidden from there and drawn no more.</summary>
    const int HiddenAfter = 8;

    public ShipClass Class { get; }
    public ShellTypeRow Shell { get; }
    public double ReachM { get; }
    /// <summary>Each range's, m, and the shell's fall there (degrees under the horizontal) and striking speed (m/s).</summary>
    public double[] RangesM { get; }
    public (double FallDeg, double StrikeMs)[] Falls { get; }

    /// <summary>The shells a target's cells have been filled to.</summary>
    public int ShotsPerCell(ZoneTarget t) => shotsPerCell[(int)t];

    /// <summary>For benches: each shell walked (the cell's index, its path, the walk's result, where it met the target, NaN
    /// for never). Called on the filling threads.</summary>
    public Action<int, HitPath, TerminalResult, double>? Each { get; set; }

    readonly SimSettings settings;
    readonly ulong seed;
    readonly bool[][] vital;
    readonly bool[] mainDeck;
    readonly P3? beltCentre;
    readonly P3[][] aims = new P3[Targets][];

    /// <summary>The point the belt's shells are aimed at (null: she has no belt).</summary>
    public P3? BeltCentre => beltCentre;

    /// <summary>The points a plate target's shells are aimed at, in turn (the belt's centre, each main barbette's, five
    /// along the main deck); empty for the vitals, whose shells fall over their outline.</summary>
    public ReadOnlySpan<P3> Aims(ZoneTarget t) => aims[(int)t];
    readonly Box[][] boxes;
    readonly ZoneCell[] cells;
    readonly Dictionary<ZonePlate, int>[] sidePlates, deckPlates;
    readonly double[] bearings;
    readonly int[] shotsPerCell = new int[Targets];

    /// <summary>A zone of a shell on a class, its gun's elevation up to maxElevationDeg; empty until filled.</summary>
    public ImmuneZone(ShipClass c, in ShellTypeRow shell, double maxElevationDeg, SimSettings s, ulong seed, PlatePick? plate = null)
    {
        this.plate = plate;
        foreach (ref readonly var bh in c.ArmourBulkheads)
            citadelBulkheadMm = Math.Max(citadelBulkheadMm, bh.ThicknessMm);
        (Class, Shell, settings, this.seed) = (c, shell, s, seed);
        var table = new RangeTable(BallisticKey.Of(shell, maxElevationDeg), MuzzleM);
        ReachM = table.ReachM;
        int n = Math.Max(0, (int)Math.Floor(ReachM / RangeStepM));
        RangesM = new double[n];
        Falls = new (double, double)[n];
        for (int i = 0; i < n; i++)
        {
            RangesM[i] = (i + 1) * RangeStepM;
            Falls[i] = table.At(RangesM[i]);
        }
        // what each target is to the lines drawn at it: boxes, and for the vitals their cells
        var b = c.Solids.Bounds;
        boxes = new Box[Targets][];
        vital = new bool[2][];
        for (int v = 0; v < 2; v++)
        {
            var t = (ZoneTarget)(v + 3);
            vital[v] = new bool[c.Cells.Length];
            var list = new List<Box>();
            for (int i = 0; i < c.Cells.Length; i++)
            {
                ref readonly var cell = ref c.Cells[i];
                bool of = Of(c, cell.Room, t);
                foreach (int r in c.Links(cell.Also))
                    of |= Of(c, r, t);
                vital[v][i] = of;
                if (of)
                    list.Add(cell.Box);
            }
            boxes[v + 3] = [.. list];
        }
        var belt = new List<Box>();
        foreach (ref readonly var p in c.SidePlates)
            if (p.Kind is StrakeKind.Belt or StrakeKind.Zone)
                belt.Add(new Box(p.X0, p.X1, b.Y0, b.Y1, p.Bottom, p.Top));
        foreach (ref readonly var p in c.Inclined)
            if (p.Kind is InclinedKind.Belt or InclinedKind.Zone)
                belt.Add(new Box(p.X0, p.X1, b.Y0, b.Y1, Math.Min(p.Z0, p.Z1), Math.Max(p.Z0, p.Z1)));
        boxes[(int)ZoneTarget.Belt] = [.. belt];
        // her main belt's centre, on her starboard side (the shells come from starboard): the one point its shells are aimed
        // at, the thickest belt plate's (the largest of equals), on the side or inside it; halfway along it, and halfway up the part of it over the
        // waterline (where shells meet a belt: one deep under it would be aimed at the sea)
        double best = 0;
        foreach (ref readonly var p in c.SidePlates)
            if (p.Kind == StrakeKind.Belt && p.ThicknessMm * 1e6 + (p.X1 - p.X0) * (p.Top - p.Bottom) > best)
            {
                best = p.ThicknessMm * 1e6 + (p.X1 - p.X0) * (p.Top - p.Bottom);
                double x = (p.X0 + p.X1) / 2, z = Over(p.Bottom, p.Top, c.Vertical.Waterline);
                beltCentre = new P3(x, c.Hull.HalfWidth(x, z), z);
            }
        foreach (ref readonly var p in c.Inclined)
            if (p.Kind == InclinedKind.Belt && p.ThicknessMm * 1e6 + (p.X1 - p.X0) * Math.Abs(p.Z1 - p.Z0) > best)
            {
                best = p.ThicknessMm * 1e6 + (p.X1 - p.X0) * Math.Abs(p.Z1 - p.Z0);
                double x = (p.X0 + p.X1) / 2, z = Over(Math.Min(p.Z0, p.Z1), Math.Max(p.Z0, p.Z1), c.Vertical.Waterline);
                beltCentre = new P3(x, p.YAt(c.Hull, x, z), z);
            }
        var barbettes = new List<Box>();
        foreach (ref readonly var sd in c.Solids.Solids)
            if (sd.Component >= 0 && MainBarbette(c, sd.Component) && sd.Box.Top > 0)
                barbettes.Add(sd.Box with { Base = Math.Max(sd.Box.Base, 0) });   // where it stands over the main deck
        boxes[(int)ZoneTarget.Barbettes] = [.. barbettes];
        // the main armour deck: Armour.MainDeck's (the class marks it), else the thickest
        mainDeck = new bool[c.ArmourDecks.Length];
        bool anyMain = false;
        double thickest = 0;
        foreach (ref readonly var d in c.ArmourDecks)
            (anyMain, thickest) = (anyMain || d.Main, Math.Max(thickest, d.ThicknessMm));
        var deck = new List<Box>();
        for (int i = 0; i < c.ArmourDecks.Length; i++)
        {
            ref readonly var d = ref c.ArmourDecks[i];
            mainDeck[i] = anyMain ? d.Main : d.ThicknessMm >= thickest;
            if (!mainDeck[i])
                continue;
            double hw = double.IsNaN(d.W) ? Math.Max(-b.Y0, b.Y1) : d.W / 2;
            deck.Add(new Box(d.X0, d.X1, -hw, hw, d.Z - 0.3, d.Z + 0.3));
        }
        foreach (ref readonly var p in c.Inclined)
            if (p.Kind == InclinedKind.Slope)
                deck.Add(new Box(p.X0, p.X1, b.Y0, b.Y1, Math.Min(p.Z0, p.Z1), Math.Max(p.Z0, p.Z1)));
        boxes[(int)ZoneTarget.Deck] = [.. deck];
        // the plates' aim points (decision 9: the belt at its centre, "the same for barbettes and the deck"): every shell
        // down a line through one of them, so a cell's shells differ by the plate's lot and the walk's rolls alone
        aims[(int)ZoneTarget.Belt] = beltCentre is { } bc0 ? [bc0] : [];
        var bp = new List<P3>();
        foreach (ref readonly var sd in c.Solids.Solids)
            if (sd.Component >= 0 && MainBarbette(c, sd.Component))
            {
                // halfway up the part of it that stands over her deck there
                double deckTop = c.Hull.DeckTop(sd.Cx), lo = Math.Max(sd.Box.Base, deckTop);
                if (sd.Box.Top > lo)
                    bp.Add(new P3(sd.Cx, sd.Cy, (lo + sd.Box.Top) / 2));
            }
        aims[(int)ZoneTarget.Barbettes] = [.. bp];
        // the main deck: five points along its longest stretch, on the centreline (one point alone would put the deck's
        // answer on whatever stands over it: a secondary turret, a funnel)
        int longestAt = -1;
        double longest = 0;
        for (int k = 0; k < c.ArmourDecks.Length; k++)
            if (mainDeck[k] && c.ArmourDecks[k].X1 - c.ArmourDecks[k].X0 > longest)
                (longest, longestAt) = (c.ArmourDecks[k].X1 - c.ArmourDecks[k].X0, k);
        if (longestAt >= 0)
        {
            var d = c.ArmourDecks[longestAt];
            aims[(int)ZoneTarget.Deck] = [.. new[] { 0.5, 0.1, 0.9, 0.3, 0.7 }.Select(f => new P3(d.X0 + f * (d.X1 - d.X0), 0, d.Z))];
        }
        else
            aims[(int)ZoneTarget.Deck] = [];
        aims[(int)ZoneTarget.Magazines] = [];
        aims[(int)ZoneTarget.Machinery] = [];
        // the plate picked: its own aim points
        aims[(int)ZoneTarget.Plate] = plate is { } pk ? AimsOf(c, pk) : [];
        boxes[(int)ZoneTarget.Plate] = [.. aims[(int)ZoneTarget.Plate].ToArray().Select(a => new Box(a.X, a.X, a.Y, a.Y, a.Z, a.Z))];
        cells = new ZoneCell[Targets * n * Angles.Length];
        sidePlates = new Dictionary<ZonePlate, int>[cells.Length];
        deckPlates = new Dictionary<ZonePlate, int>[cells.Length];
        bearings = new double[c.Mounts.Length];
        for (int i = 0; i < bearings.Length; i++)
            bearings[i] = c.Mounts[i].RestDeg;
    }

    static bool Of(ShipClass c, int room, ZoneTarget v) => room >= 0 && (v == ZoneTarget.Magazines
        ? c.Rooms[room].Kind == RoomKind.Magazine
        : c.Rooms[room].Kind is RoomKind.BoilerRoom or RoomKind.EngineRoom);

    static bool IsVitals(ZoneTarget t) => t is ZoneTarget.Magazines or ZoneTarget.Machinery;

    readonly PlatePick? plate;
    readonly double citadelBulkheadMm;

    /// <summary>The plate picked, if any.</summary>
    public PlatePick? Picked => plate;

    /// <summary>Whether a target lies as a deck (its edge the far one: beaten from a range on).</summary>
    bool LiesFlat(ZoneTarget t) => t == ZoneTarget.Deck || t == ZoneTarget.Plate && plate is { Lies: true };

    /// <summary>A side plate's centre on her starboard side: halfway along it, halfway up its part over the waterline.</summary>
    static P3 SideAim(ShipClass c, double x0, double x1, double bottom, double top)
    {
        double x = (x0 + x1) / 2, z = Over(bottom, top, c.Vertical.Waterline);
        return new P3(x, c.Hull.HalfWidth(x, z), z);
    }

    /// <summary>The points a picked plate's shells are aimed at: its centre (the largest of its rows, on the starboard
    /// side), five along a deck, each barbette's, turret's or bulkhead's in turn.</summary>
    static P3[] AimsOf(ShipClass c, PlatePick pick)
    {
        var res = new List<P3>();
        void Largest(Func<SidePlateRow, bool> of)
        {
            SidePlateRow? best = null;
            foreach (var p in c.SidePlates.ToArray())
                if (of(p) && (best is not { } b || (p.X1 - p.X0) * (p.Top - p.Bottom) > (b.X1 - b.X0) * (b.Top - b.Bottom)))
                    best = p;
            if (best is { } q)
                res.Add(SideAim(c, q.X0, q.X1, q.Bottom, q.Top));
        }
        switch (pick.Part)
        {
            case PlatePart.UpperBelt:
                Largest(p => p.Kind == StrakeKind.Upper);
                break;
            case PlatePart.EndFore:
                Largest(p => p.Kind == StrakeKind.End && p.Extent == Extent.Fore);
                break;
            case PlatePart.EndAft:
                Largest(p => p.Kind == StrakeKind.End && p.Extent == Extent.Aft);
                break;
            case PlatePart.BeltZone:
                Largest(p => p.Kind == StrakeKind.Zone);
                foreach (var p in c.Inclined.ToArray())
                    if (res.Count == 0 && p.Kind == InclinedKind.Zone)
                    {
                        double x = (p.X0 + p.X1) / 2, z = Over(Math.Min(p.Z0, p.Z1), Math.Max(p.Z0, p.Z1), c.Vertical.Waterline);
                        res.Add(new P3(x, p.YAt(c.Hull, x, z), z));
                    }
                break;
            case PlatePart.Bulkheads:
                // the citadel's: the thickest (the end belts' own bulkheads are thinner)
                double thick = 0;
                foreach (var bh in c.ArmourBulkheads.ToArray())
                    thick = Math.Max(thick, bh.ThicknessMm);
                foreach (var bh in c.ArmourBulkheads.ToArray())
                    if (bh.ThicknessMm >= 0.9 * thick)
                        res.Add(new P3(bh.X, 0, (bh.Bottom + bh.Top) / 2));
                break;
            case PlatePart.Deck:
                ArmourDeckRow? longest = null;
                foreach (var d in c.ArmourDecks.ToArray())
                    if (c.Decks[d.Deck].Number == pick.Deck && (longest is not { } l || d.X1 - d.X0 > l.X1 - l.X0))
                        longest = d;
                if (longest is { } ld)
                    foreach (double f in (ReadOnlySpan<double>)[0.5, 0.1, 0.9, 0.3, 0.7])
                        res.Add(new P3(ld.X0 + f * (ld.X1 - ld.X0), 0, ld.Z));
                break;
            case PlatePart.Slopes:
                foreach (var p in c.Inclined.ToArray())
                    if (p.Kind == InclinedKind.Slope)
                    {
                        double x = (p.X0 + p.X1) / 2, z = (p.Z0 + p.Z1) / 2;
                        res.Add(new P3(x, p.YAt(c.Hull, x, z), z));
                    }
                break;
            case PlatePart.Barbettes:
                foreach (var sd in c.Solids.Solids.ToArray())
                    if (sd.Component >= 0 && MainBarbette(c, sd.Component))
                    {
                        double lo = Math.Max(sd.Box.Base, c.Hull.DeckTop(sd.Cx));
                        if (sd.Box.Top > lo)
                            res.Add(new P3(sd.Cx, sd.Cy, (lo + sd.Box.Top) / 2));
                    }
                break;
            case PlatePart.Turrets:
                foreach (var sd in c.Solids.Solids.ToArray())
                    if (sd.Component >= 0 && MainTurret(c, sd.Component))
                        res.Add(new P3(sd.Cx, sd.Cy, (sd.Box.Base + sd.Box.Top) / 2));
                break;
            case PlatePart.ConningTower:
                foreach (var sd in c.Solids.Solids.ToArray())
                    if (sd.Component >= 0 && c.Components[sd.Component].Kind == ComponentKind.ConningTower)
                        res.Add(new P3((sd.Box.X0 + sd.Box.X1) / 2, (sd.Box.Y0 + sd.Box.Y1) / 2, (sd.Box.Base + sd.Box.Top) / 2));
                break;
            default:
                Largest(p => p.Kind == StrakeKind.Belt);
                foreach (var p in c.Inclined.ToArray())
                    if (p.Kind == InclinedKind.Belt && (res.Count == 0 || p.ThicknessMm > 0 && res.Count == 0))
                    {
                        double x = (p.X0 + p.X1) / 2, z = Over(Math.Min(p.Z0, p.Z1), Math.Max(p.Z0, p.Z1), c.Vertical.Waterline);
                        res.Add(new P3(x, p.YAt(c.Hull, x, z), z));
                    }
                break;
        }
        return [.. res];
    }

    /// <summary>Whether a component is one of her main battery's turrets.</summary>
    static bool MainTurret(ShipClass c, int component) =>
        c.Components[component].Kind == ComponentKind.Main && c.MountOf(component) is int m and >= 0 && c.Mounts[m].Kind == ComponentKind.Main;

    /// <summary>Whether a crossing is the picked plate, struck from outside (outer: against its outer face; inHull: the
    /// shell already inside her hull).</summary>
    bool PlateMet(in Crossing x, bool outer, bool inHull)
    {
        var c = Class;
        return plate?.Part switch
        {
            PlatePart.UpperBelt => outer && x.Plate == PlateKind.SideArmour && c.SidePlates[x.Index].Kind == StrakeKind.Upper,
            PlatePart.EndFore => outer && x.Plate == PlateKind.SideArmour && c.SidePlates[x.Index] is { Kind: StrakeKind.End, Extent: Extent.Fore },
            PlatePart.EndAft => outer && x.Plate == PlateKind.SideArmour && c.SidePlates[x.Index] is { Kind: StrakeKind.End, Extent: Extent.Aft },
            PlatePart.BeltZone => outer && (x.Plate == PlateKind.SideArmour && c.SidePlates[x.Index].Kind == StrakeKind.Zone
                || x.Plate == PlateKind.Inclined && c.Inclined[x.Index].Kind == InclinedKind.Zone),
            PlatePart.Bulkheads => outer && x.Plate == PlateKind.ArmourBulkhead && x.Mm >= 0.9 * citadelBulkheadMm,
            PlatePart.Deck => outer && x.Plate == PlateKind.ArmourDeck && c.Decks[c.ArmourDecks[x.Index].Deck].Number == plate.Value.Deck,
            PlatePart.Slopes => outer && x.Plate == PlateKind.Inclined && c.Inclined[x.Index].Kind == InclinedKind.Slope,
            PlatePart.Barbettes => x.Plate == PlateKind.Barbette && x.Kind == CrossingKind.PartIn && !inHull && MainBarbette(c, x.Index),
            PlatePart.Turrets => x.Kind == CrossingKind.PartIn && x.Plate is >= PlateKind.TurretFace and <= PlateKind.TurretRoof && MainTurret(c, x.Index),
            PlatePart.ConningTower => x.Kind == CrossingKind.PartIn && x.Plate == PlateKind.PartArmour && c.Components[x.Index].Kind == ComponentKind.ConningTower,
            PlatePart.Belt => outer && (x.Plate == PlateKind.SideArmour && c.SidePlates[x.Index].Kind is StrakeKind.Belt or StrakeKind.Zone
                || x.Plate == PlateKind.Inclined && c.Inclined[x.Index].Kind is InclinedKind.Belt or InclinedKind.Zone),
            _ => false,
        };
    }

    /// <summary>Halfway up the part of a plate from lo to hi over the waterline wl (halfway up it all if none is).</summary>
    static double Over(double lo, double hi, double wl) => hi > wl ? (Math.Max(lo, wl) + hi) / 2 : (lo + hi) / 2;

    /// <summary>Whether a component is a barbette of her main battery (the secondaries' are their own matter).</summary>
    static bool MainBarbette(ShipClass c, int component) =>
        c.Components[component].Kind == ComponentKind.Barbette && c.MountOf(component) is int m and >= 0 && c.Mounts[m].Kind == ComponentKind.Main;

    /// <summary>Whether she has any of this target.</summary>
    public bool Has(ZoneTarget t) => boxes[(int)t].Length > 0;

    public ref readonly ZoneCell this[ZoneTarget t, int range, int angle] => ref cells[Index(t, range, angle)];

    public int Index(ZoneTarget t, int range, int angle) => ((int)t * RangesM.Length + range) * Angles.Length + angle;

    /// <summary>The plate that let the most shells of a cell through (a plate target), or into the vitals through the side
    /// (or the deck); null for none.</summary>
    public ZonePlate? Most(ZoneTarget t, int range, int angle, bool deck = false)
    {
        var d = (deck ? deckPlates : sidePlates)[Index(t, range, angle)];
        if (d == null || d.Count == 0)
            return null;
        return d.MaxBy(kv => (kv.Value, -(int)kv.Key.Kind, -kv.Key.Index)).Key;
    }

    /// <summary>Fill the targets' cells up to this many shells, on up to threads threads; stops early (leaving the cells as
    /// they are, each whole) if cancelled.</summary>
    public void Fill(int shotsPerCell, ReadOnlySpan<ZoneTarget> targets, int threads = 1, CancellationToken cancel = default)
    {
        var todo = new List<int>();
        int per = RangesM.Length * Angles.Length;
        foreach (var t in targets)
            for (int k = 0; k < per; k++)
                if (cells[(int)t * per + k].Tried < shotsPerCell)
                    todo.Add((int)t * per + k);
        Run(todo, shotsPerCell, threads, cancel);
        foreach (var t in targets)
            this.shotsPerCell[(int)t] = Math.Max(this.shotsPerCell[(int)t], shotsPerCell);
    }

    /// <summary>More shells where it matters (the user, 2026-11-14: "toss bit more on the areas where it matters more"): the
    /// cells near an edge, their share between <see cref="Unsure"/> and 1 - it, or beside a cell on the other side of half
    /// (the next range or angle), filled up to this many shells.</summary>
    public void Refine(int shotsPerCell, ReadOnlySpan<ZoneTarget> targets, int threads = 1, CancellationToken cancel = default)
    {
        var todo = new List<int>();
        int n = RangesM.Length, na = Angles.Length;
        foreach (var t in targets)
            for (int r = 0; r < n; r++)
                for (int a = 0; a < na; a++)
                {
                    ref readonly var c = ref this[t, r, a];
                    if (c.Tried >= shotsPerCell || c.Shots == 0)
                        continue;
                    double sh = Share(t, c);
                    bool edge = sh > Unsure && sh < 1 - Unsure;
                    bool side = Share(t, this[t, r, a]) >= Edge;
                    if (r > 0 && this[t, r - 1, a].Shots > 0 && Share(t, this[t, r - 1, a]) >= Edge != side ||
                        r + 1 < n && this[t, r + 1, a].Shots > 0 && Share(t, this[t, r + 1, a]) >= Edge != side ||
                        a > 0 && this[t, r, a - 1].Shots > 0 && Share(t, this[t, r, a - 1]) >= Edge != side ||
                        a + 1 < na && this[t, r, a + 1].Shots > 0 && Share(t, this[t, r, a + 1]) >= Edge != side)
                        edge = true;
                    if (edge)
                        todo.Add(Index(t, r, a));
                }
        Run(todo, shotsPerCell, threads, cancel);
    }

    /// <summary>A share under this (or over one less it) is sure enough; between, a cell is refined.</summary>
    public const double Unsure = 0.1;

    /// <summary>The share a cell's colour goes by: through a plate, or into the vitals either way.</summary>
    static double Share(ZoneTarget t, in ZoneCell c) => IsVitals(t) ? Math.Max(c.SideShare, c.DeckShare) : c.ThroughShare;

    void Run(List<int> todo, int to, int threads, CancellationToken cancel)
    {
        if (todo.Count == 0)
            return;
        var opts = new ParallelOptions { MaxDegreeOfParallelism = Math.Max(1, threads), CancellationToken = cancel };
        try
        {
            Parallel.For(0, todo.Count, opts, () => new Walker(Class), (j, _, w) =>
            {
                FillCell(todo[j], to, w);
                return w;
            }, _ => { });
        }
        catch (OperationCanceledException)
        {
        }
    }

    /// <summary>Every target, filled alike.</summary>
    public void Fill(int shotsPerCell, int threads = 1, CancellationToken cancel = default) => Fill(shotsPerCell, All, threads, cancel);

    /// <summary>A thread's own tracer path, bender and intact condition.</summary>
    sealed class Walker
    {
        public readonly HitPath Path = new(), Probe = new();
        public readonly PathBender Bender = new();
        public readonly ShipCondition Intact = new();
        public readonly List<(int Box, double U0, double U1, double V0, double V1, double Upto)> Rects = [];

        public Walker(ShipClass c)
        {
            Intact.Reset(c);
            Bender.Reserve(c.Solids.Solids.Length);
        }
    }

    void FillCell(int i, int to, Walker w)
    {
        int ti = i / (RangesM.Length * Angles.Length), range = i / Angles.Length % RangesM.Length, angle = i % Angles.Length;
        var target = (ZoneTarget)ti;
        ref var cell = ref cells[i];
        int from = cell.Tried;
        cell.Tried = to;
        var tbox = boxes[ti];
        if (tbox.Length == 0 || double.IsNaN(Falls[range].FallDeg))
            return;
        var c = Class;
        var (fall, strike) = Falls[range];
        var dir = HitQuery.Flight(Angles[angle], fall);
        // two axes across the line of flight, and each of the target's boxes' outline on them (as a rectangle): a line is
        // drawn by picking a box by the area of its rectangle, then a point within it, kept if it meets the box and then
        // one time in as many of the target's boxes as it meets, so the lines fall evenly over the target as the shell
        // sees it
        var u = Normalize(Cross(dir, new P3(0, 0, 1)));
        if (Dot(u, u) < 0.5)
            u = new P3(0, 1, 0);
        var vv = Cross(u, dir);
        var rects = w.Rects;
        rects.Clear();
        double total = 0;
        for (int k = 0; k < tbox.Length; k++)
        {
            var bx = tbox[k];
            double u0 = double.MaxValue, u1 = double.MinValue, v0 = double.MaxValue, v1 = double.MinValue;
            for (int corner = 0; corner < 8; corner++)
            {
                var p = new P3((corner & 1) == 0 ? bx.X0 : bx.X1, (corner & 2) == 0 ? bx.Y0 : bx.Y1, (corner & 4) == 0 ? bx.Base : bx.Top);
                double a = Dot(p, u), e = Dot(p, vv);
                (u0, u1, v0, v1) = (Math.Min(u0, a), Math.Max(u1, a), Math.Min(v0, e), Math.Max(v1, e));
            }
            total += (u1 - u0) * (v1 - v0);
            rects.Add((k, u0, u1, v0, v1, total));
        }
        if (total <= 0 && aims[ti].Length == 0)
            return;
        double far = 2 * c.Solids.Radius + 50, reach = WaterRun.ReachM(Shell, strike, settings);
        var bounds = c.Solids.Bounds;
        var water = Waterplane.Level(c);
        // the same shells at every range of an angle (their aim across the outline, the plate's lot, the walk's rolls): a
        // shell gets through out to some range and not beyond, so the share falls with range as the physics does, not
        // by the cells' own draws
        ulong key = (ulong)(ti * Angles.Length + angle);
        var stream = RandomStream.Named(seed, "immune zone").Derive(key);
        var lotStream = RandomStream.Named(seed, "immune zone lots").Derive(key);
        for (int shot = from; shot < to; shot++)
        {
            // a cell none of whose first shells could meet the target is hidden from there: it's out of its sight
            if (shot >= HiddenAfter && cell.Shots == 0)
                return;
            var rng = stream.Derive((ulong)shot);
            // a line through the target's outline that, unstopped, meets it, and its first meeting with her
            int first = -1;
            double run = 0;
            var aim = aims[ti];
            for (int draw = 0; draw < (aim.Length > 0 ? 1 : DrawsPerShot); draw++)
            {
                cell.Drawn++;
                if (aim.Length > 0)
                {
                    // a plate: its shells down the lines through its aim points, in turn
                    var o0 = Add(aim[shot % aim.Length], dir, -far);
                    if (!Slab(o0, dir, bounds, out double b0, out double b1))
                        break;
                    double w0 = dir.Z < 0 ? (o0.Z - water.Z) / -dir.Z : double.PositiveInfinity;
                    cell.Traced++;
                    ShipTracer.Trace(c, Add(o0, dir, Math.Max(0, Math.Min(b0, w0) - 2)), Add(o0, dir, b1 + 2), bearings, water, w.Probe);
                    if (TargetAt(w.Probe, target, dir) < 0 || (first = HitQuery.FirstIn(w.Probe)) < 0)
                    {
                        first = -1;
                        break;
                    }
                    run = WaterRun.RunM(w.Probe, first);
                    if (run > reach)
                        first = -1;   // the sea took it
                    break;
                }
                double pick = rng.NextDouble() * total;
                int lo = 0, hi = rects.Count - 1;
                while (lo < hi)
                {
                    int mid = (lo + hi) / 2;
                    if (rects[mid].Upto < pick)
                        lo = mid + 1;
                    else
                        hi = mid;
                }
                var q = rects[lo];
                double a = rng.NextDouble(q.U0, q.U1), e = rng.NextDouble(q.V0, q.V1);
                var at = new P3(u.X * a + vv.X * e, u.Y * a + vv.Y * e, u.Z * a + vv.Z * e);
                var from0 = Add(at, dir, -far);
                // sure misses before the trace: the line misses the box (it can't meet what's in it), or meets the sea
                // farther out from her than the shell runs under it
                if (!Slab(from0, dir, tbox[q.Box], out _, out _) || !Slab(from0, dir, bounds, out double tb, out double te))
                    continue;
                // a line through m of the boxes could have come from any of them: kept one time in m
                int m = 0;
                foreach (var o in rects)
                    m += Slab(from0, dir, tbox[o.Box], out _, out _) ? 1 : 0;
                if (m > 1 && rng.NextDouble() * m >= 1)
                    continue;
                double tw = dir.Z < 0 ? (from0.Z - water.Z) / -dir.Z : double.PositiveInfinity;
                if (tb - tw > reach)
                    continue;
                cell.Traced++;
                // the line from just before it meets the sea or her bounds to just past them
                ShipTracer.Trace(c, Add(from0, dir, Math.Max(0, Math.Min(tb, tw) - 2)), Add(from0, dir, te + 2), bearings, water, w.Probe);
                if (TargetAt(w.Probe, target, dir) < 0)
                    continue;
                first = HitQuery.FirstIn(w.Probe);
                if (first < 0)
                    continue;
                run = WaterRun.RunM(w.Probe, first);
                if (run > reach)
                {
                    first = -1;   // the sea took it
                    continue;
                }
                break;
            }
            if (first < 0)
            {
                cell.Unseen++;
                continue;
            }
            var point = w.Probe.Crossings[first].Point;
            var hit = new Hit
            {
                Point = point, Direction = dir, SpeedMs = strike, WaterRunM = run, Source = HitSource.Shell, Shell = Shell, ShellType = -1,
            };
            // the hit's own path, as the damage system traces it: from just outside the point along the flight; a line that
            // met nothing before her (no sea) is that path already
            var path = w.Probe;
            if (first > 0)
            {
                Slab(point, dir, bounds, out _, out double exit);
                ShipTracer.Trace(c, new P3(point.X - dir.X, point.Y - dir.Y, point.Z - dir.Z), Add(point, dir, exit + 2), bearings, water, w.Path);
                path = w.Path;
            }
            w.Bender.Set(bearings, bearings.Length, water);
            var r = rng.Derive(1);
            var t = Terminal.Resolve(c, hit, path, w.Bender, lotStream.Derive((ulong)shot), settings, ref r, w.Intact);
            cell.Shots++;
            var xs = path.Crossings;
            int into = TargetAt(path, target, dir);
            double tin = into >= 0 ? xs[into].T : double.NaN;
            Each?.Invoke(i, path, t, tin);
            // whole up to: where it broke, burst or stopped
            double whole = t.Outcome is Outcome.BrokeUp or Outcome.Holed && t.Plate >= 0 ? xs[t.Plate].T
                : t.Bursts && !double.IsNaN(t.BurstT) ? Math.Min(t.BurstT, t.StopT) : t.StopT;
            if (!IsVitals(target))
            {
                // a plate: through it whole and not glanced off it (a path that no longer meets it stopped short of it)
                if (into < 0 || tin >= whole || tin >= t.StopT || t.Glance == into)
                    continue;
                cell.Through++;
                Tally(sidePlates, i, xs[into]);
                continue;
            }
            if (into < 0 || tin >= t.StopT)
                continue;
            if (tin >= whole)
            {
                cell.Broken++;
                continue;
            }
            // the heaviest plate it beat on the way in
            int heavy = -1;
            for (int k = 0; k < into; k++)
                if (xs[k].Plate is not (PlateKind.None or PlateKind.Contents or PlateKind.PartContents) && xs[k].Mm > 0 &&
                    (heavy < 0 || xs[k].Mm > xs[heavy].Mm))
                    heavy = k;
            bool deck = heavy >= 0 && Lies(xs[heavy].Normal);
            if (deck)
                cell.Deck++;
            else
                cell.Side++;
            if (heavy >= 0)
                Tally(deck ? deckPlates : sidePlates, i, xs[heavy]);
        }
    }

    static void Tally(Dictionary<ZonePlate, int>[] plates, int i, in Crossing x)
    {
        var key = new ZonePlate(x.Plate, x.Index, Math.Round(x.Mm));
        var d = plates[i] ??= [];
        d[key] = d.GetValueOrDefault(key) + 1;
    }

    /// <summary>The crossing at which a path first meets the target from outside, along d: a plate of it struck on its outer
    /// face (the belt from outboard, the deck from above; a barbette where it stands clear of the hull), or a space of the
    /// vitals (the plates at that point come before it); -1 for never.</summary>
    int TargetAt(HitPath path, ZoneTarget target, P3 d)
    {
        var c = Class;
        var xs = path.Crossings;
        bool inHull = false;
        for (int k = 0; k < xs.Length; k++)
        {
            ref readonly var x = ref xs[k];
            if (x.Kind == CrossingKind.HullIn)
                inHull = true;
            else if (x.Kind == CrossingKind.HullOut)
                inHull = false;
            bool outer = Dot(d, x.Normal) < 0;
            bool met = target switch
            {
                ZoneTarget.Belt => outer && (x.Plate == PlateKind.SideArmour && c.SidePlates[x.Index].Kind is StrakeKind.Belt or StrakeKind.Zone
                    || x.Plate == PlateKind.Inclined && c.Inclined[x.Index].Kind is InclinedKind.Belt or InclinedKind.Zone),
                ZoneTarget.Barbettes => x.Plate == PlateKind.Barbette && x.Kind == CrossingKind.PartIn && !inHull && MainBarbette(c, x.Index),
                ZoneTarget.Deck => outer && (x.Plate == PlateKind.ArmourDeck && mainDeck[x.Index]
                    || x.Plate == PlateKind.Inclined && c.Inclined[x.Index].Kind == InclinedKind.Slope),
                ZoneTarget.Plate => PlateMet(x, outer, inHull),
                _ => x.Kind == CrossingKind.CellIn && x.Index >= 0 && vital[(int)target - 3][x.Index],
            };
            if (met)
                return k;
        }
        return -1;
    }

    /// <summary>A target's edges at an angle, m, each between the ranges its share crosses half at. The belt and the
    /// barbettes: out to which half the shells at them get through (0 if they never do), and the reach. The deck: 0, and
    /// from which half get through (the reach if they never do). The vitals: out to which half get in through the side,
    /// and from which half get in through the deck. Immune between them if the inner is the nearer.</summary>
    public (double InnerM, double OuterM) Edges(ZoneTarget t, int angle)
    {
        int n = RangesM.Length;
        double inner = 0, outer = ReachM;
        bool vitals = IsVitals(t);
        double SideOf(int k) => vitals ? this[t, k, angle].SideShare : this[t, k, angle].ThroughShare;
        double DeckOf(int k) => vitals ? this[t, k, angle].DeckShare : this[t, k, angle].ThroughShare;
        if (!LiesFlat(t))
            for (int k = n - 1; k >= 0; k--)
                if (this[t, k, angle].Shots > 0 && SideOf(k) >= Edge)
                {
                    inner = k + 1 < n && this[t, k + 1, angle].Shots > 0 ? Cross(k, k + 1, SideOf(k), SideOf(k + 1)) : RangesM[k];
                    break;
                }
        if (LiesFlat(t) || vitals)
            for (int k = 0; k < n; k++)
                if (this[t, k, angle].Shots > 0 && DeckOf(k) >= Edge)
                {
                    outer = k > 0 && this[t, k - 1, angle].Shots > 0 ? Cross(k - 1, k, DeckOf(k - 1), DeckOf(k)) : RangesM[k];
                    break;
                }
        return (inner, outer);

        double Cross(int a, int b, double sa, double sb) => sa == sb ? RangesM[a] : RangesM[a] + (RangesM[b] - RangesM[a]) * (Edge - sa) / (sb - sa);
    }

    /// <summary>The armour's zone at an angle (the classic one): from where the belt and the barbettes hold to where the deck
    /// gives.</summary>
    public (double InnerM, double OuterM) ArmourEdges(int angle)
    {
        double belt = Has(ZoneTarget.Belt) ? Edges(ZoneTarget.Belt, angle).InnerM : 0;
        double barbettes = Has(ZoneTarget.Barbettes) ? Edges(ZoneTarget.Barbettes, angle).InnerM : 0;
        double deck = Has(ZoneTarget.Deck) ? Edges(ZoneTarget.Deck, angle).OuterM : ReachM;
        return (Math.Max(belt, barbettes), deck);
    }

    /// <summary>Where a line from o along d enters a box and leaves it (t along it), if it meets it at all.</summary>
    static bool Slab(P3 o, P3 d, in Box b, out double t0, out double t1)
    {
        double lo = double.NegativeInfinity, hi = double.PositiveInfinity;
        Axis(o.X, d.X, b.X0, b.X1);
        Axis(o.Y, d.Y, b.Y0, b.Y1);
        Axis(o.Z, d.Z, b.Base, b.Top);
        (t0, t1) = (lo, hi);
        return lo <= hi && hi >= 0;

        void Axis(double p, double dd, double a, double bb)
        {
            if (Math.Abs(dd) < 1e-12)
            {
                if (p < a || p > bb)
                    (lo, hi) = (1, 0);
                return;
            }
            double ta = (a - p) / dd, tc = (bb - p) / dd;
            if (ta > tc)
                (ta, tc) = (tc, ta);
            (lo, hi) = (Math.Max(lo, ta), Math.Min(hi, tc));
        }
    }

    /// <summary>Whether a plate with this normal lies as a deck (within 45° of level) rather than as the side.</summary>
    static bool Lies(P3 normal) => Math.Abs(normal.Z) > Math.Sqrt(0.5);

    static P3 Add(P3 a, P3 d, double t) => new(a.X + d.X * t, a.Y + d.Y * t, a.Z + d.Z * t);
    static double Dot(P3 a, P3 b) => a.X * b.X + a.Y * b.Y + a.Z * b.Z;
    static P3 Cross(P3 a, P3 b) => new(a.Y * b.Z - a.Z * b.Y, a.Z * b.X - a.X * b.Z, a.X * b.Y - a.Y * b.X);

    static P3 Normalize(P3 a)
    {
        double l = Math.Sqrt(Dot(a, a));
        return l > 1e-12 ? new P3(a.X / l, a.Y / l, a.Z / l) : default;
    }
}
