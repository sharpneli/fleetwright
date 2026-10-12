namespace Fleetwright.Sim.Core;

/// <summary>World time: milliseconds since 1850-01-01 00:00 UTC. Exact, ordered and without drift over a century of
/// game; ticks are whole milliseconds. It's a uniform clock: no leap seconds, and it stands for UT (the Earth's
/// rotation) too, since the seconds between them (delta T) change nothing a player sees. Converted to seconds as a
/// double only inside models (docs/world/architecture.md, "Time, in numbers").</summary>
public readonly record struct WorldTime(long Ms) : IComparable<WorldTime>
{
    public static readonly DateTime Epoch = new(1850, 1, 1, 0, 0, 0, DateTimeKind.Utc);

    public const long MsPerSecond = 1000;
    public const long MsPerDay = 86_400_000;

    /// <summary>J2000.0, 2000-01-01 12:00 UTC, the astronomers' epoch: 54,786.5 days after ours.</summary>
    public const long J2000Ms = 4_733_553_600_000;

    public static WorldTime FromUtc(DateTime utc)
    {
        if (utc.Kind == DateTimeKind.Local)
            throw new ArgumentException("world time is UTC; pass a UTC or unspecified DateTime");
        return new WorldTime(Math.DivRem(utc.Ticks - Epoch.Ticks, TimeSpan.TicksPerMillisecond, out long rem)
            - (rem < 0 ? 1 : 0));
    }

    public static WorldTime FromUtc(int year, int month, int day, int hour = 0, int minute = 0, int second = 0) =>
        FromUtc(new DateTime(year, month, day, hour, minute, second, DateTimeKind.Utc));

    public DateTime ToUtc() => Epoch.AddTicks(Ms * TimeSpan.TicksPerMillisecond);

    /// <summary>Days since J2000.0, the variable of the solar formulas.</summary>
    public double DaysSinceJ2000 => (Ms - J2000Ms) / (double)MsPerDay;

    public double JulianDate => 2451545.0 + DaysSinceJ2000;

    public static WorldTime operator +(WorldTime t, long ms) => new(t.Ms + ms);
    public static WorldTime operator -(WorldTime t, long ms) => new(t.Ms - ms);
    public static long operator -(WorldTime a, WorldTime b) => a.Ms - b.Ms;
    public static bool operator <(WorldTime a, WorldTime b) => a.Ms < b.Ms;
    public static bool operator >(WorldTime a, WorldTime b) => a.Ms > b.Ms;
    public static bool operator <=(WorldTime a, WorldTime b) => a.Ms <= b.Ms;
    public static bool operator >=(WorldTime a, WorldTime b) => a.Ms >= b.Ms;

    public int CompareTo(WorldTime other) => Ms.CompareTo(other.Ms);

    public override string ToString() => ToUtc().ToString("yyyy-MM-dd HH:mm:ss.fff");
}
