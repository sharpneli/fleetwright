namespace Fleetwright.Shipgen;

/// <summary>A number on the design that can be stepped: how to read and write it, its bounds, and its step (metric).
/// The one definition of an edit: the designer's knobs wrap a lever (adding the label and the units shown), and
/// mono-check steps the same levers the Overview's arrows do. Null in the design means Auto: the engine's own choice,
/// read from <see cref="Auto"/> when known. Sense: which way more of it moves the weight (+1 heavier, -1 lighter, 0
/// either way).</summary>
public sealed record NumberLever(string Id, double Step)
{
    public Func<Design, double?> Get { get; init; } = _ => null;
    public Func<Design, double?, Design> Set { get; init; } = (d, _) => d;

    /// <summary>The value the engine uses when the design leaves it out (the built ship may be null).</summary>
    public Func<Design, Ship?, double?>? Auto { get; init; }

    /// <summary>Can it go back to Auto (null)?</summary>
    public bool CanAuto { get; init; }

    /// <summary>Whole numbers only (a count).</summary>
    public bool Integer { get; init; }

    /// <summary>The style limit's JSON path (the engine's sanity bounds), when it has one.</summary>
    public string[]? LimitPath { get; init; }

    /// <summary>Bounds when the style gives none, and a floor over the style's (speed: 8 kn).</summary>
    public double Lo { get; init; }
    public double Hi { get; init; } = 1e6;
    public double? Floor { get; init; }

    /// <summary>Bounds from the design itself (a carrier's tower has a higher minimum than a warship's).</summary>
    public Func<Design, double>? HiOf { get; init; }
    public Func<Design, double>? LoOf { get; init; }

    /// <summary>Whether it means anything for the design (turret faces need a main battery). Null: always.</summary>
    public Func<Design, bool>? Applies { get; init; }

    public int Sense { get; init; } = 1;

    public bool AppliesTo(Design d) => Applies?.Invoke(d) ?? true;

    /// <summary>The value shown: the design's, else Auto's.</summary>
    public double? Value(Design d, Ship? ship) => Get(d) ?? Auto?.Invoke(d, ship);

    public (double Lo, double Hi) Range(Design d)
    {
        double lo = Lo, hi = Hi;
        if (LimitPath != null)
            foreach (var l in Styles.Get(d).Limits())
                if (l.Path.SequenceEqual(LimitPath))
                    (lo, hi) = (l.Lo, l.Hi);
        if (Floor is { } f)
            lo = Math.Max(lo, f);
        if (LoOf != null)
            lo = Math.Max(lo, LoOf(d));
        if (HiOf != null)
            hi = Math.Min(hi, HiOf(d));
        return (lo, Math.Max(lo, hi));
    }

    /// <summary>The design with the value set, kept inside the range (whole numbers for counts).</summary>
    public Design Apply(Design d, double? value)
    {
        if (value is not { } v)
            return Set(d, null);
        var (lo, hi) = Range(d);
        v = Math.Clamp(v, lo, hi);
        if (Integer)
            v = Math.Round(v);
        return Set(d, Math.Round(v, 3));
    }

    /// <summary>The value `steps` steps of `step` from value (null: the range's low end) on d: off the grid (a design's
    /// own 305 mm, say), the first step goes to the next grid line that way, not past it.</summary>
    public double Target(Design d, double? value, int steps, double step)
    {
        double v = value ?? Range(d).Lo;
        double on = steps > 0 ? Math.Floor(v / step + 1e-9) : Math.Ceiling(v / step - 1e-9);
        return (on + steps) * step;
    }

    /// <summary>The design `steps` steps away (of step, metric Step when null), as clicks on the Overview's arrows
    /// make it; null when the lever doesn't apply or the steps go nowhere (an end stop).</summary>
    public Design? Stepped(Design d, Ship? ship, int steps, double? step = null)
    {
        if (!AppliesTo(d) || Value(d, ship) is not { } v)
            return null;
        var stepped = Apply(d, Target(d, v, steps, step ?? Step));
        return Value(stepped, ship) is { } nv && Math.Abs(nv - v) > 1e-9 ? stepped : null;
    }
}

