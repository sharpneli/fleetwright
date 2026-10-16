using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Nodes;
using Fleetwright.Sim.Ships;

namespace Fleetwright.Shipgen.Cli;

/// <summary>`shipgen ammo-parity FILE...`: S0 of docs/battle/shells-armour-damage.md. Writes each design's shells as
/// data from today's placeholder (DesignerPlaceholders.Shell and ShellMix, by the look's era and navy), so no number
/// moves: an `ammunition` block per kind, era and outcome row, and each battery's `shells` with its own `mass_kg` (the
/// placeholder's k varies with calibre, so one block's k can't hold it). The muzzle velocity is left out (the default
/// gives the placeholder's). Then compiles the design before and after and fails unless every battery's shells, shares,
/// fired type and the layout hash are the same. A design that already gives shells is skipped. S1's templates replace
/// what it writes.</summary>
static class AmmoParity
{
    public static int Run(IEnumerable<string> paths)
    {
        int written = 0, skipped = 0, failed = 0;
        foreach (var p in paths)
        {
            string text = File.ReadAllText(p);
            var design = Design.Parse(text);
            if (design.Ammunition != null || design.MainBatteries.Concat(design.Secondary ?? []).Any(b => b.Shells != null)
                || !design.MainBatteries.Concat(design.Secondary ?? []).Any())
            {
                skipped++;
                continue;
            }
            string after = Migrate(text, design);
            var problems = Compare(design, Design.Parse(after));
            if (problems.Count > 0)
            {
                failed++;
                Console.WriteLine($"{p}: NOT written");
                foreach (var x in problems)
                    Console.WriteLine("  " + x);
                continue;
            }
            File.WriteAllText(p, after);
            written++;
        }
        Console.WriteLine($"{written} written, {skipped} skipped, {failed} failed parity");
        return failed > 0 ? 1 : 0;
    }

    static string Migrate(string text, Design design)
    {
        var era = design.Look?.Era switch
        {
            "victorian" => Era.Victorian,
            "great_war" => Era.GreatWar,
            "treaty" => Era.Treaty,
            "cold_war" => Era.ColdWar,
            _ => Era.Wwii,
        };
        string navy = design.Look?.Navy ?? "generic";
        var blocks = new JsonObject();
        JsonArray ShellsOf(BatteryInput b)
        {
            double cal = b.CalibreMm!.Value, len = b.CalibreLength!.Value, d = cal / 1000;
            var list = new JsonArray();
            foreach (var (kind, share) in DesignerPlaceholders.ShellMix(cal, era))
            {
                var t = DesignerPlaceholders.Shell(kind, cal, len, era, navy);
                string name = $"{KindName(kind)} {EraName(era)} {QualityName(t.Quality)}";
                if (!blocks.ContainsKey(name))
                {
                    var block = new JsonObject
                    {
                        ["kind"] = KindName(kind).ToLowerInvariant(), ["mass_k_kg_m3"] = Math.Round(t.MassKg / (d * d * d)), ["form_factor"] = t.FormFactor,
                    };
                    if (t.Diving)
                        block["cap"] = new JsonObject { ["kind"] = "break_away" };
                    block["filler"] = new JsonObject { ["share"] = Math.Round(t.FillerKg / t.MassKg, 4), ["explosive"] = new JsonObject { ["re"] = 1 } };
                    block["fuze"] = new JsonObject { ["position"] = t.Fuze == Fuze.Nose ? "nose" : "base", ["delay_s"] = t.FuzeDelayS };
                    block["outcomes"] = QualityName(t.Quality);
                    blocks[name] = block;
                }
                list.Add((JsonNode)new JsonObject { ["ammo"] = name, ["share"] = share, ["mass_kg"] = t.MassKg });
            }
            return list;
        }

        // the batteries' shells, in the file's order, then the table they name
        var shells = new List<JsonArray>();
        foreach (var b in design.MainBatteries.Concat(design.Secondary ?? []))
            shells.Add(ShellsOf(b));
        string nl = text.Contains("\r\n") ? "\r\n" : "\n";
        return Pretty(text, nl) is { } indent ? Rewrite(text, shells, blocks, indent, nl) : Insert(text, shells, blocks, nl);
    }

    static readonly JsonSerializerOptions Compact = new() { Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping };

    /// <summary>The indent of a file written by an indented writer (a scenario's designs), when re-writing it as it is
    /// gives it back byte for byte; null for the shipped designs' hand-laid lines.</summary>
    static int? Pretty(string text, string nl)
    {
        var node = JsonNode.Parse(text)!;
        foreach (int indent in new[] { 1, 2 })
            if (Write(node, indent, nl) == text)
                return indent;
        return null;
    }

    static string Write(JsonNode node, int indent, string nl) => node.ToJsonString(new JsonSerializerOptions
    {
        WriteIndented = true, IndentSize = indent, NewLine = nl, Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    });

    static string Rewrite(string text, List<JsonArray> shells, JsonObject blocks, int indent, string nl)
    {
        var node = JsonNode.Parse(text)!.AsObject();
        int k = 0;
        foreach (var key in new[] { "main", "secondary" })
            switch (node[key])
            {
                case JsonArray a:
                    foreach (var b in a)
                        b!.AsObject()["shells"] = shells[k++];
                    break;
                case JsonObject o:
                    o["shells"] = shells[k++];
                    break;
            }
        node["ammunition"] = blocks;
        return Write(node, indent, nl);
    }

