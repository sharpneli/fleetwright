using Fleetwright.Shipgen;

namespace Fleetwright.Sim.Ships;

/// <summary>What's shed first when her plant can't carry her load (docs/battle/electrics.md EL7; electric-plant.md's order [EST]):
/// searchlights, then pumps not fighting damage, then what works the guns; last what's vital, steering, fire control and lights,
/// which go only with the whole plant.</summary>
public enum Shedding : byte
{
    None,
    Searchlights,
    Pumps,
    Guns,
}

/// <summary>A generating set (docs/battle/electrics.md EL7): its kW, on diesel or steam, the space it stands in (a cell) and that
/// space's room node (-1: none of its own).</summary>
public readonly record struct GeneratorSet(double Kw, bool Diesel, int Cell, int RoomNode);

/// <summary>A group of a ship's electric consumers (docs/battle/electrics.md EL3): how many, each one's draw at full load,
/// kW, the share of that it draws in battle, what it's shed with (<see cref="Shedding.None"/>: vital), and whether only at night.</summary>
public readonly record struct ElectricConsumer(string Name, int Count, double Kw, double BattleShare, Shedding Shed = Shedding.None, bool NightOnly = false)
{
    public double ConnectedKw => Count * Kw;
    public double BattleKw => ConnectedKw * BattleShare;
}

/// <summary>What a ship's plant feeds, from what she carries and how it's driven, never her year (docs/battle/electrics.md
/// EL3; research/simulation/electric-plant.md §3.10). What runs on steam or by hand isn't in it. Rough on purpose: the fits
/// below are [EST] from a few ratings, and total kW is a stand-in for how electrified she is (electrics.md "kW is an
/// approximation"). Left out: what draws nothing in battle (cranes, winches, galley), so the connected figure runs under
/// the period's.</summary>
public sealed class ElectricLoad
{
    // research §3.1: training on one line through the motors of 1914-45, the fit to elevation per gun, in inches
    const double TrainKwPerTDegS = 0.035, ElevateKwPerIn3DegS = 0.0013;
    // [EST] fitted here to §3.10's rammers (12-in 6-9 kW, 16-in 1940 54) and hoists (12-in 27-50, 14-in 36, 16-in 1940 160),
    // and a casemate's 3-5 kW a gun: kW for a shell's kg over its reload's seconds
    const double RamKw = 1.1, HoistKw = 3.5, LightHoistKw = 0.6;
    // [EST] §3.5 and §3.8: fans 2.5-8.5 W/t, lamps 1-5 W/t; steering §3.7, 30 kW a destroyer to 100 a battleship
    const double FansWPerT = 5, LampsWPerT = 2.5, SteeringKwSmall = 30, SteeringKwBig = 100, SteeringSmallT = 2000, SteeringBigT = 40000;
    // [EST] §3.6's ρgQH/η, on 12 m of head at η 0.55; §3.3's director, the gyro and its repeaters, wireless
    const double PumpHeadM = 12, PumpEfficiency = 0.55, DirectorKw = 0.5, GyroKw = 0.7, WirelessKw = 5;

    public IReadOnlyList<ElectricConsumer> Consumers { get; }
    public double ConnectedKw { get; }
    /// <summary>The battle load by day, and at night with her searchlights burning, kW.</summary>
    public double BattleDayKw { get; }
    public double BattleNightKw { get; }
    /// <summary>Her generating sets, steam in the engine rooms and diesels outside the machinery, fore and aft (docs/battle/electrics.md
    /// EL7). Placed by this rule until the designer places them.</summary>
    public GeneratorSet[] Sets { get; }
    readonly double[] shed = new double[4];

    ElectricLoad(List<ElectricConsumer> consumers, GeneratorSet[] sets)
    {
        (Consumers, Sets) = (consumers, sets);
        foreach (var e in consumers)
        {
            ConnectedKw += e.ConnectedKw;
            BattleDayKw += e.NightOnly ? 0 : e.BattleKw;
            BattleNightKw += e.BattleKw;
            shed[(int)e.Shed] += e.BattleKw;
        }
    }

