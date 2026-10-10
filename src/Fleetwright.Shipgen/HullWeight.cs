using System.Text.Json.Serialization;

namespace Fleetwright.Shipgen;

/// <summary>The hull's construction standard: its steel's yield, how much joining adds, and how heavy its plate runs
/// against the norm.</summary>
public sealed record Construction(string Name, double YieldMpa, double JoinFactor, double Standard);

/// <summary>hull.plating with its defaults: shell plate beyond the structure's own, its material, and deck planking.</summary>
public sealed record PlatingSpec(double ShellMm, string? Material, double DeckWoodMm);

/// <summary>The hull structure's weight and girder. The plate model fills every field; a box-model hull (planing craft)
/// only T, ShellT and PlateOwnMm.</summary>
public sealed record HullStructure(double T, double ShellT)
{
    public double? MinGaugeT { get; init; }
    public double? StrengthT { get; init; }
    public double? TMinMm { get; init; }
    public double? TStrMm { get; init; }
    public double? StressMpa { get; init; }
    public double? IReqM4 { get; init; }
    public double? IArmourM4 { get; init; }
    public double? IPlatingM4 { get; init; }
    /// <summary>The girder's depth when a strength deck stands above the main deck.</summary>
    public double? DepthM { get; init; }
    /// <summary>A box-model hull's own plate.</summary>
    public double? PlateOwnMm { get; init; }

    public bool PlateModel => TMinMm != null;
}

/// <summary>The plating the game's damage model sees: mm of each kind of plate, unarmoured.</summary>
public sealed record HullPlates(
    [property: JsonIgnore(Condition = JsonIgnoreCondition.Never)] string? Material,
    double ShellMm, double ShellEndMm, double StrengthDeckMm, double StrengthDeckEndMm, double MidX0, double MidX1,
    double DeckMm, double BulkheadMm, double InnerBottomMm, double SuperstructureMm, double ControlMm, double DeckWoodMm);

/// <summary>Hull structure weight, by plate area x thickness (research/hull-weight-model.md, "Tier 2").</summary>
public static class HullWeight
{
    public const double Rho = 7.85e-3, RhoWood = 0.7e-3;
    const double KS = 2.5, CM = 40.0, FFit = 0.10, SF = 2.1, SigCap = 185.0;
    static readonly (double A, double B) TMin = (4.0, 0.03);
    const double Long = 350.0;
    const double ShellSide = 0.90, ShellBottom = 0.95, IntDeck = 0.85, IntDeckT = 0.60;
    const double Bulkheads = 1 + 1 / 0.06, BhdArea = 0.75 * 0.80, BhdT = 0.70, DbArea = 0.80 * 1.60;
    const double GirderTaper = 0.75, NeutralAxis = 0.45, ArmDeckWidth = 0.85;
    public const double SupPlateK = 0.8;

    /// <summary>hull.construction with its defaults filled in.</summary>
    public static Construction ConstructionOf(Design design)
    {
        var c = design.Hull?.Construction;
        return new Construction(c?.Name ?? "High-tensile steel, all welded", c?.YieldMpa ?? 350, c?.JoinFactor ?? 1.00,
            c?.Standard ?? 1.0);
    }

    /// <summary>hull.plating with its defaults filled in.</summary>
    public static PlatingSpec PlatingOf(Design design)
    {
        var p = design.Hull?.Plating;
        return new PlatingSpec(p?.ShellMm ?? 0.0, p?.Material, p?.DeckWoodMm ?? 0.0);
    }

    /// <summary>The minimum gauge, mm: the thinnest plate the hull is built of.</summary>
    public static double TMinMm(double L, Construction c) => (TMin.A + TMin.B * Math.Min(L, Long)) * c.Standard;

    /// <summary>Plate thicker than the structure's own, t: plain steel plate over the area.</summary>
    public static double ExtraPlateT(double areaM2, double mm, double ownMm) => Rho * areaM2 * Math.Max(0.0, mm - ownMm);

