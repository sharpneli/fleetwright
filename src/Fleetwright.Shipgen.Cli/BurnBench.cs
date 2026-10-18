using System.Globalization;
using Fleetwright.Sim.Combat;
using Fleetwright.Sim.Core;
using Fleetwright.Sim.Ships;

namespace Fleetwright.Shipgen.Cli;

/// <summary>`shipgen burn`: one space burning unfought (docs/battle/fire.md B0), through the game's own fire
/// (<see cref="Fires.Advance"/>): damage/17 §9 T2's spaces (fire_ref.py physics) with the reference's numbers beside
/// them, each run at a 1 s and a 15 s step (they must agree); then the rooms of a big space joining one by one; then T3 and
/// T4, one hose on a fire from a time on (B3), beside the reference.</summary>
static class BurnBench
{
    static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

    /// <summary>T2's spaces: name, use, m², load MJ/m², growth, doors, holes m², and fire_ref.py's flashover (min, NaN:
    /// none), peak MW and hours.</summary>
    static readonly (string Name, FireUse Use, double Area, double Q, double Growth, int Doors, double Holes, double Fo, double Peak, double Hours)[] T2 =
    [
        ("40 m2, 1943 load (240+10), door open", FireUse.Quarters, 40, 250, 0.7, 1, 0, double.NaN, 0.48, 0.4),
        ("40 m2, 1939 load, door open", FireUse.Quarters, 40, 480, 1.3, 1, 0, 4.3, 2.23, 3.1),
        ("40 m2, 1939 load, closed up", FireUse.Quarters, 40, 480, 1.3, 0, 0, double.NaN, 0.08, 0.6),
        ("40 m2, 1939 load, closed up + 1 m2 shell hole", FireUse.Quarters, 40, 480, 1.3, 0, 1, 4.1, 1.58, 4.4),
        ("40 m2, 1939 load, door + 3 m2 of holes", FireUse.Quarters, 40, 480, 1.3, 1, 3, 6.0, 5.95, 1.2),
        ("90 m2 room, 1890 cabin load, door open", FireUse.Quarters, 90, 930, 2.0, 1, 0, 4.5, 2.23, 6.0),
        ("90 m2 room, 1890 cabin load, door + 4 m2 of holes", FireUse.Quarters, 90, 930, 2.0, 1, 4, 6.1, 10.72, 2.9),
        ("90 m2 room, 1943 cabin load, door open", FireUse.Quarters, 90, 290, 0.7, 1, 0, 7.6, 2.23, 4.2),
        ("30 m2 stores, 1939, door open", FireUse.Stores, 30, 1600, 1.3, 1, 0, 2.8, 2.23, 6.0),
    ];

    const double HorizonS = 6 * 3600;

