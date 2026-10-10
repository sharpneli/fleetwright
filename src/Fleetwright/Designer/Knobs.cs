using Fleetwright.Shipgen;

namespace Fleetwright.Designer;

/// <summary>
/// One control the designer offers over the design: how to read it, how to write it, and what it means. The trim
/// sheet and the section panels show the same knobs, so an edit in one shows in the other. Knobs never hold state;
/// the design does.
/// </summary>
public abstract class Knob(string id, string label)
{
    /// <summary>Stable id: history entries made by the same knob in a row merge on it.</summary>
    public string Id { get; } = id;
    public string Label { get; } = label;

    /// <summary>Whether the knob means anything for the design (turret faces need a main battery). Null: always.</summary>
    public Func<Design, bool>? Applies { get; init; }

    public bool AppliesTo(Design d) => Applies?.Invoke(d) ?? true;
}

/// <summary>A number. Null in the design means Auto: the engine's own choice, shown from <see cref="Auto"/> when
/// known.</summary>
public sealed class NumberKnob(string id, string label, Quantity q, double stepMetric, double stepImperial) : Knob(id, label)
{
    public Quantity Quantity { get; } = q;
    public double StepMetric { get; } = stepMetric;
    public double StepImperial { get; } = stepImperial;

    public required Func<Design, double?> Get { get; init; }
    public required Func<Design, double?, Design> Set { get; init; }

    /// <summary>The value the engine uses when the design leaves it out (the built ship may be null).</summary>
    public Func<Design, Ship?, double?>? Auto { get; init; }

    /// <summary>Can the knob go back to Auto (null)?</summary>
    public bool CanAuto { get; init; }

    /// <summary>The style limit's JSON path (the engine's sanity bounds), when it has one.</summary>
    public string[]? LimitPath { get; init; }

    /// <summary>Bounds when the style gives none, and a floor over the style's (speed: 8 kn).</summary>
    public double Lo { get; init; }
    public double Hi { get; init; } = 1e6;
    public double? Floor { get; init; }

    /// <summary>Bounds from the design itself (a group can't step more turrets than it has; a carrier's tower has a
    /// higher minimum than a warship's).</summary>
    public Func<Design, double>? HiOf { get; init; }
    public Func<Design, double>? LoOf { get; init; }

    public bool Integer => Quantity == Quantity.Count;

    /// <summary>One step in metric, for the units shown.</summary>
    public double Step(UnitSystem u) => u == UnitSystem.Metric ? StepMetric : StepImperial * Units.Factor(Quantity, u);

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
}

/// <summary>One of a few named values (null: Auto).</summary>
public sealed class ChoiceKnob(string id, string label, string[] values, string[] labels) : Knob(id, label)
{
    public string[] Values { get; } = values;
    public string[] Labels { get; } = labels;
    public required Func<Design, string?> Get { get; init; }
    public required Func<Design, string?, Design> Set { get; init; }

    /// <summary>The value shown when the design leaves it out.</summary>
    public string? Default { get; init; }
}

/// <summary>On or off (null: the engine's default, <see cref="Default"/>).</summary>
public sealed class ToggleKnob(string id, string label) : Knob(id, label)
{
    public required Func<Design, bool?> Get { get; init; }
    public required Func<Design, bool?, Design> Set { get; init; }
    public bool Default { get; init; }

    public bool Value(Design d) => Get(d) ?? Default;
}

/// <summary>The knobs, by section. Per-battery and per-deck knobs are made for an index.</summary>
public static class Knobs
{
    // ------------------------------------------------------------------ record edits

    static Design Arm(Design d, Func<ArmourInput, ArmourInput> f) => d with { Armour = f(d.Armour ?? new ArmourInput()) };

    static Design Main(Design d, int i, Func<BatteryInput, BatteryInput> f)
    {
        var l = (d.Main ?? []).ToList();
        l[i] = f(l[i]);
        return d with { Main = l };
    }

