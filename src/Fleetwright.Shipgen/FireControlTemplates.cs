namespace Fleetwright.Shipgen;

/// <summary>The fire-control catalogue (docs/battle/fire-control.md F3): tech blocks of optics, computers and mounts, and
/// the presets, named fits of one of each that stand for the reference's levels (decision 1: "level 3" is a preset, not
/// a type). The one source of `Content/Templates/fire-control.json` and docs/shipgen/fire-control-templates.md
/// (`shipgen templates` writes both). A battery's fit names its blocks (`fire_control.BATTERY.fit`); a design copies the
/// blocks it names into `fire_control_tech`, and a name the design doesn't carry takes the catalogue's.
/// <list type="bullet">
/// <item>The optics' acuity, rigid base, flex, top magnification and mass: research/simulation/fire-control-designer.md
/// §2.1 and §2.5 (O1-O4); the instruments' magnification and readings a minute: fire-control-research.md §3.3 and §3.5;
/// the Zeiss stereo's 10″ from Lienau's ±65 m at 10 km (§3.3).</item>
/// <item>The computers: the trackers and their numbers as fire_control_ref.py LEVELS gives them (the sim's levels, F0-F2),
/// which designer_ref.py's C0-C6 reproduce; masses, crews and power from designer §2.3 and component-costs.md.</item>
/// <item>The mounts: designer §2.4 (M0-M4), the sights before directors from §2.4's telescope law (open 4′, early
/// telescope 2.5′, telescope 1.2′) as the levels' laying errors.</item>
/// </list>
/// A block is a machine or a make, never a year: the tech tree decides who has it. Blocks whose numbers stand in for a
/// radar's until the radar pass (decision 2) say so in their names.</summary>
public static class FireControlTemplates
{
    /// <summary>A block: its name, kind (optics, computer, mount), sources and value.</summary>
    public sealed record Template(string Name, string Kind, string Note, FcTechInput Value);

    /// <summary>A preset: its name, the blocks it names (optics, computer, mount) and what it stands for.</summary>
    public sealed record Preset(string Name, string Optics, string Computer, string Mount, string Note)
    {
        public IReadOnlyList<string> Fit => [Optics, Computer, Mount];
    }

    static Template O(string name, string kind, double delta, double rigid, double flex, double mag, double mMax, double perMin, double massK,
        string note, bool coated = false, bool gyro = false) =>
        new(name, "optics", note, new FcTechInput
        {
            Optics = new OpticsTechInput
            {
                Kind = kind, DeltaArcsec = delta, RigidBaseM = rigid, FlexExp = flex, Magnification = mag, MMax = mMax, ReadingsPerMin = perMin,
                Coated = coated, GyroStabilised = gyro, MassKgM2 = massK,
            },
        });

    static Template C(string name, ComputerTechInput c, string note) => new(name, "computer", note, new FcTechInput { Computer = c });

    static Template M(string name, bool director, double lay, double defl, string note, bool stable = false) =>
        new(name, "mount", note, new FcTechInput
        {
            Mount = new MountTechInput { DirectorFiring = director, StableVertical = stable, LayMil = lay, SalvoDeflMil = defl },
        });

    /// <summary>A computer's numbers: the tracker and the error budget every block gives (the clocks' and filters'
    /// own numbers are added with `with`).</summary>
    static ComputerTechInput K(string tracker, bool helmFree, double latency, double q, double priorKn, double priorDeg, double resid, double defl,
        double kick, double step, double salvo, double massT, double crew, double powerKw) =>
        new()
        {
            Tracker = tracker, HelmFree = helmFree, LatencyS = latency, Q = q, MaxRangeRateMs = 0, MaxBearingRateDegS = 0, AccelTauS = 0, AccelSigmaMs2 = 0,
            TurnQ = 0, SpeedQ = 0, Detect = false, RejudgeS = 0, PriorKn = priorKn, PriorDeg = priorDeg, ResidPct = resid, DeflBiasMil = defl,
            OwnTurnKickM = kick, FirstStepM = step, SalvoPct = salvo, MassT = massT, Crew = crew, PowerKw = powerKw,
        };

    /// <summary>The Dreyer's clock limits: ±1,200 yd/min in range, ±15°/min in bearing (computing.md §3, the 1918 handbook).</summary>
    const double DreyerRangeRate = 18.3, DreyerBearingRate = 0.25;

