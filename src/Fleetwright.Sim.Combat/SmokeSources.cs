using Fleetwright.Sim.Core;
using Fleetwright.Sim.Ships;

namespace Fleetwright.Sim.Combat;

/// <summary>A funnel's plume as it leaves (docs/battle/smoke.md Z0): its smoke, m²/s of extinction area, its gas, m³/s, the
/// gas's speed out, m/s, its buoyancy flux, m⁴/s³, the rise it makes, m, over its time, s, the height it starts at over
/// the sea after the downwash, m, and the downwash itself, m; the firing state's share over well fired (T3), and her
/// lee's (T3): the spread it starts with, up and across, m (0: none), and the rise it took away, m.</summary>
public readonly record struct FunnelPlume(double RateM2s, double GasM3s, double ExitMs, double Flux, double RiseM, double RiseS, double BaseM,
    double DownwashM, double Forcing = 1, double LeeSigmaZ = 0, double LeeSigmaY = 0, double LeeRiseLostM = 0);

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

    /// <summary>How high a class's funnel smoke shows at most, m (docs/battle/smoke.md Z6, the world's sight reach): at
    /// full power in still air (the wind over her decks her own speed, from ahead; the air's turbulence the settings'
    /// default wind's), each funnel's plume risen and spread a minute past its rise, its centre and two σ over it. A
    /// slower ship in a following wind can rise higher; the reach counts the usual case.</summary>
    public static double TopM(ShipClass c, SimSettings s)
    {
        var air = SmokeAir.Of(new WeatherHere(25_000, 0, s.DefaultWindMs));
        double rel = Math.Max(c.Motion.MaxSpeedKn * 0.514444, SmokeAir.CalmMs), top = 0;
        for (int i = 0; i < c.Funnels.Length; i++)
        {
            var f = Funnel(c, i, c.Motion.MaxSpeedKn, rel, 0, air, s);
            if (!(f.RateM2s > 0))
                continue;
            var p = SmokeModel.State(SmokeModel.Born(Earth.Position(0, 0), 0, AirFrame.Identity, f.BaseM, f.RiseM, f.RiseS, 15, 15, 1, 0, SmokeTint.Coal, air),
                f.RiseS + 60, AirFrame.Identity, air);
            top = Math.Max(top, p.Z + 2 * Math.Sqrt(p.SigmaZ2));
        }
        return top;
    }

    /// <summary>Funnel <paramref name="funnel"/> of a class at a speed through the water, kn, in a wind relative to the ship
    /// of <paramref name="relativeWindMs"/> from <paramref name="relativeOffBowDeg"/> off her bow (0 from ahead):
    /// <list type="bullet">
    /// <item>the fuel burnt making the power the hull needs at that speed (shipgen's part-load curve), times its smoke a
    /// kg well fired, times the firing state's (<see cref="Forcing"/>), times the funnel's share;</item>
    /// <item>its gas the same way, out through its exit area (forced draught's fans and natural draught's chimney both
    /// slow with the flow);</item>
    /// <item>the rise by Briggs at its top (the stability there), the downwash from its exit's radius, never below the
    /// funnel's foot;</item>
    /// <item>her lee (<see cref="Lee"/>): the spread it starts with and the rise it takes away.</item>
    /// </list></summary>
    public static FunnelPlume Funnel(ShipClass c, int funnel, double speedKn, double relativeWindMs, double relativeOffBowDeg, in SmokeAir air,
        SimSettings settings)
    {
        ref readonly var f = ref c.Funnels[funnel];
        var b = c.Components[f.Component].Bounds;
        double wl = c.Vertical.Waterline, top = b.Top - wl, foot = Math.Max(b.Base - wl, 0);
        double power = c.PowerAt(Math.Abs(speedKn));
        double fuel = c.FuelKgS(power) * f.Share;
        var s = c.Smoke;
        double forcing = Forcing(c.Motion.RatedShp > 0 ? power / c.Motion.RatedShp : 0, settings);
        double rate = fuel * s.M2PerKg * forcing, gas = fuel * s.GasM3PerKg;
        double exit = f.ExitAreaM2 > 0 ? gas / f.ExitAreaM2 : 0;
        double flux = SmokeModel.BuoyancyFlux(gas, s.GasTempK, air.AirK);
        var (rise, riseS) = SmokeModel.Briggs(flux, relativeWindMs, air.Stability(top));
        double down = SmokeModel.Downwash(exit, relativeWindMs, Math.Sqrt(Math.Max(f.ExitAreaM2, 0) / Math.PI));
        double baseM = Math.Max(top - down, foot);
        var (sz, sy) = Lee(c, top, relativeOffBowDeg);
        double kept = LeeRise(rise, Math.Sqrt(2) * sz);
        return new FunnelPlume(rate, gas, exit, flux, kept, riseS, baseM, top - baseM, forcing, sz, sy, rise - kept);
    }

    /// <summary>The firing state (docs/battle/smoke.md T3): the smoke a kg of fuel makes, over well fired's, at a load (the
    /// power over the rated). Pushed past what its air and furnace burn cleanly a fire makes black smoke: none up to
    /// <see cref="SimSettings.SmokeForcingFrom"/> of the rated power, rising as the square of the load over it to
    /// <see cref="SimSettings.SmokeForcedFactor"/> at the rated power, and on into the overload [EST: no figures yet
    /// (research request 1); the mechanism's shape, its size the tuning's].</summary>
    public static double Forcing(double load, SimSettings s)
    {
        double x = Math.Max(load - s.SmokeForcingFrom, 0) / Math.Max(1 - s.SmokeForcingFrom, 1e-6);
        return 1 + (s.SmokeForcedFactor - 1) * x * x;
    }

    /// <summary>Her lee (docs/battle/smoke.md T3, the building wake of the EPA's ISC models, Huber and Snyder (1976) and
    /// Schulman and Scire (1980)): her upper works a block as high as her side's area over her length (h_b) and as wide
    /// as she shows across the relative wind (h_w, her length and beam by its angle); with L_b the lesser of the two, a
    /// funnel top under h_b + 1.5 L_b (good engineering practice's stack height) is in the wake, wholly under
    /// h_b + 0.5 L_b, partly between. In it the plume starts spread 0.7 h_b up and 0.35 h_w across (at most 5 h_b wide),
    /// as the wake's at three block heights downwind. The spread it starts with, up and across, m.</summary>
    public static (double SigmaZ, double SigmaY) Lee(ShipClass c, double funnelTopM, double relativeOffBowDeg)
    {
        double len = Math.Max(c.Silhouette.LengthM, 1), beam = Math.Max(c.Motion.BeamM, 1);
        double hb = Sight.AreaAbove(c.Numbers(c.Silhouette.SideAreaM2), 0) / len;
        double a = relativeOffBowDeg * Math.PI / 180, hw = Math.Abs(len * Math.Sin(a)) + Math.Abs(beam * Math.Cos(a));
        double lb = Math.Min(hb, hw), whole = hb + 0.5 * lb, none = hb + 1.5 * lb;
        double w = Math.Clamp((none - funnelTopM) / Math.Max(none - whole, 1e-6), 0, 1);
        return (w * 0.7 * hb, w * 0.35 * Math.Min(hw, 5 * hb));
    }

    /// <summary>A plume's final rise, m, cut by its starting in the lee already diluted (Schulman and Scire's): a plume of
    /// radius <paramref name="radiusM"/> (√2 σ_z, its spread as a uniform disc) entrains faster, so its rise Z solves
    /// Z³ + (3R₀/β)Z² + (3R₀²/β²)Z = ΔH³ for the rise ΔH it would make from a point, β 0.6 (Newton's, from ΔH).</summary>
    public static double LeeRise(double riseM, double radiusM)
    {
        if (!(riseM > 0) || !(radiusM > 0))
            return riseM;
        const double Beta = 0.6;
        double a = 3 * radiusM / Beta, b = 3 * radiusM * radiusM / (Beta * Beta), target = riseM * riseM * riseM, z = riseM;
        for (int i = 0; i < 20; i++)
        {
            double fz = z * z * z + a * z * z + b * z - target, dz = 3 * z * z + 2 * a * z + b;
            double next = Math.Max(z - fz / dz, 0);
            if (Math.Abs(next - z) < 1e-6)
                return next;
            z = next;
        }
        return z;
    }
}
