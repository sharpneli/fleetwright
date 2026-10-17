using System.Text.Json;
using System.Text.Json.Nodes;
using Fleetwright.Shipgen;
using Fleetwright.Sim.Ships;

namespace Fleetwright.Sim.Tests;

/// <summary>The compiler over every golden build, and what a class promises: ids and indices that agree, the cell graph
/// both ways round, the mount table in the sprite's order, stable hashes, strictness, no allocation per query.</summary>
public class ShipClassTests
{
    [Fact]
    public void Compiles_every_golden_build()
    {
        var failed = Fleet.All.Where(b => b.Error != null).Select(b => $"{b.Case.Name}: {b.Error}").ToList();
        Assert.True(failed.Count == 0, string.Join("\n", failed.Take(10)));
        Assert.True(Fleet.All.Count >= 320, $"{Fleet.All.Count} builds");
        var problems = new List<string>();
        foreach (var b in Fleet.All)
            foreach (var p in Problems(b.Class!))
                problems.Add($"{b.Case.Name}: {p}");
        Assert.True(problems.Count == 0, string.Join("\n", problems.Take(20)));
    }

    /// <summary>What every class must hold, in words when it doesn't.</summary>
    static List<string> Problems(ShipClass c)
    {
        var p = new List<string>();
        for (int i = 0; i < c.Components.Length; i++)
            if (c.ComponentIndex(c.ComponentId(i)) != i)
                p.Add($"component {i} doesn't round-trip its id");
        for (int i = 0; i < c.Cells.Length; i++)
            if (c.CellIndex(c.CellId(i)) != i)
                p.Add($"cell {i} doesn't round-trip its id");

        // the mount table: the components with a turret shape, in component order (as TopView draws them)
        int m = 0;
        for (int i = 0; i < c.Components.Length; i++)
            if (c.Components[i].Kind is ComponentKind.Main or ComponentKind.Secondary or ComponentKind.Torpedo)
            {
                if (m >= c.Mounts.Length || c.Mounts[m].Component != i || c.Components[i].Detail != m)
                    p.Add($"mount {m} isn't component {c.ComponentId(i)}");
                m++;
            }
        if (m != c.Mounts.Length)
            p.Add($"{c.Mounts.Length} mounts for {m} mount components");

        // the cell graph lists each edge from both cells, with the same join
        for (int i = 0; i < c.Cells.Length; i++)
            foreach (var e in c.Neighbours(c.Cells[i].Neighbours))
            {
                bool back = false;
                foreach (var f in c.Neighbours(c.Cells[e.Cell].Neighbours))
                    back |= f.Cell == i && f.Join == e.Join && f.Through == e.Through;
                if (!back)
                    p.Add($"cell {c.CellId(i)} -> {c.CellId(e.Cell)} has no way back");
            }

        // a room's cells are its own or shared with it
        for (int r = 0; r < c.Rooms.Length; r++)
            foreach (int cell in c.Links(c.Rooms[r].Cells))
                if (c.Cells[cell].Room != r && !c.Links(c.Cells[cell].Also).Contains(r))
                    p.Add($"room {c.RoomId(r)} lists cell {c.CellId(cell)}, which isn't in it");

        // every shaft line's parts point back at it
        for (int s = 0; s < c.ShaftLines.Length; s++)
        {
            var sl = c.ShaftLines[s];
            if (c.Components[sl.Shaft].Detail != s || c.Components[sl.Propeller].Detail != s || sl.Alley >= 0 && c.Components[sl.Alley].Detail != s)
                p.Add($"shaft line {c.ComponentId(sl.Shaft)} isn't linked both ways");
        }

        // the power curve rises through the report's PowerShp (rounded to 100 shp, from a displacement a step short of
        // settled) at the designed speed, and SpeedAt inverts it
        var mo = c.Motion;
        if (Math.Abs(c.PowerAt(mo.MaxSpeedKn) - mo.PowerShp) > 50 + 0.01 * mo.PowerShp)
            p.Add($"power at {mo.MaxSpeedKn} kn is {c.PowerAt(mo.MaxSpeedKn):F0}, not {mo.PowerShp}");
        for (double kn = 0.25; kn < mo.MaxSpeedKn * 1.2; kn += 0.7)
            if (Math.Abs(c.SpeedAt(c.PowerAt(kn)) - kn) > 1e-9)
                p.Add($"SpeedAt(PowerAt({kn})) is {c.SpeedAt(c.PowerAt(kn))}");
        if (mo.Shafts != c.ShaftLines.Length && c.ShaftLines.Length > 0)
            p.Add($"{mo.Shafts} shafts in the plant, {c.ShaftLines.Length} shaft lines");
        return p;
    }

