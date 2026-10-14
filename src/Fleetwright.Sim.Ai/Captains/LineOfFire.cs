using Fleetwright.Sim.Combat;
using Fleetwright.Sim.Core;

namespace Fleetwright.Sim.Ai.Captains;

/// <summary>Whether a line of fire is clear of a ship's own side (docs/battle/command.md 5.1a: the 5.0 bench found 7 of
/// the Jutland's 47 losses were own fire). Shells and torpedoes hit whatever is in their way (the hit query is physical),
/// so a captain asks before he fires: a gun's line is foul while a friend (his side, or a side at peace with it) is within
/// <see cref="SimSettings.FireFoulM"/> of where its shells come down (the line's last
/// <see cref="SimSettings.FireFoulBeforeM"/> short of the target, and as far past it); a torpedo's while one is within
/// <see cref="SimSettings.TorpedoFoulM"/> of its track, out to its run. The ships off rails go in a grid once a think (the
/// captains', at a moment); a segment is checked a cube at a time along it, so a question is local, never ships × ships.
/// Not world state: rebuilt when asked at a new moment. Allocates only to grow.</summary>
public sealed class LineOfFire
{
    readonly CubeIndex grid = new();
    long builtMs = long.MinValue;

    void Build(in AiContext ctx)
    {
        if (builtMs == ctx.NowMs)
            return;
        builtMs = ctx.NowMs;
        var store = ctx.Ships;
        grid.Clear(store.Slots, ctx.Settings.LineOfFireCellM);
        for (int k = 0; k < store.OffRailsCount; k++)
        {
            int slot = store.OffRailsSlot(k);
            grid.Add(slot, store[slot].Position);
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

    /// <summary>Whether a friend of <paramref name="own"/> is near a torpedo's track toward a point, out to its run.</summary>
    public bool TorpedoFoul(in AiContext ctx, ShipState own, Vec3d toward, double runM)
    {
        var s = ctx.Settings;
        var d = toward - own.Position;
        double len = d.Length;
        if (len < 1)
            return false;
        var dir = d * (1 / len);
        return Foul(ctx, own, own.Position + dir * s.FireFoulNearM, own.Position + dir * runM, s.TorpedoFoulM);
    }

    /// <summary>Whether a friend (not himself, not an enemy) is within <paramref name="width"/> of the segment a-b.</summary>
    bool Foul(in AiContext ctx, ShipState own, Vec3d a, Vec3d b, double width)
    {
        Build(ctx);
        var store = ctx.Ships;
        var relations = ctx.World.Relations;
        var ab = b - a;
        double len = ab.Length, cell = grid.Cell;
        var dir = len > 0 ? ab * (1 / len) : Vec3d.Zero;
        int steps = (int)(len / cell) + 1;
        for (int i = 0; i <= steps; i++)
        {
            var p = a + dir * Math.Min(len, i * cell);
            int n = grid.Near(p);
            foreach (int slot in grid.Found(n))
            {
                var x = store[slot];
                if (x.Entity == own.Entity || relations.Hostile(own.Side, x.Side))
                    continue;
                var q = x.Position - a;
                double along = Math.Clamp(Vec3d.Dot(q, dir), 0, len);
                if ((q - dir * along).LengthSquared <= width * width)
                    return true;
            }
        }
        return false;
    }
}
