using System.Text.Json;
using System.Text.Json.Nodes;

namespace Fleetwright.Shipgen;

/// <summary>The design file's format version (the top-level "version" key) and the upgrades from older ones. A file
/// without the key is version 0, from before versions. When the format changes (a key renamed, moved, or one whose
/// meaning or default changes), bump <see cref="Current"/> and add the step from the old version: it rewrites the raw
/// JSON, so it can still see keys the typed design no longer has. A new optional key with a default that keeps old
/// designs as they were needs no step. A step that can't tell what an old design meant takes the default and says so
/// in the warnings (the design's UpgradeWarnings, then its report's).</summary>
public static class DesignFormat
{
    /// <summary>The version this build writes.</summary>
    public const int Current = 12;

    /// <summary>Steps[v] upgrades a version v design to v + 1, adding what it couldn't tell to the warnings.</summary>
    static readonly Action<JsonObject, List<string>>[] Steps =
    [
        (_, _) => { },   // 0 → 1: versions began; nothing else changed
        (d, _) => MaterialsTable(d),   // 1 → 2: the materials table (docs/battle/shells-armour-damage.md P0)
        (d, _) => NoOutcomeRows(d),    // 2 → 3: the shells' outcome rows gone (P3)
        JointsAndShockGrade,           // 3 → 4: the hull's joints and the fittings' shock grade (E0)
        (d, _) => FillerBody(d),       // 4 → 5: the filler's share alongside the body (E1)
        (d, _) => FireControlFits(d),  // 5 → 6: fire control as tech blocks (docs/battle/fire-control.md F3)
        (d, _) => NoComputerT(d),      // 6 → 7: the computer's weight its block's, in a transmitting station (F4c)
        Outfit,                        // 7 → 8: what burns aboard (docs/battle/fire.md B0)
        (d, _) => GunAndTorpedoTech(d), // 8 → 9: the guns and torpedoes the design's, not the look's
        PlantKinds,                    // 9 → 10: the boilers' and engines' kinds (docs/battle/shell-contents.md C1b)
        Drainage,                      // 10 → 11: what's built in against water (docs/battle/damage-control.md DC4)
        (d, _) => Housing(d),          // 11 → 12: how the guns are housed (docs/battle/magazines.md V1)
    ];

    /// <summary>11 → 12: a battery's housing was its calibre's (a barbette from 150 mm, a gunhouse from 76, a shield under);
    /// now it's the design's (<see cref="Housings"/>). Each main and secondary battery outside a casemate gets the one its
    /// calibre gave, so it builds as before.</summary>
    static void Housing(JsonObject d)
    {
        foreach (var key in (ReadOnlySpan<string>)["main", "secondary"])
        {
            JsonNode?[] batteries = d[key] switch { JsonArray a => [.. a], JsonObject o => [o], _ => [] };
            foreach (var b in batteries)
                if (b is JsonObject bo && !bo.ContainsKey("housing") && !(bo["mount"] is JsonValue m && m.TryGetValue(out string? ms) && ms == "casemate") &&
                    bo["calibre_mm"] is JsonValue c && c.TryGetValue(out double cal))
                    bo["housing"] = Housings.Suggest(cal);
        }
    }

    /// <summary>10 → 11: the pumps, counterflooding and tightness were the sim's (one pump rate by displacement); now
    /// they're the design's drainage (DrainageTemplates). A design without one gets its navy's (look.navy) in its plant's
    /// year, else the RN Great War's, with a warning.</summary>
    static void Drainage(JsonObject d, List<string> warnings)
    {
        if (d["drainage"] is not null)
            return;
        string? tech = d["machinery"]?["tech"]?["name"] is JsonValue t && t.TryGetValue(out string? ts) ? ts : null;
        string? navy = d["look"]?["navy"] is JsonValue nv && nv.TryGetValue(out string? ns) ? ns : null;
        var template = Fittings.YearOf(tech) is int year ? DrainageTemplates.ByNavyAndYear(navy, year) : DrainageTemplates.All[2];
        if (Fittings.YearOf(tech) is null)
            warnings.Add($"drainage: no plant year to set it by (machinery.tech's name); {template.Name} assumed");
        d["drainage"] = JsonSerializer.SerializeToNode(template.Value, ShipgenJson.Default.DrainageInput);
    }

