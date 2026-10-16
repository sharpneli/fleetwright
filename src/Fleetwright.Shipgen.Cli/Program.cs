using System.Diagnostics;
using System.Text.Json.Nodes;
using Fleetwright.Shipgen;
using Fleetwright.Shipgen.Golden;
using Fleetwright.Sim;
using Fleetwright.Sim.Ships;
using Fleetwright.Sim.Core;
using Fleetwright.Shipgen.Tools;
using Fleetwright.Shipgen.Render;
using Fleetwright.Shipgen.Render.Bake;
using Fleetwright.Shipgen.Render.Golden;

namespace Fleetwright.Shipgen.Cli;

/// <summary>The shipgen command: the test harness of the port (design.py's data output, validation strings and the
/// golden comparison). Arguments are parsed by hand: a few flags, and AOT-safe.</summary>
public static class Program
{
    const string Usage = """
        shipgen validate DESIGN.json... [--no-limits]
            print the validation strings (shipdesign.validate + looks.validate)
        shipgen design DESIGN.json... [--no-limits] [--out DIR] [--scale S] [--mips N] [--no-sprites] [--previews]
            build each design (design.py): DIR/<id>/report.json, hitboxes.json, ship.json and the summary line; then
            draw and bake it on the GPU (default 10 px/m, 5 mips): sprite.json, hull.png, height.png,
            turrets/<type>.png, each layer's _mips.png; --previews adds preview_rest.png and preview_starboard.png
        shipgen capture [--root DIR] [--out DIR] [--jobs N] [CASE...]
            golden.py's design capture from our side: DIR/<case>.json.gz for each golden case (default all)
        shipgen golden-diff GOLDEN OUT [--show N]
            compare two captures (directories of <case>.json.gz, or two files) by the golden rules
        shipgen golden-check [--root DIR] [--jobs N] [--show N] [CASE|PREFIX*...]
            capture and compare against the goldens in one go, printing each failing case's first differences
        shipgen golden-update [--root DIR] [CASE|PREFIX*...]
            after a deliberate change to the output: rewrite the design, SVG and sprite.json goldens of the cases
            that no longer match from ours (the PNGs stay; png-check compares them by coverage)
        shipgen bench [--root DIR] [--repeat N] [CASE|PREFIX*...]
            single-threaded build time per case (best of N), next to Python's from the golden capture, and the
            time to compile the build into a ShipClass
        shipgen draw DESIGN.json... [--out DIR] [--scale S] [--mips N]
            draw each design (default 10 px/m, 5 mips): DIR/<id>/sprite.json and the display lists as SVG (hull.svg,
            height.svg, turrets/<type>.svg)
        shipgen bake DESIGN.json... [--out DIR] [--scale S] [--mips N]
            draw and bake each design on the GPU: DIR/<id>/sprite.json, hull.png, height.png, turrets/<type>.png
            and each layer's _mips.png; for the lighting relief.png (grey to the tallest), normal.png (normal + AO in
            alpha) and normal_mips.png, and each turret type's _relief.png and _normal.png
        shipgen profile DESIGN.json... [--out DIR] [--scale S] [--navy N] [--era E]
            draw and bake each design's profile from the starboard side (default 10 px/m): DIR/<id>/profile.png (the
            full hull) and silhouette.png, and their SVGs; --navy and --era repaint it in another look
        shipgen png-check [--root DIR] [--out DIR] [CASE|PREFIX*...]
            bake every design with a sprite golden and compare its PNGs' coverage (IoU) with Python's; --out keeps
            the PNGs
        shipgen fuzz DESIGN.json... [--cases 150] [--seed 1] [--jobs N] [--mem-gb 3] [--timeout 120] [--slow 30]
                     [--mode all|numbers|choices|structure] [--no-limits] [--out fuzz_out]
            mutate the designs and build every mutant in its own process with a time and memory cap: any valid
            design must build (errors are fine), never crash, hang, blow up memory or publish a NaN. Failures
            (and builds slower than --slow s) are saved to --out as <id>_<case>.json and .txt
        shipgen verify DIR...
            check `design` output: turret sprites against their hitboxes at four angles, raised blocks and funnels
            opaque inside their hitboxes, the hull image against the hull outline, the subdivision and traverses
        shipgen shell-test DESIGN.json... [--shells 200000] [--seed 1] [--show 15] [--gratings] [--no-limits] [--underwater M]
                          [--dump DIR] [--path N]
            toss shells at each design's citadel from every bearing, falling 0-50 degrees, and flag any that reach a
            citadel cell through neither armour nor water (exit 1 if any do); --gratings closes the uptakes'
            openings in the armour decks; --underwater lets a shell go on under water to M m deep (default 0: water
            stops it). Runs on the compiled ShipClass, one random stream per shot. --dump writes
            every shot's outcome to DIR/<id>.txt, to compare two versions of the tracer shot by shot. --path N
            prints the first N shots' paths instead: everything each crossed, in order (the hit query's HitPath)
        shipgen mono-check [DESIGN...] [--steps 4] [--jobs N] [--show 10]
            sweep the designer's trim levers (belt, speed, AA, ...) STEPS steps either way of each design's value and
            build each step: list the reversals (more of a weight-adding input, a lighter ship) and lumps (a step far
            bigger than its neighbours), the biggest SHOW first. DESIGN is a path or a shipped design's name; default a
            spread of types
        shipgen svg-check [--root DIR] [--jobs N] [--show N] [CASE|PREFIX*...]
            every case's SVGs against golden/svg (Python's drawing with the port's RNG)
        shipgen sprite-check [--root DIR] [--jobs N] [--show N] [CASE|PREFIX*...]
            every design's sprite.json against golden/sprite
        shipgen sim [--from 1916-05-31T14:00] [--speed 30000] [--seconds 3] [--seed 1]
            run an empty world on its sim thread at about 60 frames a second, printing twice a second the clock, the
            speed asked and achieved, the subsolar point, and the bytes the sim thread allocated (flat in play)
        shipgen shore [--land Content/World/land-10m.bin] [--points 100000]
            the land index (Shore): build time and memory, IsLand against Land.IsLand, and each query's cost
        shipgen trials DESIGN.json... [--out DIR] [--settings Content/Sim/settings.json]
            each design's sea trials on the ship model, headless: turning circles at hard over and standard rudder,
            0 -> full, coasting, a crash stop, each beside the research's band for its kind; --out writes the circles
            as DIR/<design>_turns.svg
        shipgen duel SHOOTER [TARGET] [--range M] [--level L] [--runs N] [--minutes M] [--speed KN] [--target-speed KN] [--bearing DEG] [--course DEG] [--crew trained] [--battery B] [--seed S] [--damage]
        shipgen hits TARGET [--shooter DESIGN] [--battery B] [--kind ap|common|he] [--loadout SPEC] [--range M] [--aspect DEG] [--n N] [--per-ship K] [--seed S] [--show N] [--settle MIN]
            the damage model alone (docs/battle/fighting.md 4.3a): N hits of the shooter's battery's shell (the one it
            fires, or --kind) at a range, spread over the target as the falling shells see it from --aspect degrees off
            its bow, through the world's damage system: where they struck, the outcome classes, fire, crew and holes a
            hit, the parts most often put out of action, and each ship's state after K hits (--per-ship) and MIN minutes
            of flooding and fire (default 30: the water it took, and lost to its magazine, foundered or capsized); --show prints
            the first N hits' paths. TARGET and DESIGN are paths or names (shipgen/designs, Content/Scenarios/Designs).
            --loadout gives the shooter other shells (docs/battle/shells-armour-damage.md S5): BATTERY:SHELL[@SHARE][+SHELL...]
            with | between batteries; a battery is main, secondary, main[i] or secondary[i], a shell a template's name
            (docs/shipgen/shell-templates.md), a block of the design's, or ap, common or he (the battery's own of that kind)
        shipgen protection DESIGN... [--shells N] [--seed S] [--summary]
            what stands between the sea and each magazine (docs/shipgen/protection-plan.md H0), through the tracer alone:
            its crown against the waterline, the layers a level line from the side and a plumb line from above cross to
            it, and N shells a fall (5, 10, 20, 35 degrees from the beam) aimed inside it: the share meeting the sea first,
            the steel before it, and the share "open" (from above the water through the skin alone); several designs or
            --summary end with a line a ship
        shipgen subdivision DESIGN... [--rooms]
            the main compartments (docs/shipgen/protection-plan.md H7): how many sections and the longest, and how many
            adjacent ones the ship floats with, flooded to the sea, before the margin line (76 mm under the bulkhead
            deck at the side) goes under; the worst run that sinks her (--rooms: the rooms in it). A line a design
        shipgen flood DESIGN [--hole WHERE:Z:AREA[:p],...] [--rooms KIND[:N],...] [--minutes M] [--every S] [--step MS] [--cells]
            the flooding model alone (docs/battle/fighting.md 4.3b): the class's facts (reserve buoyancy, freeboard, TPC,
            MCT, GM), then a ship of it stopped at sea, holed on the side (WHERE mid, bow, stern or x in m; Z m over the
            waterline; AREA m²; :p to port) or with N spaces of a kind flooded (boiler_room:2), the world run on: water,
            reserve left, sinkage, trim, heel, GM and spaces out every S seconds, until it goes down; then the water by
            space (--cells: by cell, with its joins); --step sets FloodMs
        shipgen matchup A[:N][:t0][,C:N...] B[:N][:t0][,...] [--range M] [--share 0.6] [--sight M]
            who can fight whom (docs/battle/command.md 5.1g): each class's profile as the matchup sees it, each battery's
            hit chance and out chance a hit on the other side's classes, the engagement at each candidate (either side's
            gun range, a share of its main guns' reach; either side's torpedo attack) and A's judgement: the range the
            faster side picks and A's confidence (the share of B's tonnage it puts out of the fight for the share of its
            own it loses); :t0 takes a class's torpedoes away; --range fixes the range, --sight the defence's guns'
            opening range on an attack (night)
        shipgen torpedo TARGET [--shooter DESIGN] [--at mid|bow|stern|SHARE] [--depth M] [--n N] [--per-ship K] [--settle MIN] [--seed S]
                        [--range M [--aspect DEG] [--target-speed KN] [--spread N] [--runs N]]
            torpedoes (docs/battle/fighting.md 4.6), the shooter's (default destroyer). Without --range, the hit alone: N
            hits at the set depth on the target's starboard side (anywhere along it, or at a share of its length from
            the stern), K a ship, through the world's damage system, then MIN minutes of flooding (default 60): the
            outcomes, the spaces struck, holes and crew, parts put out of action, and each ship's water, list, speed
            and loss. With --range, the attack: the shooter at M m, DEG off the target's bow, fires a spread of N at it
            steaming on (default 15 kn), run after run (default 20): torpedoes fired, hits, the target lost
        shipgen ballistics DESIGN... [--research]
            each battery's range table from the shell it fires (docs/battle/fighting.md 4.2a): range, elevation, time
            of flight, angle of fall and striking velocity from a muzzle 10 m up, and the table's size and build time;
            --research adds the research's 16"/50 against its sourced times and falls
        shipgen weapons DESIGN... [--research]
            each design's weapon facts as the sim reads them (docs/battle/fighting.md 4.0): batteries, directors and
            fire-control level, a gun's reload, training and elevation, the shells and their shares, the plating; most
            are placeholders until the designer gives them. DESIGN is a path or a shipped design's name. --research
            adds the placeholders beside the research's guns
        shipgen ammo-templates DESIGN.json... [--outfits] [--dry]
            S1 of docs/battle/shells-armour-damage.md, one-off: switches each design's shells to the templates of its
            navy and year (the outfits' kinds and shares kept); --outfits (S2) gives each battery the default outfit
            instead, main 70% AP / 30% HE, secondary 30 / 70 (common for HE in the families before HE); --dry lists it
        shipgen templates
            writes the shell and explosive catalogues (ShellTemplates, Explosives) out: Content/Templates/shells.json and
            explosives.json, docs/shipgen/shell-templates.md and explosive-templates.md
        shipgen scenario [SCENARIO.json] [--minutes 60] [--every 5] [--threads N] [--at 10] [--no-land] [--replay]
            a scenario headless at full tilt (default Content/Scenarios/jutland.json): its classes' build time (no
            art), the land and the placing, then game minutes on the sim's work alone: ships on the fine tick and on
            rails, the speed it runs at, the share of the sim thread --at x takes, allocations and collections;
            --replay runs the command log again and compares the hashes
        shipgen scenario [SCENARIO.json] --play [--seeds N] [--fast 1000] [--fine 1] [--threads N] [--no-land]
            the scenario played to its end with the AI on every side (docs/battle/command.md 5.0): one seed's
            timeline and result, or a line a seed and the spread; the asked speed is --fine while an encounter is
            open (guns fire only in the fine tier) and --fast otherwise
        --root defaults to the repo's shipgen folder (found from the current directory up).
        """;

