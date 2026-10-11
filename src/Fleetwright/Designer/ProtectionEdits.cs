using Fleetwright.Shipgen;

namespace Fleetwright.Designer;

/// <summary>
/// The Protection tab's edits (docs/designer-ui/protection.md): the armour as a fixed set of parts plus any number of
/// armour decks, each added with starting values from the belt and removed by setting it to 0 (a deck: deleting it),
/// as the engine already reads an absent part; and the three coverage presets, which move extents and add or drop
/// the end armour but keep every thickness.
/// </summary>
public static class ProtectionEdits
{
    /// <summary>The parts a ship has or lacks (the decks are a list of their own).</summary>
    public enum Part { Belt, Upper, EndFore, EndAft, Bulkheads, Tds, Steering }

    public static readonly Part[] Parts = [Part.Belt, Part.Upper, Part.EndFore, Part.EndAft, Part.Bulkheads, Part.Tds, Part.Steering];

    public static string Name(Part p) => p switch
    {
        Part.Belt => "Belt",
        Part.Upper => "Upper belt",
        Part.EndFore => "End belt, fore",
        Part.EndAft => "End belt, aft",
        Part.Bulkheads => "Citadel bulkheads",
        Part.Tds => "Torpedo protection",
        _ => "Steering gear box",
    };

    static ArmourInput Arm(Design d) => d.Armour ?? new ArmourInput();
    static double BeltMm(Design d) => d.Armour?.BeltMm is double b and > 0 ? b : 200;
    static double R10(double v) => Math.Round(v / 10, MidpointRounding.AwayFromZero) * 10;
    static double R5(double v) => Math.Round(v / 5, MidpointRounding.AwayFromZero) * 5;
    static string End(Part p) => p == Part.EndFore ? "fore" : "aft";

    public static bool Has(Design d, Part p)
    {
        var a = d.Armour;
        return p switch
        {
            Part.Belt => (a?.BeltMm ?? 0) > 0,
            Part.Upper => (a?.UpperBelt?.Mm ?? 0) > 0,
            Part.EndFore or Part.EndAft => (a?.EndBelts?.Of(End(p))?.Mm ?? 0) > 0,
            Part.Bulkheads => (a?.BulkheadMm ?? 0) > 0,
            Part.Tds => (a?.TdsM ?? 0) > 0,
            _ => (a?.SteeringBox?.Mm ?? 0) > 0,
        };
    }

    static Design WithEnd(Design d, string end, EndBeltInput? b)
    {
        var a = Arm(d);
        var e = a.EndBelts ?? new EndBeltsInput();
        return d with { Armour = a with { EndBelts = end == "fore" ? e with { Fore = b } : e with { Aft = b } } };
    }

    /// <summary>The part added with starting values from the belt: 100 mm of belt over the citadel; an upper belt half
    /// the belt to the main deck; end belts 0.4 of it to the stem and stern; bulkheads 0.7; 4 m of torpedo protection;
    /// a steering gear box 0.8 of the belt, roofed like the first armour deck.</summary>
    public static Design Add(Design d, Part p)
    {
        var a = Arm(d);
        double belt = BeltMm(d);
        return p switch
        {
            Part.Belt => d with { Armour = a with { BeltMm = 100 } },
            Part.Upper => d with { Armour = a with { UpperBelt = (a.UpperBelt ?? new UpperBeltInput()) with { Mm = R10(belt * 0.5), ToDeck = a.UpperBelt?.ToDeck ?? 0, Extent = a.UpperBelt?.Extent ?? "citadel" } } },
            Part.EndFore or Part.EndAft => WithEnd(d, End(p), (a.EndBelts?.Of(End(p)) ?? new EndBeltInput()) with
            {
                Mm = R10(belt * 0.4), TipMm = R10(belt * 0.4), Reach = 1, BulkheadMm = a.EndBelts?.Of(End(p))?.BulkheadMm ?? 0,
            }),
            Part.Bulkheads => d with { Armour = a with { BulkheadMm = R10(belt * 0.7) } },
            Part.Tds => d with { Armour = a with { TdsM = 4 } },
            _ => d with
            {
                Armour = a with
                {
                    SteeringBox = (a.SteeringBox ?? new SteeringBoxInput()) with
                    {
                        Mm = R10((a.BeltMm is double b and > 0 ? b : 100) * 0.8), DeckMm = a.Decks?.FirstOrDefault()?.Mm ?? 50, BulkheadMm = a.BulkheadMm ?? 0,
                    },
                },
            },
        };
    }