    public static int Run(string settingsPath)
    {
        var s = SimSettings.Load(settingsPath);
        Console.WriteLine("== T2. One space burning unfought (damage/17 §9; fire_ref.py beside: flashover min, peak MW, hours) ==");
        Console.WriteLine($"{"space",-48} {"load",5}  {"flashover",9} {"peak MW",8} {"hours",6}  {"phases",-28} | {"ref fo",6} {"peak",6} {"h",5} | {"15 s: fo",8} {"h",5}");
        foreach (var x in T2)
        {
            var row = Row(x.Use, x.Area, x.Q, x.Growth);
            double aoh = s.FireLeakAoh + x.Doors * s.FireDoorAoh + Math.Pow(x.Holes, 1.25);
            var a = Burn(row, aoh, x.Q, 1, s);
            var b = Burn(row, aoh, x.Q, 15, s);
            Console.WriteLine(string.Format(Inv, "{0,-48} {1,5:0}  {2,9} {3,8:0.00} {4,6:0.0}  {5,-28} | {6,6} {7,6:0.00} {8,5:0.0} | {9,8} {10,5:0.0}",
                x.Name, x.Q, Min(a.Fo), a.Peak, a.Hours, a.Phases, Min(x.Fo), x.Peak, x.Hours, Min(b.Fo), b.Hours));
        }

        Console.WriteLine();
        Console.WriteLine("== Rooms joining in a big space (one door, the outfit's room by use; involved m² at 10, 30 and 60 min) ==");
        Console.WriteLine($"{"space",-48} {"room",5}  {"flashover",9} {"10 min",7} {"30 min",7} {"60 min",7} {"MW at 60",9} {"hours",6}");
        foreach (var (name, use, area, q, growth) in new (string, FireUse, double, double, double)[]
                 {
                     ("300 m2 messdeck section, 1914 stripped (450)", FireUse.Quarters, 300, 450, 1.0),
                     ("120 m2 cabin flat, 1890 (930 + paint 30)", FireUse.Cabins, 120, 960, 2.0),
                     ("200 m2 weather deck, 1890 wooden (400)", FireUse.Deck, 200, 400, 2.0),
                     ("200 m2 weather deck, 1914 (150)", FireUse.Deck, 200, 150, 1.0),
                 })
        {
            var row = Row(use, area, q, growth) with { Kind = use == FireUse.Deck ? FireSpaceKind.Deck : FireSpaceKind.Cell };
            double aoh = row.OpenAir ? double.PositiveInfinity : s.FireLeakAoh + s.FireDoorAoh;
            var r = Burn(row, aoh, q, 5, s);
            Console.WriteLine(string.Format(Inv, "{0,-48} {1,5:0}  {2,9} {3,7:0} {4,7:0} {5,7:0} {6,9:0.00} {7,6:0.0}",
                name, Fires.Room(row, s), Min(r.Fo), r.At10, r.At30, r.At60, r.Mw60, r.Hours));
        }

        Console.WriteLine();
        Console.WriteLine("== T3. One hose on a 30 m2 (Shadwell-size) fire, 1939 load (fire_ref.py beside: control min after water, out min, water t) ==");
        Console.WriteLine($"{"nozzle, flow, water at",-34} {"control",8} {"out",6} {"flashed",8} {"water t",8} | {"ref",6} {"out",6} {"t",5}");
        foreach (var (nozzle, lpm, at, rc, ro, rw) in new (string, double, double, double, double, double)[]
                 {
                     ("fog", 360, 4, 1.1, 10.1, 2.2), ("jet", 360, 4, 3.2, 12.2, 2.9), ("fog", 360, 1, 0.1, 1.9, 0.3), ("fog", 360, 8, 1.5, 14.5, 2.3),
                     ("all-purpose", 250, 4, 3.1, 14.3, 2.6), ("jet", 150, 4, double.NaN, 154.9, 19.2), ("jet", 750, 4, 1.5, 7.9, 2.9),
                 })
        {
            var a = Attack(Row(FireUse.Quarters, 30, 480, 1.3), 480, Water(s, nozzle, lpm), at, s);
            Console.WriteLine(string.Format(Inv, "{0,-34} {1,8} {2,6} {3,8} {4,8:0.0} | {5,6} {6,6:0.0} {7,5:0.0}",
                $"{nozzle} {lpm:0} L/min at {at:0} min", Min(a.Control), Min(a.Out), a.Flashed, a.WaterT, Min(rc), ro, rw));
        }

        Console.WriteLine();
        Console.WriteLine("== T4. When the first water comes: 90 m2 1939 messdeck, all-purpose 250 L/min, one hose and three (fire_ref.py's out min beside) ==");
        Console.WriteLine($"{"water at",-10} {"1 hose",8} {"flashed",8} {"3 hoses",8} {"flashed",8} {"water t",8} | {"ref 1",6} {"ref 3",6}");
        foreach (var (at, r1, r3) in new (double, double, double)[]
                 {
                     (0.5, 3, 1), (1, 4, 2), (2, 7, 4), (3, 12, 6), (4, 16, 9), (6, double.NaN, 16), (8, double.NaN, 18), (10, double.NaN, 20),
                     (15, double.NaN, 25),
                 })
        {
            var one = Attack(Row(FireUse.Quarters, 90, 480, 1.3), 480, Water(s, "all-purpose", 250), at, s);
            var three = Attack(Row(FireUse.Quarters, 90, 480, 1.3), 480, Water(s, "all-purpose", 750), at, s);
            Console.WriteLine(string.Format(Inv, "{0,-10} {1,8} {2,8} {3,8} {4,8} {5,8:0.0} | {6,6} {7,6}",
                $"{at:0.#} min", Min(one.Out), one.Flashed, Min(three.Out), three.Flashed, three.WaterT, Min(r1), Min(r3)));
        }
        return 0;
    }