    public static int Main(string[] args)
    {
        if (args.Length == 0 || args[0] is "-h" or "--help" or "help")
        {
            Console.WriteLine(Usage);
            return args.Length == 0 ? 1 : 0;
        }
        Console.OutputEncoding = System.Text.Encoding.UTF8;   // ships' names: Lützow, Fürst Bismarck
        var a = new Args(args.Skip(1));
        try
        {
            return args[0] switch
            {
                "validate" => Validate(a),
                "design" => Design(a),
                "capture" => Capture(a),
                "golden-diff" => GoldenDiffCmd(a),
                "golden-check" => GoldenCheck(a),
                "golden-update" => GoldenUpdate(a),
                "bench" => Bench(a),
                "draw" => Draw(a),
                "fuzz" => FuzzCmd(a),
                "fuzz-one" => FuzzOne(a),
                "verify" => VerifyCmd(a),
                "shell-test" => ShellTestCmd(a),
                "mono-check" => MonoCheck(a),
                "bake" => BakeCmd(a),
                "profile" => ProfileCmd(a),
                "png-check" => PngCheck(a),
                "svg-check" => Check(a, RenderGolden.CheckSvgs),
                "sprite-check" => Check(a, RenderGolden.CheckSprite),
                "sim" => SimCmd(a),
                "shore" => ShoreBench.Run(a.Get("land") ?? Path.Combine(Root(a), "..", "Content", "World", "land-10m.bin"), a.Int("points", 100_000)),
                "scenario" when a.Has("play") => ScenarioPlay.Run(a.Positional.FirstOrDefault() ?? Path.Combine(Root(a), "..", "Content", "Scenarios", "jutland.json"),
                    a.Get("settings") ?? Path.Combine(Root(a), "..", "Content", "Sim", "settings.json"),
                    a.Get("land") ?? Path.Combine(Root(a), "..", "Content", "World", "land-10m.bin"), Path.Combine(Root(a), "designs"),
                    a.Int("threads", 0), !a.Has("no-land"), a.Int("seeds", 1), D(a, "fast", 1000), D(a, "fine", 1)),
                "scenario" => ScenarioRun.Run(a.Positional.FirstOrDefault() ?? Path.Combine(Root(a), "..", "Content", "Scenarios", "jutland.json"),
                    a.Get("settings") ?? Path.Combine(Root(a), "..", "Content", "Sim", "settings.json"),
                    a.Get("land") ?? Path.Combine(Root(a), "..", "Content", "World", "land-10m.bin"), Path.Combine(Root(a), "designs"),
                    double.Parse(a.Get("minutes", "60")!, System.Globalization.CultureInfo.InvariantCulture), a.Int("threads", 0), a.Int("every", 5),
                    double.Parse(a.Get("at", "10")!, System.Globalization.CultureInfo.InvariantCulture), !a.Has("no-land"), a.Has("replay"),
                    a.Get("watch")?.Split(',').Select(int.Parse).ToArray(),
                    double.Parse(a.Get("speed", "0")!, System.Globalization.CultureInfo.InvariantCulture)),
                "duel" => Duel.Run(a.Positional.ElementAtOrDefault(0) ?? "super_dreadnought", a.Positional.ElementAtOrDefault(1) ?? a.Positional.ElementAtOrDefault(0) ?? "super_dreadnought",
                    n => LoadDesign(File.Exists(n) ? n : Path.Combine(Root(a), "designs", n + ".json")),
                    a.Get("settings") ?? Path.Combine(Root(a), "..", "Content", "Sim", "settings.json"),
                    new Duel.Options(D(a, "range", 16_000 * 0.9144), a.Int("level", -1), a.Int("runs", 20), D(a, "minutes", 15), D(a, "speed", 25),
                        D(a, "target-speed", 25), D(a, "bearing", 90), D(a, "course", 0),
                        Enum.Parse<Fleetwright.Sim.Core.CrewQuality>(a.Get("crew", "trained")!, ignoreCase: true), a.Int("seed", 1), a.Int("battery", 0), a.Has("trace"), a.Has("damage"))),
                "hits" => HitsBench.Run(a.Positional.ElementAtOrDefault(0) ?? "dreadnought", n => LoadDesign(Resolve(a, n)),
                    a.Get("settings") ?? Path.Combine(Root(a), "..", "Content", "Sim", "settings.json"),
                    new HitsBench.Options(a.Get("shooter"), a.Int("battery", 0), a.Get("kind"), D(a, "range", 12_000), D(a, "aspect", 90), a.Int("n", 2000),
                        a.Int("per-ship", 1), a.Int("seed", 1), a.Int("show", 0), D(a, "settle", 30), a.Get("loadout"))),
                "subdivision" => SubdivisionBench.Run(a.Positional.Count > 0 ? a.Positional : ["destroyer"], n => LoadDesign(Resolve(a, n)), a.Has("rooms")),
                "protection" => ProtectionBench.Run(a.Positional.Count > 0 ? a.Positional : ["destroyer"], n => LoadDesign(Resolve(a, n)), a.Int("shells", 400),
                    a.Int("seed", 1), a.Has("summary")),
                "torpedo" => TorpedoBench.Run(a.Positional.ElementAtOrDefault(0) ?? "super_dreadnought", n => LoadDesign(Resolve(a, n)),
                    a.Get("settings") ?? Path.Combine(Root(a), "..", "Content", "Sim", "settings.json"),
                    new TorpedoBench.Options(a.Get("shooter") ?? "destroyer", a.Get("at") switch
                        {
                            null => double.NaN, "mid" => 0.5, "bow" => 0.9, "stern" => 0.1,
                            var v => double.Parse(v, System.Globalization.CultureInfo.InvariantCulture),
                        }, D(a, "depth", 0), a.Int("n", 200), a.Int("per-ship", 1), a.Int("seed", 1), D(a, "settle", 60), D(a, "range", 0), D(a, "aspect", 90),
                        D(a, "target-speed", 15), a.Int("spread", 4), a.Int("runs", 20))),
                "flood" => FloodBench.Run(a.Positional.ElementAtOrDefault(0) ?? "destroyer", n => LoadDesign(Resolve(a, n)),
                    a.Get("settings") ?? Path.Combine(Root(a), "..", "Content", "Sim", "settings.json"),
                    new FloodBench.Options(a.Get("hole"), a.Get("rooms"), D(a, "minutes", 60), D(a, "every", 120), a.Int("step", 0), a.Has("cells"))),
                "ballistics" => BallisticsBench.Run(a.Positional, a.Has("research"), n => LoadDesign(File.Exists(n) ? n : Path.Combine(Root(a), "designs", n + ".json"))),
                "ammo-templates" => AmmoTemplates.Run(a.Positional, a.Has("dry"), a.Has("outfits")),
                "templates" => TemplatesWriter.Run(Path.Combine(Root(a), "..")),
                "weapons" => Weapons.Run(a.Positional, a.Has("research"), n => LoadDesign(File.Exists(n) ? n : Path.Combine(Root(a), "designs", n + ".json"))),
                "matchup" => MatchupBench.Run(a.Positional.ElementAtOrDefault(0) ?? "arethusa", a.Positional.ElementAtOrDefault(1) ?? "v25",
                    n => LoadDesign(Resolve(a, n)), a.Get("settings") ?? Path.Combine(Root(a), "..", "Content", "Sim", "settings.json"), D(a, "range", 0),
                    D(a, "share", 0.6), D(a, "sight", double.PositiveInfinity)),
                "trials" => SeaTrials.Run(a.Positional, a.Get("settings") ?? Path.Combine(Root(a), "..", "Content", "Sim", "settings.json"), a.Get("out")),
                _ => Fail($"unknown command {args[0]}\n\n{Usage}"),
            };
        }
        catch (ArgumentException e)
        {
            return Fail(e.Message);
        }
    }

