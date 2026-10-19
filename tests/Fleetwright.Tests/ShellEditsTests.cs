using Fleetwright.Designer;
using Fleetwright.Shipgen;
using SE = Fleetwright.Designer.ShellEdits;

namespace Fleetwright.Tests;

/// <summary>The shell picker's edits (docs/designer-ui/shells.md): a family, a block, shares, kinds added and removed,
/// the default loadout and the Templates panel's every-battery family. Every edit leaves the design valid, its shares
/// adding up to 1, and only the blocks its batteries name in its ammunition.</summary>
public class ShellEditsTests
{
    static Design Load(string name) => Design.Load(Paths.Shipgen("designs", name + ".json"));

    static IEnumerable<(string Name, Design D)> All() =>
        Directory.GetFiles(Paths.Shipgen("designs"), "*.json").Select(p => (Path.GetFileNameWithoutExtension(p), Design.Load(p)));

    static void Sound(Design d, string what)
    {
        Assert.Empty(ShipDesign.Validate(d, limits: false).Select(e => $"{what}: {e}"));
        var named = (d.Main ?? []).Concat(d.Secondary ?? []).SelectMany(b => b.Shells ?? []).Select(e => e.Ammo).ToHashSet();
        Assert.True(named.SetEquals((d.Ammunition ?? []).Keys), $"{what}: the ammunition holds what the batteries name");
        foreach (var b in (d.Main ?? []).Concat(d.Secondary ?? []))
            if (b.Shells is { } s)
                Assert.Equal(1, s.Sum(e => e.Share ?? 0), 6);
    }

    [Fact]
    public void Every_edit_leaves_every_design_valid()
    {
        foreach (var (name, d) in All())
            foreach (var main in new[] { true, false })
                for (int k = 0; k < ((main ? d.Main : d.Secondary)?.Count ?? 0); k++)
                {
                    if ((main ? d.Main : d.Secondary)![k].CalibreMm == null)
                        continue;
                    string at = $"{name} {(main ? "main" : "secondary")}[{k}]";
                    var f = SE.SetFamily(d, main, k, "RN 1912");
                    Sound(f, at + " family");
                    var c = SE.AddKind(f, main, k, "common");
                    Sound(c, at + " + common");
                    var s = SE.SetShare(c, main, k, 0, 55);
                    Sound(s, at + " share");
                    var r = SE.Remove(s, main, k, SE.Loadout(s, main, k).Count - 1);
                    Sound(r, at + " remove");
                    Sound(SE.Default(r, main, k), at + " default");
                }
    }

    [Fact]
    public void A_family_takes_its_light_block_for_a_light_gun_and_common_for_a_missing_kind()
    {
        var d = Load("dreadnought");
        // the 12-pdr (76 mm) secondary: RN 1906's light HE; the 12" main its heavy HE
        var sec = SE.SetFamily(d, false, 0, "RN 1906");
        Assert.Contains(SE.Loadout(sec, false, 0), e => e.Ammo == "RN 1906 light HE (lyddite)");
        var main = SE.SetFamily(d, true, 0, "RN 1906");
        Assert.Contains(SE.Loadout(main, true, 0), e => e.Ammo == "RN 1906 HE (lyddite)");
        // the 1867 family has no HE: its common shell takes HE's place
        var old = SE.SetFamily(d, true, 0, "1867 chilled iron");
        Assert.Equal(["ap", "common"], SE.Loadout(old, true, 0).Select(e => SE.TemplateOf(e.Ammo)!.Value.Kind));
        Assert.Equal("1867 chilled iron", SE.FamilyOf(old, true, 0));
    }

    [Fact]
    public void Shares_step_and_settle_to_whole_percents()
    {
        var d = Load("dreadnought");   // main 70 AP / 30 HE
        var up = SE.SetShare(d, true, 0, 0, 75);
        Assert.Equal([0.75, 0.25], SE.Loadout(up, true, 0).Select(e => e.Share!.Value));
        var top = SE.SetShare(d, true, 0, 0, 100);   // the other keeps 5%
        Assert.Equal([0.95, 0.05], SE.Loadout(top, true, 0).Select(e => e.Share!.Value));
        var three = SE.AddKind(d, true, 0, "common");
        Assert.Equal(20, SE.Loadout(three, true, 0).Single(e => SE.TemplateOf(e.Ammo)!.Value.Kind == "common").Share!.Value * 100, 6);
        var gone = SE.Remove(three, true, 0, 0);   // AP's share to the largest left
        Assert.Equal(1, SE.Loadout(gone, true, 0).Sum(e => e.Share!.Value), 6);
    }

