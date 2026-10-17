using System.Diagnostics;
using System.Globalization;
using Fleetwright.Sim;
using Fleetwright.Sim.Combat;
using Fleetwright.Sim.Core;
using Fleetwright.Sim.Ships;

namespace Fleetwright.Shipgen.Cli;

/// <summary>`shipgen burst`: a burst's gas in a closed space alone (docs/battle/shells-armour-damage.md E5; damage/11 §4.2's
/// cases on our ships): a charge of a filler set off in a room of a design, the cascade over its cell graph (what
/// ruptured, the doors blown, what deformed, the spaces reached and what each felt) and the rooms and parts the gas put
/// out of action. With no charge given, 11's cases: a destroyer's compartment under a 5" AAC, an 8" HC, a 46 cm Type 0
/// and a 250 kg bomb; her engine room under a 14" HC; a battleship's engine room and a 150 m³ space under a 16" AP; black
/// powder against TNT.</summary>
static class BurstBench
{
    static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

    public record Options(string? Room, double FillerKg, string Explosive, double CalibreMm);

    public static int Run(string design, Func<string, Design> load, string settingsPath, Options o)
    {
        var settings = SimSettings.Load(settingsPath);
        if (o.FillerKg > 0)
            return One(load(design), settings, o.Room ?? "accommodation", o.FillerKg, o.Explosive, o.CalibreMm) ? 0 : 1;
        Console.WriteLine("damage/11 §4.2's cases (W the filler's kg; P the gas's first pressure, felt what the burst space's boundaries felt):");
        One(load("destroyer"), settings, "accommodation", 3.3, "Explosive D", 127, "5\" AAC in a destroyer's compartment (WDR 51: shell blast minor)");
        One(load("destroyer"), settings, "accommodation", 9.7, "Explosive D", 203, "8\" HC, the same (Ralph Talbot: compartments wrecked, doors blown)");
        One(load("destroyer"), settings, "engine_room", 25, "TNT", 356, "14\" HC in a destroyer's engine room (S. B. Roberts)");
        One(load("destroyer"), settings, "engine_room", 61.7, "TNA", 460, "46 cm Type 0 HE, the same (08: the compartment and often the next)");
        One(load("destroyer"), settings, "accommodation", 60, "TNT", 300, "250 kg SAP bomb (WDR 51: several decks and bulkheads, little beyond ~6 m)");
        One(load("battleship"), settings, "engine_room", 18.6, "Explosive D", 406, "16\" AP Mk 8 in a battleship's engine room (Scharnhorst's boiler room)");
        One(load("battleship"), settings, "accommodation", 18.6, "Explosive D", 406, "16\" AP Mk 8 in a battleship's accommodation");
        One(load("super_dreadnought"), settings, "accommodation", 58.6, "Black powder", 381, "15\" CPC, black powder (Baden: the model leaves the deck)");
        One(load("super_dreadnought"), settings, "accommodation", 58.6, "TNT", 381, "the same shell TNT-filled");
        return 0;
    }

