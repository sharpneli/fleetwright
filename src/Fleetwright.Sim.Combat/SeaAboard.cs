using Fleetwright.Shipgen;
using Fleetwright.Sim.Core;
using Fleetwright.Sim.Ships;

namespace Fleetwright.Sim.Combat;

/// <summary>The sea coming aboard a ship (docs/battle/error-sources.md ES4): green water on her guns and glass, and her bow's
/// spray on her glass, by the seakeeping law the research cites (Ochi; Nicholson and Price:
/// research/simulation/ship-motion/ship-motion-gunnery-research.md §1.2) in place of its wetness index and BL1f's casemate
/// rule: the sea reaches a point on her when her motion against it there (<see cref="RollSea.Relative"/>) passes the point's
/// height over it, at Rice's rate. Her points: her stem, then each mount, then each eye (x, y from amidships). Each a pure
/// function of the sea's variances at the point, her attitude, speed and the wind; the gunnery and the roll bench both
/// call it.</summary>
public static class SeaAboard
{
    /// <summary>The variances at a sea step's values (<see cref="SeaMotion"/>) on the world's sea.</summary>
    public static void Work(SimSettings s, RollSea sea, SeaMotion m) =>
        sea.Relative(m.HeadingDeg, m.SpeedMs, Hull(s, m.Class!, m.Gm), m.X, m.Y, m.M0, m.M2);

    public const double Gravity = 9.81;

    /// <summary>The step a ship's sea is worked out at (her sea moves a step when one of these does): the world's sea, her heading
    /// (10°), speed (0.5 m/s, ahead) and GM (5% of her own); the key, and the step's own heading, speed and GM share, as the
    /// sea's motion against her is worked out at them (the same whenever it's asked: any compression).</summary>
    public static long Key(int seaId, double headingDeg, double speedMs, double gmShare, out double heading, out double speed, out double gm)
    {
        int h = (int)(((headingDeg % 360 + 360) % 360) / 10) % 36, v = (int)Math.Clamp(speedMs / 0.5, 0, 4095);
        int g = (int)Math.Clamp(Math.Round(20 * gmShare), 1, 4095);
        (heading, speed, gm) = (h * 10 + 5, (v + 0.5) * 0.5, g / 20.0);
        return ((long)seaId << 40) | ((long)h << 24) | ((long)v << 12) | (uint)g;
    }

    public static int Points(ShipClass cls) => 1 + cls.Mounts.Length + cls.Eyes.Length;
    public static int MountPoint(int mount) => 1 + mount;
    public static int EyePoint(ShipClass cls, int eye) => 1 + cls.Mounts.Length + eye;

    /// <summary>Her points' places, x and y, as <see cref="Points"/> orders them.</summary>
    public static void Place(ShipClass cls, Span<double> x, Span<double> y)
    {
        (x[0], y[0]) = (cls.Hull.X1, 0);
        for (int j = 0; j < cls.Mounts.Length; j++)
            (x[MountPoint(j)], y[MountPoint(j)]) = (cls.Mounts[j].X, cls.Mounts[j].Y);
        for (int e = 0; e < cls.Eyes.Length; e++)
            (x[EyePoint(cls, e)], y[EyePoint(cls, e)]) = (cls.Eyes[e].X, cls.Eyes[e].Y);
    }

    /// <summary>Her hull as the sea's motion against her reads it, her roll's natural period stretched as she's lost GM
    /// (a share of her own: T ∝ 1/√GM), a point's patch <see cref="SimSettings.SprayPatch"/> of her length.</summary>
    public static Seakeeping Hull(SimSettings s, ShipClass cls, double gmShare)
    {
        var m = cls.Motion;
        return new Seakeeping(m.LengthM, m.BeamM, m.RollPeriodS / Math.Sqrt(Math.Max(gmShare, 1e-3)), m.RollDamping, s.RollExcitation,
            s.PitchPeriodK * Math.Sqrt(Math.Max(m.DraughtM, 0.5)), s.PitchDamping, s.SprayPatch * m.LengthM);
    }

    /// <summary>A point of hers over the sea as she has settled (trimmed, heeled, down), m: z in the class's heights.</summary>
    public static double OverSea(ShipClass cls, in Attitude a, double x, double y, double z)
    {
        var (z0, dx, dy) = Flotation.Sea(cls.Vertical.Waterline, cls.Hydrostatics.Lcf, a.SinkageM, double.DegreesToRadians(a.TrimDeg),
            double.DegreesToRadians(a.HeelDeg));
        return z - (z0 + dx * x + dy * y);
    }

