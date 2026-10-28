using Fleetwright.Shipgen;
using Fleetwright.Sim.Combat;
using Fleetwright.Sim.Core;
using Fleetwright.Sim.Ships;
using Xunit.Abstractions;

namespace Fleetwright.Sim.Tests;

/// <summary>Casualties per hit (docs/battle/shells-armour-damage.md E6; damage/16 §5.4's tests, wide bands): the burst's
/// man-stopping pieces against 16's table, the flat calibre curve, a crowded AA platform, a shot through a manned
/// director and through an empty space, the propellant flash, a destroyer's killed share and a Jutland ship's toll,
/// parts blind to the people's flux scale, the walking wounded coming back, and no garbage.</summary>
public class CasualtyTests(ITestOutputHelper output)
{
    static readonly SimSettings Settings = SimSettings.Load(Path.Combine(Fleet.Repo, "Content", "Sim", "settings.json"));
    static readonly WorldTime Start = WorldTime.FromUtc(1916, 5, 31, 14);

    /// <summary>The battle stations (docs/battle/crew.md N0): the sim's station of each crew group gives every design's
    /// men at each kind of station as Shipgen placed them (its crew report's summary), to the man; short only where a
    /// transmitting station's or a torpedo room's men are summed but stand on no cell of their own (docs/battle/bugs.md).</summary>
    [Fact]
    public void Each_crew_group_is_at_the_station_Shipgen_put_its_men()
    {
        int checkedDesigns = 0;
        foreach (var b in Fleet.All)
        {
            if (b.Ship?.Report.Crew?.BattleStations is not { Count: > 0 } summary || b.Class is not { } c)
                continue;
            var men = new double[BattleStations.Count];
            BattleStations.Tally(c, null, men, new double[BattleStations.Count]);
            for (int s = 0; s < BattleStations.Count; s++)
            {
                double gap = (summary.TryGetValue(BattleStations.Names[s], out long v) ? v : 0) - men[s];
                Assert.True(gap == 0 || gap > 0 && (BattleStation)s is BattleStation.Directors or BattleStation.Torpedoes,
                    $"{b.Case.Name}: {BattleStations.Names[s]} {men[s]} against Shipgen's {v}");
            }
            Assert.All(summary.Keys, k => Assert.Contains(k, BattleStations.Names));
            checkedDesigns++;
        }
        Assert.True(checkedDesigns > 40, $"{checkedDesigns} designs");
    }

    static Fragments Shell(double cal, double kg, double filler, string explosive, string kind, double size = 1, double speed = 1)
    {
        var x = Explosives.Get(explosive)!;
        var c = ShellTemplates.CasingOf(kind);
        var k = kind switch { "ap" => ShellKind.Ap, "common" => ShellKind.Common, _ => ShellKind.He };
        return Fragments.Of(new FragmentInputs(cal, kg, filler, k, x.GurneyMs!.Value, x.Brisance!.Value, size, speed, c.Nose!.Value,
            c.Ogive!.Value, c.Body!.Value, c.Plug!.Value, c.Tail!.Value, ShellTemplates.FillerBodyOf(kind)))!;
    }

    static ShellTypeRow Charge(double kg, string explosive)
    {
        var x = Explosives.Get(explosive)!;
        return new ShellTypeRow
        {
            FillerKg = kg, FillerRe = x.Re ?? 1, FillerQDet = x.QDet ?? 4.5, FillerQComb = x.QComb ?? 15, FillerAirM3Kg = x.AirM3Kg ?? 0, FillerEta = x.Eta ?? 1,
        };
    }

    static readonly Fragments Aac5 = Shell(127, 25, 3.3, "Explosive D", "he"), He6 = Shell(152, 47.6, 6.0, "Explosive D", "he"),
        Ap16 = Shell(406, 1225, 18.55, "Explosive D", "ap", 1.25), Hc16 = Shell(406, 862, 69.7, "Explosive D", "he");

