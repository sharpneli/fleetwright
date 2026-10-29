using System.Text.Json.Serialization;

namespace Fleetwright.Shipgen;

/// <summary>The shells as design data (docs/battle/shells-armour-damage.md phase S): the design's `ammunition` table of
/// shell tech blocks, calibre-free like machinery.tech, its `explosives`, and each battery's loadout (`shells`) naming
/// the blocks. Validated here and resolved per battery into the report (kind, mass, muzzle velocity, filler and its
/// explosive, fuze), which the sim's compiler reads. A battery that gives no shells carries the default loadout of the
/// default template (ShellTemplates.DefaultLoadout).</summary>
public static class Ammunition
{
    public static readonly string[] Kinds = ["ap", "common", "he", "star"];
    public static readonly string[] Caps = ["none", "soft", "hard", "hard2", "break_away"];
    public static readonly string[] FuzePositions = ["base", "nose", "time"];
    public static readonly string[] Boosters = ["basic", "ce"];
    public static readonly string[] FuzePlates = ["single", "two"];

    static readonly string[] BlockFields =
        ["kind", "mass_k_kg_m3", "nose_crh", "form_factor", "cap", "windscreen", "body", "filler", "steel", "casing", "fuze", "star"];
    static readonly string[] EntryFields = ["ammo", "share", "mass_kg", "muzzle_velocity_ms", "fuze_delay_s"];

    /// <summary>PLACEHOLDER (until a gun tech exists): a gun's muzzle velocity with its heaviest shell from its length
    /// in calibres, 280 + 11·L m/s kept to 380-1000, from the Victorian muzzle-loaders (12"/14 RML ~400 m/s) to the
    /// 76/62's 925: the 12"/45 Mk X 831 against 775, the 15"/42 749 against 742.</summary>
    public static double DefaultMuzzleVelocity(double calibreLength) => Math.Clamp(280 + 11 * calibreLength, 380, 1000);

    /// <summary>A lighter shell from the same gun goes faster by the masses' fourth root: (m_heavy/m)^0.25.</summary>
    public static double MuzzleVelocity(double heavyMs, double heavyKg, double kg) => heavyMs * Math.Pow(heavyKg / kg, 0.25);

    public static List<string> Validate(Design design)
    {
        var errs = new List<string>();
        foreach (var (name, x) in design.Explosives ?? [])
            if (x is null)
                errs.Add($"explosives.\"{name}\": give its numbers");
            else
                errs.AddRange(Explosives.Errors(x, $"explosives.\"{name}\""));
        var table = design.Ammunition ?? [];
        foreach (var (name, a) in table)
        {
            string at = $"ammunition.\"{name}\"";
            if (a is null)
                errs.Add($"{at}: give the block's numbers");
            else
                errs.AddRange(BlockErrors(design, a, at));
        }
        foreach (var (which, list) in new[] { ("main", design.Main), ("secondary", design.Secondary) })
            for (int i = 0; i < (list?.Count ?? 0); i++)
            {
                var b = list![i];
                string at = $"{which}[{i}]";
                if (b.MuzzleVelocityMs is { } mv && !(mv > 0))
                    errs.Add($"{at}.muzzle_velocity_ms: more than 0");
                if (b.Shells is not { } shells)
                    continue;
                if (shells.Count == 0)
                    errs.Add($"{at}.shells: give at least one, or leave the key out");
                double sum = 0;
                for (int k = 0; k < shells.Count; k++)
                {
                    var e = shells[k];
                    string ek = $"{at}.shells[{k}]";
                    errs.AddRange(e.Extra.KeysOrEmpty().Select(x => $"{ek}.{x}: not a shell entry field ({string.Join(", ", EntryFields)})"));
                    if (e.Ammo is null || !table.ContainsKey(e.Ammo))
                        errs.Add($"{ek}.ammo = {Style.Quote(e.Ammo)}: not in the design's ammunition table");
                    if (!(e.Share > 0))
                        errs.Add($"{ek}.share: its share of the rounds, more than 0");
                    else
                        sum += e.Share.Value;
                    if (e.MassKg is { } kg && !(kg > 0))
                        errs.Add($"{ek}.mass_kg: more than 0");
                    if (e.MuzzleVelocityMs is { } v && !(v > 0))
                        errs.Add($"{ek}.muzzle_velocity_ms: more than 0");
                    if (e.FuzeDelayS is { } delay)
                    {
                        if (!(delay >= 0))
                            errs.Add($"{ek}.fuze_delay_s: 0 or more");
                        else if (e.Ammo is not null && table.GetValueOrDefault(e.Ammo)?.Fuze?.Position is "nose")
                            errs.Add($"{ek}.fuze_delay_s: {Style.Quote(e.Ammo)} has a nose fuze, which fires on the first plate; a delay is a base fuze's");
                    }
                }
                if (shells.Count > 0 && Math.Abs(sum - 1) > 1e-6)
                    errs.Add($"{at}.shells: the shares add up to {sum:0.###}, not 1");
            }
        return errs;
    }

