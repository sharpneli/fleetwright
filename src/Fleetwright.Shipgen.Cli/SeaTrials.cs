using System.Globalization;
using System.Text;
using Fleetwright.Sim;
using Fleetwright.Sim.Combat;
using Fleetwright.Sim.Core;
using Fleetwright.Sim.Ships;

namespace Fleetwright.Shipgen.Cli;

/// <summary>`shipgen trials`: a class's sea trials, run headless in a world on the ship model (docs/battle/plan.md step
/// 2.2): turning circles at hard over and standard rudder, working up to full speed, coasting, a crash stop. Each figure
/// has the research's band beside it (research/simulation/fire-control-research/manoeuvring.md §1-2) for ships of its
/// kind, and the turning circles go to an SVG. The bands check the model; a figure outside one is reported, not
/// fitted away.</summary>
static class SeaTrials
{
    const double Kn = Handling.MsPerKnot;
    static readonly WorldTime Start = WorldTime.FromUtc(1916, 5, 31, 14);
    const double Lat = 56.5, Lon = 4.0;   // the North Sea, open water all round

    /// <summary>The research's figures for a kind of ship.</summary>
    sealed record Kind(string Name, double TdLo, double TdHi, double T180Lo, double T180Hi, double AccLo, double AccHi,
        double CoastLo, double CoastHi, double StopLo, double StopHi);

    static Kind KindOf(MotionFacts m) => m.FullDisplacementT >= 15_000
        // BB: TD/L 2.5-3.5 (Yamato 2.5, Iowa 2.8, the US Standard ~3.4); 180° in 105-130 s; τ up 150-250 s, so 90% in
        // 2.3τ; Iowa coasting 120-200 yd a knot; crash stop "a little over 1 nm"
        ? new("battleship", 2.4, 3.6, 100, 135, 345, 575, 110, 200, 1700, 2600)
        : m.LengthM >= 60 && m.FullDisplacementT < 5_000
            // DD: TD/L 5-6 twin, 6-8 single; 180° in 105-150 s; 0 -> 30 kn in 2-3 min; ~50 yd a knot; crash stop 4-5 L
            ? new("destroyer", 5, 8, 100, 150, 100, 180, 40, 80, 4 * m.LengthM, 5 * m.LengthM)
            : m.LengthM < 60
                // MTB/PT: TD 4-6 L; 180° in 9-22 s; 0 -> 35 kn in ~35 s; the plane gone in seconds; stop in 2-4 L
                ? new("small craft", 3, 6, 9, 22, 20, 50, 5, 30, 2 * m.LengthM, 4 * m.LengthM)
                // cruisers between: TD/L 3.5-5 (Ticonderoga 4.0), 180° in ~110-130 s, τ up 90-150 s
                : new("cruiser", 3.5, 5.5, 100, 135, 200, 350, 60, 150, 1000, 2000);

