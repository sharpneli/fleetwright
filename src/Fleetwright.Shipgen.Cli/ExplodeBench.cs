using System.Diagnostics;
using System.Globalization;
using Fleetwright.Sim;
using Fleetwright.Sim.Combat;
using Fleetwright.Sim.Core;
using Fleetwright.Sim.Ships;

namespace Fleetwright.Shipgen.Cli;

/// <summary><c>shipgen explode</c> (docs/battle/explosions.md EX0, EX1): any charge in a room of a design, or a magazine
/// alight, through the gas network on the ship's own plating, beside what the game does today (<see cref="Blast.Cascade"/> or
/// <see cref="Magazines.SetOff"/>). With no charge or magazine given, the plan's anchors.</summary>
static class ExplodeBench
{
    static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

    public record Options(string? Room, double Kg, string Explosive, double CalibreMm, string? Magazine, string? Packaging, double Flooded, string? Doctrine,
        double Belt, double Keep, double Tear, double Panel, double Rupture, double Step, bool Trace, bool Cells, int Salvo = 0, bool Girder = false, double Whip = -1);

    public static int Run(IReadOnlyList<string> designs, Func<string, Design> load, string settingsPath, Options o)
    {
        var s = SimSettings.Load(settingsPath);
        var g = GasKnobs.Of(s);
        if (o.Belt >= 0)
            g.BeltShare = o.Belt;
        if (o.Keep >= 0)
            g.HeatKeep = o.Keep;
        if (o.Tear >= 0)
            g.TearShare = o.Tear;
        if (o.Panel >= 0)
            g.PanelMs = o.Panel;
        if (o.Rupture >= 0)
            g.RuptureMPaMm = o.Rupture;
        if (o.Step > 0)
            g.StepK = o.Step;
        if (o.Whip >= 0)
            g.WhipShare = o.Whip;
        if (o.Trace)
            GasNetwork.Trace = Console.WriteLine;
        Console.WriteLine(string.Format(Inv, "the network's rungs: belt {0:0.##}, heat kept {1:0.##}, tear {2:0.##} of a boundary, panel {3:0.#} ms, rupture {4:0.###} MPa/mm, step x{5:0.##}", g.BeltShare, g.HeatKeep, g.TearShare, g.PanelMs, g.RuptureMPaMm, g.StepK));
        if (o.Girder)
        {
            Intact(designs, load, s);
            return 0;
        }
        if (o.Salvo > 0)
        {
            foreach (var d in designs.Count > 0 ? designs : ["destroyer"])
                Salvo(Compile(load(d), o.Packaging), s, g, o.Salvo, o.Kg > 0 ? o.Kg : 61.7, o.Explosive, o.CalibreMm);
            return 0;
        }
        if (o.Kg > 0)
        {
            foreach (var d in designs.Count > 0 ? designs : ["destroyer"])
                Charge(Compile(load(d), o.Packaging), s, g, o.Room ?? "accommodation", o.Kg, o.Explosive, o.CalibreMm, null, o.Cells);
            return 0;
        }
        if (o.Magazine != null)
        {
            foreach (var d in designs.Count > 0 ? designs : ["lion"])
                Magazine(Compile(load(d), o.Packaging), s, g, o.Magazine, o.Flooded, o.Doctrine, o.Cells);
            return 0;
        }
        Anchors(load, s, g);
        return 0;
    }

    static ShipClass Compile(Design d, string? packaging)
    {
        if (packaging != null)
            d = d with { GunTech = d.GunTech?.ToDictionary(kv => kv.Key, kv => kv.Value with { Packaging = packaging }) };
        return ShipClassCompiler.Compile(ShipDesign.Build(d));
    }