    /// <summary>One block's errors: what it must give (kind, k, form factor, filler, fuze, the outcome row), and every
    /// number it gives in its range.</summary>
    public static IEnumerable<string> BlockErrors(Design design, AmmoInput a, string at)
    {
        foreach (var k in a.Extra.KeysOrEmpty())
            yield return $"{at}.{k}: not a shell field ({string.Join(", ", BlockFields)})";
        if (a.Kind is null || !Kinds.Contains(a.Kind))
            yield return $"{at}.kind = {Style.Quote(a.Kind)}: use {string.Join(", ", Kinds)}";
        if (!(a.MassKKgM3 > 0))
            yield return $"{at}.mass_k_kg_m3: give the shell's mass over D³ (kg/m³, D in m), more than 0";
        if (!(a.FormFactor > 0))
            yield return $"{at}.form_factor: give the drag form factor, more than 0";
        if (a.NoseCrh is { } crh && !(crh > 0))
            yield return $"{at}.nose_crh: calibres, more than 0";
        static bool Share(double? v) => v is null or (>= 0 and <= 1);
        if (a.Cap is { } cap)
        {
            foreach (var k in cap.Extra.KeysOrEmpty())
                yield return $"{at}.cap.{k}: not a cap field (kind, share)";
            if (cap.Kind is null || !Caps.Contains(cap.Kind))
                yield return $"{at}.cap.kind = {Style.Quote(cap.Kind)}: use {string.Join(", ", Caps)}";
            if (cap.Share is < 0 or >= 1)
                yield return $"{at}.cap.share: a share of the mass, 0 up to 1";
        }
        if (a.Body is { } body)
        {
            foreach (var k in body.Extra.KeysOrEmpty())
                yield return $"{at}.body.{k}: not a body field (break_ob_deg, break_t_d, quality)";
            if (body.BreakObDeg is < 0 or > 90)
                yield return $"{at}.body.break_ob_deg: degrees, 0-90";
            if (body.BreakTD is < 0)
                yield return $"{at}.body.break_t_d: calibres, 0 or more";
            if (body.Quality is { } q && !(q > 0))
                yield return $"{at}.body.quality: a multiplier, more than 0";
        }
        if (a.Filler is not { } f)
            yield return $"{at}.filler: give the filler's share of the mass and its explosive";
        else
        {
            foreach (var k in f.Extra.KeysOrEmpty())
                yield return $"{at}.filler.{k}: not a filler field (share, body, explosive, booster)";
            if (f.Share is not (>= 0 and < 1))
                yield return $"{at}.filler.share: a share of the mass, 0 up to 1";
            if (f.Body is { } fb && !(fb > 0 && fb <= 1))
                yield return $"{at}.filler.body: the filler's share alongside the body, more than 0 up to 1";
            if (f.Booster is { } boost && !Boosters.Contains(boost))
                yield return $"{at}.filler.booster = {Style.Quote(boost)}: use basic or ce";
            if (f.Explosive is { } x)
            {
                if (x.Name is { } n && design.Explosives?.ContainsKey(n) != true && Explosives.Get(n) is null)
                    yield return $"{at}.filler.explosive = {Style.Quote(n)}: not in the design's explosives or the catalogue " +
                                 $"({string.Join(", ", Explosives.All.Select(e => e.Value.Name))})";
                foreach (var e in Explosives.Errors(x, $"{at}.filler.explosive"))
                    yield return e;
            }
        }
        if (a.Steel is { } st)
        {
            foreach (var k in st.Extra.KeysOrEmpty())
                yield return $"{at}.steel.{k}: not a steel field (size, speed)";
            if (st.Size is { } sz && !(sz > 0) || st.Speed is { } sp && !(sp > 0))
                yield return $"{at}.steel: multipliers, more than 0";
        }
        if (a.Casing is { } c)
        {
            foreach (var k in c.Extra.KeysOrEmpty())
                yield return $"{at}.casing.{k}: not a casing share (nose, ogive, body, plug, tail)";
            double?[] parts = [c.Nose, c.Ogive, c.Body, c.Plug, c.Tail];
            if (!parts.All(Share))
                yield return $"{at}.casing: shares of the casing, 0 to 1";
            else if (parts.All(p => p != null) && Math.Abs(parts.Sum(p => p!.Value) - 1) > 0.01)
                yield return $"{at}.casing: the shares add up to {parts.Sum(p => p!.Value):0.###}, not 1";
        }
        if (a.Kind == "star")
        {
            // a star shell (docs/battle/night.md Q5): its candle, and a time fuze to burst it
            if (a.Star is not { } star)
                yield return $"{at}.star: a star shell gives its candle (candle_share, cd_per_kg, burn_s, fall_ms)";
            else
            {
                foreach (var k in star.Extra.KeysOrEmpty())
                    yield return $"{at}.star.{k}: not a candle field (candle_share, cd_per_kg, burn_s, fall_ms)";
                if (star.CandleShare is not (> 0 and < 1))
                    yield return $"{at}.star.candle_share: a share of the mass, more than 0 up to 1";
                if (!(star.CdPerKg > 0) || !(star.BurnS > 0) || !(star.FallMs >= 0))
                    yield return $"{at}.star: cd_per_kg and burn_s more than 0, fall_ms 0 or more";
            }
            if (a.Fuze?.Position is not "time")
                yield return $"{at}.fuze.position: a star shell bursts by a time fuze (time)";
        }
        else if (a.Star is not null)
            yield return $"{at}.star: only a star shell has a candle";
        else if (a.Fuze?.Position is "time")
            yield return $"{at}.fuze.position = time: only a star shell's (base or nose)";
        if (a.Fuze is not { } z)
            yield return $"{at}.fuze: give its position (base, nose or time) and delay";
        else
        {
            foreach (var k in z.Extra.KeysOrEmpty())
                yield return $"{at}.fuze.{k}: not a fuze field (position, delay_s, start_cal, oblique, plates, scatter, blind, water_ms, water_fires)";
            if (z.Position is null || !FuzePositions.Contains(z.Position))
                yield return $"{at}.fuze.position = {Style.Quote(z.Position)}: use base, nose or time";
            if (z.DelayS is < 0 || z.StartCal is < 0 || z.WaterMs is < 0)
                yield return $"{at}.fuze: delay_s, start_cal and water_ms are 0 or more";
            if (z.Plates is { } pl && !FuzePlates.Contains(pl))
                yield return $"{at}.fuze.plates = {Style.Quote(pl)}: use single or two";
            if (!Share(z.Oblique) || !Share(z.Scatter) || !Share(z.Blind) || !Share(z.WaterFires))
                yield return $"{at}.fuze: oblique, scatter, blind and water_fires are shares, 0 to 1";
        }
    }