    static int Fail(string msg)
    {
        Console.Error.WriteLine(msg);
        return 2;
    }

    sealed class Args
    {
        public readonly List<string> Positional = [];
        readonly Dictionary<string, string?> flags = new(StringComparer.Ordinal);

        public Args(IEnumerable<string> args)
        {
            var list = args.ToList();
            for (int i = 0; i < list.Count; i++)
            {
                string s = list[i];
                if (!s.StartsWith("--", StringComparison.Ordinal))
                {
                    Positional.Add(s);
                    continue;
                }
                int eq = s.IndexOf('=');
                if (eq > 0)
                    flags[s[2..eq]] = s[(eq + 1)..];
                else if (s is "--no-limits" or "--no-sprites" or "--previews" or "--gratings" or "--no-land" or "--replay" or "--research" or "--trace" or "--damage" or "--cells" or "--play" or "--dry" or "--outfits" or "--summary")   // the switches
                    flags[s[2..]] = null;
                else if (i + 1 < list.Count)
                    flags[s[2..]] = list[++i];
                else
                    throw new ArgumentException($"{s} needs a value");
            }
        }

        public bool Has(string k) => flags.ContainsKey(k);
        public string? Get(string k, string? def = null) => flags.TryGetValue(k, out var v) ? v : def;
        public int Int(string k, int def) => flags.TryGetValue(k, out var v) && v != null ? int.Parse(v) : def;
    }

