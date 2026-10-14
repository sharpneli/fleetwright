using System.Diagnostics;
using System.Globalization;
using Fleetwright.Sim;
using Fleetwright.Sim.Combat;
using Fleetwright.Sim.Core;
using Fleetwright.Sim.Ships;

namespace Fleetwright.Shipgen.Cli;

/// <summary>`shipgen flood`: the flooding model alone (docs/battle/fighting.md 4.3b, "The instruments"): a ship of the
/// design stopped at sea, holed where it's told (on the side at a point, or every space of a kind flooded through its
/// floor), and the world run on: the water aboard, what's left of its reserve buoyancy, its sinkage, trim, heel and GM,
/// and the spaces flooded out of action, every few minutes, until it goes down or the time is up. First the class's
/// facts the flooding reads.</summary>
static class FloodBench
{
    static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

    public record Options(string? Holes, string? Rooms, double Minutes, double EverySeconds, int StepMs, bool Cells);

    public static int Run(string design, Func<string, Design> load, string settingsPath, Options o)
    {
        var settings = SimSettings.Load(settingsPath);
        if (o.StepMs > 0)
            settings = settings with { FloodMs = o.StepMs };
        var c = ShipClassCompiler.Compile(ShipDesign.Build(load(design)));
        var f = new FloodModel(c);
        var h = c.Hydrostatics;
        double hull = 0;
        foreach (ref readonly var cell in c.Cells)
            hull += cell.VolumeM3;
        Console.WriteLine(string.Format(Inv, "{0}: {1:0} t at full load, {2} cells ({3} joins), the watertight hull {4:0} m³: reserve buoyancy {5:0} t ({6:0}%)",
            c.Name, h.DisplacementT, c.Cells.Length, f.Edges.Length, hull, f.ReserveT, 100 * f.ReserveT / h.DisplacementT));
        Console.WriteLine(string.Format(Inv, "  freeboard {0:0.00} m (the deck at the centre of flotation {1:0.00} m over the waterline), TPC {2:0.0} t, MCT {3:0} t·m, GM {4:0.00} m, KG {5:0.00} m, L {6:0} m",
            c.Vertical.Freeboard, f.DeckAtLcf - c.Vertical.Waterline, h.TpcT, h.MctTm, h.GmT, h.Kg, c.Length));

        if (o.Cells)
            for (int i = 0; i < c.Cells.Length; i++)
                if (f.Weather[i] && c.Cells[i].Box.Top < 0)
                    Console.WriteLine(string.Format(Inv, "    open above, under the main deck: {0} (top {1:0.0} m)", c.CellId(i), c.Cells[i].Box.Top));
        var w = new World(WorldTime.FromUtc(1916, 5, 31, 14), 1, settings);
        int cls = w.Ships.AddClass(c);
        w.Apply(ShipSystem.Spawn(cls, 56, 4, 0, 0));
        int e = w.Ships.NewestEntity;
        foreach (string spec in (o.Holes ?? "").Split(',', StringSplitOptions.RemoveEmptyEntries))
            Hole(w, e, c, spec);
        foreach (string spec in (o.Rooms ?? "").Split(',', StringSplitOptions.RemoveEmptyEntries))
            Rooms(w, e, c, spec);

        var sw = Stopwatch.StartNew();
        var start = w.Time;
        int slot = w.Ships.Store.SlotOf(e);
        var k = w.Ships.Store.Combat.Condition(slot);
        Console.WriteLine("  min   water t  reserve  sink m  trim°   heel°   GM m   spaces out");
        long step = Math.Max(settings.FloodMs, 1000), end = (long)(o.Minutes * 60_000), every = (long)(o.EverySeconds * 1000);
        for (long t = 0; ; t += step)
        {
            if (t > 0)
                w.AdvanceTo(start + t);
            slot = w.Ships.Store.SlotOf(e);
            bool down = slot < 0 || k.Status == ShipStatus.GoingDown;
            if (t % every != 0 && !down && t < end)
                continue;
            int outs = 0;
            for (int r = 0; r < c.Rooms.Length; r++)
                outs += k.State[c.RoomNode(r)] >= PartState.Disabled ? 1 : 0;
            var a = k.Attitude;
            Console.WriteLine(string.Format(Inv, "  {0,5:0.0}  {1,7:0}  {2,6:0}%  {3,6:0.00}  {4,6:0.00}  {5,6:0.00}  {6,5:0.00}  {7}{8}", t / 60_000.0, k.WaterT,
                100 * (f.ReserveT - k.WaterT) / Math.Max(f.ReserveT, 1), a.SinkageM, a.TrimDeg, a.HeelDeg, k.GmM, outs,
                down ? $"  GOING DOWN ({k.Loss})" : ""));
            if (down || t >= end)
                break;
        }
        Console.WriteLine(string.Format(Inv, "  {0:0.00} s for {1:0} min of world time", sw.Elapsed.TotalSeconds, (w.Time.Ms - start.Ms) / 60_000.0));
        var wet = Enumerable.Range(0, c.Rooms.Length).Select(r => (r, t: c.Links(c.Rooms[r].Cells).ToArray().Sum(i => k.WaterM3[i]) * FloodModel.SeaT))
            .Where(x => x.t >= 1).OrderByDescending(x => x.t).ToList();
        if (wet.Count > 0)
            Console.WriteLine("  water in: " + string.Join(", ", wet.Select(x => string.Format(Inv, "{0} {1:0} t{2}", c.RoomId(x.r), x.t,
                k.State[c.RoomNode(x.r)] >= PartState.Disabled ? " (out)" : ""))));
        if (o.Cells)
            for (int i = 0; i < c.Cells.Length; i++)
            {
                if (k.WaterM3[i] <= 0)
                    continue;
                var b = c.Cells[i].Box;
                Console.WriteLine(string.Format(Inv, "    {0,-16} {1,6:0} of {2,6:0} m³, {3:0.0} to {4:0.0} m, {5}{6}: {7}", c.CellId(i), k.WaterM3[i], f.Capacity[i], b.Base,
                    b.Top, c.Cells[i].Room >= 0 ? c.RoomId(c.Cells[i].Room) : "-", k.BreachM2[i] > 0 ? $", holed {k.BreachM2[i]:0.###} m²" : "",
                    string.Join(", ", c.Neighbours(c.Cells[i].Neighbours).ToArray().Select(n => $"{c.CellId(n.Cell)} {n.Join}"))));
            }
        return 0;
    }

