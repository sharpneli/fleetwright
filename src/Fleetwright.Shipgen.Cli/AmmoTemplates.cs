using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization.Metadata;

namespace Fleetwright.Shipgen.Cli;

/// <summary>`shipgen ammo-templates FILE...` (S1 of docs/battle/shells-armour-damage.md, one-off): switches each
/// design's shells from S0's placeholder blocks to the templates of its navy and year (ShellTemplates). Each battery
/// keeps its outfit's kinds and shares; each kind takes its family's block (a light gun's, under 200 mm, the family's
/// light block where it has one), copied into the design's `ammunition` with the explosives it names copied into
/// `explosives`, and the per-battery `mass_kg` goes. `--dry` prints what it would do. The mapping is the one the
/// user approved (the plan's S1 section has it).</summary>
static class AmmoTemplates
{
    /// <summary>Each design's family for its heavy guns and for its light ones. A design not listed keeps its shells.</summary>
    static readonly Dictionary<string, (string Heavy, string Light)> Map = Build();

    static Dictionary<string, (string, string)> Build()
    {
        var m = new Dictionary<string, (string, string)>(StringComparer.Ordinal);
        void Add(string family, string light, params string[] ids)
        {
            foreach (var id in ids)
                m.Add(id, (family, light));
        }
        void Same(string family, params string[] ids) => Add(family, family, ids);

        Same("1867 chilled iron", "devastation", "inflexible", "duilio");
        Same("1895 forged steel", "doom");
        Same("RU 1895", "borodino", "aurora", "buyny", "oslyabya");
        Same("JP 1904", "mikasa", "izumo", "ikazuchi");
        Same("US 1906", "connecticut");
        Same("DE 1902", "armoured_cruiser", "hessen", "deutschland_1906");
        Same("FR 1893", "babel", "bouvet", "brennus");
        Same("RN 1906", "dreadnought", "invincible", "lord_nelson");
        // the RN's Great War ships: the 12" generation's shells, or the 13.5"/15" generation's (RN 1912, by calibre below)
        Add("RN 1909", "RN 1906", "light_cruiser", "tb_destroyer", "abdiel", "agincourt", "arethusa", "bellerophon", "cambrian", "caroline", "colossus",
            "devonshire", "duke_of_edinburgh", "flotilla_leader", "indefatigable", "k_acasta", "l_laforey", "m_class", "minotaur", "neptune",
            "r_class", "scout_cruiser", "st_vincent", "town_birmingham", "town_chatham", "warrior",
            "lion", "super_dreadnought", "tiger_1914", "erebus", "canada", "erin", "iron_duke", "king_george_v", "orion", "queen_mary", "revenge");
        Same("DE 1911", "karlsruhe", "seydlitz", "nassau", "nassau_casemates", "b97", "derfflinger", "g101", "gazelle", "graudenz", "helgoland",
            "kaiser", "konig", "moltke", "nassau_1916", "pillau", "stettin", "v1", "v25", "von_der_tann", "wiesbaden");
        Same("FR 1910", "courbet", "danton");
        Same("1912 standard", "battlecruiser", "dante", "tegetthoff", "tramp_1914", "engadine");
        Same("US 1916", "pennsylvania");
        Add("US 1921", "US 1916", "colorado");
        Same("JP 1913", "kongo");
        Same("RN 1927", "kent", "nelson");
        Same("JP 1925", "nagato", "fubuki");
        Same("RU 1911", "gangut");
        Same("RN 1938", "kgv", "victory_1944");
        Same("DE 1939", "bismarck", "deutschland");
        Same("IT 1940", "littorio", "zara");
        Same("FR 1936", "dunkerque", "richelieu");
        Same("US 1941", "all_forward", "all_forward_flush", "battleship", "battleship_layered", "des_moines", "destroyer", "fleet_carrier",
            "heavy_cruiser", "liberty", "mtb", "murica", "overloaded", "pt_boat", "q_ship", "seaplane_carrier", "supercarrier");
        Same("JP 1931", "yamato");
        Same("1950 dual-purpose", "akizuki_1960", "colbert", "daring", "hamburg", "impetuoso", "sverdlov", "tiger");
        return m;
    }