    static string Root(Args a)
    {
        if (a.Get("root") is string r)
            return r;
        var dir = new DirectoryInfo(Directory.GetCurrentDirectory());
        while (dir != null && !Directory.Exists(Path.Combine(dir.FullName, "shipgen", "golden")))
            dir = dir.Parent;
        return dir != null ? Path.Combine(dir.FullName, "shipgen")
            : throw new ArgumentException("no shipgen/golden folder above the current directory: give --root");
    }

    static Shipgen.Design LoadDesign(string path) => Shipgen.Design.Load(path);

    /// <summary>A design by path, or by name among the shipped designs and then the scenarios'.</summary>
    static string Resolve(Args a, string n)
    {
        if (File.Exists(n))
            return n;
        string shipped = Path.Combine(Root(a), "designs", n + ".json");
        return File.Exists(shipped) ? shipped : Path.Combine(Root(a), "..", "Content", "Scenarios", "Designs", n + ".json");
    }

    static double D(Args a, string k, double def) => a.Get(k) is { } v ? double.Parse(v, System.Globalization.CultureInfo.InvariantCulture) : def;

    /// <summary>A design file as plain JSON data, for the fuzzer to mutate.</summary>
    static JsonObject LoadRaw(string path) => JsonFile.Load(path)!.AsObject();

    static int Validate(Args a)
    {
        bool limits = !a.Has("no-limits");
        int bad = 0;
        foreach (var p in a.Positional)
        {
            var d = LoadDesign(p);
            var errs = ShipDesign.Validate(d, limits).Concat(Looks.Validate(d)).ToList();
            Console.WriteLine(errs.Count == 0 ? $"{p}: ok" : $"{p}: invalid input:\n  " + string.Join("\n  ", errs));
            bad += errs.Count > 0 ? 1 : 0;
        }
        return bad > 0 ? 1 : 0;
    }

    static int Design(Args a)
    {
        bool limits = !a.Has("no-limits");
        string outDir = a.Get("out", "out_designs")!;
        double scale = double.Parse(a.Get("scale", "10")!, System.Globalization.CultureInfo.InvariantCulture);
        int mips = a.Int("mips", 5);
        GpuBaker? gpu = null;
        int crashed = 0;
        foreach (var p in a.Positional)
        {
            var design = LoadDesign(p);
            var errs = ShipDesign.Validate(design, limits).Concat(Looks.Validate(design)).ToList();
            if (errs.Count > 0)
            {
                Console.WriteLine($"{p}: invalid input:\n  " + string.Join("\n  ", errs));
                continue;
            }
            Ship ship;
            try
            {
                ship = ShipDesign.Build(design);
            }
            catch (NotImplementedException e)
            {
                Console.WriteLine($"{p}: not ported yet ({e.Message})");
                return 1;
            }
            catch (Exception e)
            {
                Console.WriteLine($"{p}: the generator broke on this design: {e.GetType().Name}: {e.Message}");
                crashed++;
                continue;
            }
            string dir = Path.Combine(outDir, design.Id!);
            var rep = ship.Report;
            var json = ShipgenJson.Default;
            JsonFile.Save(Path.Combine(dir, "report.json"), rep, json.Report, 2);
            JsonFile.Save(Path.Combine(dir, "hitboxes.json"), ship.Hitboxes, json.Hitboxes);
            JsonFile.Save(Path.Combine(dir, "ship.json"), ship, json.Ship);
            Console.WriteLine(Summary(design, rep));
            foreach (var e in rep.Errors)
                Console.WriteLine("      ERROR: " + e);
            foreach (var w in rep.Warnings)
                Console.WriteLine("      warn:  " + w);
            if (a.Has("no-sprites"))
                continue;
            gpu ??= new GpuBaker();
            var sp = ShipSprites.Build(ship, scale, mips);
            var baked = ShipBake.Bake(sp, gpu);
            ShipBake.Save(sp, baked, dir);
            if (a.Has("previews"))
            {
                Png.Save(Path.Combine(dir, "preview_rest.png"), Preview.Rest(sp, baked));
                Png.Save(Path.Combine(dir, "preview_starboard.png"), Preview.Starboard(sp, baked));
            }
        }
        gpu?.Dispose();
        return crashed > 0 ? 1 : 0;
    }

    /// <summary>design.py's one-line summary.</summary>
    static string Summary(Shipgen.Design design, Report rep)
    {
        var res = rep.Results;
        return $"{design.Id,14}: {(rep.Valid ? "OK " : "BAD")} " +
               $"{res.LengthM,5:F1} x {res.BeamM,4:F1} m  " +
               $"std {res.StandardDisplacementT,6:N0} t " +
               $"full {res.FullDisplacementT,6:N0} t  T {res.DraughtM,5} m  " +
               $"{res.PowerShp,9:N0} shp  " +
               $"GM {res.GmFullM,5}  trim {res.TrimM:+0.00;-0.00}  " +
               $"shift {res.LayoutShiftM:+0.0;-0.0}";
    }

    static List<GoldenCase> SelectCases(string root, IReadOnlyList<string> pats)
    {
        var all = GoldenCases.Load(root);
        if (pats.Count == 0)
            return all;
        return all.Where(c => pats.Any(p => p.EndsWith('*') ? c.Name.StartsWith(p[..^1], StringComparison.Ordinal)
                                                             : c.Name == p)).ToList();
    }

    static int Capture(Args a)
    {
        string root = Root(a);
        string outDir = a.Get("out", "capture")!;
        var cases = SelectCases(root, a.Positional);
        var sw = Stopwatch.StartNew();
        int n = 0;
        Parallel.ForEach(cases, new ParallelOptions { MaxDegreeOfParallelism = a.Int("jobs", Environment.ProcessorCount / 2) },
            c =>
            {
                var rec = GoldenCases.Capture(LoadDesign(c.DesignPath));
                JsonFile.Save(Path.Combine(outDir, c.Name + ".json.gz"), rec);
                Console.WriteLine($"[{Interlocked.Increment(ref n),3}/{cases.Count}] {c.Name}");
            });
        Console.WriteLine($"{cases.Count} cases in {sw.Elapsed.TotalSeconds:F1} s -> {outDir}");
        return 0;
    }