    /// <summary>What she sheds with this supply, kW, her searchlights counted as burning.</summary>
    public Shedding ShedAt(double supplyKw)
    {
        double vital = shed[(int)Shedding.None], guns = vital + shed[(int)Shedding.Guns], pumps = guns + shed[(int)Shedding.Pumps];
        return supplyKw >= pumps + shed[(int)Shedding.Searchlights] ? Shedding.None
            : supplyKw >= pumps ? Shedding.Searchlights : supplyKw >= guns ? Shedding.Pumps : Shedding.Guns;
    }

    public static ElectricLoad Of(ShipClass c)
    {
        var list = new List<ElectricConsumer>();
        void Add(string name, int n, double kw, double share, Shedding shed = Shedding.None, bool night = false)
        {
            if (n > 0 && kw > 0)
                list.Add(new ElectricConsumer(name, n, kw, share, shed, night));
        }

        // the guns, a line for each kind of mount: what's in a gunhouse on the plant, or a hand-worked gun's hoist
        var kinds = new SortedDictionary<(double Mm, int Barrels, bool Power, double T), int>();
        for (int m = 0; m < c.Mounts.Length; m++)
        {
            ref readonly var r = ref c.Mounts[m];
            if (r.Battery < 0 || !c.Enclosed(m) && r.Kind == ComponentKind.Torpedo || c.Drive(m) == TurretDrive.SteamHydraulic)
                continue;
            var key = (r.CalibreMm, c.TurretTypes[r.Type].Barrels, c.OnElectric(m), r.RevolvingT);
            kinds[key] = kinds.GetValueOrDefault(key) + 1;
        }
        foreach (var ((mm, barrels, power, t), n) in kinds)
        {
            int m = FirstOf(c, mm, barrels, power, t);
            ref readonly var r = ref c.Mounts[m];
            double perS = ShellKg(c, r.Battery) / Math.Max(r.ReloadS, 0.5), inches = mm / 25.4;
            if (power)
                Add($"{mm:0} mm gunhouse, {barrels} guns, {t:0} t", n, TrainKwPerTDegS * t * r.TrainDegS +
                    barrels * (ElevateKwPerIn3DegS * inches * inches * inches * r.ElevateDegS + (RamKw + HoistKw) * perS), 0.5, Shedding.Guns);
            else
                Add($"{mm:0} mm hoists", n * barrels, LightHoistKw * perS, 0.3, Shedding.Guns);
        }

        double t0 = c.Hydrostatics.DisplacementT;
        Add("ventilation", 1, FansWPerT * t0 / 1000, 0.9);
        Add("lighting", 1, LampsWPerT * t0 / 1000, 0.7);
        foreach (ref readonly var l in c.Searchlights)
            Add("searchlight", 1, l.PowerKw, 1, Shedding.Searchlights, night: true);
        if (c.Auxiliaries == AuxiliaryDrive.Electric)
        {
            double size = Math.Clamp(Math.Log(t0 / SteeringSmallT) / Math.Log(SteeringBigT / SteeringSmallT), 0, 1);
            if (c.Rudders.Length > 0)
                Add("steering", 1, SteeringKwSmall + (SteeringKwBig - SteeringKwSmall) * size, 0.4);
            Add("drainage and fire pumps", 1, c.Drainage.PumpsTph * 1000 / 3600 * 9.81 * PumpHeadM / PumpEfficiency / 1000, 0.5, Shedding.Pumps);
        }

        double computers = 0;
        foreach (ref readonly var b in c.Batteries)
            computers = Math.Max(computers, b.Fit.Computer.PowerKw);
        Add("fire-control table", 1, computers, 1);
        Add("directors", c.Directors.Length, DirectorKw, 1);
        Add("gyro and repeaters", 1, GyroKw, 1);
        Add("wireless", 1, WirelessKw, 0.3);
        return new ElectricLoad(list, SetsOf(c));
    }