    /// <summary>The catalogue: optics, then computers, then mounts, each roughly by age.</summary>
    public static readonly IReadOnlyList<Template> All =
    [
        // ---- optics
        O("By eye", "eye", 0, 0, 0, 0, 0, 1, 0,
            "No instrument: ranges by eye, about 20% of the range (fire-control-research.md §3.1). A director without a rangefinder, or one whose " +
            "instrument isn't read."),
        O("Barr & Stroud FA (O1)", "coincidence", 14, 2.0, 0.75, 24, 20, 2, 30,
            "Early coincidence, c.1895-1906: the FA2 and FA3 4.5 ft (1.37 m) at ×24, the Japanese ships' at Tsushima and Oslyabya's (§3.3; " +
            "early-hit-rates.md). O1: 14″, rigid to 2 m, ×20 at a full image (designer §2.5). Unstabilised, 2 readings a minute (§3.5)."),
        O("Barr & Stroud FQ/FT (O2)", "coincidence", 12, 4.0, 0.75, 28, 28, 2, 28,
            "Mature coincidence, c.1906-1925: the FQ2 9 ft (2.74 m) and FT24 15 ft (4.57 m) at ×28 (§3.3). O2: 12″, rigid to 4 m (designer §2.5). " +
            "Unstabilised, 2 readings a minute (§3.5)."),
        O("Barr & Stroud FQ/FT on the Argo mounting", "coincidence", 12, 4.0, 0.75, 28, 28, 8, 28,
            "The O2 instruments on Pollen's gyro-stabilised mounting: range-taking about fivefold, 8 readings a minute (§3.5).", gyro: true),
        O("Zeiss stereo (WWI)", "stereo", 10, 4.0, 0.75, 23, 28, 2, 28,
            "The German 3 m stereo of 1914-18 at ×23: Lienau's ±65 m at 10 km and 165 m at 16 km give 10″ (§3.3). Rigid base as O2's [EST]; " +
            "unstabilised, 2 readings a minute (§3.5). Stereo's haze and contrast advantages are the sensors' (F5)."),
        O("Stereo, long base (O3)", "stereo", 12, 5.0, 0.75, 28, 30, 4, 27,
            "Interwar stereo and long bases (Zeiss, interwar B&S), c.1925-40: O3, 12″, rigid to 5 m (designer §2.5). 4 readings a minute (§3.5's " +
            "WWII director 4-6)."),
        O("Stereo, long base (O3, ×23)", "stereo", 12, 5.0, 0.75, 23, 30, 4, 27,
            "O3 at ×23: Bismarck's 10.5 m stereo (§3.3, its magnification [INFERRED])."),
        O("Stereo, long base (O3, ×25)", "stereo", 12, 5.0, 0.75, 25, 30, 4, 27,
            "O3 at ×25: the Japanese plot's preset (fire_control_ref.py LEVELS)."),
        O("Coated, stabilised (O4)", "stereo", 12, 6.0, 0.8, 25, 30, 6, 25,
            "USN 1943: coated, gyro-stabilised, rigid to 6 m (designer §2.5); the Mk 48 26.5 ft at ×25 (§3.3); 6 readings a minute (§3.5).",
            coated: true, gyro: true),
        O("Auto-track ranging (radar stand-in)", "stereo", 12, 6.0, 0.8, 25, 30, 60, 25,
            "O4's instruments read as an auto-tracking radar's range: 60 a minute (§3.5). Stands in for the radar block until the radar pass.",
            coated: true, gyro: true),
        O("Digital ranging (radar and laser stand-in)", "stereo", 12, 6.0, 0.8, 25, 30, 30, 25,
            "O4's instruments read as the 1970s' radar and laser: 30 a minute (fire_control_ref.py LEVELS 8). Stands in for the radar block.",
            coated: true, gyro: true),

        // ---- computers
        C("Judgement", K("judgement", false, 5, 0, 5, 30, 2.5, 4, 0, 550, 2.5, 0, 0, 0) with { RejudgeS = 60 },
            "A sailor's eye and head: the range guessed, the target's course and speed guessed from her look and led by it, looked at again " +
            "each minute, never a rate learnt (F2: the worst computer there is)."),
        C("Eye and hand plot", K("rate_clock", false, 20, 0.02, 5, 30, 2.5, 4, 0, 550, 2.5, 0.2, 3, 0),
            "Ranges by eye, rates by a hand plot (fire_control_ref.py level 0; designer C0)."),
        C("Hand plot", K("rate_clock", false, 15, 0.02, 4, 25, 2, 3, 0, 366, 2, 0.2, 3, 0),
            "Rangefinder ranges, rates from a hand plot and estimates, no machine (level 1: the Tsushima era; designer C0)."),
        C("Dumaresq and Vickers clock", K("rate_clock", false, 15, 0.03, 3, 20, 1.5, 2.5, 0, 366, 1.8, 0.8, 4, 0) with { MaxRangeRateMs = DreyerRangeRate },
            "The Dumaresq's rates run on by the Vickers range clock (1906): constant rates only, the roller slipping on a change (computing.md " +
            "§3; level 2, designer C1)."),
        C("Dreyer table Mk I-II", K("rate_clock", false, 15, 0.03, 2.5, 15, 1.2, 2, 300, 366, 1.5, 4, 10, 1) with
            {
                MaxRangeRateMs = DreyerRangeRate, MaxBearingRateDegS = DreyerBearingRate,
            },
            "The Dreyer table's range and bearing plots and clock before the gyro feed (1911-12): the Mk III's numbers without helm-free, so own " +
            "ship's turns are re-learnt (computing.md §3: helm-free from the Mk III)."),
        C("Dreyer table Mk III-V", K("rate_clock", true, 15, 0.03, 2.5, 15, 1.2, 2, 300, 366, 1.5, 4, 10, 1) with
            {
                MaxRangeRateMs = DreyerRangeRate, MaxBearingRateDegS = DreyerBearingRate,
            },
            "The Dreyer table from the Mk III (1914-22), helm-free by the gyro: a rate clock limited to ±1,200 yd/min and ±15°/min, its bearing " +
            "plot the weak point (computing.md §3; level 3, designer C2). Crew 7-8 at the table, up to 30 in the station."),
        C("German clock and plot (EU-Anzeiger, C12)", K("rate_clock", true, 15, 0.03, 2.5, 15, 1.2, 2, 300, 366, 1.5, 4, 10, 1) with
            {
                MaxRangeRateMs = DreyerRangeRate, MaxBearingRateDegS = DreyerBearingRate,
            },
            "The German WWI range-rate clock and plot (computing.md §3). No specifications found; taken as the Dreyer Mk III's, helm-free by " +
            "the gyro [EST], the two navies' 1916 machines doing the same job."),
        C("Argo clock Mk IV", K("true_course", true, 8, 0.03, 2.5, 15, 1.2, 2, 200, 366, 1.5, 2, 4, 1),
            "Pollen's true-course plotter and variable-speed clock (1912-13): follows a changing range rate, gyro-corrected; about six in RN " +
            "service at Jutland (computing.md §3; fire_control_ref.py level 3b). Mass and crew [EST]."),
        C("Admiralty Fire Control Table", K("true_course", true, 8, 0.03, 2, 12, 0.6, 1.5, 250, 366, 1.2, 5, 8, 5),
            "The AFCT Mk I-X (1927-45): clock and MPI plot, helm-free, a straight-course target (computing.md §3; level 4, designer C3)."),
        C("Admiralty Fire Control Table, turn setting", K("turn_rate", true, 8, 0.03, 2, 12, 0.6, 1.5, 250, 366, 1.2, 5, 8, 5) with
            {
                TurnQ = 1e-7, SpeedQ = 0.1,
            },
            "The AFCT with a hand-set enemy rate of turn: a turn filter whose rate adapts slowly (level 4t; [UNCERTAIN] in the research, a " +
            "computer option anyone may fit, decision 5)."),
        C("German Rechenstelle (1930s)", K("true_course", true, 8, 0.03, 2, 12, 0.6, 1.5, 250, 366, 1.2, 5, 8, 5),
            "The German computing rooms of the 1930s, fore and aft under armour (computing.md §3): no specifications found; the research's " +
            "table puts them with the AFCT, whose numbers they take [EST]."),
        C("Type 92 Shagekiban", K("rate_clock", true, 15, 0.03, 2, 12, 0.6, 1.5, 400, 366, 1.3, 4, 15, 2),
            "The Japanese Type 92 (1932) with its Sokutekiban: rates from a range-rate plot, deflection by hand, no gyro horizon; rates rebuilt " +
            "after a manoeuvre (computing.md §3; level 4j). Crew 7 + 8; mass [EST]."),
        C("Ford Mk 8 rangekeeper", K("true_course", true, 3, 0.05, 2, 12, 0.4, 1, 150, 366, 1.0, 3, 4, 10),
            "The Ford Mk 8 with the stable vertical (1941-42): continuous, helm-free, tilt, wind and drift corrected; one operator (computing.md " +
            "§3; level 5, designer C4)."),
        C("Ford Mk 8 rangekeeper, radar ranging (stand-in)", K("true_course", true, 2, 0.05, 2, 12, 0.4, 1, 150, 366, 1.0, 3, 4, 10),
            "The Mk 8 fed by radar spotting (level 6): its shorter latency is the radar's, standing in until the radar pass."),
        C("Mk 1A computer", K("acceleration", true, 1, 0.02, 2, 12, 0.4, 0.8, 50, 366, 0.7, 2.5, 3, 10) with
            {
                AccelTauS = 20, AccelSigmaMs2 = 0.1, Detect = true,
            },
            "The 1950s analog with a curved-course (acceleration) predictor and the \"target just turned\" button (level 7, designer C5)."),
        C("Digital turn filter (Mk 86, WM-25)", K("turn_rate", true, 0.5, 0.01, 2, 12, 0.3, 0.5, 20, 366, 0.4, 0.8, 2, 8) with
            {
                TurnQ = 5e-7, SpeedQ = 0.1, Detect = true,
            },
            "The 1970s digital systems with a turn-following filter (level 8, designer C6; the filter [INFERRED]). Its speed noise is the sim's " +
            "0.1, not the reference's 0.05² (F2: ships lose way in a turn)."),

        // ---- mounts
        M("Open sights", false, 3, 3,
            "Each gun laid by its layer over open sights (4′, designer §2.4): the eyeball's (level 0)."),
        M("Early telescopic sights", false, 2.5, 2.5,
            "Each gun by its own early telescope (2.5′, designer §2.4; M0): the Tsushima era (level 1)."),
        M("Telescopic sights", false, 2, 2.5,
            "Each gun by a good telescope (1.2′ with the layer's slop, designer §2.4), continuous aim, no director (level 2)."),
        M("Director, follow the pointer", true, 1, 2,
            "Director firing: the guns laid and fired from the director, the turrets following its pointers (M1, 1912-16; level 3)."),
        M("Director, follow the pointer (no RPC)", true, 1, 1.5,
            "Director firing without remote power control, the Japanese 1930s (no RPC on Yamato, computing.md §3): the Japanese plot's " +
            "preset numbers."),
        M("Director, power drive", true, 0.8, 1.5,
            "Director firing with power training and elevation (M2, interwar; level 4)."),
        M("Stable vertical, remote power control", true, 0.5, 1.2,
            "A stable vertical and gyro firing, the mounts under remote power control (M3, 1941-45; levels 5-6).", stable: true),
        M("Stable vertical, power servo", true, 0.4, 0.8,
            "The 1950s' servos (fire_control_ref.py level 7), between M3 and M4.", stable: true),
        M("Digital servo", true, 0.3, 0.5,
            "Digital servos (M4, 1970s; level 8).", stable: true),
    ];

