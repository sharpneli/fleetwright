namespace Fleetwright.Shipgen;

/// <summary>The subdivision check (protection-plan.md H7; research 09 §7.4.5): how many adjacent main compartments
/// (sections between transverse bulkheads) the ship floats with, flooded to the sea outside, before the margin line goes
/// under: <see cref="MarginM"/> below the bulkhead deck (the main deck) at the side, at the design draught.
/// Destroyers were "four-compartment ships"; Iowa's unit machinery was redesigned so that two adjacent rooms flooded
/// stayed within it. It warns and never refuses (limits never block).</summary>
public static class Floodable
{
    /// <summary>The margin line's depth under the bulkhead deck at the side, m (the merchant rules' 3 in).</summary>
    public const double MarginM = 0.076;
    /// <summary>The longest run tried: a ship that floats with this many flooded is that or better.</summary>
    public const int MaxRun = 4;
    /// <summary>Fewer than this many and the design is warned.</summary>
    public const int Standard = 2;

    /// <summary>A run of adjacent sections flooded: the first (its index among the sections, bow first) and how many, how
    /// far the margin line went under (m, at the worse end; negative: it stayed out), the water (t) and the run's length (m).</summary>
    public sealed record Run(int First, int Count, double ImmersionM, double WaterT, double LengthM);

    /// <summary>The ship's standard (the most adjacent compartments any run of which it floats with, up to MaxRun; 0 when
    /// one alone is too many) and the worst run of the first length that fails (null: none fails).</summary>
    public static (int Standard, Run? Worst) Check(IReadOnlyList<Section> sections, IEnumerable<Cell> all, Hydrostatics h, double length,
        double waterline)
    {
        var bySection = new Dictionary<string, List<Cell>>(StringComparer.Ordinal);
        foreach (var c in all)
            if (c.Base < -1e-6)   // under the bulkhead deck
            {
                if (!bySection.TryGetValue(c.Section, out var l))
                    bySection[c.Section] = l = [];
                l.Add(c);
            }
        int n = sections.Count;
        var cells = new List<Cell>();
        for (int k = 1; k <= Math.Min(MaxRun, n); k++)
        {
            Run? worst = null;
            for (int i = 0; i + k <= n; i++)
            {
                cells.Clear();
                for (int j = i; j < i + k; j++)
                    cells.AddRange(bySection.GetValueOrDefault(sections[j].Id) ?? []);
                var (imm, water) = Flood(cells, h, length, waterline);
                if (imm > 0 && (worst is null || imm > worst.ImmersionM))
                    worst = new Run(i, k, imm, water, sections[i].X1 - sections[i + k - 1].X0);
            }
            if (worst is not null)
                return (k - 1, worst);
        }
        return (Math.Min(MaxRun, n), null);
    }

    static readonly string[] Counts = ["no", "one", "two", "three", "four"];

    /// <summary>The design's warning when it floats with fewer adjacent compartments flooded than its style asks: the worst
    /// run, its length, the rooms in it that matter, and how far the margin line goes under. Null when it meets it.</summary>
    public static string? Warning(SubdivisionData sub, int standard, Run? worst, int need)
    {
        if (standard >= need || worst is not { } w)
            return null;
        var ids = sub.Sections.Skip(w.First).Take(w.Count).Select(s => s.Id).ToHashSet(StringComparer.Ordinal);
        var inRun = sub.Cells.Where(c => ids.Contains(c.Section)).Select(c => c.Room).ToHashSet(StringComparer.Ordinal);
        var rooms = sub.Rooms.Where(r => inRun.Contains(r.Id) && r.Kind is not ("stores" or "accommodation" or "double_bottom" or "tds"))
            .Select(r => r.Id).ToList();
        string what = w.Count == 1 ? $"section {sub.Sections[w.First].Id}" : $"sections {sub.Sections[w.First].Id}-{sub.Sections[w.First + w.Count - 1].Id}";
        string Count(int k) => k < Counts.Length ? Counts[k] : k.ToString(System.Globalization.CultureInfo.InvariantCulture);
        string lead = w.Count == 1 ? "One compartment flooded sinks" : $"{char.ToUpperInvariant(Count(w.Count)[0])}{Count(w.Count)[1..]} adjacent compartments flooded sink";
        return string.Format(System.Globalization.CultureInfo.InvariantCulture,
            "{0} her past the margin line: {1} ({2:0.0} m{3}) put it {4:0.00} m under. {5}; her type should be a {6}-compartment ship. " +
            "Shorter compartments there would hold.", lead, what, w.LengthM, rooms.Count > 0 ? ": " + string.Join(", ", rooms) : "", w.ImmersionM,
            standard == 0 ? "She can't take a compartment flooded" : $"She is a {Count(standard)}-compartment ship", Count(need));
    }

    /// <summary>Cells open to the sea: each filled to the sea's level outside (its volume by its permeability), the ship
    /// settled to that water (Flotation), until it stops changing; how far the margin line goes under at the worse end,
    /// and the water aboard.</summary>
    static (double ImmersionM, double WaterT) Flood(List<Cell> cells, Hydrostatics h, double length, double waterline)
    {
        double sink = 0, trim = 0, wt = 0;
        for (int it = 0; it < 100; it++)
        {
            var (z0, dzdx, _) = Flotation.Sea(waterline, h.Lcf, sink, trim, 0);
            double m = 0;
            wt = 0;
            foreach (var c in cells)
            {
                double x = (c.X0 + c.X1) / 2, f = Math.Clamp((z0 + dzdx * x - c.Base) / Math.Max(c.Top - c.Base, 1e-6), 0, 1);
                double t = c.VolumeM3 * Math.Clamp(c.Permeability, 0, 1) * f * Weight.Seawater;
                (wt, m) = (wt + t, m + t * (x - h.Lcf));
            }
            double s1 = Flotation.SinkageM(wt, h.TpcT), t1 = Flotation.TrimRad(m, h.MctTm, length);
            bool settled = Math.Abs(s1 - sink) < 1e-4 && Math.Abs(t1 - trim) < 1e-6;
            // halfway each round: the water a sinkage lets in can be more than that sinkage takes
            (sink, trim) = ((sink + s1) / 2, (trim + t1) / 2);
            if (settled)
                break;
        }
        var (zs, dz, _) = Flotation.Sea(waterline, h.Lcf, sink, trim, 0);
        double ends = Math.Max(zs + dz * length / 2, zs - dz * length / 2);
        return (ends - (0 - MarginM), wt);
    }
}
