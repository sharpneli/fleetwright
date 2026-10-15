namespace Fleetwright.Shipgen.Tools;

/// <summary>mono-check: sweeps the designer's trim levers (the Overview tab's) a few steps either way of each design's
/// value and builds every step. Adding weight (more belt, a knot more, two more AA mounts) should never give a lighter
/// ship, and a step should move the displacement about as much as its neighbours do. A reversal is a step the wrong way;
/// a lump is a step more than Lump times its sweep's median step and over LumpFrac of the ship. Physics may still cause
/// either (a longer hull for speed needs less power); the sizer should add neither.</summary>
public static class Monotone
{
    public const double Lump = 3.0, LumpFrac = 0.01;

    /// <summary>A lever: its name, how far one step goes, its value on a design (null: doesn't apply) and the design with
    /// a new value, and which way it moves the weight (+1: more is heavier).</summary>
    public sealed record Lever(string Name, double Step, Func<Design, double?> Get, Func<Design, double, Design> Set, int Sense = 1,
        double Lo = 0.0);

    public sealed record Step(double Value, long StdT, double LengthM, double BeamM, bool Valid);

    /// <summary>One lever's sweep on a design: the steps built (in value order), and the reversals and lumps as indices of
    /// the step after the jump.</summary>
    public sealed record Sweep(string Design, string Lever, int Sense, List<Step> Steps, List<int> Reversals, List<int> Lumps);

    static Design Arm(Design d, Func<ArmourInput, ArmourInput> f) => d with { Armour = f(d.Armour ?? new ArmourInput()) };

    static int MainDeck(Design d)
    {
        var decks = d.Armour?.Decks;
        if (decks is not { Count: > 0 })
            return -1;
        int best = 0;
        for (int i = 1; i < decks.Count; i++)
            if ((decks[i].Mm ?? 0) > (decks[best].Mm ?? 0))
                best = i;
        return best;
    }

    static double? EndMean(Design d)
    {
        var eb = d.Armour?.EndBelts;
        var mms = new[] { eb?.Fore?.Mm, eb?.Aft?.Mm }.Where(m => m is > 0).Select(m => m!.Value).ToList();
        return mms.Count > 0 ? mms.Average() : null;
    }

    static double? MainMean(Design d, Func<BatteryInput, double?> f)
    {
        var vs = d.Main?.Select(f).Where(v => v is not null).Select(v => v!.Value).ToList();
        return vs is { Count: > 0 } ? vs.Average() : null;
    }

    static Design MainAll(Design d, Func<BatteryInput, double?> get, Func<BatteryInput, double, BatteryInput> set, double v)
    {
        double mean = MainMean(d, get)!.Value;
        return d with { Main = d.Main!.Select(b => get(b) is { } x ? set(b, Math.Max(0, x + v - mean)) : b).ToList() };
    }