    static readonly P3 Along = new(1, 0, 0);

    static double Stopping(Fragments f, double r) { var n = f.High.Stopping(r, 0, 0, Settings.ManStopJ); return n.Side + n.Ogive + n.Tail; }

    /// <summary>16 §4.1's table: the pieces still stopping a man 5 m off a static burst (5" AAC ~1,030, 6" HE ~1,270, 16"
    /// AP ~320, 16" HC ~1,660), within a factor of 1.6 (13's sprays here are E1's), and bigger shells breaking into
    /// bigger pieces, not more: a 16" AP throws fewer than a 5" AAC.</summary>
    [Fact]
    public void A_burst_throws_16s_man_stopping_pieces()
    {
        foreach (var (name, f, note) in new[] { ("5\" AAC", Aac5, 1030), ("6\" HE", He6, 1270), ("16\" AP", Ap16, 320), ("16\" HC", Hc16, 1660) })
        {
            double n = Stopping(f, 5);
            output.WriteLine($"{name}: {n:0} man-stopping pieces at 5 m (16: {note}), {Stopping(f, 15):0} at 15 m");
            Assert.InRange(n / note, 1 / 1.6, 1.6);
        }
        Assert.True(Stopping(Ap16, 5) < Stopping(Aac5, 5));
    }

    /// <summary>The casualties of a burst in the middle of a flat of 6 men: its pieces and its gas, independent.</summary>
    static (double Killed, double Casualties) InAFlat(Fragments f, ShellTypeRow charge, double v, in Box flat, int men)
    {
        var at = new P3((flat.X0 + flat.X1) / 2, (flat.Y0 + flat.Y1) / 2, flat.Base + 1.5);
        var (pk, pc) = Casualties.PiecesRisk(flat, Settings.CoverRoom[(int)RoomKind.Accommodation], f.High, at, Along, v, 1, 0, default, 0, Settings);
        double vol = (flat.X1 - flat.X0) * (flat.Y1 - flat.Y0) * (flat.Top - flat.Base);
        double p = Blast.Pressure(charge, charge.FillerKg, vol, Settings), felt = Blast.Felt(p, vol, 0, Settings);
        var (gk, gc) = Casualties.GasRisk(felt, Settings);
        return (men * (1 - (1 - pk) * (1 - gk)), men * (1 - (1 - pc) * (1 - gc)));
    }

    /// <summary>16 §5.4 test 1: a 16" AP burst in a battleship's flat of 6 men and a 5" AAC burst in a destroyer's of 6 come
    /// out within 1.5 of each other: calibre hardly matters, placement does (16 §2.3).</summary>
    [Fact]
    public void Calibre_hardly_moves_the_casualties_in_a_flat()
    {
        var big = new Box(0, 12, -5, 5, 0, 2.6);    // ~310 m³
        var small = new Box(0, 8, -3.5, 3.5, 0, 2.3);
        var ap = InAFlat(Ap16, Charge(18.55, "Explosive D"), 450, big, 6);
        var aac = InAFlat(Aac5, Charge(3.3, "Explosive D"), 500, small, 6);
        output.WriteLine($"16\" AP in a battleship's flat: {ap.Killed:0.0} killed, {ap.Casualties:0.0} casualties; 5\" AAC in a destroyer's: " +
                         $"{aac.Killed:0.0}, {aac.Casualties:0.0}");
        Assert.InRange(ap.Casualties / aac.Casualties, 1 / 1.5, 1.5);
    }