    /// <summary>The plan's anchors: 11 §5.5's charges, then the magazines of Lion by her stows and the others'.</summary>
    static void Anchors(Func<string, Design> load, SimSettings s, in GasKnobs g)
    {
        Console.WriteLine("\ncharges (damage/11 §5.5): the network beside today's cascade");
        var destroyer = Compile(load("destroyer"), null);
        var battleship = Compile(load("battleship"), null);
        var sd = Compile(load("super_dreadnought"), null);
        Charge(destroyer, s, g, "accommodation", 3.3, "Explosive D", 127, "1. 5\" in a destroyer's compartment: no WT bulkhead, no door");
        Charge(destroyer, s, g, "accommodation", 9.7, "Explosive D", 203, "2. 8\" the same: its doors blown");
        Charge(destroyer, s, g, "accommodation", 60, "TNT", 300, "3. 250 kg SAP: the next bulkheads and decks, little beyond ~6 m");
        Charge(destroyer, s, g, "engine_room", 61.7, "TNA", 460, "46 cm Type 0: the compartment and often the next");
        Charge(battleship, s, g, "accommodation", 18.6, "Explosive D", 406, "4a. 16\" AP in a small space: its crew down");
        Charge(battleship, s, g, "engine_room", 18.6, "Explosive D", 406, "4b. 16\" AP in an engine room: little beyond its near zone");
        Charge(sd, s, g, "accommodation", 58.6, "Black powder", 381, "6. black powder: a sixth to an eighth of TNT's pressure");
        Charge(sd, s, g, "accommodation", 58.6, "TNT", 381, null);
        Console.WriteLine("\nthe magazines, Lion by her stow (research 10 §6: bags tier 3, Clarkson tier 1, brass tier 0-1, 95% flooded tier 0)");
        foreach (var (pack, flooded, title) in new[] { ("bag", 0.0, "bags"), ("clarkson", 0.0, "Clarkson (R 32 today)"), ("brass", 0.0, "brass"), ("bag", 0.95, "bags, 95% flooded") })
        {
            Console.WriteLine($"  {title}:");
            Magazine(Compile(load("lion"), pack), s, g, "all", flooded, null, false);
        }
        Console.WriteLine("\nthe magazines, as designed");
        foreach (var d in new[] { "iron_duke", "seydlitz", "destroyer", "v25" })
        {
            Console.WriteLine($"  {d}:");
            Magazine(Compile(load(d), null), s, g, "all", 0, null, false);
        }
    }

    static World Ship(ShipClass c, SimSettings s, out ShipCondition k, out int slot)
    {
        var w = new World(WorldTime.FromUtc(1916, 5, 31, 14), 1, s);
        w.Apply(ShipSystem.Spawn(w.Ships.AddClass(c), 50, -20, 0, 0));
        slot = w.Ships.Store.SlotOf(w.Ships.NewestEntity);
        k = w.Ships.Store.Combat.Condition(slot);
        return w;
    }

