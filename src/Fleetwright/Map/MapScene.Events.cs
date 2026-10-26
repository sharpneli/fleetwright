using System.Globalization;
using System.Numerics;
using Fleetwright.Designer;
using Fleetwright.Sim;
using Fleetwright.Sim.Ai;
using Fleetwright.Sim.Ai.Captains;
using Fleetwright.Sim.Combat;
using Fleetwright.Sim.Core;
using ImGuiNET;
using Fleetwright.Ui;

namespace Fleetwright.Map;

/// <summary>The battle's events (docs/map-ui/battle-hud.md U6): the combat log's lines the player may know (our hits
/// and straddles, the hits we took, torpedoes, losses, damage control) and what the screens see change between
/// snapshots (a ship falling out, a fire starting, a contact first seen or made out), as one stream. The ticker shows
/// the newest at the bottom left, fading; J opens the whole log, filtered; a line's click goes to its ship. A ship
/// falling out says so there; the strip offers to order her back into line (the player overrules her captain:
/// decision 2). An event's words are made once, when it's first shown.</summary>
public sealed partial class MapScene
{
    enum EventKind : byte { Gun, Hit, Fire, Loss, Captain, Seen, Damage, Order }

    /// <summary>An event: when, what kind, about which ship; a log line's (its place in the feed and the sentence it's
    /// told in) or the screens' own (its words given); when it came, real time (the ticker fades it).</summary>
    readonly record struct MapEvent(long Ms, EventKind Kind, int Ship, int Line, Form Form, string? Text, long RealMs);

    readonly List<MapEvent> events = new(1024);
    int feedRead, eventsGeneration = -1;
    readonly HashSet<(int, int)> straddling = [];
    readonly Dictionary<int, byte> reasonOf = [];
    readonly Dictionary<int, sbyte> tracksOf = [];
    readonly Dictionary<int, byte> spentOf = [];
    readonly Dictionary<int, int> firesOf = [], groupOf = [];
    readonly Dictionary<int, ContactLevel> levelOf = [];
    bool logOpen;
    int logFilter;

    static readonly string[] FilterNames = ["All", "Ours", "Gunnery", "Damage", "Orders", "Seen"];
    static readonly string[] KindTags = ["GUN", "HIT", "FIRE", "LOSS", "CAPT", "SEEN", "DC", "ORDER"];
    static readonly Vector4[] KindColours =
    [
        new(0.9f, 0.71f, 0.42f, 1), new(0.88f, 0.42f, 0.35f, 1), new(1f, 0.6f, 0.25f, 1), new(1f, 0.35f, 0.29f, 1),
        new(0.36f, 0.61f, 0.88f, 1), new(0.77f, 0.61f, 0.88f, 1), new(0.44f, 0.66f, 0.86f, 1), new(0.9f, 0.77f, 0.33f, 1),
    ];

