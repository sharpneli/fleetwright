using System.Collections.Concurrent;
using Fleetwright.Shipgen;
using Fleetwright.Sim;
using Fleetwright.Sim.Combat;
using Fleetwright.Sim.Core;
using Fleetwright.Sim.Ships;

namespace Fleetwright.Designer;

/// <summary>The designer's immune zone (docs/designer-ui/immune-zone.md IZ4), off the render thread: for the shown ship
/// and a calibre of her own main battery (her own shells at it), a zone a plate picked (each at its centre), her armour's
/// summary (belt, barbettes, deck), her vitals when asked, and the shells fired in anger at the range picked. Each zone
/// fills to <see cref="First"/> shells a cell and is shown, then refines to <see cref="Refined"/> where it matters. A
/// newer ask cancels the work in hand; what was done for the same ship and calibre is kept.</summary>
sealed class ImmuneWorker : IDisposable
{
    /// <summary>The shells a cell first, and where it matters after (decision 10; "fast first", the user).</summary>
    public const int First = 8, Refined = 32;

    /// <summary>The ships a range band is fired at, and the hits each.</summary>
    public const int AngerShips = 8, AngerHits = 10;

    /// <summary>What the tab asks for: the shown ship (and the design it was built from), the calibre, the plates picked,
    /// whether the vitals are looked at, and the range picked for the shells fired in anger (m).</summary>
    public sealed record Ask(Ship Ship, Design Design, double CalibreMm, PlatePick[] Plates, bool Vitals, double AngerM)
    {
        public bool Same(Ask o) => ReferenceEquals(Ship, o.Ship) && CalibreMm == o.CalibreMm && Vitals == o.Vitals && AngerM == o.AngerM &&
                                   Plates.AsSpan().SequenceEqual(o.Plates);
    }

    /// <summary>The results for one ship and calibre (replaced whole when either changes).</summary>
    public sealed class Results
    {
        public required Ship Ship { get; init; }
        public required double CalibreMm { get; init; }
        public ShipClass? Class;
        public ShellTypeRow Shell;
        public double MaxElevationDeg;
        public string? Error;
        public readonly ConcurrentDictionary<PlatePick, ImmuneZone> Plates = new();
        public volatile ImmuneZone? Armour, Vitals;
        public volatile AngerRun? Anger;
        /// <summary>The band whose ships are all fired (-1: none yet).</summary>
        public volatile int AngerDone = -1;
    }

    readonly object gate = new();
    readonly Thread thread;
    readonly SimSettings settings = SimSettings.Load(Path.Combine(AppContext.BaseDirectory, "Content", "Sim", "settings.json"));
    readonly int threads = Math.Clamp(Environment.ProcessorCount - 2, 1, 8);
    Ask? pending;
    CancellationTokenSource cancel = new();
    bool disposed;
    volatile Results? results;
    volatile bool busy;

    public ImmuneWorker()
    {
        thread = new Thread(Loop) { IsBackground = true, Name = "Immune zone", Priority = ThreadPriority.BelowNormal };
        thread.Start();
    }

    /// <summary>The newest results (for the ship and calibre last asked for once they're set up).</summary>
    public Results? Now => results;

    /// <summary>Whether it's working on an ask.</summary>
    public bool Busy => busy || pending != null;

    /// <summary>Ask for these (every frame the tab is open: an ask like the last one does nothing).</summary>
    public void Want(Ask ask)
    {
        lock (gate)
        {
            if (last != null && last.Same(ask))
                return;
            last = ask;
            pending = ask;
            cancel.Cancel();
            Monitor.Pulse(gate);
        }
    }

    Ask? last;

