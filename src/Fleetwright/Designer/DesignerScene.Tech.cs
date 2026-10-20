using System.Globalization;
using Fleetwright.Shipgen;
using Fleetwright.Sim.Ships;

namespace Fleetwright.Designer;

/// <summary>The tech blocks' pickers (docs/designer-ui/tech-blocks.md): the gun and torpedo blocks, the shock grade and the
/// outfit in the Templates panel, each battery's fire-control fit and sights on the Fire control tab. Their edits are
/// TechEdits'; each entry's tooltip gives the numbers it means for this design.</summary>
public sealed unsafe partial class DesignerScene
{
    static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

    /// <summary>A picker over a catalogue: the entry the design is, else the design's own block after the catalogue.</summary>
    static PickRow TechPick(string label, IReadOnlyList<string> names, TechEdits.Picked p, Action<int> apply, string[]? tips,
        string[]? groups = null, bool applies = true)
    {
        var all = names.ToList();
        int i = p.Index;
        if (i < 0)
        {
            all.Add(p.Other ?? "custom");
            i = all.Count - 1;
        }
        return new PickRow(label, all.ToArray(), i, names.Count, apply) { Tips = tips, Groups = groups, Applies = applies };
    }

    PickRow GunsPick(Design d)
    {
        var list = GunTemplates.All;
        var names = list.Select(t => t.Name).ToArray();
        if (!TechEdits.HasGuns(d))
            return new PickRow("Gun block: every battery's", ["no guns"], 0, 0, _ => { });
        return TechPick("Gun block: every battery's", names, TechEdits.GunPicked(d),
            k => Edit(TechEdits.SetGun(doc.Current, names[k]), $"Guns: {names[k]}"),
            list.Select(t => GunTip(d, t)).ToArray(), list.Select(t => t.Group).ToArray());
    }

    /// <summary>What a gun block makes of this design's guns: each calibre's reload, training, elevation and pattern, by
    /// the sim's own rules (DesignerPlaceholders), then the block's note.</summary>
    string GunTip(Design d, GunTemplates.Template t)
    {
        var row = WeaponBlocks.Gun(new Design(), t.Name);   // the catalogue's block, whatever the design carries
        var lines = new List<string> { t.Name };
        var seen = new HashSet<(double, double, bool)>();
        foreach (var b in (d.Main ?? []).Concat(d.Secondary ?? []))
        {
            if (b.CalibreMm is not { } cal)
                continue;
            double len = b.CalibreLength ?? 45;
            bool dp = Housings.Of(b) == "gunhouse";
            if (!seen.Add((cal, len, dp)))
                continue;
            var g = DesignerPlaceholders.Gun(cal, len, dp ? TurretShape.Dp : TurretShape.Bb, row);
            lines.Add(string.Format(Inv, "{0}/{1:0}{2}: reload {3:0.#} s · trains {4:0.#} °/s · up to {5:0}° · pattern {6:0.0}% · {7}",
                Fmt(cal, Quantity.Calibre), len, dp ? " dp" : "", g.ReloadS, g.TrainDegS, g.MaxElevationDeg,
                DesignerPlaceholders.Pattern(cal, row), len < (t.Value.BlackPowderUnderCal ?? 0) ? "black powder" : t.Value.Propellant));
        }
        lines.Add("");
        lines.Add(t.Note);
        return string.Join("\n", lines);
    }

    PickRow TorpedoPick(Design d)
    {
        var list = TorpedoTemplates.All;
        var names = list.Select(t => t.Name).ToArray();
        if (!TechEdits.HasTorpedoes(d))
            return new PickRow("Torpedo block", ["no torpedoes"], 0, 0, _ => { });
        return TechPick("Torpedo block", names, TechEdits.TorpedoPicked(d),
            k => Edit(TechEdits.SetTorpedo(doc.Current, names[k]), $"Torpedoes: {names[k]}"),
            list.Select(t =>
            {
                var v = t.Value;
                return string.Format(Inv, "{0}\nA 21\": {1:0} kn for {2:N0} m or {3:0} kn for {4:N0} m · warhead ×{5} of TNT · gyro {6:0}° · " +
                    "mounts train {7:0} °/s · fail {8:0}%, duds {9:0}%\n\n{10}", t.Name, v.FastKn, v.FastRangeM, v.SlowKn, v.SlowRangeM,
                    v.ChargeShare, v.GyroDeg, v.MountTrainDegS, 100 * v.Fail, 100 * v.Dud, t.Note);
            }).ToArray());
    }

