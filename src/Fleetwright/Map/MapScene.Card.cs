using System.Numerics;
using Fleetwright.Designer;
using Fleetwright.Shipgen;
using Fleetwright.Sim;
using Fleetwright.Sim.Ai.Captains;
using Fleetwright.Sim.Combat;
using Fleetwright.Sim.Core;
using Fleetwright.Sim.Ships;
using ImGuiNET;
using Fleetwright.Ui;
using static Fleetwright.Ui.Pieces;
using T = Fleetwright.Ui.Theme;

namespace Fleetwright.Map;

/// <summary>The ship card (docs/map-ui/battle-hud.md U3b, redrawn in docs/map-ui/card.md): docked at the right, opened by
/// the click that selects a ship, following the selection; pinned cards (P) stay as tabs above it. Drawn with the UI
/// language's pieces: her name as a resource bar with her state's chips, four figures (speed, course, target, hits), then
/// title strips (a click folds one): her condition (the damage plate, <c>MapScene.Plate.cs</c>, and the readings beside
/// the clinometer), her guns (a row a main mount, a row a secondary battery with a pip a mount), fire control (a battery's
/// range, plot and phases), damage control (a row a party at work), the hits she took, her torpedoes. Its text is
/// written without garbage (<see cref="U8"/>); what changes only with a hit is made then.</summary>
public sealed partial class MapScene
{
    readonly CombatFeed feed = new();
    readonly List<int> pinned = [];
    const int MaxPins = Snapshot.MaxDetail - 1;
    bool cardOpen = true;
    /// <summary>Where the dock starts this frame (screen x).</summary>
    float dockLeft = float.MaxValue;

    static readonly Vector4 Ok = Theme.Ok, Fire = Theme.Fire;

    // the card's body as it's laid out: its draw list, left and width, and where the next piece goes; cardScaleW is the
    // width with the scrollbar always taken off, for anything whose height follows its width (else the scrollbar
    // comes and goes a frame each: taller without it, so it shows, shorter with it, so it hides)
    ImDrawListPtr cdl;
    float cardX, cardW, cardY, cardScaleW;
    /// <summary>The title strips folded shut (by title, for every card).</summary>
    readonly HashSet<string> shutStrips = [];

    /// <summary>The ships the cards show, asked of the sim for their detail (once a frame; a new array only on a change).</summary>
    void CardRequests(Snapshot s)
    {
        for (int i = pinned.Count - 1; i >= 0; i--)
            if (s.Ships.IndexOf(pinned[i]) < 0)
                pinned.RemoveAt(i);   // gone
        Span<int> want = stackalloc int[Snapshot.MaxDetail];
        int n = 0;
        if (selected >= 0)
            want[n++] = selected;
        foreach (int p in pinned)
            if (p != selected && n < want.Length)
                want[n++] = p;
        world.Detail(want[..n]);
    }

    void TogglePin(int entity)
    {
        if (entity < 0)
            return;
        if (!pinned.Remove(entity))
        {
            if (pinned.Count >= MaxPins)
                pinned.RemoveAt(0);
            pinned.Add(entity);
        }
    }

    /// <summary>The dock: the live card (the selection's, else the first pinned) with the pinned ones as tabs.</summary>
    void Card(Snapshot s)
    {
        var vp = ImGui.GetMainViewport();
        dockLeft = vp.Pos.X + vp.Size.X;
        bool enemy = selected >= 0 && s.Ships.IndexOf(selected) < 0 && ContactIndex(s.Contacts, selected) >= 0;
        int live = selected >= 0 && (s.Ships.IndexOf(selected) >= 0 || enemy) ? selected : pinned.Count > 0 ? pinned[0] : -1;
        if (live < 0 || !cardOpen && !pinned.Contains(live))
            return;
        if (!cardOpen && selected == live)
            (live, enemy) = (pinned.Count > 0 ? pinned[0] : -1, false);
        if (live < 0)
            return;
        int k = s.Ships.IndexOf(live);
        var cls = k >= 0 ? world.Classes.TryGet(s.Ships.Class[k]) : null;
        if (cls == null && !enemy)
            return;
        int unit = squadronCardOf == live && s.Groups.IndexOfShip(live) is int g0 and >= 0 ? UnitOfCard(s, live) : -1;
        float pad = P(8), w = P(440), tabsH = P(22);
        float top = HudTop, bottom = vp.Pos.Y + vp.Size.Y - StatusH - P(2);
        dockLeft = vp.Pos.X + vp.Size.X - pad - w;
        ImGui.SetNextWindowPos(new Vector2(vp.Pos.X + vp.Size.X - pad, top), ImGuiCond.Always, new Vector2(1, 0));
        ImGui.SetNextWindowSize(new Vector2(w, bottom - top));
        ImGui.PushStyleColor(ImGuiCol.WindowBg, T.Panel);
        ImGui.PushStyleColor(ImGuiCol.ChildBg, Vector4.Zero);
        ImGui.PushStyleVar(ImGuiStyleVar.WindowPadding, Vector2.Zero);
        ImGui.PushStyleVar(ImGuiStyleVar.WindowBorderSize, 0);
        bool open = ImGui.Begin("##card", ImGuiWindowFlags.NoDecoration | ImGuiWindowFlags.NoMove | ImGuiWindowFlags.NoSavedSettings |
                                          ImGuiWindowFlags.NoFocusOnAppearing | ImGuiWindowFlags.NoNav);
        if (open)
        {
            var dl = ImGui.GetWindowDrawList();
            var wp = ImGui.GetWindowPos();
            var ws = ImGui.GetWindowSize();
            dl.AddRectFilled(wp, wp + new Vector2(ws.X, tabsH), U(T.Ground));
            Tabs(s, live, dl, wp, ws.X);
            ImGui.SetCursorScreenPos(wp + new Vector2(0, tabsH));
            float height = 0;
            if (ImGui.BeginChild("##cardbody", new Vector2(ws.X, ws.Y - tabsH)))
            {
                cdl = ImGui.GetWindowDrawList();
                var o = ImGui.GetCursorScreenPos();
                (cardX, cardW, cardY) = (o.X, ImGui.GetContentRegionAvail().X, o.Y);
                cardScaleW = Math.Min(cardW, ws.X - ImGui.GetStyle().ScrollbarSize);
                if (enemy)
                    EnemyCard(s, live);
                else if (unit >= 0)
                    SquadronCard(s, unit);
                else
                    OwnCard(s, live, k, cls!);
                height = cardY - o.Y;
            }
            EndBody(height);
            dl.AddRect(wp, wp + ws, U(T.Edge));
        }
        ImGui.End();
        ImGui.PopStyleVar(2);
        ImGui.PopStyleColor(2);
    }

