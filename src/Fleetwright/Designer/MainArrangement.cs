using Fleetwright.Shipgen;

namespace Fleetwright.Designer;

/// <summary>
/// The main batteries' turrets as places (docs/designer-ui/mainbattery.md): the end groups, outermost first, the
/// midships turrets and the wing pairs, and the edits the main battery tab makes on them. Everything maps onto the
/// design's own fields (fore, aft, mid, wing, superfire, echelon); the end order and the superfiring run are the
/// engine's (<see cref="Layout.EndOrder"/>): at each end the batteries' turrets come in list order, and the turrets that
/// step up are one run from the outermost.
/// </summary>
public static class MainArrangement
{
    public enum Group { Fore, Aft, Mid, Wing }

    /// <summary>A turret: its battery (index into main), its group, its place there (an end's position outermost
    /// first; the n-th midships turret; the n-th wing pair), its name, its tier (0: on the deck), whether it stands on
    /// the deck behind another (no arc over it), and for wings the side (-1 port, 1 starboard).</summary>
    public readonly record struct Slot(int Battery, Group Group, int Index, string Name, int Tier, bool Blocked, int Side);

    public static bool IsEnd(Group g) => g is Group.Fore or Group.Aft;

    static IReadOnlyList<BatteryInput> Mains(Design d) => d.Main ?? [];

    public static int Count(BatteryInput b, Group g) => (g switch
    {
        Group.Fore => b.Fore,
        Group.Aft => b.Aft,
        Group.Mid => b.Mid,
        _ => b.Wing,
    }) ?? 0;

    static BatteryInput WithCount(BatteryInput b, Group g, int n) => g switch
    {
        Group.Fore => b with { Fore = n },
        Group.Aft => b with { Aft = n },
        Group.Mid => b with { Mid = n },
        _ => b with { Wing = n },
    };

    static Design With(Design d, int k, Func<BatteryInput, BatteryInput> f)
    {
        var l = Mains(d).ToList();
        l[k] = f(l[k]);
        return d with { Main = l };
    }

    /// <summary>An end's order (each turret's battery) and how many of it step up.</summary>
    public static (List<int> Order, int Run) End(Design d, Group end) => Layout.EndOrder(Mains(d), end == Group.Fore ? "fore" : "aft");

    /// <summary>A battery's superfire count for an end as the engine reads it (null: all of them).</summary>
    static int? Stepped(BatteryInput b, Group end) => b.Superfire switch
    {
        null or { All: true } => null,
        { All: false } => 0,
        var sf => end == Group.Fore ? sf.Fore : sf.Aft,
    };

    static BatteryInput WithStepped(BatteryInput b, Group end, int? k)
    {
        int? f = end == Group.Fore ? k : Stepped(b, Group.Fore), a = end == Group.Aft ? k : Stepped(b, Group.Aft);
        return b with { Superfire = f == null && a == null ? null : new Superfire(null, f, a) };
    }

    /// <summary>The design with <paramref name="run"/> of an end's turrets stepping up: each battery keeps how many of
    /// its own fall inside the run (null when all do, so turrets added later follow the default and step up too).</summary>
    public static Design SetRun(Design d, Group end, int run)
    {
        var l = Mains(d).ToList();
        int o = 0;
        for (int b = 0; b < l.Count; b++)
        {
            int n = Count(l[b], end);
            int k = Math.Clamp(run - o, 0, n);
            l[b] = WithStepped(l[b], end, k == n ? null : k);
            o += n;
        }
        return d with { Main = l };
    }

    /// <summary>Where battery <paramref name="k"/>'s next turret goes at an end: after its own (battery order sets who
    /// is outboard).</summary>
    public static int InsertAt(Design d, int k, Group end)
    {
        var l = Mains(d);
        int o = 0;
        for (int j = 0; j <= k && j < l.Count; j++)
            o += Count(l[j], end);
        return o;
    }

    /// <summary>Whether a turret added at an end would step up: it does when everything outboard of it does, unless
    /// asked for on the deck; the outermost always counts.</summary>
    public static bool AddsStepped(Design d, int k, Group end, bool flush)
    {
        int pos = InsertAt(d, k, end);
        return pos > 0 && !flush && End(d, end).Run >= pos;
    }

