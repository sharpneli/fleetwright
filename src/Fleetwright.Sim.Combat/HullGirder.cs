using Fleetwright.Shipgen;
using Fleetwright.Sim.Core;
using Fleetwright.Sim.Ships;

namespace Fleetwright.Sim.Combat;

/// <summary>What a section has lost and what bears on it (<see cref="HullGirder.Read"/>): the worst section, its place along
/// her (0 stern, 1 bow), the share of its plastic modulus gone, what's left of its capacity and the moment on it (both in
/// design moments) and their ratio (1 or more: broken); the shares of its deck, side, bottom and longitudinal plate area gone,
/// and the whip in design moments. All of her body; the ends apart.</summary>
public struct GirderReading
{
    public int Section;
    public double Station, D, Capacity, Moment, Load, Deck, Side, Bottom, Longitudinal, Whip;
    /// <summary>The worst load within an end of her still on (1 or more: that end comes off), and where.</summary>
    public double EndLoad, EndStation;
    /// <summary>This reading took an end off her (<see cref="HullGirder.Bear"/>).</summary>
    public bool EndOff;
    /// <summary>Broken: she can't carry what's on her.</summary>
    public readonly bool Broken => Load >= 1;
}

/// <summary>The hull as a beam (docs/battle/explosions.md EX3; research 07 §3.1): each section's elements are the boundaries
/// themselves, the weather deck and bottom over and under its cells, the sides, the decks and longitudinal bulkheads between
/// them, so what an explosion ruptures comes out of the section with nothing kept apart. Its capacity is the steel's yield
/// (<see cref="ShipClass.GirderStressMpa"/> × the design's safety factor) on what's left of its plastic modulus; the moment
/// is shipgen's design moment shared out as still water and waves, plus what the flooding adds, plus the whip. One per class,
/// shared by every ship and world of it (<see cref="Of"/>), read only; what a ship has lost is on her condition, and reads
/// allocate nothing.</summary>
public sealed class HullGirder
{
    static readonly System.Runtime.CompilerServices.ConditionalWeakTable<ShipClass, HullGirder> Table = new();

    /// <summary>The class's girder.</summary>
    public static HullGirder Of(ShipClass c) => Table.GetValue(c, static x => new HullGirder(x));

    enum Kind : byte { Deck, Side, Bottom, Longitudinal }

    readonly record struct Element(double AreaM2, double Z, int Edge, int Face, Kind Kind);

    /// <summary>Steel's Young's modulus, Pa.</summary>
    const double SteelE = 2.1e11;

    readonly ShipClass c;
    readonly Element[] elements;
    readonly int[] start;
    readonly double[] intact, station;
    readonly double x0, x1;

    /// <summary>The design moment, N·m (shipgen's sizing), the steel's yield, Pa, and the second moment amidships intact, m⁴.</summary>
    public double DesignNm { get; }
    public double YieldPa { get; }
    public double MidIM4 { get; }

    public HullGirder(ShipClass cls)
    {
        c = cls;
        int sections = c.Sections.Length;
        start = new int[sections + 1];
        (intact, station) = (new double[sections], new double[sections]);
        (x0, x1) = (double.PositiveInfinity, double.NegativeInfinity);
        foreach (ref readonly var cell in c.Cells)
            (x0, x1) = (Math.Min(x0, cell.Box.X0), Math.Max(x1, cell.Box.X1));
        var list = new List<Element>();
        for (int sec = 0; sec < sections; sec++)
        {
            start[sec] = list.Count;
            for (int i = 0; i < c.Cells.Length; i++)
                if (c.Cells[i].Section == sec)
                    Add(list, i, sec);
            Inclined(list, sec);
            list.Sort(start[sec], list.Count - start[sec], Comparer<Element>.Create(static (a, b) => a.Z.CompareTo(b.Z)));   // the plastic axis's walk
            station[sec] = x1 > x0 ? Math.Clamp(((c.Sections[sec].X0 + c.Sections[sec].X1) / 2 - x0) / (x1 - x0), 0, 1) : 0.5;
        }
        start[sections] = list.Count;
        elements = list.ToArray();
        DesignNm = HullWeight.DesignMomentKnM(c.Hydrostatics.DisplacementT, c.Length) * 1000;
        YieldPa = c.GirderStressMpa * HullWeight.SafetyFactor * 1e6;
        double midI = 0, midD = double.MaxValue;
        for (int sec = 0; sec < sections; sec++)
        {
            intact[sec] = Modulus(sec, default, default, out double i);
            if (Math.Abs(station[sec] - 0.5) < midD)
                (midD, midI) = (Math.Abs(station[sec] - 0.5), i);
        }
        MidIM4 = midI;
        // her two-node vertical mode: a free-free beam of her section amidships carrying her virtual mass (Kumai's)
        virtualKg = c.Hydrostatics.DisplacementT * 1000 * (1.2 + c.Beam / (3 * Math.Max(c.Vertical.Draught, 0.1)));
        double l = Math.Max(c.Length, 1);
        OmegaRad = MidIM4 > 0 ? ModeBL * ModeBL * Math.Sqrt(SteelE * MidIM4 / (virtualKg / l * l * l * l * l)) : 0;
    }

