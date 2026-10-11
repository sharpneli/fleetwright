using System.Globalization;
using Fleetwright.Designer;
using Fleetwright.Shipgen;

namespace Fleetwright;

/// <summary>What the ship card says about a built ship, formatted once in the designer's units: its name, the line under
/// it (type and era), the headline figures in big type (value, label), and the particulars (label, value). The headline figures
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

    /// <summary>A metric value in the units shown, stepped as the designer steps it (mm and in to a tenth in imperial).</summary>
    static string Fmt(double metric, Quantity q, UnitSystem u, double? stepMetric = null, bool unit = true)
    {
        double step = stepMetric is { } st ? st / Units.Factor(q, u) : q is Quantity.Armour or Quantity.Calibre && u == UnitSystem.Imperial ? 0.1 : 1;
        return Units.Format(metric, q, u, step, unit);
    }

    static string Words(string id) => string.Join(' ', id.Split('_', StringSplitOptions.RemoveEmptyEntries)
        .Select(w => char.ToUpperInvariant(w[0]) + w[1..]));

    public static ShipCard Of(Ship ship, LookInput? look, UnitSystem u)
    {
        // the guns of some batteries, one calibre at a time, largest first: "10 × 356 mm · 16 × 133 mm" (main: turrets;
        // else mounts); a battery split in two still counts as one calibre's guns
        string[] Guns(IEnumerable<BatteryInput>? bs, bool main) => (bs ?? []).Where(b => (main ? b.Turrets : b.MountCount) > 0)
            .GroupBy(b => b.CalibreMm ?? 0).OrderByDescending(g => g.Key)
            .Select(g => $"{g.Sum(b => (main ? b.Turrets : b.MountCount) * (b.Barrels ?? 1))} × {Fmt(g.Key, Quantity.Calibre, u)}").ToArray();
        var d = ship.Design;
        var res = ship.Report.Results;
        var hb = ship.Hitboxes;
        var own = Shipgen.Render.Looks.LookOf(d);
        string era = look?.Era ?? own.Era ?? "wwii";
        var sub = new List<string>();
        if (d.Type is { Length: > 0 } type)
            sub.Add(type);
        sub.Add(EraNames.GetValueOrDefault(era, Words(era)));

        var mains = Guns(d.Main, true);
        string main = string.Join(" · ", mains), first = mains.FirstOrDefault() ?? "", sec = string.Join(" · ", Guns(d.Secondary, false));
        string aa = string.Join(" · ", hb.Components.Where(c => c.Kind == "aa").GroupBy(c => Geometry.AaCfg[c.Type!].CalibreMm)
            .OrderByDescending(g => g.Key).Select(g => $"{g.Sum(c => Geometry.AaCfg[c.Type!].Barrels)} × {Fmt(g.Key, Quantity.Calibre, u)}"));
        var tubes = hb.Components.Where(c => c.Kind == "torpedo").ToList();
        string torpedoes = tubes.Count == 0 ? "" : $"{tubes.Sum(c => c.Torpedoes ?? 0)} × {Fmt(tubes[0].CalibreMm ?? 533, Quantity.Calibre, u)} tubes";
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
            heroes.Add((Fmt(belt, Quantity.Armour, u), "belt armour"));
        else
            heroes.Add((N0(d.RangeNm ?? 6000) + " nm", "range"));

        var rows = new List<(string, string)>
        {
            ("Full load", N0(res.FullDisplacementT) + " t"),
            ("Dimensions", $"{Fmt(res.LengthM, Quantity.Length, u, 0.1, false)} × {Fmt(res.BeamM, Quantity.Length, u, 0.1, false)} × " +
                           Fmt(res.DraughtM, Quantity.Length, u, 0.1)),
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
            rows.Add(("Armour", string.Join(" · ", new[] { belt > 0 ? "belt " + Fmt(belt, Quantity.Armour, u) : "", deck > 0 ? "deck " + Fmt(deck, Quantity.Armour, u) : "" }.Where(s => s.Length > 0))));
        if (aircraft > 0 && res.HangarAreaM2 is long hangar)
            rows.Add(("Hangar", u == UnitSystem.Metric ? N0(hangar) + " m²" : N0(hangar * 10.7639) + " ft²"));
        if (res.CargoT is double cargo && cargo > 0)
            rows.Add(("Cargo", N0(cargo) + " t" + (res.CargoKind is { Length: > 0 } kind ? ", " + kind : "")));
        rows.Add(("Complement", N0(res.Crew) + " men"));
        rows.Add(("Stability", $"GM {Fmt(res.GmFullM, Quantity.Length, u, 0.01)} · roll {res.RollPeriodS:0.0} s"));
        return new ShipCard((d.Name ?? d.Id ?? "Unnamed").ToUpperInvariant(), string.Join("  ·  ", sub).ToUpperInvariant(), heroes.ToArray(),
            rows.ToArray());
    }
}