    /// <summary>The tabs: the live card, then the pinned (a click selects; × unpins); at the right pin and shut.</summary>
    void Tabs(Snapshot s, int live, ImDrawListPtr dl, Vector2 a, float w)
    {
        float x = a.X + P(4);
        void Tab(int e, bool on, ImDrawListPtr dl)
        {
            ImGui.PushID(e);
            bool pin = pinned.Contains(e);
            if (dl.DockTab("##tab", new Vector2(x, a.Y), TabName(s, e), on, pin ? P(12) : 0, out float tw) && e != selected)
            {
                selected = e;
                ClearPicks();
            }
            if (pin && dl.CrossButton("##unpin", new Vector2(x + tw - P(10), a.Y + P(12)), "unpin her card"))
                pinned.Remove(e);
            ImGui.PopID();
            x += tw + P(2);
        }
        Tab(live, true, dl);
        foreach (int p in pinned)
            if (p != live)
                Tab(p, false, dl);
        bool isPinned = pinned.Contains(live);
        float right = a.X + w - P(4), shutW = ButtonWidth("×", "V"), pinW = ButtonWidth(isPinned ? "Unpin" : "Pin", "P");
        if (dl.Button("##shut", new Vector2(right - shutW, a.Y + P(1)), "×", "V"))
        {
            cardOpen = false;
            pinned.Remove(live);
        }
        if (ImGui.IsItemHovered())
            ImGui.SetTooltip("shut the card (V opens it again)");
        if (dl.Button("##pin", new Vector2(right - shutW - P(3) - pinW, a.Y + P(1)), isPinned ? "Unpin" : "Pin", "P"))
            TogglePin(live);
        if (ImGui.IsItemHovered())
            ImGui.SetTooltip("pin her card as a tab");
    }

    /// <summary>A tab's name: her name, an enemy's identity as held.</summary>
    string TabName(Snapshot s, int e) => s.Ships.IndexOf(e) < 0 && ContactIndex(s.Contacts, e) is int i and >= 0 ? Identity(s.Contacts, i) : ShipName(s, e);

    /// <summary>The unit whose card shows for a ship chosen by her unit's head: the one at the level her orders go to.</summary>
    int UnitOfCard(Snapshot s, int ship)
    {
        Chain(s);
        int level = Level(s);
        return level > 0 && level <= chain.Count ? chain[level - 1] : s.Groups.IndexOfShip(ship);
    }

    // the ships' names as the cards show them, made once a ship
    readonly Dictionary<int, string> shipNames = [];
    int shipNamesGeneration = -1;

    string ShipName(Snapshot s, int e)
    {
        if (shipNamesGeneration != world.Generation)
        {
            shipNamesGeneration = world.Generation;
            shipNames.Clear();
            targetLabels.Clear();
        }
        if (!shipNames.TryGetValue(e, out var n))
        {
            if (shipNames.Count > 512)
                shipNames.Clear();
            int k = s.Ships.IndexOf(e);
            shipNames[e] = n = world.NameOf(e) ?? (k >= 0 && world.Classes.TryGet(s.Ships.Class[k]) is { } c ? $"{c.Name} #{e}" : $"#{e}");
        }
        return n;
    }

    // ------------------------------------------------------------------ the card's pieces, laid down the body

    /// <summary>A title strip across the card: the rivet, the name in the accent's caps (muted when folded), a note, the
    /// state at the right; a click folds it. True when open.</summary>
    bool Strip(string title, string? note, ReadOnlySpan<byte> right, string? tip = null)
    {
        var a = new Vector2(cardX, cardY);
        float w = cardW;
        bool open = !shutStrips.Contains(title);
        ImGui.SetCursorScreenPos(a);
        if (ImGui.InvisibleButton(title, new Vector2(w, TitleH)))
        {
            if (open)
                shutStrips.Add(title);
            else
                shutStrips.Remove(title);
            open = !open;
        }
        bool hover = ImGui.IsItemHovered();
        if (hover && tip != null)
            Tip(tip);
        var dl = cdl;
        dl.AddRectFilledMultiColor(a, a + new Vector2(w, TitleH), U(T.TitleTop), U(T.TitleTop), U(T.TitleBottom), U(T.TitleBottom));
        dl.HLine(a.X, a.X + w, a.Y, T.Edge);
        dl.HLine(a.X + 1, a.X + w - 1, a.Y + 1, T.Bevel);
        dl.HLine(a.X, a.X + w, a.Y + TitleH - 1, T.Edge);
        float x = a.X + P(10);
        dl.Rivet(new Vector2(x, a.Y + TitleH / 2));
        x += P(7);
        dl.Text(UiFonts.Head, new Vector2(x, Mid(UiFonts.Head, a.Y, TitleH)), open ? T.Brass : hover ? T.Text : T.Muted, title);
        if (note != null)
            dl.Text(UiFonts.SansSmall, new Vector2(x + P(6) + Width(UiFonts.Head, title), Mid(UiFonts.SansSmall, a.Y, TitleH)), T.Muted, note);
        if (right.Length > 0)
            dl.TextRight(UiFonts.SansSmall, a.X + w - P(6), Mid(UiFonts.SansSmall, a.Y, TitleH), T.Muted, right);
        cardY += TitleH;
        return open;
    }

    /// <summary>A table row across the card (the n-th of its table): its top left.</summary>
    Vector2 CardRow(ref int n)
    {
        var a = new Vector2(cardX, cardY);
        cdl.RowBg(a, cardW, RowH, n++);
        cardY += RowH;
        return a;
    }

    /// <summary>A line of words in a row from x, cut at the right; where it ends.</summary>
    float Words(Vector2 row, float x, float right, Vector4 colour, ReadOnlySpan<byte> s, ImFontPtr? font = null)
    {
        var f = font ?? UiFonts.SansSmall;
        return cdl.TextClipped(f, new Vector2(x, Mid(f, row.Y, RowH)), right, colour, s);
    }

    /// <summary>A figure right-aligned at x in a row; where it starts.</summary>
    float Figure(Vector2 row, float right, Vector4 colour, ReadOnlySpan<byte> s, ImFontPtr? font = null)
    {
        var f = font ?? UiFonts.Mono;
        float x = right - Width(f, s);
        cdl.Text(f, new Vector2(x, Mid(f, row.Y, RowH)), colour, s);
        return x;
    }

    /// <summary>A row of one muted line (none, not engaged).</summary>
    void NoneRow(ReadOnlySpan<byte> s)
    {
        int n = 0;
        var r = CardRow(ref n);
        Words(r, r.X + P(6), r.X + cardW - P(6), T.Muted, s);
    }

