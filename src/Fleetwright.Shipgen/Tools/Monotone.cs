namespace Fleetwright.Shipgen.Tools;

/// <summary>mono-check: sweeps the designer's trim levers (the Overview tab's, Levers.Trim) a few steps either way of each
/// design's value, stepped as the Overview's arrows step them, and builds every step. Adding weight (more belt, a knot
/// more, two more AA mounts) should never give a lighter ship, and a step should move the displacement about as much as
/// its neighbours do. A reversal is a step the wrong way; a lump is a step more than Lump times its sweep's median step
/// and over LumpFrac of the ship. Physics may still cause either (a longer hull for speed needs less power); the sizer
/// should add neither.</summary>
public static class Monotone
{
    public const double Lump = 3.0, LumpFrac = 0.01;

    public sealed record Step(double Value, long StdT, double LengthM, double BeamM, bool Valid);

    /// <summary>One lever's sweep on a design: the steps built (in value order), and the reversals and lumps as indices of
    /// the step after the jump.</summary>
    public sealed record Sweep(string Design, string Lever, int Sense, List<Step> Steps, List<int> Reversals, List<int> Lumps);

    /// <summary>Sweeps every lever that applies to the design (the Overview's, unless given), `steps` steps either way of
    /// its value; a step the range clamps onto one already built, or the validator refuses, is left out.</summary>
    public static List<Sweep> Check(string name, Design design, int steps, IEnumerable<NumberLever>? levers = null)
    {
        var result = new List<Sweep>();
        Ship? ship = null;   // the design as it is: the levers' Auto values read it
        foreach (var lv in levers ?? Levers.Trim(design).Select(t => t.Lever))
        {
            ship ??= ShipDesign.Build(design);
            if (!lv.AppliesTo(design) || lv.Value(design, ship) is not { } v0)
                continue;
            var (lo, hi) = lv.Range(design);
            var built = new List<Step>();
            var seen = new HashSet<string>(StringComparer.Ordinal);
            for (int k = -steps; k <= steps; k++)
            {
                var d = k == 0 ? design : lv.Stepped(design, ship, k);
                if (d == null || !seen.Add(d.ToJson()) || ShipDesign.Validate(d).Count > 0)
                    continue;
                var r = (k == 0 ? ship : ShipDesign.Build(d)).Report;
                double v = k == 0 ? v0 : Math.Round(Math.Clamp(lv.Target(design, v0, k, lv.Step), lo, hi), 6);
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
            result.Add(new Sweep(name, lv.Id, lv.Sense, built, rev, lumps));
        }
        return result;
    }
}
