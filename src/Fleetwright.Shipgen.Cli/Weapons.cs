using System.Globalization;
using System.Text;
using Fleetwright.Sim.Ships;

namespace Fleetwright.Shipgen.Cli;

/// <summary>`shipgen weapons`: each class's weapon facts as the sim reads them (docs/battle/fighting.md 4.0): its
/// batteries, their directors and fire-control level, the mounts' working numbers, the shells and their shares, and
/// the hull's plating. Most of it is placeholders (DesignerPlaceholders) until the designer gives it; --research puts
/// the placeholders beside the research's sourced guns, to check them by eye.</summary>
static class Weapons
{
    static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

    public static int Run(IEnumerable<string> designs, bool research, Func<string, Design> load)
    {
        foreach (var p in designs)
        {
            var design = load(p);
            var c = ShipClassCompiler.Compile(ShipDesign.Build(design));
            Console.Write(Text(c, design.Look?.Era ?? "wwii", design.Look?.Navy ?? "generic"));
        }
        if (research)
            Console.Write(ResearchTable());
        return 0;
    }

    static string Text(ShipClass c, string era, string navy)
    {
        var sb = new StringBuilder();
        void Line(string s) => sb.AppendLine(s);
        Line($"{c} - {era}, {navy}");
        for (int b = 0; b < c.Batteries.Length; b++)
        {
            var row = c.Batteries[b];
            var mounts = c.Links(row.Mounts);
            var m = c.Mounts[mounts[0]];
            Line(string.Format(Inv, "  {0}{1} {2:0.#}/{3:0.#}: {4} mount(s), {5} gun(s); level {6} ({7:D})", row.Kind.ToString().ToLowerInvariant(),
                row.Side == Side.None ? "" : " " + row.Side.ToString().ToLowerInvariant(), row.CalibreMm, row.CalibreLength, mounts.Length, row.Guns, row.Level,
                (int)row.Level));
            if (row.Local)
                Line("    no director: local control, each mount by its layer's eye");
            else
            {
                Line("    directors: " + string.Join(", ", c.Links(row.Directors).ToArray().Select(d =>
                    string.Format(Inv, "{0} ({1:0.#} m rangefinder{2})", c.ComponentId(c.Directors[d].Component), c.Directors[d].RangefinderM,
                        c.Directors[d].Radar ? ", radar" : ""))));
                Line("    units: " + string.Join("; ", c.Links(row.Units).ToArray().Select(u => string.Format(Inv, "{0}: {1}", u,
                    string.Join(", ", c.Links(c.FcUnits[u].Instruments).ToArray().Select(i => c.Instruments[i]).Select(i =>
                        string.Format(Inv, "{0} {1:0.#} m at {2:0.#} m", i.Kind.ToString().ToLowerInvariant(), i.BaseM, i.HeightM)))))));
            }
            var (o, k, mt) = row.Fit;
            Line(string.Format(Inv, "    fit: optics {0:0}x, {1:0.#} readings a minute; computer {2:0.#} s, q {3:0.###}, prior {4:0.#} kn {5:0}°, bias {6:0.#}% " +
                "{7:0.#} mil, salvo {8:0.#}% {9:0.#} mil, own turn {10:0} m, first step {11:0} m; {12}, laying {13:0.#} mil", o.Magnification, o.ReadingsPerMin,
                k.LatencyS, k.Q, k.PriorKn, k.PriorDeg, k.ResidPct, k.DeflBiasMil, k.SalvoPct, mt.SalvoDeflMil, k.OwnTurnKickM, k.Ladder0M,
                mt.DirectorFiring ? "director firing" : "each gun lays itself", mt.LayMil));
            Line(string.Format(Inv, "    a gun: reload {0:0.#} s ({1:0.#} a minute), trains {2:0.#}°/s, elevates {3:0.#}°/s to {4:0}°; pattern {5:0.#}% of the range",
                m.ReloadS, 60 / m.ReloadS, m.TrainDegS, m.ElevateDegS, m.MaxElevationDeg, row.PatternPct));
            foreach (var l in c.Loadout.Loads(b))
            {
                var s = c.Loadout.ShellTypes[l.ShellType];
                Line(string.Format(Inv, "    {0,3:0}%  {1,-32} {2,7:0.#} kg {3,4:0} m/s  i {4:0.00}  filler {5:0.##} kg  {6}{7}{8}", l.Share * 100,
                    c.Loadout.ShellTypeId(l.ShellType), s.MassKg, s.MuzzleVelocityMs, s.FormFactor, s.FillerKg, s.Fuze == Fuze.Nose ? "nose" : "base",
                    s.Fuze == Fuze.Base ? string.Format(Inv, " {0:0.###} s", s.FuzeDelayS) : "", l.ShellType == c.Loadout.Fires(b) ? "   <- first (the magazine order)" : ""));
            }
        }
        if (c.Batteries.Length == 0)
            Line("  no guns");
        int torps = c.Mounts.ToArray().Count(x => x.Kind == ComponentKind.Torpedo);
        if (torps + c.Tubes.Length > 0)
            Line($"  torpedoes: {torps} mount(s), {c.Tubes.Length} submerged tube(s) (step 4.6)");
        var pl = c.Plating;
        Line(string.Format(Inv, "  plating ({0}): side {1:0.#} mm amidships ({2:0}..{3:0} m), {4:0.#} at the ends; strength deck {5:0.#}/{6:0.#}; " +
            "decks {7:0.#}, bulkheads {8:0.#}, inner bottom {9:0.#}; superstructure {10:0.#}, control {11:0.#}", c.Material(pl.Material) ?? "steel",
            pl.ShellMm, pl.MidX0, pl.MidX1, pl.ShellEndMm, pl.StrengthDeckMm, pl.StrengthDeckEndMm, pl.DeckMm, pl.BulkheadMm, pl.InnerBottomMm,
            pl.SuperstructureMm, pl.ControlMm));
        Line("");
        return sb.ToString();
    }