    /// <summary>The head: a name and a line under it at the left, state chips at the right, on the resource bar's ground.</summary>
    void Head(ReadOnlySpan<byte> name, Vector4 nameColour, ReadOnlySpan<byte> sub,
        ReadOnlySpan<byte> c1, ChipKind k1, ReadOnlySpan<byte> c2 = default, ChipKind k2 = ChipKind.Plain,
        ReadOnlySpan<byte> c3 = default, ChipKind k3 = ChipKind.Plain)
    {
        var a = new Vector2(cardX, cardY);
        float h = P(36);
        cdl.ResourceBarBg(a, cardW, h);
        float x = a.X + cardW - P(8);
        if (c3.Length > 0)
            x -= ChipWidth(c3) + P(4);
        if (c2.Length > 0)
            x -= ChipWidth(c2) + P(4);
        if (c1.Length > 0)
            x -= ChipWidth(c1) + P(4);
        float chipsLeft = x;
        float cy = a.Y + P(4);
        if (c1.Length > 0)
            x += cdl.Chip(new Vector2(x + P(4), cy), c1, k1) + P(4);
        if (c2.Length > 0)
            x += cdl.Chip(new Vector2(x + P(4), cy), c2, k2) + P(4);
        if (c3.Length > 0)
            cdl.Chip(new Vector2(x + P(4), cy), c3, k3);
        cdl.TextClipped(UiFonts.Title, new Vector2(a.X + P(8), a.Y + P(1)), chipsLeft - P(4), nameColour, name);
        cdl.TextClipped(UiFonts.SansSmall, new Vector2(a.X + P(8), a.Y + P(19)), a.X + cardW - P(8), T.Muted, sub);
        cardY += h;
    }

    /// <summary>A cell of the figures' row (from <c>new Vector2(cardX, cardY)</c>, closed by <see cref="EndFigures"/>): a
    /// label over a mono value with a muted note after it.</summary>
    void FigureCell(Vector2 row, ref float x, float w, string label, ReadOnlySpan<byte> value, ReadOnlySpan<byte> note, Vector4 colour, bool last = false)
    {
        float h = P(32);
        cdl.Text(UiFonts.SansSmall, new Vector2(x + P(8), row.Y + P(2)), T.Muted, label);
        float vx = x + P(8), vy = row.Y + P(16);
        cdl.TextClipped(UiFonts.Mono, new Vector2(vx, vy), x + w - P(4), colour, value);
        float nx = vx + Width(UiFonts.Mono, value) + P(4);
        if (note.Length > 0 && nx < x + w - P(8))
            cdl.TextClipped(UiFonts.SansSmall, new Vector2(nx, vy + P(1)), x + w - P(4), T.Muted, note);
        x += w;
        if (!last)
            cdl.AddLine(new Vector2(x, row.Y), new Vector2(x, row.Y + h - 1), U(T.Line));
    }

    void EndFigures()
    {
        cardY += P(32);
        cdl.HLine(cardX, cardX + cardW, cardY - 1, T.Edge);
    }

    // ------------------------------------------------------------------ the card of one of the player's ships

    void OwnCard(Snapshot s, int e, int k, ShipClass cls)
    {
        var v = s.Ships;
        int ci = s.Conditions.IndexOf(e), di = s.Details.IndexOf(e);
        CardHead(s, e, k, cls, ci);
        BigFour(s, e, k, cls, ci);
        if (Strip("CONDITION", "heights ×2", ci >= 0 ? U8.W($"{s.Conditions.Hits[ci]} hits · {s.Conditions.Fires[ci]} fires") : "no hits"u8))
            Condition(s, e, k, cls, ci, di);
        if (cls.Mounts.Length > 0 && Strip("GUNS", "fore to aft", di >= 0 ? MountsLine(s.Details, di) : ""u8))
            Guns(s, e, k, cls, di);
        if (cls.Batteries.Length > 0 && Strip("FIRE CONTROL", null, EngagedLine(s, e)))
            FireControl(s, e, cls);
        if (Strip("DAMAGE CONTROL", null, di >= 0 ? U8.W($"{s.Details.PartyCount[di]} parties · {s.Details.PumpsRigged[di]} pumps") : ""u8))
            DamageControl(s, cls, ci, di);
        if (Strip("HITS TAKEN", null, U8.W($"{feed.HitsOn(e).Count}")))
            HitsTaken(e, cls);
        if (cls.Launchers.Length > 0 && Strip("TORPEDOES", null, U8.W($"{v.TorpedoesLoaded[k]} loaded")))
            TorpedoSection(s, k, cls);
    }

    ReadOnlySpan<byte> MountsLine(ShipDetailView d, int di)
    {
        int working = 0;
        for (int m = 0; m < d.MountCount[di]; m++)
            working += d.Mount[d.MountStart[di] + m] is MountStatus.Ready or MountStatus.Loading ? 1 : 0;
        return U8.W($"{working} of {d.MountCount[di]} mounts");
    }

    ReadOnlySpan<byte> EngagedLine(Snapshot s, int e)
    {
        int n = 0;
        for (int i = 0; i < s.Batteries.Count; i++)
            n += s.Batteries.Shooter[i] == e ? 1 : 0;
        return n == 0 ? "not engaged"u8 : U8.W($"{n} {(n == 1 ? "battery" : "batteries")} engaged");
    }

    void CardHead(Snapshot s, int e, int k, ShipClass cls, int ci)
    {
        var v = s.Ships;
        var gv = s.Groups;
        int g = gv.IndexOfShip(e);
        var sub = g >= 0 ? U8.W($"{cls.Name} · {gv.Name[g]}{(gv.GuideShip[g] == e ? ", guide" : "")}") : U8.W($"{cls.Name}");
        // her state: going down, in action; who commands her; falling out
        bool going = ci >= 0 && s.Conditions.Status[ci] == ShipStatus.GoingDown;
        var c1 = going
            ? s.Conditions.Loss[ci] switch { LossKind.Magazine => "magazine: going down"u8, LossKind.Capsized => "capsizing"u8, LossKind.BrokeInTwo => "broken in two"u8, _ => "foundering"u8 }
            : v.Encounter[k] != 0 ? "in action"u8 : default;
        var held = g >= 0 ? DetachmentOf(gv, g, e) : Detachment.InLine;
        bool ai = g >= 0 && gv.Commanded[TopOf(gv, g)] == 1;
        var c2 = Held(held) ? "your orders"u8 : U8.W($"{(ai ? "AI" : "captain")} · {CaptainWord((CaptainOrder)v.Captain[k])}");
        var c3 = v.Reason[k] != 0 ? U8.W($"{ReasonText(v.Reason[k])}") : default;
        if (c1.Length == 0)
            Head(U8.W($"{ShipName(s, e)}"), T.Bright, sub, c2, Held(held) ? ChipKind.Amber : ChipKind.Plain, c3, ChipKind.Amber);
        else
            Head(U8.W($"{ShipName(s, e)}"), T.Bright, sub, c1, going ? ChipKind.Hostile : ChipKind.Amber, c2, Held(held) ? ChipKind.Amber : ChipKind.Plain, c3, ChipKind.Amber);
    }