    /// <summary>Her two-node vertical frequency, rad/s: a free-free beam of her section amidships carrying her virtual mass,
    /// Δ·(1.2 + B/3T) (Kumai; research 13 §4.2): about 1.2 Hz a destroyer, 1.0 a battlecruiser, 0.5 Yamato. A uniform beam reads
    /// 10-30% high against a real hull [INFERRED there].</summary>
    public double OmegaRad { get; }

    /// <summary>Her two-node period, s: how long a whip takes to spend itself, and what an explosion's energy must come within to whip her.</summary>
    public double PeriodS => OmegaRad > 0 ? 2 * Math.PI / OmegaRad : 1;

    readonly double virtualKg;

    /// <summary>The free-free beam's first root, βL.</summary>
    const double ModeBL = 4.7300408;

    /// <summary>The two-node mode's shape at <see cref="ModeN"/> stations from end to end, normalised to RMS 1 over the length, and its
    /// curvature amidships, |φ''|/(βL)² on the same norm (research 13's reference, §4).</summary>
    const int ModeN = 161;
    static readonly double[] Mode = new double[ModeN];
    static readonly double ModeCurvMid;

    static HullGirder()
    {
        double sig = (Math.Cosh(ModeBL) - Math.Cos(ModeBL)) / (Math.Sinh(ModeBL) - Math.Sin(ModeBL)), sq = 0;
        for (int i = 0; i < ModeN; i++)
        {
            double a = ModeBL * i / (ModeN - 1);
            Mode[i] = Math.Cosh(a) + Math.Cos(a) - sig * (Math.Sinh(a) + Math.Sin(a));
            sq += Mode[i] * Mode[i];
        }
        double norm = Math.Sqrt(sq / ModeN), mid = ModeBL / 2;
        for (int i = 0; i < ModeN; i++)
            Mode[i] /= norm;
        ModeCurvMid = Math.Abs(Math.Cosh(mid) - Math.Cos(mid) - sig * (Math.Sinh(mid) - Math.Sin(mid))) / norm;
    }

    /// <summary>A bubble's drive on her two-node mode over one cycle, by the hull's frequency times the bubble's period f₂·T, every
    /// eighth from 0 to 4: 2π·f₂T·g(f₂T), g the bubble's volume history at her frequency (research 13 §4.3; its reference's
    /// Rayleigh-Plesset bubble sized to Cole's R_max, the same for any charge and depth). Full: a bubble that collapses; Vented: one
    /// that breaks the surface at its first expansion, a step with no collapse (its history stops there, its gas gone to the air).</summary>
    static readonly double[] CycleFull =
    [
        0.000, 0.469, 0.901, 1.262, 1.526, 1.676, 1.704, 1.616, 1.426, 1.159, 0.844, 0.512, 0.194, 0.082, 0.296, 0.435, 0.496,
        0.484, 0.410, 0.293, 0.152, 0.009, 0.117, 0.213, 0.269, 0.282, 0.254, 0.194, 0.113, 0.024, 0.061, 0.129, 0.173,
    ];
    static readonly double[] CycleVented =
    [
        1.000, 0.993, 0.979, 0.958, 0.928, 0.891, 0.847, 0.797, 0.742, 0.683, 0.622, 0.559, 0.495, 0.433, 0.373, 0.317, 0.265,
        0.221, 0.184, 0.158, 0.142, 0.136, 0.136, 0.140, 0.143, 0.145, 0.145, 0.142, 0.137, 0.129, 0.119, 0.108, 0.098,
    ];