    static void Charge(ShipClass c, SimSettings s, in GasKnobs g, string room, double kg, string explosive, double calibreMm, string? title, bool cells = false)
    {
        var x = Explosives.Get(explosive) ?? throw new ArgumentException($"no explosive \"{explosive}\"");
        var shell = new ShellTypeRow
        {
            CalibreMm = calibreMm, FillerKg = kg, FillerRe = x.Re ?? 1, FillerQDet = x.QDet ?? 4.5, FillerQComb = x.QComb ?? 15,
            FillerAirM3Kg = x.AirM3Kg ?? 0, FillerEta = x.Eta ?? 1,
        };
        var parts = room.Split(':');
        if (!SnakeEnum<RoomKind>.TryParse(parts[0], out var kind))
            throw new ArgumentException($"no room kind \"{parts[0]}\"");
        int nth = parts.Length > 1 ? int.Parse(parts[1], Inv) : 0;
        var rooms = Enumerable.Range(0, c.Rooms.Length).Where(i => c.Rooms[i].Kind == kind)
            .OrderBy(i => Math.Abs((c.Rooms[i].X0 + c.Rooms[i].X1) / 2 - c.Hydrostatics.Lcf)).ToList();
        if (nth >= rooms.Count)
        {
            Console.WriteLine($"  {c.Name}: no {parts[0]} {nth}");
            return;
        }
        int r0 = rooms[nth], cell = -1;
        foreach (int ci in c.Links(c.Rooms[r0].Cells))
            if (cell < 0 || c.Cells[ci].VolumeM3 > c.Cells[cell].VolumeM3)
                cell = ci;
        var b = c.Cells[cell].Box;
        var at = new P3((b.X0 + b.X1) / 2, (b.Y0 + b.Y1) / 2, (b.Base + b.Top) / 2);
        double size = Math.Cbrt(kg * shell.FillerRe), rw = s.WreckRadiusK * size;

        _ = Ship(c, s, out var k0, out _);
        var rng = RandomStream.Named(1, "explode bench");
        var old = Blast.Cascade(c, k0, cell, at, kg, shell, 0, rw, s.NearHoleK * size, s, ref rng);

        _ = Ship(c, s, out var k, out _);
        var net = new GasNetwork(c);
        net.Detonate(k, cell, at, kg, shell, 0, rw, s.NearHoleK * size, g, s);   // a first one: the timing below is a burst's own
        net.Forget();
        _ = Ship(c, s, out k, out _);
        var sw = Stopwatch.StartNew();
        var r = net.Detonate(k, cell, at, kg, shell, 0, rw, s.NearHoleK * size, g, s);
        double us = sw.Elapsed.TotalMilliseconds * 1000;
        if (title != null)
            Console.WriteLine($"  {title}");
        Console.WriteLine(string.Format(Inv, "    {0}: {1:0.#} kg {2} in {3} ({4:0} m³ of gas)", c.Name, kg, explosive, c.RoomId(r0), Blast.GasM3(c, k, cell)));
        Console.WriteLine(string.Format(Inv, "      today:   first {0:0} kPa, felt {1:0}; {2} spaces, gave: {3} boundaries, {4} doors; to the sea or air {5:0.0} m²",
            old.FirstMPa * 1000, old.FeltMPa * 1000, old.Spaces, old.Ruptured, old.Doors, old.HoleM2));
        Console.WriteLine(string.Format(Inv, "      network: peak {0:0} kPa; {1} spaces, holed " + r.Holed + ", gave: {2} bulkheads, {3} decks, {4} doors, {5} sides, {6} weather deck, {7} bottom; {8:0.00} s in {9} steps ({10:0} µs)",
            r.PeakKPa, r.Spaces, r.Bulkheads, r.Decks, r.Doors, r.Sides, r.WeatherDecks, r.Bottoms, r.EndS, r.Steps, us));
        Hull(c, new HullGirder(c), net, r.WhipEM, g, s);
        if (cells)
            Cells(c, net);
    }