    /// <summary>9 → 10: a plant tech block says its boilers' and engines' kinds (what a shell meets in its rooms). A block
    /// without them takes what its name says (the name of the template it was picked from: Powerplant.KindsOf), else its
    /// part-load curve, and says so. A design without a block takes its style's default, which says them.</summary>
    static void PlantKinds(JsonObject d, List<string> warnings)
    {
        if (d["machinery"]?["tech"] is not JsonObject t || t.ContainsKey("boilers") && t.ContainsKey("engines"))
            return;
        static string? Str(JsonNode? n) => n is JsonValue v && v.TryGetValue(out string? s) ? s : null;
        double? bf = t["boiler_fraction"] is JsonValue b && b.TryGetValue(out double f) ? f : null;
        var (boilers, engines, guessed) = Powerplant.KindsOf(Str(t["name"]), Str(t["part_load"]), bf);
        if (!t.ContainsKey("boilers"))
            t["boilers"] = boilers;
        if (!t.ContainsKey("engines"))
            t["engines"] = engines;
        if (guessed)
            warnings.Add($"machinery.tech: its name doesn't say its boilers and engines; took {boilers} and {engines} (format 10)");
    }

    /// <summary>8 → 9: the sim worked a design's guns (reload, training, elevation, pattern, propellant) and its torpedoes
    /// by its look's era and navy; the look is the drawing's only now (the user, 2026-10-19), and they're the design's
    /// blocks (GunTemplates, TorpedoTemplates). Each battery without one names the block its look stood for, and the
    /// torpedoes theirs, and the design's gun_tech and torpedo_tech take those blocks from the catalogue, so it fights as
    /// before. No look stood for the Second War's guns, double-base, as the sim took it.</summary>
    static void GunAndTorpedoTech(JsonObject d)
    {
        string? era = d["look"]?["era"] is JsonValue e && e.TryGetValue(out string? es) ? es : null;
        string? navy = d["look"]?["navy"] is JsonValue n && n.TryGetValue(out string? ns) ? ns : null;
        string gun = GunTemplates.ForLook(era, navy);
        bool used = false;
        foreach (var key in (ReadOnlySpan<string>)["main", "secondary"])
        {
            JsonNode?[] batteries = d[key] switch { JsonArray a => [.. a], JsonObject o => [o], _ => [] };
            foreach (var b in batteries)
                if (b is JsonObject bo && !bo.ContainsKey("gun"))
                {
                    bo["gun"] = gun;
                    used = true;
                }
        }
        if (used)
            Table(d, "gun_tech", gun, JsonSerializer.SerializeToNode(GunTemplates.Get(gun)!.Value, ShipgenJson.Default.GunTechInput));
        if (d["torpedoes"] is JsonObject t && !t.ContainsKey("torpedo"))
        {
            string torpedo = TorpedoTemplates.ForLook(era);
            t["torpedo"] = torpedo;
            Table(d, "torpedo_tech", torpedo, JsonSerializer.SerializeToNode(TorpedoTemplates.Get(torpedo)!.Value, ShipgenJson.Default.TorpedoTechInput));
        }

        static void Table(JsonObject d, string key, string name, JsonNode? block)
        {
            var table = d[key] as JsonObject;
            if (table is null)
                d[key] = table = [];
            table.TryAdd(name, block);
        }
    }

    /// <summary>7 → 8: the fire loads were the sim's, by the look's era; now they're the design's outfit (Outfits). A design
    /// without one gets its navy's (look.navy) in its plant's year (machinery.tech's name ends with it), else the Great
    /// War's, with a warning.</summary>
    static void Outfit(JsonObject d, List<string> warnings)
    {
        if (d["outfit"] is not null)
            return;
        string? tech = d["machinery"]?["tech"]?["name"] is JsonValue t && t.TryGetValue(out string? ts) ? ts : null;
        string? navy = d["look"]?["navy"] is JsonValue nv && nv.TryGetValue(out string? ns) ? ns : null;
        var template = Fittings.YearOf(tech) is int year ? Outfits.ByNavyAndYear(navy, year) : Outfits.All[2];
        if (Fittings.YearOf(tech) is null)
            warnings.Add($"outfit: no plant year to set it by (machinery.tech's name); {template.Name} assumed");
        d["outfit"] = JsonSerializer.SerializeToNode(template.Value, ShipgenJson.Default.OutfitInput);
    }

    /// <summary>6 → 7: a battery's `computer_t` was the plotting room's weight, booked a director; the computer's block
    /// carries its mass now (with its crew and power), and the transmitting station holds it (fire-control.md F4c). The key
    /// goes.</summary>
    static void NoComputerT(JsonObject d)
    {
        if (d["fire_control"] is not JsonObject fc)
            return;
        foreach (var bat in (ReadOnlySpan<string>)["main", "secondary", "aa"])
            if (fc[bat] is JsonObject spec)
                spec.Remove("computer_t");
    }