    /// <summary>The plating the game's damage model sees, from the structure h: mm of each kind of plate, unarmoured.
    /// ownMm: the hull's own gauge where the structure has no plate model.</summary>
    public static HullPlates Plates(HullStructure h, double ownMm, double L, double shellMm, string? material, double supMm,
        double controlMm, double deckWoodMm = 0.0)
    {
        double tMin = h.TMinMm ?? ownMm;
        double tStr = h.TStrMm ?? 0.0;
        static double R(double v) => Math.Round(v, 1);
        double ownSup = SupPlateK * tMin;
        return new HullPlates(material, R(Math.Max(tMin, Math.Max(tStr, shellMm))), R(Math.Max(tMin, shellMm)),
            R(Math.Max(tMin, tStr)), R(tMin), R(-GirderTaper * L / 2), R(GirderTaper * L / 2), R(IntDeckT * tMin),
            R(BhdT * tMin), R(tMin), R(Math.Max(ownSup, supMm)), R(Math.Max(ownSup, Math.Max(supMm, controlMm))), R(deckWoodMm));
    }

    public static double AllowableStress(Construction c) => Math.Min(c.YieldMpa / SF, SigCap);

    /// <summary>The strength deck's area, as the model takes it.</summary>
    public static double DeckArea(double L, double B, double cb) => B * L * (0.66 + 0.33 * cb);

    /// <summary>What the model weighs per m^2 of strength deck at minimum gauge.</summary>
    public static double DeckTPerM2(double L, Construction c) =>
        Rho * KS * (TMin.A + TMin.B * Math.Min(L, Long)) * c.Standard * (1 + FFit) * c.JoinFactor;

    /// <summary>A raised stretch of hull (forecastle, poop) at minimum gauge.</summary>
    public static double RaisedT(double L, Construction c, double deckM2, double sideM2, double endM2, double shellMm = 0.0) =>
        DeckTPerM2(L, c) * (deckM2 + ShellSide * sideM2 + BhdT * endM2)
        + ExtraPlateT(ShellSide * sideM2, shellMm, TMinMm(L, c)) * c.JoinFactor;

    /// <summary>The hull structure: its weight, what it is made of and its girder.</summary>
    /// <param name="topside">the side above the widest point: the strength deck and the armour decks are its breadth at
    /// their heights (null: wall-sided).</param>
    public static HullStructure WeightOf(double L, double B, double D, double cb, double full, Construction c, double nInt,
        double doubleBottom, IReadOnlyList<(double Mm, double Z)> armourDecks, double? bulkheadDepth = null,
        double? girderDepth = null, double shellMm = 0.0, double armouredSideM2 = 0.0, Topside? topside = null)
    {
        double deckD = bulkheadDepth ?? D;   // the main deck's height (D is the girder's top when a strength deck stands above it)
        double Breadth(double z) => topside is null ? B : B * topside.MeanRatio(L, z - deckD);
        double bDeck = Breadth(D);
        double aShell = 2 * ShellSide * D * L + ShellBottom * B * L * Math.Sqrt(cb);
        double aDeck = DeckArea(L, bDeck, cb);
        double aInt = nInt * IntDeck * aDeck;
        double aBhd = Bulkheads * BhdArea * B * (bulkheadDepth ?? D);
        double aDb = doubleBottom * B * L * cb * DbArea;
        double tMin = TMinMm(L, c);
        double sig = AllowableStress(c);
        double m = full * 9.81 * L / CM * Math.Pow(Math.Min(1.0, Long / L), 2);
        double G = girderDepth ?? D;
        double iReq = m / (sig * 1000) * (G / 2);
        double iArm = armourDecks.Sum(a => ArmDeckWidth * Breadth(a.Z) * a.Mm / 1000 * Math.Pow(a.Z - NeutralAxis * G, 2));
        double zPerMm = G * (bDeck + G / 3) / 1000;
        double tStr = Math.Max(0.0, iReq - iArm) / (G / 2) / zPerMm;
        double wMin = Rho * KS * tMin * (aShell + aDeck + aInt * IntDeckT + aBhd * BhdT + aDb);
        double wStr = Rho * GirderTaper * (aShell + aDeck) * Math.Max(0.0, tStr - tMin);
        double k = (1 + FFit) * c.JoinFactor;
        double aSide = Math.Max(0.0, 2 * ShellSide * D * L - armouredSideM2);
        double wShell = (ExtraPlateT((1 - GirderTaper) * aSide, shellMm, tMin)
                         + ExtraPlateT(GirderTaper * aSide, shellMm, Math.Max(tMin, tStr))) * c.JoinFactor;
        return new HullStructure((wMin + wStr) * k + wShell, wShell)
        {
            MinGaugeT = wMin * k, StrengthT = wStr * k, TMinMm = tMin, TStrMm = tStr, StressMpa = sig, IReqM4 = iReq,
            IArmourM4 = iArm, IPlatingM4 = Math.Max(tStr, tMin) * zPerMm * G / 2,
        };
    }

