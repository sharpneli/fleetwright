using Fleetwright.Designer;
using Fleetwright.Shipgen;
using TE = Fleetwright.Designer.TechEdits;

namespace Fleetwright.Tests;

/// <summary>The tech blocks' pickers (docs/designer-ui/tech-blocks.md): every template picked on every design leaves it
/// valid, shows as picked, carries the block it names and no other; the shipped designs read as catalogue entries.</summary>
public class TechEditsTests
{
    static IEnumerable<(string Name, Design D)> All() =>
        Directory.GetFiles(Paths.Shipgen("designs"), "*.json").Order(StringComparer.Ordinal)
            .Select(p => (Path.GetFileNameWithoutExtension(p), Design.Load(p)));

    static void Valid(Design d, string what) => Assert.Empty(ShipDesign.Validate(d, limits: false).Select(e => $"{what}: {e}"));

    /// <summary>Every shipped design's blocks are the catalogue's (the survey of 2026-10-19), so each picker shows an
    /// entry, not "own values".</summary>
    [Fact]
    public void Shipped_designs_read_as_catalogue_entries()
    {
        var failures = new List<string>();
        foreach (var (name, d) in All())
        {
            if (TE.HasGuns(d) && TE.GunPicked(d).Other is { } g && !g.StartsWith("default", StringComparison.Ordinal))
                failures.Add($"{name} guns: {g}");
            if (TE.HasTorpedoes(d) && TE.TorpedoPicked(d).Other is { } t && !t.StartsWith("default", StringComparison.Ordinal))
                failures.Add($"{name} torpedo: {t}");
            if (TE.OutfitPicked(d).Other is { } o && !o.StartsWith("default", StringComparison.Ordinal))
                failures.Add($"{name} outfit: {o}");
            foreach (var b in TE.FcBatteries)
            {
                if (TE.PresetPicked(d, b).Other is "own fit" && d.FireControl?.Of(b)?.Fit is { } fit
                    && !fit.All(n => FireControlTemplates.Get(n) is { } ct && (d.FireControlTech?.GetValueOrDefault(n) is not { } own || own == ct.Value)))
                    failures.Add($"{name} {b} fit");
                for (int slot = 0; slot < 4; slot++)
                    if (TE.BlockPicked(d, b, slot).Other is { } x)
                        failures.Add($"{name} {b} slot {slot}: {x}");
            }
        }
        Assert.True(failures.Count == 0, string.Join("\n", failures.Take(40)));
    }

    [Fact]
    public void Every_pick_leaves_every_design_valid_with_one_block()
    {
        foreach (var (name, d) in All())
        {
            if (TE.HasGuns(d))
                foreach (var t in GunTemplates.All)
                {
                    var x = TE.SetGun(d, t.Name);
                    Valid(x, $"{name} gun {t.Name}");
                    Assert.Equal(new TE.Picked(GunTemplates.All.ToList().IndexOf(t), null), TE.GunPicked(x));
                    Assert.Equal([t.Name], x.GunTech!.Keys);
                    Assert.All((x.Main ?? []).Concat(x.Secondary ?? []), b => Assert.Equal(t.Name, b.Gun));
                }
            if (TE.HasTorpedoes(d))
                foreach (var t in TorpedoTemplates.All)
                {
                    var x = TE.SetTorpedo(d, t.Name);
                    Valid(x, $"{name} torpedo {t.Name}");
                    Assert.Null(TE.TorpedoPicked(x).Other);
                    Assert.Equal([t.Name], x.TorpedoTech!.Keys);
                }
            foreach (var t in Fittings.All)
                Assert.Null(TE.ShockPicked(TE.SetShockGrade(d, t)).Other);
            foreach (var t in Outfits.All)
            {
                var x = TE.SetOutfit(d, t);
                Valid(x, $"{name} outfit {t.Name}");
                Assert.Null(TE.OutfitPicked(x).Other);
            }
        }
    }

    /// <summary>Fits: a preset fills the three slots, a slot changed is an own fit, sights stand apart; the design's
    /// fire_control_tech holds exactly the blocks its batteries name.</summary>
    [Fact]
    public void Fire_control_picks_carry_what_they_name()
    {
        foreach (var name in new[] { "dreadnought", "duilio", "destroyer", "kgv" })
        {
            var d = Design.Load(Paths.Shipgen("designs", name + ".json"));
            foreach (var b in TE.FcBatteries)
                foreach (var p in FireControlTemplates.Presets)
                {
                    var x = TE.SetPreset(d, b, p.Name);
                    Valid(x, $"{name} {b} {p.Name}");
                    Assert.Null(TE.PresetPicked(x, b).Other);
                    Named(x);
                    var computers = TE.BlocksOf("computer");
                    var other = computers.First(c => !FireControlTemplates.Presets.Any(q =>
                        q.Optics == p.Optics && q.Computer == c.Name && q.Mount == p.Mount)).Name;   // a fit no preset is
                    var y = TE.SetFitBlock(x, b, 1, other);
                    Assert.Equal("own fit", TE.PresetPicked(y, b).Other);
                    Assert.Null(TE.BlockPicked(y, b, 1).Other);
                    Named(y);
                    var z = TE.SetSights(y, b, "Telescopic sights");
                    Assert.Null(TE.BlockPicked(z, b, 3).Other);
                    Named(z);
                    Valid(z, $"{name} {b} sights");
                }
        }

        static void Named(Design d)
        {
            var named = TE.FcBatteries.Select(b => d.FireControl?.Of(b)).Where(s => s != null)
                .SelectMany(s => (s!.Fit ?? []).Concat(s.Sights is { } v ? [v] : [])).ToHashSet();
            Assert.Subset(named, (d.FireControlTech ?? []).Keys.ToHashSet());
        }
    }

    [Fact]
    public void Sights_are_mounts_without_director_firing()
    {
        var sights = TE.BlocksOf("sights");
        Assert.Contains(sights, t => t.Name == FireControlTemplates.DefaultSights);
        Assert.All(sights, t => Assert.False(t.Value.Mount!.DirectorFiring));
    }
}
