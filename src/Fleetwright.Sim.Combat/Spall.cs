using Fleetwright.Sim.Ships;

namespace Fleetwright.Sim.Combat;

/// <summary>A spall cloud off the back of a plate that held (docs/battle/shells-armour-damage.md E3; damage/14): a disc of
/// the back face, part of it one big piece (the plug or the scab) and the rest flakes, flying in a cone about an axis
/// halfway between the plate's normal and the shot line. Not a burst: no gas, no shock, no fire, no breach.</summary>
public readonly record struct SpallCloud(P3 Origin, P3 Axis, double Severity, double MassKg, double BigKg, double FlakeKg, double Flakes,
    double AxialMs)
{
    /// <summary>The big piece's speed: it leaves slower than the flakes (14 §3.2 [EST]).</summary>
    public double BigMs => Spall.BigSpeedShare * AxialMs;

    public double BigJ => 0.5 * BigKg * BigMs * BigMs;

    /// <summary>A flake's energy θ off the axis, J: its speed falls as cos(1.92θ) (Verolme [SRC TNO]).</summary>
    public double FlakeJ(double thetaRad)
    {
        double v = AxialMs * Math.Max(Math.Cos(1.92 * thetaRad), 0);
        return 0.5 * FlakeKg * v * v;
    }
}

/// <summary>The spall model's numbers and the cloud's geometry (14 §3-4). Pure and allocation-free.</summary>
public static class Spall
{
    /// <summary>The scab's depth as a share of the plate (HESH scabs 0.17-0.25 T [SRC]); a flake t_s × 1.5 t_s × 1.5 t_s
    /// [EST]; the axial speed 0.25 × the normal striking speed at s = 1, (0.4 + 0.6 s) of it (bracketed by HESH's 36-75
    /// m/s [EST]); the big piece at 0.6 of it [EST]; the edge at 45° (TNO 30-50° [SRC]), the pieces densest at 0.4 of it
    /// (18°) [SRC TNO]; the big piece lands within 15° of the axis [EST].</summary>
    public const double ScabShare = 0.2, FlakeAspect = 1.5, SpeedK = 0.25, BigSpeedShare = 0.6, EdgeDeg = 45, PeakShare = 0.4, BigConeDeg = 15;

    const double Rho = 7850;
    static readonly double EdgeRad = double.DegreesToRadians(EdgeDeg);

    /// <summary>The share of the pieces per steradian θ off the axis (14's spall_ref.py density, normalised over the
    /// cone): rising from the axis to a peak at 0.4 of the edge, gone at it. About 70% lie inside 30°.</summary>
    public static double Density(double thetaRad) => Relative(thetaRad) / Norm;

    static double Relative(double thetaRad)
    {
        double x = thetaRad / EdgeRad;
        if (x >= 1 || x < 0)
            return 0;
        return x < PeakShare ? 0.35 + x / PeakShare : 1.35 * (1 - x) / (1 - PeakShare);
    }

    static readonly double Norm = Integrate(EdgeRad);

    /// <summary>∫₀^θ of the relative density over the solid angle.</summary>
    static double Integrate(double thetaRad)
    {
        const int k = 200;
        double n = 0, d = Math.Min(thetaRad, EdgeRad) / k;
        for (int i = 0; i < k; i++)
        {
            double th = (i + 0.5) * d;
            n += Relative(th) * 2 * Math.PI * Math.Sin(th) * d;
        }
        return n;
    }

    // the share of the pieces inside θ, by whole degree to the edge
    static readonly double[] Inside = Enumerable.Range(0, (int)EdgeDeg + 1).Select(d => Integrate(double.DegreesToRadians(d)) / Norm).ToArray();

    /// <summary>The share of the pieces within θ of the axis.</summary>
    public static double ShareInside(double thetaRad)
    {
        double deg = Math.Clamp(double.RadiansToDegrees(thetaRad), 0, EdgeDeg);
        int i = Math.Min((int)deg, Inside.Length - 2);
        return Inside[i] + (deg - i) * (Inside[i + 1] - Inside[i]);
    }

    /// <summary>The cloud off a plate tMm thick that stopped a shell of a calibre at v m/s, obliquity ob, its severity s
    /// (14 §3.2): a disc D + T across and 0.2 T deep, s of it; its big piece's share of it once s ≥ 0.5.</summary>
    public static SpallCloud Stopped(P3 origin, P3 axis, double calibreMm, double tMm, double v, double obliquityDeg, double s, double bigShare) =>
        Cloud(origin, axis, s, (calibreMm + tMm) / 1000, tMm, SpeedK * v * Math.Cos(double.DegreesToRadians(obliquityDeg)) * (0.4 + 0.6 * s), bigShare);

