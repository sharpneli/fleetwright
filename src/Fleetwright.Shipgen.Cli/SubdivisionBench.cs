using System.Globalization;
using Fleetwright.Shipgen;

namespace Fleetwright.Shipgen.Cli;

/// <summary>`shipgen subdivision`: the main compartments (docs/shipgen/protection-plan.md H7): how many sections, how
/// long, and how many adjacent ones the ship floats with flooded before the margin line goes under (Floodable), with the
/// worst run that sinks it and the rooms in it. A line a design.</summary>
static class SubdivisionBench
{
    static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

    public static int Run(IReadOnlyList<string> targets, Func<string, Design> load, bool rooms)
    {
        Console.WriteLine("design                       L m  sections  longest m (share)  standard  worst run that sinks her");
        foreach (var t in targets)
        {
            var ship = ShipDesign.Build(load(t));
            var hb = ship.Hitboxes;
            var secs = hb.Sections;
            var (std, worst) = Floodable.Check(secs, hb.Cells, hb.Hydrostatics, hb.Length, hb.Vertical.Waterline);   // as the report does
            var longest = secs.MaxBy(s => s.X1 - s.X0)!;
            string run = "";
            if (worst is { } w)
            {
                var ids = secs.Skip(w.First).Take(w.Count).Select(s => s.Id).ToHashSet(StringComparer.Ordinal);
                var names = hb.Rooms.Where(r => r.Kind is not ("stores" or "accommodation" or "double_bottom" or "tds") &&
                        r.Cells.Any(cid => hb.Cells.First(c => c.Id == cid) is var c && ids.Contains(c.Section) && c.Room == r.Id))
                    .Select(r => r.Id).ToList();
                run = string.Format(Inv, "{0} ({1:0.0} m, {2:0} t, margin line {3:0.00} m under){4}", string.Join("+", ids), w.LengthM, w.WaterT, w.ImmersionM,
                    rooms && names.Count > 0 ? ": " + string.Join(", ", names) : "");
            }
            Console.WriteLine(string.Format(Inv, "{0,-26} {1,6:0.0}  {2,8}  {3,7:0.0} ({4:0.000})    {5,-8}  {6}", Trim(ship.Design.Id ?? t, 26), hb.Length,
                secs.Count, longest.X1 - longest.X0, (longest.X1 - longest.X0) / hb.Length, std >= Floodable.MaxRun ? $"{std}+" : std.ToString(Inv), run));
        }
        return 0;
    }

    static string Trim(string s, int n) => s.Length <= n ? s : s[..(n - 1)] + "…";
}