    /// <summary>16 §5.4 test 3: a 5" AAC bursting on a crowded AA platform (40 men within 5 m, 70 more out to 10 m, in the
    /// open) kills 10-30 and wounds 25-60.</summary>
    [Fact]
    public void A_5in_AAC_on_a_crowded_AA_platform()
    {
        const double h = 4.43, o = 8.86;   // an inner square of 5 m's circle's area, the ring out to 10 m's
        var at = new P3(0, 0, 0.3);
        var inner = new Box(-h, h, -h, h, 0, 2.5);
        Box[] ring = [new(-o, o, h, o, 0, 2.5), new(-o, o, -o, -h, 0, 2.5), new(-o, -h, -h, h, 0, 2.5), new(h, o, -h, h, 0, 2.5)];
        double ringArea = 4 * o * o - 4 * h * h, killed = 0, hurt = 0, cover = Settings.CoverPart[(int)ComponentKind.Aa];
        void Group(in Box b, double men)
        {
            var (pk, pc) = Casualties.PiecesRisk(b, cover, Aac5.High, at, new P3(0.94, 0, -0.34), 500, Settings.ContactShare, 0, default,
                3.3 * Explosives.Get("Explosive D")!.Re!.Value, Settings);
            (killed, hurt) = (killed + men * pk, hurt + men * pc);
        }
        Group(inner, 40);
        foreach (var b in ring)
            Group(b, 70 * (b.X1 - b.X0) * (b.Y1 - b.Y0) / ringArea);
        output.WriteLine($"5\" AAC on an AA platform: {killed:0.0} killed, {hurt - killed:0.0} wounded (Colorado's air defence station: 20 / 48)");
        Assert.InRange(killed, 10, 30);
        Assert.InRange(hurt - killed, 25, 60);
    }

    /// <summary>16 §5.4 test 2: a shot that passes without bursting harms nobody where nobody stands, and in a manned 2 m
    /// director kills by its lane and its walls' debris. 16 asks for 60% of the crew (Saumarez's 11 of 11, Gneisenau's 6 of
    /// 10, its disc fitted to them); with 14 §3.1's measured debris in place of the disc, a 10 mm wall's flakes no longer
    /// stop a man and the lane and the big piece kill ~40%: the gap noted in the plan.</summary>
    [Fact]
    public void A_shot_through_a_director_kills_its_lane_and_nothing_in_an_empty_space()
    {
        var director = new Box(0, 2, -1, 1, 0, 2.2);
        P3 a = new(-0.1, 0, 1.2), b = new(2.1, 0, 1.2);
        double lane = Casualties.LaneShare(director, a, b, 0.28, Settings);
        var debris = Spall.Perforated(new P3(0.05, 0, 1.2), Along, 280, 10, 500, 0.5);
        var (dk, dc) = Casualties.CloudRisk(director, Settings.CoverPart[(int)ComponentKind.Superstructure], debris, Settings);
        double killed = 1 - (1 - lane) * (1 - dk);
        output.WriteLine($"11\" through a 2 m director: lane {lane:0.00}, debris killed {dk:0.00} (casualty {dc:0.00}): {killed:0.00} of its crew killed");
        Assert.InRange(killed, 0.35, 1);

        // nobody there, nobody hurt: no group touched
        var (c, k) = Ship("lion");
        int empty = Enumerable.Range(0, c.Cells.Length).First(i => c.Cells[i].BattleCrew == 0 && c.Cells[i].Room >= 0);
        Casualties.Begin(k);
        Casualties.Lane(c, k, Casualties.Cell(c, empty), new P3(c.Cells[empty].Box.X0, 0, c.Cells[empty].Box.Base + 1), new P3(c.Cells[empty].Box.X1, 0,
            c.Cells[empty].Box.Base + 1), 0.343, Settings);
        var res = new EffectsResult();
        var r = RandomStream.Named(1, "casualty tests");
        Casualties.Draw(c, k, 0, Settings, ref res, ref r);
        Assert.Equal(0, res.Killed + res.Wounded);
    }

    static (ShipClass C, ShipCondition K) Ship(string design)
    {
        var w = new World(Start, 3, Settings);
        var c = Fleet.Get(design).Class!;
        w.Apply(ShipSystem.Spawn(w.Ships.AddClass(c), 56, 4, 0, 0));
        return (c, w.Ships.Store.Combat.Condition(w.Ships.Store.SlotOf(1)));
    }

