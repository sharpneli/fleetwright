using System.Globalization;
using Fleetwright.Shipgen;
using Fleetwright.Sim.Combat;
using Fleetwright.Sim.Core;
using Fleetwright.Sim.Ships;

namespace Fleetwright.Shipgen.Cli;

/// <summary>`shipgen frag [TEMPLATE --calibre MM]`: a burst's pieces (docs/battle/shells-armour-damage.md E1), made by the
/// game's own generator (<see cref="Fragments"/>) from a shell's numbers, against docs/battle/damage/13's anchors: the
/// note's shells (its §3.2 table), WDR 51's 6" HC speed and splinter knee, Boise's largest piece, Okun's 8" HE table
/// regenerated (§4.1: the 50% and 1% columns within ~10% at 4-20 m), the cable cuts, the lean, the single pieces, and
/// THOR against Thompson for the heavy pieces (open question 2). With a template, that shell at a calibre alone.</summary>
static class FragBench
{
    static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

    /// <summary>13's shells: calibre, mass, filler kg, explosive, kind, steel (size, speed).</summary>
    static readonly (string Name, double Cal, double Kg, double Filler, string Explosive, string Kind, double Size, double Speed)[] Shells =
    [
        ("5in/38 AAC Mk 35", 127, 25.0, 3.3, "Explosive D", "he", 1, 1),
        ("6in/47 HC Mk 34", 152, 47.6, 6.0, "Explosive D", "he", 1, 1),
        ("8in Army HE (Okun)", 203, 109, 7.3, "TNT", "he", 1, 1),
        ("IJN 20cm common", 203, 125.9, 8.2, "TNA", "he", 1, 1),
        ("IJN 20cm T91 AP", 203, 125.9, 3.11, "TNA", "ap", 1.25, 1),
        ("8in/55 AP Mk 21", 203, 152, 2.3, "Explosive D", "ap", 1.25, 1),
        ("14in HC", 356, 578, 47.3, "Explosive D", "he", 1, 1),
        ("15in APC Mk Ia", 381, 871, 27.4, "Lyddite", "ap", 1.25, 1),
        ("16in AP Mk 8", 406, 1225, 18.55, "Explosive D", "ap", 1.25, 1),
        ("16in HC Mk 13", 406, 862, 69.7, "Explosive D", "he", 1, 1),
        ("46cm T0 HE", 460, 1360, 61.7, "TNA", "he", 1, 1),
        ("18in CPC (powder)", 457, 1506, 110.2, "Black powder", "common", 1, 1),
        ("6in common, cast iron, powder", 152, 45.4, 2.7, "Black powder", "common", 0.6, 0.8),
    ];

    static Fragments Make(double cal, double kg, double filler, string explosive, string kind, double size, double speed)
    {
        var x = Explosives.Get(explosive) ?? throw new ArgumentException($"no explosive \"{explosive}\"");
        var c = ShellTemplates.CasingOf(kind);
        return Fragments.Of(new FragmentInputs(cal, kg, filler, KindOf(kind), x.GurneyMs!.Value, x.Brisance!.Value, size, speed,
            c.Nose!.Value, c.Ogive!.Value, c.Body!.Value, c.Plug!.Value, c.Tail!.Value, ShellTemplates.FillerBodyOf(kind)))!;
    }

    static ShellKind KindOf(string k) => k switch { "ap" => ShellKind.Ap, "common" => ShellKind.Common, _ => ShellKind.He };

    static Fragments Get(string name)
    {
        var s = Shells.Single(s => s.Name == name);
        return Make(s.Cal, s.Kg, s.Filler, s.Explosive, s.Kind, s.Size, s.Speed);
    }

    public static int Run(IReadOnlyList<string> args)
    {
        if (args.Count > 0)
            return One(args);
        Table();
        Anchors();
        Okun();
        Knee();
        Cables();
        Lean();
        Pieces();
        Heavy();
        Cost();
        return 0;
    }

    static int One(IReadOnlyList<string> args)
    {
        var t = ShellTemplates.Get(args[0]) ?? throw new ArgumentException($"no shell template \"{args[0]}\"");
        int at = args.ToList().IndexOf("--calibre");
        double cal = at >= 0 ? double.Parse(args[at + 1], Inv) : 152;
        var a = t.Value;
        double d = cal / 1000, kg = a.MassKKgM3!.Value * d * d * d;
        var x = Explosives.Get(a.Filler!.Explosive!.Name!)!;
        var c = a.Casing ?? ShellTemplates.CasingOf(a.Kind!);
        var f = Fragments.Of(new FragmentInputs(cal, kg, kg * a.Filler.Share!.Value, KindOf(a.Kind!), x.GurneyMs!.Value,
            x.Brisance!.Value, a.Steel?.Size ?? 1, a.Steel?.Speed ?? 1, c.Nose!.Value, c.Ogive!.Value, c.Body!.Value, c.Plug!.Value,
            c.Tail!.Value, a.Filler.Body ?? ShellTemplates.FillerBodyOf(a.Kind!)));
        if (f is null)
        {
            Console.WriteLine($"{t.Name} at {cal} mm: no filler, no fragments");
            return 0;
        }
        Console.WriteLine($"{t.Name} at {cal:0} mm, {kg:0.#} kg, {kg * a.Filler.Share.Value:0.##} kg {x.Name}");
        Row("", f);
        Classes(f.High, "full");
        Classes(f.Low, "low-order");
        return 0;
    }