    PickRow ShockPick(Design d)
    {
        var list = Fittings.All;
        var names = list.Select(t => t.Name).ToArray();
        return TechPick("Shock grade", names, TechEdits.ShockPicked(d),
            k => Edit(TechEdits.SetShockGrade(doc.Current, list[k]), $"Shock grade: {names[k]}"),
            list.Select(t => $"{t.Name}\n\n{t.Note}").ToArray());
    }

    PickRow OutfitPick(Design d)
    {
        var list = Outfits.All;
        var names = list.Select(t => t.Name).ToArray();
        return TechPick("Outfit: what burns aboard", names, TechEdits.OutfitPicked(d),
            k => Edit(TechEdits.SetOutfit(doc.Current, list[k]), $"Outfit: {names[k]}"),
            list.Select(t => $"{t.Name}\n{OutfitLine(t.Value)}\n\n{t.Note}").ToArray());
    }

    /// <summary>An outfit's fire loads, MJ/m², as fitted / stripped where they differ.</summary>
    static string OutfitLine(OutfitInput o)
    {
        static string L(double? a, double? b) => a == b ? $"{a:0}" : $"{a:0} / {b:0}";
        FireLoadsInput f = o.Fitted ?? new(), s = o.Stripped ?? new();
        return $"Fire loads, MJ/m² (fitted / stripped): quarters {L(f.Quarters, s.Quarters)}, cabins {L(f.Cabins, s.Cabins)}, " +
               $"offices {L(f.Offices, s.Offices)}, stores {L(f.Stores, s.Stores)}, machinery {L(f.Machinery, s.Machinery)}, " +
               $"deck {L(f.Deck, s.Deck)}; paint {o.PaintMjM2:0}; growth ×{o.Growth:0.0#}";
    }

    // ------------------------------------------------------------------ fire control

    static bool HasBattery(Design d, string battery) => battery switch
    {
        "main" => (d.Main ?? []).Any(b => b.Turrets > 0),
        "secondary" => (d.Secondary ?? []).Any(b => b.MountCount > 0),
        _ => (d.Aa?.Heavy ?? 0) + (d.Aa?.Light ?? 0) > 0,
    };

    PickRow PresetPick(Design d, string battery)
    {
        var ps = FireControlTemplates.Presets;
        var names = ps.Select(p => p.Name).ToArray();
        return TechPick("Preset", names, TechEdits.PresetPicked(d, battery),
            k => Edit(TechEdits.SetPreset(doc.Current, battery, names[k]), $"Fire control, {battery}: {names[k]}"),
            ps.Select(p => $"{p.Name}: {p.Optics} · {p.Computer} · {p.Mount}\n\n{p.Note}").ToArray(),
            applies: (d.FireControl?.Of(battery)?.Directors ?? 0) > 0);
    }

    static readonly string[] SlotLabels = ["Optics", "Computer", "Mount", "Sights (local control)"];

    /// <summary>One slot of a battery's fit (0-2) or its sights (3).</summary>
    PickRow BlockPick(Design d, string battery, int slot)
    {
        var list = TechEdits.BlocksOf(slot == 3 ? "sights" : TechEdits.FitKinds[slot]);
        var names = list.Select(t => t.Name).ToArray();
        bool applies = slot == 3 ? HasBattery(d, battery) : (d.FireControl?.Of(battery)?.Directors ?? 0) > 0;
        return TechPick(SlotLabels[slot], names, TechEdits.BlockPicked(d, battery, slot),
            k => Edit(slot == 3 ? TechEdits.SetSights(doc.Current, battery, names[k]) : TechEdits.SetFitBlock(doc.Current, battery, slot, names[k]),
                $"Fire control, {battery}: {names[k]}"),
            list.Select(t => $"{t.Name}\n\n{t.Note}").ToArray(), applies: applies);
    }
}