    /// <summary>The FUZE cell: a base fuze's delay set for one battery, kept through other edits, gone where it means
    /// nothing (the block's own, a nose-fuzed block) and back to the block's on a reset.</summary>
    [Fact]
    public void A_battery_sets_its_own_fuze_delay()
    {
        var d = Load("dreadnought");   // main: RN 1906 APC (0.003 s) 70%, HE (nose) 30%
        int ap = SE.Loadout(d, true, 0).FindIndex(e => SE.TemplateOf(e.Ammo)!.Value.Kind == "ap");
        var set = SE.SetFuzeDelay(d, true, 0, ap, 0.02);
        Sound(set, "delay set");
        Assert.Equal(0.02, SE.Loadout(set, true, 0)[ap].FuzeDelayS);
        Assert.Null(SE.Loadout(set, false, 0)[0].FuzeDelayS);   // the other battery's untouched
        Assert.Equal(0.02, SE.Loadout(SE.SetShare(set, true, 0, ap, 80), true, 0)[ap].FuzeDelayS);   // kept through a share change
        Assert.Equal(0.02, SE.Loadout(SE.SetBlock(set, true, 0, ap, "RN 1912 APC (lyddite)"), true, 0)[ap].FuzeDelayS);   // and another base-fuzed block
        Assert.Null(SE.Loadout(SE.SetFuzeDelay(set, true, 0, ap, null), true, 0)[ap].FuzeDelayS);   // the reset
        double block = SE.BlockFuze(d, SE.Loadout(d, true, 0)[ap].Ammo)!.DelayS!.Value;
        Assert.Null(SE.Loadout(SE.SetFuzeDelay(d, true, 0, ap, block), true, 0)[ap].FuzeDelayS);   // the block's own is no setting
        Assert.Equal(0, SE.Loadout(SE.SetFuzeDelay(d, true, 0, ap, -1), true, 0)[ap].FuzeDelayS);   // never under 0
        // on a nose-fuzed block it means nothing, and goes
        var nose = SE.SetBlock(set, true, 0, ap, "Standard HE (1912)");
        Sound(nose, "delay on a nose block");
        Assert.All(SE.Loadout(nose, true, 0), e => Assert.Null(e.FuzeDelayS));
    }

    [Fact]
    public void A_battery_on_the_default_loadout_writes_it_out_when_edited()
    {
        var d = Load("dreadnought");
        var bare = SE.Default(SE.Default(d, true, 0), false, 0);
        Assert.Null(bare.Ammunition);
        Assert.Equal("1912 standard", SE.FamilyOfAll(bare));
        var block = SE.SetBlock(bare, true, 0, 0, "RN 1912 APC (lyddite)");
        Assert.Equal(["RN 1912 APC (lyddite)", "Standard HE (1912)"], SE.Loadout(block, true, 0).Select(e => e.Ammo));
        Sound(block, "default, then a block");
        Assert.Equal("mixed", SE.FamilyOfAll(block));
        Assert.Equal("RN 1912", SE.FamilyOfAll(SE.SetFamilyAll(block, "RN 1912")));
    }

    [Fact]
    public void The_tree_lists_the_design_navy_first_and_marks_her_era()
    {
        var d = Load("dreadnought");   // portsmouth, great war
        var tree = SE.Tree(d);
        Assert.Equal("RN", tree[0].Navy);
        Assert.Equal(SE.AnyNavy, tree[1].Navy);
        Assert.Equal("RN 1912", tree[0].Nearest);
        Assert.Equal(ShellTemplates.All.Select(t => t.Group).Distinct().Count(), tree.Sum(n => n.Families.Count));
        Assert.All(tree, n => Assert.Equal(n.Families.OrderBy(f => SE.Parse(f).Year), n.Families));
    }
}