    static void Header(string title) => Console.WriteLine($"\n{title}");

    static void Row(string name, Fragments f)
    {
        var b = f.High;
        double top = b.MottK * b.MottK * Math.Pow(Math.Log(100), 2);
        Console.WriteLine(string.Create(Inv, $"{name,-32}{b.WallMm,6:0}{b.BodyFillerKg / b.BodyKg,7:0.000}{b.SideMs,7:0}{b.SideCount,7:0}" +
            $"{b.CountAbove(0.05),7:0}{2 * b.MottK * b.MottK * 1000,8:0.#}{top,8:0.00}{b.NoseKg,7:0}{b.PlugKg,7:0}{f.SplinterMm,7:0.0}"));
    }

    static void Classes(FragmentBurst b, string what)
    {
        Console.WriteLine($"  {what}: side {b.SideMs:0} m/s, ogive {b.OgiveKg:0.#} kg at {b.OgiveMs:0}, tail {b.TailKg:0.#} kg at {b.TailMs:0}; " +
                          $"nose {b.NoseKg:0.#} kg +{b.NoseKickMs:0}, plug {b.PlugKg:0.#} kg -{b.PlugKickMs:0} m/s");
        foreach (double g in new[] { 1.0, 10, 50, 200, 1000, 5000 })
            Console.WriteLine(string.Create(Inv, $"    over {g,5:0} g: {b.CountAbove(g / 1000),7:0.#}"));
    }

    static void Table()
    {
        Header("T1  What each burst throws (13 §3.2); the game's generator. Splinter: STS the best 10% beat at 5 m (the matchup's)");
        Console.WriteLine($"{"Shell",-32}{"wall",6}{"C/M",7}{"V0",7}{"N>1g",7}{"N>50g",7}{"mean g",8}{"top1%",8}{"nose",7}{"plug",7}{"splint",7}");
        foreach (var s in Shells)
            Row(s.Name, Get(s.Name));
        Console.WriteLine("  note's: 5\" 20 0.181 957 612 70 16; 6\" HC 25 0.172 934 786 142 25; 16\" AP 136 0.025 370 303 251 2126 23 398 97");
    }

    static void Anchors()
    {
        Header("T2  Anchors");
        var hc = Get("6in/47 HC Mk 34").High;
        Console.WriteLine($"  6\" HC side spray: {hc.SideMs / 0.3048:0} ft/s; WDR 51 measured ~3,200 ft/s");
        var t91 = Get("IJN 20cm T91 AP").High;
        Console.WriteLine($"  IJN 8\" AP (Boise hit 5): largest body piece ~{t91.MassAt(1):0.0} kg; recovered 2x4\" piece of its {t91.WallMm / 25.4:0.0}\" wall " +
                          $"~{2 * 4 * t91.WallMm / 25.4 * Math.Pow(0.0254, 3) * 7850:0.0} kg");
        Console.WriteLine($"  6\" cast-iron powder common: side spray {Get("6in common, cast iron, powder").High.SideMs:0} m/s (Okun: <= 600 ft/s = 183 m/s)");
    }

    static void Okun()
    {
        Header("T3  8\" HE, static: STS (calibres) beaten by the best 50% / 10% / 1% of the side spray (model | Okun)");
        var b = Get("8in Army HE (Okun)").High;
        (int Cal, double A, double B, double C)[] okun = [(20, 0.0213, 0.093, 0.106), (50, 0.0158, 0.075, 0.100), (100, 0.0113, 0.057, 0.091),
            (500, 0.0011, 0.0186, 0.0464), (1000, 0.0006, 0.012, 0.023)];
        foreach (var (cal, a, bb, c) in okun)
        {
            double r = cal * 0.203;
            double q50 = b.DefeatMm(r, 0.5, FragmentClass.Ductile) / 203, q10 = b.DefeatMm(r, 0.1, FragmentClass.Ductile) / 203,
                q1 = b.DefeatMm(r, 0.01, FragmentClass.Ductile) / 203;
            Console.WriteLine(string.Create(Inv, $"  {cal,5} cal ({r,6:0.0} m): {q50,7:0.0000} {q10,7:0.0000} {q1,7:0.0000}  | {a,7:0.0000} {bb,7:0.0000} {c,7:0.0000}" +
                $"   ({q50 / a - 1,5:+0%;-0%} {q10 / bb - 1,5:+0%;-0%} {q1 / c - 1,5:+0%;-0%})"));
        }
    }