    static int TopOf(GroupsView gv, int g)
    {
        while (gv.Parent[g] != 0 && gv.IndexOfGroup(gv.Parent[g]) is int p and >= 0)
            g = p;
        return g;
    }

    static string CaptainWord(CaptainOrder o) => o switch
    {
        CaptainOrder.Fight => "fight",
        CaptainOrder.Follow => "follow",
        CaptainOrder.Run => "run",
        CaptainOrder.Station => "in line",
        CaptainOrder.Route => "route",
        CaptainOrder.Shadow => "shadow",
        CaptainOrder.Home => "home",
        CaptainOrder.Screen => "screen",
        _ => "course",
    };

    /// <summary>The four figures: speed (of what she can make now), course (and ordered), target (and range), hits.</summary>
    void BigFour(Snapshot s, int e, int k, ShipClass cls, int ci)
    {
        var v = s.Ships;
        var c = s.Conditions;
        double top = cls.Motion.MaxSpeedKn, now = ci >= 0 ? cls.SpeedAt(cls.PowerAt(top) * c.HeldCap[ci]) : top;
        var (p, f) = v.PoseAt(k, world.DrawMs);
        int b = MainRow(s, e);
        int target = b >= 0 ? s.Batteries.Target[b] : v.FireTarget[k];
        var row = new Vector2(cardX, cardY);
        float x = row.X, unit = cardW / 4.4f;
        FigureCell(row, ref x, unit, "SPEED", U8.W($"{v.Speed[k] / Handling.MsPerKnot:0.0} kn"), U8.W($"of {now:0.0}"), now < top - 0.5 ? T.Amber : T.Bright);
        FigureCell(row, ref x, unit, "COURSE", U8.W($"{Earth.Heading(p, f):000}°"), U8.W($"ordered {v.CourseOrderDeg[k]:000}°"), T.Bright);
        FigureCell(row, ref x, unit * 1.4f, "TARGET", target > 0 ? LabelOf(s, target) : "-"u8,
            b >= 0 && s.Batteries.RangeM[b] > 0 ? U8.W($"{s.Batteries.RangeM[b] / 1000:0.0} km") : ""u8, T.Bright);
        FigureCell(row, ref x, cardX + cardW - x, "HITS", U8.W($"{(ci >= 0 ? c.Hits[ci] : 0)}"),
            ci >= 0 && c.BreachM2[ci] > 0 ? U8.W($"holes {c.BreachM2[ci]:0.0} m²") : ""u8, ci >= 0 && c.Hits[ci] > 0 ? T.Amber : T.Bright, last: true);
        EndFigures();
    }

    /// <summary>The row of her main battery in the snapshot's batteries (the first battery engaged), or -1.</summary>
    int MainRow(Snapshot s, int e)
    {
        var b = s.Batteries;
        int best = -1;
        for (int i = 0; i < b.Count; i++)
            if (b.Shooter[i] == e && (best < 0 || b.Battery[i] < b.Battery[best]))
                best = i;
        return best;
    }

    // a target's label as the player knows it, made when it changes
    readonly Dictionary<(int, int), string> targetLabels = [];

    ReadOnlySpan<byte> LabelOf(Snapshot s, int entity)
    {
        int taken = -2;
        var c = s.Contacts;
        for (int i = 0; i < c.Count; i++)
            if (c.Entity[i] == entity)
                taken = c.TakenFor[i];
        if (!targetLabels.TryGetValue((entity, taken), out var text))
        {
            if (targetLabels.Count > 512)
                targetLabels.Clear();
            targetLabels[(entity, taken)] = text = s.Ships.IndexOf(entity) >= 0 ? ShipName(s, entity) : TargetLabel(s, entity);
        }
        return U8.W($"{text}");
    }

    // ------------------------------------------------------------------ condition

    void Condition(Snapshot s, int e, int k, ShipClass cls, int ci, int di)
    {
        var v = s.Ships;
        var c = s.Conditions;
        var d = s.Details;
        // the sim's own attitude for the moment (going down too: the one the sprite and the hit queries see)
        var att = Attitude.At(cls, v.Attitude[k], v.Loss[k], v.LossMs[k], v.GoneMs[k], world.DrawMs);
        cardY += Plate(cdl, new Vector2(cardX, cardY), cardW, cardScaleW, s, k, cls, di, att);
        cdl.HLine(cardX, cardX + cardW, cardY, T.Edge);
        // the clinometer, the readings beside it
        float cw = P(104), h = 5 * RowH;
        Clinometer(cdl, new Vector2(cardX, cardY + 1), new Vector2(cw, h - 1), cls, att, v.Loss[k]);
        cdl.AddLine(new Vector2(cardX + cw, cardY), new Vector2(cardX + cw, cardY + h), U(T.Line));
        float x0 = cardX + cw + 1, rw = cardW - cw - 1, half = rw / 2;
        float k1 = x0 + P(6), v1 = x0 + half - P(6), k2 = x0 + half + P(6), v2 = x0 + rw - P(6);
        var wp = HitQuery.WaterOf(cls, att);
        var vert = cls.Vertical;
        var (ex0, ex1) = Extent(cls);
        double water = ci >= 0 ? c.WaterT[ci] : 0, reserve = ci >= 0 ? c.ReserveT[ci] : 1;
        double afloat = reserve / Math.Max(reserve + water, 1e-6);
        Vector2 Row(int n)
        {
            var a = new Vector2(x0, cardY + n * RowH);
            cdl.RowBg(a, rw, RowH, n);
            return a;
        }
        void Key(Vector2 r, float x, ReadOnlySpan<byte> key) => cdl.Text(UiFonts.SansSmall, new Vector2(x, Mid(UiFonts.SansSmall, r.Y, RowH)), T.Muted, key);
        // afloat: a bar, the share, the water aboard
        var r = Row(0);
        Key(r, k1, "Afloat"u8);
        float bx = x0 + P(58);
        cdl.Bar(new Vector2(bx, r.Y + P(7)), new Vector2(P(64), P(6)), (float)afloat, afloat > 0.8 ? Ok : afloat > 0.6 ? T.Amber : Bad);
        cdl.AddRect(new Vector2(bx, r.Y + P(7)), new Vector2(bx + P(64), r.Y + P(13)), U(T.Edge));
        float tx = bx + P(70);
        var pct = U8.W($"{afloat * 100:0} %");
        cdl.Text(UiFonts.Mono, new Vector2(tx, Mid(UiFonts.Mono, r.Y, RowH)), T.Bright, pct);
        tx += Width(UiFonts.Mono, pct) + P(6);
        var aboard = U8.W($"{water:#,0} t");
        cdl.Text(UiFonts.Mono, new Vector2(tx, Mid(UiFonts.Mono, r.Y, RowH)), water > 0 ? T.Water : T.Muted, aboard);
        tx += Width(UiFonts.Mono, aboard) + P(4);
        Words(r, tx, v2, T.Muted, "aboard"u8);
        // trim, draughts at her ends
        r = Row(1);
        Key(r, k1, "Trim"u8);
        Figure(r, v1, Math.Abs(att.TrimDeg) >= 1 ? T.Amber : T.Bright, U8.W($"{Math.Abs(att.TrimDeg):0.00}° {(att.TrimDeg >= 0 ? "bow" : "stern")}"));
        Key(r, k2, "Draught"u8);
        Figure(r, v2, T.Bright, U8.W($"F {wp.At(ex1, 0) - vert.Keel:0.0} · A {wp.At(ex0, 0) - vert.Keel:0.0}"));
        // power and steering
        r = Row(2);
        double power = ci >= 0 ? c.PowerCap[ci] : 1;
        var steer = ci >= 0 ? c.Steering[ci] : Steering.Free;
        Key(r, k1, "Power"u8);
        Figure(r, v1, power < 0.995 ? T.Amber : T.Bright, U8.W($"{power * 100:0} %"));
        Key(r, k2, "Steering"u8);
        Figure(r, v2, steer == Steering.Free ? T.Bright : T.Amber, U8.W($"{SteerWord(steer)}"), UiFonts.SansSmall);
        // fires and the speed damage control asks for
        r = Row(3);
        int fires = ci >= 0 ? c.Fires[ci] : 0;
        Key(r, k1, "Fires"u8);
        Figure(r, v1, fires > 0 ? Fire : T.Muted, fires > 0 ? U8.W($"{fires}") : "none"u8, fires > 0 ? UiFonts.Mono : UiFonts.SansSmall);
        Key(r, k2, "Safe speed"u8);
        if (ci >= 0 && c.SafeSpeedKn[ci] < cls.Motion.MaxSpeedKn)   // over what she can make it binds nothing
            Figure(r, v2, T.Amber, U8.W($"{c.SafeSpeedKn[ci]:0} kn"));
        else
            Figure(r, v2, T.Muted, "any"u8, UiFonts.SansSmall);
        // her crew
        r = Row(4);
        Key(r, k1, "Crew"u8);
        if (di >= 0)
        {
            bool hurt = d.Killed[di] + d.Wounded[di] > 0;
            float x = v2;
            x = Figure(r, x, T.Bright, U8.W($"{d.Complement[di]:#,0}"));
            x = Figure(r, x - P(4), T.Muted, "wounded · of"u8, UiFonts.SansSmall);
            x = Figure(r, x - P(4), hurt ? T.Amber : T.Bright, U8.W($"{d.Wounded[di]:0}"));
            x = Figure(r, x - P(4), T.Muted, "killed ·"u8, UiFonts.SansSmall);
            Figure(r, x - P(4), hurt ? T.Amber : T.Bright, U8.W($"{d.Killed[di]:0}"));
        }
        cardY += h;
    }

