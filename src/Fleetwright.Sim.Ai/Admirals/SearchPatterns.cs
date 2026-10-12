using Fleetwright.Sim.Core;

namespace Fleetwright.Sim.Ai.Admirals;

/// <summary>The kinds of search a group can steam (<see cref="SearchPatterns"/>).</summary>
public enum SearchPattern : byte
{
    /// <summary>Parallel legs along an axis, stepping across by the track spacing between them (a creeping line).</summary>
    Ladder,

    /// <summary>Out from a datum in a square spiral, the legs growing by the track spacing every second turn.</summary>
    ExpandingSquare,

    /// <summary>Triangles through a datum, legs of one length, so its radial legs fan out every 60°.</summary>
    Sector,

    /// <summary>Out along a line and back, round and round until ordered otherwise.</summary>
    PatrolLine,
}

/// <summary>Search patterns as routes (docs/battle/plan.md step 2.5): the waypoints a guide steams to search an area,
/// from where it starts. Pure geometry: an admiral or the player's map turns them into a unit's route orders
/// (<see cref="Orders"/>). Legs are laid as rhumb lines, courses held, so a pattern keeps its shape on the chart; the
/// ship steams great circles between the points, which over a pattern's tens of miles is the same within metres. On
/// rails each turn is instantaneous; on the fine tick the helmsman turns at the wheel-over point.</summary>
public static class SearchPatterns
{
    /// <summary>The waypoints of a pattern from a start (the datum, or the commence-search point) into
    /// <paramref name="into"/>: how many. The axis is the first leg's course, degrees; the leg is a ladder's or patrol
    /// line's length, or a sector's radius, metres; the spacing is between tracks, metres; legs is a ladder's or a
    /// square's count of legs, or a sector's count of triangles; turns go to starboard or to port. A patrol line's
    /// route loops (<see cref="Loops"/>).</summary>
    public static int Waypoints(SearchPattern pattern, Vec3d start, double axisDeg, double legM, double spacingM, int legs, bool starboard, Span<Vec3d> into)
    {
        double turn = starboard ? 90 : -90;
        int n = 0;
        var p = start;
        switch (pattern)
        {
            case SearchPattern.Ladder:
                for (int k = 0; k < legs && n < into.Length; k++)
                {
                    p = Along(p, axisDeg + (k % 2 == 0 ? 0 : 180), legM);
                    into[n++] = p;
                    if (k + 1 < legs && n < into.Length)
                        into[n++] = p = Along(p, axisDeg + turn, spacingM);
                }
                break;
            case SearchPattern.ExpandingSquare:
                for (int k = 0; k < legs && n < into.Length; k++)
                    into[n++] = p = Along(p, axisDeg + k * turn, (k / 2 + 1) * spacingM);
                break;
            case SearchPattern.Sector:
                for (int k = 0; k < legs && n + 2 < into.Length; k++)
                {
                    double out1 = axisDeg + k * 360.0 / legs;
                    var a = Along(start, out1, legM);
                    into[n++] = a;
                    into[n++] = Along(a, out1 + (starboard ? 120 : -120), legM);
                    into[n++] = start;
                }
                break;
            case SearchPattern.PatrolLine:
                if (into.Length >= 2)
                {
                    into[n++] = Along(start, axisDeg, legM);
                    into[n++] = start;
                }
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(pattern));
        }
        return n;
    }

    /// <summary>Whether a pattern's route goes round again (a patrol line does; the searches end where they end, and the
    /// guide holds the course it arrived on).</summary>
    public static bool Loops(SearchPattern pattern) => pattern == SearchPattern.PatrolLine;

    /// <summary>The orders that put a unit (a ship, or a group: its guide steams it and the rest keep station) on a
    /// pattern's route: the same route orders whoever searches.</summary>
    public static void Orders(UnitOrders orders, Unit unit, ReadOnlySpan<Vec3d> waypoints, bool loop, Action<Command> send,
        OrderSource source = OrderSource.Player)
    {
        send(orders.ClearRoute(unit, source));
        foreach (var w in waypoints)
        {
            var (lat, lon) = Earth.LatLon(w);
            send(orders.AddWaypoint(unit, lat, lon, source));
        }
        if (loop)
            send(orders.LoopRoute(unit, true, source));
    }

    static Vec3d Along(Vec3d from, double courseDeg, double m)
    {
        Earth.Rhumb(from, ((courseDeg % 360) + 360) % 360, m, out var p, out _);
        return p;
    }
}
