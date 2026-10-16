namespace Fleetwright.Shipgen;

/// <summary>The shells as design data (docs/battle/shells-armour-damage.md phase S): the design's `ammunition` table of
/// shell tech blocks, calibre-free like machinery.tech, and each battery's outfit (`shells`) naming them. Validated
/// here and resolved per battery into the report (kind, mass, muzzle velocity, filler, fuze), which the sim's compiler
/// reads. A battery that gives no shells is left out: the sim fills it from its placeholder until the default
/// template (S1).</summary>
public static class Ammunition
{
    public static readonly string[] Kinds = ["ap", "common", "he"];
    public static readonly string[] Caps = ["none", "soft", "hard", "hard2", "break_away"];
    public static readonly string[] FuzePositions = ["base", "nose"];
    /// <summary>TRANSITIONAL (S0 to P3): the sim's outcome-table rows (gunfire_ref.py OUTCOME), until the fuze's and
    /// filler's own rates replace them.</summary>
    public static readonly string[] OutcomeRows = ["rn1916", "km1916", "rn1918", "ww2", "ijn_t91"];

    static readonly string[] BlockFields = ["kind", "mass_k_kg_m3", "form_factor", "cap", "filler", "fuze", "outcomes"];
    static readonly string[] EntryFields = ["ammo", "share", "mass_kg", "muzzle_velocity_ms"];

    /// <summary>PLACEHOLDER (until a gun tech exists): a gun's muzzle velocity with its heaviest shell from its length
    /// in calibres, 280 + 11·L m/s kept to 380-1000, from the Victorian muzzle-loaders (12"/14 RML ~400 m/s) to the
    /// 76/62's 925: the 12"/45 Mk X 831 against 775, the 15"/42 749 against 742.</summary>
    public static double DefaultMuzzleVelocity(double calibreLength) => Math.Clamp(280 + 11 * calibreLength, 380, 1000);

    /// <summary>A lighter shell from the same gun goes faster by the masses' fourth root: (m_heavy/m)^0.25.</summary>
    public static double MuzzleVelocity(double heavyMs, double heavyKg, double kg) => heavyMs * Math.Pow(heavyKg / kg, 0.25);