    /// <summary>N bursts spaced along her side at the waterline, on one ship, each finding what those before it opened:
    /// a salvo's shells burst just inside the plate (their own contact hole a vent), as the near-wall burst of 11 §5.1.</summary>
    static void Salvo(ShipClass c, SimSettings s, in GasKnobs g, int shells, double kg, string explosive, double calibreMm)
    {
        var x = Explosives.Get(explosive) ?? throw new ArgumentException($"no explosive \"{explosive}\"");
        var shell = new ShellTypeRow
        {
            CalibreMm = calibreMm, FillerKg = kg, FillerRe = x.Re ?? 1, FillerQDet = x.QDet ?? 4.5, FillerQComb = x.QComb ?? 15,
            FillerAirM3Kg = x.AirM3Kg ?? 0, FillerEta = x.Eta ?? 1,
        };
        double size = Math.Cbrt(kg * shell.FillerRe), rw = s.WreckRadiusK * size;
        _ = Ship(c, s, out var k, out _);
        var net = new GasNetwork(c);
        var girder = new HullGirder(c);
        double salvoJ = 0;
        double x0 = double.PositiveInfinity, x1 = double.NegativeInfinity;
        foreach (ref readonly var cell in c.Cells)
            (x0, x1) = (Math.Min(x0, cell.Box.X0), Math.Max(x1, cell.Box.X1));
        double wl = c.Vertical.Waterline, plate = c.Plating.ShellMm;
        double contact = Math.PI * Math.Pow(s.HoleRadiusK * size, 2) * 6 / Math.Max(plate, 1);
        Console.WriteLine(string.Format(Inv, "  {0}: {1} x {2:0.#} kg {3} along her side at the waterline, {4:0.0} m apart; a contact hole {5:0.0} m² in {6:0.0} mm", c.Name, shells, kg, explosive, (x1 - x0) / shells, contact, plate));
        for (int i = 0; i < shells; i++)
        {
            double xs = x0 + (x1 - x0) * (i + 0.5) / shells, y = Math.Max(c.Hull.HalfWidth(xs, wl + 1) - 1, 0), z = wl + 1;
            int at = -1;
            for (int ci = 0; ci < c.Cells.Length; ci++)
            {
                var b = c.Cells[ci].Box;
                if (xs >= b.X0 && xs <= b.X1 && y >= b.Y0 && y <= b.Y1 && z >= b.Base && z <= b.Top)
                    at = ci;
            }
            if (at < 0)
            {
                Console.WriteLine(string.Format(Inv, "    shell {0}: x {1:0.0}, no cell there (over the end)", i + 1, xs));
                continue;
            }
            var r = net.Detonate(k, at, new P3(xs, y, z), kg, shell, contact, rw, s.NearHoleK * size, g, s);
            net.Commit();
            salvoJ += r.WhipEM;
            var h = girder.Read(net.EdgesLost, net.FacesLost, null, 0, girder.Whip(salvoJ, g.WhipShare), s);
            Console.WriteLine(string.Format(Inv, "    shell {0} at x {1,6:0.0} in {2,-12}: peak {3,5:0} kPa, {4,3} spaces, holed {5}, gave {6} bulkheads, {7} decks, {8} doors, {9} sides, {10} weather deck, {11} bottom | worst at {12:0.00}: deck {13:0%}, sides {14:0%}, bottom {15:0%}, d {16:0.00}, load {17:0.00}",
                i + 1, xs, c.CellId(at), r.PeakKPa, r.Spaces, r.Holed, r.Bulkheads, r.Decks, r.Doors, r.Sides, r.WeatherDecks, r.Bottoms, h.Station, h.Deck, h.Side, h.Bottom, h.D, h.Load));
        }
        Hull(c, girder, net, salvoJ, g, s);
        Console.WriteLine(string.Format(Inv, "    the sea's way in: {0:0.0} m² of holes, of which under the waterline {1:0.0}", Sum(k.BreachM2), SumUnder(c, k)));
    }

    static double Sum(double[] a) => a.Sum();

    static double SumUnder(ShipClass c, ShipCondition k)
    {
        double sum = 0;
        for (int i = 0; i < c.Cells.Length; i++)
            if (k.BreachZ[i] < c.Vertical.Waterline)
                sum += k.BreachM2[i];
        return sum;
    }

