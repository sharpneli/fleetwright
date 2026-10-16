using Fleetwright.Shipgen;
using Fleetwright.Sim.Core;
using Fleetwright.Sim.Ships;

namespace Fleetwright.Sim.Combat;

/// <summary>A ship's attitude in the water: how far it has settled (m), its trim (degrees, bow down positive) and heel
/// (degrees, to starboard positive), about the centre of flotation at the waterline. The flooding model gives them
/// (docs/battle/fighting.md 4.3b); upright until then.</summary>
public readonly record struct Attitude(double SinkageM, double TrimDeg, double HeelDeg)
{
    public static readonly Attitude Upright = default;

    /// <summary>A lost ship's attitude at a moment (ms) as it goes under, from the attitude it settled to when lost
    /// (<paramref name="lossMs"/>) to its top under the sea when it's gone (<paramref name="goneMs"/>): foundered, down
    /// by the end it was trimmed to (the stern, when level); capsized, over on the side it listed to; a magazine,
    /// straight down, quickly. Afloat (<see cref="LossKind.None"/>), the settled attitude as it is. The one model the
    /// hit queries and the screens both read.</summary>
    public static Attitude At(ShipClass c, in Attitude settled, LossKind loss, long lossMs, long goneMs, long ms)
    {
        if (loss == LossKind.None)
            return settled;
        double p = Math.Clamp((ms - lossMs) / (double)Math.Max(goneMs - lossMs, 1), 0, 1);
        double top = c.Solids.Bounds.Top - c.Vertical.Waterline + 3, ease = Math.Pow(p, 1.5);
        switch (loss)
        {
            case LossKind.Foundered:
                const double Trim = 0.3;   // the slope it ends at
                double along = settled.TrimDeg > 0 ? 1 : -1;
                return new Attitude(settled.SinkageM + ease * (top + Trim * c.Length / 2),
                    settled.TrimDeg + along * double.RadiansToDegrees(Math.Atan(Trim * p)), settled.HeelDeg);
            case LossKind.Capsized:
                const double Heel = 0.9;
                double side = settled.HeelDeg >= 0 ? 1 : -1;
                return new Attitude(settled.SinkageM + ease * (top + Heel * c.Beam / 2), settled.TrimDeg,
                    settled.HeelDeg + side * double.RadiansToDegrees(Math.Atan(Heel * p)));
            default:
                return settled with { SinkageM = settled.SinkageM + Math.Pow(p, 0.7) * top };
        }
    }
}

/// <summary>Where a shell that didn't meet a ship met the sea, ship-local (Water, on the waterplane), and how far it
/// fell from the hull's waterline: Along the shell's track (negative short of it, positive over it, 0 when the track
/// passes over the hull there) and Beside it (positive to the track's right, 0 when within the hull's width).</summary>
public readonly record struct Fall(P3 Water, double Along, double Beside)
{
    public bool Straddles => Along == 0 && Beside == 0;
}

/// <summary>The hit query (docs/battle/fighting.md 4.1): a segment in Earth-centred space, a shell's last metres, through
/// one ship. It takes the segment into the ship's frame (its pose, then its sinkage, trim and heel), asks the broadphase
/// whether it comes near, and has the one tracer (<see cref="ShipTracer"/>) walk it, turrets at their bearings. What the
/// segment crossed is the <see cref="HitPath"/>; a miss's fall comes from it too. Allocates nothing.</summary>
public static class HitQuery
{
    /// <summary>The world segment w0 to w1 through the ship: false (and an empty path) when it doesn't come within the
    /// ship's radius. bearingsDeg: each mount's bearing, ship-relative (missing or NaN: at rest).</summary>
    public static bool Trace(ShipClass c, in ShipFrame frame, in Attitude attitude, Vec3d w0, Vec3d w1, ReadOnlySpan<double> bearingsDeg,
        HitPath path)
    {
        var p0 = ToShip(c, frame, attitude, w0);
        var p1 = ToShip(c, frame, attitude, w1);
        if (!Near(c, p0, p1))
        {
            path.Clear(p0, p1);
            return false;
        }
        ShipTracer.Trace(c, p0, p1, bearingsDeg, WaterOf(c, attitude), path);
        return true;
    }

    /// <summary>Broadphase: does the ship-local segment come within the ship's radius of its waterline amidships?</summary>
    public static bool Near(ShipClass c, P3 p0, P3 p1)
    {
        double wl = c.Vertical.Waterline, r = c.Solids.Radius;
        double dx = p1.X - p0.X, dy = p1.Y - p0.Y, dz = p1.Z - p0.Z, l2 = dx * dx + dy * dy + dz * dz;
        double u = l2 > 0 ? Math.Clamp(-(p0.X * dx + p0.Y * dy + (p0.Z - wl) * dz) / l2, 0, 1) : 0;
        double ex = p0.X + u * dx, ey = p0.Y + u * dy, ez = p0.Z - wl + u * dz;
        return ex * ex + ey * ey + ez * ez <= r * r;
    }

    // ------------------------------------------------------------------ the frame