    /// <summary>The presets: the levels 0-8 in order (fire_control_ref.py LEVELS), then the reference's rows beside them.</summary>
    public static readonly IReadOnlyList<Preset> Presets =
    [
        new("eyeball", "By eye", "Eye and hand plot", "Open sights", "Level 0, c.1890: ranges by eye, each gun laid by its own layer."),
        new("rangefinder", "Barr & Stroud FA (O1)", "Hand plot", "Early telescopic sights", "Level 1, 1905: an early rangefinder."),
        new("clock", "Barr & Stroud FQ/FT (O2)", "Dumaresq and Vickers clock", "Telescopic sights", "Level 2, 1910: Dumaresq and range clock."),
        new("director", "Barr & Stroud FQ/FT (O2)", "Dreyer table Mk III-V", "Director, follow the pointer", "Level 3, 1916: the Dreyer table and director firing."),
        new("table", "Stereo, long base (O3)", "Admiralty Fire Control Table", "Director, power drive", "Level 4, the 1930s: the interwar central table."),
        new("rangekeeper", "Coated, stabilised (O4)", "Ford Mk 8 rangekeeper", "Stable vertical, remote power control",
            "Level 5, 1941-42: rangekeeper and stable vertical."),
        new("radar-spotting", "Coated, stabilised (O4)", "Ford Mk 8 rangekeeper, radar ranging (stand-in)", "Stable vertical, remote power control",
            "Level 6, 1945: radar spotting (its radar a stand-in until the radar pass)."),
        new("auto-track", "Auto-track ranging (radar stand-in)", "Mk 1A computer", "Stable vertical, power servo", "Level 7, 1955: auto-tracking radar."),
        new("digital", "Digital ranging (radar and laser stand-in)", "Digital turn filter (Mk 86, WM-25)", "Digital servo", "Level 8, 1975: digital."),
        new("judgement", "By eye", "Judgement", "Open sights", "A sailor's judgement: the worst computer, for comparison (F2)."),
        new("argo", "Barr & Stroud FQ/FT on the Argo mounting", "Argo clock Mk IV", "Director, follow the pointer", "Pollen's Argo clock (level 3b)."),
        new("table-turn", "Stereo, long base (O3)", "Admiralty Fire Control Table, turn setting", "Director, power drive",
            "The AFCT with its turn setting (level 4t, decision 5)."),
        new("japanese-plot", "Stereo, long base (O3, ×25)", "Type 92 Shagekiban", "Director, follow the pointer (no RPC)",
            "The Japanese plot of the 1930s (level 4j)."),
    ];

