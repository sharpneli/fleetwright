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

    public virtual bool AppliesTo(Design d) => Applies?.Invoke(d) ?? true;
}

/// <summary>A number: a Shipgen <see cref="NumberLever"/> (reading and writing the design, its bounds, its metric step;
/// null in the design means Auto, the engine's own choice) with the label and units it is shown in. A knob the Overview
/// steps wraps the lever Shipgen defines (Levers); the others build theirs here, through the same properties.</summary>
public sealed class NumberKnob : Knob
{
    public Quantity Quantity { get; }
    public double StepImperial { get; }

    /// <summary>The edit itself.</summary>
    public NumberLever Lever { get; private set; }

    public NumberKnob(string id, string label, Quantity q, double stepMetric, double stepImperial) : base(id, label)
    {
        (Quantity, StepImperial) = (q, stepImperial);
        Lever = new NumberLever(id, stepMetric) { Integer = q == Quantity.Count };
    }

    /// <summary>A knob over a lever Shipgen defines.</summary>
    [System.Diagnostics.CodeAnalysis.SetsRequiredMembers]
    public NumberKnob(NumberLever lever, string label, Quantity q, double stepImperial) : base(lever.Id, label)
    {
        (Quantity, StepImperial, Lever) = (q, stepImperial, lever);
        if (lever.Integer != (q == Quantity.Count))
            throw new ArgumentException($"{lever.Id}: a count knob takes an integer lever and only a count knob does");
    }

    public double StepMetric => Lever.Step;

    public required Func<Design, double?> Get { get => Lever.Get; init => Lever = Lever with { Get = value }; }
    public required Func<Design, double?, Design> Set { get => Lever.Set; init => Lever = Lever with { Set = value }; }

    /// <summary>The value the engine uses when the design leaves it out (the built ship may be null).</summary>
    public Func<Design, Ship?, double?>? Auto { get => Lever.Auto; init => Lever = Lever with { Auto = value }; }

    /// <summary>Can the knob go back to Auto (null)?</summary>
    public bool CanAuto { get => Lever.CanAuto; init => Lever = Lever with { CanAuto = value }; }

    /// <summary>The style limit's JSON path (the engine's sanity bounds), when it has one.</summary>
    public string[]? LimitPath { get => Lever.LimitPath; init => Lever = Lever with { LimitPath = value }; }

    /// <summary>Bounds when the style gives none, and a floor over the style's (speed: 8 kn).</summary>
    public double Lo { get => Lever.Lo; init => Lever = Lever with { Lo = value }; }
    public double Hi { get => Lever.Hi; init => Lever = Lever with { Hi = value }; }
    public double? Floor { get => Lever.Floor; init => Lever = Lever with { Floor = value }; }

    /// <summary>Bounds from the design itself (a group can't step more turrets than it has; a carrier's tower has a
    /// higher minimum than a warship's).</summary>
    public Func<Design, double>? HiOf { get => Lever.HiOf; init => Lever = Lever with { HiOf = value }; }
    public Func<Design, double>? LoOf { get => Lever.LoOf; init => Lever = Lever with { LoOf = value }; }

    public new Func<Design, bool>? Applies { get => Lever.Applies; init => Lever = Lever with { Applies = value }; }

    public override bool AppliesTo(Design d) => Lever.AppliesTo(d);

    public bool Integer => Lever.Integer;

    /// <summary>One step in metric, for the units shown.</summary>
    public double Step(UnitSystem u) => u == UnitSystem.Metric ? StepMetric : StepImperial * Units.Factor(Quantity, u);

    /// <summary>The value shown: the design's, else Auto's.</summary>
    public double? Value(Design d, Ship? ship) => Lever.Value(d, ship);

    public (double Lo, double Hi) Range(Design d) => Lever.Range(d);

    /// <summary>The design with the value set, kept inside the range (whole numbers for counts).</summary>
    public Design Apply(Design d, double? value) => Lever.Apply(d, value);
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

    static Design MainBattery(Design d, int i, Func<BatteryInput, BatteryInput> f)
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

    // ------------------------------------------------------------------ the knobs

    public static readonly NumberKnob Speed = new(Levers.Speed, "Speed", Quantity.Speed, 0.5);

    public static readonly NumberKnob Range = new(Levers.Range, "Range", Quantity.Range, 500);

    public static readonly NumberKnob Belt = new(Levers.Belt, "Belt", Quantity.Armour, 0.5);

    public static readonly NumberKnob BeltBottom = new("belt_bottom", "Belt, lower edge", Quantity.Armour, 10, 0.5)
    {
        Get = d => d.Armour?.BeltBottomMm,
        Set = (d, v) => Arm(d, a => a with { BeltBottomMm = v }),
        Auto = (d, s) => d.Armour?.BeltMm ?? 0,
        CanAuto = true,
        LimitPath = ["armour", "belt_bottom_mm"],
    };

    public static readonly NumberKnob UpperBelt = new(Levers.UpperBelt, "Upper belt", Quantity.Armour, 0.5);

    public static readonly NumberKnob ArmourDeck = new(Levers.ArmourDeck, "Armour deck", Quantity.Armour, 0.25);

    public static readonly NumberKnob Bulkheads = new(Levers.Bulkheads, "Citadel bulkheads", Quantity.Armour, 0.5);

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