    static int GoldenDiffCmd(Args a)
    {
        if (a.Positional.Count != 2)
            throw new ArgumentException("golden-diff GOLDEN OUT");
        string g = a.Positional[0], o = a.Positional[1];
        int show = a.Int("show", 5);
        var pairs = new List<(string Name, string G, string O)>();
        if (File.Exists(g))
            pairs.Add((Path.GetFileName(g), g, o));
        else
            foreach (var f in Directory.GetFiles(g, "*.json.gz").Order(StringComparer.Ordinal))
                pairs.Add((Path.GetFileName(f)[..^8], f, Path.Combine(o, Path.GetFileName(f))));
        int bad = 0;
        foreach (var (name, gf, of) in pairs)
        {
            if (!File.Exists(of))
            {
                Console.WriteLine($"{name}: missing in {o}");
                bad++;
                continue;
            }
            var gr = JsonFile.Load(gf)!.AsObject();
            var orr = JsonFile.Load(of)!.AsObject();
            gr.Remove("build_s");
            orr.Remove("build_s");
            var diffs = Shipgen.Golden.GoldenDiff.Compare(gr, orr, show);
            if (diffs.Count > 0)
            {
                bad++;
                Console.WriteLine($"{name}: differs");
                foreach (var d in diffs)
                    Console.WriteLine("  " + d.ToString().Replace("\n", "\n  "));
            }
        }
        Console.WriteLine($"{bad} of {pairs.Count} cases differ");
        return bad > 0 ? 1 : 0;
    }

    static int Bench(Args a)
    {
        string root = Root(a);
        var cases = SelectCases(root, a.Positional);
        int repeat = a.Int("repeat", 3);
        var pyTimes = JsonFile.Load(Path.Combine(root, "golden", "design", "capture.json"))!["build_s"]!.AsObject();
        double totC = 0, totP = 0;
        foreach (var c in cases)
        {
            var design = LoadDesign(c.DesignPath);
            if (ShipDesign.Validate(design, false).Count > 0)
                continue;
            double best = double.MaxValue, bestC = double.MaxValue;
            long alloc = 0, allocC = 0;
            for (int i = 0; i < repeat; i++)
            {
                long a0 = GC.GetAllocatedBytesForCurrentThread();
                var sw = Stopwatch.StartNew();
                var ship = ShipDesign.Build(design);
                best = Math.Min(best, sw.Elapsed.TotalSeconds);
                alloc = GC.GetAllocatedBytesForCurrentThread() - a0;
                // and compiled for the sim (battle plan step 2.1)
                a0 = GC.GetAllocatedBytesForCurrentThread();
                sw.Restart();
                ShipClassCompiler.Compile(ship);
                bestC = Math.Min(bestC, sw.Elapsed.TotalSeconds);
                allocC = GC.GetAllocatedBytesForCurrentThread() - a0;
            }
            double p = (double?)pyTimes[c.Name] ?? double.NaN;
            totC += best;
            if (!double.IsNaN(p))
                totP += p;
            Console.WriteLine($"{c.Name,-24} {best,8:F3} s   python {p,8:F3} s   x{p / best,6:F1}" +
                              $"   {alloc / 1e6,6:F0} MB allocated   ShipClass {bestC * 1000,6:F1} ms {allocC / 1e6,5:F1} MB");
        }
        Console.WriteLine($"total {totC:F2} s, python {totP:F2} s (x{totP / totC:F1})");
        return 0;
    }

    static int SimCmd(Args a)
    {
        var from = DateTime.ParseExact(a.Get("from", "1916-05-31T14:00")!, ["yyyy-MM-dd'T'HH:mm", "yyyy-MM-dd"],
            null, System.Globalization.DateTimeStyles.AdjustToUniversal | System.Globalization.DateTimeStyles.AssumeUniversal);
        double speed = double.Parse(a.Get("speed", "30000")!), seconds = double.Parse(a.Get("seconds", "3")!);
        var world = new World(WorldTime.FromUtc(from), (ulong)a.Int("seed", 1));
        using var host = new SimHost(world);
        host.Start();
        host.Send(Command.SetSpeed(speed));
        var sw = Stopwatch.StartNew();
        double last = 0, nextPrint = 0.5;
        long? alloc0 = null;
        while (sw.Elapsed.TotalSeconds < seconds)
        {
            Thread.Sleep(16);
            double now = sw.Elapsed.TotalSeconds;
            host.Elapse(now - last);
            last = now;
            var s = host.Latest();
            if (s.Error != null)
                return Fail(s.Error);
            if (now < nextPrint)
                continue;
            nextPrint += 0.5;
            alloc0 ??= s.SimAllocatedBytes;
            var (lat, lon) = Earth.LatLon(s.Sun);
            Console.WriteLine($"{s.Time}  asked {s.Speed,7:G5}x  achieved {s.AchievedSpeed,9:F0}x  " +
                              $"sun overhead {lat,6:F2}, {lon,7:F2}  sim thread +{s.SimAllocatedBytes - alloc0} B");
        }
        return 0;
    }

    static int GoldenCheck(Args a)
    {
        string root = Root(a);
        var cases = SelectCases(root, a.Positional);
        int show = a.Int("show", 3);
        var sw = Stopwatch.StartNew();
        var results = new (GoldenCase C, List<Difference>? D, string? Err)[cases.Count];
        Parallel.For(0, cases.Count, new ParallelOptions { MaxDegreeOfParallelism = a.Int("jobs", Environment.ProcessorCount / 2) },
            i =>
            {
                try
                {
                    results[i] = (cases[i], GoldenCases.Check(root, cases[i], show), null);
                }
                catch (Exception e)
                {
                    results[i] = (cases[i], null, $"{e.GetType().Name}: {e.Message}\n{e.StackTrace}");
                }
            });
        int ok = 0;
        foreach (var (c, d, err) in results)
        {
            if (err != null)
            {
                Console.WriteLine($"FAIL {c.Name}: {err.Split('\n').Take(6).Aggregate((x, y) => x + "\n     " + y)}");
                continue;
            }
            if (d!.Count == 0)
            {
                ok++;
                continue;
            }
            Console.WriteLine($"DIFF {c.Name}");
            foreach (var x in d)
                Console.WriteLine("     " + x.ToString().Replace("\n", "\n     "));
        }
        Console.WriteLine($"{ok} of {cases.Count} cases match ({sw.Elapsed.TotalSeconds:F1} s)");
        return ok == cases.Count ? 0 : 1;
    }

