namespace Fleetwright.Sim.Core;

/// <summary>A vector of doubles: world positions are Earth-centred metres, and a double at 6.4e6 m resolves about a
/// nanometre, so the sim needs no floating origin (docs/battle/plan.md, "The world"). Floats only after subtracting a
/// nearby origin, on the renderer's side.</summary>
public readonly record struct Vec3d(double X, double Y, double Z)
{
    public static readonly Vec3d Zero = new(0, 0, 0);
    public static readonly Vec3d UnitX = new(1, 0, 0);
    public static readonly Vec3d UnitY = new(0, 1, 0);
    public static readonly Vec3d UnitZ = new(0, 0, 1);

    public static Vec3d operator +(Vec3d a, Vec3d b) => new(a.X + b.X, a.Y + b.Y, a.Z + b.Z);
    public static Vec3d operator -(Vec3d a, Vec3d b) => new(a.X - b.X, a.Y - b.Y, a.Z - b.Z);
    public static Vec3d operator -(Vec3d a) => new(-a.X, -a.Y, -a.Z);
    public static Vec3d operator *(Vec3d a, double s) => new(a.X * s, a.Y * s, a.Z * s);
    public static Vec3d operator *(double s, Vec3d a) => new(a.X * s, a.Y * s, a.Z * s);
    public static Vec3d operator /(Vec3d a, double s) => new(a.X / s, a.Y / s, a.Z / s);

    public static double Dot(Vec3d a, Vec3d b) => a.X * b.X + a.Y * b.Y + a.Z * b.Z;

    public static Vec3d Cross(Vec3d a, Vec3d b) =>
        new(a.Y * b.Z - a.Z * b.Y, a.Z * b.X - a.X * b.Z, a.X * b.Y - a.Y * b.X);

    public double LengthSquared => X * X + Y * Y + Z * Z;
    public double Length => Math.Sqrt(LengthSquared);

    /// <summary>This over its length; the zero vector stays zero.</summary>
    public Vec3d Normalized()
    {
        double l = Length;
        return l > 0 ? this / l : Zero;
    }
}