    public static List<string> Validate(Design design)
    {
        var errs = new List<string>();
        var table = design.Ammunition ?? [];
        foreach (var (name, a) in table)
        {
            string at = $"ammunition.\"{name}\"";
            if (a is null)
            {
                errs.Add($"{at}: give the block's numbers");
                continue;
            }
            errs.AddRange(a.Extra.KeysOrEmpty().Select(k => $"{at}.{k}: not a shell field ({string.Join(", ", BlockFields)})"));
            if (a.Kind is null || !Kinds.Contains(a.Kind))
                errs.Add($"{at}.kind = {Style.Quote(a.Kind)}: use {string.Join(", ", Kinds)}");
            if (!(a.MassKKgM3 > 0))
                errs.Add($"{at}.mass_k_kg_m3: give the shell's mass over D³ (kg/m³, D in m), more than 0");
            if (!(a.FormFactor > 0))
                errs.Add($"{at}.form_factor: give the drag form factor, more than 0");
            if (a.Cap is { } cap)
            {
                errs.AddRange(cap.Extra.KeysOrEmpty().Select(k => $"{at}.cap.{k}: not a cap field (kind, share)"));
                if (cap.Kind is null || !Caps.Contains(cap.Kind))
                    errs.Add($"{at}.cap.kind = {Style.Quote(cap.Kind)}: use {string.Join(", ", Caps)}");
                if (cap.Share is < 0 or >= 1)
                    errs.Add($"{at}.cap.share: a share of the mass, 0 up to 1");
            }
            if (a.Filler is not { } f)
                errs.Add($"{at}.filler: give the filler's share of the mass and its explosive");
            else
            {
                errs.AddRange(f.Extra.KeysOrEmpty().Select(k => $"{at}.filler.{k}: not a filler field (share, explosive)"));
                if (f.Share is not (>= 0 and < 1))
                    errs.Add($"{at}.filler.share: a share of the mass, 0 up to 1");
                if (f.Explosive is { } x)
                {
                    errs.AddRange(x.Extra.KeysOrEmpty().Select(k => $"{at}.filler.explosive.{k}: not an explosive field (re)"));
                    if (x.Re is < 0)
                        errs.Add($"{at}.filler.explosive.re: its TNT equivalent, 0 or more");
                }
            }
            if (a.Fuze is not { } z)
                errs.Add($"{at}.fuze: give its position (base or nose) and delay");
            else
            {
                errs.AddRange(z.Extra.KeysOrEmpty().Select(k => $"{at}.fuze.{k}: not a fuze field (position, delay_s)"));
                if (z.Position is null || !FuzePositions.Contains(z.Position))
                    errs.Add($"{at}.fuze.position = {Style.Quote(z.Position)}: use base or nose");
                if (z.DelayS is < 0)
                    errs.Add($"{at}.fuze.delay_s: seconds, 0 or more");
            }
            if (a.Outcomes is null || !OutcomeRows.Contains(a.Outcomes))
                errs.Add($"{at}.outcomes = {Style.Quote(a.Outcomes)}: use {string.Join(", ", OutcomeRows)}");
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
                }
                if (shells.Count > 0 && Math.Abs(sum - 1) > 1e-6)
                    errs.Add($"{at}.shells: the shares add up to {sum:0.###}, not 1");
            }
        return errs;
    }

    /// <summary>Each battery's resolved shells, main batteries first, in the design's order: those that give `shells`.
    /// Two batteries of one gun (kind, calibre and length) that carry different shells: the first's are used, with a
    /// warning, since the sim fires one gun's shells from one battery. Expects a valid design.</summary>
    public static List<BatteryShells> Resolve(Design design, List<string> warnings)
    {
        var res = new List<BatteryShells>();
        var table = design.Ammunition ?? [];
        foreach (var (which, list) in new[] { ("main", design.Main), ("secondary", design.Secondary) })
            for (int i = 0; i < (list?.Count ?? 0); i++)
            {
                var b = list![i];
                if (b.Shells is not { Count: > 0 } shells || b.CalibreMm is not { } cal || b.CalibreLength is not { } len)
                    continue;
                double d = cal / 1000;
                var masses = shells.Select(e => e.MassKg ?? table[e.Ammo!].MassKKgM3!.Value * d * d * d).ToArray();
                double heavyKg = masses.Max(), heavyMs = b.MuzzleVelocityMs ?? DefaultMuzzleVelocity(len);
                var resolved = new List<ShellReport>();
                for (int k = 0; k < shells.Count; k++)
                {
                    var e = shells[k];
                    var a = table[e.Ammo!];
                    resolved.Add(new ShellReport
                    {
                        Ammo = e.Ammo!, Kind = a.Kind!, Share = e.Share!.Value, MassKg = masses[k],
                        MuzzleVelocityMs = e.MuzzleVelocityMs ?? MuzzleVelocity(heavyMs, heavyKg, masses[k]), FormFactor = a.FormFactor!.Value,
                        Cap = a.Cap?.Kind ?? "none", FillerKg = masses[k] * a.Filler!.Share!.Value, FillerRe = a.Filler.Explosive?.Re ?? 1,
                        Fuze = a.Fuze!.Position!, FuzeDelayS = a.Fuze.DelayS ?? 0, Outcomes = a.Outcomes!,
                    });
                }
                var same = res.FirstOrDefault(r => r.Battery == which && r.CalibreMm == cal && r.CalibreLength == len);
                if (same is null)
                    res.Add(new BatteryShells(which, i, cal, len, resolved));
                else if (!same.Shells.SequenceEqual(resolved))
                    warnings.Add($"{which}[{i}]: the same gun as {which}[{same.Index}] with other shells; one gun fires one outfit, so " +
                                 $"{which}[{same.Index}]'s are used");
            }
        return res;
    }
}

/// <summary>A battery's resolved shells in the report: which battery (main or secondary, its index in the design) and
/// its gun, and the outfit.</summary>
public sealed record BatteryShells(string Battery, int Index, double CalibreMm, double CalibreLength, List<ShellReport> Shells);

/// <summary>One shell of a battery's outfit, resolved: its share of the rounds, mass, muzzle velocity, drag, cap,
/// filler (kg and TNT equivalent), fuze and (transitional) outcome row.</summary>
public sealed record ShellReport
{
    public required string Ammo { get; init; }
    public required string Kind { get; init; }
    public double Share { get; init; }
    public double MassKg { get; init; }
    public double MuzzleVelocityMs { get; init; }
    public double FormFactor { get; init; }
    public required string Cap { get; init; }
    public double FillerKg { get; init; }
    public double FillerRe { get; init; }
    public required string Fuze { get; init; }
    public double FuzeDelayS { get; init; }
    public required string Outcomes { get; init; }
}
