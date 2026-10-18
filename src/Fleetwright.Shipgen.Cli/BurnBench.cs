using System.Globalization;
using Fleetwright.Sim.Combat;
using Fleetwright.Sim.Core;
using Fleetwright.Sim.Ships;

namespace Fleetwright.Shipgen.Cli;

/// <summary>`shipgen burn`: one space burning unfought (docs/battle/fire.md B0), through the game's own fire
/// (<see cref="Fires.Advance"/>): damage/17 §9 T2's spaces (fire_ref.py physics) with the reference's numbers beside
/// them, each run at a 1 s and a 15 s step (they must agree); then the rooms of a big space joining one by one.</summary>
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
        return 0;
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
