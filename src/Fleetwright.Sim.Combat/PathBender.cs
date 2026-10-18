using Fleetwright.Sim.Ships;

namespace Fleetwright.Sim.Combat;

/// <summary>Bends a hit's path where a plate turned the shell or it glanced off one (docs/battle/shells-armour-damage.md
/// P5): the new line traced by the one tracer straight through the bend's point, from outside the ship to outside it,
/// with the ship as the first trace had it (its mounts' bearings and the sea), and its crossings past the point spliced
/// onto the path (<see cref="HitPath.Splice"/>). Traced whole, the line's own crossings say what the shell is in at the
/// bend. One per damage system (per thread); allocates nothing once its leg has grown to the ship's size.</summary>
public sealed class PathBender
{
    readonly HitPath leg = new();
    double[] bearings = [];
    int mounts;
    Waterplane water;

    /// <summary>The ship as the hit's first trace had it: each mount's bearing (the array is kept, not copied: the caller
    /// keeps it as it is until the hit's resolved) and the sea in the ship's frame.</summary>
    public void Set(double[] bearingsDeg, int count, in Waterplane sea) => (bearings, mounts, water) = (bearingsDeg, count, sea);

    /// <summary>Room for a bend through a class with this many solids, made when the class first comes into the world
    /// (a first glance in the middle of a fight would otherwise grow the leg then).</summary>
    public void Reserve(int solids) => leg.Reserve(solids);

    /// <summary>Bend the path at T, keeping its first <paramref name="keep"/> crossings, onto the unit direction d from the
    /// path's point there: false (the path as it was) when it has all its legs.</summary>
    public bool Bend(ShipClass c, HitPath path, int keep, double t, P3 d)
    {
        if (path.Legs >= HitPath.MaxLegs)
            return false;
        var p = path.At(t);
        double far = 2 * c.Solids.Radius + 10;
        var p0 = new P3(p.X - d.X * far, p.Y - d.Y * far, p.Z - d.Z * far);
        var p1 = new P3(p.X + d.X * far, p.Y + d.Y * far, p.Z + d.Z * far);
        ShipTracer.Trace(c, p0, p1, bearings.AsSpan(0, mounts), water, leg);
        return path.Splice(keep, t, leg, far);
    }
}