    /// <summary>One more turret of battery <paramref name="k"/> in a group; at an end it steps up as
    /// <see cref="AddsStepped"/> says, and everything outboard of it stays as it was.</summary>
    public static Design Add(Design d, int k, Group g, bool flush = false)
    {
        if (!IsEnd(g))
            return With(d, k, b => WithCount(b, g, Count(b, g) + 1));
        int run = End(d, g).Run, pos = InsertAt(d, k, g);
        var next = With(d, k, b => WithCount(b, g, Count(b, g) + 1));
        return SetRun(next, g, pos == 0 || (!flush && run >= pos) ? run + 1 : Math.Min(run, pos));
    }

    /// <summary>The design without a turret (a wing turret takes its pair with it).</summary>
    public static Design Remove(Design d, Slot s)
    {
        if (!IsEnd(s.Group))
            return With(d, s.Battery, b => WithCount(b, s.Group, Math.Max(0, Count(b, s.Group) - 1)));
        int run = End(d, s.Group).Run;
        var next = With(d, s.Battery, b => WithCount(b, s.Group, Math.Max(0, Count(b, s.Group) - 1)));
        return SetRun(next, s.Group, s.Index < run ? run - 1 : run);
    }

    /// <summary>Toggles whether an end's <paramref name="j"/>-th turret steps up over the one outboard of it. The run
    /// stays one run: stepping a turret up steps up everything outboard of it, putting one on the deck brings down
    /// everything inboard of it.</summary>
    public static Design ToggleStep(Design d, Group end, int j)
    {
        int run = End(d, end).Run;
        return SetRun(d, end, run > j ? j : j + 1);
    }

    /// <summary>The turret moved to another battery, in the same group.</summary>
    public static Design Reassign(Design d, Slot s, int to) => Add(Remove(d, s), to, s.Group);

    /// <summary>The turret moved to another group of its battery (to an end: inboard of its battery's turrets there).</summary>
    public static Design MoveTo(Design d, Slot s, Group g) => Add(Remove(d, s), s.Battery, g);

    /// <summary>The turret as a battery of its own: the same gun, appended to the list (so innermost at the ends).</summary>
    public static Design SplitOff(Design d, Slot s)
    {
        var next = Remove(d, s);
        var b = Mains(next)[s.Battery];
        next = next with { Main = [.. Mains(next), Unplaced(b)] };
        return Add(next, Mains(next).Count - 1, s.Group);
    }

    /// <summary>The battery's gun with no turrets.</summary>
    public static BatteryInput Unplaced(BatteryInput b) => b with { Fore = 0, Aft = 0, Mid = 0, Wing = 0, Superfire = null };

    /// <summary>A copy of the battery with no turrets, right after it.</summary>
    public static Design Duplicate(Design d, int k)
    {
        var l = Mains(d).ToList();
        l.Insert(k + 1, Unplaced(l[k]));
        return d with { Main = l };
    }

    /// <summary>The battery moved one place up (<paramref name="dir"/> -1: outboard at the ends) or down the list;
    /// each end keeps as many turrets stepping up as before.</summary>
    public static Design MoveBattery(Design d, int k, int dir)
    {
        int j = k + dir;
        var l = Mains(d).ToList();
        if (j < 0 || j >= l.Count)
            return d;
        int runF = End(d, Group.Fore).Run, runA = End(d, Group.Aft).Run;
        (l[k], l[j]) = (l[j], l[k]);
        return SetRun(SetRun(d with { Main = l }, Group.Fore, runF), Group.Aft, runA);
    }

    /// <summary>A preset's places for battery <paramref name="k"/>, its gun kept; the other batteries keep theirs.
    /// Stepped: how many of its turrets at each end step up (null: all).</summary>
    public readonly record struct Preset(string Name, string Short, int Fore, int Aft, int Mid, int Wing, bool Echelon = false,
        bool CrossDeck = false, int? SteppedFore = null, int? SteppedAft = null, bool NeedsMid = false, bool NeedsWing = false);

