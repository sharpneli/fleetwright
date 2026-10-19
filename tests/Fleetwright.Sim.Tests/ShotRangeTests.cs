using Fleetwright.Sim.Combat;
using Fleetwright.Sim.Core;
using Fleetwright.Sim.Ships;
using Xunit.Abstractions;

namespace Fleetwright.Sim.Tests;

/// <summary>The terminal walk's trace and the hitbox viewer's shot (<see cref="TerminalTrace"/>, <see cref="ShotRange"/>):
/// listening never changes a hit, and the trace tells the walk the hit took.</summary>
public class ShotRangeTests(ITestOutputHelper output)
{
    static readonly SimSettings Settings = SimSettings.Load(Path.Combine(Fleet.Repo, "Content", "Sim", "settings.json"));

    static ShipClass Class(string name) => Fleet.Get(name).Class!;

    static (ShellTypeRow Shell, double MaxElev) MainShell(ShipClass c) =>
        (c.Loadout.ShellTypes[c.Loadout.Fires(0)], c.Mounts[c.Links(c.Batteries[0].Mounts)[0]].MaxElevationDeg);

    /// <summary>The same hits on a ship of the class with and without a trace: the same outcomes, the same world.</summary>
    [Fact]
    public void A_trace_never_changes_a_hit()
    {
        var target = Class("bismarck");
        var (shell, elev) = MainShell(Class("lion"));
        (TerminalResult[], ulong) Run(bool traced)
        {
            var w = new World(WorldTime.FromUtc(1916, 5, 31, 14), 7, Settings);
            if (traced)
                w.Damage.Trace = new TerminalTrace();
            w.Apply(ShipSystem.Spawn(w.Ships.AddClass(target), 56, 4, 0, 15));
            int entity = w.Ships.NewestEntity;
            var results = new List<TerminalResult>();
            foreach (double aspect in (ReadOnlySpan<double>)[30, 90, 150])
            {
                var (dir, fall) = GivenHits.Flight(shell, elev, 16_000, aspect);
                for (double x = -0.4; x <= 0.4; x += 0.1)
                    foreach (double z in (ReadOnlySpan<double>)[-2, 0, 2])
                    {
                        // just outside her side, on the line of fire
                        var p = new P3(x * target.Length - dir.X * 20, -dir.Y * 20, target.Vertical.Waterline + z - dir.Z * 20);
                        w.Damage.Strike(w, new Hit
                        {
                            Ship = entity, Ms = w.Time.Ms, Point = p, Direction = dir, SpeedMs = fall.StrikeMs, Source = HitSource.Shell, Shell = shell, ShellType = -1,
                        });
                        results.Add(w.Damage.LastTerminal);
                    }
            }
            return (results.ToArray(), w.ComputeHash());
        }
        var (plain, plainHash) = Run(false);
        var (traced, tracedHash) = Run(true);
        Assert.Equal(plain, traced);
        Assert.Equal(plainHash, tracedHash);
    }

    /// <summary>A heavy shell at the belt: the trace's plates are the path's armour in order, each at a speed no more
    /// than the one before left it; the walk ends where the outcome says.</summary>
    [Fact]
    public void The_trace_tells_the_walk_the_hit_took()
    {
        var target = Class("bismarck");
        var (shell, elev) = MainShell(Class("lion"));
        var range = new ShotRange(Settings);
        int stopped = 0, through = 0;
        for (ulong seed = 1; seed <= 40; seed++)
        {
            var s = range.Fire(target, shell, "lion main", elev, 14_000, 90, new P3(0, 0, target.Vertical.Waterline + 0.5), seed);
            Assert.True(s.Struck, s.Note);
            Assert.NotEmpty(s.Steps);
            Assert.Equal(s.Terminal.StrikeMs, s.StrikeMs);
            Assert.Equal(s.StrikeMs, s.Steps[0].InMs);
            double v = s.StrikeMs, t = double.NegativeInfinity;
            foreach (var st in s.Steps)
            {
                var x = s.Path.Crossings[st.Crossing];
                Assert.NotEqual(PlateKind.None, x.Plate);
                Assert.Equal(x.T, st.T);
                Assert.True(st.T >= t);
                Assert.True(st.InMs <= v + 1e-9);
                Assert.True(st.OutMs <= st.InMs + 1e-9);
                (v, t) = (st.OutMs, st.T);
            }
            var last = s.Steps[^1];
            if (s.Terminal.Outcome == Outcome.Defeated)
            {
                stopped++;
                Assert.Equal(PlateCall.Stopped, last.Call);
                Assert.Equal(last.T, s.Terminal.StopT);
            }
            if (s.Terminal.Outcome is Outcome.BurstInside or Outcome.LowOrder)
            {
                through++;
                Assert.NotEqual(FuzeCause.None, s.Fuze);
                Assert.True(s.Terminal.BurstT >= s.FuzeT);
            }
            if (seed == 1)
                output.WriteLine(string.Join("\n", s.Steps.Select(st =>
                    $"{st.T:0.00} m {HitPath.PlateName(target, s.Path.Crossings[st.Crossing])} {st.Mm:0} mm at {st.ObliquityDeg:0}°: {st.InMs:0} -> {st.OutMs:0} (V_L {st.LimitMs:0}) {st.Call}")) +
                    $"\n{s.Terminal.Outcome}, parts worse {s.Parts.Count}, holes {s.Holes.Count}");
        }
        output.WriteLine($"stopped {stopped}, burst inside {through} of 40");
        Assert.True(stopped + through > 0);
    }