    /// <summary>research/simulation/fire_control_ref.py's GUNS (shell, velocity, training) and the QF 4"/45 Mk V
    /// (navweaps), each with the era the placeholders read it in.</summary>
    static readonly (string Name, double Mm, double Cal, Era Era, string Navy, double Kg, double Ms, double Train)[] Guns =
    [
        ("12in/40 (Mikasa)", 305, 40, Era.Victorian, "kure", 386, 732, 2.0),
        ("12in/45 Mk X", 305, 45, Era.GreatWar, "portsmouth", 386, 831, 2.0),
        ("30.5cm SK L/50", 305, 50, Era.GreatWar, "kiel", 405, 855, 3.0),
        ("13.5in/45 Mk V(H)", 343, 45, Era.GreatWar, "portsmouth", 635, 757, 2.0),
        ("15in/42 Mk I", 381, 42, Era.GreatWar, "portsmouth", 871, 749, 2.0),
        ("4in/45 QF Mk V", 102, 45, Era.GreatWar, "portsmouth", 14.06, 716, double.NaN),
        ("14in/45 Mk VII", 356, 45, Era.Wwii, "portsmouth", 721, 757, 2.0),
        ("38cm SK C/34", 380, 52, Era.Wwii, "kiel", 800, 820, 5.0),
        ("16in/45 Mk 6", 406, 45, Era.Wwii, "brooklyn", 1225, 701, 4.0),
        ("16in/50 Mk 7", 406, 50, Era.Wwii, "brooklyn", 1225, 762, 4.0),
        ("46cm Type 94", 460, 45, Era.Wwii, "kure", 1460, 780, 2.0),
        ("8in/55 Mk 9", 203, 55, Era.Wwii, "brooklyn", 118, 853, 6.7),
        ("6in/47 Mk 16", 152, 47, Era.Wwii, "brooklyn", 59, 812, 10.0),
        ("5in/38 Mk 12 (DP)", 127, 38, Era.Wwii, "brooklyn", 25, 792, 25.0),
        ("5in/54 Mk 45", 127, 54, Era.ColdWar, "brooklyn", 31.75, 808, 30.0),
        ("76mm/62 OTO", 76, 62, Era.ColdWar, "la_spezia", 6.3, 925, 60.0),
    ];

    static string ResearchTable()
    {
        var sb = new StringBuilder();
        sb.AppendLine("The placeholders beside the research's guns (the muzzle velocity with the heaviest shell; the shells themselves are the");
        sb.AppendLine("templates', docs/shipgen/shell-templates.md, and the research's mass here is HE for some):");
        sb.AppendLine($"  {"gun",-20} {"era",-10} {"kg",6} {"m/s",12} {"train °/s",14}  reload s");
        foreach (var g in Guns)
        {
            double ms = Ammunition.DefaultMuzzleVelocity(g.Cal);
            var (reload, train, _, _) = DesignerPlaceholders.Gun(g.Mm, g.Cal, g.Name.Contains("DP") ? TurretShape.Dp : TurretShape.Bb, g.Era);
            sb.AppendLine(string.Format(Inv, "  {0,-20} {1,-10} {2,6:0.#} {3,4:0} ({4,3:+0;-0}%) {5,5:0.#} ({6,5})  {7,6:0.#}", g.Name, g.Era,
                g.Kg, ms, 100 * (ms / g.Ms - 1), train, double.IsNaN(g.Train) ? "-" : g.Train.ToString("0.#", Inv), reload));
        }
        return sb.ToString();
    }
}