    /// <summary>The block a battery of the family fires for a kind: a light gun's light block where there is one, else
    /// the family's.</summary>
    static ShellTemplates.Template Pick(string heavy, string light, string kind, double calibreMm) =>
        Find(heavy, light, kind, calibreMm) ?? throw new InvalidOperationException($"the {heavy} family has no {kind} block");

    static ShellTemplates.Template? Find(string heavy, string light, string kind, double calibreMm)
    {
        if (calibreMm >= 330 && heavy == "RN 1909")
            heavy = "RN 1912";
        bool small = calibreMm < 200;
        return (small ? ShellTemplates.All.FirstOrDefault(t => t.Group == light && t.Light && t.Value.Kind == kind) : null)
            ?? ShellTemplates.All.FirstOrDefault(t => t.Group == heavy && !t.Light && t.Value.Kind == kind);
    }

    /// <summary>The default outfit (S2, decision 7): a main battery 70% AP and 30% HE, a secondary 30% and 70%. Either
    /// is the family's common shell where it has none of that kind: HE in the families before HE shells (~1900), AP in
    /// the post-war dual-purpose family (its SAP).</summary>
    static (string Kind, double Share)[] Outfit(bool main, string heavy, string light, double calibreMm)
    {
        string Or(string kind) => Find(heavy, light, kind, calibreMm) != null ? kind : "common";
        string ap = Or("ap"), he = Or("he");
        return main ? [(ap, 0.7), (he, 0.3)] : [(ap, 0.3), (he, 0.7)];
    }

    public static int Run(IEnumerable<string> paths, bool dry, bool outfits = false)
    {
        int written = 0, skipped = 0;
        foreach (var p in paths)
        {
            string text = File.ReadAllText(p);
            var design = Design.Parse(text);
            if (design.Id is not { } id || !Map.TryGetValue(id, out var fam) || design.Ammunition is null)
            {
                skipped++;
                continue;
            }
            var blocks = new SortedDictionary<string, AmmoInput>(StringComparer.Ordinal);
            var shells = new List<JsonArray>();
            var lines = new List<string>();
            foreach (var (b, main) in design.MainBatteries.Select(b => (b, true)).Concat((design.Secondary ?? []).Select(b => (b, false))))
            {
                var list = new JsonArray();
                // the outfit's kinds and shares: the design's (S1), or the default outfit (S2's --outfits)
                var kinds = outfits ? Outfit(main, fam.Heavy, fam.Light, b.CalibreMm!.Value)
                    : (b.Shells ?? []).Select(e => (design.Ammunition[e.Ammo!].Kind!, e.Share!.Value)).ToArray();
                foreach (var (kind, share) in kinds)
                {
                    var t = Pick(fam.Heavy, fam.Light, kind, b.CalibreMm!.Value);
                    blocks[t.Name] = t.Value;
                    list.Add((JsonNode)new JsonObject { ["ammo"] = t.Name, ["share"] = share });
                    lines.Add($"  {b.CalibreMm}/{b.CalibreLength} {(main ? "main" : "secondary")} {share:0.##} {t.Name}");
                }
                shells.Add(list);
            }
            var explosives = new SortedDictionary<string, ExplosiveInput>(StringComparer.Ordinal);
            foreach (var a in blocks.Values)
                if (a.Filler?.Explosive?.Name is { } n)
                    explosives[n] = Explosives.Get(n)!;
            if (dry)
            {
                Console.WriteLine($"{id}: {fam.Heavy}" + (fam.Light != fam.Heavy ? $" (light: {fam.Light})" : ""));
                foreach (var l in lines.Distinct())
                    Console.WriteLine(l);
                continue;
            }
            var ammoNode = JsonNode.Parse(JsonSerializer.Serialize(new Dictionary<string, AmmoInput>(blocks), Info<Dictionary<string, AmmoInput>>()))!;
            var explosivesNode = JsonNode.Parse(JsonSerializer.Serialize(new Dictionary<string, ExplosiveInput>(explosives), Info<Dictionary<string, ExplosiveInput>>()))!;
            string nl = text.Contains("\r\n") ? "\r\n" : "\n";
            string after = Pretty(text, nl) is { } indent ? Rewrite(text, shells, ammoNode, explosivesNode, indent, nl) : Replace(text, shells, ammoNode, explosivesNode, nl);
            var errs = ShipDesign.Validate(Design.Parse(after), limits: false);
            if (errs.Count > 0)
                throw new InvalidOperationException($"{p}: {string.Join("; ", errs)}");
            File.WriteAllText(p, after);
            written++;
        }
        Console.WriteLine($"{written} written, {skipped} skipped");
        return 0;
    }