    static string SteerWord(Steering s) => s switch
    {
        Steering.Jammed => "rudder jammed",
        Steering.Lost => "lost",
        Steering.Hand => "by hand",
        Steering.Engines => "by engines",
        _ => "free",
    };

    /// <summary>Whether a component is drawn on the profile: her upper works, funnels, conning tower and main mounts that
    /// stand on the hull or on each other (a top up a mast, drawn alone, would float).</summary>
    static bool Standing(in ComponentRow c, VerticalRow vert) =>
        c.Kind is ComponentKind.Superstructure or ComponentKind.Funnel or ComponentKind.ConningTower or ComponentKind.Main
        && c.Bounds.Base < vert.Keel + vert.Depth + 12;

    /// <summary>Her length's extent in her frame: the main deck's outline, else her cells.</summary>
    static (double X0, double X1) Extent(ShipClass cls)
    {
        double x0 = double.MaxValue, x1 = double.MinValue;
        foreach (var p in cls.Points(cls.DeckOutline))
            (x0, x1) = (Math.Min(x0, p.X), Math.Max(x1, p.X));
        if (x0 > x1)
            foreach (var c in cls.Cells)
                (x0, x1) = (Math.Min(x0, c.Box.X0), Math.Max(x1, c.Box.X1));
        return x0 > x1 ? (-cls.Length / 2, cls.Length / 2) : (x0, x1);
    }

    // the mounts' labels by class, made once a class
    readonly Dictionary<ShipClass, string[]> mountLabels = [];

    /// <summary>A mount's short name, by position (not the design's ids; the Royal Navy's habit, a label only): her main
    /// mounts forward lettered from A, amidships Q (P, Q, R for more), aft X then Y; the others by side and number from
    /// forward (S1, P1).</summary>
    string MountLabel(ShipClass cls, int m)
    {
        if (!mountLabels.TryGetValue(cls, out var names))
        {
            var mounts = cls.Mounts.ToArray();   // once a class
            names = new string[mounts.Length];
            int[] order = [.. Enumerable.Range(0, mounts.Length).Where(i => mounts[i].Kind == ComponentKind.Main).OrderByDescending(i => mounts[i].X)];
            double mid = 0.15 * cls.Length;
            int[] fwd = [.. order.Where(i => mounts[i].X > mid)], waist = [.. order.Where(i => Math.Abs(mounts[i].X) <= mid)],
                aft = [.. order.Where(i => mounts[i].X < -mid)];
            for (int j = 0; j < fwd.Length; j++)
                names[fwd[j]] = ((char)('A' + j)).ToString();
            for (int j = 0; j < waist.Length; j++)
                names[waist[j]] = ((char)((waist.Length == 1 ? 'Q' : 'P') + j)).ToString();
            for (int j = 0; j < aft.Length; j++)
                names[aft[j]] = ((char)('X' + Math.Min(j, 2))).ToString();
            int starboard = 0, port = 0;
            foreach (int i in Enumerable.Range(0, mounts.Length).OrderByDescending(i => mounts[i].X))
                names[i] ??= mounts[i].Y >= 0 ? $"S{++starboard}" : $"P{++port}";
            mountLabels[cls] = names;
        }
        return names[m];
    }

    // ------------------------------------------------------------------ guns

    // which batteries show a row a mount (the rest one row a battery): the main ones and small ones at first, a click on
    // the battery's row flips it; kept while the card is hers
    ulong gunsOpen;
    int gunsOpenFor = -1;