    static readonly Dictionary<string, Template> byName = All.ToDictionary(t => t.Name, StringComparer.Ordinal);

    /// <summary>The catalogue's block of a name, or null.</summary>
    public static Template? Get(string name) => byName.GetValueOrDefault(name);

    /// <summary>A name's block: the design's (fire_control_tech), else the catalogue's, else null.</summary>
    public static FcTechInput? Resolve(Design design, string name) => design.FireControlTech?.GetValueOrDefault(name) ?? Get(name)?.Value;

    /// <summary>The fit a battery's fire control names: its own, else the default preset's.</summary>
    public static IReadOnlyList<string> FitOf(DirectorInput? spec) => spec?.Fit ?? PresetNamed(DefaultPreset)!.Fit;

    /// <summary>The computers' trackers and the optics' kinds, as the blocks give them.</summary>
    public static readonly string[] Trackers = ["judgement", "rate_clock", "true_course", "acceleration", "turn_rate"],
        OpticsKinds = ["eye", "coincidence", "stereo"];

    static readonly string[] OpticsFields = ["kind", "delta_arcsec", "rigid_base_m", "flex_exp", "magnification", "m_max", "readings_per_min", "coated",
        "gyro_stabilised", "mass_kg_m2"];
    static readonly string[] ComputerFields = ["tracker", "helm_free", "latency_s", "q", "max_range_rate_ms", "max_bearing_rate_deg_s", "accel_tau_s",
        "accel_sigma_ms2", "turn_q", "speed_q", "detect", "rejudge_s", "prior_kn", "prior_deg", "resid_pct", "defl_bias_mil", "own_turn_kick_m",
        "first_step_m", "salvo_pct", "mass_t", "crew", "power_kw"];
    static readonly string[] MountFields = ["director_firing", "stable_vertical", "lay_mil", "salvo_defl_mil"];