/// <summary>The levers the designer's Overview steps (its trim sheet), and the edits behind them: one definition,
/// which the designer's knobs wrap and mono-check sweeps.</summary>
public static class Levers
{
    static Design Arm(Design d, Func<ArmourInput, ArmourInput> f) => d with { Armour = f(d.Armour ?? new ArmourInput()) };
    static Design Mach(Design d, Func<MachineryInput, MachineryInput> f) => d with { Machinery = f(d.Machinery ?? new MachineryInput()) };
    static Design HullOf(Design d, Func<HullInput, HullInput> f) => d with { Hull = f(d.Hull ?? new HullInput()) };
    static Design Sup(Design d, Func<SuperstructureInput, SuperstructureInput> f) => d with { Superstructure = f(d.Superstructure ?? new SuperstructureInput()) };
    static Design CrewOf(Design d, Func<CrewInput, CrewInput> f) => d with { Crew = f(d.Crew ?? new CrewInput()) };

    static Design Sec(Design d, int i, Func<BatteryInput, BatteryInput> f)
    {
        var l = (d.Secondary ?? []).ToList();
        l[i] = f(l[i]);
        return d with { Secondary = l };
    }

    static Design EndBelt(Design d, string end, Func<EndBeltInput, EndBeltInput> f) => Arm(d, a =>
    {
        var eb = a.EndBelts ?? new EndBeltsInput();
        var b = f(eb.Of(end) ?? new EndBeltInput());
        return a with { EndBelts = end == "fore" ? eb with { Fore = b } : eb with { Aft = b } };
    });

    static List<T> Replace<T>(List<T> l, int i, Func<T, T> f)
    {
        var r = l.ToList();
        r[i] = f(r[i]);
        return r;
    }

    static int? I(double? v) => v is { } x ? (int)Math.Round(x) : null;

    static bool HasMain(Design d) => d.Main is { Count: > 0 };

    public static readonly NumberLever Speed = new("speed", 0.5)
    {
        Get = d => d.SpeedKn,
        Set = (d, v) => d with { SpeedKn = v },
        LimitPath = ["speed_kn"],
        Floor = 8,   // slower ships aren't seaworthy in rough weather (the user's rule)
    };

    public static readonly NumberLever Range = new("range", 500)
    {
        Get = d => d.RangeNm,
        Set = (d, v) => d with { RangeNm = v },
        Auto = (d, s) => 6000,
        CanAuto = true,
        LimitPath = ["range_nm"],
    };

    public static readonly NumberLever Stress = new("stress", 0.1)
    {
        Get = d => d.Machinery?.Stress,
        Set = (d, v) => Mach(d, m => m with { Stress = v }),
        Auto = (d, s) => s?.Report.Plant.Stress,
        CanAuto = true,
        Lo = 0,
        Hi = 1,
        Sense = -1,
    };

    public static readonly NumberLever Belt = new("belt", 10)
    {
        Get = d => d.Armour?.BeltMm,
        Set = (d, v) => Arm(d, a => a with { BeltMm = v }),
        Auto = (d, s) => 0,
        LimitPath = ["armour", "belt_mm"],
    };

    public static readonly NumberLever UpperBelt = new("upper_belt", 10)
    {
        Get = d => d.Armour?.UpperBelt?.Mm,
        Set = (d, v) => Arm(d, a => a with { UpperBelt = (a.UpperBelt ?? new UpperBeltInput { ToDeck = 0, Extent = "citadel" }) with { Mm = v } }),
        Auto = (d, s) => 0,
        LimitPath = ["armour", "upper_belt", "mm"],
    };