    /// <summary>A bubble's whip on her, design moments amidships (research 13 §4.3's one-cycle law; docs/battle/explosions.md EX5):
    /// its pressure on her flat of bottom, ρ·V̈/4πr less the surface's image, over her two-node mode, M = E·I·κ·ρ·V_max·h(f₂T)·Infl/(4π·M*).
    /// The bubble is Cole's (<see cref="SimSettings.BubbleRadiusK"/>, <see cref="SimSettings.BubblePeriodK"/>); one that reaches the
    /// surface vents at its first expansion, and one that doesn't pulses again with <see cref="SimSettings.BubbleSecondShare"/> of its
    /// energy, the two cycles adding as energies. A bubble that meets her hull vents into what it opened and its pulse is spent there,
    /// a wreck, not a whip (Shubrick, Uganda, Roma): none with its distance from her nearest plate under <see cref="SimSettings.BubbleGamma"/>'s
    /// first R_max, whole past its second. at: the charge, ship frame; gapM: its distance from her nearest wetted plate.</summary>
    public double Bubble(double wKg, P3 at, double gapM, in Waterplane water, SimSettings s)
    {
        double depth = water.At(at.X, at.Y) - at.Z;
        if (!(wKg > 0) || !(OmegaRad > 0) || !(depth > 0))
            return 0;
        double z = depth + s.BubbleHeadM, size = Math.Cbrt(wKg);
        double rMax = s.BubbleRadiusK * size / Math.Cbrt(z), t = s.BubblePeriodK * size / Math.Pow(z, 5.0 / 6);
        double lo = s.BubbleGamma[0], hi = s.BubbleGamma[1], share = Math.Clamp((gapM / rMax - lo) / Math.Max(hi - lo, 1e-9), 0, 1);
        if (!(share > 0))
            return 0;
        double infl = Influence(at, depth, water) * s.BubbleWallK;
        if (rMax >= depth)
            return share * Cycle(rMax, t, CycleVented, infl);
        double first = Cycle(rMax, t, CycleFull, infl), k = Math.Cbrt(s.BubbleSecondShare), second = Cycle(rMax * k, t * k, CycleFull, infl);
        return share * Math.Sqrt(first * first + second * second);
    }

    /// <summary>One cycle's moment, design moments: a bubble of R_max and period t on an influence.</summary>
    double Cycle(double rMax, double t, double[] table, double infl)
    {
        double x = OmegaRad * t / (2 * Math.PI) * 8, h = x >= table.Length - 1 ? table[^1] : table[(int)x] + (x - (int)x) * (table[(int)x + 1] - table[(int)x]);
        double l = Math.Max(c.Length, 1), vMax = 4.0 / 3 * Math.PI * rMax * rMax * rMax;
        return SteelE * MidIM4 * ModeCurvMid * (ModeBL / l) * (ModeBL / l) * FloodModel.SeaT * 1000 * vMax * h * infl / (4 * Math.PI * virtualKg) / DesignNm;
    }

    /// <summary>How much of a source's pressure lands on her mode, m: Σ φ(x)·(1/r − 1/r′)·dA over her flat of bottom (her beam, at her
    /// keel), r′ from the surface's image.</summary>
    double Influence(P3 at, double depth, in Waterplane water)
    {
        const int strips = 5;
        double l = Math.Max(c.Length, 1), mid = (x0 + x1) / 2, dx = l / (ModeN - 1), dy = c.Beam / strips;
        double draught = Math.Max(water.At(at.X, 0) - c.Vertical.Keel, 0), below = depth - draught, image = depth + draught, sum = 0;
        for (int i = 0; i < ModeN; i++)
        {
            double ax = mid - l / 2 + i * dx - at.X, row = 0;
            for (int j = 0; j < strips; j++)
            {
                double ay = (-0.5 + (j + 0.5) / strips) * c.Beam - at.Y, flat = ax * ax + ay * ay;
                row += 1 / Math.Max(Math.Sqrt(flat + below * below), 0.5) - 1 / Math.Sqrt(flat + image * image);
            }
            sum += Mode[i] * row;
        }
        return Math.Abs(sum) * dx * dy;
    }