    /// <summary>The design with each gun battery stamped with its loadout's mean shell mass, kg (BatteryInput.ShellKg, the
    /// shares by the masses Resolve gives): what its magazines weigh (S3). Expects a valid design.</summary>
    public static Design WithShellMasses(Design design)
    {
        List<BatteryInput>? Stamp(List<BatteryInput>? list, bool main) => list?.Select(b =>
            b.CalibreMm is { } cal && b.CalibreLength is not null ? b with { ShellKg = Loadout(design, b, main).Sum(o => o.Share * o.MassKg) } : b).ToList();
        return design with { Main = Stamp(design.Main, true), Secondary = Stamp(design.Secondary, false) };
    }

    /// <summary>A loadout's entries with a share of the family's star shell (docs/battle/night.md Q5): the star's
    /// <paramref name="share"/>, the others' scaled to make room.</summary>
    public static List<ShellEntryInput> WithStar(IEnumerable<ShellEntryInput> entries, string star, double share) =>
        [.. entries.Select(e => e with { Share = Math.Round(e.Share!.Value * (1 - share), 6) }), new ShellEntryInput { Ammo = star, Share = share }];

    /// <summary>A battery's loadout: its shells (block, name, share, mass, its own velocity and fuze delay if given), or
    /// the default loadout's.</summary>
    static List<(AmmoInput Block, string Name, double Share, double MassKg, double? MuzzleVelocityMs, double? FuzeDelayS)> Loadout(Design design, BatteryInput b,
        bool main)
    {
        var table = design.Ammunition ?? [];
        double d = b.CalibreMm!.Value / 1000;
        return b.Shells is { Count: > 0 } shells
            ? shells.Select(e => (table[e.Ammo!], e.Ammo!, e.Share!.Value, e.MassKg ?? table[e.Ammo!].MassKKgM3!.Value * d * d * d, e.MuzzleVelocityMs,
                e.FuzeDelayS)).ToList()
            : ShellTemplates.DefaultLoadout(main).Select(o => (o.Block, o.Name, o.Share, o.Block.MassKKgM3!.Value * d * d * d, (double?)null, (double?)null))
                .ToList();
    }

