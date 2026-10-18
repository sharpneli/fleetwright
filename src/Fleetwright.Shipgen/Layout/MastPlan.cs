namespace Fleetwright.Shipgen;

/// <summary>Masts and their tops (docs/battle/fire-control.md F4): the kinds, the heights a design leaves to the ship, and
/// the weights. research/simulation/superstructure-research.md §6: the fire-control top is set by range, 2.0-2.5 times the
/// highest gun's height over the water (§6.2's default: Bismarck's foretop ~31 m over a ~14.5 m superfiring turret,
/// Yamato's 40 over 17; an 1898 fighting top 20-24 m over 8.5; Dreadnought's 28-30 over ~10 the tall one, which the
/// default leaves low: a design gives her height); masts and their topmasts stand higher still, unmanned. Height is the
/// design's trade against topweight and windage, never an era's value.</summary>
public static class MastPlan
{
    public static readonly string[] Kinds = ["pole", "tripod", "cage", "lattice", "tower", "none"], TopKinds = ["none", "fighting", "spotting", "crows_nest"];

    /// <summary>A crow's nest's roof stands this far under the masthead by default, m [EST].</summary>
    public const double NestUnderHeadM = 1.0;

    /// <summary>The top's floor over the water as a share of the highest gun's: the research's 2.0-2.5, its middle.</summary>
    public const double TopOverGun = 2.25;

    /// <summary>A top's radius by default: a spotting top 1.5 m, a fighting top 1.6, a crow's nest (a lookout's barrel)
    /// 0.75 [EST], each tier more half a metre (a pagoda's platforms).</summary>
    public static double DefaultTopR(string kind, int tiers) => kind switch { "fighting" => 1.6, "crows_nest" => 0.75, _ => 1.5 } + 0.5 * (tiers - 1);

    /// <summary>The highest gun's axis above the main deck: the main mounts', else the secondaries', about 0.6 of the way
    /// up the gunhouse; a deck level with none.</summary>
    public static double HighestGunM(List<Mount> mounts)
    {
        foreach (var kind in (ReadOnlySpan<ComponentKind>)[ComponentKind.Main, ComponentKind.Secondary])
        {
            double best = double.NegativeInfinity;
            foreach (var m in mounts)
                if (m.Kind == kind)
                    best = Math.Max(best, m.Base + 0.6 * (m.Top - m.Base));
            if (best > double.NegativeInfinity)
                return best;
        }
        return Layout.LevelH;
    }

    /// <summary>A mast's top, or null: the design's (a spotting top when it carries a director and gives none), its floor
    /// the design's or TopOverGun times the highest gun's height over the water, and over what it must clear (the bridge
    /// tower's director, the funnels); with a director on it, at least wide enough for the director's body (directorR:
    /// half its footprint's diagonal). A crow's nest stands at the masthead instead (bareHeadM: the head the mast has
    /// without a top), its roof <see cref="NestUnderHeadM"/> under it: a lookout's post wants all the height there is.</summary>
    public static MastTop? Top(MastInput? given, double gunOverWaterM, double freeboardM, double clearM, double directorR = 0,
        double bareHeadM = 0)
    {
        string? kind = given?.Top is null or "none" ? given?.Director is null ? null : "spotting" : given.Top;
        if (kind is null)
            return null;
        int tiers = Math.Max(given!.Tiers ?? 1, 1);
        double floor = given.TopM ?? (kind == "crows_nest"
            ? Math.Max(bareHeadM - NestUnderHeadM - MastTop.TierH * tiers, clearM + 0.3)
            : Math.Max(TopOverGun * gunOverWaterM - freeboardM, clearM + 0.3));
        return new MastTop(kind, floor, given.TopRadiusM ?? Math.Max(DefaultTopR(kind, tiers), directorR), tiers);
    }

    /// <summary>A masthead by default: over the funnels and the bridge tower as before, and with a top a topmast over it
    /// and any director on it (0.3 of the top's height, 4 m at least); a crow's nest only its pole's end over it.</summary>
    public static double Head(double overFunnelsM, double towerM, MastTop? top, bool director = false)
    {
        double head = Math.Max(overFunnelsM, towerM > 0 ? towerM + 2.0 : 0.0);
        if (top is { Kind: "crows_nest" } && !director)
            return Math.Max(head, top.Roof + NestUnderHeadM);
        return top is null ? head : Math.Max(head, top.Roof + (director ? Layout.LevelH : 0) + Math.Max(4.0, 0.3 * top.Floor));
    }

    public static readonly string[] Batteries = ["main", "secondary", "aa"];

    /// <summary>A kind's weight as poles' worth of the same height: a pole 1, a tripod's three legs, a cage's and a lattice's
    /// struts 4 [EST], a tower's plated column 8 [EST].</summary>
    public static double Legs(string kind) => kind switch { "tripod" => 3, "cage" or "lattice" => 4, "tower" => 8, _ => 1 };

    /// <summary>A tier of a top, t: its floor (0.05 t/m²), a 2.2 m wall of 5 mm plate round it, and a tonne of gear and men
    /// (a crow's nest's one lookout and his voice pipe 0.2 t) [EST].</summary>
    public static double TopWeightT(double r, string kind = "spotting") =>
        Math.PI * r * r * 0.05 + 2 * Math.PI * r * MastTop.TierH * 0.005 * Weight.Steel + (kind == "crows_nest" ? 0.2 : 1.0);

    public static List<string> Validate(Design design)
    {
        var errs = new List<string>();
        var masts = design.Masts;
        if (masts is null)
            return errs;
        if (masts.Count > 2)
            errs.Add($"masts: {masts.Count} given; a warship has a foremast and a mainmast (2 at most)");
        for (int i = 0; i < masts.Count; i++)
        {
            string at = $"masts[{i}]";
            if (masts[i] is not { } m)
            {
                errs.Add($"{at}: give a mast (or {{\"kind\": \"none\"}})");
                continue;
            }
            foreach (var k in m.Extra.KeysOrEmpty())
                errs.Add($"{at}.{k}: not a mast field (kind, height_m, top, top_m, top_radius_m, tiers, director)");
            if (m.Kind is { } kind && !Kinds.Contains(kind))
                errs.Add($"{at}.kind = {Style.Quote(kind)}: use {string.Join(", ", Kinds)}");
            if (m.Top is { } top && !TopKinds.Contains(top))
                errs.Add($"{at}.top = {Style.Quote(top)}: use {string.Join(", ", TopKinds)}");
            if (m.Director is { } bat)
            {
                if (!Batteries.Contains(bat))
                    errs.Add($"{at}.director = {Style.Quote(bat)}: use {string.Join(", ", Batteries)}");
                if (m.Kind == "none")
                    errs.Add($"{at}: no mast to carry a director");
            }
            foreach (var (k, v) in new[] { ("height_m", m.HeightM), ("top_m", m.TopM), ("top_radius_m", m.TopRadiusM) })
                if (v is double d && !(d > 0 && double.IsFinite(d)))
                    errs.Add($"{at}.{k}: a number, more than 0");
            if (m.Tiers is < 1 or > 5)
                errs.Add($"{at}.tiers: 1 to 5");
            if (m.HeightM is double h && m.TopM is double f && h < f + MastTop.TierH * (m.Tiers ?? 1))
                errs.Add($"{at}: its masthead ({h} m) under its top's roof ({f + MastTop.TierH * (m.Tiers ?? 1)} m)");
        }
        return errs;
    }
}