    /// <summary>A cell's share of its section: its outside faces, and the decks and longitudinal bulkheads it shares with
    /// cells of its section, once each. Plate alone on the sides (a belt is hung on them, and carries no longitudinal load,
    /// as shipgen's sizing has it); an armour deck in a deck, as shipgen counts it.</summary>
    void Add(List<Element> list, int i, int sec)
    {
        ref readonly var cell = ref c.Cells[i];
        var b = cell.Box;
        var hull = HullFaces.Of(c);
        for (int f = 0; f < 4; f++)
        {
            int x = i * 4 + f;
            double fa = hull.Area[x];
            if (!(fa > 0))
                continue;
            if (f < 2)
                list.Add(new Element(fa / Math.Max(b.X1 - b.X0, 1e-6) * Math.Max(hull.HeldMm[x], 0) / 1000, (b.Top + b.Base) / 2, -1, x, Kind.Side));
            else if (f == 2)
                list.Add(new Element((b.Y1 - b.Y0) * hull.Mm[x] / 1000, b.Top, -1, x, Kind.Deck));
            else
                list.Add(new Element(fa / Math.Max(b.X1 - b.X0, 1e-6) * hull.Mm[x] / 1000, hull.Point[x].Z, -1, x, Kind.Bottom));   // where her bottom is, as wide as it's there
        }
        var nb = cell.Neighbours;
        for (int j = nb.Start; j < nb.End; j++)
        {
            ref readonly var e = ref c.Neighbour(j);
            if (e.Join == JoinKind.Open || e.Cell < i || c.Cells[e.Cell].Section != sec)
                continue;
            var o = c.Cells[e.Cell].Box;
            double mm = Blast.JoinMm(c, e, b, o);
            int q = Leaks.Entry(c, i, j);
            if (e.Join == JoinKind.Deck)
            {
                double w = Math.Max(Math.Min(b.Y1, o.Y1) - Math.Max(b.Y0, o.Y0), 0);
                list.Add(new Element(w * mm / 1000, o.Base >= b.Top - 0.05 ? b.Top : b.Base, q, -1, Kind.Deck));
            }
            else
            {
                double h = Math.Max(Math.Min(b.Top, o.Top) - Math.Max(b.Base, o.Base), 0);
                list.Add(new Element(h * mm / 1000, (Math.Min(b.Top, o.Top) + Math.Max(b.Base, o.Base)) / 2, q, -1, Kind.Longitudinal));
            }
        }
    }

    /// <summary>The inclined plates through a section (a deck's slopes: protection-plan.md H6), each side, as shipgen counts them.</summary>
    void Inclined(List<Element> list, int sec)
    {
        double xm = (c.Sections[sec].X0 + c.Sections[sec].X1) / 2;
        foreach (ref readonly var r in c.Inclined)
        {
            if (xm < r.X0 || xm > r.X1)
                continue;
            double dy = Math.Abs(r.U1 - r.U0), dz = Math.Abs(r.Z1 - r.Z0), run = Math.Sqrt(dy * dy + dz * dz);
            list.Add(new Element(2 * run * c.MaterialOf(r.Material).SteelMm(r.ThicknessMm) / 1000, (r.Z0 + r.Z1) / 2, -1, -1, Kind.Longitudinal));
        }
    }