    /// <summary>5 → 6: a battery's fire control was a level the sim read from its fit's tonnage, capped by the look's era
    /// (the placeholder fire-control.md F3 replaced); now it's a fit of tech blocks (FireControlTemplates). Each battery
    /// with directors and no fit gets the blocks of the preset its level was, so it fires as before, and the design's
    /// fire_control_tech the catalogue's blocks it names. The level's rule: no computer, a rangefinder (else the eye);
    /// under 1 t a clock, under 5 t the Dreyer and director, under 8 t the interwar table, more a rangekeeper; radar a
    /// rangekeeper with a rangefinder, auto-track without; never past the era's best (rangefinders before 1906, the Dreyer
    /// in the Great War, the table between the wars, radar spotting in the Second).</summary>
    static void FireControlFits(JsonObject d)
    {
        if (d["fire_control"] is not JsonObject fc)
            return;
        static double Num(JsonNode? n) => n is JsonValue v && v.TryGetValue(out double x) ? x : 0;
        string? era = d["look"]?["era"] is JsonValue e && e.TryGetValue(out string? es) ? es : null;
        int best = era switch { "victorian" => 1, "great_war" => 3, "treaty" => 4, null or "wwii" => 6, _ => 8 };
        foreach (var bat in (ReadOnlySpan<string>)["main", "secondary", "aa"])
        {
            if (fc[bat] is not JsonObject spec || spec.ContainsKey("fit") || Num(spec["directors"]) <= 0)
                continue;
            double rf = Num(spec["rangefinder_m"]), computer = Num(spec["computer_t"]);
            int level = Num(spec["radar_t"]) > 0 ? rf > 0 ? 5 : 7
                : computer <= 0 ? rf > 0 ? 1 : 0
                : computer < 1 ? 2
                : computer < 5 ? 3
                : computer < 8 ? 4
                : 5;
            var preset = FireControlTemplates.Presets[Math.Min(level, best)];
            spec["fit"] = new JsonArray([.. preset.Fit.Select(n => (JsonNode)JsonValue.Create(n)!)]);
            var table = d["fire_control_tech"] as JsonObject;
            foreach (var n in preset.Fit)
                if (table?.ContainsKey(n) != true)
                {
                    if (table is null)
                        d["fire_control_tech"] = table = [];
                    table[n] = JsonSerializer.SerializeToNode(FireControlTemplates.Get(n)!.Value, ShipgenJson.Default.FcTechInput);
                }
        }
    }

    /// <summary>4 → 5: the side spray is thrown by the body's own charge-to-metal ratio (E1; damage/13 §2.1), so each
    /// ammunition block gives its filler's share alongside the body (filler.body); a block without one takes its kind's
    /// (ShellTemplates.FillerBodyOf), as the templates have it.</summary>
    static void FillerBody(JsonObject d)
    {
        if (d["ammunition"] is not JsonObject table)
            return;
        foreach (var (_, node) in table)
            if (node is JsonObject a && a["filler"] is JsonObject f && !f.ContainsKey("body") &&
                a["kind"] is JsonValue k && k.TryGetValue(out string? kind))
                f["body"] = ShellTemplates.FillerBodyOf(kind);
    }