    /// <summary>Once a frame, after the feed has read the snapshot: its new lines and the snapshot's changes as events.</summary>
    void CollectEvents(Snapshot s)
    {
        if (world.Generation != eventsGeneration)
        {
            eventsGeneration = world.Generation;
            events.Clear();
            straddling.Clear();
            reasonOf.Clear();
            tracksOf.Clear();
            spentOf.Clear();
            firesOf.Clear();
            groupOf.Clear();
            levelOf.Clear();
            eventTexts.Clear();
            feedRead = 0;
            tally = default;
        }
        var lines = feed.Lines;
        if (feedRead > lines.Count)
            feedRead = 0;
        for (; feedRead < lines.Count; feedRead++)
            FromLog(s, feedRead);
        var v = s.Ships;
        for (int k = 0; k < v.Count; k++)
        {
            int e = v.Entity[k];
            if (s.PlayerSide >= 0 && v.Side[k] != s.PlayerSide)
                continue;
            // falling out: an event; her squadron remembered, for "back into line" (the strip's)
            byte reason = v.Reason[k], was = reasonOf.GetValueOrDefault(e);
            if (reason != was)
            {
                reasonOf[e] = reason;
                if (reason is (byte)CaptainReason.OutSpeed or (byte)CaptainReason.OutGuns or (byte)CaptainReason.OutList
                    or (byte)CaptainReason.OutWater or (byte)CaptainReason.OutSteering)
                {
                    string name = ShipName(s, e), why = ReasonText(reason);
                    Add(s, EventKind.Captain, e, $"{name} {why}");
                }
                else if (reason == (byte)CaptainReason.OutAmmunition)
                    Add(s, EventKind.Captain, e, $"{ShipName(s, e)}: out of ammunition, going home");
            }
            // the player's shell order spent (docs/battle/shell-choice.md A5): the choice her captain's again
            byte spent = v.ShellSpent[k];
            if (spent != spentOf.GetValueOrDefault(e))
            {
                spentOf[e] = spent;
                if (spent > 0)
                    Add(s, EventKind.Captain, e, $"{ShipName(s, e)}: out of {KindName((Sim.Ships.ShellKind)(spent - 1))} for the main guns, her captain chooses again");
            }
            // the conn's lookouts (docs/battle/command.md "The conn"): tracks coming at her, once a sighting
            sbyte tracks = v.Tracks[k];
            if (tracks != tracksOf.GetValueOrDefault(e))
            {
                tracksOf[e] = tracks;
                if (tracks != 0)
                    Add(s, EventKind.Captain, e, tracks > 0 ? $"{ShipName(s, e)}: torpedo tracks to starboard!" : $"{ShipName(s, e)}: torpedo tracks to port!");
            }
            if (s.Groups.IndexOfShip(e) is int g and >= 0)
                groupOf[e] = s.Groups.Id[g];
            // a fire starting
            int ci = s.Conditions.IndexOf(e), fires = ci >= 0 ? s.Conditions.Fires[ci] : 0;
            if (fires > 0 && firesOf.GetValueOrDefault(e) == 0)
                Add(s, EventKind.Fire, e, $"{ShipName(s, e)} on fire");
            firesOf[e] = fires;
        }
        // contacts: first seen, made out
        var c = s.Contacts;
        int from = v.IndexOf(lastOwn);
        for (int i = 0; i < c.Count; i++)
        {
            int e = c.Entity[i];
            if (!levelOf.TryGetValue(e, out var was))
            {
                levelOf[e] = c.Level[i];
                if (from >= 0)
                {
                    var p = v.PoseAt(from, world.DrawMs).Position;
                    Add(s, EventKind.Seen, e, string.Format(CultureInfo.InvariantCulture, "Contact: {0}, {1:000}°, {2:0.0} km from {3}",
                        Identity(c, i), (Earth.Bearing(p, c.Position[i]) % 360 + 360) % 360, Earth.Distance(p, c.Position[i]) / 1000, ShipName(s, lastOwn)));
                }
                else
                    Add(s, EventKind.Seen, e, $"Contact: {Identity(c, i)}");
            }
            else if (c.Level[i] > was)
            {
                levelOf[e] = c.Level[i];
                if (c.Level[i] == ContactLevel.Name)
                    Add(s, EventKind.Seen, e, $"{Identity(c, i)} identified");
                else if (c.Level[i] == ContactLevel.Class)
                    Add(s, EventKind.Seen, e, $"Made out: {Identity(c, i)}");
                else if (c.Level[i] == ContactLevel.Type)
                    Add(s, EventKind.Seen, e, $"Made out: a {Identity(c, i)}");
            }
        }
    }

    /// <summary>A log line as an event, when it's one the player would want: our hits, our first straddle on each target,
    /// the hits we took, torpedoes, losses, damage control's news.</summary>
    /// <summary>A log line as an event, when it's one the player would want: our hits, our first straddle on each target,
    /// the hits we took, torpedoes, losses, damage control's news. Its text waits until it's shown (<see cref="TextOf"/>):
    /// a big fight writes many lines a frame, and few of them are ever read.</summary>
    void FromLog(Snapshot s, int line)
    {
        var x = feed.Lines[line];
        bool shooterOurs = Ours(s, x.Shooter), targetOurs = Ours(s, x.Target);
        switch (x.Kind)
        {
            case CombatEvent.Hit when targetOurs:
                tally.Taken++;
                AddLine(EventKind.Hit, x.Target, line, Form.HitTaken, x.Ms);
                break;
            case CombatEvent.Hit when shooterOurs:
                tally.Scored++;
                AddLine(EventKind.Gun, x.Target, line, Form.HitScored, x.Ms);
                break;
            case CombatEvent.Straddle when shooterOurs && straddling.Add((x.Shooter, x.Target)):
                AddLine(EventKind.Gun, x.Target, line, Form.Straddle, x.Ms);
                break;
            case CombatEvent.Torpedoes when shooterOurs:
                AddLine(EventKind.Gun, x.Target, line, Form.TorpedoesOut, x.Ms);
                break;
            case CombatEvent.Torpedoes when targetOurs:
                AddLine(EventKind.Hit, x.Target, line, Form.TorpedoesIn, x.Ms);
                break;
            case CombatEvent.Lost:
                if (targetOurs)
                    tally.Lost++;
                else
                    tally.Sunk++;
                AddLine(EventKind.Loss, x.Target, line, targetOurs ? Form.LostOurs : Form.LostTheirs, x.Ms);
                break;
            case CombatEvent.DamageControl when targetOurs && x.Count <= (int)DcEvent.SafeSpeed:
                AddLine(EventKind.Damage, x.Target, line, Form.DamageControl, x.Ms);
                break;
        }
    }

