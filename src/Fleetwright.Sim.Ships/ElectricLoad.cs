using Fleetwright.Shipgen;

namespace Fleetwright.Sim.Ships;

/// <summary>A group of a ship's electric consumers (docs/battle/electrics.md EL3): how many, each one's draw at full load,
/// kW, the share of that it draws in battle, and whether only at night.</summary>
public readonly record struct ElectricConsumer(string Name, int Count, double Kw, double BattleShare, bool NightOnly = false)
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

    ElectricLoad(List<ElectricConsumer> consumers)
    {
        Consumers = consumers;
        foreach (var e in consumers)
        {
            ConnectedKw += e.ConnectedKw;
            BattleDayKw += e.NightOnly ? 0 : e.BattleKw;
            BattleNightKw += e.BattleKw;
        }
    }

    public static ElectricLoad Of(ShipClass c)
    {
        var list = new List<ElectricConsumer>();
        void Add(string name, int n, double kw, double share, bool night = false)
        {
            if (n > 0 && kw > 0)
                list.Add(new ElectricConsumer(name, n, kw, share, night));
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
                    barrels * (ElevateKwPerIn3DegS * inches * inches * inches * r.ElevateDegS + (RamKw + HoistKw) * perS), 0.5);
            else
                Add($"{mm:0} mm hoists", n * barrels, LightHoistKw * perS, 0.3);
        }

        double t0 = c.Hydrostatics.DisplacementT;
        Add("ventilation", 1, FansWPerT * t0 / 1000, 0.9);
        Add("lighting", 1, LampsWPerT * t0 / 1000, 0.7);
        foreach (ref readonly var l in c.Searchlights)
            Add("searchlight", 1, l.PowerKw, 1, night: true);
        if (c.Auxiliaries == AuxiliaryDrive.Electric)
        {
            double size = Math.Clamp(Math.Log(t0 / SteeringSmallT) / Math.Log(SteeringBigT / SteeringSmallT), 0, 1);
            if (c.Rudders.Length > 0)
                Add("steering", 1, SteeringKwSmall + (SteeringKwBig - SteeringKwSmall) * size, 0.4);
            Add("drainage and fire pumps", 1, c.Drainage.PumpsTph * 1000 / 3600 * 9.81 * PumpHeadM / PumpEfficiency / 1000, 0.5);
        }

        double computers = 0;
        foreach (ref readonly var b in c.Batteries)
            computers = Math.Max(computers, b.Fit.Computer.PowerKw);
        Add("fire-control table", 1, computers, 1);
        Add("directors", c.Directors.Length, DirectorKw, 1);
        Add("gyro and repeaters", 1, GyroKw, 1);
        Add("wireless", 1, WirelessKw, 0.3);
        return new ElectricLoad(list);
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