    /// <summary>Her bow wave at speed where it stands at x, m over the sea (<see cref="SimSettings.SprayBowWave"/>).</summary>
    public static double BowWave(SimSettings s, ShipClass cls, double speedMs, double x)
    {
        double share = Math.Clamp(1 - (cls.Hull.X1 - x) / Math.Max(s.SprayBowWaveSpan * cls.Motion.LengthM, 1), 0, 1);
        return s.SprayBowWave * speedMs * speedMs / (2 * Gravity) * share;
    }

    /// <summary>A mount's guns' axis, in the class's heights: their ports, where the sea comes in.</summary>
    public static double GunAxis(ShipClass cls, int mount)
    {
        ref readonly var comp = ref cls.Components[cls.Mounts[mount].Component];
        return comp.Bounds.Base + 0.55 * (comp.Bounds.Top - comp.Bounds.Base);
    }

    /// <summary>How often green water reaches a height (z, the class's) at a point of hers (x, y; the sea's variances
    /// there), a second: the sea passing it over her bow wave there (<see cref="RollSea.SeaReach"/>).</summary>
    public static double Green(SimSettings s, ShipClass cls, in Attitude a, double speedMs, double x, double y, double m0, double m2, double z) =>
        RollSea.SeaReach(m0, m2, OverSea(cls, a, x, y, z) - BowWave(s, cls, speedMs, x));

    /// <summary>How often her bow's spray reaches a height (z, the class's) at a point of hers, a second: the sea rising to
    /// her flare at the stem (<see cref="SimSettings.SprayFlareShare"/> of her freeboard there, less her bow wave; the sea's
    /// variances at her stem) throws a sheet up at a share of her speed and a gain on the sea's rate as it rises
    /// (<see cref="SimSettings.SpraySpeedShare"/>, <see cref="SimSettings.SprayJetGain"/>), and the wind over her deck (her
    /// speed and the wind's along her, at the point's height) carries it aft: it reaches the point if it's still that high
    /// when it gets there. The sea's rate at a crossing is Rayleigh about √m₂ (Rice), so the share of the sheets that reach
    /// is the chance it rose fast enough.</summary>
    public static double Spray(SimSettings s, ShipClass cls, in Attitude a, double speedMs, double headingDeg, in WeatherHere wx,
        double stemM0, double stemM2, double x, double y, double z)
    {
        if (!(stemM0 > 0 && stemM2 > 0))
            return 0;
        double x1 = cls.Hull.X1, v = Math.Max(speedMs, 0);
        double flare = s.SprayFlareShare * OverSea(cls, a, x1, 0, cls.Hull.DeckTop(x1)) - BowWave(s, cls, v, x1);
        double sheets = flare <= 0 ? Math.Sqrt(stemM2 / stemM0) / (2 * Math.PI) : RollSea.SeaReach(stemM0, stemM2, flare);
        if (!(sheets > 1e-9))
            return 0;
        double zp = OverSea(cls, a, x, y, z), d = Math.Max(x1 - x, 1);
        double deck = v + wx.WindMs * SmokeAir.Profile(zp) * Math.Cos(double.DegreesToRadians(wx.WindFromDeg - headingDeg));
        if (deck <= 0.5)
            return 0;   // the wind over her deck from aft: the sheet falls back at her bow
        double tau = d / deck, rise = (Math.Max(zp - flare, 0) + 0.5 * Gravity * tau * tau) / tau;
        double need = (rise - s.SpraySpeedShare * v) / Math.Max(s.SprayJetGain, 1e-6);
        return need <= 0 ? sheets : sheets * Math.Exp(-need * need / (2 * stemM2));
    }

    /// <summary>The share of the time a point wetted at a rate (a second) is out, each wetting clearing in its own time,
    /// s.</summary>
    public static double Out(double rate, double clearS) => rate > 0 ? 1 - Math.Exp(-rate * clearS) : 0;
}

/// <summary>A ship's sea (<see cref="SeaAboard"/>): her class's points (<see cref="SeaAboard.Place"/>) and the variances of the
/// sea's height against each and of its rate (<see cref="RollSea.Relative"/>), worked out at the step's heading, speed and GM
/// share (<see cref="Key"/>: the sea's and the step's). Made as she spawns, its arrays her class's; the gunnery keeps it.</summary>
public sealed class SeaMotion
{
    public ShipClass? Class;
    public long Key = -1;
    public int Stamp;
    public double HeadingDeg, SpeedMs, Gm = 1;
    public double[] X = [], Y = [], M0 = [], M2 = [];

    public void Reset(ShipClass cls)
    {
        int n = SeaAboard.Points(cls);
        if (X.Length != n)
            (X, Y, M0, M2) = (new double[n], new double[n], new double[n], new double[n]);
        (Class, Key, Stamp, Gm) = (cls, -1, 0, 1);
        Array.Clear(M0);
        Array.Clear(M2);
        Array.Clear(Y);
        SeaAboard.Place(cls, X, Y);
    }
}