    /// <summary>Each gun battery's resolved shells, main batteries first, in the design's order: its `shells`, else the
    /// default loadout. Two batteries of one gun (kind, calibre and length) that carry different shells: the first's are
    /// used, with a warning, since the sim fires one gun's shells from one battery. Expects a valid design.
    /// <para><paramref name="gun"/> is the design whose loadout sets each gun's muzzle velocity (its heaviest shell's), when
    /// that isn't this one: a loadout's (<see cref="WithLoadout"/>), so a heavier shell than the design's leaves the gun
    /// slower by the fourth-root rule, as a lighter kind leaves it faster.</para></summary>
    public static List<BatteryShells> Resolve(Design design, List<string> warnings, Design? gun = null)
    {
        var res = new List<BatteryShells>();
        foreach (var (which, list) in new[] { ("main", design.Main), ("secondary", design.Secondary) })
            for (int i = 0; i < (list?.Count ?? 0); i++)
            {
                var b = list![i];
                if (b.CalibreMm is not { } cal || b.CalibreLength is not { } len)
                    continue;
                var resolved = ResolveBattery(design, which, i, gun);
                var same = res.FirstOrDefault(r => r.Battery == which && r.CalibreMm == cal && r.CalibreLength == len);
                if (same is null)
                    res.Add(new BatteryShells(which, i, cal, len, resolved));
                else if (!same.Shells.SequenceEqual(resolved))
                    warnings.Add($"{which}[{i}]: the same gun as {which}[{same.Index}] with other shells; one gun fires one loadout, so " +
                                 $"{which}[{same.Index}]'s are used");
            }
        return res;
    }

