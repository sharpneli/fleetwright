using Fleetwright.Sim.Combat;
using Fleetwright.Sim.Core;

namespace Fleetwright.Sim.Ai.Captains;

/// <summary>Whether a line of fire is clear of a ship's own side (docs/battle/command.md 5.1a: the 5.0 bench found 7 of
/// the Jutland's 47 losses were own fire). Shells and torpedoes hit whatever is in their way (the hit query is physical),
/// so a captain asks before he fires: a gun's line is foul while a friend (his side, or a side at peace with it) is within
/// <see cref="SimSettings.FireFoulM"/> of where its shells come down (the line's last
/// <see cref="SimSettings.FireFoulBeforeM"/> short of the target, and as far past it); a torpedo's while one will come within
/// <see cref="SimSettings.TorpedoFoulM"/> of it on its run, each going on as she is (a slow weapon: a flotilla turned
/// together sees its tracks bend astern of the line, clear of the boats ahead). The ships off rails go in a grid once a
/// think (the captains', at a moment); a segment is checked a cube at a time along it, so a question is local, never
/// ships × ships. A grid a side, so a question only meets the ships it asks about, never the enemy round the fall. Not
/// world state: rebuilt when asked at a new moment. Allocates only to grow.</summary>
public sealed class LineOfFire
{
    readonly CubeIndex?[] grids = new CubeIndex?[256];   // by side
    readonly List<byte> sides = [];   // the sides with ships off rails at this moment
    long builtMs = long.MinValue;
    double cell;

    void Build(in AiContext ctx)
    {
        if (builtMs == ctx.NowMs)
            return;
        builtMs = ctx.NowMs;
        var store = ctx.Ships;
        cell = ctx.Settings.LineOfFireCellM;
        sides.Clear();
        for (int k = 0; k < store.OffRailsCount; k++)
        {
            int slot = store.OffRailsSlot(k);
            var x = store[slot];
            var grid = grids[x.Side] ??= new CubeIndex();
            if (!sides.Contains(x.Side))
            {
                sides.Add(x.Side);
                grid.Clear(store.Slots, cell);
            }
            grid.Add(slot, x.Position);
        }
    }

    /// <summary>Whether a friend of <paramref name="own"/> is near the fall of his shells at <paramref name="target"/>.</summary>
    public bool GunsFoul(in AiContext ctx, ShipState own, Vec3d target)
    {
        var s = ctx.Settings;
        var d = target - own.Position;
        double range = d.Length;
        if (range < 1)
            return false;
        var dir = d * (1 / range);
        double from = Math.Max(s.FireFoulNearM, range - s.FireFoulBeforeM);
        return Foul(ctx, own, own.Position + dir * from, own.Position + dir * (range + s.FireFoulM), s.FireFoulM);
    }

    /// <summary>Whether a friend of <paramref name="own"/> will come near a torpedo run toward a point at a speed (m/s), out
    /// to its run: her closest approach to it past the first <see cref="SimSettings.FireFoulNearM"/>, each going on as
    /// she is now, within <see cref="SimSettings.TorpedoFoulM"/> and as far again as she could have turned off her course
    /// by <see cref="SimSettings.TorpedoFoulTurnDeg"/> by then. Friends are looked for within a cube of the track, so one
    /// that would steam in from farther isn't seen.</summary>
    public bool TorpedoFoul(in AiContext ctx, ShipState own, Vec3d toward, double runM, double speedMs)
    {
        var s = ctx.Settings;
        var d = toward - own.Position;
        double len = d.Length;
        if (len < 1 || speedMs <= 0)
            return false;
        var dir = d * (1 / len);
        Build(ctx);
        var store = ctx.Ships;
        var relations = ctx.World.Relations;
        var torpedo = dir * speedMs;
        double t0 = s.FireFoulNearM / speedMs, t1 = runM / speedMs, turn = Math.Sin(s.TorpedoFoulTurnDeg * Math.PI / 180);
        int steps = (int)(runM / cell) + 1;
        foreach (byte side in sides)
        {
            if (relations.Hostile(own.Side, side))
                continue;
            var grid = grids[side]!;
            for (int i = 0; i <= steps; i++)
            {
                int n = grid.NearAnyOrder(own.Position + dir * Math.Min(runM, i * cell));
                foreach (int slot in grid.Found(n))
                {
                    var x = store[slot];
                    if (x.Entity == own.Entity)
                        continue;
                    // where she is from the torpedo's start, and how she moves from it; her margin grows as far as she could
                    // turn off her course by then. Her distance less the margin is convex in t: its least by golden section
                    var q = x.Position - own.Position;
                    var u = x.Forward * x.Speed - torpedo;
                    double drift = turn * x.Speed;
                    if (Clearance(q, u, drift, s.TorpedoFoulM, t0, t1) <= 0)
                        return true;
                }
            }
        }
        return false;
    }