    static void Knee()
    {
        Header("T4  Splinter plating: share of the side spray (>= 1 g) through STS at normal, 5 m, static (the game's classes)");
        (double Mm, string Name)[] plates = [(6.1, "10 lb"), (12.2, "20 lb"), (15.6, "25 lb"), (18.4, "30 lb"), (24.5, "40 lb"), (38.1, "1.5in")];
        Console.WriteLine($"{"Shell",-26}" + string.Concat(plates.Select(p => $"{p.Name,8}")));
        foreach (var name in new[] { "5in/38 AAC Mk 35", "6in/47 HC Mk 34", "IJN 20cm common", "14in HC", "16in HC Mk 13", "16in AP Mk 8", "18in CPC (powder)" })
        {
            var b = Get(name).High;
            Console.WriteLine($"{name,-26}" + string.Concat(plates.Select(p =>
                $"{b.ThroughCount(5, b.SideMs, Fragments.MildMm(p.Mm, FragmentClass.Ductile)) / b.SideCount * 100,7:0}%")));
        }
        Console.WriteLine("  note's: 5\" 32/5/1/1/0/0, 6\" HC 39/8/3/1/0/0, 14\" HC 66/35/24/18/8/1, 16\" AP 66/35/24/18/8/1 (WDR 51: 25-30 lb the knee for 5-8\" HE)");
    }

    static void Cables()
    {
        Header("T5  A 5 cm cable run crossing the band: P(cut) by fragments >= 5 g still over 150 m/s");
        Console.WriteLine($"{"Shell",-26}{"2 m",8}{"5 m",8}{"10 m",8}{"20 m",8}");
        foreach (var name in new[] { "5in/38 AAC Mk 35", "6in/47 HC Mk 34", "IJN 20cm common", "16in AP Mk 8", "16in HC Mk 13" })
        {
            var b = Get(name).High;
            Console.WriteLine($"{name,-26}" + string.Concat(new[] { 2.0, 5, 10, 20 }.Select(r => $"{1 - Math.Exp(-b.LineHits(r, 0.05, b.SideMs)),8:0.00}")));
        }
        Console.WriteLine("  note's: 5\" 0.79/0.47/0.27/0.15, 6\" HC 0.89/0.59/0.36/0.20, 16\" AP 0.69/0.37/0.21/0.11");
    }

    static void Lean()
    {
        Header("T6  The ring leans forward with the shell's speed at the burst; a box 5 m off square to the shell, its share through STS");
        foreach (var (name, v) in new[] { ("6in/47 HC Mk 34", 0.0), ("6in/47 HC Mk 34", 500), ("16in AP Mk 8", 0), ("16in AP Mk 8", 150), ("16in AP Mk 8", 350),
                     ("IJN 20cm T91 AP", 300) })
        {
            var b = Get(name).High;
            double speed = Math.Sqrt(b.SideMs * b.SideMs + v * v), lean = double.RadiansToDegrees(Math.Atan2(v, b.SideMs));
            Console.WriteLine(string.Create(Inv, $"  {name,-22} v {v,4:0}: fragments {speed,5:0} m/s, ring {lean,3:0}° forward of square"));
        }
    }

    static void Pieces()
    {
        Header("T7  The single pieces: the nose slug and the base plug (kick, and their speed when the shell bursts at v_r)");
        foreach (var name in new[] { "8in/55 AP Mk 21", "IJN 20cm T91 AP", "15in APC Mk Ia", "16in AP Mk 8", "16in HC Mk 13" })
        {
            var b = Get(name).High;
            Console.WriteLine(string.Create(Inv, $"  {name,-22} nose {b.NoseKg,5:0} kg +{b.NoseKickMs,3:0}, plug {b.PlugKg,4:0} kg -{b.PlugKickMs,3:0}" +
                $"   v_r 100: {100 + b.NoseKickMs,4:0}/{Math.Max(0, 100 - b.PlugKickMs),3:0}   v_r 300: {300 + b.NoseKickMs,4:0}/{Math.Max(0, 300 - b.PlugKickMs),3:0}"));
        }
    }

    static void Heavy()
    {
        Header("Open question 2: THOR above 53 g. Mild steel a compact steel piece gets through at 400 m/s: THOR vs Thompson (a blunt piece, q 0.6, mild 0.7)");
        foreach (double kg in new[] { 0.05, 0.5, 2, 5, 20 })
        {
            double thor = Fragments.MildMmAt(kg, 400);
            double dia = Math.Cbrt(kg / 7850) * 1000;
            double thompson = Terminal.PenMm(400, dia, kg, 0) * 0.6 / 0.7;
            Console.WriteLine(string.Create(Inv, $"  {kg,5:0.##} kg ({dia,3:0} mm): THOR {thor,6:0.0} mm, Thompson {thompson,6:0.0} mm"));
        }
    }

    static void Cost()
    {
        Header("Cost: one box's hits");
        var b = Get("16in HC Mk 13").High;
        var box = new Box(3, 6, -2, 2, 0, 3);
        var sw = System.Diagnostics.Stopwatch.StartNew();
        double sum = 0;
        int n = 200_000;
        for (int i = 0; i < n; i++)
            sum += b.Hits(new P3(0, 0, 1 + i % 3), new P3(1, 0, 0), 200, box, 6);
        Console.WriteLine($"  {sw.Elapsed.TotalMilliseconds * 1000 / n:0.000} µs a box ({sum / n:0.0} hits each)");
    }
}
