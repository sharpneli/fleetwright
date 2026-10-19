using Fleetwright.Shipgen;

namespace Fleetwright.Sim.Ships;

/// <summary>A design's gun and torpedo blocks as the sim's rows: the design's own (`gun_tech`, `torpedo_tech`), else the
/// catalogue's (<see cref="GunTemplates"/>, <see cref="TorpedoTemplates"/>), else the default when it names none. Strict,
/// as the class compile is: a name in neither, or a number missing, throws.</summary>
public static class WeaponBlocks
{
    /// <summary>A battery's gun block by the name it gives (null: the default).</summary>
    public static GunTechRow Gun(Design design, string? name)
    {
        var g = GunTemplates.Resolve(design, name)
            ?? throw new ShipCompileException($"gun: no block \"{name}\" in the design's gun_tech or the catalogue");
        string at = $"gun \"{name ?? GunTemplates.Default}\"";
        return new GunTechRow
        {
            ReloadShare = N(g.ReloadShare, at, "reload_share"), HeavySlow = N(g.HeavySlow, at, "heavy_slow"),
            HeavySlowUnder40Cal = N(g.HeavySlowUnder40Cal, at, "heavy_slow_under_40_cal"), HeavySlowUnder30Cal = N(g.HeavySlowUnder30Cal, at, "heavy_slow_under_30_cal"),
            TrainShare = N(g.TrainShare, at, "train_share"), DpTrainShare = N(g.DpTrainShare, at, "dp_train_share"),
            MaxElevationDeg = N(g.MaxElevationDeg, at, "max_elevation_deg"), DpMaxElevationDeg = N(g.DpMaxElevationDeg, at, "dp_max_elevation_deg"),
            PatternPct = N(g.PatternPct, at, "pattern_pct"), PatternExp = N(g.PatternExp, at, "pattern_exp"), PatternLightPct = N(g.PatternLightPct, at, "pattern_light_pct"),
            Propellant = Propellants.Of(g.Propellant) ?? throw new ShipCompileException($"{at}: no propellant \"{g.Propellant}\""),
            BlackPowderUnderCal = N(g.BlackPowderUnderCal, at, "black_powder_under_cal"),
        };
    }

    /// <summary>The design's torpedo block (torpedoes.torpedo; none: the default).</summary>
    public static TorpedoTechRow Torpedo(Design design)
    {
        string? name = design.Torpedoes?.Torpedo;
        var t = TorpedoTemplates.Resolve(design, name)
            ?? throw new ShipCompileException($"torpedo: no block \"{name}\" in the design's torpedo_tech or the catalogue");
        string at = $"torpedo \"{name ?? TorpedoTemplates.Default}\"";
        return new TorpedoTechRow
        {
            FastKn = N(t.FastKn, at, "fast_kn"), FastRangeM = N(t.FastRangeM, at, "fast_range_m"), SlowKn = N(t.SlowKn, at, "slow_kn"),
            SlowRangeM = N(t.SlowRangeM, at, "slow_range_m"), ChargeShare = N(t.ChargeShare, at, "charge_share"),
            MountTrainDegS = N(t.MountTrainDegS, at, "mount_train_deg_s"), MountReloadS = N(t.MountReloadS, at, "mount_reload_s"),
            TubeReloadS = N(t.TubeReloadS, at, "tube_reload_s"), GyroDeg = N(t.GyroDeg, at, "gyro_deg"), FailShare = N(t.Fail, at, "fail"),
            DudShare = N(t.Dud, at, "dud"),
        };
    }

    static double N(double? v, string at, string what) => v ?? throw new ShipCompileException($"{at}: no {what}");
}
