using Fleetwright.Sim.Core;
using Fleetwright.Sim.Ships;
using Old = Fleetwright.Shipgen.Tools.ShellTest;

namespace Fleetwright.Sim.Tests;

/// <summary>shell-test's port to ShipClass against the first version on the hitboxes (battle plan step 2.1): the same
/// shots, drawn up front, give the same outcome, cause, hole and entry, shot by shot, on every design.</summary>
public class ShellTestPortTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Port_agrees_with_the_old_tracer_shot_by_shot(bool gratings)
    {
        const int Shots = 1500;
        var problems = new List<string>();
        int tested = 0, leaks = 0;
        foreach (var b in Fleet.Designs)
        {
            var port = new ShellTest(b.Class!, gratings);
            var old = new Old.Tracer(b.Ship!.Hitboxes, gratings);
            string name = b.Case.Name;
            if (port.Roof != old.Roof)
            {
                problems.Add($"{name}: roof {port.Roof} vs {old.Roof}");
                continue;
            }
            var cells = new List<string>();
            foreach (int i in port.CitadelCells)
                cells.Add(b.Class!.CellId(i));
            if (!cells.SequenceEqual(old.CitadelCells))
            {
                problems.Add($"{name}: citadel cells differ");
                continue;
            }
            if (cells.Count == 0)
                continue;
            tested++;
            var stream = RandomStream.FromSeed(11);
            for (int k = 0; k < Shots; k++)
            {
                var shot = port.Draw(stream.Derive((ulong)k));
                var (ux, uy, uz) = shot.Back();
                var o = port.Trace(shot, out var lk);
                int oo = old.Trace(shot.X, shot.Y, shot.Z, ux, uy, uz, out var cause, out var hole, out var holeFace, out var entry, out var entryFace);
                if ((int)o != oo)
                {
                    problems.Add($"{name} shot {k}: {o} vs {oo}");
                    continue;
                }
                if (o != ShellTest.Outcome.Leak)
                    continue;
                leaks++;
                string ours = $"{port.CauseText(lk)} | {R(lk.Hole.X)} {R(lk.Hole.Y)} {R(lk.Hole.Z)} {ShellTest.FaceName(lk.HoleFace)} | " +
                              (lk.EntryFace == ShellTest.Face.None ? "-" : $"{R(lk.Entry.X)} {R(lk.Entry.Y)} {R(lk.Entry.Z)}") + $" {ShellTest.FaceName(lk.EntryFace)}";
                string theirs = $"{cause} | {hole![0]} {hole[1]} {hole[2]} {holeFace} | " + (entry is null ? "-" : $"{entry[0]} {entry[1]} {entry[2]}") +
                                $" {entryFace}";
                if (ours != theirs)
                    problems.Add($"{name} shot {k}: {ours}  vs  {theirs}");
            }
        }
        Assert.True(problems.Count == 0, $"{problems.Count} disagreements:\n" + string.Join("\n", problems.Take(20)));
        Assert.True(tested >= 40, $"{tested} designs with a citadel");
        Assert.True(gratings || leaks > 100, $"{leaks} leaks: the comparison should see some");
    }

    static double R(double v) => Math.Round(v, 2);

    [Fact]
    public void A_run_is_its_shots_whatever_the_threads()
    {
        var c = Fleet.Get("nassau").Class!;
        var r = ShellTest.Run(c, 4000, 5);
        // the same shots one by one on this thread
        var st = new ShellTest(c);
        var stream = RandomStream.FromSeed(5);
        int armour = 0, water = 0, leak = 0;
        for (int k = 0; k < 4000; k++)
            switch (st.Trace(st.Draw(stream.Derive((ulong)k)), out _))
            {
                case ShellTest.Outcome.Armour: armour++; break;
                case ShellTest.Outcome.Water: water++; break;
                default: leak++; break;
            }
        Assert.Equal(armour, r.Bands.Sum(b => b.Armour));
        Assert.Equal(water, r.Bands.Sum(b => b.Water));
        Assert.Equal(leak, r.Leaks.Count);
        Assert.Equal(ShellTest.Text("nassau", r, 10), ShellTest.Text("nassau", ShellTest.Run(c, 4000, 5), 10));
    }
}
