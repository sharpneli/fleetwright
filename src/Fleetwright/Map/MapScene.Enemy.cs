using System.Numerics;
using Fleetwright.Designer;
using Fleetwright.Sim;
using Fleetwright.Sim.Ai;
using Fleetwright.Sim.Combat;
using Fleetwright.Sim.Core;
using Fleetwright.Sim.Ships;
using ImGuiNET;
using Fleetwright.Ui;
using static Fleetwright.Ui.Pieces;
using T = Fleetwright.Ui.Theme;

namespace Fleetwright.Map;

/// <summary>The enemy's card and strip, and a unit's card (docs/map-ui/battle-hud.md U4). An enemy is shown only as the
/// player holds her (her contact: identity, estimated course and speed, what's seen of her, the book's figures once her
/// class is made out), with the player's fire on her and hers, and the orders against her for the ship the player last
/// chose and her unit. A unit's card: its ships in a table, its formation and task, whose it is.</summary>
public sealed partial class MapScene
{
    void EnemyCard(Snapshot s, int e)
    {
        var c = s.Contacts;
        int i = ContactIndex(c, e);
        if (i < 0)
            return;
        var cls = c.Level[i] >= ContactLevel.Class ? world.Classes.TryGet(c.TakenFor[i]) : null;
        var sub = c.Level[i] switch
        {
            ContactLevel.Name => cls != null ? U8.W($"{cls.Name}: identified") : "identified"u8,
            ContactLevel.Class => "her class made out by her silhouette, not her name"u8,
            ContactLevel.Type => U8.W($"a {TypeWord(c.Type[i])}: her class not made out"),
            ContactLevel.Ship => "a ship, not made out"u8,
            ContactLevel.Light => "a light in the dark: a gun's flash or a funnel's glow"u8,
            _ => "smoke on the horizon"u8,
        };
        var seen = new WorldTime(c.SeenMs[i]).ToUtc();
        Head(U8.W($"{Identity(c, i)}"), T.Hostile, sub, "enemy"u8, ChipKind.Hostile,
            c.InSight[i] ? "in sight"u8 : U8.W($"last seen {seen.Hour:00}:{seen.Minute:00}"), ChipKind.Plain,
            c.SmokeInSight[i] ? "her smoke"u8 : c.LightInSight[i] ? "her lights"u8 : default, ChipKind.Plain);

        // range and bearing from the player's ship; her course and speed as estimated
        int from = s.Ships.IndexOf(lastOwn);
        var at = c.Position[i];
        var row = new Vector2(cardX, cardY);
        float x = row.X, unit = cardW / 4;
        if (from >= 0)
        {
            var p = s.Ships.PoseAt(from, world.DrawMs).Position;
            FigureCell(row, ref x, unit, "RANGE", U8.W($"{Earth.Distance(p, at) / 1000:0.0} km"), ""u8, T.Bright);
            FigureCell(row, ref x, unit, "BEARING", U8.W($"{(Earth.Bearing(p, at) % 360 + 360) % 360:000}°"), ""u8, T.Bright);
        }
        else
        {
            FigureCell(row, ref x, unit, "RANGE", "-"u8, ""u8, T.Bright);
            FigureCell(row, ref x, unit, "BEARING", "-"u8, ""u8, T.Bright);
        }
        double ms = c.Velocity[i].Length;
        FigureCell(row, ref x, unit, "COURSE", ms > 0.5 ? U8.W($"~{(Earth.Heading(at, c.Velocity[i]) % 360 + 360) % 360:000}°") : "-"u8, U8.W($"±{c.MajorM[i]:0} m"), T.Bright);
        FigureCell(row, ref x, cardX + cardW - x, "SPEED", U8.W($"~{ms / Handling.MsPerKnot:0} kn"), "est."u8, T.Bright, last: true);
        EndFigures();

        float right = cardX + cardW - P(6);
        if (Strip("WHAT WE SEE", "a lookout's", ""u8,
                "Only what a lookout could see: fires, a list, low in the water, stopped, her guns silent. Her hits and flooding stay hers."))
        {
            if (c.Seen[i] == SeenFlags.None)
                NoneRow(c.InSight[i] ? "no damage seen"u8 : "nothing: she's out of sight"u8);
            else
            {
                int n = 0;
                var r = CardRow(ref n);
                Words(r, r.X + P(6), right, T.Amber, U8.W($"{SeenWords(c.Seen[i])}"));
            }
        }
        if (cls != null && Strip("FROM THE BOOK", "not what she has left", U8.W($"{cls.Name}")))
        {
            int n = 0;
            for (int b = 0; b < cls.Batteries.Length; b++)
            {
                var r = CardRow(ref n);
                Words(r, r.X + P(6), right, T.Text, U8.W($"{BatteryLabel(cls, b)} {(cls.Batteries[b].Kind == Battery.Main ? "main" : "secondary")}"));
            }
            var q = CardRow(ref n);
            Words(q, q.X + P(6), right, T.Text, U8.W($"{cls.Motion.MaxSpeedKn:0.0} kn · {cls.Length:0} m long"));
            if (cls.Launchers.Length > 0)
            {
                q = CardRow(ref n);
                Words(q, q.X + P(6), right, T.Text, U8.W($"torpedo tubes: {cls.Launchers.Length}"));
            }
        }
        if (Strip("OUR FIRE ON HER", null, ""u8))
        {
            bool any = false;
            var b = s.Batteries;
            int n = 0;
            for (int j = 0; j < b.Count; j++)
                if (b.Target[j] == e)
                {
                    any = true;
                    var r = CardRow(ref n);
                    float fx = right;
                    fx = Figure(r, fx, T.Bright, U8.W($"{b.Hits[j]}"));
                    fx = Figure(r, fx - P(3), T.Muted, "hits"u8, UiFonts.SansSmall);
                    fx = Figure(r, fx - P(6), T.Bright, U8.W($"{b.Straddles[j]}"));
                    fx = Figure(r, fx - P(3), T.Muted, "str"u8, UiFonts.SansSmall);
                    fx = Figure(r, fx - P(6), T.Bright, U8.W($"{b.Salvos[j]}"));
                    fx = Figure(r, fx - P(3), T.Muted, "salvos"u8, UiFonts.SansSmall);
                    float wx = Words(r, r.X + P(6), fx - P(8), T.Bright, U8.W($"{ShipName(s, b.Shooter[j])}"));
                    Words(r, wx + P(6), fx - P(8), T.Muted, U8.W($"{StateText(b.State[j], b.Phase[j])}"));
                }
            if (!any)
                NoneRow("none of ours on her"u8);
        }
        if (Strip("HER FIRE", null, ""u8))
        {
            int on = HerTarget(e);
            if (on > 0)
            {
                int n = 0;
                var r = CardRow(ref n);
                float wx = Words(r, r.X + P(6), right, T.Muted, "on"u8);
                Words(r, wx + P(4), right, T.Bright, LabelOf(s, on));
            }
            else
                NoneRow("not seen firing at ours"u8);
        }
    }

