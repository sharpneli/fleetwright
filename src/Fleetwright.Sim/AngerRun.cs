using Fleetwright.Sim.Combat;
using Fleetwright.Sim.Core;
using Fleetwright.Sim.Ships;

namespace Fleetwright.Sim;

/// <summary>Shells fired in anger (docs/designer-ui/immune-zone.md section 2, decision 6): a sweep over the fight, ships
/// of a class each given hits of a shell at one range and one target angle, through the world's own damage system
/// (<see cref="GivenHits.Ship"/>), settled for their flooding and fire, tallied by range band. Each band is a world of its
/// own, its ships added a round at a time over every band, so the bands fill together and stop together; each ship's
/// range and hits come from a stream of her own, so a band's ships are the same however many threads fill them.</summary>
public sealed class AngerRun
{
    /// <summary>A band's width, m; the first starts at one width.</summary>
    public const double BandM = 2000;

    /// <summary>A hit as it struck her: where it first met her (ship frame) and its class (<see cref="GivenHits.Report.HitClasses"/>).</summary>
    public readonly record struct Dot(P3 Point, byte Class);

    /// <summary>One range band: its ships' report, and each hit's dot.</summary>
    public sealed class Band
    {
        public double FromM { get; init; }
        public double ToM { get; init; }
        public GivenHits.Report Report { get; init; } = null!;
        public List<Dot> Dots { get; } = [];
        internal World? World;
        internal int Entity;
        internal readonly HitPath Path = new();
        internal int Cls;
        /// <summary>The ships it has had (by their index: the same ships however it was filled).</summary>
        public int Fired { get; internal set; }
    }

    public ShipClass Class { get; }
    public ShellTypeRow Shell { get; }
    public double ReachM { get; }
    public int HitsPerShip { get; }
    public double SettleS { get; }
    public Band[] Bands { get; }
    /// <summary>The ships each band has had.</summary>
    public int ShipsPerBand { get; private set; }

    readonly RangeTable table;
    readonly SimSettings settings;
    readonly ulong seed;

    public AngerRun(ShipClass c, in ShellTypeRow shell, double maxElevationDeg, SimSettings s, ulong seed, int hitsPerShip = 10, double settleMin = 30)
    {
        (Class, Shell, settings, this.seed, HitsPerShip, SettleS) = (c, shell, s, seed, hitsPerShip, settleMin * 60);
        table = new RangeTable(BallisticKey.Of(shell, maxElevationDeg), ImmuneZone.MuzzleM);
        ReachM = table.ReachM;
        var bands = new List<Band>();
        for (double from = BandM; from + BandM / 4 < ReachM; from += BandM)
            bands.Add(new Band { FromM = from, ToM = Math.Min(from + BandM, ReachM), Report = Fresh(c) });
        Bands = [.. bands];
    }

    static GivenHits.Report Fresh(ShipClass c) => new()
    {
        PartsOut = new int[c.NodeCount], BatteryMounts = new double[c.Batteries.Length], BatteryWorking = new double[c.Batteries.Length],
    };

    /// <summary>The target angle the k-th ship of a band is hit from: the zone's angles in a spread order, so a few ships
    /// see them all round.</summary>
    public static double AngleOf(int k) => ImmuneZone.Angles[k * 5 % ImmuneZone.Angles.Length];

    /// <summary>Add ships to every band, a round at a time, until each has had this many; stops after the round it's
    /// cancelled in. progress, if given, is told after each round.</summary>
    public void Fill(int shipsPerBand, int threads = 1, CancellationToken cancel = default, Action<int>? progress = null)
    {
        var opts = new ParallelOptions { MaxDegreeOfParallelism = Math.Max(1, threads) };
        while (ShipsPerBand < shipsPerBand && !cancel.IsCancellationRequested)
        {
            int k = ShipsPerBand;
            Parallel.For(0, Bands.Length, opts, b => OneShip(Bands[b], b, k));
            ShipsPerBand++;
            progress?.Invoke(ShipsPerBand);
        }
    }

    /// <summary>Add ships to one band until it has had this many (the designer's range picked on its chart): its own
    /// ships, the same as <see cref="Fill"/> gives it; stops between ships if cancelled.</summary>
    public void FillBand(int b, int ships, CancellationToken cancel = default)
    {
        var band = Bands[b];
        while (band.Fired < ships && !cancel.IsCancellationRequested)
            OneShip(band, b, band.Fired);
    }

    void OneShip(Band band, int b, int k)
    {
        band.Fired = Math.Max(band.Fired, k + 1);
        if (band.World == null)
        {
            // each band a world of its own, made when its first ship comes
            band.World = new World(WorldTime.FromUtc(1916, 5, 31, 14), RandomStream.Named(seed, "anger world").Derive((ulong)b).Seed, settings);
            band.Cls = band.World.Ships.AddClass(Class);
        }
        var rng = RandomStream.Named(seed, "anger").Derive((ulong)b).Derive((ulong)k);
        double range = rng.NextDouble(band.FromM, band.ToM);
        var (fall, strike) = table.At(range);
        if (double.IsNaN(fall))
            return;
        var aim = new GivenHits.Aim(Class, Shell, (HitQuery.Flight(AngleOf(k), fall), (range, 0, fall, strike)), settings);
        var dots = band.Dots;
        band.Entity = GivenHits.Ship(band.Report, band.World, band.Cls, band.Entity, aim, band.Path, ref rng, HitsPerShip, SettleS,
            each: (_, path, t, _) =>
            {
                int first = HitQuery.FirstIn(path);
                if (first >= 0)
                    dots.Add(new Dot(path.Crossings[first].Point, (byte)GivenHits.Report.ClassOf(t.Outcome)));
            });
    }
}
