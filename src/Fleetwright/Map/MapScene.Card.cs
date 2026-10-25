using System.Numerics;
using Fleetwright.Designer;
using Fleetwright.Shipgen;
using Fleetwright.Sim;
using Fleetwright.Sim.Ai.Captains;
using Fleetwright.Sim.Combat;
using Fleetwright.Sim.Core;
using Fleetwright.Sim.Ships;
using ImGuiNET;

namespace Fleetwright.Map;

/// <summary>The ship card (docs/map-ui/battle-hud.md U3b): docked at the right, opened by the click that selects a ship,
/// following the selection; pinned cards (P) stay as tabs above it. For one of the player's ships: her name and who
/// commands her, speed, course, target and hits; her condition drawn from her class (the plan with her mounts by state,
/// her holes, fires and wrecked parts; the profile with the water in her spaces and her waterline now against her
/// design's), flotation, list, power, steering and crew; each mount's state and rounds; each battery's fire control;
/// damage control's parties and pumps; the hits she took; her torpedoes. Its text is written without garbage
/// (<see cref="U8"/>); what changes only with a hit is made then.</summary>
public sealed partial class MapScene
{
    readonly CombatFeed feed = new();
    readonly List<int> pinned = [];
    const int MaxPins = Snapshot.MaxDetail - 1;
    bool cardOpen = true;
    /// <summary>Where the dock starts this frame (screen x): the strip centres in what's left of it.</summary>
    float dockLeft = float.MaxValue;