    /// <summary>The main armour deck (Armour.MainDeck), or a new citadel deck on the second deck when there is none.</summary>
    public static readonly NumberLever ArmourDeck = new("armour_deck", 5)
    {
        Get = d => Armour.MainDeck(d.Armour?.Decks ?? []) is int i and >= 0 ? d.Armour!.Decks![i].Mm : null,
        Set = (d, v) => Arm(d, a =>
        {
            var decks = (a.Decks ?? []).ToList();
            int i = Armour.MainDeck(decks);
            if (i < 0)
                decks.Insert(0, new ArmourDeckInput { Deck = 1, Mm = v ?? 0, Extent = "citadel" });
            else
                decks[i] = decks[i] with { Mm = v ?? 0 };
            return a with { Decks = decks };
        }),
        Auto = (d, s) => 0,
        Lo = 0,
        Hi = 2000,
    };

    /// <summary>Deck entry i's flat plate.</summary>
    public static NumberLever DeckMm(int i) => new($"deck{i}_mm", 5)
    {
        Get = d => d.Armour?.Decks?.ElementAtOrDefault(i)?.Mm,
        Set = (d, v) => Arm(d, a => a with { Decks = Replace(a.Decks!, i, x => x with { Mm = v ?? 0 }) }),
        Lo = 0,
        Hi = 2000,
    };

    public static readonly NumberLever Bulkheads = new("bulkheads", 10)
    {
        Get = d => d.Armour?.BulkheadMm,
        Set = (d, v) => Arm(d, a => a with { BulkheadMm = v }),
        Auto = (d, s) => 0,
        LimitPath = ["armour", "bulkhead_mm"],
    };

    /// <summary>Both end belts at once: their mean, and a new value moves each (and its tip, when set) by the same
    /// amount. Only for a design that has end belts.</summary>
    public static readonly NumberLever EndBelts = new("end_belts", 10)
    {
        Get = d => EndBeltMean(d),
        Set = (d, v) =>
        {
            if (v is not { } x || EndBeltMean(d) is not { } mean)
                return d;
            double dv = x - mean;
            foreach (var end in new[] { "fore", "aft" })
                if (d.Armour?.EndBelts?.Of(end) is { Mm: > 0 })
                    d = EndBelt(d, end, b => b with
                    {
                        Mm = Math.Max(0, Math.Round(b.Mm!.Value + dv, 3)),
                        TipMm = b.TipMm is { } t ? Math.Max(0, Math.Round(t + dv, 3)) : null,
                    });
            return d;
        },
        Applies = d => EndBeltMean(d) != null,
        Hi = 2000,
    };

    static double? EndBeltMean(Design d)
    {
        double sum = 0;
        int n = 0;
        foreach (var end in new[] { "fore", "aft" })
            if (d.Armour?.EndBelts?.Of(end) is { Mm: > 0 } b)
            {
                sum += b.Mm!.Value;
                n++;
            }
        return n > 0 ? Math.Round(sum / n, 3) : null;
    }

    /// <summary>A lever over every main battery at once: the first battery's value, and a new value moves each battery
    /// by the same amount (a design with 305 and 234 mm guns steps both by 10 mm).</summary>
    static NumberLever AllMain(string id, double step, Func<BatteryInput, double?> get, Func<BatteryInput, double?, BatteryInput> set,
        string[] limit, Func<Design, Ship?, double?>? auto = null, bool integer = false) =>
        new(id, step)
        {
            Get = d => d.Main is { Count: > 0 } m ? get(m[0]) : null,
            Set = (d, v) =>
            {
                if (d.Main is not { Count: > 0 } m)
                    return d;
                if (v is not { } x)
                    return d with { Main = m.Select(b => set(b, null)).ToList() };
                double first = get(m[0]) ?? auto?.Invoke(d, null) ?? x;
                return d with { Main = m.Select((b, i) => set(b, Math.Max(0, Math.Round((i == 0 ? x : (get(b) ?? first) + x - first), 3)))).ToList() };
            },
            Auto = auto,
            LimitPath = limit,
            Applies = HasMain,
            Integer = integer,
        };

