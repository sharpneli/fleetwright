using Fleetwright.Sim.Ships;

namespace Fleetwright.Sim.Tests;

/// <summary>The weapon facts (docs/battle/fighting.md 4.0): every gun mount in a battery with shells to fire, every
/// number a gun needs filled, the placeholders near the research's sourced guns, and a ship with no fire control at
/// all still a ship that fights (every battery in local control).</summary>
public class WeaponFactsTests
{
    [Fact]
    public void Every_golden_build_has_its_weapon_facts()
    {
        var problems = new List<string>();
        var shared = new Dictionary<string, ShellTypeRow>(StringComparer.Ordinal);
        foreach (var b in Fleet.All.Where(b => b.Class != null))
            foreach (var p in Problems(b.Class!, shared))
                problems.Add($"{b.Case.Name}: {p}");
        Assert.True(problems.Count == 0, string.Join("\n", problems.Take(20)));
        Assert.True(shared.Count > 50, $"{shared.Count} shell types over the fleet");
    }

    static List<string> Problems(ShipClass c, Dictionary<string, ShellTypeRow> shared)
    {
        var p = new List<string>();
        static bool Pos(double v) => double.IsFinite(v) && v > 0;
        for (int i = 0; i < c.Mounts.Length; i++)
        {
            var m = c.Mounts[i];
            if (m.Kind == ComponentKind.Torpedo)
            {
                if (m.Battery != -1)
                    p.Add($"torpedo mount {i} is in battery {m.Battery}");
                continue;
            }
            if (m.Battery < 0 || m.Battery >= c.Batteries.Length || !c.Links(c.Batteries[m.Battery].Mounts).Contains(i))
                p.Add($"gun mount {i} isn't in its battery {m.Battery}");
            if (!Pos(m.ReloadS) || !Pos(m.TrainDegS) || !Pos(m.ElevateDegS) || !(m.MaxElevationDeg is > 5 and <= 90))
                p.Add($"gun mount {i}: reload {m.ReloadS}, train {m.TrainDegS}, elevate {m.ElevateDegS}, max {m.MaxElevationDeg}");
        }
        int inBatteries = 0;
        for (int b = 0; b < c.Batteries.Length; b++)
        {
            var row = c.Batteries[b];
            inBatteries += row.Mounts.Count;
            if (row.Kind is not (Battery.Main or Battery.Secondary))
                p.Add($"battery {b} is a {row.Kind} battery");
            int guns = 0;
            foreach (int m in c.Links(row.Mounts))
            {
                guns += c.TurretTypes[c.Mounts[m].Type].Barrels;
                if (c.Mounts[m].Battery != b || c.Mounts[m].CalibreMm != row.CalibreMm || c.Mounts[m].CalibreLength != row.CalibreLength)
                    p.Add($"battery {b} holds mount {m} of another gun or battery");
            }
            if (guns != row.Guns || guns == 0)
                p.Add($"battery {b}: {row.Guns} guns, the mounts have {guns}");
            foreach (int d in c.Links(row.Directors))
                if (c.Directors[d].Battery != row.Kind)
                    p.Add($"battery {b} ({row.Kind}) is directed by a {c.Directors[d].Battery} director");
            if (row.Local != (row.Directors.Count == 0) || row.Local && row.Level != FireControlLevel.Eyeball)
                p.Add($"battery {b}: local {row.Local} at level {row.Level}");
            double share = 0;
            bool fires = false;
            foreach (var l in c.ShellLoads(row.Shells))
            {
                share += l.Share;
                fires |= l.ShellType == row.Fires;
                if (c.ShellTypes[l.ShellType].CalibreMm != row.CalibreMm)
                    p.Add($"battery {b} carries a {c.ShellTypes[l.ShellType].CalibreMm} mm shell");
            }
            if (Math.Abs(share - 1) > 1e-9 || !fires)
                p.Add($"battery {b}: shares add to {share}, fires a shell it {(fires ? "carries" : "doesn't carry")}");
        }
        if (inBatteries != c.Mounts.ToArray().Count(m => m.Kind != ComponentKind.Torpedo))
            p.Add($"{inBatteries} mounts in batteries");

        for (int t = 0; t < c.ShellTypes.Length; t++)
        {
            var s = c.ShellTypes[t];
            if (!Pos(s.MassKg) || !Pos(s.MuzzleVelocityMs) || !Pos(s.FormFactor) || !Pos(s.FillerKg) || !(s.FuzeDelayS >= 0)
                || s.FillerKg > 0.2 * s.MassKg || (s.Fuze == Fuze.Nose) != (s.Kind == ShellKind.He))
                p.Add($"shell type {c.ShellTypeId(t)}: {s}");
            // shared by id: one id, one row, whichever class it's in
            if (shared.TryGetValue(c.ShellTypeId(t), out var other) && other != s)
                p.Add($"shell type {c.ShellTypeId(t)} differs from another class's");
            else
                shared[c.ShellTypeId(t)] = s;
        }

        var pl = c.Plating;
        if (!Pos(pl.ShellMm) || !Pos(pl.ShellEndMm) || !Pos(pl.StrengthDeckMm) || !(pl.MidX0 < pl.MidX1) || !Pos(pl.SuperstructureMm))
            p.Add($"plating {pl}");
        return p;
    }

    /// <summary>The placeholders against the research's guns of the Great War (fire_control_ref.py GUNS, and the QF
    /// 4"/45 Mk V from navweaps: 14.06 kg HE at 716 m/s): AP mass and muzzle velocity within 10%, and the mount's
    /// numbers in the research's ranges.</summary>
    [Theory]
    [InlineData(305, 45, 386, 831)]    // 12"/45 Mk X
    [InlineData(343, 45, 635, 757)]    // 13.5"/45 Mk V(H)
    [InlineData(381, 42, 871, 749)]    // 15"/42 Mk I
    [InlineData(102, 45, 14.06, 716)]  // 4"/45 QF Mk V
    public void Placeholders_land_near_the_research_guns(double mm, double cal, double kg, double ms)
    {
        var s = DesignerPlaceholders.Shell(ShellKind.Ap, mm, cal, Era.GreatWar, "portsmouth");
        Assert.InRange(s.MassKg / kg, 0.9, 1.1);
        Assert.InRange(s.MuzzleVelocityMs / ms, 0.9, 1.1);
        Assert.Equal(ShellQuality.Rn1916, s.Quality);

        // reload: the 4"'s 6-10 rounds a minute, the heavy guns' 1-2; training 2-3 °/s for the big turrets
        var (reload, train, _, maxElev) = DesignerPlaceholders.Gun(mm, cal, TurretShape.Bb, Era.GreatWar);
        Assert.InRange(reload, mm < 200 ? 6 : 30, mm < 200 ? 10 : 60);
        if (mm > 300)
            Assert.InRange(train, 1.5, 3.5);
        Assert.InRange(maxElev, 13.5, 20);
    }

    [Fact]
    public void A_ship_with_no_fire_control_fights_by_eye()
    {
        foreach (var name in new[] { "doom", "devastation", "duilio", "inflexible" })
        {
            var c = Fleet.Get(name).Class!;
            Assert.NotEmpty(c.Batteries.ToArray());
            foreach (var b in c.Batteries)
            {
                Assert.True(b.Local, $"{name}: a {b.CalibreMm} mm battery with directors");
                Assert.Equal(FireControlLevel.Eyeball, b.Level);
            }
        }
    }

    [Fact]
    public void Batteries_follow_the_guns_and_the_fit()
    {
        // KGV's quad and twin 14" turrets are one battery; two secondary guns are four batteries, a side each, each side's
        // on its own side's director
        var kgv = Fleet.Get("kgv").Class!;
        Assert.Equal(1, kgv.Batteries.ToArray().Count(b => b.Kind == Battery.Main));
        Assert.Equal(10, kgv.Batteries[0].Guns);
        var bismarck = Fleet.Get("bismarck").Class!;
        var secs = bismarck.Batteries.ToArray().Where(b => b.Kind == Battery.Secondary).ToArray();
        Assert.Equal(4, secs.Length);
        Assert.Equal(2, secs.Count(b => b.Side == Side.Port));
        Assert.Equal(2, secs.Count(b => b.Side == Side.Starboard));
        foreach (var b in secs)
        {
            var dirs = bismarck.Links(b.Directors).ToArray();
            Assert.Single(dirs);
            Assert.EndsWith(b.Side == Side.Port ? "P" : "S", bismarck.ComponentId(bismarck.Directors[dirs[0]].Component));
            foreach (int m in bismarck.Links(b.Mounts))
                Assert.Equal(b.Side == Side.Port, bismarck.Mounts[m].Y < 0);
        }
        // a centreline secondary is a third, its own
        Assert.Contains(Fleet.Get("murica").Class!.Batteries.ToArray(), b => b.Kind == Battery.Secondary && b.Side == Side.Centre);

        // the Great War's dreadnought: a director and a Dreyer table, AP from the main battery, HE from the 12-pdrs
        var dn = Fleet.Get("dreadnought").Class!;
        var main = dn.Batteries[0];
        Assert.Equal((Battery.Main, FireControlLevel.Director), (main.Kind, main.Level));
        Assert.Equal(ShellKind.Ap, dn.ShellTypes[main.Fires].Kind);
        Assert.Equal("305/45 AP great_war rn1916", dn.ShellTypeId(main.Fires));
        var sec = dn.Batteries[1];
        Assert.True(sec.Local);
        Assert.Equal(ShellKind.He, dn.ShellTypes[sec.Fires].Kind);

        // a destroyer's main guns are light guns: HE, as a battleship's secondaries
        var tb = Fleet.Get("tb_destroyer").Class!;
        Assert.Equal(Battery.Main, tb.Batteries[0].Kind);
        Assert.Equal(ShellKind.He, tb.ShellTypes[tb.Batteries[0].Fires].Kind);

        // Mikasa's rangefinders and no clock: level 1
        Assert.Equal(FireControlLevel.Rangefinder, Fleet.Get("mikasa").Class!.Batteries[0].Level);
    }
}
