namespace Fleetwright.Shipgen;

/// <summary>Protection schemes (protection-plan.md H8; docs/shipgen/protection-templates.md): a family's layout put on a
/// design. The one implementation: the designer's templates and `shipgen scheme` both call it.</summary>
public static class ProtectionSchemes
{
    /// <summary>The design with the scheme's inputs set; what the scheme leaves null stays as the design has it. Applying
    /// it twice gives what applying it once did (the slopes keep the deck's thickness, the flat takes its share of it).</summary>
    public static Design Apply(Design design, ProtectionSchemeInput s)
    {
        var a = design.Armour ?? new ArmourInput();
        var decks = (a.Decks ?? []).ToList();
        // the citadel's main armour deck: its thickest over the citadel (or the whole length), slopes counted, the lower of
        // equals; a scheme's own light lower deck under it never takes its place, so applying it again finds the same deck
        int roof = -1;
        double Plate(ArmourDeckInput d) => Math.Max(d.Mm ?? 0, d.SlopeMm ?? 0);
        for (int i = 0; i < decks.Count; i++)
            if (decks[i].Deck is int n && n >= 0 && (decks[i].Extent ?? "citadel") is "citadel" or "full" &&
                (roof < 0 || Plate(decks[i]) > Plate(decks[roof]) || Plate(decks[i]) == Plate(decks[roof]) && n > decks[roof].Deck))
                roof = i;
        if (roof >= 0 && (s.SlopeDeg is not null || s.FlatShare is not null))
        {
            var d = decks[roof];
            double slope = d.SlopeMm is > 0 ? d.SlopeMm.Value : d.Mm ?? 0;
            decks[roof] = d with
            {
                SlopeMm = slope, SlopeDeg = s.SlopeDeg ?? d.SlopeDeg, Mm = s.FlatShare is double f ? Math.Round(f * slope) : d.Mm,
            };
        }
        if (roof >= 0 && s.LowerDeck is { } low)
        {
            int under = decks[roof].Deck!.Value + 1;
            if (!decks.Any(d => d.Deck == under && (d.Extent ?? "citadel") == "citadel"))
            {
                int at = decks.FindLastIndex(d => d.Deck <= under) + 1;   // top down
                decks.Insert(at, new ArmourDeckInput
                {
                    Deck = under, Mm = low.Mm ?? 0, SlopeMm = low.SlopeMm, SlopeDeg = low.SlopeDeg, Extent = "citadel",
                });
            }
        }
        double belt = a.BeltMm ?? 0;
        return design with
        {
            Armour = a with
            {
                Decks = decks, BeltPosition = s.BeltPosition ?? a.BeltPosition, BeltInclineDeg = s.BeltInclineDeg ?? a.BeltInclineDeg,
                BeltExtent = s.BeltExtent ?? a.BeltExtent,
                BeltBottomMm = s.BeltBottomShare is double bs && belt > 0 ? Math.Round(bs * belt) : a.BeltBottomMm,
                InnerBulkhead = s.InnerBulkhead ?? a.InnerBulkhead, MagazineBox = s.MagazineBox ?? a.MagazineBox,
            },
        };
    }

    /// <summary>A scheme's unknown keys, as validation names them.</summary>
    public static List<string> Errors(ProtectionSchemeInput s, string where)
    {
        var errs = s.Extra.KeysOrEmpty().Select(k => $"{where}.{k}: not a scheme setting").ToList();
        errs.AddRange(s.LowerDeck?.Extra.KeysOrEmpty().Select(k => $"{where}.lower_deck.{k}: use mm, slope_mm, slope_deg") ?? []);
        if (s.FlatShare is double f && !(f > 0 && f <= 2))
            errs.Add($"{where}.flat_share: the flat's plate as a share of the slopes', over 0 and at most 2");
        if (s.BeltBottomShare is double b && !(b >= 0 && b <= 1))
            errs.Add($"{where}.belt_bottom_share: 0 to 1");
        return errs;
    }
}