    /// <summary>The least over t0..t1 of |q + u·t| − (width + drift·t): how far a friend keeps clear of a torpedo, past her
    /// margin (≤ 0: foul).</summary>
    static double Clearance(Vec3d q, Vec3d u, double drift, double width, double t0, double t1)
    {
        double F(double t) => (q + u * t).Length - width - drift * t;
        const double Phi = 0.6180339887498949;
        double a = t0, b = t1, c = b - Phi * (b - a), d = a + Phi * (b - a), fc = F(c), fd = F(d);
        for (int i = 0; i < 24 && b - a > 0.05; i++)
        {
            if (fc <= fd)
            {
                (b, d, fd) = (d, c, fc);
                c = b - Phi * (b - a);
                fc = F(c);
            }
            else
            {
                (a, c, fc) = (c, d, fd);
                d = a + Phi * (b - a);
                fd = F(d);
            }
        }
        return Math.Min(Math.Min(fc, fd), Math.Min(F(t0), F(t1)));
    }

    /// <summary>Whether a friend of <paramref name="own"/> (not himself, nor one of his own squadron, whom he sees in station)
    /// is within a radius of a point (docs/battle/night.md Q6: a shape there might be one of his), at most the grid's
    /// cell.</summary>
    public bool FriendNear(in AiContext ctx, GroupSystem groups, ShipState own, Vec3d p, double radiusM)
    {
        if (radiusM <= 0)
            return false;
        Build(ctx);
        var store = ctx.Ships;
        var relations = ctx.World.Relations;
        var unit = groups.Of(own.Entity);
        double r2 = radiusM * radiusM;
        foreach (byte side in sides)
        {
            if (relations.Hostile(own.Side, side))
                continue;
            var grid = grids[side]!;
            int n = grid.NearAnyOrder(p);
            foreach (int slot in grid.Found(n))
            {
                var x = store[slot];
                if (x.Entity != own.Entity && (unit == null || groups.Of(x.Entity) != unit) && (x.Position - p).LengthSquared <= r2)
                    return true;
            }
        }
        return false;
    }

    /// <summary>Whether a friend (not himself, not an enemy) is within <paramref name="width"/> of the segment a-b.</summary>
    bool Foul(in AiContext ctx, ShipState own, Vec3d a, Vec3d b, double width)
    {
        Build(ctx);
        var store = ctx.Ships;
        var relations = ctx.World.Relations;
        var ab = b - a;
        double len = ab.Length;
        var dir = len > 0 ? ab * (1 / len) : Vec3d.Zero;
        // the cubes round points a cube apart along it; a segment that fits in one cube's reach of its middle (a gun's
        // fall) needs only the middle's, which holds every ship the points' would
        bool one = len / 2 + width <= cell;
        int steps = one ? 0 : (int)(len / cell) + 1;
        foreach (byte side in sides)
        {
            if (relations.Hostile(own.Side, side))
                continue;
            var grid = grids[side]!;
            for (int i = 0; i <= steps; i++)
            {
                var p = one ? a + dir * (len / 2) : a + dir * Math.Min(len, i * cell);
                int n = grid.NearAnyOrder(p);
                foreach (int slot in grid.Found(n))
                {
                    var x = store[slot];
                    if (x.Entity == own.Entity)
                        continue;
                    var q = x.Position - a;
                    double along = Math.Clamp(Vec3d.Dot(q, dir), 0, len);
                    if ((q - dir * along).LengthSquared <= width * width)
                        return true;
                }
            }
        }
        return false;
    }
}