    void Loop()
    {
        while (true)
        {
            Ask ask;
            CancellationToken token;
            lock (gate)
            {
                while (pending == null && !disposed)
                    Monitor.Wait(gate);
                if (disposed)
                    return;
                ask = pending!;
                pending = null;
                cancel = new CancellationTokenSource();
                token = cancel.Token;
            }
            busy = true;
            try
            {
                Work(ask, token);
            }
            catch (OperationCanceledException)
            {
            }
            catch (Exception e)
            {
                if (results is { } r)
                    r.Error = e.Message;
            }
            busy = false;
        }
    }

    void Work(Ask ask, CancellationToken token)
    {
        var r = results;
        if (r == null || !ReferenceEquals(r.Ship, ask.Ship) || r.CalibreMm != ask.CalibreMm)
        {
            // a new ship or calibre: her class, and her own shells at the calibre
            r = new Results { Ship = ask.Ship, CalibreMm = ask.CalibreMm };
            var cls = ReferenceEquals(results?.Ship, ask.Ship) ? results!.Class : null;
            try
            {
                cls ??= ShipClassCompiler.Compile(ask.Ship);
                r.Class = cls;
                r.Shell = Loadout.ShellsAt(ask.Design, true, 0, ask.CalibreMm).First(s => s.Kind == "ap").Shell;
                int b = Array.FindIndex(cls.Batteries.ToArray(), x => x.Kind == Battery.Main);
                foreach (int m in cls.Links(cls.Batteries[b].Mounts))
                    r.MaxElevationDeg = Math.Max(r.MaxElevationDeg, cls.Mounts[m].MaxElevationDeg);
            }
            catch (Exception e)
            {
                r.Error = e.Message;
                results = r;
                return;
            }
            results = r;
        }
        if (r.Class == null)
            return;
        ImmuneZone New(PlatePick? pick) => new(r.Class, r.Shell, r.MaxElevationDeg, settings, 1, pick);
        // the plates picked, each shown after its first pass; then refined
        foreach (var p in ask.Plates)
            if (!r.Plates.ContainsKey(p))
            {
                var z = New(p);
                z.Fill(First, [ZoneTarget.Plate], threads, token);
                token.ThrowIfCancellationRequested();
                r.Plates[p] = z;
            }
        // the range picked: the shells fired in anger there
        r.Anger ??= new AngerRun(r.Class, r.Shell, r.MaxElevationDeg, settings, 1, AngerHits);
        int band = Array.FindIndex(r.Anger.Bands, x => ask.AngerM >= x.FromM && ask.AngerM < x.ToM);
        if (band < 0 && r.Anger.Bands.Length > 0)
            band = ask.AngerM < r.Anger.Bands[0].FromM ? 0 : r.Anger.Bands.Length - 1;
        if (band >= 0)
        {
            r.AngerDone = r.Anger.Bands[band].Fired >= AngerShips ? band : -1;
            r.Anger.FillBand(band, AngerShips, token);
            token.ThrowIfCancellationRequested();
            r.AngerDone = band;
        }
        foreach (var p in ask.Plates)
        {
            r.Plates[p].Refine(Refined, [ZoneTarget.Plate], threads, token);
            token.ThrowIfCancellationRequested();
        }
        // her armour's summary, and her vitals if they're looked at
        if (r.Armour == null)
        {
            var z = New(null);
            z.Fill(First, ImmuneZone.Armour, threads, token);
            token.ThrowIfCancellationRequested();
            r.Armour = z;
        }
        r.Armour.Refine(Refined, ImmuneZone.Armour, threads, token);
        token.ThrowIfCancellationRequested();
        if (ask.Vitals)
        {
            if (r.Vitals == null)
            {
                var z = New(null);
                z.Fill(First, [ZoneTarget.Magazines, ZoneTarget.Machinery], threads, token);
                token.ThrowIfCancellationRequested();
                r.Vitals = z;
            }
            r.Vitals.Refine(Refined, [ZoneTarget.Magazines, ZoneTarget.Machinery], threads, token);
        }
    }

    public void Dispose()
    {
        lock (gate)
        {
            disposed = true;
            cancel.Cancel();
            Monitor.Pulse(gate);
        }
    }
}