    public static NumberKnob DeckMm(int i) => new(Levers.DeckMm(i), "Thickness", Quantity.Armour, 0.25);

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
        Set = (d, v) => MainBattery(d, i, b => b with { CalibreMm = v }),
        LimitPath = ["main", "calibre_mm"],
    };

    public static NumberKnob MainLength(int i) => new($"main{i}_len", "Length, calibres", Quantity.Ratio, 1, 1)
    {
        Get = d => d.Main?.ElementAtOrDefault(i)?.CalibreLength,
        Set = (d, v) => MainBattery(d, i, b => b with { CalibreLength = v }),
        LimitPath = ["main", "calibre_length"],
    };

    public static NumberKnob MainBarrels(int i) => new($"main{i}_barrels", "Guns per turret", Quantity.Count, 1, 1)
    {
        Get = d => d.Main?.ElementAtOrDefault(i)?.Barrels,
        Set = (d, v) => MainBattery(d, i, b => b with { Barrels = I(v) }),
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
        Set = (d, v) => MainBattery(d, i, b => group switch
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
        Set = (d, v) => MainBattery(d, i, b =>
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
        Set = (d, v) => MainBattery(d, i, b => b with { Echelon = v }),
        Applies = d => (d.Main?.ElementAtOrDefault(i)?.Wing ?? 0) > 0,
    };

    public static NumberKnob MainArmour(int i) => new($"main{i}_armour", "Turret faces", Quantity.Armour, 10, 0.5)
    {
        Get = d => d.Main?.ElementAtOrDefault(i)?.ArmourMm,
        Set = (d, v) => MainBattery(d, i, b => b with { ArmourMm = v }),
        Auto = (d, s) => 0,
        LimitPath = ["main", "armour_mm"],
        Applies = d => d.Main?.Count > i,
    };

    public static NumberKnob MainRounds(int i) => new($"main{i}_rounds", "Rounds per gun", Quantity.Count, 10, 10)
    {
        Get = d => d.Main?.ElementAtOrDefault(i)?.RoundsPerGun,
        Set = (d, v) => MainBattery(d, i, b => b with { RoundsPerGun = v }),
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
    public static NumberKnob SecPerSide(int i) => new(Levers.SecPerSide(i), "Mounts per side", Quantity.Count, 1);

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

    public static readonly NumberKnob AaLight = new(Levers.AaLight, "Light AA mounts", Quantity.Count, 2);

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

    /// <summary>A design with its submerged tubes edited (a design without torpedoes gets none on deck).</summary>
    static Design Sub(Design d, Func<SubmergedInput, SubmergedInput> f)
    {
        var tp = d.Torpedoes ?? new TorpedoInput { Mounts = 0, Tubes = 4 };
        return d with { Torpedoes = tp with { Submerged = f(tp.Submerged ?? new SubmergedInput()) } };
    }

    public static readonly NumberKnob SubPerSide = new("sub_beam", "Beam tubes a side", Quantity.Count, 1, 1)
    {
        Get = d => d.Torpedoes?.Submerged?.PerSide,
        Set = (d, v) => Sub(d, s => s with { PerSide = I(v) }),
        Auto = (d, s) => 0,
        LimitPath = ["torpedoes", "submerged", "per_side"],
        Hi = 6,
    };

    public static readonly NumberKnob SubBow = new("sub_bow", "Bow tubes", Quantity.Count, 1, 1)
    {
        Get = d => d.Torpedoes?.Submerged?.Bow,
        Set = (d, v) => Sub(d, s => s with { Bow = I(v) }),
        Auto = (d, s) => 0,
        LimitPath = ["torpedoes", "submerged", "bow"],
        Hi = 2,
    };

    public static readonly NumberKnob SubStern = new("sub_stern", "Stern tubes", Quantity.Count, 1, 1)
    {
        Get = d => d.Torpedoes?.Submerged?.Stern,
        Set = (d, v) => Sub(d, s => s with { Stern = I(v) }),
        Auto = (d, s) => 0,
        LimitPath = ["torpedoes", "submerged", "stern"],
        Hi = 2,
    };

    public static readonly NumberKnob SubPerTube = new("sub_per_tube", "Torpedoes per tube", Quantity.Count, 1, 1)
    {
        Get = d => d.Torpedoes?.Submerged?.TorpedoesPerTube,
        Set = (d, v) => Sub(d, s => s with { TorpedoesPerTube = I(v) }),
        Auto = (d, s) => SubmergedTubes.PerTubeDefault,
        LimitPath = ["torpedoes", "submerged", "torpedoes_per_tube"],
        Lo = 1,
        Hi = 10,
        Applies = d => SubmergedTubes.Count(d) > 0,
    };

    // speed and machinery
    public static readonly NumberKnob Stress = new(Levers.Stress, "Design rating", Quantity.Ratio, 0.1);

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
    public static readonly NumberKnob BlockCoefficient = new(Levers.BlockCoefficient, "Hull form, full to fine", Quantity.Ratio, 0.01);

    public static readonly NumberKnob Freeboard = new(Levers.Freeboard, "Freeboard (x standard)", Quantity.Ratio, 0.05);

    /// <summary>The construction standard (hull.construction.standard): the minimum plate gauge, x a naval hull's. Light
    /// scantlings (0.85, a torpedo craft's 0.75) save weight on a small ship and leave thinner plate for splinters and
    /// shells to go through; robust ones (1.25) the other way.</summary>
    public static readonly NumberKnob Scantlings = new("scantlings", "Scantlings (x naval gauge)", Quantity.Ratio, 0.05, 0.05)
    {
        Get = d => d.Hull?.Construction?.Standard,
        Set = (d, v) => Hull(d, h => h with { Construction = (h.Construction ?? new ConstructionInput()) with { Standard = v } }),
        Auto = (d, s) => 1,
        CanAuto = true,
        Lo = 0.6,
        Hi = 1.5,
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

    public static readonly NumberKnob TowerLevels = new(Levers.TowerLevels, "Bridge tower levels", Quantity.Count, 1);

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

    /// <summary>A main battery's turret rangefinders, each turret's base across its gunhouse's rear (fire-control.md F4b);
    /// 0: none.</summary>
    public static NumberKnob MainRangefinder(int i) => new($"main{i}_rf", "Turret rangefinders", Quantity.Length, 0.5, 1)
    {
        Get = d => d.Main?.ElementAtOrDefault(i)?.RangefinderM,
        Set = (d, v) => MainBattery(d, i, b => b with { RangefinderM = v }),
        Auto = (d, s) => 0,
        Lo = 0,
        Hi = 15,
        Applies = d => d.Main?.Count > i,
    };

    /// <summary>The rangefinder in a hood on after control's top, a control position for the main battery (F4b).</summary>
    public static readonly NumberKnob AftRangefinder = new("fc_aft_rf", "Aft control rangefinder", Quantity.Length, 0.5, 1)
    {
        Get = d => d.FireControl?.AftRangefinderM,
        Set = (d, v) => d with { FireControl = (d.FireControl ?? new FireControlInput()) with { AftRangefinderM = v } },
        Auto = (d, s) => 0,
        Lo = 0,
        Hi = 15,
    };

    /// <summary>The rangefinder on the conning tower's roof (F4b).</summary>
    public static readonly NumberKnob TowerRangefinder = new("fc_ct_rf", "Conning tower rangefinder", Quantity.Length, 0.5, 1)
    {
        Get = d => d.FireControl?.ConningTowerRangefinderM,
        Set = (d, v) => d with { FireControl = (d.FireControl ?? new FireControlInput()) with { ConningTowerRangefinderM = v } },
        Auto = (d, s) => 0,
        Lo = 0,
        Hi = 15,
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
    public static readonly NumberKnob Endurance = new(Levers.Endurance, "Stores for", Quantity.Days, 5);

    public static readonly ToggleKnob Distiller = new("distiller", "Distiller")
    {
        Get = d => d.Crew?.Distiller,
        Set = (d, v) => Crew(d, c => c with { Distiller = v }),
        Default = true,
    };

    // ------------------------------------------------------------------ hull form: the topside (hull.section)

    static bool IsTumblehome(Design d) => d.Hull?.Section?.Topside == "tumblehome";

    static Design Section(Design d, Func<SectionInput, SectionInput> f) =>
        Hull(d, h => h with { Section = f(h.Section ?? new SectionInput { Topside = "tumblehome" }) });

    /// <summary>Wall-sided or tumblehome. Back to wall-sided drops the tumblehome's settings (the engine rejects them
    /// on a wall-sided hull).</summary>
    public static readonly ChoiceKnob Topside = new("topside", "Topside", ["wall_sided", "tumblehome"], ["wall-sided", "tumblehome"])
    {
        Get = d => d.Hull?.Section?.Topside,
        Set = (d, v) => Hull(d, h => h with { Section = v == "tumblehome" ? (h.Section ?? new SectionInput()) with { Topside = v } : null }),
        Default = "wall_sided",
    };

    public static readonly NumberKnob TumblehomeStrength = new("th_strength", "Tumblehome, strength", Quantity.Ratio, 0.1, 0.1)
    {
        Get = d => d.Hull?.Section?.Strength,
        Set = (d, v) => Section(d, s => s with { Strength = v }),
        Auto = (d, s) => Shipgen.Tumblehome.DefaultStrength,
        CanAuto = true,
        LimitPath = ["hull", "section", "strength"],
        Applies = IsTumblehome,
    };

    public static readonly NumberKnob TumblehomeKnuckle = new("th_knuckle", "Widest point (x freeboard)", Quantity.Ratio, 0.05, 0.05)
    {
        Get = d => d.Hull?.Section?.Knuckle,
        Set = (d, v) => Section(d, s => s with { Knuckle = v }),
        Auto = (d, s) => Shipgen.Tumblehome.DefaultKnuckle,
        CanAuto = true,
        Lo = 0,
        Hi = Shipgen.Tumblehome.MaxKnuckle,
        Applies = IsTumblehome,
    };

    public static readonly ChoiceKnob TumblehomeExtent = new("th_extent", "Tumblehome over", ["full", "midships"], ["full length", "midships (ends fade out)"])
    {
        Get = d => d.Hull?.Section?.Extent,
        Set = (d, v) => Section(d, s => s with { Extent = v }),
        Default = "full",
        Applies = IsTumblehome,
    };

    // ------------------------------------------------------------------ hull and upperworks: details

    public static readonly ChoiceKnob DeckFinish = new("deck_finish", "Deck", ["wood", "steel"], ["planked", "steel"])
    {
        Get = d => d.Deck,
        Set = (d, v) => d with { Deck = v },
    };

    public static readonly NumberKnob ShellMm = new("shell_mm", "Side shell (0: the structure's own)", Quantity.Armour, 1, 0.05)
    {
        Get = d => d.Hull?.Plating?.ShellMm,
        Set = (d, v) => Hull(d, h => h with { Plating = (h.Plating ?? new PlatingInput()) with { ShellMm = v } }),
        Auto = (d, s) => 0,
        LimitPath = ["hull", "plating", "shell_mm"],
    };

    public static readonly NumberKnob DeckWoodMm = new("deck_wood_mm", "Deck planking", Quantity.Armour, 5, 0.25)
    {
        Get = d => d.Hull?.Plating?.DeckWoodMm,
        Set = (d, v) => Hull(d, h => h with { Plating = (h.Plating ?? new PlatingInput()) with { DeckWoodMm = v } }),
        Auto = (d, s) => 0,
        CanAuto = true,
        LimitPath = ["hull", "plating", "deck_wood_mm"],
    };

    public static readonly NumberKnob LevelsOverBridge = new("levels_over_bridge", "Tower levels over the bridge", Quantity.Count, 1, 1)
    {
        Get = d => d.Superstructure?.LevelsOverBridge,
        Set = (d, v) => Sup(d, s => s with { LevelsOverBridge = I(v) }),
        CanAuto = true,
        LimitPath = ["superstructure", "levels_over_bridge"],
        Applies = d => Styles.Get(d).HasControlTowers,
    };

    public static readonly NumberKnob SuperstructurePlating = new("sup_plating", "Superstructure plating (0: own gauge)", Quantity.Armour, 1, 0.05)
    {
        Get = d => d.Superstructure?.PlatingMm,
        Set = (d, v) => Sup(d, s => s with { PlatingMm = v }),
        Auto = (d, s) => 0,
        LimitPath = ["superstructure", "plating_mm"],
    };

    public static readonly NumberKnob ControlPlating = new("control_mm", "Control positions' plating", Quantity.Armour, 5, 0.25)
    {
        Get = d => d.Superstructure?.ControlMm,
        Set = (d, v) => Sup(d, s => s with { ControlMm = v }),
        Auto = (d, s) => 0,
        LimitPath = ["superstructure", "control_mm"],
    };

    // ------------------------------------------------------------------ protection: details

    public static readonly NumberKnob BeltDepth = new("belt_depth", "Belt, below the waterline", Quantity.Length, 0.1, 0.5)
    {
        Get = d => d.Armour?.BeltDepthM,
        Set = (d, v) => Arm(d, a => a with { BeltDepthM = v }),
        CanAuto = true,
        LimitPath = ["armour", "belt_depth_m"],
        Applies = d => (d.Armour?.BeltMm ?? 0) > 0,
    };

    public static readonly NumberKnob BeltHeight = new("belt_height", "Belt, above the waterline", Quantity.Length, 0.1, 0.5)
    {
        Get = d => d.Armour?.BeltHeightM,
        Set = (d, v) => Arm(d, a => a with { BeltHeightM = v }),
        CanAuto = true,
        LimitPath = ["armour", "belt_height_m"],
        Applies = d => (d.Armour?.BeltMm ?? 0) > 0,
    };

    public static readonly NumberKnob UpperBeltToDeck = new("upper_belt_to", "Upper belt up to deck", Quantity.Count, 1, 1)
    {
        Get = d => d.Armour?.UpperBelt?.ToDeck,
        Set = (d, v) => Arm(d, a => a with { UpperBelt = (a.UpperBelt ?? new UpperBeltInput()) with { ToDeck = I(v) } }),
        Auto = (d, s) => 0,
        Lo = -2,
        Hi = 0,
        Applies = d => (d.Armour?.UpperBelt?.Mm ?? 0) > 0,
    };

    public static readonly ChoiceKnob UpperBeltExtent = new("upper_belt_extent", "Upper belt over", DeckExtents,
        ["citadel", "full length", "fore end", "aft end", "both ends"])
    {
        Get = d => d.Armour?.UpperBelt?.Extent,
        Set = (d, v) => Arm(d, a => a with { UpperBelt = (a.UpperBelt ?? new UpperBeltInput()) with { Extent = v } }),
        Default = "citadel",
        Applies = d => (d.Armour?.UpperBelt?.Mm ?? 0) > 0,
    };

    public static NumberKnob EndBeltTip(string end) => new($"end_belt_{end}_tip", $"End belt {end}, at the tip", Quantity.Armour, 10, 0.5)
    {
        Get = d => d.Armour?.EndBelts?.Of(end)?.TipMm,
        Set = (d, v) => EndBelt(d, end, b => b with { TipMm = v }),
        Auto = (d, s) => d.Armour?.EndBelts?.Of(end)?.Mm,
        CanAuto = true,
        LimitPath = ["armour", "end_belts", end, "tip_mm"],
        Applies = d => (d.Armour?.EndBelts?.Of(end)?.Mm ?? 0) > 0,
    };

    public static NumberKnob EndBeltBulkhead(string end) => new($"end_belt_{end}_bh", $"End belt {end}, closing bulkhead", Quantity.Armour, 10, 0.5)
    {
        Get = d => d.Armour?.EndBelts?.Of(end)?.BulkheadMm,
        Set = (d, v) => EndBelt(d, end, b => b with { BulkheadMm = v }),
        Auto = (d, s) => 0,
        LimitPath = ["armour", "end_belts", end, "bulkhead_mm"],
        Applies = d => (d.Armour?.EndBelts?.Of(end)?.Mm ?? 0) > 0,
    };

    static Design Steering(Design d, Func<SteeringBoxInput, SteeringBoxInput> f) =>
        Arm(d, a => a with { SteeringBox = f(a.SteeringBox ?? new SteeringBoxInput { Mm = 0, DeckMm = 0, BulkheadMm = 0 }) });

    public static readonly NumberKnob SteeringSides = new("steer_mm", "Steering gear box, sides", Quantity.Armour, 10, 0.5)
    {
        Get = d => d.Armour?.SteeringBox?.Mm,
        Set = (d, v) => Steering(d, s => s with { Mm = v }),
        Auto = (d, s) => 0,
        LimitPath = ["armour", "steering_box", "mm"],
    };

    public static readonly NumberKnob SteeringRoof = new("steer_deck", "Steering gear box, roof", Quantity.Armour, 10, 0.5)
    {
        Get = d => d.Armour?.SteeringBox?.DeckMm,
        Set = (d, v) => Steering(d, s => s with { DeckMm = v }),
        Auto = (d, s) => 0,
        LimitPath = ["armour", "steering_box", "deck_mm"],
    };

    public static readonly NumberKnob SteeringEnds = new("steer_bh", "Steering gear box, ends", Quantity.Armour, 10, 0.5)
    {
        Get = d => d.Armour?.SteeringBox?.BulkheadMm,
        Set = (d, v) => Steering(d, s => s with { BulkheadMm = v }),
        Auto = (d, s) => 0,
        LimitPath = ["armour", "steering_box", "bulkhead_mm"],
    };

    public static readonly NumberKnob FlightDeckArmour = new("flight_deck_mm", "Flight deck", Quantity.Armour, 5, 0.25)
    {
        Get = d => d.Armour?.FlightDeckMm,
        Set = (d, v) => Arm(d, a => a with { FlightDeckMm = v }),
        Auto = (d, s) => 0,
        Lo = 0,
        Hi = 2000,
        Applies = d => d.StyleName == "carrier",
    };

    // ------------------------------------------------------------------ protection: the layers (protection-plan.md H2-H6,
    // docs/designer-ui/protection-layers.md)

    public static readonly NumberKnob BeltMagazines = new("belt_magazines", "Belt at the magazines", Quantity.Armour, 10, 0.5)
    {
        Get = d => d.Armour?.BeltMagazinesMm,
        Set = (d, v) => Arm(d, a => a with { BeltMagazinesMm = v }),
        LimitPath = ["armour", "belt_magazines_mm"],
        Applies = d => (d.Armour?.BeltMm ?? 0) > 0,
    };

    public static readonly NumberKnob BeltIncline = new("belt_incline", "Belt's lean", Quantity.Angle, 1, 1)
    {
        Get = d => d.Armour?.BeltInclineDeg,
        Set = (d, v) => Arm(d, a => a with { BeltInclineDeg = v is > 0 ? v : null }),
        Auto = (d, s) => 0,
        LimitPath = ["armour", "belt_incline_deg"],
        Applies = d => (d.Armour?.BeltMm ?? 0) > 0,
    };

    public static readonly ChoiceKnob BeltPosition = new("belt_position", "Belt stands", ["external", "internal"], ["on the side", "inside the shell"])
    {
        Get = d => d.Armour?.BeltPosition,
        Set = (d, v) => Arm(d, a => a with { BeltPosition = v == "internal" ? v : null }),
        Default = "external",
        Applies = d => (d.Armour?.BeltMm ?? 0) > 0,
    };

    public static readonly ChoiceKnob BeltExtent = new("belt_extent", "Belt runs over", ["citadel", "machinery"], ["the citadel", "the machinery"])
    {
        Get = d => d.Armour?.BeltExtent,
        Set = (d, v) => Arm(d, a => a with { BeltExtent = v == "machinery" ? v : null }),
        Default = "citadel",
        Applies = d => (d.Armour?.BeltMm ?? 0) > 0,
    };

    public static NumberKnob DeckSlopeMm(int i) => new($"deck{i}_slope_mm", "Slopes", Quantity.Armour, 5, 0.25)
    {
        Get = d => d.Armour?.Decks?.ElementAtOrDefault(i)?.SlopeMm,
        Set = (d, v) => Arm(d, a => a with { Decks = Replace(a.Decks!, i, x => x with { SlopeMm = v }) }),
        LimitPath = ["armour", "decks", "slope_mm"],
    };

    public static NumberKnob DeckSlopeDeg(int i) => new($"deck{i}_slope_deg", "Slopes' angle", Quantity.Angle, 1, 1)
    {
        Get = d => d.Armour?.Decks?.ElementAtOrDefault(i)?.SlopeDeg,
        Set = (d, v) => Arm(d, a => a with { Decks = Replace(a.Decks!, i, x => x with { SlopeDeg = v }) }),
        Auto = (d, s) => Armour.SlopeDegDefault,
        CanAuto = true,
        LimitPath = ["armour", "decks", "slope_deg"],
    };

    public static NumberKnob DeckMagazinesMm(int i) => new($"deck{i}_magazines_mm", "At the magazines", Quantity.Armour, 5, 0.25)
    {
        Get = d => d.Armour?.Decks?.ElementAtOrDefault(i)?.MagazinesMm,
        Set = (d, v) => Arm(d, a => a with { Decks = Replace(a.Decks!, i, x => x with { MagazinesMm = v }) }),
        Lo = 0,
        Hi = 2000,
    };

    public static readonly NumberKnob TdsLayers = new("tds_layers", "Torpedo protection layers", Quantity.Count, 1, 1)
    {
        Get = d => d.Armour?.TdsLayers,
        Set = (d, v) => Arm(d, a => a with { TdsLayers = I(v) is > 1 ? I(v) : null }),
        Auto = (d, s) => 1,
        LimitPath = ["armour", "tds_layers"],
        Applies = d => (d.Armour?.TdsM ?? 0) > 0,
    };

    public static readonly NumberKnob TdsHolding = new("tds_holding", "Holding bulkhead", Quantity.Armour, 5, 0.25)
    {
        Get = d => d.Armour?.TdsHoldingMm,
        Set = (d, v) => Arm(d, a => a with { TdsHoldingMm = v }),
        Auto = (d, s) => Armour.Tds(d).HoldingMm,
        CanAuto = true,
        LimitPath = ["armour", "tds_holding_mm"],
        Applies = d => (d.Armour?.TdsM ?? 0) > 0,
    };

    public static readonly NumberKnob TdsLayerWall = new("tds_layer_mm", "Layer walls", Quantity.Armour, 1, 0.05)
    {
        Get = d => d.Armour?.TdsLayerMm,
        Set = (d, v) => Arm(d, a => a with { TdsLayerMm = v }),
        Auto = (d, s) => Armour.Tds(d).LayerMm,
        CanAuto = true,
        LimitPath = ["armour", "tds_layer_mm"],
        Applies = d => (d.Armour?.TdsM ?? 0) > 0 && (d.Armour?.TdsLayers ?? 1) > 1,
    };

    static Design Inner(Design d, Func<InnerBulkheadInput, InnerBulkheadInput> f) =>
        Arm(d, a => a with { InnerBulkhead = f(a.InnerBulkhead ?? new InnerBulkheadInput { Mm = 0 }) });

    public static readonly NumberKnob InnerMm = new("inner_mm", "Inner bulkhead", Quantity.Armour, 5, 0.25)
    {
        Get = d => d.Armour?.InnerBulkhead?.Mm,
        Set = (d, v) => Inner(d, x => x with { Mm = v }),
        Auto = (d, s) => 0,
        LimitPath = ["armour", "inner_bulkhead", "mm"],
    };

    public static readonly NumberKnob InnerMagazines = new("inner_mags", "Inner bulkhead at the magazines", Quantity.Armour, 5, 0.25)
    {
        Get = d => d.Armour?.InnerBulkhead?.MagazinesMm,
        Set = (d, v) => Inner(d, x => x with { MagazinesMm = v }),
        Auto = (d, s) => d.Armour?.InnerBulkhead?.Mm ?? 0,
        CanAuto = true,
        LimitPath = ["armour", "inner_bulkhead", "magazines_mm"],
    };

    public static readonly NumberKnob InnerInboard = new("inner_inboard", "Inner bulkhead in from the side", Quantity.Length, 0.1, 0.5)
    {
        Get = d => d.Armour?.InnerBulkhead?.InboardM,
        Set = (d, v) => Inner(d, x => x with { InboardM = v }),
        Auto = (d, s) => s != null ? Armour.MagazineWingM(d, s.Hitboxes.Beam) : null,
        CanAuto = true,
        LimitPath = ["armour", "inner_bulkhead", "inboard_m"],
    };

    static Design Box(Design d, Func<MagazineBoxInput, MagazineBoxInput> f) => Arm(d, a => a with { MagazineBox = f(a.MagazineBox ?? new MagazineBoxInput()) });

    public static readonly NumberKnob BoxSide = new("box_side", "Magazine boxes, sides", Quantity.Armour, 5, 0.25)
    {
        Get = d => d.Armour?.MagazineBox?.SideMm,
        Set = (d, v) => Box(d, x => x with { SideMm = v }),
        Auto = (d, s) => 0,
        LimitPath = ["armour", "magazine_box", "side_mm"],
    };

    public static readonly NumberKnob BoxCrown = new("box_crown", "Magazine boxes, crown", Quantity.Armour, 5, 0.25)
    {
        Get = d => d.Armour?.MagazineBox?.CrownMm,
        Set = (d, v) => Box(d, x => x with { CrownMm = v }),
        Auto = (d, s) => 0,
        LimitPath = ["armour", "magazine_box", "crown_mm"],
    };

    public static readonly NumberKnob BoxEnd = new("box_end", "Magazine boxes, ends", Quantity.Armour, 5, 0.25)
    {
        Get = d => d.Armour?.MagazineBox?.EndMm,
        Set = (d, v) => Box(d, x => x with { EndMm = v }),
        Auto = (d, s) => 0,
        LimitPath = ["armour", "magazine_box", "end_mm"],
    };

    public static readonly NumberKnob BoxFloor = new("box_floor", "Magazine boxes, floor", Quantity.Armour, 5, 0.25)
    {
        Get = d => d.Armour?.MagazineBox?.FloorMm,
        Set = (d, v) => Box(d, x => x with { FloorMm = v }),
        Auto = (d, s) => 0,
        LimitPath = ["armour", "magazine_box", "floor_mm"],
    };

    public static readonly NumberKnob MagazineWing = new("magazine_wing", "Magazine wings", Quantity.Length, 0.1, 0.5)
    {
        Get = d => d.Armour?.MagazineWingM,
        Set = (d, v) => Arm(d, a => a with { MagazineWingM = v }),
        Auto = (d, s) => s != null ? Armour.MagazineWingShare * s.Hitboxes.Beam : null,
        CanAuto = true,
        LimitPath = ["armour", "magazine_wing_m"],
    };

    // ------------------------------------------------------------------ batteries: details

    public static ToggleKnob MainCrossDeck(int i) => new($"main{i}_cross", "Cross-deck fire (echelon)")
    {
        Get = d => d.Main?.ElementAtOrDefault(i)?.CrossDeck,
        Set = (d, v) => MainBattery(d, i, b => b with { CrossDeck = v }),
        Applies = d => d.Main?.ElementAtOrDefault(i) is { Wing: > 0, Echelon: true },
    };

    public static ChoiceKnob MainStandsOn(int i) => new($"main{i}_stands", "Wing and midships turrets on", ["deck", "deckhouse"], ["the deck", "the deckhouse"])
    {
        Get = d => d.Main?.ElementAtOrDefault(i)?.AmidshipsStandsOn,
        Set = (d, v) => MainBattery(d, i, b => b with { AmidshipsStandsOn = v }),
        Default = "deck",
        Applies = d => Styles.Get(d).HasRaisedMounts && d.Main?.ElementAtOrDefault(i) is { } b && (b.Wing ?? 0) + (b.Mid ?? 0) > 0,
    };

    public static NumberKnob SecRounds(int i) => new($"sec{i}_rounds", "Rounds per gun", Quantity.Count, 10, 10)
    {
        Get = d => d.Secondary?.ElementAtOrDefault(i)?.RoundsPerGun,
        Set = (d, v) => Sec(d, i, b => b with { RoundsPerGun = v }),
        Auto = (d, s) => d.Secondary?.ElementAtOrDefault(i)?.CalibreMm is { } c ? Math.Round(Batteries.RoundsPerGun(c)) : null,
        CanAuto = true,
        LimitPath = ["secondary", "rounds_per_gun"],
    };

    public static ChoiceKnob SecTier(int i) => new($"sec{i}_tier", "Casemate tier", ["lower", "upper"], ["lower (in the hull)", "upper (on the main deck)"])
    {
        Get = d => d.Secondary?.ElementAtOrDefault(i)?.Tier,
        Set = (d, v) => Sec(d, i, b => b with { Tier = v }),
        Default = "lower",
        Applies = d => d.Secondary?.ElementAtOrDefault(i)?.Mount == "casemate",
    };

    public static ChoiceKnob SecStandsOn(int i) => new($"sec{i}_stands", "Stands on", ["deck", "deckhouse"], ["the deck", "the deckhouse"])
    {
        Get = d => d.Secondary?.ElementAtOrDefault(i)?.StandsOn,
        Set = (d, v) => Sec(d, i, b => b with { StandsOn = v }),
        Default = "deck",
        Applies = d => Styles.Get(d).HasRaisedMounts && d.Secondary?.ElementAtOrDefault(i) is { } b && (b.Mount ?? "deck") == "deck",
    };

    /// <summary>Where the battery stands, one of four (the Secondary tab's position buttons): writes mount, tier and
    /// stands_on together.</summary>
    public static ChoiceKnob SecPosition(int i) => new($"sec{i}_pos", "Position", SecondaryArrangement.Ids,
        ["on the deckhouse", "on the deck", "upper casemates", "lower casemates"])
    {
        Get = d => d.Secondary?.ElementAtOrDefault(i) is { } b ? SecondaryArrangement.Ids[(int)SecondaryArrangement.Of(b)] : null,
        Set = (d, v) => SecondaryArrangement.SetPos(d, i, (SecondaryArrangement.Pos)Math.Max(0, Array.IndexOf(SecondaryArrangement.Ids, v))),
        Default = "deck",
        Applies = d => d.Secondary?.Count > i,
    };

    // ------------------------------------------------------------------ machinery: details

    public static readonly NumberKnob Funnels = new("funnels", "Funnels (at least)", Quantity.Count, 1, 1)
    {
        Get = d => d.Funnels,
        Set = (d, v) => d with { Funnels = v },
        Auto = (d, s) => s?.Report.Plant.Funnels,
        CanAuto = true,
        LimitPath = ["funnels"],
    };

    // ------------------------------------------------------------------ masts, joints, doors (docs/designer-ui/tech-blocks.md T3)

    static readonly string[] MastNames = ["Foremast", "Mainmast"];

    /// <summary>A mast's input, the list grown to hold it (a mast left empty takes the defaults).</summary>
    static Design Mast(Design d, int i, Func<MastInput, MastInput> f)
    {
        var l = (d.Masts ?? []).ToList();
        while (l.Count <= i)
            l.Add(new MastInput());
        l[i] = f(l[i]);
        return d with { Masts = l };
    }

    static MastInput? MastOf(Design d, int i) => d.Masts?.ElementAtOrDefault(i);

    /// <summary>A mast's kind (masts[0] the foremast, [1] the mainmast); auto: a pole, the foremast a tripod from 150 m.</summary>
    public static ChoiceKnob MastKind(int i) => new($"mast{i}_kind", $"{MastNames[i]}", MastPlan.Kinds, MastPlan.Kinds)
    {
        Get = d => MastOf(d, i)?.Kind,
        Set = (d, v) => Mast(d, i, m => m with { Kind = v }),
        Default = "auto",
    };

    public static ChoiceKnob MastTop(int i) => new($"mast{i}_top", $"{MastNames[i]} top", MastPlan.TopKinds,
        ["none", "fighting top", "spotting top", "crow's nest"])
    {
        Get = d => MastOf(d, i)?.Top,
        Set = (d, v) => Mast(d, i, m => m with { Top = v }),
        Default = "auto",
        Applies = d => MastOf(d, i)?.Kind != "none",
    };

    /// <summary>The battery one of whose directors stands in the mast's top (fire-control.md F4b); none: "none".</summary>
    public static ChoiceKnob MastDirector(int i) => new($"mast{i}_director", $"{MastNames[i]} top's director", ["none", "main", "secondary", "aa"],
        ["none", "a main director", "a secondary director", "an AA director"])
    {
        Get = d => MastOf(d, i)?.Director,
        Set = (d, v) => Mast(d, i, m => m with { Director = v == "none" ? null : v }),
        Default = "none",
        Applies = d => MastOf(d, i)?.Kind != "none",
    };

    /// <summary>The masthead over the main deck; auto: the top at 2.25 times the highest gun's height, a topmast over it.</summary>
    public static NumberKnob MastHeight(int i) => new($"mast{i}_height", $"{MastNames[i]} height", Quantity.Length, 1, 1)
    {
        Get = d => MastOf(d, i)?.HeightM,
        Set = (d, v) => Mast(d, i, m => m with { HeightM = v }),
        Auto = (d, s) => s?.Hitboxes.Components.Where(c => c.Kind == ComponentKind.Mast).OrderByDescending(c => c.X ?? 0)
            .ElementAtOrDefault(i)?.Top,
        CanAuto = true,
        Lo = 10,
        Hi = 80,
        Applies = d => MastOf(d, i)?.Kind != "none",
    };

    public static NumberKnob MastTiers(int i) => new($"mast{i}_tiers", $"{MastNames[i]} top's tiers", Quantity.Count, 1, 1)
    {
        Get = d => MastOf(d, i)?.Tiers,
        Set = (d, v) => Mast(d, i, m => m with { Tiers = I(v) }),
        Auto = (d, s) => 1,
        Lo = 1,
        Hi = 5,
        Applies = d => MastOf(d, i) is { } m && m.Kind != "none" && m.Top is not (null or "none"),
    };

    static Design Joints(Design d, Func<ConstructionInput, ConstructionInput> f) =>
        Hull(d, h => h with { Construction = f(h.Construction!) });

    /// <summary>How the shell plating is joined (Fittings.Joint), the construction's own; a template sets it.</summary>
    public static readonly ChoiceKnob ShellJoint = new("joint_shell", "Shell plating joined", ["riveted", "welded"], ["riveted", "welded"])
    {
        Get = d => d.Hull?.Construction?.Shell,
        Set = (d, v) => Joints(d, c => c with { Shell = v }),
        Default = "welded",
        Applies = d => d.Hull?.Construction != null,
    };

    public static readonly ChoiceKnob InternalsJoint = new("joint_internals", "Internals joined", ["riveted", "welded"], ["riveted", "welded"])
    {
        Get = d => d.Hull?.Construction?.Internals,
        Set = (d, v) => Joints(d, c => c with { Internals = v }),
        Default = "welded",
        Applies = d => d.Hull?.Construction != null,
    };

    /// <summary>Doors in the main bulkheads below the damage-control deck (null: the style's, merchants yes).</summary>
    public static readonly ToggleKnob DoorsBelow = new("doors_below", "Doors in the bulkheads below")
    {
        Get = d => d.Subdivision?.DoorsBelow,
        Set = (d, v) => d with { Subdivision = (d.Subdivision ?? new SubdivisionInput()) with { DoorsBelow = v } },
    };

    public static readonly ChoiceKnob Transmission = new("transmission", "Transmission", ["mechanical", "electric"], ["mechanical", "electric (turbo-/diesel-electric)"])
    {
        Get = d => d.Machinery?.Transmission,
        Set = (d, v) => Mach(d, m => m with { Transmission = v }),
        Default = "mechanical",
    };

    public static readonly NumberKnob UnitsPerShaft = new("units_per_shaft", "Units per shaft", Quantity.Count, 1, 1)
    {
        Get = d => d.Machinery?.UnitsPerShaft,
        Set = (d, v) => Mach(d, m => m with { UnitsPerShaft = I(v) }),
        Auto = (d, s) => s?.Report.Plant is { Shafts: > 0 } p ? p.Units / p.Shafts : null,
        CanAuto = true,
        Lo = 1,
        Hi = 6,
    };

    public static readonly ToggleKnob CentrelineBulkhead = new("centreline_bh", "Centreline bulkhead in the machinery")
    {
        Get = d => d.Machinery?.CentrelineBulkhead,
        Set = (d, v) => Mach(d, m => m with { CentrelineBulkhead = v }),
    };

    public static readonly ChoiceKnob Bunkers = new("bunkers", "Coal bunkers", ["wing", "ends"], ["beside the machinery", "at its ends"])
    {
        Get = d => d.Machinery?.Bunkers,
        Set = (d, v) => Mach(d, m => m with { Bunkers = v }),
        Default = "wing",
    };

    public static readonly NumberKnob WingBunker = new("wing_bunker_m", "Wing bunker width", Quantity.Length, 0.1, 0.5)
    {
        Get = d => d.Machinery?.WingBunkerM,
        Set = (d, v) => Mach(d, m => m with { WingBunkerM = v }),
        Auto = (d, s) => 2,
        CanAuto = true,
        Lo = 0.5,
        Hi = 6,
        Applies = d => (d.Machinery?.Bunkers ?? "wing") == "wing",
    };

    public static readonly NumberKnob Rudders = new("rudders", "Rudders", Quantity.Count, 1, 1)
    {
        Get = d => d.Machinery?.Rudders,
        Set = (d, v) => Mach(d, m => m with { Rudders = I(v) }),
        Auto = (d, s) => 1,
        CanAuto = true,
        Lo = 1,
        Hi = 8,
    };

    // ------------------------------------------------------------------ fire control and crew: details

    public static NumberKnob DirectorArmour(string battery) => new($"fc_{battery}_armour", "Director armour", Quantity.Armour, 5, 0.25)
    {
        Get = d => d.FireControl?.Of(battery)?.ArmourMm,
        Set = (d, v) => Fc(d, battery, x => x with { ArmourMm = v }),
        Auto = (d, s) => 0,
        LimitPath = ["fire_control", battery, "armour_mm"],
        Applies = d => (d.FireControl?.Of(battery)?.Directors ?? 0) > 0,
    };

    public static NumberKnob DirectorRadar(string battery) => new($"fc_{battery}_radar", "Fire-control radar, t", Quantity.Ratio, 0.5, 0.5)
    {
        Get = d => d.FireControl?.Of(battery)?.RadarT,
        Set = (d, v) => Fc(d, battery, x => x with { RadarT = v }),
        Auto = (d, s) => 0,
        LimitPath = ["fire_control", battery, "radar_t"],
        Applies = d => (d.FireControl?.Of(battery)?.Directors ?? 0) > 0,
    };

    public static readonly NumberKnob SearchRadar = new("search_radar", "Search radar, t", Quantity.Ratio, 0.5, 0.5)
    {
        Get = d => d.FireControl?.SearchRadarT,
        Set = (d, v) => d with { FireControl = (d.FireControl ?? new FireControlInput()) with { SearchRadarT = v } },
        Auto = (d, s) => 0,
        LimitPath = ["fire_control", "search_radar_t"],
    };

    public static readonly NumberKnob BufferDays = new("buffer_days", "Stores beyond the fuel, days", Quantity.Days, 1, 1)
    {
        Get = d => d.Crew?.BufferDays,
        Set = (d, v) => Crew(d, c => c with { BufferDays = v }),
        Auto = (d, s) => 5,
        CanAuto = true,
        Lo = 0,
        Hi = 90,
    };

    public static readonly NumberKnob WaterRation = new("water_l", "Water per man, l/day", Quantity.Ratio, 5, 5)
    {
        Get = d => d.Crew?.WaterLPerDay,
        Set = (d, v) => Crew(d, c => c with { WaterLPerDay = v }),
        Auto = (d, s) => d.Crew?.Standard?.WaterLPerDay,
        CanAuto = true,
        Lo = 1,
        Hi = 300,
    };

    public static readonly NumberKnob BerthRatio = new("berth_ratio", "Berths per man", Quantity.Ratio, 0.05, 0.05)
    {
        Get = d => d.Crew?.BerthRatio,
        Set = (d, v) => Crew(d, c => c with { BerthRatio = v }),
        Auto = (d, s) => 1,
        CanAuto = true,
        Lo = 0.3,
        Hi = 1.5,
    };

    public static readonly NumberKnob OfficerFraction = new("officer_frac", "Officers, share of the crew", Quantity.Ratio, 0.01, 0.01)
    {
        Get = d => d.Crew?.OfficerFraction,
        Set = (d, v) => Crew(d, c => c with { OfficerFraction = v }),
        CanAuto = true,
        Lo = 0.01,
        Hi = 0.5,
    };

    // ------------------------------------------------------------------ materials: names for the game's ballistics

    /// <summary>A material name from the catalogue; "" is none of its own (a part takes the armour scheme's).</summary>
    static ChoiceKnob Material(string id, string label, string group, string unset, Func<Design, string?> get,
        Func<Design, string?, Design> set, Func<Design, bool>? applies = null)
    {
        var names = Templates.MaterialNames(group).ToArray();
        return new ChoiceKnob(id, label, ["", .. names], [unset, .. names])
        {
            Get = get,
            Set = (d, v) => set(d, v is null or "" ? null : v),
            Default = "",
            Applies = applies,
        };
    }

    const string FromScheme = "from the armour scheme";

    public static ChoiceKnob HullMaterial => Material("hull_material", "Hull plating material", "structure", "not named",
        d => d.Hull?.Plating?.Material, (d, v) => Hull(d, h => h with { Plating = (h.Plating ?? new PlatingInput()) with { Material = v } }));

    public static ChoiceKnob SuperstructureMaterial => Material("sup_material", "Superstructure material", "structure", "not named",
        d => d.Superstructure?.Material, (d, v) => Sup(d, s => s with { Material = v }));

    public static ChoiceKnob UpperBeltMaterial => Material("upper_belt_material", "Upper belt", "armour", FromScheme,
        d => d.Armour?.UpperBelt?.Material,
        (d, v) => Arm(d, a => a with { UpperBelt = (a.UpperBelt ?? new UpperBeltInput()) with { Material = v } }),
        d => (d.Armour?.UpperBelt?.Mm ?? 0) > 0);

    public static ChoiceKnob EndBeltMaterial(string end) => Material($"end_belt_{end}_material", $"End belt {end}", "armour", FromScheme,
        d => d.Armour?.EndBelts?.Of(end)?.Material, (d, v) => EndBelt(d, end, b => b with { Material = v }),
        d => (d.Armour?.EndBelts?.Of(end)?.Mm ?? 0) > 0);

    public static ChoiceKnob DeckMaterial(int i) => Material($"deck{i}_material", "Material", "armour", FromScheme,
        d => d.Armour?.Decks?.ElementAtOrDefault(i)?.Material,
        (d, v) => Arm(d, a => a with { Decks = Replace(a.Decks!, i, x => x with { Material = v }) }));

    public static ChoiceKnob SteeringMaterial => Material("steer_material", "Steering gear box, sides", "armour", FromScheme,
        d => d.Armour?.SteeringBox?.Material, (d, v) => Steering(d, s => s with { Material = v }),
        d => (d.Armour?.SteeringBox?.Mm ?? 0) > 0);

    public static ChoiceKnob SteeringRoofMaterial => Material("steer_deck_material", "Steering gear box, roof", "armour", FromScheme,
        d => d.Armour?.SteeringBox?.DeckMaterial, (d, v) => Steering(d, s => s with { DeckMaterial = v }),
        d => (d.Armour?.SteeringBox?.DeckMm ?? 0) > 0);

    /// <summary>A part's material in the armour scheme's own map (belt, bulkheads, conning tower: the parts with no
    /// material field of their own). Changing one makes the scheme custom.</summary>
    public static ChoiceKnob SchemeMaterial(string part, string label) => Material($"scheme_{part}_material", label, "armour", "the engine's default",
        d => d.Armour?.Materials?.GetValueOrDefault(part),
        (d, v) => Arm(d, a =>
        {
            var m = new Dictionary<string, string>(a.Materials ?? []);
            if (v == null)
                m.Remove(part);
            else
                m[part] = v;
            return a with { Materials = m };
        }));

    public static ChoiceKnob MainMaterial(int i) => Material($"main{i}_material", "Turret and barbette material", "armour", FromScheme,
        d => d.Main?.ElementAtOrDefault(i)?.Material, (d, v) => MainBattery(d, i, b => b with { Material = v }));

    public static ChoiceKnob SecMaterial(int i) => Material($"sec{i}_material", "Mount material", "armour", FromScheme,
        d => d.Secondary?.ElementAtOrDefault(i)?.Material, (d, v) => Sec(d, i, b => b with { Material = v }));

    /// <summary>Every knob the design has, per-battery and per-deck ones included.</summary>
    public static IEnumerable<Knob> All(Design d)
    {
        Knob[] fixedKnobs =
        [
            Speed, Range, Belt, BeltBottom, UpperBelt, ArmourDeck, Bulkheads, Tds, AaHeavy, AaLight, TorpedoMounts, TorpedoTubes,
            SubPerSide, SubBow, SubStern, SubPerTube,
            Stress, Shafts, Arrangement, BlockCoefficient, Freeboard, Scantlings, Raised, TowerLevels, DeckhouseLevels, AftControl,
            Endurance, Distiller,
            Topside, TumblehomeStrength, TumblehomeKnuckle, TumblehomeExtent, DeckFinish, ShellMm, DeckWoodMm, LevelsOverBridge,
            SuperstructurePlating, ControlPlating, BeltDepth, BeltHeight, UpperBeltToDeck, UpperBeltExtent, SteeringSides,
            SteeringRoof, SteeringEnds, FlightDeckArmour, Funnels, Transmission, UnitsPerShaft, CentrelineBulkhead, Bunkers,
            WingBunker, Rudders, SearchRadar, AftRangefinder, TowerRangefinder, BufferDays, WaterRation, BerthRatio, OfficerFraction,
            ShellJoint, InternalsJoint, DoorsBelow,
            HullMaterial, SuperstructureMaterial, UpperBeltMaterial, SteeringMaterial, SteeringRoofMaterial,
            MainFaces, MainCalibres, MainRoundsAll, EndBelts,
            BeltMagazines, BeltIncline, BeltPosition, BeltExtent, TdsLayers, TdsHolding, TdsLayerWall, InnerMm, InnerMagazines, InnerInboard,
            BoxSide, BoxCrown, BoxEnd, BoxFloor, MagazineWing,
        ];
        foreach (var k in fixedKnobs)
            yield return k;
        for (int i = 0; i < 2; i++)
        {
            yield return MastKind(i);
            yield return MastTop(i);
            yield return MastDirector(i);
            yield return MastHeight(i);
            yield return MastTiers(i);
        }
        foreach (var end in new[] { "fore", "aft" })
        {
            yield return EndBeltMm(end);
            yield return EndBeltReach(end);
            yield return EndBeltTip(end);
            yield return EndBeltBulkhead(end);
            yield return EndBeltMaterial(end);
        }
        for (int i = 0; i < (d.Armour?.Decks?.Count ?? 0); i++)
        {
            yield return DeckMm(i);
            yield return DeckLevel(i);
            yield return DeckExtent(i);
            yield return DeckMaterial(i);
            yield return DeckSlopeMm(i);
            yield return DeckSlopeDeg(i);
            yield return DeckMagazinesMm(i);
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
            yield return MainCrossDeck(i);
            yield return MainStandsOn(i);
            yield return MainMaterial(i);
            yield return MainArmour(i);
            yield return MainRounds(i);
            yield return MainRangefinder(i);
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
            yield return SecRounds(i);
            yield return SecTier(i);
            yield return SecStandsOn(i);
            yield return SecMaterial(i);
            yield return SecPosition(i);
        }
        foreach (var b in new[] { "main", "secondary", "aa" })
        {
            yield return Directors(b);
            yield return Rangefinder(b);
            yield return DirectorArmour(b);
            yield return DirectorRadar(b);
        }
    }

    // ------------------------------------------------------------------ the overview's broad strokes

    public static readonly NumberKnob MainFaces = new(Levers.MainFaces, "Turret faces", Quantity.Armour, 0.5);

    public static readonly NumberKnob MainCalibres = new(Levers.MainCalibres, "Main calibre", Quantity.Calibre, 0.5);

    public static readonly NumberKnob MainRoundsAll = new(Levers.MainRoundsAll, "Main rounds per gun", Quantity.Count, 10);

    /// <summary>Both end belts at once: shows their mean, and a new value moves each (and its tip, when set) by the same
    /// amount. Only for a design that has end belts: adding them is the Protection tab's.</summary>
    public static readonly NumberKnob EndBelts = new(Levers.EndBelts, "End belts, both", Quantity.Armour, 0.5);

    /// <summary>Every armour thickness the design gives, times f (Levers.ScaleArmour).</summary>
    public static Design ScaleArmour(Design d, double f) => Levers.ScaleArmour(d, f);
}
