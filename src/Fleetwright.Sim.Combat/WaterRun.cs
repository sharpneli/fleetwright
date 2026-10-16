using Fleetwright.Sim.Core;
using Fleetwright.Sim.Ships;

namespace Fleetwright.Sim.Combat;

/// <summary>A shell's run under the water, straight on along its line of flight from where it met the sea
/// (docs/battle/shells-armour-damage.md P6; research/simulation/armor/05-obliquity-multilayer.md §6). Quadratic drag
/// bleeds its speed as v·e^(−x/L), L a number of calibres: <see cref="SimSettings.UnderwaterDecayCalibres"/> for a
/// pointed shell (its tumbling folded in), <see cref="SimSettings.UnderwaterDecayCalibresDiving"/> for a diving one. The
/// sea is flat, and the ship's waterline is where its flotation puts it (<see cref="HitQuery.WaterOf"/>). The shell
/// system follows it <see cref="ReachM"/> on; the terminal model strikes the hull at <see cref="Speed"/>. Pure.</summary>
public static class WaterRun
{
    /// <summary>The shell's decay length in the water, m: the run over which its speed falls by e.</summary>
    public static double DecayM(in ShellTypeRow shell, SimSettings s) =>
        shell.CalibreMm / 1000 * (shell.Diving ? s.UnderwaterDecayCalibresDiving : s.UnderwaterDecayCalibres);

    /// <summary>Its speed after runM under the water, having met it at v.</summary>
    public static double Speed(double v, double runM, double decayM) => runM > 0 ? v * Math.Exp(-runM / decayM) : v;

    /// <summary>The seconds it takes to run runM under the water, having met it at v (dx/dt = v·e^(−x/L)).</summary>
    public static double TimeS(double v, double runM, double decayM) => decayM * double.ExpM1(runM / decayM) / Math.Max(v, 1e-9);

    /// <summary>How far it gets in t seconds under the water, having met it at v: L·ln(1 + v·t/L).</summary>
    public static double RunInM(double v, double t, double decayM) => decayM * Math.Log(1 + Math.Max(v, 0) * Math.Max(t, 0) / decayM);

    /// <summary>Whether its fuze starts on the water (a base fuze, when <see cref="SimSettings.FuzeStartsOnWater"/>):
    /// its delay runs from there, so it bursts <see cref="FuzeRunM"/> on. A nose fuze fires on the water itself.</summary>
    public static bool FuzeStarts(in ShellTypeRow shell, SimSettings s) => s.FuzeStartsOnWater && shell.Fuze == Fuze.Base;

    /// <summary>Where under the water a fuze started on it fires, m along the run (infinite if it doesn't start there).</summary>
    public static double FuzeRunM(in ShellTypeRow shell, double v, SimSettings s) =>
        FuzeStarts(shell, s) ? RunInM(v, shell.FuzeDelayS, DecayM(shell, s)) : double.PositiveInfinity;

    /// <summary>How far under the water the hit query follows it, having met it at v: none for a nose fuze (it fires on
    /// the sea: a near miss); else <see cref="SimSettings.UnderwaterReachDecays"/> decay lengths, or less where a fuze
    /// started on the water fires.</summary>
    public static double ReachM(in ShellTypeRow shell, double v, SimSettings s) =>
        shell.Fuze == Fuze.Nose ? 0 : Math.Min(s.UnderwaterReachDecays * DecayM(shell, s), FuzeRunM(shell, v, s));

    /// <summary>How far a path ran under the water before crossing i: from its first entry into the water before it (a
    /// path that starts in the water enters at its start), or 0.</summary>
    public static double RunM(HitPath path, int i)
    {
        var xs = path.Crossings;
        for (int j = 0; j < i; j++)
            if (xs[j].Kind == CrossingKind.WaterIn)
                return Math.Max(xs[i].T - xs[j].T, 0);
        return 0;
    }
}