    void Guns(Snapshot s, int e, int k, ShipClass cls, int di)
    {
        var d = s.Details;
        long now = s.Time.Ms;
        if (gunsOpenFor != e)
        {
            (gunsOpenFor, gunsOpen) = (e, 0);
            for (int b = 0; b < Math.Min(cls.Batteries.Length, 64); b++)
                if (cls.Batteries[b].Kind == Battery.Main || cls.Links(cls.Batteries[b].Mounts).Length <= 4)
                    gunsOpen |= 1ul << b;
        }
        var a = new Vector2(cardX, cardY);
        cdl.Head(a, cardW);
        float xs = a.X + P(56), right = a.X + cardW - P(6);
        cdl.HeadCell(a, a.X + P(6), "Mount");
        cdl.HeadCell(a, xs, "State");
        cdl.HeadCell(a, right, "AP · com · HE", right: true);
        cardY += HeadH;
        int n = 0;
        for (int b = 0; b < cls.Batteries.Length; b++)
        {
            var bat = cls.Batteries[b];
            bool open = b < 64 && (gunsOpen & (1ul << b)) != 0;
            // the battery's row: its kind and guns, its target, the shell it loads
            var g = new Vector2(cardX, cardY);
            ImGui.PushID(b);
            var (click, hover) = HitRow("##bat", g, new Vector2(cardW, RowH));
            ImGui.PopID();
            if (click && b < 64)
                gunsOpen ^= 1ul << b;
            cdl.GroupBg(g, cardW, RowH, hover);
            cdl.Twisty(new Vector2(g.X + P(9), g.Y + RowH / 2), open);
            cdl.Text(UiFonts.SansBold, new Vector2(g.X + P(17), Mid(UiFonts.SansBold, g.Y, RowH)), T.Bright, bat.Kind == Battery.Main ? "Main" : "Sec.");
            float shellX = right;
            if (s.Ships.BatteryStart[k] >= 0)
            {
                // its shell (docs/battle/shell-choice.md A5): the kind it loads, and the round in its guns while that changes
                int o = s.Ships.BatteryOrdered[s.Ships.BatteryStart[k] + b], gi = s.Ships.BatteryInGuns[s.Ships.BatteryStart[k] + b];
                if (o == 0)
                    shellX = Figure(g, right, T.Muted, gi > 0 ? U8.W($"{KindName((ShellKind)(gi - 1))}, magazine order") : "none left"u8, UiFonts.SansSmall);
                else if (gi == o || gi == 0)
                    shellX = Figure(g, right, s.Ships.ShellHeld[k] > 0 && bat.Chief ? T.Brass : T.Bright, U8.W($"loads {KindName((ShellKind)(o - 1))}"), UiFonts.SansSmall);
                else
                    shellX = Figure(g, right, T.Amber, U8.W($"{KindName((ShellKind)(gi - 1))} in, then {KindName((ShellKind)(o - 1))}"), UiFonts.SansSmall);
            }
            int row = BatteryRow(s, e, b);
            float tx = Words(g, xs, shellX - P(8), T.Muted, U8.W($"{BatteryLabel(cls, b)} {(row >= 0 ? "on" : "· no target")}"));
            if (row >= 0)
                Words(g, tx + P(4), shellX - P(8), T.Bright, LabelOf(s, s.Batteries.Target[row]));
            cardY += RowH;
            n = 0;
            var mounts = ForeToAft(cls, b);
            if (open)
                foreach (int m in mounts)
                    MountRow(s, cls, di, m, now, ref n, xs, right);
            else
                BatterySummary(s, cls, di, b, mounts, ref n, xs, right);
        }
    }

    void MountRow(Snapshot s, ShipClass cls, int di, int m, long now, ref int n, float xs, float right)
    {
        var d = s.Details;
        var r = CardRow(ref n);
        cdl.Text(UiFonts.Mono, new Vector2(r.X + P(6), Mid(UiFonts.Mono, r.Y, RowH)), T.Bright, MountLabel(cls, m));
        if (di < 0)
        {
            Words(r, xs, right, T.Muted, "-"u8);
            return;
        }
        int j = d.MountStart[di] + m;
        var st = d.Mount[j];
        long until = d.MountUntilMs[j];
        float x = xs;
        if (st == MountStatus.Loading)
        {
            double reload = cls.Mounts[m].ReloadS * 1000;
            float done = reload > 0 ? Math.Clamp(1 - (float)((until - now) / reload), 0, 1) : 0;
            cdl.Bar(new Vector2(x, r.Y + P(7)), new Vector2(P(40), P(6)), done, T.PlanLine);
            x += P(46);
        }
        var text = st switch
        {
            MountStatus.Ready => "ready"u8,
            MountStatus.Loading => U8.W($"loading {Math.Max(0, (until - now) / 1000):0} s"),
            MountStatus.Jammed => until == long.MaxValue || until <= now ? "jammed in train"u8 : U8.W($"jammed in train · ~{Math.Ceiling((until - now) / 60_000.0):0} min"),
            MountStatus.Down => until > now ? U8.W($"down · ~{Math.Ceiling((until - now) / 1000.0):0} s") : "down"u8,
            _ => "out of action"u8,
        };
        var rounds = U8.W($"{Rounds(d.MountAp[j])} · {Rounds(d.MountCommon[j])} · {Rounds(d.MountHe[j])}");
        float rx = right - Width(UiFonts.Mono, rounds);
        x = Words(r, x, rx - P(6), st is MountStatus.Ready or MountStatus.Loading ? T.Text : MountInk(st), text);
        if (d.MountGunsOut[j] > 0)
            Words(r, x + P(4), rx - P(6), T.Amber, U8.W($"· {d.MountGunsOut[j]} gun out"));
        Figure(r, right, st == MountStatus.Out ? T.Muted : T.Bright, rounds);
    }

    /// <summary>A battery in one row: its mounts' span (S1–8), a pip a mount by state, what's wrong in words (or how many
    /// are ready), its rounds.</summary>
    void BatterySummary(Snapshot s, ShipClass cls, int di, int b, int[] mounts, ref int n, float xs, float right)
    {
        var d = s.Details;
        var r = CardRow(ref n);
        if (mounts.Length == 0)
            return;
        string first = MountLabel(cls, mounts[0]), last = MountLabel(cls, mounts[^1]);
        var span = mounts.Length == 1 ? U8.W($"{first}")
            : first.Length > 1 && last.Length > 1 && first[0] == last[0] ? U8.W($"{first}–{last.AsSpan(1)}") : U8.W($"{first}–{last}");
        cdl.Text(UiFonts.Mono, new Vector2(r.X + P(6), Mid(UiFonts.Mono, r.Y, RowH)), T.Bright, span);
        if (di < 0)
            return;
        float x = xs;
        int ready = 0, ap = 0, com = 0, he = 0, wrong = 0;
        foreach (int m in mounts)
        {
            int j = d.MountStart[di] + m;
            var st = d.Mount[j];
            var p = new Vector2(x, r.Y + P(5.5f));
            var q = p + new Vector2(P(7), P(9));
            if (st == MountStatus.Out)
                cdl.AddRectFilled(p, q, U(Bad with { W = 0.35f }));
            if (st == MountStatus.Loading)
                cdl.AddRectFilled(new Vector2(p.X, p.Y + P(5)), q, U(T.PlanLine with { W = 0.55f }));
            cdl.AddRect(p, q, U(st is MountStatus.Ready or MountStatus.Loading ? T.PlanLine with { W = 0.75f } : MountInk(st)));
            x += P(9);
            ready += st is MountStatus.Ready or MountStatus.Loading ? 1 : 0;
            (ap, com, he) = (ap + Math.Max(Rounds(d.MountAp[j]), 0), com + Math.Max(Rounds(d.MountCommon[j]), 0), he + Math.Max(Rounds(d.MountHe[j]), 0));
        }
        var rounds = U8.W($"{ap:#,0} · {com:#,0} · {he:#,0}");
        float rx = right - Width(UiFonts.Mono, rounds) - P(6);
        x += P(4);
        if (ready == mounts.Length)
            Words(r, x, rx, T.Text, U8.W($"{ready} ready"));
        else
            foreach (int m in mounts)
            {
                var st = d.Mount[d.MountStart[di] + m];
                if (st is MountStatus.Ready or MountStatus.Loading)
                    continue;
                if (wrong++ == 3)
                {
                    Words(r, x, rx, T.Muted, "…"u8);
                    break;
                }
                if (wrong > 1)
                    x = Words(r, x, rx, T.Muted, " · "u8);
                x = Words(r, x, rx, MountInk(st), U8.W($"{MountLabel(cls, m)} {(st == MountStatus.Out ? "out" : st == MountStatus.Jammed ? "jammed" : "down")}"));
            }
        Figure(r, right, T.Bright, rounds);
    }