    const double StackDeck = 0.6;
    static readonly (double Lo, double Hi) InnerBottomT = (4000.0, 10000.0);
    const double GirderMid = 0.2;

    /// <summary>The mean height of raised stretches of hull over the midbody, |x| &lt;= GirderMid L.</summary>
    public static double RaisedGirderH(IReadOnlyList<RaisedStretch> raised, double L)
    {
        double a = -GirderMid * L, b = GirderMid * L;
        double tot = 0.0;
        foreach (var s in raised)
            tot += Math.Max(0.0, Math.Min(b, s.X1) - Math.Max(a, s.X0)) * s.Levels * Geometry.DeckPitch;
        return tot / (b - a);
    }

    /// <summary>The hull's structure weight and girder. arm: the armour's geometry; above: the style's strength deck
    /// above the main deck, or null; raised: the layout's raised stretches.</summary>
    public static HullStructure HullStructure(Design design, double L, double B, double cb, double D, double full, ArmourLayout arm,
        StrengthDeck? above, IReadOnlyList<RaisedStretch> raised, Topside? topside = null)
    {
        double nInt = StackDeck * Math.Max(0.0, (D - Powerplant.DoubleBottom(D) - Decks.MinTier) / Geometry.DeckPitch);
        var (lo, hi) = InnerBottomT;
        double inner = Math.Min(1.0, Math.Max(0.0, (full - lo) / (hi - lo)));
        var plates = arm.Decks.Where(d => d.X0 <= 0.0 && 0.0 <= d.X1).Select(d => (d.Mm, d.Z)).ToList();
        double depth = D;
        if (above != null)
        {
            depth = D + above.H;
            nInt += above.Decks;
            plates.AddRange(above.Plates);
        }
        double rh = raised.Count > 0 ? RaisedGirderH(raised, L) : 0.0;
        double sideArm = arm.Strakes.Where(s => s.Kind != "box").Sum(s => (s.X1 - s.X0) * (s.Top - s.Bottom));
        if (arm.BeltMm > 0)
            sideArm += (arm.X1 - arm.X0) * (arm.BeltTop - arm.BeltBottom);
        var h = WeightOf(L, B, depth, cb, full, ConstructionOf(design), nInt, inner, plates, bulkheadDepth: D,
            girderDepth: rh != 0 ? depth + rh : null, shellMm: PlatingOf(design).ShellMm,
            armouredSideM2: 2 * Math.Max(0.0, sideArm), topside: topside);
        return h with { DepthM = depth };
    }

    /// <summary>A box-model hull (planing craft) has no plate model: structure = hull_k (L B D)^hull_exp plus the shell's
    /// extra plating; its own gauge is the style's (PlateOwnMm).</summary>
    public static HullStructure BoxStructure(Design design, double L, double B, double D, Tuning tun)
    {
        double plank = tun.PlateOwnMm;
        double shellT = ExtraPlateT(2 * ShellSide * D * L, PlatingOf(design).ShellMm, plank);
        return new HullStructure(tun.HullK * Math.Pow(L * B * D, tun.HullExp) + shellT, shellT) { PlateOwnMm = plank };
    }

    /// <summary>Warnings on a solved hull's structure.</summary>
    public static List<string> StructureChecks(HullStructure h)
    {
        var result = new List<string>();
        if ((h.StrengthT ?? 0.0) > (h.MinGaugeT ?? double.PositiveInfinity))
            result.Add($"The hull is very long for its depth: {h.StrengthT:N0} t of its plating (strength " +
                     $"deck and shell {h.TStrMm:F0} mm, where {h.TMinMm:F0} mm would do) only " +
                     "keeps it from breaking in two. A shorter hull or an armour deck high in it would help.");
        return result;
    }