    public static readonly NumberLever MainFaces = AllMain("main_faces", 10,
        b => b.ArmourMm, (b, v) => b with { ArmourMm = v }, ["main", "armour_mm"], (d, s) => 0);

    public static readonly NumberLever MainCalibres = AllMain("main_calibres", 10,
        b => b.CalibreMm, (b, v) => b with { CalibreMm = v }, ["main", "calibre_mm"]);

    public static readonly NumberLever MainRoundsAll = AllMain("main_rounds", 10,
        b => b.RoundsPerGun, (b, v) => b with { RoundsPerGun = v }, ["main", "rounds_per_gun"],
        (d, s) => d.Main is { Count: > 0 } m && m[0].CalibreMm is { } c ? Math.Round(Batteries.RoundsPerGun(c)) : null, integer: true);

    /// <summary>Every armour thickness the design gives, as a percentage (100: as it is; ScaleArmour).</summary>
    public static readonly NumberLever ArmourScale = new("armour_scale", 5)
    {
        Get = d => d.Armour != null || d.Main is { Count: > 0 } ? 100 : null,
        Set = (d, v) => v is { } p ? ScaleArmour(d, p / 100) : d,
        Lo = 5,
    };

    /// <summary>Mounts per side, for a battery given in pairs (warships).</summary>
    public static NumberLever SecPerSide(int i) => new($"sec{i}_per_side", 1)
    {
        Get = d => d.Secondary?.ElementAtOrDefault(i)?.PerSide,
        Set = (d, v) => Sec(d, i, b => b with { PerSide = I(v) }),
        Auto = (d, s) => 0,
        LimitPath = ["secondary", "per_side"],
        Hi = 30,
        Applies = d => d.Secondary?.ElementAtOrDefault(i) is { Count: null },
        Integer = true,
    };

    public static readonly NumberLever AaLight = new("aa_light", 2)
    {
        Get = d => d.Aa?.Light,
        Set = (d, v) => d with { Aa = (d.Aa ?? new AaInput()) with { Light = I(v) } },
        Auto = (d, s) => 0,
        LimitPath = ["aa", "light"],
        Hi = 120,
        Integer = true,
    };

    public static readonly NumberLever BlockCoefficient = new("cb", 0.01)
    {
        Get = d => d.Hull?.BlockCoefficient,
        Set = (d, v) => HullOf(d, h => h with { BlockCoefficient = v }),
        Auto = (d, s) => Styles.Get(d).DefaultBlockCoefficient,
        CanAuto = true,
        LimitPath = ["hull", "block_coefficient"],
        Sense = 0,
    };

    public static readonly NumberLever Freeboard = new("freeboard", 0.05)
    {
        Get = d => d.Hull?.Freeboard,
        Set = (d, v) => HullOf(d, h => h with { Freeboard = v }),
        Auto = (d, s) => 1,
        CanAuto = true,
        LimitPath = ["hull", "freeboard"],
    };

    public static readonly NumberLever TowerLevels = new("tower", 1)
    {
        Get = d => d.Superstructure?.TowerLevels,
        Set = (d, v) => Sup(d, s => s with { TowerLevels = I(v) }),
        Auto = (d, s) => s?.Report.Bridge?.TowerLevels,
        CanAuto = true,
        LimitPath = ["superstructure", "tower_levels"],
        LoOf = d => Styles.Get(d).MinTowerLevels,
        Applies = d => Styles.Get(d).MinTowerLevels > 0,
        Integer = true,
    };

    public static readonly NumberLever Endurance = new("endurance", 5)
    {
        Get = d => d.Crew?.EnduranceDays,
        Set = (d, v) => CrewOf(d, c => c with { EnduranceDays = v }),
        Auto = (d, s) => s?.Report.Crew?.EnduranceDays,
        CanAuto = true,
        Lo = 1,
        Hi = 365,
    };