    /// <summary>C0 (docs/battle/shell-contents.md): the fuze runs on time. Past the plate that started it, each plate's
    /// delay left is the one before less the time to get there at the speed it left with; a burst after its delay is
    /// where the last of it runs out. So a plate that slows the shell brings its burst in.</summary>
    [Fact]
    public void The_fuze_runs_on_time()
    {
        Assert.Equal(16, Terminal.Slowed(20, 10, 500, 300), 9);   // 0.02 s left: 10 m at 500, 6 m at 300
        Assert.Equal(10, Terminal.Slowed(20, 10, 500, 0), 9);
        Assert.True(double.IsNaN(Terminal.Slowed(double.NaN, 10, 500, 300)));

        var target = Class("bismarck");
        var (shell, elev) = MainShell(Class("lion"));
        var range = new ShotRange(Settings);
        int checkedSteps = 0, bursts = 0;
        for (ulong seed = 1; seed <= 20; seed++)
            foreach (double z in (ReadOnlySpan<double>)[-1, 1.5, 4])
            {
                var s = range.Fire(target, shell, "lion main", elev, 18_000, 70, new P3(0, 0, target.Vertical.Waterline + z), seed);
                if (!s.Struck || s.Fuze is FuzeCause.None or FuzeCause.Nose)
                    continue;
                PlateStep? prev = null;
                foreach (var st in s.Steps)
                {
                    if (double.IsNaN(st.FuzeLeftS))
                        continue;
                    if (prev is { } p && st.Call is PlateCall.Through or PlateCall.Glanced)
                    {
                        Assert.Equal(p.FuzeLeftS - (st.T - p.T) / p.OutMs, st.FuzeLeftS, 6);
                        checkedSteps++;
                    }
                    prev = st;
                }
                if (prev is { } last && s.Terminal.Outcome is Outcome.BurstInside or Outcome.LowOrder && s.Terminal.BurstT > last.T + 0.5)
                {
                    Assert.Equal(last.T + last.FuzeLeftS * last.OutMs, s.Terminal.BurstT, 6);
                    bursts++;
                }
            }
        output.WriteLine($"plates checked {checkedSteps}, bursts {bursts}");
        Assert.True(checkedSteps > 0 && bursts > 0);
    }

    /// <summary>A face-hardened plate met from behind: Bismarck's 38 cm through Dreadnought's belt at 1 km reaches her far
    /// side's armour with its fuze still running, and meets its soft back first. It goes through as a plate of its q, and
    /// the face doesn't shatter it (it did: the far plate took the near one's face rules).</summary>
    [Fact]
    public void Armour_met_from_behind_doesnt_shatter_the_shell()
    {
        Assert.True(Terminal.FromBehind(new P3(0, -1, 0), new P3(0, -1, 0)));
        Assert.False(Terminal.FromBehind(new P3(0, -1, 0), new P3(0, 1, 0)));
        Assert.False(Terminal.FromBehind(new P3(0, -1, 0), new P3(double.NaN, double.NaN, double.NaN)));

        var target = Class("dreadnought");
        var (shell, elev) = MainShell(Class("bismarck"));
        var range = new ShotRange(Settings);
        int far = 0, through = 0;
        for (ulong seed = 1; seed <= 20; seed++)
            foreach (double z in (ReadOnlySpan<double>)[0.5, 2, 3.5])
            {
                var s = range.Fire(target, shell, "bismarck main", elev, 1000, 90, new P3(0, 0, target.Vertical.Waterline + z), seed);
                if (!s.Struck || s.Steps.Length == 0)
                    continue;
                double nearY = s.Steps[0].Point.Y;
                foreach (var st in s.Steps)
                    if (st.Mm >= 150 && st.Point.Y * nearY < 0)
                    {
                        far++;
                        Assert.NotEqual(PlateCall.Shattered, st.Call);
                        through += st.Call == PlateCall.Through ? 1 : 0;
                    }
            }
        output.WriteLine($"far side's armour met {far} times, through whole {through}");
        Assert.True(far > 0 && through > 0);
    }
}
