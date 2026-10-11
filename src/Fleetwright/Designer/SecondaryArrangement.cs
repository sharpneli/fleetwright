using Fleetwright.Shipgen;

namespace Fleetwright.Designer;

/// <summary>
/// The secondary batteries as positions (docs/designer-ui/secondary.md): where a battery's guns stand, one of four
/// answers, and the edits the Secondary tab makes in pairs. Everything maps onto the design's own fields: a position is
/// mount + tier + stands_on, a pair is one of per_side (or two of count). The yard (the layout) decides the places.
/// </summary>
public static class SecondaryArrangement
{
    /// <summary>Where a battery stands, top to bottom: raised onto the deckhouse, on the deck, an upper casemate (a
    /// housing on the main deck), a lower casemate (in the hull side).</summary>
    public enum Pos { House, Deck, Upper, Lower }

    public static readonly Pos[] All = [Pos.House, Pos.Deck, Pos.Upper, Pos.Lower];

    public static readonly string[] Ids = ["house", "deck", "upper", "lower"];

    public static string Text(Pos p) => p switch
    {
        Pos.House => "on the deckhouse",
        Pos.Deck => "on the deck",
        Pos.Upper => "in upper casemates",
        _ => "in lower casemates",
    };

    public static bool Casemate(Pos p) => p is Pos.Upper or Pos.Lower;

    static IReadOnlyList<BatteryInput> Secs(Design d) => d.Secondary ?? [];

    public static Pos Of(BatteryInput b) => b.Mount == "casemate"
        ? b.Tier == "upper" ? Pos.Upper : Pos.Lower
        : b.StandsOn == "deckhouse" ? Pos.House : Pos.Deck;

    /// <summary>The battery in position p: the fields that no longer apply go (stands_on on a casemate is an error),
    /// the ones that still do keep their spelling.</summary>
    public static BatteryInput WithPos(BatteryInput b, Pos p) => p switch
    {
        Pos.House => b with { Mount = b.Mount == "casemate" ? null : b.Mount, Tier = null, StandsOn = "deckhouse" },
        Pos.Deck => b with { Mount = b.Mount == "casemate" ? null : b.Mount, Tier = null, StandsOn = b.StandsOn == "deck" ? "deck" : null },
        Pos.Upper => b with { Mount = "casemate", Tier = "upper", StandsOn = null },
        _ => b with { Mount = "casemate", Tier = b.Tier == "lower" ? "lower" : null, StandsOn = null },
    };

    /// <summary>The positions a style offers: the deckhouse with raised mounts, casemates on a style that has them.</summary>
    public static bool Offers(Style style, Pos p) => p switch
    {
        Pos.House => style.HasRaisedMounts,
        Pos.Deck => true,
        _ => style.HasCasemates,
    };

    static Design With(Design d, int k, Func<BatteryInput, BatteryInput> f)
    {
        var l = Secs(d).ToList();
        l[k] = f(l[k]);
        return d with { Secondary = l };
    }

    /// <summary>A battery given as a total (count) rather than in pairs; at the ends a mount is one, not two.</summary>
    static bool Counted(BatteryInput b) => b.Count != null;

    static int PairStep(BatteryInput b) => b.Where == "ends" ? 1 : 2;

    /// <summary>Pairs asked for (mounts, for a battery given as a total).</summary>
    public static int Wanted(BatteryInput b) => Counted(b) ? b.MountCount : b.MountsPerSide;

    public static Design SetPos(Design d, int k, Pos p) => With(d, k, b => WithPos(b, p));

    public static Design AddPair(Design d, int k) =>
        With(d, k, b => Counted(b) ? b with { Count = b.MountCount + PairStep(b) } : b with { PerSide = b.MountsPerSide + 1 });

    public static Design RemovePair(Design d, int k) =>
        With(d, k, b => Counted(b) ? b with { Count = Math.Max(0, b.MountCount - PairStep(b)) } : b with { PerSide = Math.Max(0, b.MountsPerSide - 1) });

    /// <summary>One pair of battery k's gun as a new battery (in position p), with the rest of k's settings.</summary>
    static BatteryInput OnePair(BatteryInput b, Pos p) =>
        WithPos(Counted(b) ? b with { Count = PairStep(b) } : b with { PerSide = 1 }, p);

    /// <summary>The + at a position's track: a pair of k if k stands there, else a new battery there with k's gun (one
    /// pair), at the end of the list. A style with one secondary battery moves k there instead. Returns the battery
    /// the pair went to.</summary>
    public static (Design D, int K) AddPairAt(Design d, int k, Pos p, bool takesList)
    {
        var b = Secs(d)[k];
        if (Of(b) == p)
            return (AddPair(d, k), k);
        if (!takesList)
            return (SetPos(d, k, p), k);
        return (d with { Secondary = [.. Secs(d), OnePair(b, p)] }, Secs(d).Count);
    }

    /// <summary>One pair off battery k as a new battery right after it, in the same position.</summary>
    public static Design SplitPair(Design d, int k)
    {
        var l = Secs(d).ToList();
        var b = l[k];
        l[k] = Counted(b) ? b with { Count = Math.Max(0, b.MountCount - PairStep(b)) } : b with { PerSide = Math.Max(0, b.MountsPerSide - 1) };
        l.Insert(k + 1, OnePair(b, Of(b)));
        return d with { Secondary = l };
    }

    /// <summary>A pair of k becomes a pair of j (j's gun and position).</summary>
    public static Design MovePair(Design d, int k, int j) => AddPair(RemovePair(d, k), j);

    /// <summary>The same gun as a new battery right after k, with one pair.</summary>
    public static Design Duplicate(Design d, int k)
    {
        var l = Secs(d).ToList();
        l.Insert(k + 1, OnePair(l[k], Of(l[k])));
        return d with { Secondary = l };
    }

    public static Design MoveBattery(Design d, int k, int dir)
    {
        var l = Secs(d).ToList();
        (l[k], l[k + dir]) = (l[k + dir], l[k]);
        return d with { Secondary = l };
    }

    /// <summary>A secondary mount id's battery and pair number: S1S is battery 0's first pair, SB2P battery 1's second
    /// (<see cref="Batteries.BatteryPrefix"/>), S3 an end mount. Null for anything else.</summary>
    public static (int K, int N, int Side)? ParseId(string id)
    {
        if (id.Length < 2 || id[0] != 'S')
            return null;
        int i = 1, k = 0;
        if (i < id.Length && id[i] is >= 'A' and <= 'Z')
            k = id[i++] - 'A';
        int n0 = i;
        while (i < id.Length && char.IsAsciiDigit(id[i]))
            i++;
        if (i == n0 || !int.TryParse(id.AsSpan(n0, i - n0), out int n))
            return null;
        int side = 0;
        if (i < id.Length)
        {
            if (i != id.Length - 1 || id[i] is not ('S' or 'P'))
                return null;
            side = id[i] == 'S' ? 1 : -1;
        }
        return (k, n, side);
    }
}
