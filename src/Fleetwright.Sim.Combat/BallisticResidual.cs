using Fleetwright.Sim.Core;
using Fleetwright.Sim.Ships;

namespace Fleetwright.Sim.Combat;

/// <summary>What a computer tells the guns of the day's ballistics, as the shells meet it: the wind, the air's density and
/// the guns' muzzle velocity (docs/battle/error-sources.md ES3). In place of the old residual bias (<c>resid_pct</c>,
/// <c>defl_bias_mil</c>: one draw a battery of a lumped size), each part from its physics and what the machine measures of
/// it:
/// <list type="bullet">
/// <item><b>The wind</b> carries a shell across by the lag rule (Didion; McCoy, Modern Exterior Ballistics §9):
/// W·(T − X/(V₀ cos θ₀)), the time it lags a flight in a vacuum; along the line exactly, in the air's frame (a wind from
/// behind is a muzzle velocity slower through the air, raised a little, and the air carries the shell W·T on), from the
/// table's flights: the lag rule there gives two thirds of it, the drag's change with the shell's speed through the air
/// left out (BallisticsTests: within a few per cent of a flight integrated through the wind both ways). W is the wind
/// along its path, here the sea's profile (<see cref="SmokeAir.Profile"/>) averaged over its time in flight. A machine with no wind input corrects none; one set from the deck takes the wind its
/// anemometer gives (at <see cref="AnemometerM"/>, so short of the wind aloft), one fed the ballistic wind takes that; each
/// with its measuring error.</item>
/// <item><b>The air's density</b> as the machine assumes it, off by its measuring (a barometer and thermometer on deck, the
/// air aloft unknown), its error's range from the table's flights.</item>
/// <item><b>The muzzle velocity</b> as the machine knows it (calibration, the charges' temperature, wear), the same way.</item>
/// </list>
/// Drift and the Earth's turning are corrected by every machine (the flights have the Earth's turning; no shell drifts:
/// neither modelled, generous to the gunner). The sizes are flagged estimates on each machine.</summary>
public static class BallisticResidual
{
    /// <summary>The ship's anemometer over the sea, m [EST: a foremast's height].</summary>
    public const double AnemometerM = 30;

    public const double Knot = 0.514444;

    /// <summary>A flight's sensitivities at an elevation from a muzzle h0 over the sea: the time its shell lags a vacuum's
    /// (s: a wind across carries it that far a m/s), the range a wind from behind adds a m/s (s), the wind along its path
    /// over the 10 m wind (its time in flight's mean), and the range a 1% higher muzzle velocity and a 1% denser air give
    /// it (m).</summary>
    public readonly record struct Flight(double LagS, double AlongS, double WindShare, double PerMvPct, double PerDensityPct);

    /// <summary><see cref="Flight"/> from the table (some twenty lookups: once a salvo).</summary>
    public static Flight Of(BallisticTable table, double elevationDeg, double h0)
    {
        var fall = table.Fall(elevationDeg, h0);
        if (double.IsNaN(fall.Range))
            return default;
        double v0 = table.Key.MuzzleVelocityMs * Math.Cos(double.DegreesToRadians(elevationDeg));
        double lag = Math.Max(fall.Tof - fall.Range / Math.Max(v0, 1), 0);
        const int n = 12;
        double share = 0, rho = BallisticTable.DensityAt(h0);
        for (int i = 0; i < n; i++)
            share += SmokeAir.Profile(h0 + table.At(elevationDeg, 1, rho, (i + 0.5) / n * fall.Tof).H);
        double perMv = (table.Fall(elevationDeg, h0, 1.01).Range - table.Fall(elevationDeg, h0, 0.99).Range) / 2;
        double perRho = (SeaRange(table, elevationDeg, h0, rho * 1.01, fall.Tof) - SeaRange(table, elevationDeg, h0, rho * 0.99, fall.Tof)) / 2;
        double along = (Behind(table, elevationDeg, h0, AlongStepMs) - Behind(table, elevationDeg, h0, -AlongStepMs)) / (2 * AlongStepMs);
        return new Flight(lag, double.IsNaN(along) ? lag : along, share / n, perMv, perRho);
    }