    public static List<string> Validate(Design design)
    {
        var errs = new List<string>();
        if (design.Hull?.Freeboard is double f && !(f > 0))
            errs.Add("hull.freeboard: a factor above 0 on the style's standard freeboard (1.0)");
        if (design.Hull?.Construction is { } c)
        {
            foreach (var (k, v) in new[] { ("yield_mpa", c.YieldMpa), ("join_factor", c.JoinFactor), ("standard", c.Standard) })
                if (v is double x && !(x > 0))
                    errs.Add($"hull.construction.{k}: must be a number above 0");
        }
        if (design.Hull?.Plating is { } p)
        {
            errs.AddRange(p.Extra.KeysOrEmpty().Select(k => $"hull.plating.{k}: not a plating setting (shell_mm, material, deck_wood_mm)"));
            if (p.ShellMm < 0)
                errs.Add("hull.plating.shell_mm: a number, 0 or more (0: the structure's own gauge)");
            if (p.DeckWoodMm < 0)
                errs.Add("hull.plating.deck_wood_mm: a number, 0 or more (0: a bare steel deck)");
            if (p.Material is "")
                errs.Add("hull.plating.material: name the material as a string");
        }
        return errs;
    }
}

/// <summary>What a ship carries that burns or explodes, stowed low in the hull: the magazines for its guns'
/// ammunition, and a carrier's aviation ordnance and fuel.</summary>
public static class Ordnance
{
    public const double TPerM3 = 0.6;
    public const int Tiers = 2;
    public const double MinRoom = 1.5;
    const double ReadyK = 17000.0, ReadyP = 1.4, WarheadK = 2.0e-6;
    const double TorpedoMm = 533;

    /// <summary>(base, top) above the main deck of a room standing on the inner bottom, top on a deck of the stack
    /// (decks: heights above the keel, the main deck first) and never above the roof.</summary>
    public static (double Base, double Top) Span(IReadOnlyList<double> decks, double innerBottom, double roof, double? needH = null,
        int tiers = Tiers)
    {
        double D = decks[0], ib = innerBottom;
        using var _ = Scratch<double>.Rent(out var ups);
        for (int i = 0; i < decks.Count; i++)
            if (ib + 1e-6 < decks[i] && decks[i] <= roof + 1e-6)
                ups.Add(decks[i]);
        ups.Sort();
        if (ups.Count == 0)
            ups.Add(roof);
        double top;
        if (needH is null)
            top = ups[Math.Min(tiers, ups.Count) - 1];
        else
        {
            double nh = needH.Value;
            top = ups[^1];
            foreach (var z in ups)
                if (z >= ib + nh - 1e-6)
                {
                    top = z;
                    break;
                }
        }
        return (ib - D, top - D);
    }

    public static (double Base, double Top) Span(PlantPlan plan, double? needH = null, int tiers = Tiers) =>
        Span(plan.Decks, plan.InnerBottom, plan.Top, needH, tiers);

    /// <summary>The height of a room given `tiers` deck spaces.</summary>
    public static double Height(PlantPlan plan, int tiers = Tiers)
    {
        var (b, t) = Span(plan, tiers: tiers);
        return t - b;
    }

    public static double AmmoT(TurretType t) => Batteries.MountWeights(t, 0.0, 0.0, 0).Ammo;

    /// <summary>Ready-use ammunition at a gun mount at action stations: (rounds, tonnes).</summary>
    public static (long N, double T) ReadyUse(double calibreMm, double barrels, double? cap = null)
    {
        double n = barrels * Math.Max(1L, (long)Math.Round(ReadyK * Math.Pow(calibreMm, -ReadyP)));
        if (cap is double c)
            n = Math.Min(n, c);
        return ((long)n, n * Batteries.ShellK * Math.Pow(calibreMm, 3) / 1000.0 * Batteries.AmmoMult);
    }

    public static double WarheadKg(double diameterMm = TorpedoMm) => WarheadK * Math.Pow(diameterMm, 3);

    public static double AmmoM3(TurretType t) => AmmoT(t) / TPerM3;