    /// <summary>One gun battery's resolved shells (main or secondary, its index; it has a calibre and length), in its
    /// loadout's order: what <see cref="Resolve"/> reports for it, and the designer shows.</summary>
    public static List<ShellReport> ResolveBattery(Design design, string which, int i, Design? gun = null)
    {
        var b = (which == "main" ? design.Main : design.Secondary)![i];
        var loadout = Loadout(design, b, which == "main");
        var masses = loadout.Select(e => e.MassKg).ToArray();
        var anchor = gun is null ? loadout : Loadout(gun, (which == "main" ? gun.Main : gun.Secondary)![i], which == "main");
        double heavyKg = anchor.Max(e => e.MassKg), heavyMs = b.MuzzleVelocityMs ?? DefaultMuzzleVelocity(b.CalibreLength!.Value);
        var resolved = new List<ShellReport>();
        for (int k = 0; k < loadout.Count; k++)
        {
            var (a, name, share, _, mv, delay) = loadout[k];
            var x = Explosives.Resolve(design, a.Filler!.Explosive)!;
            resolved.Add(new ShellReport
            {
                Ammo = name, Kind = a.Kind!, Share = share, MassKg = masses[k], MuzzleVelocityMs = mv ?? MuzzleVelocity(heavyMs, heavyKg, masses[k]),
                FormFactor = a.FormFactor!.Value, Cap = a.Cap?.Kind ?? "none", CapShare = a.Cap?.Share ?? 0, BreakObDeg = a.Body?.BreakObDeg ?? 90,
                BreakTD = a.Body?.BreakTD ?? 0, BodyQuality = a.Body?.Quality ?? 1, FillerKg = masses[k] * a.Filler.Share!.Value,
                Explosive = x.Name ?? "", FillerRe = x.Re ?? 1, Booster = a.Filler.Booster ?? "basic",
                FillerQDet = x.QDet ?? 4.5, FillerQComb = x.QComb ?? 15, FillerAirM3Kg = x.AirM3Kg ?? 2.66, FillerEta = x.Eta ?? 1,
                FillerInPlate = x.InPlate ?? 0, FillerFire = x.Fire ?? 0.25, FillerFull = (a.Filler.Booster == "ce" ? x.Full?.Ce : x.Full?.Basic) ?? 1,
                Fuze = a.Fuze!.Position!, FuzeDelayS = delay ?? a.Fuze.DelayS ?? 0, FuzeStartCal = a.Fuze.StartCal ?? FuzeDefaults.StartCal,
                FuzeOblique = a.Fuze.Oblique ?? FuzeDefaults.Oblique, FuzePlates = a.Fuze.Plates ?? "single",
                FuzeScatter = a.Fuze.Scatter ?? FuzeDefaults.Scatter, FuzeBlind = a.Fuze.Blind ?? FuzeDefaults.Blind,
                FuzeWaterMs = a.Fuze.WaterMs ?? FuzeDefaults.WaterMs, FuzeWaterFires = a.Fuze.WaterFires ?? FuzeDefaults.WaterFires,
                GurneyMs = x.GurneyMs ?? 2438, Brisance = x.Brisance ?? 1, SteelSize = a.Steel?.Size ?? 1, SteelSpeed = a.Steel?.Speed ?? 1,
                Casing = Casing(a), FillerBody = a.Filler.Body ?? ShellTemplates.FillerBodyOf(a.Kind!),
                StarCd = a.Star is { } st ? masses[k] * st.CandleShare!.Value * st.CdPerKg!.Value : null, StarBurnS = a.Star?.BurnS,
                StarFallMs = a.Star?.FallMs,
            });
        }
        return resolved;
    }

    /// <summary>A block's casing shares, its kind's (ShellTemplates.CasingOf) where it gives none.</summary>
    static CasingReport Casing(AmmoInput a)
    {
        var k = ShellTemplates.CasingOf(a.Kind!);
        var c = a.Casing;
        return new CasingReport(c?.Nose ?? k.Nose!.Value, c?.Ogive ?? k.Ogive!.Value, c?.Body ?? k.Body!.Value, c?.Plug ?? k.Plug!.Value,
            c?.Tail ?? k.Tail!.Value);
    }