    /// <summary>What's left of a section, as its ultimate moment reads it: the plastic modulus, m³, Σ A·|z - z_p| about the
    /// height that parts her area in two (a girder collapses when yield has spread through the section, not when its outermost
    /// plate first yields, so a sliver of keel far from the axis doesn't set it, and losing a plate can't raise it), and its
    /// elastic second moment for the whip. <see cref="BucklingK"/> takes off what the compressed side buckles.</summary>
    double Modulus(int sec, ReadOnlySpan<double> edgeLost, ReadOnlySpan<double> faceLost, out double i)
    {
        double a = 0, az = 0;
        for (int n = start[sec]; n < start[sec + 1]; n++)
        {
            ref readonly var e = ref elements[n];
            double left = e.AreaM2 * (1 - Lost(e, edgeLost, faceLost));
            if (!(left > 0))
                continue;
            (a, az) = (a + left, az + left * e.Z);
        }
        i = 0;
        if (!(a > 0))
            return 0;
        // the plastic axis: the height with half her area above it, walking up the section
        double zp = elements[start[sec]].Z, under = 0;
        for (int n = start[sec]; n < start[sec + 1]; n++)
        {
            ref readonly var e = ref elements[n];
            under += e.AreaM2 * (1 - Lost(e, edgeLost, faceLost));
            if (under * 2 >= a)
            {
                zp = e.Z;
                break;
            }
        }
        double na = az / a, plastic = 0;
        for (int n = start[sec]; n < start[sec + 1]; n++)
        {
            ref readonly var e = ref elements[n];
            double left = e.AreaM2 * (1 - Lost(e, edgeLost, faceLost));
            plastic += left * Math.Abs(e.Z - zp);
            i += left * (e.Z - na) * (e.Z - na);
        }
        return BucklingK * plastic;
    }

    /// <summary>The ultimate moment's share of the fully plastic one: what buckling on the compressed side takes off [EST: a stiffened
    /// hull's ultimate is about its first-yield moment, 0.8-0.9 of plastic].</summary>
    const double BucklingK = 0.85;

    /// <summary>The share of an element gone (an inclined plate, with no boundary of its own, never is).</summary>
    static double Lost(in Element e, ReadOnlySpan<double> edgeLost, ReadOnlySpan<double> faceLost) =>
        e.Edge >= 0 ? (e.Edge < edgeLost.Length ? edgeLost[e.Edge] : 0) : e.Face >= 0 && e.Face < faceLost.Length ? faceLost[e.Face] : 0;

    /// <summary>The moment what an explosion drives puts on her, design moments. The hull takes an impulse J = √(2·E·m) from
    /// m kg driven with E joules (gas here; the water, for a bubble under her), and the energy that reaches her two-node
    /// bending is J²/2 over her own mass, so E·m/M; of that a share, k: what's vertical and in phase [EST]. Then the bending
    /// energy M²L/2EI gives the moment. energyMass: the sum of E·m over what came within her period.</summary>
    public double Whip(double energyMass, double k) =>
        energyMass > 0 ? Math.Sqrt(2 * k * energyMass / (c.Hydrostatics.DisplacementT * 1000) * SteelE * MidIM4 / Math.Max(c.Length, 1)) / DesignNm : 0;

    /// <summary>The flooding's moment by section, design moments, hogging positive: the water's weight against the buoyancy
    /// that carries it, parallel sinkage and trim over a prismatic waterplane, summed from the bow. An end that's come off carries
    /// nothing across its cut.</summary>
    void Flooding(ShipCondition? k, Span<double> moment)
    {
        moment.Clear();
        if (k is null || !(k.WaterT > 0))
            return;
        var sections = c.Sections;
        double l = Math.Max(x1 - x0, 1), total = 0, about = 0, mid = (x0 + x1) / 2;
        Span<double> weight = sections.Length <= 256 ? stackalloc double[sections.Length] : new double[sections.Length];
        weight.Clear();
        for (int i = 0; i < c.Cells.Length; i++)
        {
            double t = k.WaterM3[i] * FloodModel.SeaT;
            if (t <= 0)
                continue;
            int sec = c.Cells[i].Section;
            double xm = (sections[sec].X0 + sections[sec].X1) / 2;
            if (xm > k.CutBowX || xm < k.CutSternX)
                continue;
            weight[sec] += t;
            (total, about) = (total + t, about + t * (xm - mid));
        }
        double v = 0, m = 0, xPrev = double.NaN;
        for (int sec = sections.Length - 1; sec >= 0; sec--)
        {
            double xm = (sections[sec].X0 + sections[sec].X1) / 2, len = sections[sec].X1 - sections[sec].X0;
            if (!double.IsNaN(xPrev))
                m += v * Math.Abs(xPrev - xm);
            moment[sec] = m * 9.81 * 1000 / DesignNm;
            double buoyancy = (total / l + 12 * about * (xm - mid) / (l * l * l)) * len;
            v += weight[sec] - buoyancy;
            xPrev = xm;
        }
    }