    static int GoldenUpdate(Args a)
    {
        string root = Root(a);
        var cases = SelectCases(root, a.Positional);
        var design = new System.Collections.Concurrent.ConcurrentBag<string>();
        Parallel.ForEach(cases, new ParallelOptions { MaxDegreeOfParallelism = a.Int("jobs", Environment.ProcessorCount / 2) }, c =>
        {
            if (GoldenCases.Update(root, c))
                design.Add(c.Name);
        });
        var (svg, sprite) = RenderGolden.Update(root, cases);
        Console.WriteLine($"rewritten: {design.Count} design, {svg.Count} svg, {sprite.Count} sprite.json of {cases.Count} cases");
        foreach (var (kind, names) in new[] { ("design", design.Order(StringComparer.Ordinal).ToList()), ("svg", svg), ("sprite", sprite) })
            if (names.Count > 0)
                Console.WriteLine($"  {kind}: {string.Join(" ", names)}");
        return 0;
    }

    static int Draw(Args a)
    {
        string outDir = a.Get("out", "out_sprites")!;
        double scale = double.Parse(a.Get("scale", "10")!, System.Globalization.CultureInfo.InvariantCulture);
        int mips = a.Int("mips", 5);
        foreach (var path in a.Positional)
        {
            var design = LoadDesign(path);
            var sp = ShipSprites.Build(ShipDesign.Build(design), scale, mips);
            string dir = Path.Combine(outDir, design.Id!);
            Directory.CreateDirectory(Path.Combine(dir, "turrets"));
            JsonFile.Save(Path.Combine(dir, "sprite.json"), sp.Meta, RenderJson.Default.SpriteMeta, 2);
            File.WriteAllText(Path.Combine(dir, "hull.svg"), SvgWriter.Write(sp.Hull));
            File.WriteAllText(Path.Combine(dir, "height.svg"), SvgWriter.Write(sp.Height));
            foreach (var (tid, sc) in sp.Turrets)
                File.WriteAllText(Path.Combine(dir, "turrets", tid + ".svg"), SvgWriter.Write(sc));
            var size = sp.Meta.SizePx;
            Console.WriteLine($"drew {design.Id}: {size[0]}x{size[1]} px, {sp.Turrets.Count} turret types, " +
                              $"{sp.Clutter.Count} clutter items -> {dir}");
        }
        return 0;
    }

    /// <summary>Run a per-case check over the selected cases in parallel and print each failing case.</summary>
    static int Check(Args a, Func<string, GoldenCase, int, List<string>> check)
    {
        string root = Root(a);
        var cases = SelectCases(root, a.Positional);
        int show = a.Int("show", 3);
        var sw = Stopwatch.StartNew();
        var results = new (GoldenCase C, List<string>? D, string? Err)[cases.Count];
        Parallel.For(0, cases.Count, new ParallelOptions { MaxDegreeOfParallelism = a.Int("jobs", Environment.ProcessorCount / 2) },
            i =>
            {
                try
                {
                    results[i] = (cases[i], check(root, cases[i], show), null);
                }
                catch (Exception e)
                {
                    results[i] = (cases[i], null, $"{e.GetType().Name}: {e.Message}\n{e.StackTrace}");
                }
            });
        int ok = 0;
        foreach (var (c, d, err) in results)
        {
            if (err != null)
            {
                Console.WriteLine($"FAIL {c.Name}: {err.Split('\n').Take(8).Aggregate((x, y) => x + "\n     " + y)}");
                continue;
            }
            if (d!.Count == 0)
            {
                ok++;
                continue;
            }
            Console.WriteLine($"DIFF {c.Name}");
            foreach (var x in d)
                Console.WriteLine("     " + x.Replace("\n", "\n     "));
        }
        Console.WriteLine($"{ok} of {cases.Count} cases match ({sw.Elapsed.TotalSeconds:F1} s)");
        return ok == cases.Count ? 0 : 1;
    }

    static int BakeCmd(Args a)
    {
        string outDir = a.Get("out", "out_sprites")!;
        double scale = double.Parse(a.Get("scale", "10")!, System.Globalization.CultureInfo.InvariantCulture);
        int mips = a.Int("mips", 5);
        using var gpu = new GpuBaker();
        Console.WriteLine($"GPU: {gpu.Driver}, {gpu.Samples}");
        foreach (var path in a.Positional)
        {
            var design = LoadDesign(path);
            var sw = Stopwatch.StartNew();
            var sp = ShipSprites.Build(ShipDesign.Build(design), scale, mips);
            double tBuild = sw.Elapsed.TotalSeconds;
            var baked = ShipBake.Bake(sp, gpu);
            double tBake = sw.Elapsed.TotalSeconds - tBuild;
            string dir = Path.Combine(outDir, design.Id!);
            ShipBake.Save(sp, baked, dir);
            Console.WriteLine($"baked {design.Id}: {baked.Hull.Width}x{baked.Hull.Height} px, {sp.Turrets.Count} turret types " +
                              $"(build+draw {tBuild:F2} s, bake {tBake:F2} s, save {sw.Elapsed.TotalSeconds - tBuild - tBake:F2} s) -> {dir}");
        }
        return 0;
    }

    static int ProfileCmd(Args a)
    {
        string outDir = a.Get("out", "out_profiles")!;
        double scale = double.Parse(a.Get("scale", "10")!, System.Globalization.CultureInfo.InvariantCulture);
        string? navy = a.Get("navy"), era = a.Get("era");
        var look = navy != null || era != null ? new LookInput { Navy = navy, Era = era } : null;
        using var gpu = new GpuBaker();
        foreach (var path in a.Positional)
        {
            var design = LoadDesign(path);
            var sw = Stopwatch.StartNew();
            var p = ShipProfile.Build(ShipDesign.Build(design), scale, look);
            double tDraw = sw.Elapsed.TotalSeconds;
            var (full, sil) = ProfileBake.Bake(p, gpu);
            string dir = Path.Combine(outDir, design.Id!);
            ProfileBake.Save(dir, full, sil);
            File.WriteAllText(Path.Combine(dir, "profile.svg"), SvgWriter.Write(p.Full));
            File.WriteAllText(Path.Combine(dir, "silhouette.svg"), SvgWriter.Write(p.Silhouette));
            Console.WriteLine($"profiled {design.Id}: {full.Width}x{full.Height} px, silhouette {sil.Width}x{sil.Height} px " +
                              $"(build+draw {tDraw:F2} s, bake {sw.Elapsed.TotalSeconds - tDraw:F2} s) -> {dir}");
        }
        return 0;
    }

