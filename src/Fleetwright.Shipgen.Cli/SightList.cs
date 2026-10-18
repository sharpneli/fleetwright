using System.Globalization;
using System.Text;
using Fleetwright.Sim.Combat;
using Fleetwright.Sim.Core;
using Fleetwright.Sim.Ships;

namespace Fleetwright.Shipgen.Cli;

/// <summary>`shipgen sight`: each class's sighting facts as the sim reads them (docs/battle/fire-control.md F4d): its
/// eyes (kind, part, height over the sea, horizon, rangefinder, blind arc), its silhouette's top and its area above a
/// few heights, how far a lookout in the tallest of the classes' tops makes it out, and the reach the world derives
/// from the tallest eye and silhouette among them.</summary>
static class SightList
{
    static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

    public static int Run(IEnumerable<string> designs, Func<string, Design> load, string settingsPath, string? from)
    {
        var s = SimSettings.Load(settingsPath);
        var classes = designs.Select(p => ShipClassCompiler.Compile(ShipDesign.Build(load(p)))).ToList();
        double eyeMax = classes.Max(c => c.Eyes.Length > 0 ? c.Eyes[0].HeightM : 0), topMax = classes.Max(c => c.Silhouette.TopM);
        var observer = from is null ? null : ShipClassCompiler.Compile(ShipDesign.Build(load(from)));
        double eye = observer is { Eyes.Length: > 0 } ? observer.Eyes[0].HeightM : eyeMax;
        var sb = new StringBuilder();
        void Line(string t) => sb.AppendLine(t);
        foreach (var c in classes)
        {
            var sil = c.Silhouette;
            var side = c.Numbers(sil.SideAreaM2);
            var end = c.Numbers(sil.EndAreaM2);
            string Areas(ReadOnlySpan<double> t)
            {
                var parts = new List<string>();
                for (int h = 0; h < t.Length; h += 10)
                    parts.Add(string.Format(Inv, "{0} m {1:0}", h, t[h]));
                return string.Join(", ", parts);
            }
            Line(string.Format(Inv, "{0}: silhouette top {1:0.0} m over the sea; {2:0} masts, {3} funnels", c, sil.TopM, sil.Masts, sil.Funnels));
            Line("  side on, m² above: " + Areas(side));
            Line("  end on, m² above:  " + Areas(end));
            foreach (var e in c.Eyes)
            {
                int blind = 0;
                for (int k = 0; k < Sight.Sectors; k++)
                    blind += (e.Blind >> k & 1) != 0 ? 1 : 0;
                Line(string.Format(Inv, "  {0,-13} {1,-26} {2,5:0.0} m, horizon {3,4:0.0} km{4}{5}", e.Kind.ToString().ToLowerInvariant(),
                    c.ComponentId(e.Component), e.HeightM, SightModel.HorizonM(e.HeightM, s) / 1000,
                    e.RangefinderM > 0 ? string.Format(Inv, ", rangefinder {0:0.#} m x{1:0}", e.RangefinderM, SightModel.Magnification(e, s)) : "",
                    blind > 0 ? string.Format(Inv, ", blind {0:0}°", blind * Sight.SectorDeg) : ""));
            }
            Line(string.Format(Inv, "  made out side on by {0}a lookout at {1:0.0} m (x{2:0}): {3:0.0} km in 25 km visibility, {4:0.0} km in clear air; bow on {5:0.0} km",
                observer is null ? "" : observer + "'s highest eye, ", eye, s.SightLookoutMagnification,
                SightModel.MadeOutAtM(eye, s.SightLookoutMagnification, c, 25_000, 90, 30, false, s) / 1000,
                SightModel.MadeOutAtM(eye, s.SightLookoutMagnification, c, double.PositiveInfinity, 90, 30, false, s) / 1000,
                SightModel.MadeOutAtM(eye, s.SightLookoutMagnification, c, 25_000, 0, 30, false, s) / 1000));
            if (observer is not null)
                Line(string.Format(Inv, "  first sighted by {0} closing at 30 kn (median, its {1} highest eyes): side on {2:0.0} km in 25 km visibility, " +
                    "{3:0.0} km in clear air; at 45° {4:0.0} km and {5:0.0} km", observer, s.SightEyes,
                    SightModel.FirstSightingM(observer, c, 25_000, 90, 30 * 0.5144, s) / 1000,
                    SightModel.FirstSightingM(observer, c, double.PositiveInfinity, 90, 30 * 0.5144, s) / 1000,
                    SightModel.FirstSightingM(observer, c, 25_000, 45, 30 * 0.5144, s) / 1000,
                    SightModel.FirstSightingM(observer, c, double.PositiveInfinity, 45, 30 * 0.5144, s) / 1000));
        }
        Line(string.Format(Inv, "reach: the tallest eye {0:0.0} m and top {1:0.0} m see each other's tops at {2:0.0} km at most", eyeMax, topMax,
            SightModel.ReachM(eyeMax, topMax, s) / 1000));
        Console.Write(sb.ToString());
        return 0;
    }
}
