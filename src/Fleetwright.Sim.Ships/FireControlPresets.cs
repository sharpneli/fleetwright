using Fleetwright.Shipgen;

namespace Fleetwright.Sim.Ships;

/// <summary>The fire-control presets (docs/battle/fire-control.md decision 1: the levels become presets): the catalogue's
/// named fits (Shipgen's <see cref="FireControlTemplates.Presets"/>) as the sim reads them, the levels 0-8 first
/// (fire_control_ref.py LEVELS), then the reference's rows beside them for benches and fits that ask by name: a sailor's
/// judgement (the worst computer, there for comparison), Pollen's Argo clock, the AFCT with its turn setting and the
/// Japanese plot. Local control (a battery without a director) fires at row 0.</summary>
public static class FireControlPresets
{
    /// <summary>The first preset past the levels: the sailor's judgement.</summary>
    public const int Judgement = 9;

    static readonly (string Name, FireControlFit Fit)[] Fits = [.. FireControlTemplates.Presets.Select(p => (p.Name, FireControlBlocks.Fit(null, p.Fit)))];

    /// <summary>The presets: the levels 0-8, then the ones beside them.</summary>
    public static int Count => Fits.Length;

    /// <summary>A level's fit.</summary>
    public static ref readonly FireControlFit Of(FireControlLevel level) => ref Fits[(int)level].Fit;

    /// <summary>A preset's fit by its index.</summary>
    public static ref readonly FireControlFit At(int preset) => ref Fits[preset].Fit;

    /// <summary>A preset's name ("director", "judgement", ...).</summary>
    public static string Name(int preset) => preset >= 0 && preset < Fits.Length ? Fits[preset].Name : $"preset {preset}";

    /// <summary>A preset by its name or its level's (case aside), or -1.</summary>
    public static int Find(string name)
    {
        for (int i = 0; i < Fits.Length; i++)
            if (string.Equals(Fits[i].Name, name, StringComparison.OrdinalIgnoreCase) || i < 9 && string.Equals(((FireControlLevel)i).ToString(), name,
                    StringComparison.OrdinalIgnoreCase))
                return i;
        return -1;
    }

    /// <summary>Local control's: each mount by its layer's eye, the officer at the guns judging her course and speed by her
    /// look and looking again each minute (the judgement's computer: docs/battle/balance.md P4, BL1e; fire-control.md F6's
    /// choice: a plain rate clock fed by the eye ran a closing rate past the closest approach, 12.5% crossing at 1.5 km
    /// against the judgement's 21%, the same elsewhere).</summary>
    public static ref readonly FireControlFit Local => ref local;

    static readonly FireControlFit local = Fits[0].Fit with { Computer = Fits[Judgement].Fit.Computer };

    /// <summary>The level a fit is labelled with (decision 1: a level is a preset's label, read by the matchup until F10
    /// and by the displays): a level preset's own when its computer is that preset's, the presets beside the levels by
    /// the level they stand beside (judgement the eyeball, the Argo and the Japanese plot the Dreyer's, the turn setting
    /// the table's), any other computer by its kind.</summary>
    public static FireControlLevel LevelOf(in FireControlFit fit)
    {
        for (int i = 0; i < Fits.Length; i++)
            if (Fits[i].Fit.Computer == fit.Computer)
                return i switch
                {
                    < Judgement => (FireControlLevel)i,
                    Judgement => FireControlLevel.Eyeball,
                    Judgement + 2 => FireControlLevel.Table,
                    _ => FireControlLevel.Director,
                };
        var k = fit.Computer;
        return k.Tracker switch
        {
            TrackerKind.Judgement => FireControlLevel.Eyeball,
            TrackerKind.RateClock => k.HelmFree ? FireControlLevel.Director : k.MaxRangeRateMs > 0 ? FireControlLevel.Clock : FireControlLevel.Rangefinder,
            TrackerKind.TrueCourse => k.LatencyS >= 8 ? FireControlLevel.Table : FireControlLevel.Rangekeeper,
            TrackerKind.Acceleration => FireControlLevel.AutoTrack,
            _ => k.LatencyS >= 8 ? FireControlLevel.Table : FireControlLevel.Digital,
        };
    }
}