    /// <summary>16 §5.4 test 5: a turret's flash kills 85% or more of the men it reaches (Derfflinger's C: 73 of 78), an open
    /// battery's 45-65% (Malaya's).</summary>
    [Fact]
    public void A_flash_kills_most_of_a_turrets_train_and_half_a_battery()
    {
        var (c, k) = Ship("lion");
        int m = Enumerable.Range(0, c.Mounts.Length).First(i => c.Mounts[i].Magazine >= 0 && c.Components[c.Mounts[i].Component].BattleCrew > 0);
        int g = Casualties.Part(c.Mounts[m].Component), men = Casualties.Men(c, k, g);
        var r = RandomStream.Named(2, "casualty tests");
        int killed = 0, hurt = 0, n = 0;
        for (int i = 0; i < 40; i++)
        {
            Array.Clear(k.Gone);
            Casualties.Begin(k);
            Casualties.Train(c, k, c.Mounts[m].Magazine, Settings);
            for (int j = 0; j < k.TouchedN; j++)
                n += Casualties.Men(c, k, k.Touched[j]);
            var res = new EffectsResult();
            k.Returns = 0;
            Casualties.Draw(c, k, 0, Settings, ref res, ref r);
            (killed, hurt) = (killed + res.Killed, hurt + res.Killed + res.Wounded);
        }
        output.WriteLine($"a turret's train: {100.0 * killed / n:0}% of its {n / 40} men killed, {100.0 * hurt / n:0}% hurt");
        Assert.True(killed >= 0.85 * n, $"{killed} of {n}");

        Casualties.Begin(k);
        Array.Clear(k.Gone);
        int battery = 0, bn = 0;
        for (int i = 0; i < 400; i++)
        {
            Array.Clear(k.Gone);
            Casualties.Begin(k);
            Casualties.Flash(k, g, false, Settings);
            var res = new EffectsResult();
            k.Returns = 0;
            Casualties.Draw(c, k, 0, Settings, ref res, ref r);
            (battery, bn) = (battery + res.Killed, bn + men);
        }
        Assert.InRange((double)battery / bn, 0.45, 0.65);
    }

    GivenHits.Report Hits(string target, string shooter, int battery, double rangeM, int n, int perShip, ShellKind? kind = null, SimSettings? settings = null)
    {
        var sc = Fleet.Get(shooter).Class!;
        int type = sc.Loadout.Fires(battery);
        if (kind is { } kk)
            foreach (var l in sc.Loadout.Loads(battery))
                if (sc.Loadout.ShellTypes[l.ShellType].Kind == kk)
                    type = l.ShellType;
        var r = GivenHits.Run(Fleet.Get(target).Class!, sc.Loadout.ShellTypes[type], sc.Mounts[sc.Links(sc.Batteries[battery].Mounts)[0]].MaxElevationDeg,
            rangeM, 90, n, perShip, 1, settings ?? Settings);
        int nf = Math.Max(r.Hits - r.Flashes, 1);
        output.WriteLine($"{shooter} on {target} at {rangeM / 1000:0} km: killed {r.Killed / r.Hits:0.00}, wounded {r.Wounded / r.Hits:0.00} a hit " +
                         $"(flashes aside {(r.Killed - r.FlashKilled) / nf:0.00} / {(r.Wounded - r.FlashWounded) / nf:0.00}); killed share " +
                         $"{r.Killed / Math.Max(r.Killed + r.Wounded, 1):0.00}, killing nobody {100.0 * r.KilledNone / r.Hits:0}%");
        return r;
    }