    static readonly Vector4 Ok = new(0.525f, 0.78f, 0.494f, 1), OkDim = new(0.66f, 0.78f, 0.64f, 1), WaterTint = new(0.435f, 0.655f, 0.863f, 1),
        Fire = new(1f, 0.6f, 0.25f, 1), CardBg = new(0.051f, 0.071f, 0.086f, 0.96f);

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
        float sc = UiFonts.Scale, pad = 8 * sc, w = 440 * sc;
        float top = HudTop, bottom = vp.Pos.Y + vp.Size.Y - pad - 30 * sc;
        dockLeft = vp.Pos.X + vp.Size.X - pad - w;
        ImGui.SetNextWindowPos(new Vector2(vp.Pos.X + vp.Size.X - pad, top), ImGuiCond.Always, new Vector2(1, 0));
        ImGui.SetNextWindowSize(new Vector2(w, bottom - top));
        ImGui.PushStyleColor(ImGuiCol.WindowBg, CardBg);
        ImGui.PushStyleVar(ImGuiStyleVar.WindowPadding, new Vector2(0, 0));
        bool open = ImGui.Begin("##card", ImGuiWindowFlags.NoDecoration | ImGuiWindowFlags.NoMove | ImGuiWindowFlags.NoSavedSettings |
                                          ImGuiWindowFlags.NoFocusOnAppearing | ImGuiWindowFlags.NoNav);
        ImGui.PopStyleVar();
        ImGui.PopStyleColor();
        if (open)
        {
            Tabs(s, live);
            ImGui.PushStyleVar(ImGuiStyleVar.WindowPadding, new Vector2(12, 6) * sc);
            if (ImGui.BeginChild("##cardbody", Vector2.Zero, ImGuiChildFlags.AlwaysUseWindowPadding))
            {
                if (enemy)
                    EnemyCard(s, live);
                else if (unit >= 0)
                    SquadronCard(s, unit);
                else
                    OwnCard(s, live, k, cls!);
            }
            ImGui.EndChild();
            ImGui.PopStyleVar();
        }
        ImGui.End();
    }

    /// <summary>The tabs: the live card, then the pinned (a click selects; × unpins); at the right pin and close.</summary>
    void Tabs(Snapshot s, int live)
    {
        float sc = UiFonts.Scale;
        ImGui.SetCursorPos(new Vector2(8, 6) * sc);
        ImGui.PushStyleVar(ImGuiStyleVar.FramePadding, new Vector2(8, 3) * sc);
        void Tab(int e, bool on)
        {
            ImGui.PushID(e);
            bool pin = pinned.Contains(e);
            ImGui.PushStyleColor(ImGuiCol.Button, on ? DesignerTheme.Panel2 : new Vector4(1, 1, 1, 0.03f));
            ImGui.PushStyleColor(ImGuiCol.Text, on ? DesignerTheme.Bright : pin ? DesignerTheme.Brass : DesignerTheme.Muted);
            if (ImGui.Button(TabName(s, e)) && e != selected)
            {
                selected = e;
                ClearPicks();
            }
            ImGui.PopStyleColor(2);
            if (pin)
            {
                ImGui.SameLine(0, 0);
                if (Seg("×##unpin"))
                    pinned.Remove(e);
            }
            ImGui.PopID();
            ImGui.SameLine(0, 3 * sc);
        }
        Tab(live, true);
        foreach (int p in pinned)
            if (p != live)
                Tab(p, false);
        float right = ImGui.GetWindowWidth() - 8 * sc - 2 * 26 * sc;
        if (ImGui.GetCursorPosX() < right)
            ImGui.SetCursorPosX(right);
        if (Seg("Pin##pin", pinned.Contains(live), first: true))
            TogglePin(live);
        if (ImGui.IsItemHovered())
            ImGui.SetTooltip("pin her card as a tab (P)");
        ImGui.SameLine(0, 2 * sc);
        if (Seg("×##close", first: true))
        {
            cardOpen = false;
            pinned.Remove(live);
        }
        if (ImGui.IsItemHovered())
            ImGui.SetTooltip("shut the card (V opens it again)");
        ImGui.PopStyleVar();
        var p0 = ImGui.GetWindowPos();
        float y = p0.Y + ImGui.GetCursorPosY() + 2 * sc;
        ImGui.GetWindowDrawList().AddLine(new Vector2(p0.X, y), new Vector2(p0.X + ImGui.GetWindowWidth(), y), ImGui.GetColorU32(DesignerTheme.Line));
        ImGui.Dummy(new Vector2(1, 4 * sc));
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

    // ------------------------------------------------------------------ the card of one of the player's ships

    void OwnCard(Snapshot s, int e, int k, ShipClass cls)
    {
        var v = s.Ships;
        int ci = s.Conditions.IndexOf(e), di = s.Details.IndexOf(e);
        CardHead(s, e, k, cls, ci);
        BigFour(s, e, k, cls, ci);
        if (Section("Condition", ci >= 0 ? U8.W($"{s.Conditions.Hits[ci]} hits · {s.Conditions.Fires[ci]} fires") : "no hits"u8))
            Condition(s, e, k, cls, ci, di);
        if (cls.Mounts.Length > 0 && Section("Guns", di >= 0 ? MountsLine(s.Details, di) : default))
            Guns(s, e, cls, di);
        if (cls.Batteries.Length > 0 && Section("Fire control", default))
            FireControl(s, e, cls);
        if (Section("Damage control", di >= 0 ? U8.W($"{s.Details.PartyCount[di]} parties · {s.Details.PumpsRigged[di]} pumps") : default))
            DamageControl(s, cls, ci, di);
        if (Section("Hits taken", U8.W($"{feed.HitsOn(e).Count}")))
            HitsTaken(e, cls);
        if (cls.Launchers.Length > 0 && Section("Torpedoes", U8.W($"{v.TorpedoesLoaded[k]} loaded")))
            TorpedoSection(s, k, cls);
    }

    ReadOnlySpan<byte> MountsLine(ShipDetailView d, int di)
    {
        int working = 0;
        for (int m = 0; m < d.MountCount[di]; m++)
            working += d.Mount[d.MountStart[di] + m] is MountStatus.Ready or MountStatus.Loading ? 1 : 0;
        return U8.W($"{working} of {d.MountCount[di]} mounts");
    }

    /// <summary>A folding section's head, its state at the right: true when open.</summary>
    static bool Section(string title, ReadOnlySpan<byte> right)
    {
        ImGui.Spacing();
        ImGui.PushStyleColor(ImGuiCol.Header, new Vector4(1, 1, 1, 0.03f));
        ImGui.PushStyleColor(ImGuiCol.HeaderHovered, new Vector4(1, 1, 1, 0.07f));
        ImGui.PushStyleColor(ImGuiCol.HeaderActive, new Vector4(1, 1, 1, 0.1f));
        ImGui.PushStyleColor(ImGuiCol.Text, DesignerTheme.Brass);
        bool open = ImGui.CollapsingHeader(title, ImGuiTreeNodeFlags.DefaultOpen);
        ImGui.PopStyleColor(4);
        if (right.Length > 0)
        {
            var min = ImGui.GetItemRectMin();
            var max = ImGui.GetItemRectMax();
            var size = U8.Size(right);
            U8.Draw(ImGui.GetWindowDrawList(), new Vector2(max.X - size.X - 6 * UiFonts.Scale, (min.Y + max.Y - size.Y) / 2),
                ImGui.GetColorU32(DesignerTheme.Muted), right);
        }
        return open;
    }

    /// <summary>A coloured label: a word on a tinted ground.</summary>
    static void Chip(ReadOnlySpan<byte> text, Vector4 colour)
    {
        float sc = UiFonts.Scale;
        ImGui.PushFont(UiFonts.SansSmall);
        var size = U8.Size(text);
        var p = ImGui.GetCursorScreenPos();
        var dl = ImGui.GetWindowDrawList();
        dl.AddRectFilled(p, p + size + new Vector2(10, 3) * sc, ImGui.GetColorU32(colour with { W = 0.16f }), 2 * sc);
        U8.Draw(dl, p + new Vector2(5, 1.5f) * sc, ImGui.GetColorU32(colour), text);
        ImGui.Dummy(size + new Vector2(10, 3) * sc);
        ImGui.PopFont();
        ImGui.SameLine(0, 4 * sc);
    }

    void CardHead(Snapshot s, int e, int k, ShipClass cls, int ci)
    {
        var v = s.Ships;
        var gv = s.Groups;
        ImGui.PushFont(UiFonts.PosterFigure);
        ImGui.TextColored(DesignerTheme.Bright, ShipName(s, e));
        ImGui.PopFont();
        int g = gv.IndexOfShip(e);
        ImGui.PushTextWrapPos(0);
        if (g >= 0)
            U8.Text(U8.W($"{cls.Name} · {gv.Name[g]}{(gv.GuideShip[g] == e ? ", guide" : "")}"), DesignerTheme.Muted);
        else
            U8.Text(U8.W($"{cls.Name}"), DesignerTheme.Muted);
        ImGui.PopTextWrapPos();
        // her state: in action, who commands her, falling out, going down
        bool going = ci >= 0 && s.Conditions.Status[ci] == ShipStatus.GoingDown;
        if (going)
            Chip(s.Conditions.Loss[ci] switch { LossKind.Magazine => "MAGAZINE: GOING DOWN"u8, LossKind.Capsized => "CAPSIZING"u8, LossKind.BrokeInTwo => "BROKEN IN TWO"u8, _ => "FOUNDERING"u8 }, Bad);
        else if (v.Encounter[k] != 0)
            Chip("IN ACTION"u8, DesignerTheme.Amber);
        var held = g >= 0 ? DetachmentOf(gv, g, e) : Detachment.InLine;
        bool ai = g >= 0 && gv.Commanded[TopOf(gv, g)] == 1;
        if (Held(held))
            Chip("YOUR ORDERS"u8, DesignerTheme.Brass);
        else
            Chip(U8.W($"{(ai ? "AI" : "CAPTAIN")} · {CaptainWord((CaptainOrder)v.Captain[k])}"), Friend);
        if (v.Reason[k] != 0)
            Chip(U8.W($"{ReasonText(v.Reason[k])}"), Bad);
        ImGui.NewLine();
    }

    static int TopOf(GroupsView gv, int g)
    {
        while (gv.Parent[g] != 0 && gv.IndexOfGroup(gv.Parent[g]) is int p and >= 0)
            g = p;
        return g;
    }

    static string CaptainWord(CaptainOrder o) => o switch
    {
        CaptainOrder.Fight => "FIGHT",
        CaptainOrder.Follow => "FOLLOW",
        CaptainOrder.Run => "RUN",
        CaptainOrder.Station => "IN LINE",
        CaptainOrder.Route => "ROUTE",
        CaptainOrder.Shadow => "SHADOW",
        CaptainOrder.Home => "HOME",
        CaptainOrder.Screen => "SCREEN",
        _ => "COURSE",
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
        ImGui.Spacing();
        if (!ImGui.BeginTable("##big", 4, ImGuiTableFlags.BordersInnerV | ImGuiTableFlags.SizingStretchSame))
            return;
        Fig("SPEED", U8.W($"{v.Speed[k] / Handling.MsPerKnot:0.0} kn"), U8.W($"of {now:0.0} now"), now < top - 0.5 ? DesignerTheme.Amber : DesignerTheme.Bright);
        Fig("COURSE", U8.W($"{Earth.Heading(p, f):000}°"), U8.W($"ordered {v.CourseOrderDeg[k]:000}°"), DesignerTheme.Bright);
        Fig("TARGET", target > 0 ? LabelOf(s, target) : "-"u8, b >= 0 && s.Batteries.RangeM[b] > 0 ? U8.W($"{s.Batteries.RangeM[b] / 1000:0.0} km") : default, DesignerTheme.Bright);
        Fig("HITS", U8.W($"{(ci >= 0 ? c.Hits[ci] : 0)}"), ci >= 0 && c.BreachM2[ci] > 0 ? U8.W($"holes {c.BreachM2[ci]:0.0} m²") : default,
            ci >= 0 && c.Hits[ci] > 0 ? DesignerTheme.Amber : DesignerTheme.Bright);
        ImGui.EndTable();

        static void Fig(string label, ReadOnlySpan<byte> value, ReadOnlySpan<byte> sub, Vector4 colour)
        {
            ImGui.TableNextColumn();
            ImGui.PushFont(UiFonts.SansSmall);
            ImGui.TextDisabled(label);
            ImGui.PopFont();
            ImGui.PushFont(UiFonts.Mono);
            U8.Text(value, colour);
            ImGui.PopFont();
            ImGui.PushFont(UiFonts.SansSmall);
            U8.Text(sub, DesignerTheme.Muted);
            ImGui.PopFont();
        }
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
        var c = s.Conditions;
        var d = s.Details;
        float wAvail = ImGui.GetContentRegionAvail().X;
        Plan(s, k, cls, ci, di, wAvail);
        Profile(s, k, cls, ci, di, wAvail);
        if (ImGui.BeginTable("##cond", 2, ImGuiTableFlags.SizingFixedFit))
        {
            ImGui.TableSetupColumn("##k", ImGuiTableColumnFlags.WidthFixed, 96 * UiFonts.Scale);
            ImGui.TableSetupColumn("##v", ImGuiTableColumnFlags.WidthStretch);
            double water = ci >= 0 ? c.WaterT[ci] : 0, reserve = ci >= 0 ? c.ReserveT[ci] : 1;
            double afloat = reserve / Math.Max(reserve + water, 1e-6);
            Row("Flotation");
            Bar((float)afloat, afloat > 0.8 ? Ok : afloat > 0.6 ? DesignerTheme.Amber : Bad);
            ImGui.SameLine();
            U8.Text(U8.W($"{afloat * 100:0}% · {water:#,0} t aboard"));
            var a = ci >= 0 ? c.Attitude[ci] : default;
            Row("List · trim");
            U8.Text(U8.W($"{Math.Abs(a.HeelDeg):0.0}° {(a.HeelDeg >= 0 ? "stbd" : "port")} · {Math.Abs(a.TrimDeg):0.00}° by the {(a.TrimDeg >= 0 ? "bow" : "stern")}"));
            Row("Fires");
            if (ci >= 0 && c.Fires[ci] > 0)
                U8.Text(U8.W($"{c.Fires[ci]} burning"), Fire);
            else
                ImGui.TextDisabled("none");
            Row("Power · steering");
            var steer = ci >= 0 ? c.Steering[ci] : Steering.Free;
            U8.Text(U8.W($"{(ci >= 0 ? c.PowerCap[ci] * 100 : 100):0}% power · {SteerWord(steer)}"), steer == Steering.Free ? DesignerTheme.Bright : DesignerTheme.Amber);
            if (ci >= 0 && double.IsFinite(c.SafeSpeedKn[ci]))
            {
                Row("Safe speed");
                U8.Text(U8.W($"{c.SafeSpeedKn[ci]:0} kn (damage control asks)"), DesignerTheme.Amber);
            }
            if (di >= 0)
            {
                Row("Crew");
                U8.Text(U8.W($"{d.Killed[di]:0} killed, {d.Wounded[di]:0} wounded of {d.Complement[di]:#,0}"),
                    d.Killed[di] + d.Wounded[di] > 0 ? DesignerTheme.Amber : DesignerTheme.Bright);
            }
            ImGui.EndTable();
        }
    }

    static void Row(string key)
    {
        ImGui.TableNextRow();
        ImGui.TableNextColumn();
        ImGui.TextDisabled(key);
        ImGui.TableNextColumn();
    }

    static void Bar(float share, Vector4 colour, float w = 90)
    {
        float sc = UiFonts.Scale, h = 6 * sc;
        var p = ImGui.GetCursorScreenPos() + new Vector2(0, (ImGui.GetTextLineHeight() - h) / 2);
        var dl = ImGui.GetWindowDrawList();
        dl.AddRectFilled(p, p + new Vector2(w * sc, h), ImGui.GetColorU32(DesignerTheme.Inset), 1);
        dl.AddRectFilled(p, p + new Vector2(w * sc * Math.Clamp(share, 0, 1), h), ImGui.GetColorU32(colour), 1);
        ImGui.Dummy(new Vector2(w * sc, ImGui.GetTextLineHeight()));
    }

    static string SteerWord(Steering s) => s switch
    {
        Steering.Jammed => "rudder jammed",
        Steering.Lost => "steering lost",
        Steering.Hand => "steered by hand",
        Steering.Engines => "steered by engines",
        _ => "steering free",
    };

    /// <summary>Her plan, drawn from her class: the main deck's outline, her upper works, her mounts by state (lettered),
    /// her wrecked parts outlined, her fires and her holes at their cells.</summary>
    void Plan(Snapshot s, int k, ShipClass cls, int ci, int di, float wAvail)
    {
        float sc = UiFonts.Scale;
        var (x0, x1) = Extent(cls);
        double scale = (wAvail - 8 * sc) / Math.Max(x1 - x0, 1);
        float h = (float)Math.Max(cls.Beam * scale * 1.3, 40 * sc);
        var o = ImGui.GetCursorScreenPos();
        var dl = ImGui.GetWindowDrawList();
        float cy = o.Y + h / 2, left = o.X + 4 * sc;
        Vector2 P(double x, double y) => new(left + (float)((x - x0) * scale), cy + (float)(y * scale));
        // the deck
        var pts = cls.Points(cls.DeckOutline);
        if (pts.Length >= 3)
        {
            Span<Vector2> poly = stackalloc Vector2[Math.Min(pts.Length, 512)];
            for (int i = 0; i < poly.Length; i++)
                poly[i] = P(pts[i].X, pts[i].Y);
            dl.AddConvexPolyFilled(ref poly[0], poly.Length, ImGui.GetColorU32(new Vector4(0.2f, 0.25f, 0.29f, 1)));
            dl.AddPolyline(ref poly[0], poly.Length, ImGui.GetColorU32(new Vector4(0.55f, 0.62f, 0.68f, 1)), ImDrawFlags.Closed, 1);
        }
        else
            dl.AddRect(P(x0, -cls.Beam / 2), P(x1, cls.Beam / 2), ImGui.GetColorU32(DesignerTheme.Muted));
        // the upper works
        var comps = cls.Components;
        for (int i = 0; i < comps.Length; i++)
        {
            var kind = comps[i].Kind;
            if (kind is not (ComponentKind.Superstructure or ComponentKind.ConningTower or ComponentKind.Funnel))
                continue;
            var bx = comps[i].Bounds;
            uint col = ImGui.GetColorU32(kind == ComponentKind.Funnel ? new Vector4(0.12f, 0.14f, 0.16f, 1) : new Vector4(0.3f, 0.36f, 0.41f, 1));
            dl.AddRectFilled(P(bx.X0, bx.Y0), P(bx.X1, bx.Y1), col);
        }
        // wrecked parts (her own damaged parts: the components among them)
        var v = s.Ships;
        for (int i = 0; i < v.PartCount[k]; i++)
        {
            int part = v.Parts[v.PartStart[k] + i];
            var st = v.PartStates[v.PartStart[k] + i];
            if (part >= comps.Length || st is PartState.Ok or PartState.Damaged)
                continue;
            var bx = comps[part].Bounds;
            dl.AddRect(P(bx.X0, bx.Y0), P(bx.X1, bx.Y1), ImGui.GetColorU32(st == PartState.Destroyed ? Bad : DesignerTheme.Amber), 0, 0, 1.5f * sc);
        }
        // fires, at their cells
        var cells = cls.Cells;
        for (int i = 0; i < v.FireCount[k]; i++)
        {
            int cell = v.FireCells[v.FireStart[k] + i];
            if (cell < 0 || cell >= cells.Length)
                continue;
            var bx = cells[cell].Box;
            var at = P((bx.X0 + bx.X1) / 2, (bx.Y0 + bx.Y1) / 2);
            float pulse = 0.7f + 0.3f * MathF.Sin((float)ImGui.GetTime() * 5 + i);
            dl.AddCircleFilled(at, 6 * sc, ImGui.GetColorU32(Fire with { W = 0.35f * pulse }));
            dl.AddCircleFilled(at, 3 * sc, ImGui.GetColorU32(new Vector4(1f, 0.85f, 0.45f, pulse)));
        }
        // the mounts, ringed by state
        var mounts = cls.Mounts;
        var d = s.Details;
        for (int m = 0; m < mounts.Length; m++)
        {
            var bx = comps[mounts[m].Component].Bounds;
            float r = Math.Max((float)((bx.Y1 - bx.Y0) / 2 * scale), 2.5f * sc);
            var at = P(mounts[m].X, mounts[m].Y);
            var status = di >= 0 ? d.Mount[d.MountStart[di] + m] : MountStatus.Ready;
            uint col = ImGui.GetColorU32(MountColour(status));
            bool main = mounts[m].Kind == ComponentKind.Main;
            dl.AddCircle(at, r, col, 0, main ? 2 * sc : 1.2f * sc);
            if (status == MountStatus.Out)
            {
                dl.AddLine(at - new Vector2(r, r) * 0.7f, at + new Vector2(r, r) * 0.7f, col, 1.5f * sc);
                dl.AddLine(at + new Vector2(-r, r) * 0.7f, at + new Vector2(r, -r) * 0.7f, col, 1.5f * sc);
            }
            if (main)
            {
                var label = MountLabel(cls, m);
                var size = ImGui.CalcTextSize(label);
                dl.AddText(new Vector2(at.X - size.X / 2, o.Y - 1), col, label);
            }
        }
        // the holes to the sea
        for (int i = 0; di >= 0 && i < d.HoleCount[di]; i++)
        {
            int j = d.HoleStart[di] + i;
            var bx = cells[d.HoleCell[j]].Box;
            var at = P((bx.X0 + bx.X1) / 2, d.HoleY[j]);
            dl.AddCircleFilled(at, Math.Clamp((float)Math.Sqrt(d.HoleM2[j]) * 3 * sc, 2 * sc, 6 * sc), ImGui.GetColorU32(Bad));
        }
        ImGui.Dummy(new Vector2(wAvail, h + ImGui.GetTextLineHeight() * 0.4f));
    }

    /// <summary>Her profile, drawn from her cells (heights twice the lengths' scale, to read): each space outlined, the
    /// water in each to its share, the counterflooded in green, her holes, her upper works; her design's waterline
    /// dashed and the one she floats at now.</summary>
    void Profile(Snapshot s, int k, ShipClass cls, int ci, int di, float wAvail)
    {
        float sc = UiFonts.Scale;
        var (x0, x1) = Extent(cls);
        double scale = (wAvail - 8 * sc) / Math.Max(x1 - x0, 1), zs = scale * 2;
        var vert = cls.Vertical;
        double zLow = vert.Keel, zHigh = vert.Keel + vert.Depth;
        var comps = cls.Components;
        for (int i = 0; i < comps.Length; i++)
            if (Standing(comps[i], vert))
                zHigh = Math.Max(zHigh, comps[i].Bounds.Top);
        float h = (float)((zHigh - zLow) * zs) + 4 * sc;
        var o = ImGui.GetCursorScreenPos();
        var dl = ImGui.GetWindowDrawList();
        float left = o.X + 4 * sc, bottom = o.Y + h;
        Vector2 P(double x, double z) => new(left + (float)((x - x0) * scale), bottom - (float)((z - zLow) * zs));
        // the upper works, then the spaces
        for (int i = 0; i < comps.Length; i++)
            if (Standing(comps[i], vert))
            {
                var bx = comps[i].Bounds;
                dl.AddRectFilled(P(bx.X0, bx.Top), P(bx.X1, bx.Base), ImGui.GetColorU32(new Vector4(0.26f, 0.31f, 0.36f, 1)));
            }
        var cells = cls.Cells;
        uint edge = ImGui.GetColorU32(new Vector4(0.55f, 0.62f, 0.68f, 0.35f));
        dl.AddRectFilled(P(x0, vert.Keel + vert.Depth), P(x1, vert.Keel), ImGui.GetColorU32(new Vector4(0.17f, 0.21f, 0.25f, 1)));
        for (int i = 0; i < cells.Length; i++)
        {
            var bx = cells[i].Box;
            dl.AddRect(P(bx.X0, bx.Top), P(bx.X1, bx.Base), edge);
        }
        var d = s.Details;
        for (int i = 0; di >= 0 && i < d.FloodCount[di]; i++)
        {
            int j = d.FloodStart[di] + i;
            var bx = cells[d.FloodCell[j]].Box;
            double z = bx.Base + (bx.Top - bx.Base) * d.FloodShare[j];
            dl.AddRectFilled(P(bx.X0, z), P(bx.X1, bx.Base), ImGui.GetColorU32(d.FloodCounter[j] ? new Vector4(0.36f, 0.7f, 0.6f, 0.85f) : WaterTint with { W = 0.85f }));
        }
        for (int i = 0; di >= 0 && i < d.HoleCount[di]; i++)
        {
            int j = d.HoleStart[di] + i;
            var bx = cells[d.HoleCell[j]].Box;
            dl.AddCircleFilled(P((bx.X0 + bx.X1) / 2, d.HoleZ[j]), Math.Clamp((float)Math.Sqrt(d.HoleM2[j]) * 3 * sc, 2 * sc, 6 * sc), ImGui.GetColorU32(Bad));
        }
        // the waterlines: the design's, dashed; now: settled, trimmed (down by the bow: the water higher forward)
        double wl = vert.Waterline;
        for (double x = x0; x < x1; x += 6)
            dl.AddLine(P(x, wl), P(Math.Min(x + 3, x1), wl), ImGui.GetColorU32(new Vector4(0.7f, 0.85f, 1f, 0.5f)), 1);
        if (ci >= 0)
        {
            var a = s.Conditions.Attitude[ci];
            double lcf = cls.Hydrostatics.Lcf, t = Math.Tan(a.TrimDeg * Math.PI / 180);
            dl.AddLine(P(x0, wl + a.SinkageM + (x0 - lcf) * t), P(x1, wl + a.SinkageM + (x1 - lcf) * t), ImGui.GetColorU32(WaterTint), 1.6f * sc);
        }
        ImGui.Dummy(new Vector2(wAvail, h + 4 * sc));
    }

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

    static Vector4 MountColour(MountStatus s) => s switch
    {
        MountStatus.Ready => Ok,
        MountStatus.Loading => OkDim,
        MountStatus.Jammed or MountStatus.Down => DesignerTheme.Amber,
        _ => Bad,
    };

    // the mounts' targetLabels by class, made once a class
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

    // ------------------------------------------------------------------ guns and fire control

    void Guns(Snapshot s, int e, ShipClass cls, int di)
    {
        var d = s.Details;
        long now = s.Time.Ms;
        if (!ImGui.BeginTable("##guns", 3, ImGuiTableFlags.SizingFixedFit | ImGuiTableFlags.RowBg))
            return;
        ImGui.TableSetupColumn("Mount", ImGuiTableColumnFlags.WidthFixed, 70 * UiFonts.Scale);
        ImGui.TableSetupColumn("State", ImGuiTableColumnFlags.WidthStretch);
        ImGui.TableSetupColumn("AP · com · HE", ImGuiTableColumnFlags.WidthFixed, 110 * UiFonts.Scale);
        ImGui.PushFont(UiFonts.SansSmall);
        ImGui.TableHeadersRow();
        ImGui.PopFont();
        for (int b = 0; b < cls.Batteries.Length; b++)
        {
            var bat = cls.Batteries[b];
            ImGui.TableNextRow();
            ImGui.TableNextColumn();
            int row = BatteryRow(s, e, b);
            ImGui.TextDisabled(bat.Kind == Battery.Main ? "main" : "secondary");
            ImGui.TableNextColumn();
            string label = BatteryLabel(cls, b);
            U8.Text(row >= 0 ? U8.W($"{label} on {LabelOf(s, s.Batteries.Target[row])}") : U8.W($"{label}: no target"), DesignerTheme.Muted);
            ImGui.TableNextColumn();
            // its shell (docs/battle/shell-choice.md A5): the kind it loads, and the round in its guns while that changes
            if (s.Ships.IndexOf(e) is int k and >= 0 && s.Ships.BatteryStart[k] >= 0)
            {
                int o = s.Ships.BatteryOrdered[s.Ships.BatteryStart[k] + b], g = s.Ships.BatteryInGuns[s.Ships.BatteryStart[k] + b];
                ImGui.PushFont(UiFonts.SansSmall);
                if (o == 0)
                    U8.Right(g > 0 ? U8.W($"{KindName((ShellKind)(g - 1))}, magazine order") : "none left"u8, DesignerTheme.Muted);
                else if (g == o || g == 0)
                    U8.Right(U8.W($"loads {KindName((ShellKind)(o - 1))}"), s.Ships.ShellHeld[k] > 0 && bat.Chief ? DesignerTheme.Brass : DesignerTheme.Bright);
                else
                    U8.Right(U8.W($"{KindName((ShellKind)(g - 1))} in, then {KindName((ShellKind)(o - 1))}"), DesignerTheme.Amber);
                ImGui.PopFont();
            }
            foreach (int m in ForeToAft(cls, b))
            {
                ImGui.TableNextRow();
                ImGui.TableNextColumn();
                ImGui.TextUnformatted(MountLabel(cls, m));
                ImGui.TableNextColumn();
                if (di < 0)
                {
                    ImGui.TextDisabled("-");
                    ImGui.TableNextColumn();
                    continue;
                }
                int j = d.MountStart[di] + m;
                var st = d.Mount[j];
                long until = d.MountUntilMs[j];
                var text = st switch
                {
                    MountStatus.Ready => "ready"u8,
                    MountStatus.Loading => U8.W($"loading {Math.Max(0, (until - now) / 1000):0} s"),
                    MountStatus.Jammed => until == long.MaxValue || until <= now ? "jammed in train"u8 : U8.W($"jammed, ~{Math.Ceiling((until - now) / 60_000.0):0} min"),
                    MountStatus.Down => until > now ? U8.W($"down, ~{Math.Ceiling((until - now) / 1000.0):0} s") : "down"u8,
                    _ => "out of action"u8,
                };
                U8.Text(text, MountColour(st));
                if (d.MountGunsOut[j] > 0)
                {
                    ImGui.SameLine();
                    U8.Text(U8.W($"· {d.MountGunsOut[j]} gun out"), DesignerTheme.Amber);
                }
                ImGui.TableNextColumn();
                ImGui.PushFont(UiFonts.Mono);
                U8.Right(U8.W($"{Rounds(d.MountAp[j])} · {Rounds(d.MountCommon[j])} · {Rounds(d.MountHe[j])}"), st == MountStatus.Out ? DesignerTheme.Muted : DesignerTheme.Bright);
                ImGui.PopFont();
            }
        }
        ImGui.EndTable();
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

    void FireControl(Snapshot s, int e, ShipClass cls)
    {
        var b = s.Batteries;
        bool any = false;
        for (int bat = 0; bat < cls.Batteries.Length; bat++)
        {
            int i = BatteryRow(s, e, bat);
            if (i < 0)
                continue;
            any = true;
            var row = cls.Batteries[bat];
            U8.Text(U8.W($"{BatteryLabel(cls, bat)} {(row.Kind == Battery.Main ? "main" : "secondary")} on {LabelOf(s, b.Target[i])}"), DesignerTheme.Bright);
            ImGui.PushFont(UiFonts.Mono);
            U8.Text(b.RangeM[i] > 0 ? U8.W($"{b.RangeM[i]:#,0} m · plot ±{b.SigmaM[i]:0} m · {(b.Sees[i] ? "in sight" : "not seen")}") : U8.W($"{StateText(b.State[i], b.Phase[i])}"),
                b.Sees[i] ? DesignerTheme.Text : DesignerTheme.Amber);
            ImGui.PopFont();
            U8.Text(b.Local[i] ? "local control"u8 : U8.W($"director, level {b.Level[i]} ({FireControlPresets.Name(b.Level[i])})"), DesignerTheme.Muted);
            Phases(b.State[i] == BatteryState.Firing ? b.Phase[i] : (FirePhase)255);
            ImGui.PushID(bat);
            if (ImGui.BeginTable("##fc", 4, ImGuiTableFlags.BordersInnerV | ImGuiTableFlags.SizingStretchSame))
            {
                Num("SALVOS", b.Salvos[i]);
                Num("STRADDLES", b.Straddles[i]);
                Num("HITS", b.Hits[i]);
                Num("IN THE AIR", b.InFlight[i]);
                ImGui.EndTable();
            }
            ImGui.PopID();
            ImGui.Spacing();
        }
        if (!any)
            ImGui.TextDisabled("not engaged");

        static void Num(string label, int n)
        {
            ImGui.TableNextColumn();
            ImGui.PushFont(UiFonts.SansSmall);
            ImGui.TextDisabled(label);
            ImGui.PopFont();
            ImGui.PushFont(UiFonts.Mono);
            U8.Text(U8.W($"{n}"), DesignerTheme.Bright);
            ImGui.PopFont();
        }
    }

    static readonly FirePhase[] PhaseOrder = [FirePhase.Opening, FirePhase.Ranging, FirePhase.Effect];

    /// <summary>Her fire discipline's phases, the one she's in lit (none: not firing).</summary>
    static void Phases(FirePhase now)
    {
        float sc = UiFonts.Scale, w = (ImGui.GetContentRegionAvail().X - 4 * sc) / 3, h = 18 * sc;
        var dl = ImGui.GetWindowDrawList();
        var p = ImGui.GetCursorScreenPos();
        var phases = PhaseOrder;
        for (int i = 0; i < 3; i++)
        {
            var a = p + new Vector2(i * (w + 2 * sc), 0);
            bool on = phases[i] == now, done = now != (FirePhase)255 && phases[i] < now;
            dl.AddRectFilled(a, a + new Vector2(w, h), ImGui.GetColorU32(on ? new Vector4(0.17f, 0.23f, 0.12f, 1) : done ? new Vector4(0.11f, 0.16f, 0.11f, 1) : DesignerTheme.Inset), 1);
            if (on)
                dl.AddLine(a + new Vector2(0, h - 1), a + new Vector2(w, h - 1), ImGui.GetColorU32(Ok), 2 * sc);
            var text = i == 0 ? "Opening"u8 : i == 1 ? "Ranging"u8 : "Fire for effect"u8;
            var size = U8.Size(text);
            U8.Draw(dl, a + new Vector2((w - size.X) / 2, (h - size.Y) / 2), ImGui.GetColorU32(on ? Ok : done ? OkDim : DesignerTheme.Muted), text);
        }
        ImGui.Dummy(new Vector2(3 * w, h + 2 * sc));
    }

    // ------------------------------------------------------------------ damage control, hits, torpedoes

    void DamageControl(Snapshot s, ShipClass cls, int ci, int di)
    {
        var d = s.Details;
        var c = s.Conditions;
        if (di < 0)
        {
            ImGui.TextDisabled("-");
            return;
        }
        if (d.PartyCount[di] == 0)
            ImGui.TextDisabled("no parties at work");
        // the parties standing by in one line; each at work in its own
        int idle = 0;
        double idleMen = 0;
        for (int i = 0; i < d.PartyCount[di]; i++)
            if (d.PartyFire[d.PartyStart[di] + i] < 0 && d.PartyJob[d.PartyStart[di] + i] == RepairJob.None)
                (idle, idleMen) = (idle + 1, idleMen + d.PartyMen[d.PartyStart[di] + i]);
        if (idle > 0)
            U8.Text(U8.W($"{idle} {(idle == 1 ? "party" : "parties")} standing by, {idleMen:0} men"), DesignerTheme.Muted);
        for (int i = 0; i < d.PartyCount[di]; i++)
        {
            int j = d.PartyStart[di] + i;
            if (d.PartyFire[j] < 0 && d.PartyJob[j] == RepairJob.None)
                continue;
            var job = d.PartyFire[j] >= 0 ? "fighting a fire"u8 : JobWord(d.PartyJob[j]);
            U8.Text(U8.W($"{d.PartyMen[j]:0} men"), DesignerTheme.Muted);
            ImGui.SameLine(60 * UiFonts.Scale);
            U8.Text(job, d.PartyFire[j] >= 0 ? Fire : d.PartyJob[j] == RepairJob.None ? DesignerTheme.Muted : DesignerTheme.Bright);
            if (d.PartyJob[j] != RepairJob.None && d.PartyDone[j] > 0)
            {
                ImGui.SameLine();
                U8.Text(U8.W($"· {d.PartyDone[j]:0} man-min done"), DesignerTheme.Muted);
            }
        }
        ImGui.Spacing();
        U8.Text(U8.W($"Pumps rigged: {d.PumpsRigged[di]} · spaces with water: {d.FloodCount[di]} · holes: {d.HoleCount[di]}"));
        if (ci >= 0 && (c.CounterfloodPortT[ci] > 0 || c.CounterfloodStarboardT[ci] > 0))
            U8.Text(U8.W($"Counterflooding left: {c.CounterfloodPortT[ci]:0} t port, {c.CounterfloodStarboardT[ci]:0} t stbd"), DesignerTheme.Muted);
        for (int i = 0; i < d.MagazineCount[di]; i++)
            U8.Text(U8.W($"Magazine flooded: {cls.RoomId(d.MagazineRoom[d.MagazineStart[di] + i])}"), WaterTint);
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

    // her hits' lines, made when a hit comes
    int hitsFor = -1, hitsKnown = -1;
    readonly List<string> hitLines = [];

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
                hitLines.Add(x.Kind == CombatEvent.Lost
                    ? $"{when}  {(LossKind)x.Count switch { LossKind.Magazine => "magazine exploded", LossKind.Capsized => "capsized", LossKind.BrokeInTwo => "broke in two", _ => "foundered" }}"
                    : $"{when}  {HitText(x, cls)}");
            }
        }
        if (hitLines.Count == 0)
            ImGui.TextDisabled("none");
        ImGui.PushTextWrapPos(0);
        foreach (var line in hitLines)
            ImGui.TextUnformatted(line);
        ImGui.PopTextWrapPos();
        if (hits.Count > hitLines.Count)
            U8.Text(U8.W($"and {hits.Count - hitLines.Count} more"), DesignerTheme.Muted);
    }

    void TorpedoSection(Snapshot s, int k, ShipClass cls)
    {
        var t = cls.Torpedoes[cls.Launchers[0].Torpedo];
        U8.Text(U8.W($"{s.Ships.TorpedoesLoaded[k]} loaded, {s.Ships.TorpedoesLeft[k]} left · {t.CalibreMm:0} mm"));
        U8.Text(U8.W($"{t.FastKn:0} kn to {t.FastRangeM / 1000:0.0} km, or {t.SlowKn:0} kn to {t.SlowRangeM / 1000:0.0} km"), DesignerTheme.Muted);
    }
}
