namespace Fleetwright.Sim.Ships;

/// <summary>The fire-control presets (docs/battle/fire-control.md decision 1: the levels become presets): a fit for each
/// <see cref="FireControlLevel"/>, the rows of research/simulation/fire_control_ref.py LEVELS (0-8) split onto the
/// optics, the computer and the mounts. Local control (a battery without a director) fires at row 0. What a design's
/// fit is made from until F3 writes them to the catalogue (Content/Templates/fire-control.json) as named combinations
/// of tech blocks.</summary>
public static class FireControlPresets
{
    static readonly FireControlFit[] Fits =
    [
        Fit(1, 0, 20, 0.02, 5, 30, 2.5, 4, false, 3, 0, 550, 2.5, 3),            // 0 eyeball
        Fit(2, 24, 15, 0.02, 4, 25, 2, 3, false, 2.5, 0, 366, 2, 2.5),           // 1 early rangefinder
        Fit(2, 28, 15, 0.03, 3, 20, 1.5, 2.5, false, 2, 0, 366, 1.8, 2.5),       // 2 Dumaresq and clock
        Fit(2, 28, 15, 0.03, 2.5, 15, 1.2, 2, true, 1, 300, 366, 1.5, 2),        // 3 Dreyer and director
        Fit(4, 28, 8, 0.03, 2, 12, 0.6, 1.5, true, 0.8, 250, 366, 1.2, 1.5),     // 4 interwar table
        Fit(6, 25, 3, 0.05, 2, 12, 0.4, 1, true, 0.5, 150, 366, 1, 1.2),         // 5 rangekeeper
        Fit(6, 25, 2, 0.05, 2, 12, 0.4, 1, true, 0.5, 150, 366, 1, 1.2),         // 6 radar spotting
        Fit(60, 25, 1, 0.02, 2, 12, 0.4, 0.8, true, 0.4, 50, 366, 0.7, 0.8),     // 7 auto-track
        Fit(30, 25, 0.5, 0.01, 2, 12, 0.3, 0.5, true, 0.3, 20, 366, 0.4, 0.5),   // 8 digital
    ];

    static FireControlFit Fit(double readingsPerMin, double magnification, double latencyS, double q, double priorKn, double priorDeg,
        double residPct, double deflBiasMil, bool director, double layMil, double ownTurnKickM, double ladder0M, double salvoPct, double salvoDeflMil) =>
        new(new OpticsRow(magnification, readingsPerMin),
            new ComputerRow
            {
                LatencyS = latencyS, Q = q, PriorKn = priorKn, PriorDeg = priorDeg, ResidPct = residPct, DeflBiasMil = deflBiasMil,
                OwnTurnKickM = ownTurnKickM, Ladder0M = ladder0M, SalvoPct = salvoPct, SalvoDeflMil = salvoDeflMil,
            },
            new MountTechRow(director, layMil));

    /// <summary>The presets: one a level, 0-8.</summary>
    public static int Count => Fits.Length;

    /// <summary>A level's fit.</summary>
    public static ref readonly FireControlFit Of(FireControlLevel level) => ref Fits[(int)level];

    /// <summary>Local control's: each mount by its layer's eye.</summary>
    public static ref readonly FireControlFit Local => ref Fits[0];
}