    public static int Run(IEnumerable<string> paths, string settingsPath, string? outDir)
    {
        var settings = SimSettings.Load(settingsPath);
        foreach (var path in paths)
        {
            var design = Design.Load(path);
            var cls = ShipClassCompiler.Compile(ShipDesign.Build(design));
            var m = cls.Motion;
            var k = KindOf(m);
            double full = m.MaxSpeedKn;
            Console.WriteLine($"{cls.Name}: {m.LengthM:F0} m, {m.FullDisplacementT:#,0} t, {full} kn, {m.Rudders} rudder(s); " +
                              $"read as a {k.Name}. Placeholders from the designer: TD {m.TacticalDiameterM:F0} m, rudder " +
                              $"{m.RudderRateDegS:F1}°/s to {m.HardOverDeg}°, power 0 -> full in {m.PowerRampS:F0} s, cut in {m.PowerCutS:F0} s");

            var hard = Turn(cls, settings, m.HardOverDeg, out var hardTrack);
            var std = Turn(cls, settings, settings.StandardRudderDeg, out var stdTrack);
            Row("TD at hard over", $"{hard.Td:F0} m = {hard.Td / m.LengthM:F2} L", hard.Td / m.LengthM, k.TdLo, k.TdHi, "L");
            Row("  advance, transfer at 90°", $"{hard.Advance:F0} m, {hard.Transfer:F0} m", double.NaN, 0, 0, "");
            Row("  90°, 180°, 360°", $"{hard.T90:F0} s, {hard.T180:F0} s, {hard.T360:F0} s", hard.T180, k.T180Lo, k.T180Hi, "s (180°)");
            Row("  speed held", $"{hard.Speed / Kn:F1} kn ({1 - hard.Speed / (full * Kn):P0} lost)", double.NaN, 0, 0, "");
            Row($"TD at standard rudder {settings.StandardRudderDeg}°", $"{std.Td:F0} m = {std.Td / hard.Td:F2} of hard over", double.NaN, 0, 0, "");

            var (acc, coast, coastM, stop, stopM) = Speeds(cls, settings);
            Row("0 -> 90% of full", $"{acc:F0} s", acc, k.AccLo, k.AccHi, "s");
            Row("full -> half, engines stopped", $"{coast:F0} s, {coastM:F0} m = {coastM / 0.9144 / (full / 2):F0} yd a knot",
                coastM / 0.9144 / (full / 2), k.CoastLo, k.CoastHi, "yd/kn");
            Row("crash stop from full", $"{stop:F0} s, {stopM:F0} m", stopM, k.StopLo, k.StopHi, "m");

            if (outDir != null)
            {
                Directory.CreateDirectory(outDir);
                string file = Path.Combine(outDir, $"{Path.GetFileNameWithoutExtension(path)}_turns.svg");
                File.WriteAllText(file, Svg(cls.Name, hardTrack, stdTrack));
                Console.WriteLine($"  {file}");
            }
            Console.WriteLine();
        }
        return 0;
    }

    static void Row(string what, string value, double x, double lo, double hi, string unit)
    {
        string band = double.IsNaN(x) ? "" : $"research {lo:0.#}-{hi:0.#} {unit}{(x < lo || x > hi ? "  << outside" : "")}";
        Console.WriteLine($"  {what,-34} {value,-36} {band}");
    }

    static (World, ShipState) Trial(ShipClass cls, SimSettings settings, double kn)
    {
        var w = new World(Start, 1, settings);
        int c = w.Ships.AddClass(cls);
        w.Apply(ShipSystem.Spawn(c, Lat, Lon, 0, kn));
        return (w, w.Ships.Store[w.Ships.Store.SlotOf(1)]);
    }

    static void Run(World w, double s) => w.AdvanceTo(w.Time + (long)Math.Round(s * 1000));

    record struct TurnResult(double Td, double Advance, double Transfer, double T90, double T180, double T360, double Speed);

    /// <summary>From full speed, the rudder put over and held for a full circle.</summary>
    static TurnResult Turn(ShipClass cls, SimSettings settings, double rudderDeg, out List<(double X, double Y)> track)
    {
        var (w, s) = Trial(cls, settings, cls.Motion.MaxSpeedKn);
        Run(w, 1);
        var frame = s.Frame;
        w.Apply(ShipSystem.Rudder(1, rudderDeg));
        var r = new TurnResult();
        track = [(0, 0)];
        double turned = 0, last = s.HeadingDeg, t = 0;
        while (turned < 360 && t < 7200)
        {
            Run(w, 0.5);
            t += 0.5;
            double d = s.HeadingDeg - last;
            turned += d - 360 * Math.Round(d / 360);
            last = s.HeadingDeg;
            var (x, y, _) = frame.ToLocal(s.Position);
            track.Add((x, y));
            if (r.T90 == 0 && turned >= 90)
                (r.T90, r.Advance, r.Transfer) = (t, x, y);
            if (r.T180 == 0 && turned >= 180)
                (r.T180, r.Td) = (t, y);
            r.Speed = s.Speed;
        }
        r.T360 = t;
        return r;
    }