    static int Rounds(int n) => n == int.MaxValue ? -1 : n;

    // the batteries' labels by class (BatteryRow.Label makes a string each read), made once a class
    readonly Dictionary<ShipClass, string[]> batteryLabels = [];

    string BatteryLabel(ShipClass cls, int battery)
    {
        if (!batteryLabels.TryGetValue(cls, out var all))
            batteryLabels[cls] = all = BatteryLabels(cls);
        return all[battery];
    }

    // apart, so the lambda's capture of the class isn't made on every call (C# makes it where the captured lives)
    static string[] BatteryLabels(ShipClass cls) => [.. Enumerable.Range(0, cls.Batteries.Length).Select(b => cls.Batteries[b].Label)];

    // each battery's mounts from forward, made once a class
    readonly Dictionary<ShipClass, int[][]> foreToAft = [];

    int[] ForeToAft(ShipClass cls, int battery)
    {
        if (!foreToAft.TryGetValue(cls, out var all))
        {
            var mounts = cls.Mounts.ToArray();
            all = new int[cls.Batteries.Length][];
            for (int b = 0; b < all.Length; b++)
                all[b] = [.. cls.Links(cls.Batteries[b].Mounts).ToArray().OrderByDescending(m => mounts[m].X)];
            foreToAft[cls] = all;
        }
        return all[battery];
    }

    int BatteryRow(Snapshot s, int e, int battery)
    {
        var b = s.Batteries;
        for (int i = 0; i < b.Count; i++)
            if (b.Shooter[i] == e && b.Battery[i] == battery)
                return i;
        return -1;
    }

    // ------------------------------------------------------------------ fire control

    void FireControl(Snapshot s, int e, ShipClass cls)
    {
        var b = s.Batteries;
        bool any = false;
        float xs = cardX + P(56), right = cardX + cardW - P(6);
        int n = 0;
        for (int bat = 0; bat < cls.Batteries.Length; bat++)
        {
            int i = BatteryRow(s, e, bat);
            if (i < 0)
                continue;
            any = true;
            var kind = cls.Batteries[bat].Kind;
            // its range, plot and whether it sees her; the discipline's phases
            var r = CardRow(ref n);
            cdl.Text(UiFonts.SansBold, new Vector2(r.X + P(6), Mid(UiFonts.SansBold, r.Y, RowH)), T.Bright, kind == Battery.Main ? "Main" : "Sec.");
            float px = Phases(r, right, b.State[i] == BatteryState.Firing ? b.Phase[i] : (FirePhase)255);
            float x = xs;
            if (b.RangeM[i] > 0)
            {
                x = Words(r, x, px - P(6), T.Bright, U8.W($"{b.RangeM[i]:#,0} m"), UiFonts.Mono);
                x = Words(r, x + P(4), px - P(6), T.Muted, U8.W($"· plot ±{b.SigmaM[i]:0} m ·"));
                Words(r, x + P(4), px - P(6), b.Sees[i] ? T.Muted : T.Amber, b.Sees[i] ? "seen"u8 : "not seen"u8);
            }
            else
                Words(r, x, px - P(6), b.Sees[i] ? T.Text : T.Amber, U8.W($"{StateText(b.State[i], b.Phase[i])}"));
            // what it fires on and by, and its tally
            r = CardRow(ref n);
            float fx = right;
            fx = Figure(r, fx, T.Bright, U8.W($"{b.InFlight[i]}"));
            fx = Figure(r, fx - P(3), T.Muted, "air"u8, UiFonts.SansSmall);
            fx = Figure(r, fx - P(6), T.Bright, U8.W($"{b.Hits[i]}"));
            fx = Figure(r, fx - P(3), T.Muted, "hits"u8, UiFonts.SansSmall);
            fx = Figure(r, fx - P(6), T.Bright, U8.W($"{b.Straddles[i]}"));
            fx = Figure(r, fx - P(3), T.Muted, "str"u8, UiFonts.SansSmall);
            fx = Figure(r, fx - P(6), T.Bright, U8.W($"{b.Salvos[i]}"));
            fx = Figure(r, fx - P(3), T.Muted, "salvos"u8, UiFonts.SansSmall);
            Words(r, xs, fx - P(8), T.Muted, b.Local[i]
                ? U8.W($"{BatteryLabel(cls, bat)} on {LabelOf(s, b.Target[i])} · local control")
                : U8.W($"{BatteryLabel(cls, bat)} on {LabelOf(s, b.Target[i])} · director, level {b.Level[i]} ({FireControlPresets.Name(b.Level[i])})"));
        }
        if (!any)
            NoneRow("not engaged"u8);
    }

    static readonly FirePhase[] PhaseOrder = [FirePhase.Opening, FirePhase.Ranging, FirePhase.Effect];

    /// <summary>Her fire discipline's phases as a strip ending at <paramref name="right"/>, the one she's in lit, those
    /// behind her muted (none lit: not firing); where it starts.</summary>
    float Phases(Vector2 row, float right, FirePhase now)
    {
        float h = P(15), y = row.Y + (RowH - h) / 2, x = right;
        for (int i = 2; i >= 0; i--)
        {
            var text = i == 0 ? "Open"u8 : i == 1 ? "Range"u8 : "Effect"u8;
            float w = Width(UiFonts.SansSmall, text) + P(12);
            x -= w - (i == 2 ? 0 : 1);
            bool on = PhaseOrder[i] == now, done = now != (FirePhase)255 && PhaseOrder[i] < now;
            var p = new Vector2(x, y);
            cdl.AddRectFilled(p, p + new Vector2(w, h), U(on ? T.Panel2 : T.Inset));
            cdl.AddRect(p, p + new Vector2(w, h), U(on ? T.PlanLine : T.StepEdge));
            cdl.Text(UiFonts.SansSmall, new Vector2(x + P(6), Mid(UiFonts.SansSmall, y, h)), on ? T.Bright : done ? T.Muted : T.Faint, text);
        }
        return x;
    }