    static Design Sec(Design d, int i, Func<BatteryInput, BatteryInput> f)
    {
        var l = (d.Secondary ?? []).ToList();
        l[i] = f(l[i]);
        return d with { Secondary = l };
    }

    static Design Mach(Design d, Func<MachineryInput, MachineryInput> f) => d with { Machinery = f(d.Machinery ?? new MachineryInput()) };
    static Design Hull(Design d, Func<HullInput, HullInput> f) => d with { Hull = f(d.Hull ?? new HullInput()) };
    static Design Sup(Design d, Func<SuperstructureInput, SuperstructureInput> f) => d with { Superstructure = f(d.Superstructure ?? new SuperstructureInput()) };
    static Design Crew(Design d, Func<CrewInput, CrewInput> f) => d with { Crew = f(d.Crew ?? new CrewInput()) };

    static Design Fc(Design d, string battery, Func<DirectorInput, DirectorInput> f)
    {
        var fc = d.FireControl ?? new FireControlInput();
        var dir = f(fc.Of(battery) ?? new DirectorInput());
        fc = battery switch { "main" => fc with { Main = dir }, "secondary" => fc with { Secondary = dir }, _ => fc with { Aa = dir } };
        return d with { FireControl = fc };
    }

    static Design EndBelt(Design d, string end, Func<EndBeltInput, EndBeltInput> f) => Arm(d, a =>
    {
        var eb = a.EndBelts ?? new EndBeltsInput();
        var b = f(eb.Of(end) ?? new EndBeltInput());
        return a with { EndBelts = end == "fore" ? eb with { Fore = b } : eb with { Aft = b } };
    });

    static int? I(double? v) => v is { } x ? (int)Math.Round(x) : null;

    static bool HasMain(Design d) => d.Main is { Count: > 0 };
    static bool HasSecondary(Design d) => d.Secondary is { Count: > 0 };

    /// <summary>The main armour deck's entry: the first over the citadel (or the whole length), else the first.</summary>
    static int MainDeckIndex(Design d)
    {
        var decks = d.Armour?.Decks;
        if (decks is not { Count: > 0 })
            return -1;
        int i = decks.FindIndex(x => x.Extent is null or "citadel" or "full");
        return i >= 0 ? i : 0;
    }

    // ------------------------------------------------------------------ the knobs

    public static readonly NumberKnob Speed = new("speed", "Speed", Quantity.Speed, 0.5, 0.5)
    {
        Get = d => d.SpeedKn,
        Set = (d, v) => d with { SpeedKn = v },
        LimitPath = ["speed_kn"],
        Floor = 8,   // slower ships aren't seaworthy in rough weather (the user's rule)
    };

    public static readonly NumberKnob Range = new("range", "Range", Quantity.Range, 500, 500)
    {
        Get = d => d.RangeNm,
        Set = (d, v) => d with { RangeNm = v },
        Auto = (d, s) => 6000,
        CanAuto = true,
        LimitPath = ["range_nm"],
    };

    public static readonly NumberKnob Belt = new("belt", "Belt", Quantity.Armour, 10, 0.5)
    {
        Get = d => d.Armour?.BeltMm,
        Set = (d, v) => Arm(d, a => a with { BeltMm = v }),
        Auto = (d, s) => 0,
        LimitPath = ["armour", "belt_mm"],
    };

    public static readonly NumberKnob BeltBottom = new("belt_bottom", "Belt, lower edge", Quantity.Armour, 10, 0.5)
    {
        Get = d => d.Armour?.BeltBottomMm,
        Set = (d, v) => Arm(d, a => a with { BeltBottomMm = v }),
        Auto = (d, s) => d.Armour?.BeltMm ?? 0,
        CanAuto = true,
        LimitPath = ["armour", "belt_bottom_mm"],
    };