    /// <summary>A world point in the ship's own coordinates (the hitboxes': +x bow, +y starboard, heights over the main
    /// deck), its attitude taken out: the frame's sea-level point, turned back about the centre of flotation.</summary>
    public static P3 ToShip(ShipClass c, in ShipFrame frame, in Attitude a, Vec3d world)
    {
        var (x, y, h) = frame.ToLocal(world);
        double wl = c.Vertical.Waterline, lcf = c.Hydrostatics.Lcf;
        if (a == Attitude.Upright)
            return new P3(x, y, h + wl);
        Trig(a, out double ct, out double st, out double cp, out double sp);
        // v = R(q - P) + (Lcf, 0, -sinkage), R = heel after trim, P = (Lcf, 0, Waterline): q = R^T (v - (Lcf, 0, -sinkage)) + P
        double ux = x - lcf, uy = y, uz = h + a.SinkageM;
        double wy = uy * cp - uz * sp, wz = uy * sp + uz * cp;   // heel undone
        double dx = ux * ct - wz * st, dz = ux * st + wz * ct;   // trim undone
        return new P3(dx + lcf, wy, dz + wl);
    }

    /// <summary>A ship-local point in the world: ToShip's inverse.</summary>
    public static Vec3d ToWorld(ShipClass c, in ShipFrame frame, in Attitude a, P3 p)
    {
        double wl = c.Vertical.Waterline, lcf = c.Hydrostatics.Lcf;
        if (a == Attitude.Upright)
            return frame.ToWorld(p.X, p.Y, p.Z - wl);
        Trig(a, out double ct, out double st, out double cp, out double sp);
        double dx = p.X - lcf, dy = p.Y, dz = p.Z - wl;
        double tx = dx * ct + dz * st, tz = -dx * st + dz * ct;   // trim: the bow down
        double hy = dy * cp + tz * sp, hz = -dy * sp + tz * cp;   // heel: the starboard side down
        return frame.ToWorld(tx + lcf, hy, hz - a.SinkageM);
    }

    /// <summary>The sea's surface in the ship's frame at an attitude: higher on the hull by the sinkage, toward the bow
    /// when trimmed by it, and to the side it heels to.</summary>
    public static Waterplane WaterOf(ShipClass c, in Attitude a)
    {
        if (a == Attitude.Upright)
            return Waterplane.Level(c);
        var (z0, dzdx, dzdy) = Flotation.Sea(c.Vertical.Waterline, c.Hydrostatics.Lcf, a.SinkageM, double.DegreesToRadians(a.TrimDeg),
            double.DegreesToRadians(a.HeelDeg));
        return new Waterplane(z0, dzdx, dzdy);
    }

    static void Trig(in Attitude a, out double ct, out double st, out double cp, out double sp)
    {
        double t = double.DegreesToRadians(a.TrimDeg), p = double.DegreesToRadians(a.HeelDeg);
        (ct, st, cp, sp) = (Math.Cos(t), Math.Sin(t), Math.Cos(p), Math.Sin(p));
    }

    // ------------------------------------------------------------------ a miss

    /// <summary>Where the path's segment met the sea (its first entry; else where its line meets the waterplane) and how
    /// far that is from the hull's waterline along the shell's track and beside it. The waterline is the level one,
    /// from the class (v0: an attitude doesn't reshape it).</summary>
    public static Fall FallOf(ShipClass c, HitPath path, in Waterplane water)
    {
        P3 s = path.Start, e = path.End, w = default;
        bool found = false;
        foreach (ref readonly var x in path.Crossings)
            if (x.Kind == CrossingKind.WaterIn)
            {
                (w, found) = (x.Point, true);
                break;
            }
        if (!found)
        {
            double f0 = s.Z - water.At(s.X, s.Y), f1 = (e.Z - s.Z) - water.DzDx * (e.X - s.X) - water.DzDy * (e.Y - s.Y);
            double u = Math.Abs(f1) > 1e-12 ? -f0 / f1 : 1;
            w = new P3(s.X + (e.X - s.X) * u, s.Y + (e.Y - s.Y) * u, s.Z + (e.Z - s.Z) * u);
        }
        // the track's direction on the water, and its right
        double tx = e.X - s.X, ty = e.Y - s.Y, tl = Math.Sqrt(tx * tx + ty * ty);
        (tx, ty) = tl > 0 ? (tx / tl, ty / tl) : (1, 0);
        double rx = -ty, ry = tx;   // +y is starboard: the right of a track along +x is +y
        double a0 = double.PositiveInfinity, a1 = double.NegativeInfinity, b0 = double.PositiveInfinity, b1 = double.NegativeInfinity;
        foreach (var p in c.Solids.Waterline)
            foreach (double y in (ReadOnlySpan<double>)[p.Y, -p.Y])
            {
                double along = p.X * tx + y * ty, beside = p.X * rx + y * ry;
                (a0, a1, b0, b1) = (Math.Min(a0, along), Math.Max(a1, along), Math.Min(b0, beside), Math.Max(b1, beside));
            }
        double wa = w.X * tx + w.Y * ty, wb = w.X * rx + w.Y * ry;
        return new Fall(w, wa < a0 ? wa - a0 : wa > a1 ? wa - a1 : 0, wb < b0 ? wb - b0 : wb > b1 ? wb - b1 : 0);
    }
}