    /// <summary>Whom an enemy last fired at among the player's ships (the feed's newest salvo of hers), or 0.</summary>
    int HerTarget(int e)
    {
        var lines = feed.Lines;
        for (int i = lines.Count - 1, n = 0; i >= 0 && n < 2000; i--, n++)
            if (lines[i].Shooter == e && lines[i].Kind == CombatEvent.Fired)
                return lines[i].Target;
        return 0;
    }

    // the orders' button labels, made once per name
    readonly Dictionary<(string, string), string> orderLabels = [];

    string U8Label(string verb, string name) =>
        orderLabels.TryGetValue((verb, name), out var l) ? l : orderLabels[(verb, name)] = verb + name;

    // ------------------------------------------------------------------ a unit's card

    void SquadronCard(Snapshot s, int g)
    {
        var gv = s.Groups;
        var v = s.Ships;
        bool action = false;
        for (int i = 0; i < gv.MemberCount[g] && !gv.Force[g]; i++)
            action |= v.IndexOf(gv.Members[gv.MemberStart[g] + i]) is int k and >= 0 && v.Encounter[k] != 0;
        int top = TopOf(gv, g);
        var whose = gv.Commanded[top] == 1 ? "AI commander"u8 : gv.Commanded[top] == 2 ? "yours"u8 : "your orders"u8;
        var whoseKind = gv.Commanded[top] == 0 ? ChipKind.Amber : ChipKind.Plain;
        var task = gv.Task[g] != 0 ? U8.W($"{TaskText(gv.Task[g], gv.TaskStatus[g])}") : default;
        var sub = U8.W($"{GroupFormationNames[(int)gv.Formation[g]]}, {gv.SpacingM[g]:0} m · guide {(gv.GuideShip[g] != 0 ? ShipName(s, gv.GuideShip[g]) : "-")}");
        if (action)
            Head(U8.W($"{gv.Name[g]}"), T.Bright, sub, "in action"u8, ChipKind.Amber, whose, whoseKind, task, ChipKind.Plain);
        else
            Head(U8.W($"{gv.Name[g]}"), T.Bright, sub, whose, whoseKind, task, ChipKind.Plain);
        float right = cardX + cardW - P(6);
        if (Strip(gv.Force[g] ? "UNITS" : "SHIPS", null, U8.W($"{gv.MemberCount[g]}")))
        {
            int n = 0;
            if (gv.Force[g])
                for (int i = 0; i < gv.MemberCount[g]; i++)
                {
                    if (gv.IndexOfGroup(gv.Members[gv.MemberStart[g] + i]) is int sub2 and >= 0)
                    {
                        var r = CardRow(ref n);
                        Words(r, r.X + P(6), right - P(60), T.Bright, U8.W($"{gv.Name[sub2]}"));
                        Figure(r, right, T.Muted, U8.W($"{gv.MemberCount[sub2]} ships"), UiFonts.SansSmall);
                    }
                }
            else
            {
                // a row a ship: afloat, hits, mounts working, speed; a click selects her
                var a = new Vector2(cardX, cardY);
                float cHits = right - P(110), cGuns = right - P(36), cKn = right, bar = right - P(186);
                cdl.Head(a, cardW);
                cdl.HeadCell(a, a.X + P(6), "Ship");
                cdl.HeadCell(a, bar, "Afloat");
                cdl.HeadCell(a, cHits, "Hits", right: true);
                cdl.HeadCell(a, cGuns, "Guns", right: true);
                cdl.HeadCell(a, cKn, "kn", right: true);
                cardY += HeadH;
                for (int i = 0; i < gv.MemberCount[g]; i++)
                {
                    int e = gv.Members[gv.MemberStart[g] + i];
                    int k = v.IndexOf(e);
                    if (k < 0)
                        continue;
                    int ci = s.Conditions.IndexOf(e);
                    var r = new Vector2(cardX, cardY);
                    ImGui.PushID(e);
                    var (click, hover) = HitRow("##ship", r, new Vector2(cardW, RowH));
                    ImGui.PopID();
                    if (click)
                    {
                        selected = e;
                        squadronCardOf = -1;
                    }
                    cdl.RowBg(r, cardW, RowH, n++, hover: hover);
                    cardY += RowH;
                    Words(r, r.X + P(6), bar - P(8), T.Bright, U8.W($"{ShipName(s, e)}"));
                    double water = ci >= 0 ? s.Conditions.WaterT[ci] : 0, reserve = ci >= 0 ? s.Conditions.ReserveT[ci] : 1;
                    float afloat = (float)(reserve / Math.Max(reserve + water, 1e-6));
                    cdl.Bar(new Vector2(bar, r.Y + P(7)), new Vector2(P(44), P(6)), afloat, afloat > 0.8f ? Ok : afloat > 0.6f ? T.Amber : Bad);
                    int hits = ci >= 0 ? s.Conditions.Hits[ci] : 0;
                    Figure(r, cHits, hits > 0 ? T.Amber : T.Bright, U8.W($"{hits}"));
                    Figure(r, cGuns, T.Bright, ci >= 0 ? U8.W($"{s.Conditions.MountsWorking[ci]}/{s.Conditions.Mounts[ci]}") : "all"u8);
                    Figure(r, cKn, T.Bright, U8.W($"{v.Speed[k] / Handling.MsPerKnot:0}"));
                }
            }
        }
        if (Strip("ORDERS", null, ""u8))
        {
            var at = new Vector2(cardX + P(6), cardY + P(5));
            var unit = Unit.Group(gv.Id[g]);
            string form = GroupFormationNames[(int)gv.Formation[g]];
            if (cdl.Button("##cardform", at, form, menu: true))
                ImGui.OpenPopup("##cardform");
            at.X += ButtonWidth(form, null, menu: true) + P(3);
            if (ImGui.BeginPopup("##cardform"))
            {
                FormationEdit(gv, g, ref spacingEdit, "##cf");
                ImGui.EndPopup();
            }
            if (cdl.Button("##cardfight", at, "Fight"))
                Behaviour(3, unit, 0);
            at.X += ButtonWidth("Fight", null) + P(3);
            if (cdl.Button("##cardwithdraw", at, "Withdraw"))
                world.Send(world.Orders.WithdrawHome(unit));
            at.X += ButtonWidth("Withdraw", null) + P(3);
            if (gv.Parent[g] == 0)
            {
                bool his = gv.Commanded[g] == 1;
                if (cdl.Button("##cardai", at, "AI", lit: his ? Lit.On : Lit.None))
                    world.Send(world.Orders.Automate(unit, !his));
                if (ImGui.IsItemHovered())
                    Tip(his ? "the side's commander fights it: click to take it" : "click to leave it to the side's commander");
            }
            cardY = at.Y + P(20) + P(5);
        }
    }
}