    /// <summary>Which of the log's sentences an event is told in.</summary>
    enum Form : byte { Given, HitTaken, HitScored, Straddle, TorpedoesOut, TorpedoesIn, LostOurs, LostTheirs, DamageControl }

    /// <summary>A log line's event in words (made once, when first shown).</summary>
    string Describe(Snapshot s, in MapEvent ev)
    {
        var x = feed.Lines[ev.Line];
        switch (ev.Form)
        {
            case Form.HitTaken:
                var cls = s.Ships.IndexOf(x.Target) is int k and >= 0 ? world.Classes.TryGet(s.Ships.Class[k]) : null;
                return $"{ShipName(s, x.Target)} hit{(x.Source == HitSource.Torpedo ? " by a torpedo" : "")}: {(cls != null ? HitText(x, cls) : "")}";
            case Form.HitScored:
                return $"{ShipName(s, x.Shooter)} hit {TargetLabel(s, x.Target)}{((x.Effect & HitEffect.Fire) != 0 ? ": a fire" : "")}{((x.Effect & HitEffect.Magazine) != 0 ? ": HER MAGAZINE" : "")}";
            case Form.Straddle:
                return $"{ShipName(s, x.Shooter)} straddling {TargetLabel(s, x.Target)}";
            case Form.TorpedoesOut:
                return $"{ShipName(s, x.Shooter)} fired {x.Count} torpedoes at {TargetLabel(s, x.Target)}";
            case Form.TorpedoesIn:
                return $"Torpedoes fired at {ShipName(s, x.Target)}";
            case Form.LostOurs or Form.LostTheirs:
                string how = (LossKind)x.Count switch { LossKind.Magazine => "her magazine exploded", LossKind.Capsized => "capsized", LossKind.BrokeInTwo => "broke in two", _ => "foundered" };
                return ev.Form == Form.LostOurs ? $"{ShipName(s, x.Target)} lost: {how}" : $"Seen sunk: {TargetLabel(s, x.Target)}, {how}";
            case Form.DamageControl:
                var dc = s.Ships.IndexOf(x.Target) is int kd and >= 0 ? world.Classes.TryGet(s.Ships.Class[kd]) : null;
                string what = (DcEvent)x.Count switch
                {
                    DcEvent.Gave => $"{x.Battery} bulkhead{(x.Battery == 1 ? "" : "s")} gave way",
                    DcEvent.Steering => "steering rigged again",
                    DcEvent.Repaired => dc != null && x.Part >= 0 && x.Part < dc.Components.Length ? $"{dc.ComponentId(x.Part)} repaired" : "a part repaired",
                    DcEvent.Restored => "a flooded room back in action",
                    DcEvent.CounterfloodOut => "the counterflood tanks are full",
                    _ => $"damage control asks for {x.Battery} kn",
                };
                return $"{ShipName(s, x.Target)}: {what}";
        }
        return "";
    }

    // each event's time and words, made when first shown
    readonly List<(string Time, string Text)?> eventTexts = [];

    (string Time, string Text) TextOf(Snapshot s, int i)
    {
        while (eventTexts.Count < events.Count)
            eventTexts.Add(null);
        if (eventTexts[i] is { } done)
            return done;
        var ev = events[i];
        var made = (Clock(ev.Ms), ev.Text ?? Describe(s, ev));
        eventTexts[i] = made;
        return made;
    }

    bool Ours(Snapshot s, int entity) => entity > 0 && s.Ships.IndexOf(entity) is int k and >= 0 && (s.PlayerSide < 0 || s.Ships.Side[k] == s.PlayerSide);

    void Add(Snapshot s, EventKind kind, int ship, string text, long ms = -1) =>
        events.Add(new MapEvent(ms >= 0 ? ms : s.Time.Ms, kind, ship, -1, Form.Given, text, Environment.TickCount64));

