using Fleetwright.Sim.Ships;

namespace Fleetwright.Sim.Combat;

/// <summary>A funnel's plume as it leaves (docs/battle/smoke.md Z0): its smoke, m²/s of extinction area, its gas, m³/s, the
/// gas's speed out, m/s, its buoyancy flux, m⁴/s³, the rise it makes, m, over its time, s, the height it starts at over
/// the sea after the downwash, m, and the downwash itself, m.</summary>
public readonly record struct FunnelPlume(double RateM2s, double GasM3s, double ExitMs, double Flux, double RiseM, double RiseS, double BaseM,
    double DownwashM);

/// <summary>The smoke's sources, one implementation for the field, the map and the benches (docs/battle/smoke.md Z0).</summary>
public static class SmokeSources
{
    /// <summary>Funnel <paramref name="funnel"/> of a class at a speed through the water, kn, in a wind relative to the ship
    /// of <paramref name="relativeWindMs"/>:
    /// <list type="bullet">
    /// <item>the fuel burnt making the power the hull needs at that speed (shipgen's part-load curve), times its smoke a
    /// kg, times the funnel's share;</item>
    /// <item>its gas the same way, out through its exit area (forced draught's fans and natural draught's chimney both
    /// slow with the flow);</item>
    /// <item>the rise by Briggs at its top (the stability there), the downwash from its exit's radius, never below the
    /// funnel's foot.</item>
    /// </list></summary>
    public static FunnelPlume Funnel(ShipClass c, int funnel, double speedKn, double relativeWindMs, in SmokeAir air)
    {
        ref readonly var f = ref c.Funnels[funnel];
        var b = c.Components[f.Component].Bounds;
        double wl = c.Vertical.Waterline, top = b.Top - wl, foot = Math.Max(b.Base - wl, 0);
        double fuel = c.FuelKgS(c.PowerAt(Math.Abs(speedKn))) * f.Share;
        var s = c.Smoke;
        double rate = fuel * s.M2PerKg, gas = fuel * s.GasM3PerKg;
        double exit = f.ExitAreaM2 > 0 ? gas / f.ExitAreaM2 : 0;
        double flux = SmokeModel.BuoyancyFlux(gas, s.GasTempK, air.AirK);
        var (rise, riseS) = SmokeModel.Briggs(flux, relativeWindMs, air.Stability(top));
        double down = SmokeModel.Downwash(exit, relativeWindMs, Math.Sqrt(Math.Max(f.ExitAreaM2, 0) / Math.PI));
        double baseM = Math.Max(top - down, foot);
        return new FunnelPlume(rate, gas, exit, flux, rise, riseS, baseM, top - baseM);
    }
}
