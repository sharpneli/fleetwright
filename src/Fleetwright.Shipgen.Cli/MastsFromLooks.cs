using System.Text.Json;
using System.Text.Json.Nodes;

namespace Fleetwright.Shipgen.Cli;

/// <summary>`shipgen masts-from-looks FILE... [--dry]` (docs/battle/fire-control.md F4a, one-off): writes each warship
/// design's `masts` from what its look drew (looks.jsonc's mast shapes, before they left the looks): a tripod where the
/// look gave a long ship's foremast legs, a cage or lattice where it drew one, a fighting or spotting top on every mast
/// where it drew a top, with the look's radius and tiers. Heights are left to the ship (MastPlan). Each file keeps its
/// layout and is validated after.</summary>
static class MastsFromLooks
{
    public static int Run(IReadOnlyList<string> paths, bool dry)
    {
        foreach (var p in paths)
        {
            string text = File.ReadAllText(p);
            var design = Design.Parse(text);
            if (design.StyleName != "warship" || design.Masts is not null)
            {
                Console.WriteLine($"{p}: {(design.Masts is null ? design.StyleName : "has masts")}, left alone");
                continue;
            }
            var ship = ShipDesign.Build(design);
            var sh = Render.Looks.Shapes(design);
            string? mode = sh.Mast;
            double legs = sh.Tripod ?? (mode is "pole" or "fighting_top" or "cage" or "lattice" ? 0.0 : 1.0);
            double cage = sh.CageR ?? (mode == "cage" ? 3.0 : 0.0), lattice = sh.LatticeR ?? (mode == "lattice" ? 2.2 : 0.0);
            double topR = sh.TopR ?? (mode == "fighting_top" ? 1.6 : 0.0);
            long tiers = topR != 0 ? sh.TopTiers ?? 1 : 0;
            var masts = new JsonArray();
            var comps = ship.Hitboxes.Components;
            foreach (string id in (ReadOnlySpan<string>)["Foremast", "Mainmast"])
            {
                var c = comps.FirstOrDefault(x => x.Kind == ComponentKind.Mast && x.Id == id);
                if (c is null)
                {
                    if (id == "Foremast")
                        masts.Add((JsonNode)new JsonObject { ["kind"] = "none" });
                    continue;
                }
                bool tall = c.Type == "tripod";   // the old default's big mast: the one the look dressed
                string kind = tall && cage > 0 ? "cage" : tall && lattice > 0 ? "lattice" : tall && legs > 0 ? "tripod" : "pole";
                var m = new JsonObject { ["kind"] = kind };
                if (tiers > 0)
                {
                    m["top"] = mode == "fighting_top" ? "fighting" : "spotting";
                    m["top_radius_m"] = topR;
                    if (tiers > 1)
                        m["tiers"] = tiers;
                }
                masts.Add((JsonNode)m);
            }
            Console.WriteLine($"{p}: {masts.ToJsonString()}");
            if (dry)
                continue;
            var before = JsonNode.Parse(text)!.AsObject();
            var after = before.DeepClone().AsObject();
            after["masts"] = masts;
            string nl = text.Contains("\r\n") ? "\r\n" : "\n";
            string written = DesignUpgrade.Pretty(text, nl) is { } indent ? DesignUpgrade.Write(after, indent, nl)
                : DesignUpgrade.Splice(text, before, after, ["masts"], [], nl);
            var errs = ShipDesign.Validate(Design.Parse(written), limits: false);
            if (errs.Count > 0)
                throw new InvalidOperationException($"{p}: {string.Join("; ", errs)}");
            File.WriteAllText(p, written);
        }
        return 0;
    }
}