    /// <summary>A bench's look along her: each section's place, its elements, its area, top and bottom, and its modulus intact.</summary>
    public IEnumerable<(double Station, int Elements, double AreaM2, double Top, double Bottom, double ZM3)> Profile()
    {
        for (int sec = 0; sec < c.Sections.Length; sec++)
        {
            double a = 0, top = double.NegativeInfinity, bottom = double.PositiveInfinity;
            for (int n = start[sec]; n < start[sec + 1]; n++)
                (a, top, bottom) = (a + elements[n].AreaM2, Math.Max(top, elements[n].Z), Math.Min(bottom, elements[n].Z));
            yield return (station[sec], start[sec + 1] - start[sec], a, top, bottom, intact[sec]);
        }
    }

    /// <summary>A bench's look into a section: its elements, by kind, area and height.</summary>
    public IEnumerable<(string Kind, double AreaM2, double Z, double Mm)> Elements(double at)
    {
        int sec = 0;
        for (int i = 0; i < station.Length; i++)
            if (Math.Abs(station[i] - at) < Math.Abs(station[sec] - at))
                sec = i;
        for (int n = start[sec]; n < start[sec + 1]; n++)
            yield return (elements[n].Kind.ToString(), elements[n].AreaM2, elements[n].Z, 0);
    }

    /// <summary>A hull's standing moment and capacity with what's gone gone: her worst section. sea 1: a seaway's waves.
    /// Still water and the waves are <see cref="SimSettings.GirderMoment"/>'s shares of the design moment. A bench's and a
    /// test's, on what it says is lost; a ship's goes by <see cref="Read(ShipCondition, double, double, SimSettings)"/>.</summary>
    public GirderReading Read(ReadOnlySpan<double> edgeLost, ReadOnlySpan<double> faceLost, ShipCondition? k, double sea, double whip, SimSettings s)
    {
        int sections = c.Sections.Length;
        Span<double> z = sections <= 256 ? stackalloc double[sections] : new double[sections];
        for (int sec = 0; sec < sections; sec++)
            z[sec] = Modulus(sec, edgeLost, faceLost, out _);
        return Reading(z, edgeLost, faceLost, k, sea, whip, s);
    }

    /// <summary>A ship's reading: what she has lost and the water in her, a sea and a whip.</summary>
    public GirderReading Read(ShipCondition k, double sea, double whip, SimSettings s)
    {
        if (k.GirderStale)
        {
            for (int sec = 0; sec < k.GirderZ.Length; sec++)
                k.GirderZ[sec] = Modulus(sec, k.EdgeLost, k.FaceLost, out _);
            k.GirderStale = false;
        }
        return Reading(k.GirderZ, k.EdgeLost, k.FaceLost, k, sea, whip, s);
    }

