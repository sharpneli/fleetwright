using Fleetwright.Shipgen;
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
            if (row.Local ? row.Fit != FireControlPresets.Local : row.Level != FireControlPresets.LevelOf(row.Fit))
                p.Add($"battery {b}: its fit and its level ({row.Level}) don't go together");
            // its fire-control units (fire-control.md F0): its directors', each with its director's eyes as instruments
            if (!c.Links(row.Units).SequenceEqual(c.Links(row.Directors)))
                p.Add($"battery {b}: units {string.Join(",", c.Links(row.Units).ToArray())} aren't its directors'");
            foreach (int u in c.Links(row.Units))
            {
                var unit = c.FcUnits[u];
                if (unit.Battery != row.Kind || unit.Instruments.Count == 0)
                    p.Add($"unit {u}: a {unit.Battery} unit with {unit.Instruments.Count} instruments on a {row.Kind} battery");
                foreach (int i in c.Links(unit.Instruments))
                {
                    var ins = c.Instruments[i];
                    var eye = c.Eyes[ins.Eye];
                    if (ins.Unit != u || ins.Component != c.Directors[unit.Director].Component || eye.Kind != EyeKind.Director ||
                        eye.Component != ins.Component || ins.BaseM != eye.RangefinderM || ins.HeightM != eye.HeightM)
                        p.Add($"unit {u}: instrument {i} isn't its director's eye");
                }
            }
            double share = 0;
            bool fires = false;
            foreach (var l in c.Loadout.Loads(b))
            {
                share += l.Share;
                fires |= l.ShellType == c.Loadout.Fires(b);
                if (c.Loadout.ShellTypes[l.ShellType].CalibreMm != row.CalibreMm)
                    p.Add($"battery {b} carries a {c.Loadout.ShellTypes[l.ShellType].CalibreMm} mm shell");
            }
            if (Math.Abs(share - 1) > 1e-9 || !fires)
                p.Add($"battery {b}: shares add to {share}, fires a shell it {(fires ? "carries" : "doesn't carry")}");
        }
        if (inBatteries != c.Mounts.ToArray().Count(m => m.Kind != ComponentKind.Torpedo))
            p.Add($"{inBatteries} mounts in batteries");

        for (int t = 0; t < c.Loadout.ShellTypes.Length; t++)
        {
            var s = c.Loadout.ShellTypes[t];
            if (!Pos(s.MassKg) || !Pos(s.MuzzleVelocityMs) || !Pos(s.FormFactor) || !Pos(s.FillerKg) || !(s.FuzeDelayS >= 0)
                || s.FillerKg > 0.2 * s.MassKg || s.Kind == ShellKind.He && s.Fuze != Fuze.Nose || !(s.FillerRe > 0))
                p.Add($"shell type {c.Loadout.ShellTypeId(t)}: {s}");
            // shared by id: one id, one shell, whichever class it's in (but for its muzzle velocity, which follows the
            // outfit's heaviest shell: a template beside a heavier AP leaves the gun faster)
            if (shared.TryGetValue(c.Loadout.ShellTypeId(t), out var other) && other with { MuzzleVelocityMs = 0 } != s with { MuzzleVelocityMs = 0 })
                p.Add($"shell type {c.Loadout.ShellTypeId(t)} differs from another class's");
            else
                shared[c.Loadout.ShellTypeId(t)] = s;
        }

        var pl = c.Plating;
        if (!Pos(pl.ShellMm) || !Pos(pl.ShellEndMm) || !Pos(pl.StrengthDeckMm) || !(pl.MidX0 < pl.MidX1) || !Pos(pl.SuperstructureMm))
            p.Add($"plating {pl}");
        return p;
    }

    /// <summary>The class reads the design's shells: a block's drag shows in its shell, and a battery that gives none
    /// carries the default outfit (docs/battle/shells-armour-damage.md S1).</summary>
    [Fact]
    public void Shells_come_from_the_design()
    {
        var dn = Fleet.Get("dreadnought").Ship!.Design;
        var faster = ShipClassCompiler.Compile(ShipDesign.Build(dn with
        {
            Ammunition = dn.Ammunition!.ToDictionary(kv => kv.Key, kv => kv.Value with { FormFactor = 0.9 }),
        }));
        Assert.Equal(0.9, faster.Loadout.ShellTypes[faster.Loadout.Fires(0)].FormFactor);

        var bare = ShipClassCompiler.Compile(ShipDesign.Build(dn with
        {
            Ammunition = null, Main = dn.Main!.Select(b => b with { Shells = null }).ToList(),
            Secondary = dn.Secondary!.Select(b => b with { Shells = null }).ToList(),
        }));
        var main = bare.Loadout.Loads(0).ToArray();
        Assert.Equal([(ShellKind.Ap, 0.7), (ShellKind.He, 0.3)], main.Select(l => (bare.Loadout.ShellTypes[l.ShellType].Kind, l.Share)));
        Assert.Equal(ShellTemplates.Get("Standard APC (1912)")!.Value.MassKKgM3!.Value * 0.305 * 0.305 * 0.305,
            bare.Loadout.ShellTypes[bare.Loadout.Fires(0)].MassKg, 9);
        // in the magazine order: a secondary fires HE first
        var sec = bare.Loadout.Loads(1).ToArray();
        Assert.Equal([(ShellKind.He, 0.7), (ShellKind.Ap, 0.3)], sec.Select(l => (bare.Loadout.ShellTypes[l.ShellType].Kind, l.Share)));
    }

    /// <summary>Rounds by kind (docs/battle/shells-armour-damage.md S2): every battery's loads in the magazine order (main
    /// AP first, secondary HE first) and its lead the first of them; every mount's rounds split over its battery's loads,
    /// adding up to its rounds, each near its share.</summary>
    [Fact]
    public void Each_mount_counts_its_rounds_by_kind_in_the_magazine_order()
    {
        var problems = new List<string>();
        foreach (var b in Fleet.All.Where(b => b.Ship != null && b.Class != null))
        {
            var c = b.Class!;
            for (int i = 0; i < c.Batteries.Length; i++)
            {
                var row = c.Batteries[i];
                var loads = c.Loadout.Loads(i).ToArray();
                var ranks = loads.Select(l => MagazineOrder.Rank(row.Kind, c.Loadout.ShellTypes[l.ShellType].Kind)).ToArray();
                if (!ranks.SequenceEqual(ranks.Order()) || c.Loadout.Fires(i) != loads[0].ShellType)
                    problems.Add($"{b.Case.Name} battery {i}: loads out of the magazine order");
            }
            for (int m = 0; m < c.Mounts.Length; m++)
            {
                var mr = c.Mounts[m];
                int s0 = c.Loadout.MountLoadStart(m), n = c.Loadout.MountLoadStart(m + 1) - s0;
                if (mr.Battery < 0)
                {
                    if (n != 0)
                        problems.Add($"{b.Case.Name} torpedo mount {m} has {n} loads");
                    continue;
                }
                var loads = c.Loadout.Loads(mr.Battery).ToArray();
                var rounds = c.Loadout.LoadRounds.Slice(s0, n).ToArray();
                if (n != loads.Length || mr.Rounds > 0 && (rounds.Sum() != mr.Rounds || rounds.Where((r, k) => Math.Abs(r - mr.Rounds * loads[k].Share) >= 1).Any()))
                    problems.Add($"{b.Case.Name} mount {m}: {mr.Rounds} rounds as {string.Join("/", rounds)}");
            }
        }
        Assert.True(problems.Count == 0, string.Join("\n", problems.Take(20)));
    }

    /// <summary>The templates and the placeholders against the research's guns of the Great War (fire_control_ref.py
    /// GUNS, and the QF 4"/45 Mk V from navweaps: 14.06 kg HE at 716 m/s): the RN family's shell mass and the default
    /// muzzle velocity within 10%, and the mount's numbers in the research's ranges.</summary>
    [Theory]
    [InlineData(305, 45, 386, 831, "RN 1909 APC (lyddite)")]    // 12"/45 Mk X
    [InlineData(343, 45, 635, 757, "RN 1912 APC (lyddite)")]    // 13.5"/45 Mk V(H)
    [InlineData(381, 42, 871, 749, "RN 1912 APC (lyddite)")]    // 15"/42 Mk I
    [InlineData(102, 45, 14.06, 716, "RN 1906 light HE (lyddite)")]  // 4"/45 QF Mk V
    public void Placeholders_land_near_the_research_guns(double mm, double cal, double kg, double ms, string template)
    {
        double d = mm / 1000;
        Assert.InRange(ShellTemplates.Get(template)!.Value.MassKKgM3!.Value * d * d * d / kg, 0.9, 1.1);
        Assert.InRange(Ammunition.DefaultMuzzleVelocity(cal) / ms, 0.9, 1.1);

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
            // its own side's directors and the centreline's (her pair, or since the turret rangefinders shortened her,
            // fire-control.md F4b, two on the centreline serving both sides)
            var dirs = bismarck.Links(b.Directors).ToArray();
            Assert.NotEmpty(dirs);
            foreach (int d in dirs)
            {
                var bx = bismarck.Components[bismarck.Directors[d].Component].Bounds;
                double y = (bx.Y0 + bx.Y1) / 2;
                Assert.True(Math.Abs(y) < 0.5 || (y < 0) == (b.Side == Side.Port), $"{bismarck.ComponentId(bismarck.Directors[d].Component)} on the wrong side");
            }
            foreach (int m in bismarck.Links(b.Mounts))
                Assert.Equal(b.Side == Side.Port, bismarck.Mounts[m].Y < 0);
        }
        // a centreline secondary is a third, its own
        Assert.Contains(Fleet.Get("murica").Class!.Batteries.ToArray(), b => b.Kind == Battery.Secondary && b.Side == Side.Centre);

        // the Great War's dreadnought: a director and a Dreyer table, AP from the main battery, HE from the 12-pdrs
        var dn = Fleet.Get("dreadnought").Class!;
        var main = dn.Batteries[0];
        Assert.Equal((Battery.Main, FireControlLevel.Director), (main.Kind, main.Level));
        Assert.Equal(ShellKind.Ap, dn.Loadout.ShellTypes[dn.Loadout.Fires(0)].Kind);
        Assert.Equal("305/45 RN 1906 APC (powder)", dn.Loadout.ShellTypeId(dn.Loadout.Fires(0)));
        var sec = dn.Batteries[1];
        Assert.True(sec.Local);
        Assert.Equal(ShellKind.He, dn.Loadout.ShellTypes[dn.Loadout.Fires(1)].Kind);

        // the magazine order (decision 7): a destroyer's main guns fire AP first, as a battleship's main battery, and its
        // secondaries HE first; no size threshold
        var tb = Fleet.Get("tb_destroyer").Class!;
        Assert.Equal(Battery.Main, tb.Batteries[0].Kind);
        Assert.Equal(ShellKind.Ap, tb.Loadout.ShellTypes[tb.Loadout.Fires(0)].Kind);

        // Mikasa's rangefinders and no clock: level 1
        Assert.Equal(FireControlLevel.Rangefinder, Fleet.Get("mikasa").Class!.Batteries[0].Level);
    }
}
