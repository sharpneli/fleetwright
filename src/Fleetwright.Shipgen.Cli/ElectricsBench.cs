using System.Text;
using Fleetwright.Shipgen;
using Fleetwright.Sim.Ships;

namespace Fleetwright.Shipgen.Cli;

/// <summary><c>shipgen electrics DESIGN...</c> (docs/battle/electrics.md EL3): what a ship's plant feeds, each consumer's
/// connected and battle kW, beside the plant her namesake carried where the research has it (a check, not a target:
/// research/simulation/electric-plant.md §1, its bands in "Checks").</summary>
static class ElectricsBench
{
    static readonly Dictionary<string, double> Recorded = new()
    {
        // Seydlitz's own isn't recorded: the Moltke class's, her nearest
        ["dreadnought"] = 350, ["invincible"] = 800, ["tiger_1914"] = 750, ["seydlitz"] = 1200, ["derfflinger"] = 1660,
        ["nassau"] = 1280, ["borodino"] = 738, ["kgv"] = 2640, ["bismarck"] = 7910, ["yamato"] = 4800, ["richelieu"] = 9000,
    };

    public static int Run(IEnumerable<string> designs, Func<string, Design> load)
    {
        var sb = new StringBuilder();
        foreach (string name in designs)
        {
            var c = ShipClassCompiler.Compile(ShipDesign.Build(load(name)));
            var e = c.Electric;
            string id = Path.GetFileNameWithoutExtension(name);
            sb.AppendLine($"{id}: {c.Hydrostatics.DisplacementT:0} t; gunhouses {c.TurretDrive}, auxiliaries {c.Auxiliaries}");
            sb.AppendLine($"  {"consumer",-34} {"n",3} {"kW each",8} {"connected",10} {"battle",7}");
            foreach (var u in e.Consumers.Where(u => u.Name != "searchlight"))
                sb.AppendLine($"  {u.Name,-34} {u.Count,3} {u.Kw,8:0.0} {u.ConnectedKw,10:0} {u.BattleKw,7:0}");
            var lights = e.Consumers.Where(u => u.Name == "searchlight").ToArray();
            if (lights.Length > 0)
                sb.AppendLine($"  {"searchlights (night only)",-34} {lights.Length,3} {lights.Average(u => u.Kw),8:0.0} {lights.Sum(u => u.ConnectedKw),10:0} {lights.Sum(u => u.BattleKw),7:0}");
            sb.AppendLine($"  connected {e.ConnectedKw:0} kW, battle {e.BattleDayKw:0} by day, {e.BattleNightKw:0} at night; {1000 * e.BattleDayKw / c.Hydrostatics.DisplacementT:0} W/t");
            sb.Append($"  plant {c.PlantKw:0} kW ({1000 * c.PlantKw / c.Hydrostatics.DisplacementT:0} W/t), {e.Sets.Count(g => !g.Diesel)} steam and {e.Sets.Count(g => g.Diesel)} diesel sets, battle/plant {e.BattleDayKw / Math.Max(c.PlantKw, 1):0.00}");
            if (Recorded.TryGetValue(id, out double plant))
                sb.Append($"; her namesake's plant {plant:0} kW");
            sb.AppendLine();
            sb.AppendLine();
        }
        Console.Write(sb);
        return 0;
    }
}