    const double AlongStepMs = 2;

    /// <summary>Its fall in a steady wind from behind (m/s), m: in the air's frame it leaves slower and a little steeper,
    /// and the air carries it on by the wind over its time in flight.</summary>
    static double Behind(BallisticTable table, double elevationDeg, double h0, double windMs)
    {
        double e = double.DegreesToRadians(elevationDeg), v = table.Key.MuzzleVelocityMs;
        double vx = v * Math.Cos(e) - windMs, vy = v * Math.Sin(e);
        var f = table.Fall(double.RadiansToDegrees(Math.Atan2(vy, vx)), h0, Math.Sqrt(vx * vx + vy * vy) / v);
        return f.Range + windMs * f.Tof;
    }

    /// <summary>Where a flight in an air reaches the sea, m: from its point at a time near its fall, on down at its rates.</summary>
    static double SeaRange(BallisticTable table, double elevationDeg, double h0, double density, double nearS)
    {
        var p = table.At(elevationDeg, 1, density, nearS);
        double dt = (p.H + h0) / Math.Max(-p.HDot, 1);
        return p.S + p.SDot * dt;
    }

    /// <summary>The wind the machine works from, m/s, Earth-centred: none, the deck's (the 10 m wind at the anemometer), or
    /// the ballistic wind; with its measuring error (a vector, m/s).</summary>
    static Vec3d Assumed(in ComputerRow c, in Flight f, Vec3d wind10, Vec3d error) => c.Wind switch
    {
        WindInput.Deck => wind10 * SmokeAir.Profile(AnemometerM) + error,
        WindInput.Ballistic => wind10 * f.WindShare + error,
        _ => default,
    };

    /// <summary>A salvo's fall off the machine's aim, m: along the line of fire and to its right. The 10 m wind (m/s, the
    /// way it blows); the machine's measuring errors, in its own σ (a wind vector in knots' σ, the density's and the
    /// velocity's draws); the line's directions; each part's scale on the error ladder (wind; the rest the bias rung).</summary>
    public static (double Range, double Across) Fall(in ComputerRow c, in Flight f, Vec3d wind10, Vec3d windZ, double densityZ, double mvZ,
        Vec3d along, Vec3d across, double windScale, double biasScale)
    {
        var dw = wind10 * f.WindShare - Assumed(c, f, wind10, windZ * (c.WindErrKn * Knot));
        double range = windScale * Vec3d.Dot(dw, along) * f.AlongS
                       + biasScale * (mvZ * c.MvErrPct * f.PerMvPct - densityZ * c.DensityErrPct * f.PerDensityPct);
        return (range, windScale * Vec3d.Dot(dw, across) * f.LagS);
    }

    /// <summary>The machine's own doubt of its ballistics at a range (1 σ): along the line, a share of the range; across it,
    /// rad. The wind it doesn't have (none: all of it; the deck's: its measuring and the wind aloft over it; the ballistic:
    /// its measuring), each way; the density's and the velocity's errors in range.</summary>
    public static (double RangeShare, double LineRad) Doubt(in ComputerRow c, in Flight f, double windMs, double rangeM)
    {
        double err = c.WindErrKn * Knot, w = Math.Max(windMs, 0);
        double sw = c.Wind switch
        {
            WindInput.Deck => Math.Sqrt(err * err + Math.Pow((f.WindShare - SmokeAir.Profile(AnemometerM)) * w, 2)),
            WindInput.Ballistic => err,
            _ => f.WindShare * w,
        };
        double r = Math.Max(rangeM, 1);
        double sr = Math.Sqrt(Math.Pow(sw * f.AlongS, 2) + Math.Pow(c.MvErrPct * f.PerMvPct, 2) + Math.Pow(c.DensityErrPct * f.PerDensityPct, 2));
        return (sr / r, sw * f.LagS / r);
    }
}