    /// <summary>The Overview's levers (DesignerScene.Overview BaseLevers), the armour deck as the thickest deck.</summary>
    public static readonly Lever[] Levers =
    [
        new("speed", 0.5, d => d.SpeedKn, (d, v) => d with { SpeedKn = v }, Lo: 8),
        new("range", 500, d => d.RangeNm ?? Navarch.DefaultRangeNm, (d, v) => d with { RangeNm = v }, Lo: 500),
        new("stress", 0.1, d => d.Machinery?.Stress, (d, v) => d with { Machinery = d.Machinery! with { Stress = Math.Min(1, v) } }, -1),
        new("belt", 10, d => d.Armour?.BeltMm, (d, v) => Arm(d, a => a with { BeltMm = v })),
        new("upper_belt", 10, d => d.Armour?.UpperBelt?.Mm, (d, v) => Arm(d, a => a with { UpperBelt = a.UpperBelt! with { Mm = v } })),
        new("armour_deck", 5, d => MainDeck(d) is int i and >= 0 ? d.Armour!.Decks![i].Mm : null,
            (d, v) => Arm(d, a => a with { Decks = a.Decks!.Select((x, k) => k == MainDeck(d) ? x with { Mm = v } : x).ToList() })),
        new("bulkheads", 10, d => d.Armour?.BulkheadMm, (d, v) => Arm(d, a => a with { BulkheadMm = v })),
        new("end_belts", 10, EndMean, (d, v) =>
        {
            double dv = v - EndMean(d)!.Value;
            EndBeltInput? E(EndBeltInput? b) => b is { Mm: > 0 } ? b with { Mm = Math.Max(0, b.Mm!.Value + dv), TipMm = b.TipMm is { } t ? Math.Max(0, t + dv) : null } : b;
            return Arm(d, a => a with { EndBelts = a.EndBelts! with { Fore = E(a.EndBelts.Fore), Aft = E(a.EndBelts.Aft) } });
        }),
        new("turret_faces", 10, d => MainMean(d, b => b.ArmourMm), (d, v) => MainAll(d, b => b.ArmourMm, (b, x) => b with { ArmourMm = x }, v)),
        new("main_calibre", 10, d => MainMean(d, b => b.CalibreMm), (d, v) => MainAll(d, b => b.CalibreMm, (b, x) => b with { CalibreMm = x }, v), Lo: 50),
        new("main_rounds", 10, d => MainMean(d, b => b.RoundsPerGun), (d, v) => MainAll(d, b => b.RoundsPerGun, (b, x) => b with { RoundsPerGun = x }, v), Lo: 10),
        new("sec_per_side", 1, d => d.Secondary is { Count: > 0 } s && s[0].PerSide is { } p ? p : null,
            (d, v) => d with { Secondary = d.Secondary!.Select((s, k) => k == 0 ? s with { PerSide = (int)v } : s).ToList() }),
        new("aa_light", 2, d => d.Aa?.Light ?? 0, (d, v) => d with { Aa = (d.Aa ?? new AaInput()) with { Light = (int)v } }),
        new("aa_heavy", 1, d => d.Aa?.Heavy ?? 0, (d, v) => d with { Aa = (d.Aa ?? new AaInput()) with { Heavy = (int)v } }),
        new("freeboard", 0.05, d => d.Hull?.Freeboard ?? 1.0, (d, v) => d with { Hull = (d.Hull ?? new HullInput()) with { Freeboard = v } }, Lo: 0.3),
        new("tower", 1, d => d.Superstructure?.TowerLevels, (d, v) => d with { Superstructure = d.Superstructure! with { TowerLevels = (int)v } }, Lo: 2),
        new("endurance", 5, d => d.Crew?.EnduranceDays, (d, v) => d with { Crew = d.Crew! with { EnduranceDays = v } }, Lo: 5),
    ];

    /// <summary>Sweeps every lever that applies to the design, `steps` steps either way of its value (inputs the
    /// validator refuses are left out).</summary>
    public static List<Sweep> Check(string name, Design design, int steps, IEnumerable<Lever>? levers = null)
    {
        var result = new List<Sweep>();
        foreach (var lv in levers ?? Levers)
        {
            if (lv.Get(design) is not { } v0)
                continue;
            var built = new List<Step>();
            for (int k = -steps; k <= steps; k++)
            {
                double v = Math.Round(v0 + k * lv.Step, 6);
                if (v < lv.Lo)
                    continue;
                Design d;
                try
                {
                    d = lv.Set(design, v);
                }
                catch (NullReferenceException)
                {
                    break;   // the lever needs a part the design doesn't have
                }
                if (ShipDesign.Validate(d).Count > 0)
                    continue;
                var r = ShipDesign.Build(d).Report;
                built.Add(new Step(v, r.Results.StandardDisplacementT, r.Results.LengthM, r.Results.BeamM, r.Valid));
            }
            if (built.Count < 2)
                continue;
            var deltas = Enumerable.Range(1, built.Count - 1).Select(i => (double)(built[i].StdT - built[i - 1].StdT)).ToList();
            var abs = deltas.Select(Math.Abs).Order().ToList();
            double median = abs[abs.Count / 2];
            var rev = new List<int>();
            var lumps = new List<int>();
            for (int i = 0; i < deltas.Count; i++)
            {
                if (!built[i].Valid || !built[i + 1].Valid)
                    continue;
                if (lv.Sense != 0 && deltas[i] * lv.Sense < -0.5)
                    rev.Add(i + 1);
                if (Math.Abs(deltas[i]) > Lump * Math.Max(median, 1.0) && Math.Abs(deltas[i]) > LumpFrac * built[i].StdT)
                    lumps.Add(i + 1);
            }
            result.Add(new Sweep(name, lv.Name, lv.Sense, built, rev, lumps));
        }
        return result;
    }
}
