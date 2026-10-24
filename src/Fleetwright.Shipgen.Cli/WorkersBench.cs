using System.Diagnostics;
using Fleetwright.Sim.Core;

namespace Fleetwright.Shipgen.Cli;

/// <summary><c>shipgen workers [--items 250] [--item-ns 160] [--gap-us 100] [--dispatches 20000]</c>
/// (docs/battle/workers.md W0): what a dispatch costs on the sim's workers. A job of <c>items</c> items, each about
/// <c>item-ns</c> of arithmetic (the ships' step is ~160 ns a ship), dispatched again and again, back to back and with
/// <c>gap-us</c> of serial work between (the rest of a tick), on 0, 1, 2, 4 and 8 workers and a few spin windows: the
/// dispatch's wall time, against the inline run's.</summary>
static class WorkersBench
{
    struct Spin(double[] slots, int perItem) : IJob
    {
        public readonly void Run(int from, int to, int worker)
        {
            for (int i = from; i < to; i++)
            {
                double x = slots[i];
                for (int k = 0; k < perItem; k++)
                    x = x * 0.999999 + 1e-7;
                slots[i] = x;
            }
        }
    }

    public static int Run(int items, double itemNs, double gapUs, int dispatches)
    {
        var slots = new double[items];
        // calibrate the inner loop to itemNs an item
        int perItem = 1;
        var job = new Spin(slots, perItem);
        for (; ; perItem *= 2)
        {
            job = new Spin(slots, perItem);
            long t = Stopwatch.GetTimestamp();
            for (int r = 0; r < 200; r++)
                job.Run(0, items, 0);
            double ns = Probes.Ms(Stopwatch.GetTimestamp() - t) * 1e6 / (200.0 * items);
            if (ns >= itemNs)
            {
                perItem = (int)Math.Max(1, perItem * itemNs / ns);
                break;
            }
        }
        job = new Spin(slots, perItem);
        Console.WriteLine($"{items} items of ~{itemNs:G} ns, chunks of 16, {dispatches} dispatches; the gap between them {gapUs:G} µs of serial work");
        Console.WriteLine($"  {"workers",7} {"spin µs",8} {"back to back µs",16} {"with the gap µs",16}");
        long gapTicks = (long)(gapUs * 1e-6 * Stopwatch.Frequency);
        foreach (int n in new[] { 0, 1, 2, 4, 8 })
            foreach (double spin in n == 0 ? new[] { 0.0 } : new[] { 0.0, 50, 200, 1000 })
            {
                using var w = new Workers(n, spin);
                for (int r = 0; r < 1000; r++)
                    w.For(job, items, 16);   // warm: the holder, the JIT
                long t = Stopwatch.GetTimestamp();
                for (int r = 0; r < dispatches; r++)
                    w.For(job, items, 16);
                double tight = Probes.Ms(Stopwatch.GetTimestamp() - t) * 1000 / dispatches;
                long inJob = 0;
                for (int r = 0; r < dispatches; r++)
                {
                    long g = Stopwatch.GetTimestamp() + gapTicks;
                    while (Stopwatch.GetTimestamp() < g)
                        Thread.SpinWait(10);
                    long t0 = Stopwatch.GetTimestamp();
                    w.For(job, items, 16);
                    inJob += Stopwatch.GetTimestamp() - t0;
                }
                Console.WriteLine($"  {n,7} {spin,8:G} {tight,16:F2} {Probes.Ms(inJob) * 1000 / dispatches,16:F2}");
            }
        return 0;
    }
}
