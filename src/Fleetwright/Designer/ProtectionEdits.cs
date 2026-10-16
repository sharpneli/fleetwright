using Fleetwright.Shipgen;

namespace Fleetwright.Designer;

/// <summary>
/// The Protection tab's edits (docs/designer-ui/protection.md): the armour as a fixed set of parts plus any number of
/// armour decks, each added with starting values from the belt and removed by setting it to 0 (a deck: deleting it),
/// as the engine already reads an absent part; and the three coverage presets, which move extents and add or drop
/// the end armour but keep every thickness. The layers (docs/designer-ui/protection-layers.md): the parts behind the side
/// (inner bulkhead, magazine boxes, magazine wings), a belt's or deck's zone and a deck's slopes, and the layout
/// templates (Content/Templates/protection.json) put on through ProtectionSchemes.Apply.
/// </summary>
public static class ProtectionEdits
{
    /// <summary>The parts a ship has or lacks (the decks are a list of their own).</summary>
    public enum Part { Belt, Upper, EndFore, EndAft, Bulkheads, Tds, Steering, Inner, Box, Wings }

    public static readonly Part[] Parts = [Part.Belt, Part.Upper, Part.EndFore, Part.EndAft, Part.Bulkheads, Part.Tds, Part.Steering, Part.Inner, Part.Box,
        Part.Wings];