    /// <summary>The part gone: its thickness 0, the rest of its entry kept (so adding it back keeps its extent).</summary>
    public static Design Remove(Design d, Part p)
    {
        var a = Arm(d);
        return p switch
        {
            Part.Belt => d with { Armour = a with { BeltMm = 0 } },
            Part.Upper => d with { Armour = a with { UpperBelt = (a.UpperBelt ?? new UpperBeltInput()) with { Mm = 0 } } },
            Part.EndFore or Part.EndAft => a.EndBelts?.Of(End(p)) is { } e ? WithEnd(d, End(p), e with { Mm = 0 }) : d,
            Part.Bulkheads => d with { Armour = a with { BulkheadMm = 0 } },
            Part.Tds => d with { Armour = a with { TdsM = 0 } },
            _ => d with { Armour = a with { SteeringBox = (a.SteeringBox ?? new SteeringBoxInput()) with { Mm = 0, DeckMm = 0, BulkheadMm = 0 } } },
        };
    }

    // ------------------------------------------------------------------ armour decks

    static IReadOnlyList<ArmourDeckInput> DecksOf(Design d) => d.Armour?.Decks ?? [];

    /// <summary>An armour deck on deck <paramref name="deck"/> over <paramref name="extent"/>, kept in deck order;
    /// returns its index.</summary>
    public static Design AddDeck(Design d, int deck, string extent, double mm, out int index)
    {
        var l = DecksOf(d).ToList();
        index = l.FindLastIndex(x => (x.Deck ?? 0) <= deck) + 1;
        l.Insert(index, new ArmourDeckInput { Deck = deck, Mm = mm, Extent = extent });
        return d with { Armour = Arm(d) with { Decks = l } };
    }

    public static Design RemoveDeck(Design d, int i)
    {
        var l = DecksOf(d).ToList();
        l.RemoveAt(i);
        return d with { Armour = Arm(d) with { Decks = l } };
    }

    public static Design SetDeck(Design d, int i, Func<ArmourDeckInput, ArmourDeckInput> f)
    {
        var l = DecksOf(d).ToList();
        l[i] = f(l[i]);
        return d with { Armour = Arm(d) with { Decks = l } };
    }

    /// <summary>The citadel's roof among the design's decks: the lowest (highest-numbered) deck over the citadel or the
    /// whole length; -1 without one.</summary>
    public static int RoofDeck(Design d) =>
        DecksOf(d).Where(x => (x.Extent ?? "citadel") is "citadel" or "full" && (x.Mm ?? 0) > 0).Select(x => x.Deck ?? 0).DefaultIfEmpty(-1).Max();

    static bool Ends(string? extent) => extent is "fore" or "aft" or "ends";

    // ------------------------------------------------------------------ coverage

    public enum Coverage { AllOrNothing, ArmouredEnds, CompleteBelt }

    public static readonly Coverage[] Coverages = [Coverage.AllOrNothing, Coverage.ArmouredEnds, Coverage.CompleteBelt];

    public static string Name(Coverage c) => c switch
    {
        Coverage.AllOrNothing => "All-or-nothing",
        Coverage.ArmouredEnds => "Armoured ends",
        _ => "Complete belt",
    };

    public static string Purpose(Coverage c) => c switch
    {
        Coverage.AllOrNothing => "Armour only the citadel: belt, roof deck and bulkheads. The ends are left to flood.",
        Coverage.ArmouredEnds => "Belt and deck carried on to the ends, thinner, to keep them dry and the trim steady.",
        _ => "The ends armoured and the upper belt carried the full length.",
    };

    /// <summary>What the design reads as: complete with an upper belt the full length and both end belts, armoured
    /// ends with any end belt or deck armour over an end, else all-or-nothing.</summary>
    public static Coverage Reads(Design d)
    {
        var a = d.Armour;
        bool ends = Has(d, Part.EndFore) || Has(d, Part.EndAft) || DecksOf(d).Any(x => (x.Mm ?? 0) > 0 && Ends(x.Extent))
                    || Has(d, Part.Upper) && a?.UpperBelt?.Extent is not (null or "citadel");
        if (Has(d, Part.Upper) && a?.UpperBelt?.Extent == "full" && Has(d, Part.EndFore) && Has(d, Part.EndAft))
            return Coverage.CompleteBelt;
        return ends ? Coverage.ArmouredEnds : Coverage.AllOrNothing;
    }