    /// <summary>The ids of the mounts that carry ammunition (guns, not torpedo tubes).</summary>
    public static List<string> Guns(IEnumerable<Mount> mounts) => mounts.Where(m => m.Kind is "main" or "secondary").Select(m => m.Id).ToList();

    /// <summary>The magazine volume the mounts' booked ammunition ("Magazine &lt;id&gt;" weights) needs.</summary>
    public static double BookedM3(Layout lay, IEnumerable<string> mids)
    {
        var names = new HashSet<string>(mids.Select(m => $"Magazine {m}"), StringComparer.Ordinal);
        return lay.Weights.Where(w => names.Contains(w.Name)).Select(w => w.W).Sum() / TPerM3;
    }

    /// <summary>The length a zone `width` across needs for volume_m3 at `tiers` deck spaces tall.</summary>
    public static double ZoneLength(double volumeM3, double width, PlantPlan plan, int tiers = Tiers, double least = MinRoom) =>
        Math.Max(least, volumeM3 / Math.Max(width * Height(plan, tiers), 1.0));

    /// <summary>Stow the ordnance in zones. Adds the compartments, sets each mount's Magazine, and moves its
    /// "Magazine &lt;id&gt;" weight to its room. Returns {room id: (x0, x1, base, top)}.</summary>
    public static Dictionary<string, (double X0, double X1, double Base, double Top)> Stow(Layout lay, IReadOnlyList<Mount> mounts,
        IEnumerable<Zone> zones)
    {
        var plan = lay.Geo.Plant!;
        var byId = new Dictionary<string, Mount>(StringComparer.Ordinal);
        foreach (var m in mounts)
            byId[m.Id] = m;
        var ammo = new Dictionary<string, Weight>(StringComparer.Ordinal);
        foreach (var w in lay.Weights)
            if (w.Name.StartsWith("Magazine ", StringComparison.Ordinal))
                ammo[w.Name] = w;
        var result = new Dictionary<string, (double, double, double, double)>(StringComparer.Ordinal);
        foreach (var z in zones)
        {
            var rooms = new List<(ZoneRoom R, List<Mount> Ms, double T, double V)>();
            foreach (var r in z.Rooms)
            {
                var ms = (r.Mounts ?? []).Where(mid => ammo.ContainsKey($"Magazine {mid}")).Select(mid => byId[mid]).ToList();
                double t = r.Tonnes + ms.Select(m => ammo[$"Magazine {m.Id}"].W).Sum();
                rooms.Add((r, ms, t, t / (r.TPerM3 ?? TPerM3)));
            }
            double vol = rooms.Select(r => r.V).Sum();
            if (vol <= 0)
                continue;
            double L = z.X1 - z.X0;
            var (bse, top) = Span(plan, vol / Math.Max(1.0, L * 2 * z.HalfWidth));
            double x = z.X1;
            foreach (var (r, ms, t, v) in rooms)
            {
                if (v <= 0)
                    continue;
                double l = L * v / vol;
                var c = new Compartment
                {
                    Id = r.Id, Kind = r.Kind ?? "magazine", X0 = x - l, X1 = x, HalfWidth = z.HalfWidth, Base = bse, Top = top,
                    Tonnes = Math.Round(t, 1),
                };
                if (ms.Count == 1 && z.Own)
                    c.Mount = ms[0].Id;
                else if (ms.Count > 0)
                    c.Mounts = ms.Select(m => m.Id).ToList();
                lay.Compartments.Add(c);
                foreach (var m in ms)
                {
                    m.Magazine = r.Id;
                    var w = ammo[$"Magazine {m.Id}"];
                    w.X = x - l / 2;
                    w.ZRel = ZRel.Deck((bse + top) / 2);
                }
                result[r.Id] = (x - l, x, bse, top);
                x -= l;
            }
        }
        return result;
    }

    /// <summary>A zone for one mount's own magazine: under it on the centreline, its diameter long.</summary>
    public static Zone OwnZone(Mount m, double innerHw)
    {
        double r = m.T.R;
        return new Zone(m.X - r, m.X + r, Math.Min(r, innerHw), [new ZoneRoom($"Magazine {m.Id}", [m.Id])], Own: true);
    }
}
