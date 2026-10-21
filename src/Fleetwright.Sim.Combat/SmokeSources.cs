using Fleetwright.Sim.Ships;

namespace Fleetwright.Sim.Combat;

/// <summary>A funnel's plume as it leaves (docs/battle/smoke.md Z0): its smoke, m²/s of extinction area, its gas, m³/s, the
/// gas's speed out, m/s, its buoyancy flux, m⁴/s³, the rise it makes, m, over its time, s, the height it starts at over
/// the sea after the downwash, m, and the downwash itself, m.</summary>
public readonly record struct FunnelPlume(double RateM2s, double GasM3s, double ExitMs, double Flux, double RiseM, double RiseS, double BaseM,
    double DownwashM);

/// <summary>A fire's smoke as it leaves its opening (docs/battle/smoke.md Z4): its smoke, m²/s, its flames' height over
/// the opening, m (the smoke leaves from their tips), and the rise it makes, m, over its time, s.</summary>
public readonly record struct FirePlume(double RateM2s, double FlameM, double RiseM, double RiseS);

/// <summary>A mount's smoke when it fires (docs/battle/smoke.md Z4): its extinction area, m², where it ends up after the
/// muzzle's jet (along the bore over the sea, m, and up, m), its spread at the end of the jet, m (the fireball's core), and
/// the rise it makes, m, over its time, s.</summary>
public readonly record struct GunPuff(double AreaM2, double AlongM, double UpM, double SigmaM, double RiseM, double RiseS);

/// <summary>The smoke's sources, one implementation for the field, the map and the benches (docs/battle/smoke.md Z0, Z4).</summary>
public static class SmokeSources
{
    /// <summary>A fire's smoke (hits-and-fires.md I5, moved here from the map's look in Z4): the fuel it burns at a heat
    /// of combustion <see cref="FireHeatMJkg"/> (cellulosics and cable), a soot yield from a growing fire's to a developed
    /// one's, burning charges' their own, and soot's mass extinction (Mulholland) [EST, fire research gaps 1 and 3].</summary>
    public const double FireHeatMJkg = 18, FireYieldGrow = 0.03, FireYieldDeveloped = 0.08, FireYieldCharges = 0.02, SootM2Kg = 8700;

    /// <summary>The share of a fire's heat that goes into its plume (the rest radiated): Briggs' flux is the plume's.</summary>
    public const double FireConvectiveShare = 0.7;

    /// <summary>A fire of <paramref name="mw"/> MW (and its burning charges' <paramref name="chargesMW"/>) in a phase,
    /// out of an opening <paramref name="widthM"/> across, in a wind relative to the ship of
    /// <paramref name="relativeWindMs"/>: its soot (a developed fire's yield, a growing one's, a fire coming under control
    /// between), its flames' height by Heskestad (never under 0.4 of the opening), and its rise by Briggs from its
    /// convected heat (10 MW developed: ~350 m²/s, rising ~65 m in 7 m/s).</summary>
    public static FirePlume Fire(double mw, double chargesMW, BurnPhase phase, double widthM, double relativeWindMs, in SmokeAir air)
    {
        double q = Math.Max(mw, 0), qc = Math.Max(chargesMW, 0), d = Math.Max(widthM, 0.5);
        double dev = phase is BurnPhase.Developed or BurnPhase.Decay ? 1 : phase == BurnPhase.Control ? 0.3 : 0;
        double yield = FireYieldGrow + (FireYieldDeveloped - FireYieldGrow) * dev;
        double rate = (q / FireHeatMJkg * yield + qc / FireHeatMJkg * FireYieldCharges) * SootM2Kg;
        double flame = Math.Max(MagazineBlast.Heskestad((q + qc) * 1000, d), 0.4 * d);
        var (rise, riseS) = SmokeModel.Briggs(SmokeModel.HeatFlux(FireConvectiveShare * (q + qc)), relativeWindMs, air.Stability(flame));
        return new FirePlume(rate, flame, rise, riseS);
    }

    /// <summary>A mount's smoke when <paramref name="barrels"/> of its guns fire (muzzle-flashes.md F1, the gun's
    /// <see cref="MuzzleFlash"/>): the solids of its charges (<see cref="MuzzleFlash.SmokeM2"/>, a share by the barrels
    /// that fired), carried along the bore by the jet to 2.5 of the expected fireball's length λ_f past the Mach disk, a
    /// core 0.35 λ_f across, rising 0.6 λ_f over 3 + 0.05 λ_f s (the map's gun puffs, muzzle-flash research §5). The water
    /// fog is left out: it's gone in seconds, before a sight line could read it.</summary>
    public static GunPuff Gun(in MuzzleFlash gun, int barrels, double elevationDeg)
    {
        double lf = gun.ExpectedFlashLengthM, el = elevationDeg * Math.PI / 180, jet = gun.MachDiskM + 2.5 * lf;
        double share = Math.Clamp(barrels / (double)Math.Max(gun.Guns, 1), 0, 1);
        return new GunPuff(gun.SmokeM2 * share, jet * Math.Cos(el), jet * Math.Sin(el), 0.35 * lf, 0.6 * lf, 3 + 0.05 * lf);
    }

    /// <summary>A magazine's smoke (magazine-explosions.md X6, the drawing's body and after-smoke, here in Z4): the
    /// fireball's soot, its column optical depth 8 over a Gaussian σ of a quarter of its diameter (A = 8·2πσ²), centred
    /// 0.35 of its diameter over the deck and lifting half its diameter over two of its lives; then the wreck's fires,
    /// <see cref="WreckSmokeM2s"/> times the scale squared, fading over <see cref="WreckSmokeFadeS"/> [EST].</summary>
    public const double WreckSmokeM2s = 15_000, WreckSmokeFadeS = 40;

    /// <summary>The fireball's soot of a magazine of <paramref name="massKg"/> burned fast: its extinction area, m², its
    /// σ, m, its centre's height over the deck, m, and the rise it makes, m, over its time, s.</summary>
    public static (double AreaM2, double SigmaM, double UpM, double RiseM, double RiseS) Fireball(double massKg)
    {
        var (d, life) = MagazineBlast.Fireball(massKg);
        double sigma = d / 4;
        return (8 * 2 * Math.PI * sigma * sigma, sigma, 0.35 * d, MagazineBlast.LiftD * d, 2 * life);
    }

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
