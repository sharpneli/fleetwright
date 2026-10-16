using System.Globalization;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace Fleetwright.Shipgen.Cli;

/// <summary>`shipgen templates`: the shell and explosive catalogues (ShellTemplates, Explosives: their one source)
/// written out as the game's template assets, `Content/Templates/shells.json` and `explosives.json` (entries {name,
/// group, note, value}, as tools/designer_templates.py writes the others), and as the docs' pages,
/// docs/shipgen/shell-templates.md and explosive-templates.md. Never edit those by hand.</summary>
static class TemplatesWriter
{
    static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

    public static int Run(string repo)
    {
        var json = new JsonSerializerOptions
        {
            WriteIndented = true, IndentSize = 1, NewLine = "\n", Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        };
        JsonNode Value<T>(T v) => JsonNode.Parse(JsonSerializer.Serialize(v, ShipgenJson.Default.Options.GetTypeInfo(typeof(T))))!;
        var shells = new JsonArray(ShellTemplates.All.Select(t => (JsonNode)new JsonObject
        {
            ["name"] = t.Name, ["group"] = t.Group, ["note"] = t.Note, ["value"] = Value(t.Value),
        }).ToArray());
        var explosives = new JsonArray(Explosives.All.Select(e => (JsonNode)new JsonObject
        {
            ["name"] = e.Value.Name, ["group"] = e.Value.From!.Value.ToString(Inv), ["note"] = e.Note, ["value"] = Value(e.Value),
        }).ToArray());
        Write(Path.Combine(repo, "Content", "Templates", "shells.json"), shells.ToJsonString(json) + "\n");
        Write(Path.Combine(repo, "Content", "Templates", "explosives.json"), explosives.ToJsonString(json) + "\n");
        Write(Path.Combine(repo, "docs", "shipgen", "shell-templates.md"), ShellPage());
        Write(Path.Combine(repo, "docs", "shipgen", "explosive-templates.md"), ExplosivePage());
        return 0;
    }

    static void Write(string path, string text)
    {
        File.WriteAllText(path, text);
        Console.WriteLine($"wrote {path}");
    }

    static string N(double? v, string f = "0.###") => v is { } x ? x.ToString(f, Inv) : "-";

    static string ShellPage()
    {
        var sb = new StringBuilder();
        sb.Append("""
            # Shell templates

            Written by `shipgen templates` from `src/Fleetwright.Shipgen/ShellTemplates.cs`; don't edit by hand. The game's
            loadout catalogue and the designer's templates are `Content/Templates/shells.json`, from the same table.

            A template is an `ammunition` block (docs/shipgen/README.md): calibre-free, so a navy's family of guns shares it.
            Mass is k·D³ (D in m); k is shown here also as W/D³ in lb/in³, the literature's unit (1 lb/in³ = 27,680 kg/m³).
            Shares are of the shell's mass, thresholds in calibres. A design copies the blocks it uses; a battery that gives
            no shells carries the default outfit of the **1912 standard** family (main 70% AP / 30% HE, secondary 30% / 70%).
            Light guns (under 200 mm) of a family take its light blocks where it has them. Notes cite the research
            (`research/simulation/armor/04-shells-fuzes.md` §1.7 and §3.5, `armor/00` §5.4, `damage/13` §2.5 and §3.2); [EST]
            marks numbers no source gives.

            Columns: kind; W/D³; nose crh and drag form factor; cap (share); body break-up (obliquity °, T/D) and quality;
            filler (share, explosive, booster); fuze (position, delay s, start calibres, plates, delay scatter, blinds); steel
            (fragment size, speed); the transitional outcome row.

            """.Replace("\r\n", "\n"));
        foreach (var family in ShellTemplates.All.GroupBy(t => t.Group))
        {
            sb.Append("\n## ").Append(family.Key).Append("\n\n");
            sb.Append("| Template | Kind | W/D³ | crh / i | Cap | Body | Filler | Fuze | Steel | Row |\n");
            sb.Append("|---|---|---|---|---|---|---|---|---|---|\n");
            foreach (var t in family)
            {
                var a = t.Value;
                string cap = a.Cap?.Kind is null or "none" ? "none" : $"{a.Cap.Kind} {N(a.Cap.Share * 100, "0.#")}%";
                var z = a.Fuze!;
                string fuze = z.Position == "nose" ? "nose" : $"base {N(z.DelayS)} s, {N(z.StartCal)} cal, {z.Plates}, ±{N(z.Scatter * 100, "0")}%, {N(z.Blind * 100, "0")}% blind";
                sb.Append(CultureInfo.InvariantCulture, $"| {t.Name}{(t.Light ? " (light)" : "")} | {a.Kind} | {N(a.MassKKgM3 / 27_679.9, "0.00")} | {N(a.NoseCrh, "0.#")} / {N(a.FormFactor, "0.00")} | {cap} | " +
                    $"{N(a.Body?.BreakObDeg, "0")}°, {N(a.Body?.BreakTD, "0.##")}; {N(a.Body?.Quality, "0.00")} | {N(a.Filler!.Share * 100, "0.##")}% {a.Filler.Explosive?.Name}, {a.Filler.Booster} | " +
                    $"{fuze} | {N(a.Steel?.Size, "0.##")}/{N(a.Steel?.Speed, "0.##")} | {a.Outcomes} |\n");
            }
            sb.Append('\n');
            foreach (var t in family)
                sb.Append("- **").Append(t.Name).Append(":** ").Append(t.Note).Append('\n');
        }
        return sb.ToString();
    }

    static string ExplosivePage()
    {
        var sb = new StringBuilder();
        sb.Append("""
            # Explosive templates

            Written by `shipgen templates` from `src/Fleetwright.Shipgen/Explosives.cs`; don't edit by hand. The game's catalogue
            is `Content/Templates/explosives.json`, from the same table.

            A shell's `filler.explosive` names one; a design copies the ones it names into its `explosives`, and a filler can
            override any number inline. Sources: `docs/battle/damage/12_explosive_fillers.md` §4.1 (re, in-plate, full
            detonation, fire), `11_blast_confinement.md` §2.4 (heats, air, eta), `13_fragmentation.md` §2.1 and §3.1 (Gurney,
            brisance). The sim reads `re` (the blast: W = filler kg × re) from S1 on; the rest ride along for P3 and phase E.

            | Explosive | From | re | Q det / comb MJ/kg | Air m³/kg | eta | Gurney m/s | Brisance | In plate | Full basic / CE | Fire |
            |---|---|---|---|---|---|---|---|---|---|---|

            """.Replace("\r\n", "\n"));
        foreach (var e in Explosives.All)
        {
            var x = e.Value;
            sb.Append(CultureInfo.InvariantCulture, $"| {x.Name} | {x.From} | {N(x.Re, "0.00")} | {N(x.QDet, "0.0")} / {N(x.QComb, "0.0")} | {N(x.AirM3Kg, "0.00")} | " +
                $"{N(x.Eta, "0.00")} | {N(x.GurneyMs, "0")} | {N(x.Brisance, "0.00")} | {N(x.InPlate, "0.00")} | {N(x.Full?.Basic, "0.00")} / {N(x.Full?.Ce, "0.00")} | {N(x.Fire, "0.00")} |\n");
        }
        sb.Append('\n');
        foreach (var e in Explosives.All)
            sb.Append("- **").Append(e.Value.Name).Append(":** ").Append(e.Note).Append('\n');
        return sb.ToString();
    }
}