    /// <summary>16 §5.4 test 4: the killed share over a destroyer's hits is 0.25-0.4 (G7's 0.32).</summary>
    [Fact]
    public void A_destroyers_killed_share_is_a_third()
    {
        var r = Hits("fubuki", "fubuki", 0, 6000, 600, 10, ShellKind.He);
        Assert.InRange(r.Killed / (r.Killed + r.Wounded), 0.25, 0.4);
    }

    /// <summary>16 §5.4 test 6: a Jutland capital ship over 15 hits, flashes aside: 15-45 killed, 30-70 wounded (G1's 1.94 and
    /// 3.0 a hit).</summary>
    [Fact]
    public void A_Jutland_capital_ship_over_15_hits()
    {
        var r = Hits("seydlitz", "lion", 0, 14_000, 600, 15);
        int nf = Math.Max(r.Hits - r.Flashes, 1);
        Assert.InRange(15 * (r.Killed - r.FlashKilled) / nf, 15, 45);
        Assert.InRange(15 * (r.Wounded - r.FlashWounded) / nf, 30, 70);
    }

    /// <summary>16 §5.4 test 7: parts take 13's free-field fragments; only people take the flux scale. The same hits with S
    /// 1 and 30 wreck the same parts.</summary>
    [Fact]
    public void Parts_are_blind_to_the_peoples_flux_scale()
    {
        var one = Hits("seydlitz", "lion", 1, 8000, 150, 10, ShellKind.He, Settings with { CasualtyS = 1 });
        var thirty = Hits("seydlitz", "lion", 1, 8000, 150, 10, ShellKind.He, Settings with { CasualtyS = 30 });
        Assert.Equal(one.PartsOut, thirty.PartsOut);
        Assert.Equal(one.Outcomes, thirty.Outcomes);
        Assert.True(thirty.Killed + thirty.Wounded > one.Killed + one.Wounded);
    }

    /// <summary>16 §4.8: the walking wounded are back at their station when their time comes; the killed and those out for
    /// the action stay gone.</summary>
    [Fact]
    public void The_walking_wounded_come_back()
    {
        var (c, k) = Ship("lion");
        int g = Enumerable.Range(0, c.Components.Length + c.Cells.Length).First(i => Casualties.Men(c, k, i) >= 10);
        int men = Casualties.Men(c, k, g);
        k.Gone[g] = 6;
        k.CrewLost = 6;
        k.Back(g, 4, 20 * 60_000);
        Assert.Equal(20 * 60_000, k.NextClearMs);
        k.Clear(10 * 60_000);
        Assert.Equal(men - 6, Casualties.Men(c, k, g));
        k.Clear(20 * 60_000);
        Assert.Equal(men - 2, Casualties.Men(c, k, g));
        Assert.Equal(2, k.CrewLost);
        Assert.Equal(0, k.Returns);
    }

    /// <summary>16 §5.4 test 8: the people's model costs nothing in garbage (DamageTests' replay test covers the rest).</summary>
    [Fact]
    public void Casualties_allocate_nothing()
    {
        var box = new Box(0, 10, -4, 4, 0, 2.5);
        var cloud = Spall.Perforated(new P3(0, 0, 1), Along, 343, 25, 450, 0.5);
        Casualties.PiecesRisk(box, 0.6, He6.High, new P3(5, 0, 1), Along, 400, 1, 6, new P3(1, 0, 0), 5, Settings);
        long before = GC.GetAllocatedBytesForCurrentThread();
        double sum = 0;
        for (int i = 0; i < 200; i++)
        {
            sum += Casualties.PiecesRisk(box, 0.6, He6.High, new P3(5, 0, 1), Along, 400, 1, 6, new P3(1, 0, 0), 5, Settings).Killed;
            sum += Casualties.CloudRisk(box, 0.6, cloud, Settings).Casualty + Casualties.GasRisk(0.2, Settings).Killed;
        }
        Assert.Equal(0, GC.GetAllocatedBytesForCurrentThread() - before);
        Assert.True(sum > 0);
    }
}