    static (double Acc, double Coast, double CoastM, double Stop, double StopM) Speeds(ShipClass cls, SimSettings settings)
    {
        double full = cls.Motion.MaxSpeedKn;
        var (w, s) = Trial(cls, settings, 0);
        w.Apply(ShipSystem.Speed(1, full));
        double acc = 0;
        while (s.Speed < 0.9 * full * Kn && acc < 7200)
        {
            Run(w, 1);
            acc++;
        }

        (w, s) = Trial(cls, settings, full);
        var p0 = s.Position;
        w.Apply(ShipSystem.Speed(1, 0));
        double coast = 0;
        while (s.Speed > full / 2 * Kn && coast < 7200)
        {
            Run(w, 1);
            coast++;
        }
        double coastM = Earth.Distance(p0, s.Position);

        (w, s) = Trial(cls, settings, full);
        p0 = s.Position;
        w.Apply(ShipSystem.Speed(1, -full));
        double stop = 0;
        while (s.Speed > 0 && stop < 7200)
        {
            Run(w, 0.5);
            stop += 0.5;
        }
        return (acc, coast, coastM, stop, Earth.Distance(p0, s.Position));
    }

    /// <summary>The two circles from above, the ship starting at the origin heading up, metres, a 100 m grid.</summary>
    static string Svg(string name, List<(double X, double Y)> hard, List<(double X, double Y)> std)
    {
        var all = hard.Concat(std).ToList();
        double x0 = all.Min(p => p.Y) - 100, x1 = all.Max(p => p.Y) + 100, y0 = -all.Max(p => p.X) - 100, y1 = -all.Min(p => p.X) + 100;
        var ic = CultureInfo.InvariantCulture;
        var sb = new StringBuilder();
        sb.Append(ic, $"<svg xmlns=\"http://www.w3.org/2000/svg\" viewBox=\"{x0:F0} {y0:F0} {x1 - x0:F0} {y1 - y0:F0}\" width=\"{(x1 - x0) / 2:F0}\" height=\"{(y1 - y0) / 2:F0}\">\n");
        sb.Append(ic, $"<rect x=\"{x0:F0}\" y=\"{y0:F0}\" width=\"{x1 - x0:F0}\" height=\"{y1 - y0:F0}\" fill=\"#f4f1e8\"/>\n");
        for (double g = Math.Ceiling(x0 / 100) * 100; g < x1; g += 100)
            sb.Append(ic, $"<line x1=\"{g}\" y1=\"{y0:F0}\" x2=\"{g}\" y2=\"{y1:F0}\" stroke=\"#ddd\" stroke-width=\"2\"/>\n");
        for (double g = Math.Ceiling(y0 / 100) * 100; g < y1; g += 100)
            sb.Append(ic, $"<line x1=\"{x0:F0}\" y1=\"{g}\" x2=\"{x1:F0}\" y2=\"{g}\" stroke=\"#ddd\" stroke-width=\"2\"/>\n");
        void Path(List<(double X, double Y)> t, string colour) =>
            sb.Append("<polyline fill=\"none\" stroke=\"").Append(colour).Append("\" stroke-width=\"6\" points=\"")
              .AppendJoin(' ', t.Select(p => string.Format(ic, "{0:F1},{1:F1}", p.Y, -p.X))).Append("\"/>\n");
        Path(std, "#7a9cc6");
        Path(hard, "#b0413e");
        sb.Append(ic, $"<text x=\"{x0 + 20:F0}\" y=\"{y0 + 60:F0}\" font-size=\"40\" font-family=\"sans-serif\">{System.Security.SecurityElement.Escape(name)}: hard over (red), standard rudder (blue); grid 100 m</text>\n");
        sb.Append("</svg>\n");
        return sb.ToString();
    }
}