    static int PngCheck(Args a)
    {
        string root = Root(a);
        string? keep = a.Get("out");
        var cases = SelectCases(root, a.Positional)
            .Where(c => (string?)RenderGolden.State(root, "sprite", c.Name) == "ok")
            .Where(c => File.Exists(Path.Combine(root, "golden", "sprite", c.Name, "hull.png"))).ToList();   // cases since the port have no PNGs
        using var gpu = new GpuBaker();
        Console.WriteLine($"GPU: {gpu.Driver}, {gpu.Samples}; {cases.Count} designs");
        // build and draw in parallel, bake one at a time on the one device
        var sprites = new ShipSprites[cases.Count];
        Parallel.For(0, cases.Count, i => sprites[i] = RenderGolden.Draw(cases[i], outline: 1));   // Python's inks
        double worst = 1;
        var sw = Stopwatch.StartNew();
        Console.WriteLine($"{"case",-22} {"hull",7} {"height",7} {"turret",7} {"colour",7}");
        for (int i = 0; i < cases.Count; i++)
        {
            var c = cases[i];
            var b = ShipBake.Bake(sprites[i], gpu);
            if (keep != null)
                ShipBake.Save(sprites[i], b, Path.Combine(keep, c.Name));
            string gdir = Path.Combine(root, "golden", "sprite", c.Name);
            double hull = ShipBake.CoverageIoU(Png.Load(Path.Combine(gdir, "hull.png")), b.Hull);
            double height = ShipBake.CoverageIoU(Png.Load(Path.Combine(gdir, "height.png")), b.Height);
            double tur = b.Turrets.Count == 0 ? 1 : b.Turrets.Min(t => ShipBake.CoverageIoU(Png.Load(Path.Combine(gdir, "turrets", t.Key + ".png")), t.Value));
            double col = ShipBake.ColourDiff(Png.Load(Path.Combine(gdir, "hull.png")), b.Hull);
            double m = Math.Min(hull, Math.Min(height, tur));
            worst = Math.Min(worst, m);
            Console.WriteLine($"{c.Name,-22} {hull,7:F4} {height,7:F4} {tur,7:F4} {col,7:F2}{(m < 0.98 ? "  LOW" : "")}");
        }
        Console.WriteLine($"worst IoU {worst:F4} over {cases.Count} designs (bake {sw.Elapsed.TotalSeconds:F1} s)");
        return worst >= 0.98 ? 0 : 1;
    }

    /// <summary>The child of `fuzz`: build one mutant (IN.json) and write the outcome to OUT.txt, its first line the
    /// outcome and the rest the detail.</summary>
    static int FuzzOne(Args a)
    {
        var (outcome, detail) = Fuzz.Check(LoadRaw(a.Positional[0]), !a.Has("no-limits"));
        File.WriteAllText(a.Positional[1], outcome + "\n" + detail);
        return 0;
    }

    static int FuzzCmd(Args a)
    {
        int cases = a.Int("cases", 150), jobs = a.Int("jobs", Environment.ProcessorCount);
        int timeout = a.Int("timeout", 120);
        double memGb = double.Parse(a.Get("mem-gb", "3")!, System.Globalization.CultureInfo.InvariantCulture);
        double slowS = double.Parse(a.Get("slow", "30")!, System.Globalization.CultureInfo.InvariantCulture);
        bool limits = !a.Has("no-limits");
        string mode = a.Get("mode", "all")!, outDir = a.Get("out", "fuzz_out")!;
        var rng = new Random(a.Int("seed", 1));
        var bases = a.Positional.Select(p => (Path: p, D: LoadRaw(p))).ToList();
        if (bases.Count == 0)
            throw new ArgumentException("fuzz DESIGN.json...");
        var choices = Fuzz.CorpusChoices(bases.Select(b => b.D));
        var work = Directory.CreateTempSubdirectory("shipgen_fuzz_").FullName;
        var muts = Enumerable.Range(0, cases).Select(i =>
        {
            var (src, b) = bases[i % bases.Count];
            var (d, ch) = Fuzz.Mutate(b, rng, limits, choices, mode);
            return (Case: i, Src: src, D: d, Changes: ch);
        }).ToList();
        var counts = new System.Collections.Concurrent.ConcurrentDictionary<string, int>();
        var slow = new System.Collections.Concurrent.ConcurrentBag<(double S, int Case, string Src)>();
        int bad = 0;
        object print = new();
        string self = Environment.ProcessPath!;
        bool viaDotnet = Path.GetFileNameWithoutExtension(self).Equals("dotnet", StringComparison.OrdinalIgnoreCase);
        Parallel.ForEach(muts, new ParallelOptions { MaxDegreeOfParallelism = jobs }, m =>
        {
            string inPath = Path.Combine(work, $"{m.Case}.json"), resPath = Path.Combine(work, $"{m.Case}.txt");
            JsonFile.Save(inPath, m.D);
            var psi = new ProcessStartInfo(self) { RedirectStandardError = true, RedirectStandardOutput = true, UseShellExecute = false };
            if (viaDotnet)
                psi.ArgumentList.Add(Path.Combine(AppContext.BaseDirectory, "shipgen.dll"));
            foreach (var x in new[] { "fuzz-one", inPath, resPath })
                psi.ArgumentList.Add(x);
            if (!limits)
                psi.ArgumentList.Add("--no-limits");
            var sw = Stopwatch.StartNew();
            using var p = Process.Start(psi)!;
            var err = p.StandardError.ReadToEndAsync();
            string outcome, detail;
            long cap = (long)(memGb * (1L << 30)), peak = 0;
            while (true)
            {
                if (p.WaitForExit(100))
                    break;
                try
                {
                    p.Refresh();
                    peak = Math.Max(peak, p.PeakWorkingSet64);
                }
                catch (InvalidOperationException)   // it exited just now
                {
                }
                if (peak > cap || sw.Elapsed.TotalSeconds > timeout)
                {
                    try { p.Kill(true); } catch (InvalidOperationException) { }
                    p.WaitForExit();
                    break;
                }
            }
            double dt = sw.Elapsed.TotalSeconds;
            if (File.Exists(resPath))
            {
                var t = File.ReadAllText(resPath);
                int nl = t.IndexOf('\n');
                (outcome, detail) = (t[..nl], t[(nl + 1)..]);
            }
            else if (dt > timeout)
                (outcome, detail) = ("hang", $"no result after {timeout} s");
            else if (peak > cap)
                (outcome, detail) = ("memory", $"over {memGb} GB");
            else
                (outcome, detail) = ("crash", $"exit {p.ExitCode}: {err.Result}");
            counts.AddOrUpdate(outcome, 1, (_, n) => n + 1);
            bool fail = outcome is "crash" or "memory" or "hang";
            if (!fail && dt > slowS)
            {
                slow.Add((dt, m.Case, m.Src));
                detail = $"{dt:F0} s ({outcome})";
                outcome = "slow";
            }
            if (fail || outcome == "slow")
            {
                Directory.CreateDirectory(outDir);
                string stem = Path.Combine(outDir, $"{(string?)m.D["id"]}_{m.Case}");
                JsonFile.Save(stem + ".json", m.D);
                File.WriteAllText(stem + ".txt", $"{m.Src}, changed: {string.Join("; ", m.Changes)}\n\n{outcome}: {detail}\n");
            }
            if (fail)
            {
                Interlocked.Increment(ref bad);
                lock (print)
                    Console.WriteLine($"{outcome.ToUpperInvariant(),7} {(string?)m.D["id"]}_{m.Case}.json  ({string.Join("; ", m.Changes)})\n" +
                                      $"        {detail.Split('\n')[0]}");
            }
        });
        Directory.Delete(work, true);
        Console.WriteLine("outcomes: " + string.Join(", ", counts.OrderBy(kv => kv.Key, StringComparer.Ordinal).Select(kv => $"{kv.Key} {kv.Value}")));
        foreach (var (sec, c, src) in slow.OrderByDescending(x => x.S).Take(10))
            Console.WriteLine($"   slow: case {c} ({src}) {sec:F0} s");
        return bad > 0 ? 1 : 0;
    }