    static JsonTypeInfo<T> Info<T>() => (JsonTypeInfo<T>)ShipgenJson.Default.Options.GetTypeInfo(typeof(T));

    static readonly JsonSerializerOptions Compact = new() { Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping };

    static string Spaced(JsonNode n) => n.ToJsonString(Compact).Replace(",\"", ", \"").Replace("\":", "\": ").Replace("},{", "}, {");

    /// <summary>The indent of a file written by an indented writer (a scenario's designs), when re-writing it gives it
    /// back byte for byte; null for the shipped designs' hand-laid lines.</summary>
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

    static string Rewrite(string text, List<JsonArray> shells, JsonNode ammo, JsonNode explosives, int indent, string nl)
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
        node["ammunition"] = ammo;
        node["explosives"] = explosives;
        return Write(node, indent, nl);
    }

    /// <summary>Into the shipped designs' lines: each battery's `shells` value and the `ammunition` line replaced, the
    /// explosives on a line after it.</summary>
    static string Replace(string text, List<JsonArray> shells, JsonNode ammo, JsonNode explosives, string nl)
    {
        if (Encoding.UTF8.GetByteCount(text) != text.Length)
            throw new InvalidOperationException("not an ASCII design");
        var bytes = Encoding.UTF8.GetBytes(text);
        var reader = new Utf8JsonReader(bytes, new JsonReaderOptions { CommentHandling = JsonCommentHandling.Skip, AllowTrailingCommas = true });
        bool hasExplosives = JsonNode.Parse(text)!.AsObject().ContainsKey("explosives");
        var spans = new List<(int Start, int End, string With)>();
        int depth = 0, k = 0;
        string? key = null;
        while (reader.Read())
            switch (reader.TokenType)
            {
                case JsonTokenType.PropertyName when depth == 1:
                    key = reader.GetString();
                    if (key is "ammunition" or "explosives")
                    {
                        reader.Read();
                        int start = (int)reader.TokenStartIndex;
                        reader.Skip();
                        spans.Add((start, (int)reader.TokenStartIndex + 1, key == "explosives" ? Spaced(explosives)
                            : hasExplosives ? Spaced(ammo) : Spaced(ammo) + "," + nl + " \"explosives\": " + Spaced(explosives)));
                    }
                    break;
                case JsonTokenType.PropertyName when key is "main" or "secondary" && reader.GetString() == "shells":
                    {
                        reader.Read();
                        int start = (int)reader.TokenStartIndex;
                        reader.Skip();
                        spans.Add((start, (int)reader.TokenStartIndex + 1, Spaced(shells[k++])));
                        break;
                    }
                case JsonTokenType.StartObject or JsonTokenType.StartArray:
                    depth++;
                    break;
                case JsonTokenType.EndObject or JsonTokenType.EndArray:
                    depth--;
                    break;
            }
        if (k != shells.Count)
            throw new InvalidOperationException($"found {k} shells for {shells.Count} batteries");
        var sb = new StringBuilder();
        int at = 0;
        foreach (var (start, end, with) in spans.OrderBy(s => s.Start))
        {
            sb.Append(text, at, start - at).Append(with);
            at = end;
        }
        return sb.Append(text, at, text.Length - at).ToString();
    }
}