    /// <summary>Her sets: about 250 kW each, 2 to 10 (the recorded plants' 3-8 and 6-14) [EST]. The steam sets a room each in turn along
    /// her engine rooms (Iowa's pair a room), or boiler rooms if she has none; the diesels in turn aft and forward of the machinery, low
    /// on the centreline, in stores or quarters (Seydlitz's groups, Bismarck's four plants).</summary>
    static GeneratorSet[] SetsOf(ShipClass c)
    {
        int n = Math.Clamp((int)Math.Round(c.PlantKw / 250), 2, 10);
        int diesels = c.DieselShare > 0 ? Math.Clamp((int)Math.Round(n * c.DieselShare), 1, n) : 0, steam = n - diesels;
        var rooms = new List<int>();
        foreach (var kind in (ReadOnlySpan<RoomKind>)[RoomKind.EngineRoom, RoomKind.BoilerRoom])
        {
            for (int r = 0; r < c.Rooms.Length; r++)
                if (c.Rooms[r].Kind == kind && c.Links(c.Rooms[r].Cells).Length > 0)
                    rooms.Add(r);
            if (rooms.Count > 0)
                break;
        }
        double x0 = double.MaxValue, x1 = double.MinValue;
        foreach (int r in rooms)
            (x0, x1) = (Math.Min(x0, c.Rooms[r].X0), Math.Max(x1, c.Rooms[r].X1));
        var sets = new GeneratorSet[n];
        for (int i = 0; i < n; i++)
        {
            if (i < steam && rooms.Count > 0)
            {
                int r = rooms[i % rooms.Count];
                sets[i] = new GeneratorSet(c.PlantKw * (1 - c.DieselShare) / Math.Max(steam, 1), false, c.Links(c.Rooms[r].Cells)[0], c.RoomNode(r));
            }
            else
            {
                int cell = OutsideCell(c, (i - steam) % 2 == 0 ? x0 : x1);
                double kw = diesels > 0 ? c.PlantKw * c.DieselShare / diesels : c.PlantKw / n;
                sets[i] = new GeneratorSet(kw, diesels > 0, cell, cell >= 0 && c.Cells[cell].Room >= 0 ? c.RoomNode(c.Cells[cell].Room) : -1);
            }
        }
        return sets;
    }

    /// <summary>A space for a diesel set: stores or quarters nearest a station, low and near the centreline.</summary>
    static int OutsideCell(ShipClass c, double x)
    {
        int best = -1;
        double score = double.MaxValue;
        for (int i = 0; i < c.Cells.Length; i++)
        {
            ref readonly var cell = ref c.Cells[i];
            if (cell.Room >= 0 && c.Rooms[cell.Room].Kind is not (RoomKind.Stores or RoomKind.Accommodation))
                continue;
            var b = cell.Box;
            double s = Math.Abs((b.X0 + b.X1) / 2 - x) + Math.Abs((b.Y0 + b.Y1) / 2) + 3 * (b.Base - c.Vertical.Keel);
            if (s < score)
                (score, best) = (s, i);
        }
        return best;
    }

    static int FirstOf(ShipClass c, double mm, int barrels, bool power, double t)
    {
        for (int m = 0; m < c.Mounts.Length; m++)
        {
            ref readonly var r = ref c.Mounts[m];
            if (r.Battery >= 0 && r.CalibreMm == mm && c.TurretTypes[r.Type].Barrels == barrels && c.OnElectric(m) == power && r.RevolvingT == t)
                return m;
        }
        return 0;
    }

    /// <summary>The heaviest shell a battery carries, kg.</summary>
    static double ShellKg(ShipClass c, int battery)
    {
        double kg = 0;
        foreach (var l in c.Loadout.Loads(battery))
            kg = Math.Max(kg, c.Loadout.ShellTypes[l.ShellType].MassKg);
        return kg;
    }
}