    static int VerifyCmd(Args a)
    {
        double worst = 1;
        foreach (var dir in a.Positional)
        {
            var r = Verify.Check(dir);
            Console.WriteLine(r.Text);
            worst = Math.Min(worst, r.Worst);
        }
        Console.WriteLine(worst > 0.85 ? "\nALL OK" : "\nSOME MISMATCH");
        return worst > 0.85 ? 0 : 1;
    }

    static readonly string[] MonoDefault =
    [
        "bismarck", "yamato", "dreadnought", "kongo", "heavy_cruiser", "light_cruiser", "destroyer", "fubuki", "nelson",
        "dunkerque", "armoured_cruiser", "mikasa", "fleet_carrier", "pt_boat", "tanker",
    ];

    static int MonoCheck(Args a)
    {
        string root = Root(a);
        int steps = a.Int("steps", 4), show = a.Int("show", 30);
        var names = a.Positional.Count > 0 ? a.Positional : [.. MonoDefault];
        var designs = names.Select(n => (Name: Path.GetFileNameWithoutExtension(n),
            Design: LoadDesign(File.Exists(n) ? n : Path.Combine(root, "designs", n + ".json")))).ToList();
        var work = designs.SelectMany(d => Monotone.Levers.Select(l => (d.Name, d.Design, Lever: l))).ToList();
        var sweeps = new List<Monotone.Sweep>?[work.Count];
        var sw = Stopwatch.StartNew();
        Parallel.For(0, work.Count, new ParallelOptions { MaxDegreeOfParallelism = a.Int("jobs", Environment.ProcessorCount) },
            i => sweeps[i] = Monotone.Check(work[i].Name, work[i].Design, steps, [work[i].Lever]));
        var all = sweeps.SelectMany(s => s!).ToList();
        int builds = all.Sum(s => s.Steps.Count), nRev = all.Sum(s => s.Reversals.Count), nLump = all.Sum(s => s.Lumps.Count);
        // one line per finding: the step, how far it went against the usual step, and what the hull did
        var found = new List<(double Size, string Line)>();
        foreach (var s in all)
        {
            var abs = Enumerable.Range(1, s.Steps.Count - 1).Select(i => Math.Abs(s.Steps[i].StdT - s.Steps[i - 1].StdT)).Order().ToList();
            double median = Math.Max(1, abs[abs.Count / 2]);
            foreach (int i in s.Reversals.Union(s.Lumps).Order())
            {
                var (p, q) = (s.Steps[i - 1], s.Steps[i]);
                long dt = q.StdT - p.StdT;
                found.Add((Math.Abs(dt) / (double)p.StdT, $"{s.Design,-16} {s.Lever,-13} {p.Value,7}->{q.Value,-7} {dt,+7} t " +
                    $"({100.0 * dt / p.StdT,+5:F1}%, {dt / median,+5:F1} x the usual {median:F0} t)  L {q.LengthM - p.LengthM,+6:F2}  " +
                    $"B {q.BeamM - p.BeamM,+5:F2}  {(s.Reversals.Contains(i) ? "reversal" : "lump")}"));
            }
        }
        foreach (var (_, line) in found.OrderByDescending(f => f.Size).Take(show))
            Console.WriteLine(line);
        foreach (var g in all.GroupBy(s => s.Lever))
            Console.WriteLine($"{g.Key,-14} {g.Sum(s => s.Reversals.Count),3} reversals {g.Sum(s => s.Lumps.Count),3} lumps");
        Console.WriteLine($"{nRev} reversals, {nLump} lumps over {all.Count} sweeps ({builds} builds, {sw.Elapsed.TotalSeconds:F0} s)");
        return nRev + nLump > 0 ? 1 : 0;
    }

    static int ShellTestCmd(Args a)
    {
        int shells = a.Int("shells", 200000), seed = a.Int("seed", 1), show = a.Int("show", 15), leaky = 0;
        foreach (var p in a.Positional)
        {
            var design = LoadDesign(p);
            if (ShipDesign.Validate(design, !a.Has("no-limits")) is { Count: > 0 } errs)
            {
                Console.WriteLine($"{p}: invalid input:\n  " + string.Join("\n  ", errs));
                continue;
            }
            var sw = Stopwatch.StartNew();
            var cls = ShipClassCompiler.Compile(ShipDesign.Build(design));
            if (a.Int("path", 0) is > 0 and int paths)
            {
                // the first shots' paths, drawn as the run draws them
                var st = new ShellTest(cls, a.Has("gratings"), Underwater(a));
                var path = new HitPath();
                var stream = RandomStream.FromSeed((ulong)seed);
                for (int k = 0; k < paths && st.CitadelCells.Length > 0; k++)
                {
                    var shot = st.Draw(stream.Derive((ulong)k));
                    var o = st.Trace(shot, out var lk, path);
                    Console.WriteLine($"{design.Id ?? p} shot {k}: bearing {shot.BearingDeg:F1}°, fall {shot.FallDeg:F1}° into {cls.CellId(shot.Cell)}: {o}" +
                                      (o == ShellTest.Outcome.Leak ? $" ({st.CauseText(lk)})" : ""));
                    Console.Write(path.Text(cls));
                }
                continue;
            }
            var outcomes = a.Get("dump") is null ? null : new ShellTest.Outcome[shells];
            var r = ShellTest.Run(cls, shells, (ulong)seed, a.Has("gratings"), outcomes, Underwater(a));
            Console.Write(ShellTest.Text(design.Id ?? p, r, show));
            if (outcomes != null)
                DumpShots(a.Get("dump")!, design.Id ?? Path.GetFileNameWithoutExtension(p), outcomes, r);
            Console.WriteLine($"  ({sw.Elapsed.TotalSeconds:F1} s)");
            leaky += r.Leaks.Count > 0 ? 1 : 0;
        }
        Console.WriteLine(leaky == 0 ? "\nNO LEAKS" : $"\n{leaky} DESIGN(S) LEAK");
        return leaky == 0 ? 0 : 1;
    }

    static double Underwater(Args a) => double.Parse(a.Get("underwater", "0")!, System.Globalization.CultureInfo.InvariantCulture);

    /// <summary>shell-test --dump DIR: every shot's outcome, and a leak's cause and faces, one line a shot, in
    /// DIR/ID.txt: the record a new version of the tracer is compared against shot by shot.</summary>
    static void DumpShots(string dir, string id, ShellTest.Outcome[] outcomes, ShellTest.Result r)
    {
        Directory.CreateDirectory(dir);
        var leaks = r.Leaks.ToDictionary(l => l.Shot);
        using var w = new StreamWriter(Path.Combine(dir, id + ".txt"));
        for (int k = 0; k < outcomes.Length; k++)
            w.WriteLine(leaks.TryGetValue(k, out var l) ? $"{k} {outcomes[k]} {l.Cause} | {l.HoleFace} | {l.EntryFace}" : $"{k} {outcomes[k]}");
    }
}
