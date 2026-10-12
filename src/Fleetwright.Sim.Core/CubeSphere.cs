namespace Fleetwright.Sim.Core;

/// <summary>The sphere as a cube's six faces, each seen through its own gnomonic projection: a point p on face f
/// (the axis F nearest p) is at (p·U / p·F, p·V / p·F) in the face's plane, a square from -1 to 1. Great circles are
/// straight lines there, so a leg across a face is a 2D line. Adjacent faces' coordinates agree along their shared
/// edge (both are the cube's surface), so grids of the same size meet cell to cell across the seams. The map's baked
/// land tiles on it, and <see cref="Shore"/> grids it.</summary>
public static class CubeSphere
{
    /// <summary>The faces: the centre's axis, and the plane's two axes.</summary>
    static readonly (Vec3d F, Vec3d U, Vec3d V)[] faces =
    [
        (Vec3d.UnitX, Vec3d.UnitY, Vec3d.UnitZ), (-Vec3d.UnitX, -Vec3d.UnitY, Vec3d.UnitZ),
        (Vec3d.UnitY, -Vec3d.UnitX, Vec3d.UnitZ), (-Vec3d.UnitY, Vec3d.UnitX, Vec3d.UnitZ),
        (Vec3d.UnitZ, Vec3d.UnitY, -Vec3d.UnitX), (-Vec3d.UnitZ, Vec3d.UnitY, Vec3d.UnitX),
    ];

    public const int FaceCount = 6;

    public static (Vec3d F, Vec3d U, Vec3d V) Face(int face) => faces[face];

    /// <summary>The face a point (any length) is on: its largest component's axis, ties to the earlier face.</summary>
    public static int FaceOf(Vec3d p)
    {
        double ax = Math.Abs(p.X), ay = Math.Abs(p.Y), az = Math.Abs(p.Z);
        if (ax >= ay && ax >= az)
            return p.X >= 0 ? 0 : 1;
        if (ay >= az)
            return p.Y >= 0 ? 2 : 3;
        return p.Z >= 0 ? 4 : 5;
    }

    /// <summary>A point (any length, in front of the face's plane: p·F > 0) in a face's gnomonic plane. Points on
    /// other faces project outside the square.</summary>
    public static (double X, double Y) Project(Vec3d p, int face)
    {
        var (f, u, v) = faces[face];
        double w = Vec3d.Dot(p, f);
        return (Vec3d.Dot(p, u) / w, Vec3d.Dot(p, v) / w);
    }

    /// <summary>The unit vector at (x, y) of a face's plane (any x and y: outside the square is on other faces).</summary>
    public static Vec3d Unproject(int face, double x, double y)
    {
        var (f, u, v) = faces[face];
        return (f + u * x + v * y).Normalized();
    }
}
