namespace Fleetwright.Sim.Core;

/// <summary>A ship's local frame in the world: the frame the hitboxes and sprites use, +x to the bow, +y to
/// starboard, heights up (over the frame's origin). That frame is left-handed: the naval body frame's x and y with
/// its z (down) turned to up, because shipgen measures heights. So it's three explicit world vectors, and points map
/// by components; no right-handed cross product or look-at matrix may stand in for it (docs/battle/plan.md, step
/// 1.1). Inside 250 m the sphere departs from the frame's plane by 5 mm, so ship-local work stays flat.</summary>
public readonly record struct ShipFrame(Vec3d Origin, Vec3d Forward, Vec3d Starboard, Vec3d Up)
{
    /// <summary>The frame of a ship at a position, heading along a tangent vector. Starboard is forward × up: heading
    /// north, it's east.</summary>
    public static ShipFrame At(Vec3d position, Vec3d forward)
    {
        var up = position.Normalized();
        var f = (forward - up * Vec3d.Dot(forward, up)).Normalized();
        return new ShipFrame(position, f, Vec3d.Cross(f, up), up);
    }

    /// <summary>The world point at ship-local (x to the bow, y to starboard, h up).</summary>
    public Vec3d ToWorld(double x, double y, double h) => Origin + Forward * x + Starboard * y + Up * h;

    /// <summary>The ship-local (x, y, h) of a world point.</summary>
    public (double X, double Y, double H) ToLocal(Vec3d world)
    {
        var d = world - Origin;
        return (Vec3d.Dot(d, Forward), Vec3d.Dot(d, Starboard), Vec3d.Dot(d, Up));
    }
}