    /// <summary>A loadout (docs/battle/shells-armour-damage.md S5, decision 6): the design with the given batteries'
    /// `shells` in place of its own. A battery is "main" or "secondary" (all of that kind) or "main[i]", "secondary[i]"
    /// (one, by its index in the design). Each entry's ammo is a block of <paramref name="blocks"/> (given with the
    /// loadout), of the design's own table, or a catalogue template by name (copied in, as the designer copies them); or
    /// a kind (ap, common, he): the battery's own block of it, else the design's first of it. Only the shells change:
    /// the guns, the magazines' rounds and the armour stay the design's, and nothing checks that a shell suits the gun
    /// beyond its calibre (memory: allow silliness). Null, with the errors, when a battery or block isn't there or the
    /// loaded design doesn't validate. Resolve the result with <c>gun: design</c>.</summary>
    public static Design? WithLoadout(Design design, IReadOnlyList<(string Battery, List<ShellEntryInput> Shells)> batteries,
        IReadOnlyDictionary<string, AmmoInput>? blocks, List<string> errors)
    {
        int had = errors.Count;
        var table = new Dictionary<string, AmmoInput>(design.Ammunition ?? [], StringComparer.Ordinal);
        var main = design.Main?.ToList();
        var secondary = design.Secondary?.ToList();
        foreach (var (key, shells) in batteries)
        {
            string at = $"loadout.{key}";
            int open = key.IndexOf('[');
            string which = open < 0 ? key : key[..open];
            int index = -1;
            if (which is not ("main" or "secondary") || open >= 0 && (!key.EndsWith(']') ||
                !int.TryParse(key.AsSpan(open + 1, key.Length - open - 2), System.Globalization.NumberStyles.None, System.Globalization.CultureInfo.InvariantCulture, out index)))
            {
                errors.Add($"{at}: name a battery as main, secondary, main[i] or secondary[i]");
                continue;
            }
            var list = which == "main" ? main : secondary;
            if (list is not { Count: > 0 } || index >= list.Count)
            {
                errors.Add($"{at}: the design has no {(index < 0 ? which + " battery" : $"{which}[{index}]")}");
                continue;
            }
            if (shells.Count == 0)
                errors.Add($"{at}: give at least one shell");
            for (int i = 0; i < list.Count; i++)
            {
                if (index >= 0 && i != index)
                    continue;
                var mine = new List<ShellEntryInput>(shells.Count);
                foreach (var e in shells)
                {
                    string? name = e.Ammo;
                    if (name is null)
                        errors.Add($"{at}: a shell without its ammo");
                    else if (blocks?.TryGetValue(name, out var given) == true)
                        table[name] = given;
                    else if (!table.ContainsKey(name))
                    {
                        if (ShellTemplates.Get(name) is { } t)
                            table[name] = t.Value;
                        else if (Kinds.Contains(name))
                        {
                            name = OwnOfKind(list[i], which == "main", name, table);
                            if (name is null)
                                errors.Add($"{at}: the design has no {e.Ammo} shell to carry");
                        }
                        else
                            errors.Add($"{at}: no shell \"{name}\" in the loadout, the design's ammunition or the catalogue");
                    }
                    mine.Add(e with { Ammo = name });
                }
                list[i] = list[i] with { Shells = mine };
            }
        }
        if (errors.Count > had)
            return null;
        var loaded = design with { Ammunition = table, Main = main, Secondary = secondary };
        errors.AddRange(Validate(loaded).Select(e => "loadout: " + e));
        return errors.Count > had ? null : loaded;
    }

    /// <summary>A battery's own block of a kind (its loadout's, or the default loadout's when it gives none, copied into the
    /// table), else the table's first of the kind; null for none.</summary>
    static string? OwnOfKind(BatteryInput b, bool main, string kind, Dictionary<string, AmmoInput> table)
    {
        if (b.Shells is { Count: > 0 } shells)
        {
            if (shells.FirstOrDefault(e => e.Ammo is { } n && table.TryGetValue(n, out var a) && a.Kind == kind) is { Ammo: { } own })
                return own;
        }
        else if (ShellTemplates.DefaultLoadout(main).FirstOrDefault(o => o.Block.Kind == kind) is { Block: not null } d)
        {
            table[d.Name] = d.Block;
            return d.Name;
        }
        return table.FirstOrDefault(kv => kv.Value.Kind == kind).Key;
    }
}

/// <summary>A battery's resolved shells in the report: which battery (main or secondary, its index in the design) and
/// its gun, and the loadout.</summary>
public sealed record BatteryShells(string Battery, int Index, double CalibreMm, double CalibreLength, List<ShellReport> Shells);