    void AddLine(EventKind kind, int ship, int line, Form form, long ms) =>
        events.Add(new MapEvent(ms, kind, ship, line, form, null, Environment.TickCount64));

    /// <summary>The fight's count for the top bar: hits scored and taken, ships lost and seen sunk.</summary>
    (int Scored, int Taken, int Lost, int Sunk) tally;

    static string Clock(long ms) => new WorldTime(ms).ToUtc().ToString("HH:mm", CultureInfo.InvariantCulture);

    /// <summary>An event of the player's own giving (the strip's "back into line"), for the log.</summary>
    void Ordered(Snapshot s, int ship, string text) => Add(s, EventKind.Order, ship, text);

    // ------------------------------------------------------------------ the ticker and the log

    /// <summary>The newest events at the bottom left, above the strip: each fades from 14 s old; a click goes to its ship.</summary>
    void Ticker(Snapshot s)
    {
        var vp = ImGui.GetMainViewport();
        float sc = UiFonts.Scale, pad = 8 * sc, w = 400 * sc;
        if (events.Count == 0 || logOpen)
            return;
        ImGui.SetNextWindowPos(new Vector2(vp.Pos.X + pad, vp.Pos.Y + vp.Size.Y - 104 * sc), ImGuiCond.Always, new Vector2(0, 1));
        ImGui.SetNextWindowSize(new Vector2(w, 0));
        ImGui.SetNextWindowBgAlpha(0);
        ImGui.PushStyleVar(ImGuiStyleVar.WindowPadding, Vector2.Zero);
        ImGui.PushStyleVar(ImGuiStyleVar.ItemSpacing, new Vector2(4, 2) * sc);
        if (ImGui.Begin("##ticker", ImGuiWindowFlags.NoDecoration | ImGuiWindowFlags.AlwaysAutoResize | ImGuiWindowFlags.NoSavedSettings |
                                    ImGuiWindowFlags.NoFocusOnAppearing | ImGuiWindowFlags.NoNav | ImGuiWindowFlags.NoMove | ImGuiWindowFlags.NoBackground))
        {
            long now = Environment.TickCount64;
            int first = Math.Max(0, events.Count - 5);
            for (int i = first; i < events.Count; i++)
            {
                var ev = events[i];
                float age = (now - ev.RealMs) / 1000f, alpha = Math.Clamp(1 - (age - 14) / 20, 0.45f, 1);
                EventLine(s, ev, i, alpha, true);
            }
            ImGui.PushFont(UiFonts.SansSmall);
            U8.Text(U8.W($"J: the log · {events.Count} lines"), Theme.Muted with { W = 0.8f });
            if (ImGui.IsItemClicked())
                logOpen = true;
            ImGui.PopFont();
        }
        ImGui.End();
        ImGui.PopStyleVar(2);
    }

    /// <summary>One event's line: its time, its kind's tag, its text; a click selects its ship and centres on it.</summary>
    void EventLine(Snapshot s, in MapEvent ev, int id, float alpha, bool shaded)
    {
        float sc = UiFonts.Scale;
        ImGui.PushID(id);
        var p = ImGui.GetCursorScreenPos();
        float w = ImGui.GetContentRegionAvail().X, tw = 40 * sc, kw = 44 * sc;
        ImGui.PushTextWrapPos(ImGui.GetCursorPosX() + w);
        float wrap = w - tw - kw - 8 * sc;
        var (time, text) = TextOf(s, id);
        var size = ImGui.CalcTextSize(text, false, wrap);
        float h = Math.Max(size.Y, ImGui.GetTextLineHeight()) + 3 * sc;
        bool click = ImGui.InvisibleButton("##ev", new Vector2(w, h));
        var dl = ImGui.GetWindowDrawList();
        if (shaded || ImGui.IsItemHovered())
            dl.AddRectFilled(p, p + new Vector2(w, h), ImGui.GetColorU32(new Vector4(0.04f, 0.055f, 0.067f, ImGui.IsItemHovered() ? 0.9f : 0.66f * alpha)), 2 * sc);
        ImGui.PushFont(UiFonts.Mono);
        dl.AddText(p + new Vector2(6 * sc, 1.5f * sc), ImGui.GetColorU32(Theme.Muted with { W = alpha }), time);
        ImGui.PopFont();
        ImGui.PushFont(UiFonts.SansSmall);
        dl.AddText(p + new Vector2(6 * sc + tw, 2.5f * sc), ImGui.GetColorU32(KindColours[(int)ev.Kind] with { W = alpha }), KindTags[(int)ev.Kind]);
        ImGui.PopFont();
        dl.AddText(ImGui.GetFont(), ImGui.GetFontSize(), p + new Vector2(6 * sc + tw + kw, 1.5f * sc), ImGui.GetColorU32(Theme.Text with { W = alpha }),
            text, wrap);
        ImGui.PopTextWrapPos();
        if (click)
            GoTo(s, ev.Ship);
        ImGui.PopID();
    }