    public static readonly NumberKnob UpperBelt = new("upper_belt", "Upper belt", Quantity.Armour, 10, 0.5)
    {
        Get = d => d.Armour?.UpperBelt?.Mm,
        Set = (d, v) => Arm(d, a => a with { UpperBelt = (a.UpperBelt ?? new UpperBeltInput { ToDeck = 0, Extent = "citadel" }) with { Mm = v } }),
        Auto = (d, s) => 0,
        LimitPath = ["armour", "upper_belt", "mm"],
    };

    public static readonly NumberKnob ArmourDeck = new("armour_deck", "Armour deck", Quantity.Armour, 5, 0.25)
    {
        Get = d => MainDeckIndex(d) is int i and >= 0 ? d.Armour!.Decks![i].Mm : null,
        Set = (d, v) => Arm(d, a =>
        {
            var decks = (a.Decks ?? []).ToList();
            int i = MainDeckIndex(d);
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

    public static readonly NumberKnob Bulkheads = new("bulkheads", "Citadel bulkheads", Quantity.Armour, 10, 0.5)
    {
        Get = d => d.Armour?.BulkheadMm,
        Set = (d, v) => Arm(d, a => a with { BulkheadMm = v }),
        Auto = (d, s) => 0,
        LimitPath = ["armour", "bulkhead_mm"],
    };

    public static readonly NumberKnob Tds = new("tds", "Torpedo protection depth", Quantity.Length, 0.5, 1)
    {
        Get = d => d.Armour?.TdsM,
        Set = (d, v) => Arm(d, a => a with { TdsM = v }),
        Auto = (d, s) => 0,
        LimitPath = ["armour", "tds_m"],
    };

    public static NumberKnob EndBeltMm(string end) => new($"end_belt_{end}", $"End belt {end}", Quantity.Armour, 10, 0.5)
    {
        Get = d => d.Armour?.EndBelts?.Of(end)?.Mm,
        Set = (d, v) => EndBelt(d, end, b => b with { Mm = v }),
        Auto = (d, s) => 0,
        LimitPath = ["armour", "end_belts", end, "mm"],
    };

    public static NumberKnob EndBeltReach(string end) => new($"end_belt_{end}_reach", $"End belt {end}, reach", Quantity.Ratio, 0.1, 0.1)
    {
        Get = d => d.Armour?.EndBelts?.Of(end)?.Reach,
        Set = (d, v) => EndBelt(d, end, b => b with { Reach = v }),
        Auto = (d, s) => 1,
        CanAuto = true,
        LimitPath = ["armour", "end_belts", end, "reach"],
        Applies = d => (d.Armour?.EndBelts?.Of(end)?.Mm ?? 0) > 0,
    };

    // armour decks, per entry
    public static readonly string[] DeckExtents = ["citadel", "full", "fore", "aft", "ends"];

    public static NumberKnob DeckMm(int i) => new($"deck{i}_mm", "Thickness", Quantity.Armour, 5, 0.25)
    {
        Get = d => d.Armour?.Decks?.ElementAtOrDefault(i)?.Mm,
        Set = (d, v) => Arm(d, a => a with { Decks = Replace(a.Decks!, i, x => x with { Mm = v ?? 0 }) }),
        Lo = 0,
        Hi = 2000,
    };

    public static NumberKnob DeckLevel(int i) => new($"deck{i}_deck", "On deck", Quantity.Count, 1, 1)
    {
        Get = d => d.Armour?.Decks?.ElementAtOrDefault(i)?.Deck,
        Set = (d, v) => Arm(d, a => a with { Decks = Replace(a.Decks!, i, x => x with { Deck = I(v) ?? 1 }) }),
        Lo = -2,
        Hi = 8,
    };

    public static ChoiceKnob DeckExtent(int i) => new($"deck{i}_extent", "Over", DeckExtents, ["citadel", "full length", "fore end", "aft end", "both ends"])
    {
        Get = d => d.Armour?.Decks?.ElementAtOrDefault(i)?.Extent,
        Set = (d, v) => Arm(d, a => a with { Decks = Replace(a.Decks!, i, x => x with { Extent = v }) }),
        Default = "citadel",
    };

    static List<T> Replace<T>(List<T> l, int i, Func<T, T> f)
    {
        var r = l.ToList();
        r[i] = f(r[i]);
        return r;
    }

    // main batteries, per battery
    public static NumberKnob MainCalibre(int i) => new($"main{i}_cal", "Calibre", Quantity.Calibre, 10, 0.5)
    {
        Get = d => d.Main?.ElementAtOrDefault(i)?.CalibreMm,
        Set = (d, v) => Main(d, i, b => b with { CalibreMm = v }),
        LimitPath = ["main", "calibre_mm"],
    };

    public static NumberKnob MainLength(int i) => new($"main{i}_len", "Length, calibres", Quantity.Ratio, 1, 1)
    {
        Get = d => d.Main?.ElementAtOrDefault(i)?.CalibreLength,
        Set = (d, v) => Main(d, i, b => b with { CalibreLength = v }),
        LimitPath = ["main", "calibre_length"],
    };

    public static NumberKnob MainBarrels(int i) => new($"main{i}_barrels", "Guns per turret", Quantity.Count, 1, 1)
    {
        Get = d => d.Main?.ElementAtOrDefault(i)?.Barrels,
        Set = (d, v) => Main(d, i, b => b with { Barrels = I(v) }),
        LimitPath = ["main", "barrels"],
        Hi = 4,
    };

    public static NumberKnob MainGroup(int i, string group) => new($"main{i}_{group}", group switch
    {
        "fore" => "Turrets forward",
        "aft" => "Turrets aft",
        "mid" => "Turrets amidships",
        _ => "Wing pairs",
    }, Quantity.Count, 1, 1)
    {
        Get = d => d.Main?.ElementAtOrDefault(i) is { } b ? group switch
        {
            "fore" => b.Fore,
            "aft" => b.Aft,
            "mid" => b.Mid,
            _ => b.Wing,
        } : null,
        Auto = (d, s) => 0,
        Set = (d, v) => Main(d, i, b => group switch
        {
            "fore" => b with { Fore = I(v) },
            "aft" => b with { Aft = I(v) },
            "mid" => b with { Mid = I(v) },
            _ => b with { Wing = I(v) },
        }),
        LimitPath = ["main", group],
        Hi = 6,
        Applies = d => group switch
        {
            "mid" => Styles.Get(d).HasMidshipsTurrets,
            "wing" => Styles.Get(d).HasWingTurrets,
            _ => true,
        },
    };

    /// <summary>How many of an end group's turrets stand stepped (the outermost, on the deck, counts).</summary>
    public static NumberKnob MainStepped(int i, string end) => new($"main{i}_sf_{end}", end == "fore" ? "Superfiring forward" : "Superfiring aft",
        Quantity.Count, 1, 1)
    {
        Get = d => d.Main?.ElementAtOrDefault(i) is { } b ? (end == "fore" ? Layout.SteppedCounts(b).Fore : Layout.SteppedCounts(b).Aft) : null,
        Set = (d, v) => Main(d, i, b =>
        {
            var (f, a) = Layout.SteppedCounts(b);
            return b with { Superfire = end == "fore" ? new Superfire(null, I(v), a) : new Superfire(null, f, I(v)) };
        }),
        HiOf = d => d.Main?.ElementAtOrDefault(i) is { } b ? (end == "fore" ? b.Fore ?? 0 : b.Aft ?? 0) : 0,
        Applies = d => d.Main?.ElementAtOrDefault(i) is { } b && (end == "fore" ? b.Fore ?? 0 : b.Aft ?? 0) > 1,
    };

    public static ToggleKnob MainEchelon(int i) => new($"main{i}_echelon", "Wing turrets in echelon")
    {
        Get = d => d.Main?.ElementAtOrDefault(i)?.Echelon,
        Set = (d, v) => Main(d, i, b => b with { Echelon = v }),
        Applies = d => (d.Main?.ElementAtOrDefault(i)?.Wing ?? 0) > 0,
    };

    public static NumberKnob MainArmour(int i) => new($"main{i}_armour", "Turret faces", Quantity.Armour, 10, 0.5)
    {
        Get = d => d.Main?.ElementAtOrDefault(i)?.ArmourMm,
        Set = (d, v) => Main(d, i, b => b with { ArmourMm = v }),
        Auto = (d, s) => 0,
        LimitPath = ["main", "armour_mm"],
        Applies = d => d.Main?.Count > i,
    };

    public static NumberKnob MainRounds(int i) => new($"main{i}_rounds", "Rounds per gun", Quantity.Count, 10, 10)
    {
        Get = d => d.Main?.ElementAtOrDefault(i)?.RoundsPerGun,
        Set = (d, v) => Main(d, i, b => b with { RoundsPerGun = v }),
        Auto = (d, s) => d.Main?.ElementAtOrDefault(i)?.CalibreMm is { } c ? Math.Round(Batteries.RoundsPerGun(c)) : null,
        CanAuto = true,
        LimitPath = ["main", "rounds_per_gun"],
        Applies = d => d.Main?.Count > i,
    };

    // secondary batteries, per battery
    public static NumberKnob SecCalibre(int i) => new($"sec{i}_cal", "Calibre", Quantity.Calibre, 5, 0.5)
    {
        Get = d => d.Secondary?.ElementAtOrDefault(i)?.CalibreMm,
        Set = (d, v) => Sec(d, i, b => b with { CalibreMm = v }),
        LimitPath = ["secondary", "calibre_mm"],
    };

    public static NumberKnob SecLength(int i) => new($"sec{i}_len", "Length, calibres", Quantity.Ratio, 1, 1)
    {
        Get = d => d.Secondary?.ElementAtOrDefault(i)?.CalibreLength,
        Set = (d, v) => Sec(d, i, b => b with { CalibreLength = v }),
        LimitPath = ["secondary", "calibre_length"],
    };

    public static NumberKnob SecBarrels(int i) => new($"sec{i}_barrels", "Guns per mount", Quantity.Count, 1, 1)
    {
        Get = d => d.Secondary?.ElementAtOrDefault(i)?.Barrels,
        Set = (d, v) => Sec(d, i, b => b with { Barrels = I(v) }),
        LimitPath = ["secondary", "barrels"],
        Hi = 4,
    };

    /// <summary>Mounts per side, for a battery given in pairs (warships).</summary>
    public static NumberKnob SecPerSide(int i) => new($"sec{i}_per_side", "Mounts per side", Quantity.Count, 1, 1)
    {
        Get = d => d.Secondary?.ElementAtOrDefault(i)?.PerSide,
        Set = (d, v) => Sec(d, i, b => b with { PerSide = I(v) }),
        Auto = (d, s) => 0,
        LimitPath = ["secondary", "per_side"],
        Hi = 30,
        Applies = d => d.Secondary?.ElementAtOrDefault(i) is { Count: null },
    };

    /// <summary>All the mounts, for a battery given as a total (merchants, carriers; may be odd).</summary>
    public static NumberKnob SecCount(int i) => new($"sec{i}_count", "Mounts", Quantity.Count, 1, 1)
    {
        Get = d => d.Secondary?.ElementAtOrDefault(i)?.Count,
        Set = (d, v) => Sec(d, i, b => b with { Count = I(v) }),
        LimitPath = ["secondary", "count"],
        Hi = 60,
        Applies = d => d.Secondary?.ElementAtOrDefault(i) is { Count: not null },
    };

    public static ChoiceKnob SecMount(int i) => new($"sec{i}_mount", "Mount", ["deck", "casemate"], ["on deck", "casemate"])
    {
        Get = d => d.Secondary?.ElementAtOrDefault(i)?.Mount,
        Set = (d, v) => Sec(d, i, b => b with { Mount = v }),
        Default = "deck",
    };

    public static NumberKnob SecArmour(int i) => new($"sec{i}_armour", "Shields", Quantity.Armour, 5, 0.25)
    {
        Get = d => d.Secondary?.ElementAtOrDefault(i)?.ArmourMm,
        Set = (d, v) => Sec(d, i, b => b with { ArmourMm = v }),
        Auto = (d, s) => 25,
        CanAuto = true,
        Lo = 0,
        Hi = 2000,
    };

    public static readonly NumberKnob AaHeavy = new("aa_heavy", "Heavy AA mounts", Quantity.Count, 1, 1)
    {
        Get = d => d.Aa?.Heavy,
        Set = (d, v) => d with { Aa = (d.Aa ?? new AaInput()) with { Heavy = I(v) } },
        Auto = (d, s) => 0,
        LimitPath = ["aa", "heavy"],
        Hi = 60,
    };

    public static readonly NumberKnob AaLight = new("aa_light", "Light AA mounts", Quantity.Count, 2, 2)
    {
        Get = d => d.Aa?.Light,
        Set = (d, v) => d with { Aa = (d.Aa ?? new AaInput()) with { Light = I(v) } },
        Auto = (d, s) => 0,
        LimitPath = ["aa", "light"],
        Hi = 120,
    };

    public static readonly NumberKnob TorpedoMounts = new("tt_mounts", "Torpedo mounts", Quantity.Count, 1, 1)
    {
        Get = d => d.Torpedoes?.Mounts,
        Set = (d, v) => d with { Torpedoes = (d.Torpedoes ?? new TorpedoInput { Tubes = 3 }) with { Mounts = I(v) } },
        Auto = (d, s) => 0,
        LimitPath = ["torpedoes", "mounts"],
        Hi = 10,
    };

    public static readonly NumberKnob TorpedoTubes = new("tt_tubes", "Tubes per mount", Quantity.Count, 1, 1)
    {
        Get = d => d.Torpedoes?.Tubes,
        Set = (d, v) => d with { Torpedoes = (d.Torpedoes ?? new TorpedoInput()) with { Tubes = I(v) } },
        LimitPath = ["torpedoes", "tubes"],
        Hi = 10,
        Applies = d => (d.Torpedoes?.Mounts ?? 0) > 0,
    };

    // speed and machinery
    public static readonly NumberKnob Stress = new("stress", "Design rating", Quantity.Ratio, 0.1, 0.1)
    {
        Get = d => d.Machinery?.Stress,
        Set = (d, v) => Mach(d, m => m with { Stress = v }),
        Auto = (d, s) => s?.Report.Plant.Stress,
        CanAuto = true,
        Lo = 0,
        Hi = 1,
    };

    public static readonly NumberKnob Shafts = new("shafts", "Shafts", Quantity.Count, 1, 1)
    {
        Get = d => d.Machinery?.Shafts,
        Set = (d, v) => Mach(d, m => m with { Shafts = I(v) }),
        Auto = (d, s) => s?.Report.Plant.Shafts,
        CanAuto = true,
        Lo = 1,
        Hi = 6,
    };

    public static readonly ChoiceKnob Arrangement = new("arrangement", "Machinery", ["grouped", "unit"], ["grouped", "unit (alternating)"])
    {
        Get = d => d.Machinery?.Arrangement?.Name,
        Set = (d, v) => Mach(d, m => m with { Arrangement = v == null ? null : new Arrangement(v, null) }),
        Default = "grouped",
        Applies = d => d.Machinery?.Arrangement?.Rooms == null,   // a custom room list is edited elsewhere (later)
    };

    // hull and upperworks
    public static readonly NumberKnob BlockCoefficient = new("cb", "Hull form, full to fine", Quantity.Ratio, 0.01, 0.01)
    {
        Get = d => d.Hull?.BlockCoefficient,
        Set = (d, v) => Hull(d, h => h with { BlockCoefficient = v }),
        Auto = (d, s) => Styles.Get(d).DefaultBlockCoefficient,
        CanAuto = true,
        LimitPath = ["hull", "block_coefficient"],
    };

    public static readonly NumberKnob Freeboard = new("freeboard", "Freeboard (x standard)", Quantity.Ratio, 0.05, 0.05)
    {
        Get = d => d.Hull?.Freeboard,
        Set = (d, v) => Hull(d, h => h with { Freeboard = v }),
        Auto = (d, s) => 1,
        CanAuto = true,
        LimitPath = ["hull", "freeboard"],
    };

    /// <summary>The forecastle presets (hull.raised); a custom list shows as its own entry.</summary>
    public static readonly ChoiceKnob Raised = new("raised", "Raised hull",
        ["flush", "forecastle_bridge", "forecastle_funnels", "forecastle_poop", "breastwork"],
        ["flush deck", "forecastle to the bridge", "forecastle to the funnels", "forecastle and poop", "raised amidships"])
    {
        Get = d => RaisedName(d.Hull?.Raised),
        Set = (d, v) => Hull(d, h => h with { Raised = RaisedOf(v) }),
        Default = "flush",
        Applies = d => d.StyleName == "warship",
    };

    static string? RaisedName(List<RaisedInput>? r)
    {
        if (r == null)
            return null;
        string key = string.Join(";", r.Select(x => $"{x.From}-{x.To}-{x.Decks ?? 1}"));
        return key switch
        {
            "" => "flush",
            "bow-bridge-1" => "forecastle_bridge",
            "bow-funnels-1" => "forecastle_funnels",
            "bow-bridge-1;aft_group-stern-1" => "forecastle_poop",
            "bridge-aft_control-1" => "breastwork",
            _ => "custom",
        };
    }

    static List<RaisedInput>? RaisedOf(string? name) => name switch
    {
        null => null,
        "flush" => [],
        "forecastle_bridge" => [new RaisedInput { From = "bow", To = "bridge", Decks = 1 }],
        "forecastle_funnels" => [new RaisedInput { From = "bow", To = "funnels", Decks = 1 }],
        "forecastle_poop" => [new RaisedInput { From = "bow", To = "bridge", Decks = 1 }, new RaisedInput { From = "aft_group", To = "stern", Decks = 1 }],
        "breastwork" => [new RaisedInput { From = "bridge", To = "aft_control", Decks = 1 }],
        _ => null,
    };

    public static readonly NumberKnob TowerLevels = new("tower", "Bridge tower levels", Quantity.Count, 1, 1)
    {
        Get = d => d.Superstructure?.TowerLevels,
        Set = (d, v) => Sup(d, s => s with { TowerLevels = I(v) }),
        Auto = (d, s) => s?.Report.Bridge?.TowerLevels,
        CanAuto = true,
        LoOf = d => Styles.Get(d).MinTowerLevels,
        Hi = 10,
        Applies = d => Styles.Get(d).MinTowerLevels > 0,
    };

    public static readonly NumberKnob DeckhouseLevels = new("deckhouse", "Deckhouse levels", Quantity.Count, 1, 1)
    {
        Get = d => d.Superstructure?.DeckhouseLevels,
        Set = (d, v) => Sup(d, s => s with { DeckhouseLevels = I(v) }),
        Auto = (d, s) => 1,
        CanAuto = true,
        Lo = 1,
        Hi = 4,
        Applies = d => Styles.Get(d).HasDeckhouseLevels,
    };

    public static readonly ToggleKnob AftControl = new("aft_control", "Aft control position")
    {
        Get = d => d.Superstructure?.AftControl,
        Set = (d, v) => Sup(d, s => s with { AftControl = v }),
        Default = true,
        Applies = d => Styles.Get(d).HasControlTowers,
    };

    // fire control
    public static NumberKnob Directors(string battery) => new($"fc_{battery}", battery switch
    {
        "main" => "Main directors",
        "secondary" => "Secondary directors",
        _ => "AA directors",
    }, Quantity.Count, 1, 1)
    {
        Get = d => d.FireControl?.Of(battery)?.Directors,
        Set = (d, v) => Fc(d, battery, x => x with { Directors = v }),
        Auto = (d, s) => 0,
        Lo = 0,
        Hi = 6,
    };

    public static NumberKnob Rangefinder(string battery) => new($"fc_{battery}_rf", "Rangefinder base", Quantity.Length, 0.5, 1)
    {
        Get = d => d.FireControl?.Of(battery)?.RangefinderM,
        Set = (d, v) => Fc(d, battery, x => x with { RangefinderM = v }),
        Auto = (d, s) => 0,
        Lo = 0,
        Hi = 15,
        Applies = d => (d.FireControl?.Of(battery)?.Directors ?? 0) > 0,
    };

    // crew
    public static readonly NumberKnob Endurance = new("endurance", "Stores for", Quantity.Days, 5, 5)
    {
        Get = d => d.Crew?.EnduranceDays,
        Set = (d, v) => Crew(d, c => c with { EnduranceDays = v }),
        Auto = (d, s) => s?.Report.Crew?.EnduranceDays,
        CanAuto = true,
        Lo = 1,
        Hi = 365,
    };

    public static readonly ToggleKnob Distiller = new("distiller", "Distiller")
    {
        Get = d => d.Crew?.Distiller,
        Set = (d, v) => Crew(d, c => c with { Distiller = v }),
        Default = true,
    };

    /// <summary>Every knob the design has, per-battery and per-deck ones included.</summary>
    public static IEnumerable<Knob> All(Design d)
    {
        Knob[] fixedKnobs =
        [
            Speed, Range, Belt, BeltBottom, UpperBelt, ArmourDeck, Bulkheads, Tds, AaHeavy, AaLight, TorpedoMounts, TorpedoTubes,
            Stress, Shafts, Arrangement, BlockCoefficient, Freeboard, Raised, TowerLevels, DeckhouseLevels, AftControl,
            Endurance, Distiller,
        ];
        foreach (var k in fixedKnobs)
            yield return k;
        foreach (var end in new[] { "fore", "aft" })
        {
            yield return EndBeltMm(end);
            yield return EndBeltReach(end);
        }
        for (int i = 0; i < (d.Armour?.Decks?.Count ?? 0); i++)
        {
            yield return DeckMm(i);
            yield return DeckLevel(i);
            yield return DeckExtent(i);
        }
        for (int i = 0; i < (d.Main?.Count ?? 0); i++)
        {
            yield return MainCalibre(i);
            yield return MainLength(i);
            yield return MainBarrels(i);
            foreach (var g in new[] { "fore", "aft", "mid", "wing" })
                yield return MainGroup(i, g);
            yield return MainStepped(i, "fore");
            yield return MainStepped(i, "aft");
            yield return MainEchelon(i);
            yield return MainArmour(i);
            yield return MainRounds(i);
        }
        for (int i = 0; i < (d.Secondary?.Count ?? 0); i++)
        {
            yield return SecCalibre(i);
            yield return SecLength(i);
            yield return SecBarrels(i);
            yield return SecPerSide(i);
            yield return SecCount(i);
            yield return SecMount(i);
            yield return SecArmour(i);
        }
        foreach (var b in new[] { "main", "secondary", "aa" })
        {
            yield return Directors(b);
            yield return Rangefinder(b);
        }
    }

    /// <summary>The overview's trim sheet: the continuous levers that change weight (research §15.7).</summary>
    public static readonly NumberKnob[] TrimSheet =
    [
        Speed, Range, Belt, ArmourDeck, MainArmour(0), MainRounds(0), SecPerSide(0),
    ];
}