    /// <summary>A hole on the side: WHERE:Z:AREA[:p], WHERE mid (the centre of flotation), bow or stern (a tenth of the length
    /// in from the end) or x in metres; Z metres over the waterline; AREA m²; :p to port (else starboard).</summary>
    static void Hole(World w, int e, ShipClass c, string spec)
    {
        var p = spec.Split(':');
        if (p.Length < 3)
            throw new ArgumentException($"a hole is WHERE:Z:AREA[:p], not {spec}");
        var at = p[0] switch { "mid" => HoleAt.Mid, "bow" => HoleAt.Bow, "stern" => HoleAt.Stern, _ => HoleAt.X };
        double x = at == HoleAt.X ? double.Parse(p[0], Inv) : 0;
        Console.WriteLine($"  a hole of {p[2]} m² at {p[0]}, {p[1]} m over the waterline");
        w.Apply(DamageSystem.SideHole(e, at, x, double.Parse(p[1], Inv), double.Parse(p[2], Inv), p.Length < 4 || p[3] != "p"));
    }

    /// <summary>Spaces flooded: KIND[:N], the N (default all) rooms of a kind nearest amidships, each cell holed 1 m² at its
    /// floor.</summary>
    static void Rooms(World w, int e, ShipClass c, string spec)
    {
        var p = spec.Split(':');
        var kind = Enum.Parse<RoomKind>(p[0].Replace("_", ""), ignoreCase: true);
        Console.WriteLine($"  {spec} flooded");
        w.Apply(DamageSystem.FloodRooms(e, kind, p.Length > 1 ? int.Parse(p[1], Inv) : int.MaxValue));
    }
}