    [Fact]
    public void Hashes_name_the_design_and_its_layout()
    {
        var path = Path.Combine(Fleet.Shipgen, "designs", "bismarck.json");
        var a = ShipClassCompiler.Compile(ShipDesign.Build(Design.Load(path)));
        var b = ShipClassCompiler.Compile(ShipDesign.Build(Design.Load(path)));
        Assert.Equal(a.DesignHash, b.DesignHash);
        Assert.Equal(a.LayoutHash, b.LayoutHash);
        Assert.Equal(ShipDesign.Version, a.ShipgenVersion);

        // the file's spelling doesn't matter: reindented, the same design
        var text = JsonNode.Parse(File.ReadAllText(path))!.ToJsonString(new JsonSerializerOptions { WriteIndented = true, IndentSize = 7 });
        var respelled = JsonSerializer.Deserialize(text, ShipgenJson.Default.Design)!;
        Assert.Equal(a.DesignHash, ContentHash.OfDesign(respelled));

        // a different design, or the same one a knot faster, is another class
        var yamato = Fleet.Get("yamato").Class!;
        Assert.NotEqual(a.DesignHash, yamato.DesignHash);
        Assert.NotEqual(a.LayoutHash, yamato.LayoutHash);
        var faster = Design.Load(path) with { SpeedKn = Design.Load(path).SpeedKn + 1 };
        Assert.NotEqual(a.DesignHash, ContentHash.OfDesign(faster));

        // every design's layout is its own
        var layouts = Fleet.Designs.Select(d => d.Class!.LayoutHash).ToList();
        Assert.Equal(layouts.Count, layouts.Distinct().Count());
    }

    static Ship Mutated(string design, Action<JsonObject> edit)
    {
        var ship = ShipDesign.Build(Design.Load(Path.Combine(Fleet.Shipgen, "designs", design + ".json")));
        var node = JsonSerializer.SerializeToNode(ship.Hitboxes, ShipgenJson.Default.Hitboxes)!.AsObject();
        edit(node);
        return ship with { Hitboxes = node.Deserialize(ShipgenJson.Default.Hitboxes)! };
    }

    [Fact]
    public void A_build_the_compiler_cant_take_is_an_error()
    {
        string Error(Action<JsonObject> edit) => Assert.Throws<ShipCompileException>(() => ShipClassCompiler.Compile(Mutated("nassau", edit))).Message;
        static JsonObject Comp(JsonObject hb, string id) => hb["components"]!.AsArray().Select(c => c!.AsObject()).First(c => (string)c["id"]! == id);

        Assert.Contains("two components are called \"A\"", Error(hb => Comp(hb, "Y")["id"] = "A"));
        Assert.Contains("unknown component kind \"zeppelin\"", Error(hb => Comp(hb, "Foremast")["kind"] = "zeppelin"));
        Assert.Contains("no room \"Magazine Z\"", Error(hb => Comp(hb, "A")["magazine"] = "Magazine Z"));
        Assert.Contains("no deck or bulkhead \"Bulkhead 99\"",
            Error(hb => hb["cells"]![5]!["neighbours"]![0]![1] = "Bulkhead 99"));
        // (a room's kind is an enum: a build can't carry an unknown one, and its JSON won't load)
        Assert.Contains("unknown room kind \"ballroom\"",
            Assert.Throws<JsonException>(() => Mutated("nassau", hb => hb["rooms"]![0]!["kind"] = "ballroom")).Message);
        // a link of the wrong kind: a gun's barbette that is a mast
        Assert.Contains("is a mast, not a barbette", Error(hb => Comp(hb, "A")["barbette"] = "Foremast"));
        // the untouched build compiles
        ShipClassCompiler.Compile(Mutated("nassau", _ => { }));
    }

    [Fact]
    public void The_casemate_flag_and_the_bottom_tier_compile()
    {
        var c = Fleet.Get("mikasa").Class!;
        Assert.Contains(c.Mounts.ToArray(), m => m.Casemate);
        Assert.Contains(c.Mounts.ToArray(), m => !m.Casemate && m.Kind == ComponentKind.Main);
        Assert.Equal(1, c.Tiers.ToArray().Count(t => t.Floor == -1));
        var seaplane = Fleet.Get("seaplane_carrier").Class!;
        Assert.Equal(ComponentKind.HangarBay, seaplane.Components[seaplane.ComponentIndex("Hangar bay")].Kind);
        Assert.Equal(ComponentKind.Superstructure, seaplane.Components[seaplane.ComponentIndex("Hangar")].Kind);
    }

    [Fact]
    public void Queries_allocate_nothing()
    {
        var c = Fleet.Get("bismarck").Class!;
        var st = new ShellTest(c);
        var shots = Enumerable.Range(0, 200).Select(k => st.Draw(Core.RandomStream.FromSeed(7).Derive((ulong)k))).ToArray();
        double sink = 0;
        void Work()
        {
            for (double kn = 0; kn < 35; kn += 0.3)
                sink += c.SpeedAt(c.PowerAt(kn));
            for (int i = 0; i < c.Cells.Length; i++)
                foreach (var e in c.Neighbours(c.Cells[i].Neighbours))
                    sink += e.Cell;
            sink += c.ComponentIndex("A") + c.CellIndex(c.CellId(3));
            foreach (ref readonly var s in c.Solids.Solids)
                sink += c.Solids.Inside(s, 1, 2, 0) ? 1 : 0;
            foreach (var shot in shots)
                sink += (int)st.Trace(shot, out _);
        }
        Work();
        long before = GC.GetAllocatedBytesForCurrentThread();
        Work();
        Assert.Equal(0, GC.GetAllocatedBytesForCurrentThread() - before);
        Assert.NotEqual(0, sink);
    }
}