    /// <summary>3 → 4: the hull's joints (hull.construction.shell and .internals) and the fittings' shock grade
    /// (fittings.shock_grade) are design numbers now (Fittings). A construction's joints come from its name, as the
    /// hull templates name them; a design with no construction takes the default block's (welded) and needs none. The
    /// grade is the one its navy (look.navy) fitted to in its plant's year (machinery.tech's name ends with it), unsecured
    /// on a merchant hull. What neither says is left to the default (welded, G0) with a warning.</summary>
    static void JointsAndShockGrade(JsonObject d, List<string> warnings)
    {
        if (d["hull"]?["construction"] is JsonObject c && !(c.ContainsKey("shell") && c.ContainsKey("internals")))
        {
            string? name = c["name"] is JsonValue n && n.TryGetValue(out string? s) ? s : null;
            if (Fittings.JointsFromName(name) is var (shell, internals))
            {
                c.TryAdd("shell", shell.Name());
                c.TryAdd("internals", internals.Name());
            }
            else
                warnings.Add($"hull.construction: its name ({(name is null ? "none" : $"\"{name}\"")}) doesn't say riveted or " +
                             "welded; welded assumed (give shell and internals)");
        }
        if (d["fittings"]?["shock_grade"] is not null)
            return;
        string? tech = d["machinery"]?["tech"]?["name"] is JsonValue t && t.TryGetValue(out string? ts) ? ts : null;
        bool merchant = d["style"] is JsonValue st && st.TryGetValue(out string? style) && style == "merchant";
        string? navy = d["look"]?["navy"] is JsonValue nv && nv.TryGetValue(out string? ns) ? ns : null;
        if (Fittings.YearOf(tech) is not int year)
        {
            warnings.Add("fittings.shock_grade: no plant year to set it by (machinery.tech's name); G0 assumed");
            return;
        }
        var fittings = d["fittings"] as JsonObject;
        if (fittings is null)
            d["fittings"] = fittings = [];
        fittings["shock_grade"] = Fittings.ByNavyAndYear(navy, year, merchant).ToString();
    }

    /// <summary>2 → 3: an ammunition block named the sim's outcome-table row (`outcomes`, S0's transitional field); its
    /// fuze's blinds and its explosive's rates replace it (P3). The field goes, and a block whose fuze gives no `blind`
    /// takes its row's dud share (the rows' last numbers: rn1916 0.05, km1916 0.15, rn1918 0.10, ww2 0.08, ijn_t91 0.13).
    /// The loadout blocks in a scenario aren't designs; they're read as given.</summary>
    static void NoOutcomeRows(JsonObject d)
    {
        if (d["ammunition"] is not JsonObject table)
            return;
        foreach (var (_, node) in table)
        {
            if (node is not JsonObject a || a["outcomes"] is not JsonValue row)
                continue;
            a.Remove("outcomes");
            double dud = row.TryGetValue(out string? name) ? name switch
            {
                "km1916" => 0.15, "rn1918" => 0.10, "ww2" => 0.08, "ijn_t91" => 0.13, _ => 0.05,
            } : 0.05;
            if (a["fuze"] is JsonObject fuze && !fuze.ContainsKey("blind"))
                fuze["blind"] = dud;
        }
    }

    /// <summary>1 → 2: a material was a name the game looked up; now it's the design's block (MaterialTemplates).
    /// Each name the parts give (any "material" or "deck_material", and armour.materials' values) gets the catalogue's
    /// block in `materials`, where the catalogue knows it and the design doesn't have one. A version 1 design has no
    /// other "material" keys, so the walk needn't know the parts.</summary>
    static void MaterialsTable(JsonObject d)
    {
        var names = new SortedSet<string>(StringComparer.Ordinal);
        void Walk(JsonNode? n)
        {
            switch (n)
            {
                case JsonObject o:
                    foreach (var (k, v) in o)
                        if (k is "material" or "deck_material" && v is JsonValue s && s.TryGetValue(out string? name) && name.Length > 0)
                            names.Add(name);
                        else
                            Walk(v);
                    break;
                case JsonArray a:
                    foreach (var v in a)
                        Walk(v);
                    break;
            }
        }
        Walk(d);
        if (d["armour"]?["materials"] is JsonObject parts)
            foreach (var (_, v) in parts)
                if (v is JsonValue s && s.TryGetValue(out string? name) && name.Length > 0)
                    names.Add(name);
        var table = d["materials"] as JsonObject;
        foreach (var n in names)
            if (MaterialTemplates.Get(n) is { } m && table?.ContainsKey(n) != true)
            {
                if (table is null)
                    d["materials"] = table = [];
                table[n] = JsonSerializer.SerializeToNode(m, ShipgenJson.Default.MaterialInput);
            }
    }

    static readonly JsonDocumentOptions ReadOptions = new() { CommentHandling = JsonCommentHandling.Skip, AllowTrailingCommas = true };

    /// <summary>The design JSON as this build reads it: older versions upgraded to <see cref="Current"/>, a newer one
    /// left as it is (<see cref="Warnings"/> says so). warnings gets what the upgrade had to assume.</summary>
    public static JsonObject Upgrade(string json, List<string>? warnings = null)
    {
        var d = JsonNode.Parse(json, documentOptions: ReadOptions) as JsonObject
            ?? throw new JsonException("a design is a JSON object");
        int v = Version(d);
        if (v < Current)
        {
            for (; v < Current; v++)
                Steps[v](d, warnings ?? []);
            d["version"] = Current;
        }
        return d;
    }

    static int Version(JsonObject d)
    {
        if (d["version"] is not JsonValue n)
            return 0;
        if (!n.TryGetValue(out int v) || v < 0)
            throw new JsonException($"version: {n.ToJsonString()} isn't a design format version (a whole number, 0 or more)");
        return v;
    }

    /// <summary>The build's warning for a design from a newer Fleetwright: it loads, but what this one doesn't know is
    /// ignored.</summary>
    public static string[] Warnings(Design d) => d.Version > Current
        ? [$"Made with a newer Fleetwright (design format {d.Version}, this one reads up to {Current}): what it doesn't know is ignored."]
        : [.. d.UpgradeWarnings ?? []];
}