    // ------------------------------------------------------------------ damage control, hits, torpedoes

    void DamageControl(Snapshot s, ShipClass cls, int ci, int di)
    {
        var d = s.Details;
        var c = s.Conditions;
        if (di < 0)
        {
            NoneRow("-"u8);
            return;
        }
        float xs = cardX + P(56), right = cardX + cardW - P(6);
        int n = 0;
        // each party at work in its own row; those standing by in one
        int idle = 0;
        double idleMen = 0;
        for (int i = 0; i < d.PartyCount[di]; i++)
        {
            int j = d.PartyStart[di] + i;
            if (d.PartyFire[j] < 0 && d.PartyJob[j] == RepairJob.None)
            {
                (idle, idleMen) = (idle + 1, idleMen + d.PartyMen[j]);
                continue;
            }
            var r = CardRow(ref n);
            cdl.Text(UiFonts.Mono, new Vector2(r.X + P(6), Mid(UiFonts.Mono, r.Y, RowH)), T.Bright, U8.W($"{d.PartyMen[j]:0}"));
            float x = Words(r, xs, right, d.PartyFire[j] >= 0 ? Fire : T.Text, d.PartyFire[j] >= 0 ? "fighting a fire"u8 : JobWord(d.PartyJob[j]));
            if (d.PartyJob[j] != RepairJob.None && d.PartyDone[j] > 0)
                Words(r, x + P(4), right, T.Muted, U8.W($"· {d.PartyDone[j]:0} man-min done"));
        }
        if (idle > 0)
        {
            var r = CardRow(ref n);
            cdl.Text(UiFonts.Mono, new Vector2(r.X + P(6), Mid(UiFonts.Mono, r.Y, RowH)), T.Muted, U8.W($"{idleMen:0}"));
            Words(r, xs, right, T.Muted, U8.W($"{idle} {(idle == 1 ? "party" : "parties")} standing by"));
        }
        if (d.PartyCount[di] == 0)
            NoneRow("no parties"u8);
        {
            var r = CardRow(ref n);
            Words(r, r.X + P(6), right, T.Muted, U8.W($"Pumps rigged {d.PumpsRigged[di]} · spaces with water {d.FloodCount[di]} · holes {d.HoleCount[di]}"));
        }
        if (ci >= 0 && (c.CounterfloodPortT[ci] > 0 || c.CounterfloodStarboardT[ci] > 0))
        {
            var r = CardRow(ref n);
            Words(r, r.X + P(6), right, T.Muted, U8.W($"Counterflooding left: {c.CounterfloodPortT[ci]:0} t port, {c.CounterfloodStarboardT[ci]:0} t stbd"));
        }
        for (int i = 0; i < d.MagazineCount[di]; i++)
        {
            var r = CardRow(ref n);
            Words(r, r.X + P(6), right, T.Water, U8.W($"Magazine flooded: {cls.RoomId(d.MagazineRoom[d.MagazineStart[di] + i])}"));
        }
    }

    static ReadOnlySpan<byte> JobWord(RepairJob job) => job switch
    {
        RepairJob.Door => "closing a door"u8,
        RepairJob.Redog => "re-dogging a door"u8,
        RepairJob.Caulk => "caulking"u8,
        RepairJob.Shore => "shoring a bulkhead"u8,
        RepairJob.Plug => "plugging a hole"u8,
        RepairJob.Patch => "patching a hole"u8,
        RepairJob.Seam => "a sprung seam"u8,
        RepairJob.RigPump => "rigging a pump"u8,
        RepairJob.Mat => "a collision mat"u8,
        RepairJob.Steering => "rigging the steering"u8,
        RepairJob.None => "standing by"u8,
        _ => "repairs"u8,
    };

    // her hits' lines (the time apart), made when a hit comes
    int hitsFor = -1, hitsKnown = -1;
    readonly List<(string When, string What)> hitLines = [];

    void HitsTaken(int e, ShipClass cls)
    {
        var hits = feed.HitsOn(e);
        if (hitsFor != e || hitsKnown != hits.Count)
        {
            (hitsFor, hitsKnown) = (e, hits.Count);
            hitLines.Clear();
            for (int i = hits.Count - 1; i >= 0 && hitLines.Count < 20; i--)
            {
                var x = feed.Lines[hits[i]];
                string when = new WorldTime(x.Ms).ToUtc().ToString("HH:mm", U8.Inv);
                hitLines.Add((when, x.Kind == CombatEvent.Lost
                    ? (LossKind)x.Count switch { LossKind.Magazine => "magazine exploded", LossKind.Capsized => "capsized", LossKind.BrokeInTwo => "broke in two", _ => "foundered" }
                    : HitText(x, cls)));
            }
        }
        if (hitLines.Count == 0)
            NoneRow("none"u8);
        int n = 0;
        float right = cardX + cardW - P(6);
        foreach (var (when, what) in hitLines)
        {
            var r = CardRow(ref n);
            cdl.Text(UiFonts.Mono, new Vector2(r.X + P(6), Mid(UiFonts.Mono, r.Y, RowH)), T.Muted, when);
            Words(r, r.X + P(56), right, T.Text, U8.W($"{what}"));
            if (ImGui.IsMouseHoveringRect(r, r + new Vector2(cardW, RowH)) && Width(UiFonts.SansSmall, what) > right - r.X - P(56))
                Tip(what);
        }
        if (hits.Count > hitLines.Count)
        {
            var r = CardRow(ref n);
            Words(r, r.X + P(56), right, T.Muted, U8.W($"and {hits.Count - hitLines.Count} more"));
        }
    }

    void TorpedoSection(Snapshot s, int k, ShipClass cls)
    {
        var t = cls.Torpedoes[cls.Launchers[0].Torpedo];
        int n = 0;
        float right = cardX + cardW - P(6);
        var r = CardRow(ref n);
        Words(r, r.X + P(6), right, T.Text, U8.W($"{s.Ships.TorpedoesLoaded[k]} loaded, {s.Ships.TorpedoesLeft[k]} left · {t.CalibreMm:0} mm"));
        r = CardRow(ref n);
        Words(r, r.X + P(6), right, T.Muted, U8.W($"{t.FastKn:0} kn to {t.FastRangeM / 1000:0.0} km, or {t.SlowKn:0} kn to {t.SlowRangeM / 1000:0.0} km"));
    }
}