    /// <summary>One charge in the room of a kind nearest amidships (ROOM, or ROOM:n the nth from it).</summary>
    static bool One(Design design, SimSettings settings, string room, double fillerKg, string explosive, double calibreMm, string? title = null)
    {
        var c = ShipClassCompiler.Compile(ShipDesign.Build(design));
        var x = Explosives.Get(explosive) ?? throw new ArgumentException($"no explosive \"{explosive}\"");
        var shell = new ShellTypeRow
        {
            CalibreMm = calibreMm, FillerKg = fillerKg, FillerRe = x.Re ?? 1, FillerQDet = x.QDet ?? 4.5, FillerQComb = x.QComb ?? 15,
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
            return false;
        }
        int r0 = rooms[nth], cell = -1;
        foreach (int ci in c.Links(c.Rooms[r0].Cells))
            if (cell < 0 || c.Cells[ci].VolumeM3 > c.Cells[cell].VolumeM3)
                cell = ci;   // its largest cell
        var b = c.Cells[cell].Box;
        var at = new P3((b.X0 + b.X1) / 2, (b.Y0 + b.Y1) / 2, (b.Base + b.Top) / 2);

        var w = new World(WorldTime.FromUtc(1916, 5, 31, 14), 1, settings);
        int cls = w.Ships.AddClass(c);
        w.Apply(ShipSystem.Spawn(cls, 56, 4, 0, 0));
        var warm = w.Ships.Store.Combat.Condition(w.Ships.Store.SlotOf(w.Ships.NewestEntity));
        w.Apply(ShipSystem.Spawn(cls, 56.1, 4, 0, 0));
        var k = w.Ships.Store.Combat.Condition(w.Ships.Store.SlotOf(w.Ships.NewestEntity));
        double size = Math.Cbrt(fillerKg * shell.FillerRe), rw = settings.WreckRadiusK * size;
        var rng = RandomStream.Named(1, "burst bench");
        double gas = Blast.GasM3(c, k, cell), vents = Blast.VentM2(c, k, cell, settings);
        Blast.Prepare(c, k);
        var rw0 = RandomStream.Named(2, "burst bench");
        Blast.Cascade(c, warm, cell, at, fillerKg, shell, 0, settings.WreckRadiusK * Math.Cbrt(fillerKg * shell.FillerRe),
            settings.NearHoleK * Math.Cbrt(fillerKg * shell.FillerRe), settings, ref rw0);   // a first one on another ship: the timing below is a burst's own
        var sw = Stopwatch.StartNew();
        var res = Blast.Cascade(c, k, cell, at, fillerKg, shell, 0, rw, settings.NearHoleK * size, settings, ref rng);
        double us = sw.Elapsed.TotalMilliseconds * 1000;

        if (title != null)
            Console.WriteLine($"\n{title}");
        Console.WriteLine(string.Format(Inv, "  {0}: {1:0.#} kg {2} in {3} ({4}, {5:0} m³ of gas, vents {6:0.00} m²), the near zone {7:0.0} m",
            c.Name, fillerKg, explosive, c.RoomId(r0), c.CellId(cell), gas, vents, rw));
        Console.WriteLine(string.Format(Inv, "  P {0:0.000} MPa, felt {1:0.000}{2}; {3} round(s), {4} space(s) reached ({5:0} m³), at the end P {6:0.000}",
            res.FirstMPa, res.FeltMPa, res.Open ? " (open)" : "", res.Rounds, res.Spaces, res.VolumeM3, res.LastMPa));
        Console.WriteLine(string.Format(Inv, "  ruptured {0}, doors blown {1}, deformed {2}; opened between spaces {3:0.00} m², sprung {4:0.000} m², to the sea or air {5:0.000} m² ({6:0} µs)",
            res.Ruptured, res.Doors, res.Deformed, res.OpenM2, res.LeakM2, res.HoleM2, us));
        for (int i = 0; i < res.Spaces; i++)
        {
            int ci = k.BlastRegion[i];
            var bx = c.Cells[ci].Box;
            string what = string.Join(", ", RoomsOf(c, ci).Select(r => $"{c.RoomId(r)} {c.Rooms[r].Kind}"));
            double felt = k.BlastFelt[ci];
            string out_ = string.Join(", ", RoomsOf(c, ci).Where(r => felt >= settings.RoomBlastMPa[Math.Min((int)c.Rooms[r].Kind, settings.RoomBlastMPa.Length - 1)])
                .Select(r => c.Rooms[r].Kind.ToString()));
            Console.WriteLine(string.Format(Inv, "    {0,-14} felt {1:0.000} MPa  x {2,6:0.0}..{3,6:0.0}  z {4,5:0.0}..{5,5:0.0}  {6}{7}", c.CellId(ci), felt, bx.X0, bx.X1,
                bx.Base, bx.Top, what, out_.Length > 0 ? "  [out: " + out_ + "]" : ""));
        }
        return true;
    }

    static IEnumerable<int> RoomsOf(ShipClass c, int cell)
    {
        var list = new List<int>();
        foreach (int r in c.RoomsIn(cell))
            list.Add(r);
        return list;
    }
}