    public static readonly Preset[] Presets =
    [
        new("1 · 1", "A · Y", 1, 1, 0, 0),
        new("2 · 2 superfiring", "A B↑ · X↑ Y", 2, 2, 0, 0),
        new("2 · Q · 2", "A B↑ · Q · X↑ Y", 2, 2, 1, 0, NeedsMid: true),
        new("3 forward", "A B↑ C", 3, 0, 0, 0, SteppedFore: 2),
        new("2 · 1", "A B↑ · Y", 2, 1, 0, 0),
        new("Dreadnought", "A · W1 · X Y", 1, 2, 0, 1, SteppedAft: 1, NeedsWing: true),
        new("Hexagonal", "A · W1–2 · Y", 1, 1, 0, 2, NeedsWing: true),
        new("Echelon", "A · W1 ech. · Y", 1, 1, 0, 1, Echelon: true, CrossDeck: true, NeedsWing: true),
    ];

    public static Design Apply(Design d, int k, Preset p) => With(d, k, b => b with
    {
        Fore = p.Fore,
        Aft = p.Aft,
        Mid = p.Mid,
        Wing = p.Wing,
        Echelon = p.Wing > 0 ? p.Echelon : b.Echelon,
        CrossDeck = p.Wing > 0 ? p.CrossDeck : b.CrossDeck,
        Superfire = p.SteppedFore == null && p.SteppedAft == null ? null : new Superfire(null, p.SteppedFore, p.SteppedAft),
    });

    /// <summary>Every turret: the fore end (A B C, outermost first), the aft end (Y X W), the midships turrets
    /// (Q P R S) and the wing pairs (W1P, W1S, ...), in battery order within the waist.</summary>
    public static List<Slot> Slots(Design d)
    {
        var slots = new List<Slot>();
        foreach (var g in new[] { Group.Fore, Group.Aft })
        {
            var (order, run) = End(d, g);
            for (int j = 0; j < order.Count; j++)
                slots.Add(new Slot(order[j], g, j, Layout.TurretName(g == Group.Fore ? "ABC" : "YXW", j), j < run ? j : 0, j > 0 && j >= run, 0));
        }
        var l = Mains(d);
        int m = 0, w = 0;
        for (int b = 0; b < l.Count; b++)
            for (int i = 0; i < Count(l[b], Group.Mid); i++, m++)
                slots.Add(new Slot(b, Group.Mid, m, Layout.TurretName("QPRS", m), 0, false, 0));
        for (int b = 0; b < l.Count; b++)
            for (int i = 0; i < Count(l[b], Group.Wing); i++, w++)
            {
                slots.Add(new Slot(b, Group.Wing, w, $"W{w + 1}P", 0, false, -1));
                slots.Add(new Slot(b, Group.Wing, w, $"W{w + 1}S", 0, false, 1));
            }
        return slots;
    }

    /// <summary>A battery's places in one line, read bow to stern in the plan's names: "A B↑ · W1–2 abreast · X↑ Y"
    /// (↑: superfiring).</summary>
    public static string Positions(Design d, int k, List<Slot> slots)
    {
        var b = Mains(d)[k];
        var parts = new List<string>();
        string Names(IEnumerable<Slot> ss) => string.Join(" ", ss.Select(s => s.Tier > 0 ? s.Name + "↑" : s.Name));
        var fore = slots.Where(s => s.Battery == k && s.Group == Group.Fore).ToList();
        if (fore.Count > 0)
            parts.Add(Names(fore));
        var mid = slots.Where(s => s.Battery == k && s.Group == Group.Mid).ToList();
        if (mid.Count > 0)
            parts.Add(Names(mid));
        var pairs = slots.Where(s => s.Battery == k && s.Group == Group.Wing && s.Side < 0).Select(s => s.Index + 1).ToList();
        if (pairs.Count > 0)
            parts.Add($"W{pairs[0]}{(pairs.Count > 1 ? "–" + pairs[^1] : "")} " +
                      (b.Echelon == true ? b.CrossDeck == true ? "ech. × deck" : "echelon" : "abreast"));
        var aft = slots.Where(s => s.Battery == k && s.Group == Group.Aft).Reverse().ToList();
        if (aft.Count > 0)
            parts.Add(Names(aft));
        return parts.Count > 0 ? string.Join(" · ", parts) : "no turrets";
    }
}
