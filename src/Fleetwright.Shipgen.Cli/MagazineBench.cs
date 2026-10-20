using System.Globalization;
using Fleetwright.Sim.Core;
using Fleetwright.Sim.Ships;

namespace Fleetwright.Shipgen.Cli;

/// <summary><c>shipgen magazine</c>: the magazines alone (docs/battle/magazines.md V0): each design's magazines (what they
/// hold, where they lie against the waterline, the mounts they feed) and each mount's ammunition train, the stations a
/// flash runs along from the gunhouse down to the magazine.</summary>
static class MagazineBench
{
    static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

    public static int Run(IReadOnlyList<string> designs, Func<string, Design> load, string settingsPath)
    {
        var settings = SimSettings.Load(settingsPath);
        foreach (var name in designs.Count > 0 ? designs : ["iron_duke"])
            List(ShipClassCompiler.Compile(ShipDesign.Build(load(name))), settings);
        return 0;
    }

    static void List(ShipClass c, SimSettings s)
    {
        double wl = c.Vertical.Waterline;
        Console.WriteLine($"{c.Name}:");
        for (int room = 0; room < c.Rooms.Length; room++)
        {
            ref readonly var rr = ref c.Rooms[room];
            if (rr.Kind != RoomKind.Magazine)
                continue;
            var fed = Enumerable.Range(0, c.Mounts.Length).Where(m => c.Mounts[m].Magazine == room).Select(m => c.ComponentId(c.Mounts[m].Component)).ToList();
            Console.WriteLine(string.Format(Inv, "  {0}: {1:0.#} t in {2:0} m³, x {3:0.0} to {4:0.0} m, {5:+0.0;-0.0} to {6:+0.0;-0.0} m on the waterline; feeds {7}",
                c.RoomId(room), rr.Tonnes, rr.VolumeM3, rr.X0, rr.X1, rr.Base - wl, rr.Top - wl, fed.Count == 0 ? "nothing" : string.Join(", ", fed)));
        }
        for (int m = 0; m < c.Mounts.Length; m++)
        {
            var train = c.Train(m);
            if (train.Length == 0)
                continue;
            ref readonly var mr = ref c.Mounts[m];
            string housing = c.Enclosed(m) ? "gunhouse" : mr.Casemate ? "casemate" : "shield";
            var stations = new List<string>();
            foreach (var st in train)
                stations.Add($"{Station(st.Kind)} ({(st.Part >= 0 ? c.ComponentId(st.Part) : c.RoomId(st.Room))})");
            Console.WriteLine(string.Format(Inv, "  {0} ({1}, {2:0} mm, {3}): {4}", c.ComponentId(mr.Component), mr.Kind, mr.CalibreMm, housing,
                string.Join(" > ", stations)));
        }
    }

    static string Station(StationKind k) => k switch
    {
        StationKind.Gunhouse => "gunhouse",
        StationKind.Chamber => "working chamber",
        StationKind.Casemate => "casemate",
        StationKind.Handling => "handling room",
        _ => "magazine",
    };
}