    public static string Name(Part p) => p switch
    {
        Part.Belt => "Belt",
        Part.Upper => "Upper belt",
        Part.EndFore => "End belt, fore",
        Part.EndAft => "End belt, aft",
        Part.Bulkheads => "Citadel bulkheads",
        Part.Tds => "Torpedo protection",
        Part.Inner => "Inner bulkhead",
        Part.Box => "Magazine boxes",
        Part.Wings => "Magazine wings",
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
            Part.Inner => a?.InnerBulkhead is { } ib && Math.Max(ib.Mm ?? 0, ib.MagazinesMm ?? 0) > 0,
            Part.Box => a?.MagazineBox is { } mb && (mb.SideMm ?? 0) + (mb.CrownMm ?? 0) + (mb.EndMm ?? 0) + (mb.FloorMm ?? 0) > 0,
            Part.Wings => a?.MagazineWingM is null or > 0,
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
    /// a steering gear box 0.8 of the belt, roofed like the first armour deck; an inner bulkhead 0.10 of the belt (0.14
    /// at the magazines); magazine boxes 0.4 of it, crown and ends 0.6 of the sides; wings at their auto width.</summary>
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
            Part.Inner => d with
            {
                Armour = a with
                {
                    InnerBulkhead = new InnerBulkheadInput
                    {
                        Mm = R5(Math.Max(20, (a.BeltMm is double b1 and > 0 ? b1 : 100) * 0.10)),
                        MagazinesMm = R5(Math.Max(25, (a.BeltMm is double b2 and > 0 ? b2 : 100) * 0.14)),
                    },
                },
            },
            Part.Box => d with { Armour = a with { MagazineBox = BoxFrom(a.BeltMm is double b3 and > 0 ? b3 : 60) } },
            Part.Wings => d with { Armour = a with { MagazineWingM = null } },
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
            Part.Inner => d with { Armour = a with { InnerBulkhead = null } },
            Part.Box => d with { Armour = a with { MagazineBox = null } },
            Part.Wings => d with { Armour = a with { MagazineWingM = 0 } },
            _ => d with { Armour = a with { SteeringBox = (a.SteeringBox ?? new SteeringBoxInput()) with { Mm = 0, DeckMm = 0, BulkheadMm = 0 } } },
        };
    }

    static MagazineBoxInput BoxFrom(double belt)
    {
        double t = R5(Math.Max(25, belt * 0.4));
        return new MagazineBoxInput { SideMm = t, CrownMm = R5(t * 0.6), EndMm = R5(t * 0.6), FloorMm = 0 };
    }

    // ------------------------------------------------------------------ a part's zone and slopes

    /// <summary>The belt's zone at the magazines: 1.08 of the belt (KGV's 374 against 348).</summary>
    public static Design AddBeltZone(Design d) => d with { Armour = Arm(d) with { BeltMagazinesMm = R10(BeltMm(d) * 1.08) } };

    public static Design RemoveBeltZone(Design d) => d with { Armour = Arm(d) with { BeltMagazinesMm = null } };

    /// <summary>Slopes on deck i: 1.4 of its flat at 30°, down to the belt's lower edge.</summary>
    public static Design AddSlopes(Design d, int i) => SetDeck(d, i, x => x with { SlopeMm = R5((x.Mm is double m and > 0 ? m : 50) * 1.4), SlopeDeg = 30 });

    public static Design RemoveSlopes(Design d, int i) => SetDeck(d, i, x => x with { SlopeMm = null, SlopeDeg = null });

    /// <summary>Deck i's zone at the magazines: 1.2 of its flat.</summary>
    public static Design AddDeckZone(Design d, int i) => SetDeck(d, i, x => x with { MagazinesMm = R5((x.Mm is double m and > 0 ? m : 50) * 1.2) });

    public static Design RemoveDeckZone(Design d, int i) => SetDeck(d, i, x => x with { MagazinesMm = null });

    /// <summary>Can deck i take slopes and a zone: over the citadel or the whole length, on the hull's stack.</summary>
    public static bool CitadelDeck(Design d, int i) =>
        DecksOf(d).ElementAtOrDefault(i) is { } x && (x.Extent ?? "citadel") is "citadel" or "full" && (x.Deck ?? 0) >= 0;

    // ------------------------------------------------------------------ layout templates

    /// <summary>The design with no layout: an upright belt on the side over the citadel, no inner bulkhead or boxes, the
    /// main deck flat at its slopes' plate, and no sloped deck under it (a scheme's lower deck). Zones and plates stay.</summary>
    public static Design Plain(Design d)
    {
        var a = Arm(d);
        var decks = DecksOf(d).ToList();
        int main = ProtectionSchemes.MainDeck(decks);
        if (main >= 0)
        {
            var m = decks[main];
            if (m.SlopeMm is > 0)
                decks[main] = m with { Mm = m.SlopeMm, SlopeMm = null, SlopeDeg = null };
            int under = (m.Deck ?? 0) + 1;
            decks.RemoveAll(x => x.Deck == under && (x.Extent ?? "citadel") == "citadel" && x.SlopeMm is > 0);
        }
        return d with
        {
            Armour = a with
            {
                Decks = decks, BeltPosition = null, BeltInclineDeg = null, BeltExtent = null, InnerBulkhead = null, MagazineBox = null,
            },
        };
    }

    /// <summary>A layout put on: the design made plain, then the scheme applied (ProtectionSchemes.Apply), so one layout
    /// replaces another rather than mixing with it.</summary>
    public static Design ApplyLayout(Design d, ProtectionSchemeInput s) => ProtectionSchemes.Apply(Plain(d), s);

    static (ArmourDeckInput? Main, ArmourDeckInput? Lower) LayoutDecks(Design d)
    {
        var decks = DecksOf(d);
        int main = ProtectionSchemes.MainDeck(decks);
        var m = main >= 0 ? decks[main] : null;
        var low = m != null ? decks.FirstOrDefault(x => x.Deck == (m.Deck ?? 0) + 1 && (x.Extent ?? "citadel") == "citadel" && x.SlopeMm is > 0) : null;
        return (m, low);
    }

    /// <summary>The layout's inputs as one string, to tell layouts apart.</summary>
    public static string LayoutKey(Design d)
    {
        var a = d.Armour;
        var (m, low) = LayoutDecks(d);
        var ib = a?.InnerBulkhead;
        var mb = a?.MagazineBox;
        return string.Join("|", a?.BeltPosition ?? "", a?.BeltInclineDeg is > 0 ? $"{a.BeltInclineDeg:0.#}" : "", a?.BeltExtent ?? "",
            ib != null ? $"{ib.Mm}/{ib.MagazinesMm}/{ib.InboardM}" : "", mb != null ? $"{mb.SideMm}/{mb.CrownMm}/{mb.EndMm}/{mb.FloorMm}" : "",
            m is { SlopeMm: > 0 } ? $"{m.Mm}/{m.SlopeMm}/{m.SlopeDeg}" : "", low != null ? $"{low.Mm}/{low.SlopeMm}/{low.SlopeDeg}" : "");
    }

    /// <summary>The layout's form without its figures: what stands where, not how thick.</summary>
    static string LayoutShape(Design d)
    {
        var a = d.Armour;
        var (m, low) = LayoutDecks(d);
        return string.Join("|", a?.BeltPosition ?? "", a?.BeltInclineDeg is > 0, a?.BeltExtent ?? "", Has(d, Part.Inner), Has(d, Part.Box),
            m is { SlopeMm: > 0 }, low != null);
    }

    /// <summary>Has the design any layout input at all?</summary>
    public static bool HasLayout(Design d) => LayoutShape(d) != LayoutShape(Plain(d));

    /// <summary>Which template the design is on: Index (-1: none), Exact when the template puts the design's own figures,
    /// else the first template whose form it has (its own values: Nelson's 18°).</summary>
    public static (int Index, bool Exact) LayoutOf(Design d, IReadOnlyList<ProtectionSchemeInput> schemes)
    {
        if (!HasLayout(d))
            return (-1, false);   // plain: a template that changes nothing here (an unarmoured ship) isn't the one it's on
        string key = LayoutKey(d), shape = LayoutShape(d);
        int byShape = -1;
        for (int i = 0; i < schemes.Count; i++)
        {
            var x = ApplyLayout(d, schemes[i]);
            if (LayoutKey(x) == key)
                return (i, true);
            if (byShape < 0 && LayoutShape(x) == shape)
                byShape = i;
        }
        return (byShape, false);
    }

    /// <summary>What a layout makes of this design, a line per part ("third deck: slopes 110 mm at 22°, flat 80 mm").</summary>
    public static List<string> DescribeLayout(Design d)
    {
        var lines = new List<string>();
        var a = Arm(d);
        foreach (var x in DecksOf(d))
            if (x.SlopeMm is > 0)
                lines.Add($"{Decks.DeckName(x.Deck ?? 0)}: slopes {x.SlopeMm:0} mm at {x.SlopeDeg ?? Armour.SlopeDegDefault:0}°, flat {x.Mm ?? 0:0} mm");
        if (a.BeltPosition == "internal")
            lines.Add($"belt inside the shell, leaning {a.BeltInclineDeg ?? 0:0}°, lower edge {a.BeltBottomMm ?? a.BeltMm ?? 0:0} mm, a lower belt to the inner bottom");
        else if (a.BeltInclineDeg is > 0)
            lines.Add($"belt on the side, leaning {a.BeltInclineDeg:0}°");
        if (a.BeltExtent == "machinery")
            lines.Add("belt over the machinery only");
        if (a.InnerBulkhead is { } ib)
            lines.Add($"inner bulkhead {ib.Mm ?? 0:0} mm" + (ib.MagazinesMm is { } im ? $", {im:0} at the magazines" : ""));
        if (a.MagazineBox is { } mb)
            lines.Add("magazine boxes: " + string.Join(", ", new[] { ("side", mb.SideMm), ("crown", mb.CrownMm), ("end", mb.EndMm), ("floor", mb.FloorMm) }
                .Where(p => p.Item2 is > 0).Select(p => $"{p.Item1} {p.Item2:0}")));
        return lines;
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
        Part.Inner => d.Armour?.InnerBulkhead?.Mm,
        Part.Box => d.Armour?.MagazineBox?.SideMm,
        Part.Wings => (d.Armour?.MagazineWingM ?? 0) * 1000,
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