    /// <summary>The design's fire_control_tech blocks: each one kind, its fields known, its numbers given and 0 or more; and
    /// each battery's fit: names that resolve, one block of each kind.</summary>
    public static List<string> Validate(Design design)
    {
        var errs = new List<string>();
        foreach (var (name, b) in design.FireControlTech ?? [])
        {
            string at = $"fire_control_tech.\"{name}\"";
            if (b is null)
            {
                errs.Add($"{at}: give its numbers");
                continue;
            }
            foreach (var k in b.Extra.KeysOrEmpty())
                errs.Add($"{at}.{k}: not a kind of block (optics, computer, mount)");
            int kinds = (b.Optics is null ? 0 : 1) + (b.Computer is null ? 0 : 1) + (b.Mount is null ? 0 : 1);
            if (kinds != 1)
                errs.Add($"{at}: a block is one of optics, computer or mount ({kinds} given)");
            void Numbers(string kind, IEnumerable<(string Key, double? V)> given, IEnumerable<string> required)
            {
                foreach (var (k, v) in given)
                    if (v is double d && !(d >= 0 && double.IsFinite(d)))
                        errs.Add($"{at}.{kind}.{k}: a number, 0 or more");
                foreach (var (k, v) in given)
                    if (v is null && required.Contains(k))
                        errs.Add($"{at}.{kind}.{k}: give it");
            }
            if (b.Optics is { } o)
            {
                foreach (var k in o.Extra.KeysOrEmpty())
                    errs.Add($"{at}.optics.{k}: not an optics field ({string.Join(", ", OpticsFields)})");
                if (!OpticsKinds.Contains(o.Kind))
                    errs.Add($"{at}.optics.kind = {Style.Quote(o.Kind ?? "")}: use {string.Join(", ", OpticsKinds)}");
                Numbers("optics", [("delta_arcsec", o.DeltaArcsec), ("rigid_base_m", o.RigidBaseM), ("flex_exp", o.FlexExp), ("magnification", o.Magnification),
                    ("m_max", o.MMax), ("readings_per_min", o.ReadingsPerMin), ("mass_kg_m2", o.MassKgM2)], ["magnification", "readings_per_min"]);
                if (o.ReadingsPerMin is 0)
                    errs.Add($"{at}.optics.readings_per_min: more than 0");
            }
            if (b.Computer is { } c)
            {
                foreach (var k in c.Extra.KeysOrEmpty())
                    errs.Add($"{at}.computer.{k}: not a computer field ({string.Join(", ", ComputerFields)})");
                if (!Trackers.Contains(c.Tracker))
                    errs.Add($"{at}.computer.tracker = {Style.Quote(c.Tracker ?? "")}: use {string.Join(", ", Trackers)}");
                Numbers("computer", [("latency_s", c.LatencyS), ("q", c.Q), ("max_range_rate_ms", c.MaxRangeRateMs), ("max_bearing_rate_deg_s", c.MaxBearingRateDegS),
                    ("accel_tau_s", c.AccelTauS), ("accel_sigma_ms2", c.AccelSigmaMs2), ("turn_q", c.TurnQ), ("speed_q", c.SpeedQ), ("rejudge_s", c.RejudgeS),
                    ("prior_kn", c.PriorKn), ("prior_deg", c.PriorDeg), ("resid_pct", c.ResidPct), ("defl_bias_mil", c.DeflBiasMil), ("own_turn_kick_m", c.OwnTurnKickM),
                    ("first_step_m", c.FirstStepM), ("salvo_pct", c.SalvoPct), ("mass_t", c.MassT), ("crew", c.Crew), ("power_kw", c.PowerKw)],
                    ["latency_s", "prior_kn", "prior_deg", "resid_pct", "defl_bias_mil", "first_step_m", "salvo_pct"]);
            }
            if (b.Mount is { } m)
            {
                foreach (var k in m.Extra.KeysOrEmpty())
                    errs.Add($"{at}.mount.{k}: not a mount field ({string.Join(", ", MountFields)})");
                Numbers("mount", [("lay_mil", m.LayMil), ("salvo_defl_mil", m.SalvoDeflMil)], ["lay_mil", "salvo_defl_mil"]);
            }
        }
        foreach (var bat in (ReadOnlySpan<string>)["main", "secondary", "aa"])
        {
            if (design.FireControl?.Of(bat)?.Fit is not { } fit)
                continue;
            string at = $"fire_control.{bat}.fit";
            var kinds = new List<string>();
            foreach (var n in fit)
            {
                if (Resolve(design, n) is not { } b)
                {
                    errs.Add($"{at}: no block {Style.Quote(n ?? "")} in fire_control_tech or the catalogue");
                    continue;
                }
                if (b.Optics is not null)
                    kinds.Add("optics");
                if (b.Computer is not null)
                    kinds.Add("computer");
                if (b.Mount is not null)
                    kinds.Add("mount");
            }
            foreach (var k in (ReadOnlySpan<string>)["optics", "computer", "mount"])
            {
                int count = kinds.Count(x => x == k);
                if (count != 1 && fit.All(n => n is not null && Resolve(design, n) is not null))
                    errs.Add($"{at}: name one {k} block ({count} named)");
            }
        }
        return errs;
    }

    /// <summary>A preset by its name (case aside), or null.</summary>
    public static Preset? PresetNamed(string name) => Presets.FirstOrDefault(p => string.Equals(p.Name, name, StringComparison.OrdinalIgnoreCase));

    /// <summary>The fit a battery with directors and no fit fires with: the early rangefinder's (its rangefinder read if it has
    /// one, else ranges by eye).</summary>
    public const string DefaultPreset = "rangefinder";
}