    /// <summary>What the explosion left of the girder: calm water and a seaway.</summary>
    static void Hull(ShipClass c, HullGirder girder, GasNetwork net, double energyMass, in GasKnobs g, SimSettings s)
    {
        double whip = girder.Whip(energyMass, g.WhipShare);
        var calm = girder.Read(net.EdgesLost, net.FacesLost, null, 0, whip, s);
        var sea = girder.Read(net.EdgesLost, net.FacesLost, null, 1, whip, s);
        Console.WriteLine(string.Format(Inv, "      the girder: worst at {0:0.00} of her length, section lost {1:0.00} (decks {2:0%}, sides {3:0%}, bottom {4:0%}, longitudinals {5:0%}), left {6:0.00} design moments; whip {7:0.00}; load {8:0.00} calm{9}, {10:0.00} in a seaway{11}{12}",
            calm.Station, calm.D, calm.Deck, calm.Side, calm.Bottom, calm.Longitudinal, calm.Capacity, whip, calm.Load, calm.Load >= 1 ? " BROKEN" : "", sea.Load, sea.Load >= 1 ? " BROKEN" : "", calm.EndLoad >= 1 ? string.Format(Inv, "; her end off at {0:0.00}", calm.EndStation) : sea.EndLoad >= 1 ? string.Format(Inv, "; in a seaway her end off at {0:0.00}", sea.EndStation) : ""));
    }

    /// <summary>The girder intact: is the section right? Its second moment against shipgen's, capacity over the design moment, and the worst load in a seaway.</summary>
    static void Intact(IReadOnlyList<string> designs, Func<string, Design> load, SimSettings s)
    {
        Console.WriteLine("design                         I, m4  shipgen's  cap/design   calm   seaway  worst at  ends, seaway");
        foreach (var d in designs.Count > 0 ? designs : ["destroyer", "v25", "lion", "iron_duke", "seydlitz", "battleship", "super_dreadnought", "yamato", "bismarck", "dreadnought"])
        {
            var shipped = ShipDesign.Build(load(d));
            var c = ShipClassCompiler.Compile(shipped);
            var h = new HullGirder(c);
            var gr = shipped.Report.Hull.Girder;
            var calm = h.Read(default, default, null, 0, 0, s);
            var sea = h.Read(default, default, null, 1, 0, s);
            double mid = h.YieldPa * (h.MidIM4 / (c.Vertical.Depth / 2)) / h.DesignNm;
            Console.WriteLine(string.Format(Inv, "{0,-28} {1,8:0.0} {2,10:0.0} {3,11:0.00} {4,6:0.00} {5,8:0.00} {6,9:0.00} {7,8:0.00} at {8:0.00}", d, h.MidIM4, gr is null ? double.NaN : gr.PlatingM4 + gr.ArmourDecksM4, mid, calm.Load, sea.Load, sea.Station, sea.EndLoad, sea.EndStation));
            if (designs.Count > 0)
            {
                Console.WriteLine(string.Format(Inv, "   plating: shell {0:0.0} mid, {1:0.0} end; strength deck {2:0.0} mid, {3:0.0} end; mid over x {4:0.0} to {5:0.0}; decks {6:0.0}, inner bottom {7:0.0}; L {8:0.0}", c.Plating.ShellMm, c.Plating.ShellEndMm, c.Plating.StrengthDeckMm, c.Plating.StrengthDeckEndMm, c.Plating.MidX0, c.Plating.MidX1, c.Plating.DeckMm, c.Plating.InnerBottomMm, c.Length));
                foreach (var at in new[] { 0.5, 0.24, 0.17 })
                {
                    Console.WriteLine(string.Format(Inv, "   the section at {0:0.00}:", at));
                    foreach (var e in h.Elements(at).GroupBy(e => (e.Kind, Math.Round(e.Z, 1))).OrderByDescending(gz => gz.Key.Item2))
                        Console.WriteLine(string.Format(Inv, "      {0,-13} z {1,6:0.0}  {2,6:0.000} m² ({3})", e.Key.Kind, e.Key.Item2, e.Sum(v => v.AreaM2), e.Count()));
                }
                foreach (var p in h.Profile())
                    Console.WriteLine(string.Format(Inv, "   at {0:0.00}: {1,3} elements, {2,6:0.00} m², z {3,6:0.0} to {4,6:0.0}, Z {5,7:0.00} m³, capacity {6,5:0.00} design moments", p.Station, p.Elements, p.AreaM2, p.Bottom, p.Top, p.ZM3, h.YieldPa * p.ZM3 / h.DesignNm));
            }
        }
    }