    /// <summary>The preset applied: thicknesses stay, new plates start from the belt. <paramref name="deepest"/> is the
    /// ship's lowest deck an armour deck can lie on (the deck over the inner bottom).</summary>
    public static Design Apply(Design d, Coverage c, int deepest)
    {
        switch (c)
        {
            case Coverage.AllOrNothing:
                foreach (var p in new[] { Part.EndFore, Part.EndAft })
                    if (Has(d, p))
                        d = Remove(d, p);
                d = d with
                {
                    Armour = Arm(d) with
                    {
                        Decks = DecksOf(d).Where(x => !Ends(x.Extent)).Select(x => x.Extent == "full" ? x with { Extent = "citadel" } : x).ToList(),
                        UpperBelt = Has(d, Part.Upper) ? d.Armour!.UpperBelt! with { Extent = "citadel" } : d.Armour?.UpperBelt,
                    },
                };
                return d;
            case Coverage.ArmouredEnds:
                foreach (var p in new[] { Part.EndFore, Part.EndAft })
                    if (!Has(d, p))
                        d = Add(d, p);
                if (!DecksOf(d).Any(x => (x.Mm ?? 0) > 0 && (Ends(x.Extent) || x.Extent == "full")))
                {
                    int roof = RoofDeck(d);
                    int below = roof < 0 ? 1 : Math.Min(roof + 1, deepest);
                    double mm = R5((DecksOf(d).FirstOrDefault(x => x.Deck == roof)?.Mm ?? 50) * 0.6);
                    d = AddDeck(d, below, "ends", Math.Max(5, mm), out _);
                }
                return d;
            default:
                d = Apply(d, Coverage.ArmouredEnds, deepest);
                foreach (var end in new[] { "fore", "aft" })
                    d = WithEnd(d, end, d.Armour!.EndBelts!.Of(end)! with { Reach = 1 });
                if (!Has(d, Part.Upper))
                    d = Add(d, Part.Upper);
                return d with { Armour = Arm(d) with { UpperBelt = d.Armour!.UpperBelt! with { Extent = "full" } } };
        }
    }

    /// <summary>What going from <paramref name="a"/> to <paramref name="b"/> does, a line per change ("adds the end
    /// belt, fore 160 mm"), for the presets' tooltips.</summary>
    public static List<string> Describe(Design a, Design b)
    {
        var lines = new List<string>();
        string Mm(double? v) => $"{v ?? 0:0} mm";
        foreach (var p in Parts)
        {
            bool ha = Has(a, p), hb = Has(b, p);
            if (ha != hb)
                lines.Add($"{(hb ? "adds" : "drops")} the {Name(p).ToLowerInvariant()} " +
                          (p == Part.Tds ? $"{(hb ? b : a).Armour?.TdsM ?? 0:0.0} m" : Mm(Thickness(hb ? b : a, p))));
        }
        if (Has(a, Part.Upper) && Has(b, Part.Upper) && a.Armour!.UpperBelt!.Extent != b.Armour!.UpperBelt!.Extent)
            lines.Add($"the upper belt over the {ExtentText(b.Armour.UpperBelt.Extent)}");
        foreach (var end in new[] { "fore", "aft" })
            if (a.Armour?.EndBelts?.Of(end) is { Mm: > 0 } ea && b.Armour?.EndBelts?.Of(end) is { Mm: > 0 } eb && (ea.Reach ?? 1) != (eb.Reach ?? 1))
                lines.Add($"the end belt, {end} carried to the {(end == "fore" ? "stem" : "stern")}");
        var da = DecksOf(a).Where(x => (x.Mm ?? 0) > 0).Select(Key).ToList();
        var db = DecksOf(b).Where(x => (x.Mm ?? 0) > 0).Select(Key).ToList();
        foreach (var k in da.Except(db))
            lines.Add($"drops the {k}");
        foreach (var k in db.Except(da))
            lines.Add($"adds the {k}");
        return lines;

        static string Key(ArmourDeckInput x) => $"{Decks.DeckNameLower(x.Deck ?? 0)}, {ExtentText(x.Extent)} {x.Mm ?? 0:0} mm";
    }

    static double? Thickness(Design d, Part p) => p switch
    {
        Part.Belt => d.Armour?.BeltMm,
        Part.Upper => d.Armour?.UpperBelt?.Mm,
        Part.EndFore or Part.EndAft => d.Armour?.EndBelts?.Of(End(p))?.Mm,
        Part.Bulkheads => d.Armour?.BulkheadMm,
        Part.Tds => (d.Armour?.TdsM ?? 0) * 1000,
        _ => d.Armour?.SteeringBox?.Mm,
    };

    public static string ExtentText(string? e) => e switch
    {
        null or "citadel" => "citadel",
        "full" => "full length",
        "fore" => "fore end",
        "aft" => "aft end",
        "ends" => "both ends",
        _ => e,
    };
}