/// <summary>One shell of a battery's loadout, resolved: its share of the rounds, mass, muzzle velocity, drag, cap (its
/// kind and share of the mass), body (the obliquity and T/D past which it breaks up on face-hardened plate, 90° and 0
/// when the block gives none, and its quality on that plate), filler (kg, the explosive, its TNT equivalent, the
/// booster, and the explosive's rates: bursting in the plate at the limit, and full detonation with this booster) and
/// fuze (position, delay, the plate that starts it in calibres at normal and its share at high obliquity, single or
/// two plates, the delay's scatter and the blinds; <see cref="FuzeDefaults"/> where the block gives none), and what its
/// fragments are made of (E1; damage/13): the explosive's Gurney constant and brisance, the steel's size and speed
/// multipliers, the casing's shares and the filler's share alongside the body (the kind's where the block gives none).</summary>
public sealed record ShellReport
{
    public required string Ammo { get; init; }
    public required string Kind { get; init; }
    public double Share { get; init; }
    public double MassKg { get; init; }
    public double MuzzleVelocityMs { get; init; }
    public double FormFactor { get; init; }
    public required string Cap { get; init; }
    public double CapShare { get; init; }
    public double BreakObDeg { get; init; }
    [JsonPropertyName("break_t_d")] public double BreakTD { get; init; }
    public double BodyQuality { get; init; }
    public double FillerKg { get; init; }
    public required string Explosive { get; init; }
    public double FillerRe { get; init; }
    /// <summary>Its explosive's heats of detonation and of full combustion (MJ/kg), the air its products need to burn
    /// (m³/kg) and its share of them that's gas (η): the blast in a closed space (damage/11 §2.4; E5).</summary>
    public double FillerQDet { get; init; }
    public double FillerQComb { get; init; }
    public double FillerAirM3Kg { get; init; }
    public double FillerEta { get; init; }
    public required string Booster { get; init; }
    public double FillerInPlate { get; init; }
    /// <summary>Its explosive's fire: how readily its burst sets what's near alight (damage/12 §4.1: black powder 1, TNT
    /// 0.25).</summary>
    public double FillerFire { get; init; }
    public double FillerFull { get; init; }
    public required string Fuze { get; init; }
    public double FuzeDelayS { get; init; }
    public double FuzeStartCal { get; init; }
    public double FuzeOblique { get; init; }
    public required string FuzePlates { get; init; }
    public double FuzeScatter { get; init; }
    public double FuzeBlind { get; init; }
    /// <summary>On the water (E7; damage/15 §6.2): how long the fuze takes to fire once the shell meets it, ms (a nose
    /// fuze's surface burst depth; a base fuze's on top of its delay), and the share that fire there at all.</summary>
    public double FuzeWaterMs { get; init; }
    public double FuzeWaterFires { get; init; }
    public double GurneyMs { get; init; }
    public double Brisance { get; init; }
    public double SteelSize { get; init; }
    public double SteelSpeed { get; init; }
    public required CasingReport Casing { get; init; }
    public double FillerBody { get; init; }
    /// <summary>A star shell's flare (docs/battle/night.md Q5): its light, cd (its candle's mass by its light a kilogram),
    /// how long it burns, s, and how fast it falls, m/s; none for any other kind.</summary>
    public double? StarCd { get; init; }
    public double? StarBurnS { get; init; }
    public double? StarFallMs { get; init; }
}

/// <summary>A shell's casing as shares of its metal: the nose slug, the ogive's spray, the body's side spray, the base
/// plug and the tail (13 §2.5).</summary>
public sealed record CasingReport(double Nose, double Ogive, double Body, double Plug, double Tail);

/// <summary>A fuze's numbers where its block gives none (armor/00 §6.2-6.3): Okun's single-plate start, 0.07 calibres at
/// normal and 0.45 of it from 61° (his formula's own), a delay scattered by a quarter (σ 20-30%), 5% blind; on the water
/// (damage/15 §2.3, §6.2 [EST]) a WWII point-detonating fuze's 0.5 ms, and all that work fire.</summary>
public static class FuzeDefaults
{
    public const double StartCal = 0.07, Oblique = 0.45, Scatter = 0.25, Blind = 0.05, WaterMs = 0.5, WaterFires = 1;
}