    /// <summary>A heavy shell glancing off a thin roof drives it in (Dunkerque at Mers-el-Kébir; 14 §2.2): a cloud at s
    /// 0.5, a disc 2D across [EST, one case].</summary>
    public static SpallCloud Roof(P3 origin, P3 axis, double calibreMm, double tMm, double v, double obliquityDeg, double bigShare) =>
        Cloud(origin, axis, 0.5, 2 * calibreMm / 1000, tMm, SpeedK * v * Math.Cos(double.DegreesToRadians(obliquityDeg)) * 0.7, bigShare);

    static SpallCloud Cloud(P3 origin, P3 axis, double s, double acrossM, double tMm, double axialMs, double bigShare)
    {
        double ts = ScabShare * tMm / 1000;
        double mass = s * Rho * Math.PI / 4 * acrossM * acrossM * ts;
        double big = s >= 0.5 ? bigShare * mass : 0;
        double flake = Rho * ts * (FlakeAspect * ts) * (FlakeAspect * ts);
        return new SpallCloud(origin, axis, s, mass, big, flake, flake > 0 ? (mass - big) / flake : 0, axialMs);
    }

    /// <summary>Backing behind the plate (14 §4.3: the ironclads' teak, Class A's concrete, the inner skin) catches it: of a
    /// mass per area at least the scab's it takes every flake, twice that the big piece too; less thins the flakes by
    /// the ratio. backingSteelMm is the layers' mass as steel mm.</summary>
    public static SpallCloud Backed(in SpallCloud c, double plateMm, double backingSteelMm)
    {
        if (backingSteelMm <= 0)
            return c;
        double ratio = backingSteelMm / Math.Max(ScabShare * plateMm, 1e-9);
        return c with { Flakes = c.Flakes * Math.Max(1 - ratio, 0), BigKg = ratio >= 2 ? 0 : c.BigKg };
    }

    /// <summary>The flakes expected to strike a box with an energy of at least fragility J: the pieces' density at its
    /// angle off the axis times its presented area over its distance squared (14 §6.3: λ = N·f(θ)·A/r²).</summary>
    public static double Hits(in SpallCloud c, in Box b, double fragilityJ)
    {
        if (!(c.Flakes > 0))
            return 0;
        double cx = (b.X0 + b.X1) / 2, cy = (b.Y0 + b.Y1) / 2, cz = (b.Base + b.Top) / 2;
        double rx = cx - c.Origin.X, ry = cy - c.Origin.Y, rz = cz - c.Origin.Z, rc = Math.Sqrt(rx * rx + ry * ry + rz * rz);
        if (rc < 1e-6)
            return c.Flakes;
        double th = Theta(c, rx / rc, ry / rc, rz / rc);
        if (th >= EdgeRad || c.FlakeJ(th) < fragilityJ)
            return 0;
        double dx = b.X1 - b.X0, dy = b.Y1 - b.Y0, dz = b.Top - b.Base;
        double area = Math.Abs(rx / rc) * dy * dz + Math.Abs(ry / rc) * dx * dz + Math.Abs(rz / rc) * dx * dy;
        double near = Math.Max(Effects.BoxDistance(b, c.Origin), 0.3), r = Math.Max(near, 0.5 * rc);
        return c.Flakes * Density(th) * Math.Min(area / (r * r), 2 * Math.PI);
    }

    /// <summary>The flakes with an energy of at least fragility J, of all of them: those inside the angle where the speed
    /// falls under it (what lands in a space that takes the whole cloud).</summary>
    public static double Counted(in SpallCloud c, double fragilityJ)
    {
        if (!(c.Flakes > 0) || c.FlakeJ(0) < fragilityJ)
            return 0;
        // ½m(v cos 1.92θ)² = J at cos 1.92θ = √(J/E₀)
        double th = Math.Acos(Math.Sqrt(fragilityJ / c.FlakeJ(0))) / 1.92;
        return c.Flakes * ShareInside(th);
    }

    /// <summary>The angle off the cloud's axis of a unit direction.</summary>
    public static double Theta(in SpallCloud c, double ux, double uy, double uz) =>
        Math.Acos(Math.Clamp(ux * c.Axis.X + uy * c.Axis.Y + uz * c.Axis.Z, -1, 1));
}