    GirderReading Reading(ReadOnlySpan<double> z, ReadOnlySpan<double> edgeLost, ReadOnlySpan<double> faceLost, ShipCondition? k, double sea, double whip, SimSettings s)
    {
        int sections = c.Sections.Length;
        Span<double> flood = sections <= 256 ? stackalloc double[sections] : new double[sections];
        Flooding(k, flood);
        double still = s.GirderMoment[0], waves = s.GirderMoment[1];
        var worst = new GirderReading { Section = -1, Load = -1, Whip = whip };
        double end = 0, endAt = 0;
        for (int sec = 0; sec < sections; sec++)
        {
            double u = station[sec];
            // a free beam: no shear at its ends, so the moment rises from them as x²: sin², not 07's rough sin, which would have
            // the tip of her carrying a third of her moment
            double bend = Math.Sin(Math.PI * u) * Math.Sin(Math.PI * u);
            double moment = Math.Abs(still * bend + flood[sec]) + (waves * Math.Clamp(sea, 0, 1.5) + whip) * bend;
            double capacity = YieldPa * z[sec] / DesignNm;
            double load = capacity > 0 ? moment / capacity : double.PositiveInfinity;
            // near an end what breaks takes the end off and she may live (07 §3.2: New Orleans); read apart from her body
            if (Math.Min(u, 1 - u) <= s.GirderEndShare)
            {
                bool gone = k is not null && (u > 0.5 ? double.IsFinite(k.CutBowX) : double.IsFinite(k.CutSternX));
                if (!gone && load > end)
                    (end, endAt) = (load, u);
                continue;
            }
            if (load <= worst.Load)
                continue;
            worst = new GirderReading { Section = sec, Station = u, D = intact[sec] > 0 ? 1 - z[sec] / intact[sec] : 0, Capacity = capacity, Moment = moment, Load = load, Whip = whip };
        }
        if (worst.Section >= 0)
            Shares(ref worst, edgeLost, faceLost);
        (worst.EndLoad, worst.EndStation) = (end, endAt);
        return worst;
    }

    /// <summary>What's on her now (docs/battle/explosions.md: after every explosion and as she floods): an explosion's whip, its gas's
    /// (energyMass, <see cref="Whip"/>) and its bubble's (design moments, <see cref="Bubble"/>), joins what's left of those in the last
    /// moments (each spends itself in her period; several out of phase add as energies), and her worst section is read in this sea.
    /// An end that can't carry its load comes off and she may live: everything past the cut open to the sea and what stands there
    /// gone (07 §3.2). Whether she's broken is the reading's to say.</summary>
    public GirderReading Bear(ShipCondition k, double energyMass, long now, double sea, SimSettings s, double bubble = 0)
    {
        double gas = Whip(energyMass, s.GirderWhipK);
        k.WhipSq = k.WhipSq * Math.Exp(-Math.Max(now - k.WhipMs, 0) / (1000 * PeriodS)) + gas * gas + bubble * bubble;
        k.WhipMs = now;
        var r = Read(k, sea, Math.Sqrt(k.WhipSq), s);
        if (r.EndLoad >= 1 && !r.Broken)
        {
            TakeOff(k, r.EndStation, s);
            r.EndOff = true;
        }
        return r;
    }

    void TakeOff(ShipCondition k, double at, SimSettings s)
    {
        bool bow = at > 0.5;
        double cut = x0 + at * (x1 - x0);
        if (bow)
            k.CutBowX = cut;
        else
            k.CutSternX = cut;
        for (int i = 0; i < c.Cells.Length; i++)
        {
            var b = c.Cells[i].Box;
            if (bow ? b.X1 < cut : b.X0 > cut)
                continue;
            k.Hole(i, s.RupturedShare * (b.Y1 - b.Y0) * (b.Top - b.Base), new P3((b.X0 + b.X1) / 2, (b.Y0 + b.Y1) / 2, b.Base));
        }
        for (int j = 0; j < c.Components.Length; j++)
        {
            var b = c.Components[j].Bounds;
            double xm = (b.X0 + b.X1) / 2;
            if (bow ? xm > cut : xm < cut)
                k.Worsen(j, PartState.Destroyed);
        }
    }

    void Shares(ref GirderReading r, ReadOnlySpan<double> edgeLost, ReadOnlySpan<double> faceLost)
    {
        Span<double> all = stackalloc double[4], gone = stackalloc double[4];
        all.Clear();
        gone.Clear();
        for (int n = start[r.Section]; n < start[r.Section + 1]; n++)
        {
            ref readonly var e = ref elements[n];
            all[(int)e.Kind] += e.AreaM2;
            gone[(int)e.Kind] += e.AreaM2 * Lost(e, edgeLost, faceLost);
        }
        double Share(int i, Span<double> a, Span<double> b) => a[i] > 0 ? b[i] / a[i] : 0;
        (r.Deck, r.Side, r.Bottom, r.Longitudinal) = (Share(0, all, gone), Share(1, all, gone), Share(2, all, gone), Share(3, all, gone));
    }
}