/// <summary>A fit's tech blocks as the sim reads them (docs/battle/fire-control.md F3): each name the design's block
/// (`fire_control_tech`), else the catalogue's (<see cref="FireControlTemplates"/>). Strict, as the class compile is: a
/// name in neither, a kind given twice or missing, or a number missing throws.</summary>
public static class FireControlBlocks
{
    /// <summary>The fit a list of block names makes, the design's blocks first (design null: the catalogue's only).</summary>
    public static FireControlFit Fit(Design? design, IReadOnlyList<string> names)
    {
        OpticsTechInput? o = null;
        ComputerTechInput? c = null;
        MountTechInput? m = null;
        foreach (var n in names)
        {
            var b = Block(design, n) ?? throw new ShipCompileException($"fire control: no block \"{n}\" in the design's fire_control_tech or the catalogue");
            if (b.Optics is { } bo)
                o = o is null ? bo : throw Twice("optics", n);
            if (b.Computer is { } bc)
                c = c is null ? bc : throw Twice("computer", n);
            if (b.Mount is { } bm)
                m = m is null ? bm : throw Twice("mount", n);
        }
        return new FireControlFit(Optics(o ?? throw Missing("optics", names)), Computer(c ?? throw Missing("computer", names)),
            Mount(m ?? throw Missing("mount", names)));
    }

    /// <summary>A name's block: the design's, else the catalogue's, else null.</summary>
    public static FcTechInput? Block(Design? design, string name) =>
        design is null ? FireControlTemplates.Get(name)?.Value : FireControlTemplates.Resolve(design, name);

    static ShipCompileException Twice(string kind, string name) => new($"fire control: a fit names two {kind} blocks (\"{name}\" the second)");

    static ShipCompileException Missing(string kind, IReadOnlyList<string> names) =>
        new($"fire control: the fit [{string.Join(", ", names)}] names no {kind} block");

    static double N(double? v, string what) => v ?? throw new ShipCompileException($"fire control: a block gives no {what}");

    public static OpticsRow Optics(OpticsTechInput o) => o.Magnification is > 0
        ? new(o.Magnification.Value, N(o.ReadingsPerMin, "readings_per_min"), N(o.DeltaArcsec, "delta_arcsec"), N(o.RigidBaseM, "rigid_base_m"),
            N(o.FlexExp, "flex_exp"), Kinds.InstrumentOf(o.Kind), o.MMax ?? 0, o.Coated ?? false, o.GyroStabilised ?? false)
        : new(0, N(o.ReadingsPerMin, "readings_per_min"), 0, 0, 0, InstrumentKind.Eye);

    public static ComputerRow Computer(ComputerTechInput c) => new()
    {
        Tracker = Kinds.TrackerOf(c.Tracker), HelmFree = c.HelmFree ?? false, MaxRangeRateMs = c.MaxRangeRateMs ?? 0,
        MaxBearingRateDegS = c.MaxBearingRateDegS ?? 0, AccelTauS = c.AccelTauS ?? 0, AccelSigmaMs2 = c.AccelSigmaMs2 ?? 0, TurnQ = c.TurnQ ?? 0,
        SpeedQ = c.SpeedQ ?? 0, Detect = c.Detect ?? false, RejudgeS = c.RejudgeS ?? 0, LatencyS = N(c.LatencyS, "latency_s"), Q = c.Q ?? 0,
        PriorKn = N(c.PriorKn, "prior_kn"), PriorDeg = N(c.PriorDeg, "prior_deg"), ResidPct = N(c.ResidPct, "resid_pct"),
        DeflBiasMil = N(c.DeflBiasMil, "defl_bias_mil"), OwnTurnKickM = c.OwnTurnKickM ?? 0, FirstStepM = N(c.FirstStepM, "first_step_m"),
        SalvoPct = N(c.SalvoPct, "salvo_pct"),
    };

    public static MountTechRow Mount(MountTechInput m)
    {
        bool director = m.DirectorFiring ?? false, vertical = m.StableVertical ?? false;
        return new(director, N(m.LayMil, "lay_mil"), N(m.SalvoDeflMil, "salvo_defl_mil"), m.RollTauS ?? DesignerPlaceholders.RollTau(vertical),
            m.CrossLevel ?? DesignerPlaceholders.CrossLevel(director, vertical), vertical);
    }
}
