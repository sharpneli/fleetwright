using Fleetwright.Shipgen;

namespace Fleetwright.Designer;

/// <summary>
/// The design's tech blocks picked from their catalogues (docs/designer-ui/tech-blocks.md): the gun block, the torpedo
/// block, each battery's fire-control fit and sights, the shock grade and the outfit. A pick names the template where the
/// design names it, copies the catalogue's block into the design's table (gun_tech, torpedo_tech, fire_control_tech) so the
/// design stands on its own, and drops the blocks nothing names any more. Templates only: a block's numbers aren't edited
/// here. A design carries one gun block (the user, 2026-10-19): the pick names it on every battery.
/// </summary>
public static class TechEdits
{
    /// <summary>Where a design's block stands against its catalogue: the catalogue entry it is (Index), or not (-1) with
    /// what to call it instead: "default: X" when the design names none, "mixed", "X · own values" for a catalogue name
    /// with other numbers, "custom: X" for a name the catalogue lacks.</summary>
    public readonly record struct Picked(int Index, string? Other);

    /// <summary>A named block against a catalogue of names: <paramref name="own"/> is the design's block under that name
    /// (null: the design carries none, so the catalogue's is used), <paramref name="same"/> whether it equals the
    /// catalogue's.</summary>
    public static Picked Match(IReadOnlyList<string> names, string? name, bool mixed, bool own, Func<int, bool> same, string dflt)
    {
        if (mixed)
            return new(-1, "mixed");
        if (name == null)
            return new(-1, $"default: {dflt}");
        for (int i = 0; i < names.Count; i++)
            if (names[i] == name)
                return !own || same(i) ? new(i, null) : new(-1, $"{name} · own values");
        return new(-1, $"custom: {name}");
    }

    // ------------------------------------------------------------------ guns

    static IEnumerable<BatteryInput> Batteries(Design d) => (d.Main ?? []).Concat(d.Secondary ?? []);

    /// <summary>Has the design any gun battery to name a gun block?</summary>
    public static bool HasGuns(Design d) => Batteries(d).Any();

    /// <summary>The gun block the batteries name: (its name, false) when they agree (null: none named, the default), (null,
    /// true) when they differ.</summary>
    public static (string? Name, bool Mixed) GunOf(Design d)
    {
        bool first = true;
        string? name = null;
        foreach (var b in Batteries(d))
        {
            if (first)
                (name, first) = (b.Gun, false);
            else if (b.Gun != name)
                return (null, true);
        }
        return (name, false);
    }

    /// <summary>Every battery fires the catalogue's gun block <paramref name="name"/>, the design's one gun block.</summary>
    public static Design SetGun(Design d, string name)
    {
        var block = GunTemplates.Get(name)?.Value ?? throw new ArgumentException($"no gun template \"{name}\"");
        if (!HasGuns(d))
            return d;
        return d with
        {
            Main = d.Main?.Select(b => b with { Gun = name }).ToList(),
            Secondary = d.Secondary?.Select(b => b with { Gun = name }).ToList(),
            GunTech = new Dictionary<string, GunTechInput>(StringComparer.Ordinal) { [name] = block },
        };
    }

    public static Picked GunPicked(Design d)
    {
        var (name, mixed) = GunOf(d);
        var list = GunTemplates.All;
        bool own = name != null && d.GunTech?.ContainsKey(name) == true;
        return Match(list.Select(t => t.Name).ToList(), name, mixed, own, i => d.GunTech![name!] == list[i].Value, GunTemplates.Default);
    }

    // ------------------------------------------------------------------ torpedoes

    /// <summary>Has the design torpedoes to name a block for (deck mounts or submerged tubes)?</summary>
    public static bool HasTorpedoes(Design d) =>
        (d.Torpedoes?.Mounts ?? 0) > 0 || d.Torpedoes?.Submerged is { } s && (s.PerSide ?? 0) + (s.Bow ?? 0) + (s.Stern ?? 0) > 0;

    /// <summary>The torpedoes are the catalogue's block <paramref name="name"/>, the design's one torpedo block.</summary>
    public static Design SetTorpedo(Design d, string name)
    {
        var block = TorpedoTemplates.Get(name)?.Value ?? throw new ArgumentException($"no torpedo template \"{name}\"");
        if (d.Torpedoes is not { } tp)
            return d;
        return d with
        {
            Torpedoes = tp with { Torpedo = name },
            TorpedoTech = new Dictionary<string, TorpedoTechInput>(StringComparer.Ordinal) { [name] = block },
        };
    }

    public static Picked TorpedoPicked(Design d)
    {
        string? name = d.Torpedoes?.Torpedo;
        var list = TorpedoTemplates.All;
        bool own = name != null && d.TorpedoTech?.ContainsKey(name) == true;
        return Match(list.Select(t => t.Name).ToList(), name, false, own, i => d.TorpedoTech![name!] == list[i].Value, TorpedoTemplates.Default);
    }

    // ------------------------------------------------------------------ fire control

    /// <summary>The batteries a design's fire control is given for.</summary>
    public static readonly string[] FcBatteries = ["main", "secondary", "aa"];

    /// <summary>The fit's slots, in its order.</summary>
    public static readonly string[] FitKinds = ["optics", "computer", "mount"];

    /// <summary>The catalogue's blocks of a kind (optics, computer, mount), and the mounts a battery's guns may lay by in
    /// local control (no director firing).</summary>
    public static IReadOnlyList<FireControlTemplates.Template> BlocksOf(string kind) =>
        kind == "sights"
            ? FireControlTemplates.All.Where(t => t.Kind == "mount" && t.Value.Mount?.DirectorFiring == false).ToList()
            : FireControlTemplates.All.Where(t => t.Kind == kind).ToList();