    /// <summary>To an event's ship: selected (hers or an enemy as held), the map centred on her.</summary>
    void GoTo(Snapshot s, int ship)
    {
        if (s.Ships.IndexOf(ship) is int k and >= 0)
            CentreOn(s.Ships.PoseAt(k, world.DrawMs).Position);
        else if (ContactIndex(s.Contacts, ship) is int i and >= 0)
            CentreOn(s.Contacts.Position[i]);
        else
            return;
        selected = ship;
        ClearPicks();
        squadronCardOf = -1;
        cardOpen = true;
    }

    /// <summary>The whole log (J), at the left over the rail: newest first, filtered by kind or to the player's ships.</summary>
    void LogDrawer(Snapshot s)
    {
        if (!logOpen)
            return;
        var vp = ImGui.GetMainViewport();
        float sc = UiFonts.Scale, pad = 8 * sc, w = 460 * sc;
        ImGui.SetNextWindowPos(new Vector2(vp.Pos.X + pad, HudTop));
        ImGui.SetNextWindowSize(new Vector2(w, vp.Pos.Y + vp.Size.Y - HudTop - 104 * sc));
        ImGui.SetNextWindowBgAlpha(0.95f);
        if (ImGui.Begin("##log", ImGuiWindowFlags.NoDecoration | ImGuiWindowFlags.NoSavedSettings | ImGuiWindowFlags.NoMove | ImGuiWindowFlags.NoNav))
        {
            ImGui.TextColored(Theme.Brass, "LOG");
            ImGui.SameLine();
            ImGui.TextDisabled("J");
            for (int f = 0; f < FilterNames.Length; f++)
            {
                ImGui.SameLine(0, f == 0 ? 16 * sc : 1);
                if (Seg(FilterNames[f], logFilter == f, first: true))
                    logFilter = f;
            }
            ImGui.SameLine(ImGui.GetWindowWidth() - 30 * sc);
            if (Seg("×##logx", first: true))
                logOpen = false;
            ImGui.Separator();
            if (ImGui.BeginChild("##loglines"))
            {
                int shown = 0;
                for (int i = events.Count - 1; i >= 0 && shown < 400; i--)
                {
                    var ev = events[i];
                    if (!Keep(s, ev))
                        continue;
                    EventLine(s, ev, i, 1, false);
                    shown++;
                }
                if (shown == 0)
                    ImGui.TextDisabled("nothing yet");
            }
            ImGui.EndChild();
        }
        ImGui.End();

        bool Keep(Snapshot s, in MapEvent ev) => logFilter switch
        {
            1 => Ours(s, ev.Ship),
            2 => ev.Kind == EventKind.Gun,
            3 => ev.Kind is EventKind.Hit or EventKind.Fire or EventKind.Loss or EventKind.Damage,
            4 => ev.Kind is EventKind.Captain or EventKind.Order,
            5 => ev.Kind == EventKind.Seen,
            _ => true,
        };
    }

    /// <summary>Whether a ship of the player's has fallen out and her squadron is still there to take her back.</summary>
    bool CanComeBack(Snapshot s, int k) =>
        s.Ships.Reason[k] is (byte)CaptainReason.OutSpeed or (byte)CaptainReason.OutGuns or (byte)CaptainReason.OutList
            or (byte)CaptainReason.OutWater or (byte)CaptainReason.OutSteering
        && groupOf.TryGetValue(s.Ships.Entity[k], out int g) && s.Groups.IndexOfGroup(g) >= 0;

    /// <summary>The player overrules a captain who fell out (decision 2): an order of the player's first (he stays, at his
    /// ship's risk), then her squadron takes her back.</summary>
    void BackIntoLine(Snapshot s, int ship)
    {
        if (!groupOf.TryGetValue(ship, out int g))
            return;
        world.Send(world.Orders.Fight(Unit.Ship(ship)));
        world.Send(GroupSystem.Add(g, ship));
        Ordered(s, ship, $"{ShipName(s, ship)}: ordered back into line");
    }
}
