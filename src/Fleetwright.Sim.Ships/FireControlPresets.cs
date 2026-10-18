namespace Fleetwright.Sim.Ships;

/// <summary>The fire-control presets (docs/battle/fire-control.md decision 1: the levels become presets): a fit for each
/// <see cref="FireControlLevel"/>, the rows of research/simulation/fire_control_ref.py LEVELS (0-8) split onto the
/// optics, the computer and the mounts, each computer with its machine's tracker (F2); then the reference's rows beside the
/// levels, for benches and fits that ask by name: a sailor's judgement (the worst computer, there for
/// comparison), Pollen's Argo clock, the AFCT with its turn setting and the Japanese plot. Local control (a battery
/// without a director) fires at row 0. What a design's fit is made from until F3 writes them to the catalogue
/// (Content/Templates/fire-control.json) as named combinations of tech blocks.</summary>
public static class FireControlPresets
{
    /// <summary>The first preset past the levels: the sailor's judgement.</summary>
    public const int Judgement = 9;

    static readonly (string Name, FireControlFit Fit)[] Fits =
    [
        ("eyeball", Fit(1, 0, new() { Tracker = TrackerKind.RateClock, LatencyS = 20, Q = 0.02, PriorKn = 5, PriorDeg = 30, ResidPct = 2.5,
            DeflBiasMil = 4, Ladder0M = 550, SalvoPct = 2.5, SalvoDeflMil = 3 }, false, 3)),
        ("rangefinder", Fit(2, 24, new() { Tracker = TrackerKind.RateClock, LatencyS = 15, Q = 0.02, PriorKn = 4, PriorDeg = 25, ResidPct = 2,
            DeflBiasMil = 3, Ladder0M = 366, SalvoPct = 2, SalvoDeflMil = 2.5 }, false, 2.5)),
        ("clock", Fit(2, 28, new() { Tracker = TrackerKind.RateClock, MaxRangeRateMs = 18.3, LatencyS = 15, Q = 0.03, PriorKn = 3, PriorDeg = 20,
            ResidPct = 1.5, DeflBiasMil = 2.5, Ladder0M = 366, SalvoPct = 1.8, SalvoDeflMil = 2.5 }, false, 2)),
        ("director", Fit(2, 28, new() { Tracker = TrackerKind.RateClock, HelmFree = true, MaxRangeRateMs = 18.3, MaxBearingRateDegS = 0.25,
            LatencyS = 15, Q = 0.03, PriorKn = 2.5, PriorDeg = 15, ResidPct = 1.2, DeflBiasMil = 2, OwnTurnKickM = 300, Ladder0M = 366, SalvoPct = 1.5,
            SalvoDeflMil = 2 }, true, 1)),
        ("table", Fit(4, 28, new() { Tracker = TrackerKind.TrueCourse, HelmFree = true, LatencyS = 8, Q = 0.03, PriorKn = 2, PriorDeg = 12,
            ResidPct = 0.6, DeflBiasMil = 1.5, OwnTurnKickM = 250, Ladder0M = 366, SalvoPct = 1.2, SalvoDeflMil = 1.5 }, true, 0.8)),
        ("rangekeeper", Fit(6, 25, new() { Tracker = TrackerKind.TrueCourse, HelmFree = true, LatencyS = 3, Q = 0.05, PriorKn = 2, PriorDeg = 12,
            ResidPct = 0.4, DeflBiasMil = 1, OwnTurnKickM = 150, Ladder0M = 366, SalvoPct = 1, SalvoDeflMil = 1.2 }, true, 0.5)),
        ("radar-spotting", Fit(6, 25, new() { Tracker = TrackerKind.TrueCourse, HelmFree = true, LatencyS = 2, Q = 0.05, PriorKn = 2, PriorDeg = 12,
            ResidPct = 0.4, DeflBiasMil = 1, OwnTurnKickM = 150, Ladder0M = 366, SalvoPct = 1, SalvoDeflMil = 1.2 }, true, 0.5)),
        ("auto-track", Fit(60, 25, new() { Tracker = TrackerKind.Acceleration, HelmFree = true, AccelTauS = 20, AccelSigmaMs2 = 0.1, Detect = true,
            LatencyS = 1, Q = 0.02, PriorKn = 2, PriorDeg = 12, ResidPct = 0.4, DeflBiasMil = 0.8, OwnTurnKickM = 50, Ladder0M = 366, SalvoPct = 0.7,
            SalvoDeflMil = 0.8 }, true, 0.4)),
        ("digital", Fit(30, 25, new() { Tracker = TrackerKind.TurnRate, HelmFree = true, TurnQ = 5e-7, SpeedQ = 0.1, Detect = true, LatencyS = 0.5, Q = 0.01,
            PriorKn = 2, PriorDeg = 12, ResidPct = 0.3, DeflBiasMil = 0.5, OwnTurnKickM = 20, Ladder0M = 366, SalvoPct = 0.4, SalvoDeflMil = 0.5 }, true, 0.3)),

        // beside the levels
        ("judgement", Fit(1, 0, new() { Tracker = TrackerKind.Judgement, RejudgeS = 60, LatencyS = 5, PriorKn = 5, PriorDeg = 30, ResidPct = 2.5,
            DeflBiasMil = 4, Ladder0M = 550, SalvoPct = 2.5, SalvoDeflMil = 3 }, false, 3)),
        ("argo", Fit(8, 28, new() { Tracker = TrackerKind.TrueCourse, HelmFree = true, LatencyS = 8, Q = 0.03, PriorKn = 2.5, PriorDeg = 15,
            ResidPct = 1.2, DeflBiasMil = 2, OwnTurnKickM = 200, Ladder0M = 366, SalvoPct = 1.5, SalvoDeflMil = 2 }, true, 1)),
        ("table-turn", Fit(4, 28, new() { Tracker = TrackerKind.TurnRate, HelmFree = true, TurnQ = 1e-7, SpeedQ = 0.1, LatencyS = 8, Q = 0.03, PriorKn = 2,
            PriorDeg = 12, ResidPct = 0.6, DeflBiasMil = 1.5, OwnTurnKickM = 250, Ladder0M = 366, SalvoPct = 1.2, SalvoDeflMil = 1.5 }, true, 0.8)),
        ("japanese-plot", Fit(4, 25, new() { Tracker = TrackerKind.RateClock, HelmFree = true, LatencyS = 15, Q = 0.03, PriorKn = 2, PriorDeg = 12,
            ResidPct = 0.6, DeflBiasMil = 1.5, OwnTurnKickM = 400, Ladder0M = 366, SalvoPct = 1.3, SalvoDeflMil = 1.5 }, true, 1)),
    ];

    static FireControlFit Fit(double readingsPerMin, double magnification, ComputerRow computer, bool director, double layMil) =>
        new(new OpticsRow(magnification, readingsPerMin), computer, new MountTechRow(director, layMil));

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

    /// <summary>Local control's: each mount by its layer's eye.</summary>
    public static ref readonly FireControlFit Local => ref Fits[0].Fit;
}