    static Design Fc(Design d, string battery, Func<DirectorInput, DirectorInput> f)
    {
        var fc = d.FireControl ?? new FireControlInput();
        var dir = f(fc.Of(battery) ?? new DirectorInput());
        fc = battery switch { "main" => fc with { Main = dir }, "secondary" => fc with { Secondary = dir }, _ => fc with { Aa = dir } };
        return d with { FireControl = fc };
    }

    /// <summary>A battery's fit: a preset's three blocks.</summary>
    public static Design SetPreset(Design d, string battery, string preset)
    {
        var p = FireControlTemplates.PresetNamed(preset) ?? throw new ArgumentException($"no fire-control preset \"{preset}\"");
        return FcTable(Fc(d, battery, x => x with { Fit = p.Fit.ToList() }), p.Fit);
    }

    /// <summary>One slot of a battery's fit (0 optics, 1 computer, 2 mount); a battery with no fit starts from the default
    /// preset's.</summary>
    public static Design SetFitBlock(Design d, string battery, int slot, string name)
    {
        var fit = FireControlTemplates.FitOf(d.FireControl?.Of(battery)).ToList();
        fit[slot] = name;
        return FcTable(Fc(d, battery, x => x with { Fit = fit }), [name]);
    }

    /// <summary>The mount block a battery's guns lay by when no director fires them.</summary>
    public static Design SetSights(Design d, string battery, string name) => FcTable(Fc(d, battery, x => x with { Sights = name }), [name]);

    /// <summary>The picked blocks copied in from the catalogue, the blocks no battery names dropped.</summary>
    static Design FcTable(Design d, IEnumerable<string> picked)
    {
        var table = new Dictionary<string, FcTechInput>(d.FireControlTech ?? [], StringComparer.Ordinal);
        foreach (var n in picked)
            if (FireControlTemplates.Get(n) is { } t)
                table[n] = t.Value;
        var named = new HashSet<string>(StringComparer.Ordinal);
        foreach (var b in FcBatteries)
            if (d.FireControl?.Of(b) is { } spec)
            {
                foreach (var n in spec.Fit ?? [])
                    named.Add(n);
                if (spec.Sights != null)
                    named.Add(spec.Sights);
            }
        foreach (var n in table.Keys.ToList())
            if (!named.Contains(n))
                table.Remove(n);
        return d with { FireControlTech = table.Count > 0 ? table : null };
    }

    /// <summary>The preset a battery's fit is: its index, "own fit" for blocks no preset names together, "default: X" for
    /// none given.</summary>
    public static Picked PresetPicked(Design d, string battery)
    {
        var fit = d.FireControl?.Of(battery)?.Fit;
        if (fit == null)
            return new(-1, $"default: {FireControlTemplates.DefaultPreset}");
        var ps = FireControlTemplates.Presets;
        for (int i = 0; i < ps.Count; i++)
            if (ps[i].Fit.SequenceEqual(fit) && fit.All(n => SameAsCatalogue(d, n)))
                return new(i, null);
        return new(-1, "own fit");
    }

    /// <summary>A slot's block (or the sights, slot 3) against its kind's list.</summary>
    public static Picked BlockPicked(Design d, string battery, int slot)
    {
        var spec = d.FireControl?.Of(battery);
        string name = slot == 3 ? FireControlTemplates.SightsOf(spec) : FireControlTemplates.FitOf(spec)[slot];
        var list = BlocksOf(slot == 3 ? "sights" : FitKinds[slot]);
        bool own = d.FireControlTech?.ContainsKey(name) == true;
        return Match(list.Select(t => t.Name).ToList(), name, false, own, i => SameAsCatalogue(d, name), "");
    }

    static bool SameAsCatalogue(Design d, string name) =>
        d.FireControlTech?.GetValueOrDefault(name) is not { } own || FireControlTemplates.Get(name) is { } t && FcEqual(own, t.Value);

    // FcTechInput's parts are records with only values, compared by value
    static bool FcEqual(FcTechInput a, FcTechInput b) => a.Optics == b.Optics && a.Computer == b.Computer && a.Mount == b.Mount;

    // ------------------------------------------------------------------ shock grade and outfit

    public static Design SetShockGrade(Design d, Fittings.Template t) =>
        d with { Fittings = (d.Fittings ?? new FittingsInput()) with { ShockGrade = t.Grade.ToString() } };

    public static Picked ShockPicked(Design d)
    {
        var g = Fittings.GradeOf(d);
        int i = Array.FindIndex(Fittings.All, t => t.Grade == g);
        return d.Fittings?.ShockGrade == null ? new(-1, $"default: {Fittings.All[i].Name}") : new(i, null);
    }

    public static Design SetOutfit(Design d, Outfits.Template t) => d with { Outfit = t.Value };

    public static Picked OutfitPicked(Design d)
    {
        if (d.Outfit is not { } o)
            return new(-1, $"default: {Outfits.Default(d).Name}");
        var list = Outfits.All;
        return Match(list.Select(t => t.Name).ToList(), o.Name ?? "own outfit", false, true, i => OutfitEqual(o, list[i].Value), "");
    }

    static bool OutfitEqual(OutfitInput a, OutfitInput b) =>
        a.Fitted == b.Fitted && a.Stripped == b.Stripped && a.PaintMjM2 == b.PaintMjM2 && a.Growth == b.Growth;
}