    static void Magazine(ShipClass c, SimSettings s, in GasKnobs g, string which, double flooded, string? doctrine, bool cells)
    {
        var net = new GasNetwork(c);
        var girder = new HullGirder(c);
        for (int room = 0; room < c.Rooms.Length; room++)
        {
            if (c.Rooms[room].Kind != RoomKind.Magazine || !(which == "all" || c.RoomId(room).Equals(which, StringComparison.OrdinalIgnoreCase)))
                continue;
            if (!Enumerable.Range(0, c.Mounts.Length).Any(m => c.Mounts[m].Magazine == room && c.Mounts[m].Battery >= 0))
                continue;
            var w0 = Ship(c, s, out var k0, out int slot0);
            k0.Flash = Magazines.Of(s, doctrine ?? s.DamageControlDefault);
            Flood(c, k0, room, flooded);
            var rng = RandomStream.Named(1, "explode bench");
            var old = Magazines.SetOff(c, k0, room, w0.Ships.Store.Combat.Mounts(slot0), s, 0, ref rng);

            var w = Ship(c, s, out var k, out int slot);
            net.Forget();
            k.Flash = k0.Flash;
            Flood(c, k, room, flooded);
            var mounts = w.Ships.Store.Combat.Mounts(slot).ToArray();
            double gas = 0;
            foreach (int ci in c.Links(c.Rooms[room].Cells))
                gas += Blast.GasM3(c, k, ci);
            var sw = Stopwatch.StartNew();
            var r = net.Burn(k, room, mounts, g, s);
            double ms = sw.Elapsed.TotalMilliseconds;
            Console.WriteLine(string.Format(Inv, "    {0} ({1:0} m³ of gas):", c.RoomId(room), gas));
            Console.WriteLine(string.Format(Inv, "      today:   {0}, peak {1:0} kPa, burned {2:0.0} t, girder lost {3:0.00} (load {4:0.00}), vented {5}, magazines {6}",
                old.Tier, old.PeakKPa, old.BurnedKg / 1000, old.D, old.GirderLoad, old.Vented, old.Magazines));
            Console.WriteLine(string.Format(Inv, "      network: peak {0:0} kPa, burned {1:0.0} t, {2} spaces ({3:0} m³), gave: {4} bulkheads, {5} decks, {6} doors, {7} sides, {8} weather deck, {9} bottom; vented {10}, lifted {11}, magazines {12}; first {13:0.00} s, the hull's {14:0.00} s, ran {15:0.0} s in {16} steps ({17:0.0} ms)",
                r.PeakKPa, r.BurnedKg / 1000, r.Spaces, r.VolumeM3, r.Bulkheads, r.Decks, r.Doors, r.Sides, r.WeatherDecks, r.Bottoms, r.Vented, r.Lifted, r.Magazines, r.FirstS, r.HullS, r.EndS, r.Steps, ms));
            Hull(c, girder, net, r.WhipEM, g, s);
            if (cells)
                Cells(c, net);
        }
    }

    static void Flood(ShipClass c, ShipCondition k, int room, double flooded)
    {
        if (flooded > 0)
            foreach (int cell in c.Links(c.Rooms[room].Cells))
                k.WaterM3[cell] = flooded * c.Cells[cell].VolumeM3 * Math.Clamp(c.Cells[cell].Permeability, 0.05, 1);
    }

    static void Cells(ShipClass c, GasNetwork net)
    {
        foreach (int i in net.Reached())
        {
            var b = c.Cells[i].Box;
            Console.WriteLine(string.Format(Inv, "        {0,-16} peak {1,6:0} kPa  x {2,6:0.0}..{3,6:0.0}  z {4,5:0.0}..{5,5:0.0}  {6:0} m³", c.CellId(i), net.PeakKPaOf(i), b.X0, b.X1, b.Base, b.Top, c.Cells[i].VolumeM3));
        }
    }
}