    /// <summary>Into the shipped designs' lines: each battery's shells at the end of its object, the table on a line of
    /// its own at the end.</summary>
    static string Insert(string text, List<JsonArray> shells, JsonObject blocks, string nl)
    {
        var bytes = Encoding.UTF8.GetBytes(text);
        var reader = new Utf8JsonReader(bytes, new JsonReaderOptions { CommentHandling = JsonCommentHandling.Skip, AllowTrailingCommas = true });
        var ends = new List<int>();
        int top = -1, depth = 0;
        string? key = null;
        bool single = false;
        while (reader.Read())
            switch (reader.TokenType)
            {
                case JsonTokenType.PropertyName when depth == 1:
                    key = reader.GetString();
                    break;
                case JsonTokenType.StartObject or JsonTokenType.StartArray:
                    if (depth == 1)
                        single = reader.TokenType == JsonTokenType.StartObject;
                    depth++;
                    break;
                case JsonTokenType.EndObject or JsonTokenType.EndArray:
                    if (reader.TokenType == JsonTokenType.EndObject && key is "main" or "secondary" && depth == (single ? 2 : 3))
                        ends.Add((int)reader.TokenStartIndex);
                    if (depth == 1)
                        top = (int)reader.TokenStartIndex;
                    depth--;
                    break;
            }
        if (ends.Count != shells.Count)
            throw new InvalidOperationException($"found {ends.Count} battery objects for {shells.Count} batteries");
        var sb = new StringBuilder();
        int at = 0;
        void Up(int to, string add)
        {
            // byte offsets: the designs are ASCII, so they're char offsets too
            string head = text[at..to];
            string trimmed = head.TrimEnd();
            sb.Append(trimmed).Append(add).Append(head[trimmed.Length..]);
            at = to;
        }
        for (int i = 0; i < ends.Count; i++)
            Up(ends[i], ", \"shells\": " + shells[i].ToJsonString(Compact).Replace(",\"", ", \"").Replace("\":", "\": ").Replace("},{", "}, {"));
        Up(top, "," + nl + " \"ammunition\": " + blocks.ToJsonString(Compact).Replace(",\"", ", \"").Replace("\":", "\": ").Replace("},{", "}, {"));
        sb.Append(text[at..]);
        if (Encoding.UTF8.GetByteCount(text) != text.Length)
            throw new InvalidOperationException("not an ASCII design");
        return sb.ToString();
    }

    /// <summary>What differs between the two designs' compiled shells.</summary>
    static List<string> Compare(Design before, Design after)
    {
        var a = ShipClassCompiler.Compile(ShipDesign.Build(before));
        var b = ShipClassCompiler.Compile(ShipDesign.Build(after));
        var p = new List<string>();
        if (a.Batteries.Length != b.Batteries.Length)
            return [$"{a.Batteries.Length} batteries before, {b.Batteries.Length} after"];
        for (int i = 0; i < a.Batteries.Length; i++)
        {
            var (x, y) = (a.Batteries[i], b.Batteries[i]);
            var (lx, ly) = (a.ShellLoads(x.Shells).ToArray(), b.ShellLoads(y.Shells).ToArray());
            if (lx.Length != ly.Length)
                p.Add($"battery {i}: {lx.Length} shells before, {ly.Length} after");
            for (int k = 0; k < Math.Min(lx.Length, ly.Length); k++)
                if (lx[k].Share != ly[k].Share || a.ShellTypeId(lx[k].ShellType) != b.ShellTypeId(ly[k].ShellType)
                    || a.ShellTypes[lx[k].ShellType] != b.ShellTypes[ly[k].ShellType])
                    p.Add($"battery {i} shell {k}: {a.ShellTypeId(lx[k].ShellType)} {a.ShellTypes[lx[k].ShellType]} {lx[k].Share} before, " +
                          $"{b.ShellTypeId(ly[k].ShellType)} {b.ShellTypes[ly[k].ShellType]} {ly[k].Share} after");
            if (a.ShellTypeId(x.Fires) != b.ShellTypeId(y.Fires))
                p.Add($"battery {i} fires {a.ShellTypeId(x.Fires)} before, {b.ShellTypeId(y.Fires)} after");
        }
        if (a.LayoutHash != b.LayoutHash)
            p.Add("the layout hash moved");
        if (b.Batteries.Length > 0 && ShipDesign.Build(after).Report.Shells is null)
            p.Add("no shells in the report");
        return p;
    }

    static string KindName(ShellKind k) => k switch { ShellKind.Ap => "AP", ShellKind.Common => "common", _ => "HE" };

    static string EraName(Era e) => e switch
    {
        Era.Victorian => "victorian",
        Era.GreatWar => "great_war",
        Era.Treaty => "treaty",
        Era.Wwii => "wwii",
        _ => "cold_war",
    };

    static string QualityName(ShellQuality q) => q switch
    {
        ShellQuality.Rn1916 => "rn1916",
        ShellQuality.Km1916 => "km1916",
        ShellQuality.Rn1918 => "rn1918",
        ShellQuality.Ww2 => "ww2",
        _ => "ijn_t91",
    };
}
