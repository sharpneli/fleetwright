using System.Globalization;
using Fleetwright.Shipgen;

namespace Fleetwright;

/// <summary>What the ship card says about a built ship, formatted once (metric): its name, the line under it (type, navy,
/// era), the headline figures in big type (value, label), and the particulars (label, value). The headline figures
/// follow the kind of ship: a carrier leads with its aircraft, a merchant with its deadweight, a warship with its main
/// battery and belt.</summary>
public sealed record ShipCard(string Name, string Subtitle, (string Value, string Label)[] Heroes, (string Label, string Value)[] Rows)
{
    static readonly Dictionary<string, string> EraNames = new(StringComparer.Ordinal)
    {
        ["victorian"] = "Victorian", ["great_war"] = "Great War", ["treaty"] = "Treaty era", ["wwii"] = "Second World War",
        ["cold_war"] = "Cold War",
    };

    static string N0(double v) => v.ToString("N0", CultureInfo.InvariantCulture);

    static string M(double v) => v.ToString("0.0", CultureInfo.InvariantCulture);

    static string Words(string id) => string.Join(' ', id.Split('_', StringSplitOptions.RemoveEmptyEntries)
        .Select(w => char.ToUpperInvariant(w[0]) + w[1..]));

    /// <summary>The guns of some batteries, "8 × 380 mm · 12 × 150 mm" (main: turrets; else mounts).</summary>
    static string Guns(IEnumerable<BatteryInput>? bs, bool main) => string.Join(" · ", (bs ?? []).Where(b => (main ? b.Turrets : b.MountCount) > 0)
        .Select(b => $"{(main ? b.Turrets : b.MountCount) * (b.Barrels ?? 1)} × {b.CalibreMm ?? 0:0} mm"));

    public static ShipCard Of(Ship ship, LookInput? look)
    {
        var d = ship.Design;
        var res = ship.Report.Results;
        var hb = ship.Hitboxes;
        var own = Shipgen.Render.Looks.LookOf(d);
        string navy = look?.Navy ?? own.Navy ?? "generic", era = look?.Era ?? own.Era ?? "wwii";
        var sub = new List<string>();
        if (d.Type is { Length: > 0 } type)
            sub.Add(type);
        if (navy != "generic")
            sub.Add(Words(navy));
        sub.Add(EraNames.GetValueOrDefault(era, Words(era)));

        string main = Guns(d.Main, true), first = Guns(d.Main?.Take(1), true), sec = Guns(d.Secondary, false);
        string aa = string.Join(" · ", hb.Components.Where(c => c.Kind == "aa").GroupBy(c => Geometry.AaCfg[c.Type!].CalibreMm)
            .OrderByDescending(g => g.Key).Select(g => $"{g.Sum(c => Geometry.AaCfg[c.Type!].Barrels)} × {g.Key:0} mm"));
        var tubes = hb.Components.Where(c => c.Kind == "torpedo").ToList();
        string torpedoes = tubes.Count == 0 ? "" : $"{tubes.Sum(c => c.Torpedoes ?? 0)} × {tubes[0].CalibreMm ?? 533:0} mm tubes";
        double belt = hb.Armour.Belt?.ThicknessMm ?? 0, deck = (hb.Armour.Decks ?? []).Select(dk => dk.ThicknessMm).DefaultIfEmpty(0).Max();
        long aircraft = res.AircraftCapacity ?? 0;
        double speed = d.SpeedKn ?? 0;

        var heroes = new List<(string, string)> { (N0(res.StandardDisplacementT) + " t", "standard displacement"), ($"{speed:0.#} kn", "speed") };
        if (aircraft > 0)
            heroes.Add((N0(aircraft), "aircraft"));
        else if (res.DeadweightT is long dwt && dwt > 0 && main.Length == 0)
            heroes.Add((N0(dwt) + " t", "deadweight"));
        if (first.Length > 0)
            heroes.Add((first, "main battery"));
        if (belt > 0)
            heroes.Add(($"{belt:0} mm", "belt armour"));
        else
            heroes.Add((N0(d.RangeNm ?? 6000) + " nm", "range"));

        var rows = new List<(string, string)>
        {
            ("Full load", N0(res.FullDisplacementT) + " t"),
            ("Dimensions", $"{M(res.LengthM)} × {M(res.BeamM)} × {M(res.DraughtM)} m"),
            ("Machinery", $"{N0(res.PowerShp)} shp, {ship.Report.Plant.Shafts} shafts"),
            ("Plant", ship.Report.Plant.Name),
            ("Range", N0(d.RangeNm ?? 6000) + " nm"),
        };
        if (main.Length > 0 && main != first)
            rows.Add(("Main battery", main));
        if (sec.Length > 0)
            rows.Add(("Secondary", sec));
        if (aa.Length > 0)
            rows.Add(("Anti-aircraft", aa));
        if (torpedoes.Length > 0)
            rows.Add(("Torpedoes", torpedoes));
        if (belt > 0 || deck > 0)
            rows.Add(("Armour", string.Join(" · ", new[] { belt > 0 ? $"belt {belt:0} mm" : "", deck > 0 ? $"deck {deck:0} mm" : "" }.Where(s => s.Length > 0))));
        if (aircraft > 0 && res.HangarAreaM2 is long hangar)
            rows.Add(("Hangar", N0(hangar) + " m²"));
        if (res.CargoT is double cargo && cargo > 0)
            rows.Add(("Cargo", N0(cargo) + " t" + (res.CargoKind is { Length: > 0 } kind ? ", " + kind : "")));
        rows.Add(("Complement", N0(res.Crew) + " men"));
        rows.Add(("Stability", $"GM {res.GmFullM:0.00} m · roll {res.RollPeriodS:0.0} s"));
        return new ShipCard((d.Name ?? d.Id ?? "Unnamed").ToUpperInvariant(), string.Join("  ·  ", sub).ToUpperInvariant(), heroes.ToArray(),
            rows.ToArray());
    }
}