    /// <summary>One of the Overview's levers; Deck: the deck entry a deck's row is for (-1 for the others).</summary>
    public sealed record TrimLever(NumberLever Lever, int Deck = -1);

    /// <summary>The Overview's levers, in its order: the rows the designer's Overview shows and mono-check sweeps. The
    /// armour deck stands for each of the design's deck entries, highest first (the main deck is often a thin capping
    /// deck over a thicker one below), or for the main armour deck when it has none.</summary>
    public static List<TrimLever> Trim(Design d)
    {
        var decks = d.Armour?.Decks ?? [];
        var list = new List<TrimLever> { new(Speed), new(Range), new(Stress), new(Belt), new(UpperBelt) };
        if (decks.Count == 0)
            list.Add(new(ArmourDeck));
        else
            list.AddRange(Enumerable.Range(0, decks.Count).OrderBy(i => decks[i].Deck ?? 1).Select(i => new TrimLever(DeckMm(i), i)));
        list.AddRange(new[] { Bulkheads, EndBelts, MainFaces, ArmourScale, MainCalibres, MainRoundsAll, SecPerSide(0), AaLight,
            Freeboard, BlockCoefficient, TowerLevels, Endurance }.Select(l => new TrimLever(l)));
        return list;
    }

    /// <summary>Every armour thickness the design gives, times <paramref name="f"/> to the nearest mm: belt (its lower
    /// edge, its zone over the magazines), upper belt, end belts (and tips and bulkheads), citadel bulkheads, decks (their
    /// slopes and magazine zones), flight deck, the steering gear box, the inner bulkhead, the magazine boxes and the main
    /// turret faces (the barbettes follow the faces). Thicknesses the design leaves to the engine stay so.</summary>
    public static Design ScaleArmour(Design d, double f)
    {
        static double? M(double? mm, double f) => mm is { } x ? Math.Round(x * f) : null;
        if (d.Armour is { } a)
            d = d with
            {
                Armour = a with
                {
                    BeltMm = M(a.BeltMm, f),
                    BeltBottomMm = M(a.BeltBottomMm, f),
                    BeltMagazinesMm = M(a.BeltMagazinesMm, f),
                    BulkheadMm = M(a.BulkheadMm, f),
                    FlightDeckMm = M(a.FlightDeckMm, f),
                    Decks = a.Decks?.Select(x => x with { Mm = M(x.Mm, f), SlopeMm = M(x.SlopeMm, f), MagazinesMm = M(x.MagazinesMm, f) }).ToList(),
                    InnerBulkhead = a.InnerBulkhead is { } ib ? ib with { Mm = M(ib.Mm, f), MagazinesMm = M(ib.MagazinesMm, f) } : null,
                    MagazineBox = a.MagazineBox is { } mb ? mb with
                    {
                        SideMm = M(mb.SideMm, f), CrownMm = M(mb.CrownMm, f), EndMm = M(mb.EndMm, f), FloorMm = M(mb.FloorMm, f),
                    } : null,
                    UpperBelt = a.UpperBelt is { } u ? u with { Mm = M(u.Mm, f) } : null,
                    EndBelts = a.EndBelts is { } eb ? eb with
                    {
                        Fore = eb.Fore is { } fo ? fo with { Mm = M(fo.Mm, f), TipMm = M(fo.TipMm, f), BulkheadMm = M(fo.BulkheadMm, f) } : null,
                        Aft = eb.Aft is { } af ? af with { Mm = M(af.Mm, f), TipMm = M(af.TipMm, f), BulkheadMm = M(af.BulkheadMm, f) } : null,
                    } : null,
                    SteeringBox = a.SteeringBox is { } sb ? sb with { Mm = M(sb.Mm, f), DeckMm = M(sb.DeckMm, f), BulkheadMm = M(sb.BulkheadMm, f) } : null,
                },
            };
        if (d.Main is { Count: > 0 } m)
            d = d with { Main = m.Select(b => b with { ArmourMm = M(b.ArmourMm, f) }).ToList() };
        return d;
    }
}