    /// <summary>A nozzle's water at a flow (the settings' FireNozzles).</summary>
    static FireWater Water(SimSettings s, string nozzle, double lpm)
    {
        var n = s.FireNozzles.Single(x => x.Name == nozzle);
        return new FireWater(lpm * n.FogShare, lpm, n.BoilShare);
    }

    record struct Attacked(double Control, double Out, bool Flashed, double WaterT);

    /// <summary>A fire lit by a 0.1 MW seat in a space with one door, a hose on it from a time on (fire_ref.py's
    /// compartment_attack): when it came under control after the water (min), when it was out (min from ignition; NaN
    /// not in 4 h), whether it flashed over, and the water left aboard (t).</summary>
    static Attacked Attack(in FireSpaceRow row, double q, FireWater hose, double atMin, SimSettings s)
    {
        const double dt = 2;
        var x = new BurnState { Phase = BurnPhase.Grow, Q = 0.1, Seat = 0.1, E0 = q * row.AreaM2, E = q * row.AreaM2 };
        double aoh = s.FireLeakAoh + s.FireDoorAoh, t = 0, control = double.NaN, water = 0;
        bool flashed = false;
        while (x.Burning && t < 4 * 3600)
        {
            var e = Fires.Advance(row, ref x, aoh, q, dt, s, false, t >= atMin * 60 ? hose : default, out double aboard);
            t += dt;
            water += aboard;
            flashed |= e == BurnEvent.Flashover;
            if (double.IsNaN(control) && x.Phase == BurnPhase.Control)
                control = t / 60 - atMin;
        }
        return new Attacked(control, x.Burning ? double.NaN : t / 60, flashed, water / 1000);
    }

    static FireSpaceRow Row(FireUse use, double area, double q, double growth) => new()
    {
        Kind = FireSpaceKind.Cell, Use = use, AreaM2 = area, HeightM = 2.6, LoadMJm2 = q, StrippedMJm2 = q, Growth = growth,
    };

    static string Min(double m) => double.IsNaN(m) ? "-" : m.ToString("0.0", Inv);

    record struct Result(double Fo, double Peak, double Hours, string Phases, double At10, double At30, double At60, double Mw60);

    /// <summary>A fire lit by a 0.1 MW seat (fire_ref.py's free burn) and left to burn: when it flashed over, its peak, how long it burned,
    /// the phases it went through, and the area involved at 10, 30 and 60 minutes.</summary>
    static Result Burn(in FireSpaceRow row, double aoh, double q, double dt, SimSettings s)
    {
        var x = new BurnState { Phase = BurnPhase.Grow, Q = 0.1, Seat = 0.1, E0 = q * row.AreaM2, E = q * row.AreaM2 };
        double fo = double.NaN, peak = 0, t = 0, at10 = 0, at30 = 0, at60 = 0, mw60 = 0;
        var phases = new List<BurnPhase> { BurnPhase.Grow };
        while (x.Burning && t < HorizonS)
        {
            var e = Fires.Advance(row, ref x, aoh, q, dt, s);
            t += dt;
            if (e == BurnEvent.Flashover && double.IsNaN(fo))
                fo = t / 60;
            peak = Math.Max(peak, x.Q);
            if (phases[^1] != x.Phase)
                phases.Add(x.Phase);
            if (t <= 600)
                at10 = x.Involved;
            if (t <= 1800)
                at30 = x.Involved;
            if (t <= 3600)
                (at60, mw60) = (x.Involved, x.Q);
        }
        return new Result(fo, peak, t / 3600, string.Join("/", phases.Select(p => p.ToString().ToLowerInvariant())), at10, at30, at60, mw60);
    }
}
