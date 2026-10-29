using System.Globalization;
using System.Numerics;
using Fleetwright.Designer;
using Fleetwright.Sim;
using Fleetwright.Sim.Ai;
using Fleetwright.Sim.Ai.Captains;
using Fleetwright.Sim.Combat;
using Fleetwright.Sim.Core;
using Fleetwright.Sim.Ships;
using ImGuiNET;
using SDL;
using static SDL.SDL3;
using Fleetwright.Ui;
using static Fleetwright.Ui.Pieces;
using T = Fleetwright.Ui.Theme;

namespace Fleetwright.Map;

/// <summary>The orders strip (docs/map-ui/strip.md; battle-hud.md U2): a row of docked panes along the bottom, under the
/// rail, while one of the player's ships is selected: who the orders go to (the ship or a unit she's in), the helm, the
/// guns, the task and a unit's formation; an enemy's own two panes while she's selected. Every order is a button with
/// its key, both read from one table (<see cref="Def"/>), so a key can't drift from its button. And the cursor's verb:
/// what a right-click does where it is (steer, engage, follow), and the armed picks ("Engage…", "Fire at…", "Follow…",
/// "Run from…") a click completes.</summary>
public sealed partial class MapScene
{
    /// <summary>What the next click on the map does, armed from the strip or the task menu (the dev's salvo is
    /// <see cref="fireArmed"/>): on a ship of ours, an enemy, or the sea (docs/battle/admirals-orders.md O9).</summary>
    enum ClickPick : byte
    {
        None, Engage, Torpedoes, Follow, Run,
        Screen, Scout, Cover, Join, Station, Fight, Intercept, Shadow, Attack, GoTo, Patrol, Search, Deploy, DivideFrom,
    }

    ClickPick armedPick;
    long lastClickMs;
    int lastClickShip = -1;

    /// <summary>The strip's orders, each a button and a key (<see cref="Def"/>).</summary>
    enum Act : byte
    {
        LevelUp, LevelDown, BackIntoLine,
        Port90, Port10, Stbd10, Stbd90, Course, Slower, Faster, HardOver, Conn, ClearRoute, Smoke,
        AtWill, Engage, HoldFire, Shells, Torpedoes,
        Fight, Follow, Hold, Tasks, Withdraw, Auto,
        LineAhead, Columns, Abreast, Turn, Deploy, DeployPort, DeployStbd, Formation,
        EngageShip, EngageUnit, TorpedoesAt,   // an enemy selected: the ship alone first, Alt+E before E
        Count,
    }

    /// <summary>An order's key (with shift, or only with alt), its chip, its button's label and its tooltip. Key 0 and
    /// no chip: a button alone (Withdraw: keys are for what's used often, docs/map-ui/input.md). W, A, S and D are the
    /// camera's.</summary>
    readonly record struct ActDef(uint Key, bool Shift, bool Alt, string? Chip, string Label, string Tip);

    static ActDef Def(Act a) => a switch
    {
        Act.LevelUp => new(SDLK_Q, false, false, "Q", "", "orders a level up: her squadron, then the force over it"),
        Act.LevelDown => new(SDLK_Q, true, false, "Shift+Q", "", "orders a level down, toward the ship"),
        Act.BackIntoLine => new(SDLK_B, true, false, "Shift+B", "Back into line", "order her back to her squadron: her captain stays, at his ship's risk"),
        Act.Port90 => new(SDLK_LEFT, true, false, "Shift+←", "−90°", "the course 90° to port"),
        Act.Port10 => new(SDLK_LEFT, false, false, "←", "−10°", "the course 10° to port"),
        Act.Stbd10 => new(SDLK_RIGHT, false, false, "→", "+10°", "the course 10° to starboard"),
        Act.Stbd90 => new(SDLK_RIGHT, true, false, "Shift+→", "+90°", "the course 90° to starboard"),
        Act.Course => new(SDLK_RETURN, false, false, "Enter", "", "the course ordered: Enter, type one, Enter (Esc leaves it)"),
        Act.Slower => new(SDLK_DOWN, false, false, "↓", "", "the telegraph a bell slower"),
        Act.Faster => new(SDLK_UP, false, false, "↑", "", "the telegraph a bell faster"),
        Act.HardOver => new(SDLK_O, false, false, "O", "Hard over", "the course orders put the rudder hard over"),
        Act.Conn => new(SDLK_C, false, false, "C", "Conn", "take the conn: nothing but your helm orders moves her: no combing torpedo tracks (the lookouts call them), no turning away, no falling out; a task or a behaviour gives it back"),
        Act.ClearRoute => new(SDLK_DELETE, false, false, "Del", "Clear route", "the waypoints dropped: she steers the course"),
        Act.Smoke => new(SDLK_M, false, false, "M", "Make smoke", "her funnels burn rich: a screen (every ship of her squadron when the orders go to a unit)"),
        Act.AtWill => new(SDLK_G, false, false, "G", "Fire at will", "each battery at the enemy it judges best"),
        Act.Engage => new(SDLK_E, false, false, "E", "Engage…", "then right-click an enemy (or right-click her without this: engage is the default)"),
        Act.HoldFire => new(SDLK_X, false, false, "X", "Hold fire", "no gun fires"),
        Act.Shells => new(SDLK_R, false, false, "R", "", "the main guns' shell, for the selection: by target (her captain judges what a round does to the target as she's made out), or a kind held until they're out of it; the round in the gun goes first"),
        Act.Torpedoes => new(SDLK_E, true, false, "Shift+E", "Torpedoes at…", "then right-click an enemy"),
        Act.Fight => new(SDLK_B, false, false, "B", "Fight", "the nearest enemy in sight; turns away when outgunned (Fight… in the task menu: one you pick)"),
        Act.Follow => new(SDLK_F, false, false, "F", "Follow…", "station astern of her: then right-click a ship"),
        Act.Hold => new(SDLK_H, false, false, "H", "Hold", "hold the course and speed ordered"),
        Act.Tasks => new(SDLK_T, false, false, "T", "Task", "every order, by what it's about, and what the unit is for"),
        Act.Withdraw => new(0, false, false, null, "Withdraw", "break off and go home, away from any enemy first"),
        Act.Auto => new(SDLK_I, false, false, "I", "AI", "the side's commander fights it: any order of yours takes it back"),
        Act.LineAhead => new(SDLK_L, false, false, "L", "Line ahead", "line ahead; a force: one line, every squadron in it, on the course it steers (on the same course the far columns drop back a column's length each, so a fleet takes long: deploying turns it into line quicker)"),
        Act.Columns => new(SDLK_K, false, false, "K", "Columns", "the cruising order: every squadron a column in line ahead, the columns abreast as far apart as one is long, ready to deploy"),
        Act.Abreast => new(SDLK_L, true, false, "Shift+L", "Abreast", "line abreast; a force: every ship abreast, each squadron at its own spacing"),
        Act.Turn => new(SDLK_U, false, false, "U", "Turn", "turn together: every ship at once; the line becomes a line of bearing on its old axis. Or the battle turn, about"),
        Act.Deploy => new(SDLK_N, false, false, "N", "Deploy…", "into line of battle: right-click where the line should steer, or the enemy (across his course)"),
        Act.DeployPort => new(SDLK_COMMA, false, false, ",", "to port", "deploy 8 points to port: one line, the port wing column leading round, the rest in its wake"),
        Act.DeployStbd => new(SDLK_PERIOD, false, false, ".", "to stbd", "deploy 8 points to starboard: one line, the starboard wing column leading round, the rest in its wake"),
        Act.Formation => new(SDLK_Z, false, false, "Z", "", "this level's formation and spacing: echelons and lines of bearing"),
        Act.EngageShip => new(SDLK_E, false, true, "Alt+E", "", "engage her with the ship you had alone"),
        Act.EngageUnit => new(SDLK_E, false, false, "E", "", "engage her with the ship's squadron: its admiral spreads the fire"),
        Act.TorpedoesAt => new(SDLK_E, true, false, "Shift+E", "Torpedoes at her", "fire what's loaded and bears"),
        _ => default,
    };

    /// <summary>What the strip's orders work on this frame: the selection's row and the row of the ship that steams the
    /// orders, the unit they go to and its level (0 the ship; else the chain's group <see cref="G"/>), her class; or an
    /// enemy's contact and the ship of ours the orders against her come from.</summary>
    struct Orders
    {
        public Snapshot S;
        public int K, Kc, Level, G;
        public Unit Unit;
        public ShipClass? Cls;
        public bool Enemy;
        public int Contact, From, FromGroup;
    }

    bool OrdersNow(Snapshot s, out Orders c)
    {
        c = default;
        c.S = s;
        int k = s.Ships.IndexOf(selected);
        if (k >= 0)
        {
            var (unit, cmd) = Commanded(s);
            c.K = k;
            c.Kc = s.Ships.IndexOf(cmd) is int kc and >= 0 ? kc : k;
            c.Level = Level(s);
            c.G = c.Level > 0 ? chain[c.Level - 1] : -1;
            c.Unit = unit;
            c.Cls = world.Classes.TryGet(s.Ships.Class[k]);
            return true;
        }
        if (selected < 0 || ContactIndex(s.Contacts, selected) is not (int ci and >= 0))
            return false;
        c.Enemy = true;
        c.Contact = ci;
        c.From = s.Ships.IndexOf(lastOwn);
        c.FromGroup = c.From >= 0 ? s.Groups.IndexOfShip(lastOwn) : -1;
        return true;
    }

    /// <summary>Why an order can't be given now (its button greyed with this in its tooltip, its key ignored), or null.</summary>
    string? WhyNot(Act a, in Orders c)
    {
        var s = c.S;
        var v = s.Ships;
        var gv = s.Groups;
        if (c.Enemy)
        {
            if (c.From < 0)
                return "choose one of your ships first";
            if (a == Act.EngageUnit)
                return c.FromGroup < 0 ? "she's in no squadron" : null;
            if (a == Act.TorpedoesAt)
            {
                var from = world.Classes.TryGet(v.Class[c.From]);
                if (from == null || from.Launchers.Length == 0)
                    return "no tubes";
                if (v.TorpedoesLoaded[c.From] == 0)
                    return "none loaded";
                double reach = from.Torpedoes[from.Launchers[0].Torpedo].SlowRangeM;
                return Earth.Distance(v.PoseAt(c.From, world.DrawMs).Position, s.Contacts.Position[c.Contact]) > reach ? "out of their reach" : null;
            }
            return a == Act.EngageShip ? null : "";
        }
        bool guns = c.Cls != null && c.Cls.Batteries.Length > 0;
        switch (a)
        {
            case Act.LevelUp:
                return c.Level >= chain.Count ? "the top of her chain" : null;
            case Act.LevelDown:
                return c.Level == 0 ? "the orders go to her already" : null;
            case Act.BackIntoLine:
                return v.Reason[c.K] != 0 && CanComeBack(s, c.K) ? null : "she's in her line";
            case Act.ClearRoute:
                return v.RouteCount[c.Kc] == 0 ? "no route" : null;
            case Act.Smoke:
            {
                if (!world.Settings.Smoke)
                    return "the smoke field is off";
                Smokers(gv, selected, c.Unit.IsGroup, smokers);
                foreach (int e in smokers)
                    if (v.IndexOf(e) is int i and >= 0 && world.Classes.TryGet(v.Class[i]) is { Funnels.Length: > 0 })
                        return null;
                return "no funnels";
            }
            case Act.AtWill or Act.Engage or Act.HoldFire:
                return guns ? null : "no guns";
            case Act.Shells:
                return guns && v.Loadout[c.K] != null && ChiefBattery(c.Cls!) >= 0 ? null : "no main guns";
            case Act.Torpedoes:
                return c.Cls == null || c.Cls.Launchers.Length == 0 ? "no tubes" : v.TorpedoesLoaded[c.K] == 0 ? "none loaded" : null;
            case Act.Auto:
                if (c.G >= 0)
                    return gv.Parent[c.G] != 0 ? "in a force: hand over the force" : Tagged(s, gv.Commanded[c.G]) ? null : "your side has no commander";
                if (gv.IndexOfShip(selected) >= 0)
                    return "her squadron's: hand over its top unit";
                return Tagged(s, v.Commanded[c.Kc]) ? null : "your side has no commander";
            case Act.LineAhead or Act.Abreast or Act.Turn or Act.Deploy or Act.DeployPort or Act.DeployStbd or Act.Formation:
                return c.G < 0 ? "a ship: Q for her squadron" : null;
            case Act.Columns:
                return c.G < 0 ? "a ship: Q for her squadron" : gv.Force[c.G] ? null : "a force's: its squadrons as columns";
            case Act.EngageShip or Act.EngageUnit or Act.TorpedoesAt:
                return "";
            default:
                return null;
        }
    }

    /// <summary>How an order's button is lit: the order in force, or armed for its click.</summary>
    Lit LitOf(Act a, in Orders c)
    {
        var v = c.S.Ships;
        var fire = (FireOrder)v.Fire[c.K];
        var order = (CaptainOrder)v.Captain[c.Kc];
        static Lit On(bool on) => on ? Lit.On : Lit.None;
        Lit Armed(ClickPick p, bool on) => armedPick == p ? Lit.Armed : On(on);
        switch (a)
        {
            case Act.HardOver: return On(hardOver);
            case Act.Conn: return On(OwnConn(c.S, c.Unit));
            case Act.Smoke:
            {
                Smokers(c.S.Groups, selected, c.Unit.IsGroup, smokers);
                bool making = true;
                foreach (int e in smokers)
                    if (v.IndexOf(e) is int i and >= 0)
                        making &= v.MakingSmoke[i];
                return On(making);
            }
            case Act.AtWill: return On(fire == FireOrder.AtWill);
            case Act.Engage: return Armed(ClickPick.Engage, fire == FireOrder.Engage);
            case Act.HoldFire: return On(fire == FireOrder.Hold);
            case Act.Shells: return On(v.ShellHeld[c.K] > 0);
            case Act.Torpedoes: return Armed(ClickPick.Torpedoes, false);
            case Act.Fight: return On(order == CaptainOrder.Fight);
            case Act.Follow: return Armed(ClickPick.Follow, order == CaptainOrder.Follow);
            case Act.Hold: return On(order is CaptainOrder.Course or CaptainOrder.None);
            case Act.Tasks: return On(taskOpen);
            case Act.Auto: return On(c.G >= 0 ? c.S.Groups.Commanded[c.G] == 1 : v.Commanded[c.Kc] == 1);
            case Act.LineAhead: return On(c.G >= 0 && ShapeOf(c.S.Groups, c.G) == (int)LineShape.OneLine);
            case Act.Columns: return On(c.G >= 0 && ShapeOf(c.S.Groups, c.G) == (int)LineShape.Columns);
            case Act.Abreast: return On(c.G >= 0 && ShapeOf(c.S.Groups, c.G) == (int)LineShape.Abreast);
            case Act.Deploy: return Armed(ClickPick.Deploy, false);
            default: return Lit.None;
        }
    }

    /// <summary>An order given, from its button or its key; <see cref="WhyNot"/> has let it.</summary>
    void Do(Act a, in Orders c)
    {
        var s = c.S;
        var v = s.Ships;
        var o = world.Orders;
        var unit = c.Unit;
        switch (a)
        {
            case Act.LevelUp: PickLevel(s, c.Level + 1); break;
            case Act.LevelDown: PickLevel(s, c.Level - 1); break;
            case Act.BackIntoLine: BackIntoLine(s, selected); break;
            case Act.Port90 or Act.Port10 or Act.Stbd10 or Act.Stbd90:
                int by = a switch { Act.Port90 => -90, Act.Port10 => -10, Act.Stbd10 => 10, _ => 90 };
                world.Send(o.Course(unit, ((v.CourseOrderDeg[c.Kc] + by) % 360 + 360) % 360, hardOver ? 90 : 0));
                break;
            case Act.Course: courseFocus = true; break;
            case Act.Slower or Act.Faster: RingBell(c, a == Act.Faster ? 1 : -1); break;
            case Act.HardOver: hardOver = !hardOver; break;
            case Act.Conn: world.Send(o.Conn(unit, !OwnConn(s, unit))); break;
            case Act.ClearRoute: world.Send(o.ClearRoute(unit)); break;
            case Act.Smoke:
                bool making = LitOf(Act.Smoke, c) == Lit.On;   // fills the smokers
                foreach (int e in smokers)
                    world.Send(SmokeSystem.MakeSmoke(e, !making));
                break;
            case Act.AtWill: world.Send(o.FireAtWill(unit)); break;
            case Act.Engage: Arm(ClickPick.Engage); break;
            case Act.HoldFire: world.Send(o.HoldFire(unit)); break;
            case Act.Shells: popupAsked = "##shells"; break;
            case Act.Torpedoes: Arm(ClickPick.Torpedoes); break;
            case Act.Fight: Behaviour(3, unit, 0); break;
            case Act.Follow: Arm(ClickPick.Follow); break;
            case Act.Hold: Behaviour(0, unit, 0); break;
            case Act.Tasks: taskOpen = !taskOpen; break;
            case Act.Withdraw:
                foreach (var u in OrderUnits(s))
                    world.Send(o.WithdrawHome(u));
                break;
            case Act.Auto: world.Send(o.Automate(unit, LitOf(Act.Auto, c) != Lit.On)); break;
            case Act.LineAhead: SendShape(s, LineShape.OneLine); break;
            case Act.Columns: SendShape(s, LineShape.Columns); break;
            case Act.Abreast: SendShape(s, LineShape.Abreast); break;
            case Act.Turn: popupAsked = "##turn"; break;
            case Act.Deploy: Arm(ClickPick.Deploy); break;
            case Act.DeployPort: DeployAbeam(s, -1); break;
            case Act.DeployStbd: DeployAbeam(s, 1); break;
            case Act.Formation: popupAsked = "##formation"; break;
            case Act.EngageShip: world.Send(o.Engage(Unit.Ship(lastOwn), selected)); break;
            case Act.EngageUnit: world.Send(o.Engage(Unit.Group(s.Groups.Id[c.FromGroup]), selected)); break;
            case Act.TorpedoesAt: world.Send(TorpedoSystem.Launch(lastOwn, selected, v.TorpedoesLoaded[c.From])); break;
        }
    }

    /// <summary>The strip's keys (the map's own are <see cref="Key"/>'s): an order's while its strip is up. Whether the key
    /// was one.</summary>
    bool StripKey(uint key)
    {
        var s = world.Current;
        if (!OrdersNow(s, out var c))
            return false;
        var mod = SDL_GetModState();
        bool shift = (mod & SDL_Keymod.SDL_KMOD_SHIFT) != 0, alt = (mod & SDL_Keymod.SDL_KMOD_ALT) != 0;
        if (key == SDLK_KP_ENTER)
            key = SDLK_RETURN;
        for (var a = (Act)0; a < Act.Count; a++)
        {
            var d = Def(a);
            if (d.Key != key || d.Shift != shift || d.Alt && !alt || c.Enemy != a >= Act.EngageShip)
                continue;
            if (WhyNot(a, c) == null)
                Do(a, c);
            return true;
        }
        return false;
    }

    /// <summary>The telegraph's bells: a share of full speed, or flank (her overload).</summary>
    static readonly (string Label, double Of, bool Flank)[] Bells =
        [("Astern", -1, false), ("Stop", 0, false), ("Slow", 0.25, false), ("Half", 0.5, false), ("Full", 1, false), ("Flank", 1, true)];

    /// <summary>A bell's speed for the ship that steams the orders (0 with no class).</summary>
    double BellKn(Snapshot s, int kc, int bell)
    {
        if (world.Classes.TryGet(s.Ships.Class[kc]) is not { } guide)
            return 0;
        double full = guide.Motion.MaxSpeedKn;
        double flank = Math.Max(full, guide.SpeedAt(guide.Motion.RatedShp * Math.Max(guide.Motion.OverloadMax, 1)));
        return Bells[bell].Of * (Bells[bell].Flank ? flank : full);
    }

    /// <summary>The speed ordered: what her captain was asked for, though a limit from above (her line's) rings less;
    /// else what's rung.</summary>
    static double OrderedKn(Snapshot s, int kc) => double.IsNaN(s.Ships.SpeedAskedKn[kc]) ? s.Ships.SpeedOrderKn[kc] : s.Ships.SpeedAskedKn[kc];

    /// <summary>Whether her plant makes more than full (an overload): with none, flank is full and its bell is out.</summary>
    bool HasFlank(Snapshot s, int kc) => BellKn(s, kc, Bells.Length - 1) > BellKn(s, kc, Bells.Length - 2) + 0.05;

    /// <summary>The bell rung now: the nearest to the speed ordered.</summary>
    int BellNow(Snapshot s, int kc)
    {
        double kn = OrderedKn(s, kc);
        int best = 0;
        for (int i = 1; i < Bells.Length; i++)
            if (Math.Abs(BellKn(s, kc, i) - kn) < Math.Abs(BellKn(s, kc, best) - kn))
                best = i;
        return best;
    }

    void RingBell(in Orders c, int step)
    {
        int bell = Math.Clamp(BellNow(c.S, c.Kc) + step, 0, Bells.Length - (HasFlank(c.S, c.Kc) ? 1 : 2));
        world.Send(world.Orders.Speed(c.Unit, Math.Round(BellKn(c.S, c.Kc, bell), 2)));
    }

    static int ChiefBattery(ShipClass cls)
    {
        for (int b = 0; b < cls.Batteries.Length; b++)
            if (cls.Batteries[b].Chief)
                return b;
        return -1;
    }

    // ------------------------------------------------------------------ the strip's frame

    /// <summary>The status bar's height, and the strip's over it.</summary>
    static float StatusH => P(20);
    static float StripH => P(105);

    /// <summary>The strip's top (the rail stops above it, the task menu sits on it).</summary>
    static float StripTopY(ImGuiViewportPtr vp) => vp.Pos.Y + vp.Size.Y - StatusH - P(2) - StripH;

    float stripTop;
    // the popup an order asked for (from a key or its button), opened inside the strip's window
    string? popupAsked;
    // where the popups' buttons were drawn: they open above them
    Vector2 shellsAt, turnAt, formationAt;
    bool courseFocus, courseTyping;

    // the pane being laid out: its left, its top, the pen in its row, the widest row; dry: measured, not drawn
    ImDrawListPtr sdl;
    float paneX, paneTop, penX, penY, paneRows;
    bool dry;
    // a joined row's lit button, its edge drawn again over its neighbours'
    Vector2 litAt;
    float litW;
    Lit litKind;
    // each pane's width, measured by the dry pass
    readonly float[] paneW = new float[8];
    int paneN;

    /// <summary>The strip, while one of the player's ships (or, with truth, any ship) or an enemy is selected.</summary>
    void Strip(Snapshot s)
    {
        var vp = ImGui.GetMainViewport();
        stripTop = StripTopY(vp);
        bool any = OrdersNow(s, out var c);
        if (!any || c.Enemy)
        {
            armedPick = ClickPick.None;
            taskOpen = false;
        }
        if (!any)
            return;
        if (!c.Enemy)
            StripTexts(s, c.K, c.Kc, c.Cls);
        // measured, then drawn in a window just the panes' size, so the map past them takes its clicks
        dry = true;
        paneN = 0;
        float left = vp.Pos.X + P(8), width = Panes(c, left) - left;
        dry = false;
        paneN = 0;
        ImGui.SetNextWindowPos(new Vector2(left, stripTop));
        ImGui.SetNextWindowSize(new Vector2(width, StripH));
        ImGui.PushStyleVar(ImGuiStyleVar.WindowPadding, Vector2.Zero);
        ImGui.PushStyleVar(ImGuiStyleVar.WindowBorderSize, 0);
        if (ImGui.Begin("##strip", ImGuiWindowFlags.NoDecoration | ImGuiWindowFlags.NoMove | ImGuiWindowFlags.NoSavedSettings |
                                   ImGuiWindowFlags.NoFocusOnAppearing | ImGuiWindowFlags.NoNav | ImGuiWindowFlags.NoBackground |
                                   ImGuiWindowFlags.NoScrollbar | ImGuiWindowFlags.NoScrollWithMouse))
        {
            sdl = ImGui.GetWindowDrawList();
            sdl.AddRectFilled(new Vector2(left, stripTop), new Vector2(left + width, stripTop + StripH), U(T.Ground));
            Panes(c, left);
            Popups(c);
        }
        ImGui.End();
        ImGui.PopStyleVar(2);
        if (!c.Enemy)
            TaskMenu(s);
    }

    /// <summary>The panes from <paramref name="x"/>: where they end.</summary>
    float Panes(in Orders c, float x)
    {
        if (c.Enemy)
        {
            x = EnemyPane(c, x);
            return AgainstPane(c, x);
        }
        x = ToPane(c, x);
        x = HelmPane(c, x);
        x = GunsPane(c, x);
        x = TaskPane(c, x);
        return c.G >= 0 ? FormationPane(c, x) : x - P(2);
    }

    /// <summary>A pane begun at <paramref name="x"/>: measured (the dry pass) or drawn at the width it measured. Its rows
    /// follow (<see cref="Row"/>), then <see cref="EndPane"/>.</summary>
    void BeginPane(float x, string title, string? note, float rightW, float minW = 0)
    {
        (paneX, paneTop, paneRows) = (x, stripTop, Math.Max(minW, P(17) + Width(UiFonts.Head, title) + (note != null ? P(6) + Width(UiFonts.SansSmall, note) : 0)
            + P(14) + rightW + P(6)));
        if (!dry)
            sdl.Pane(new Vector2(x, stripTop), new Vector2(paneW[paneN], StripH), title, note);
    }

    float EndPane()
    {
        if (dry)
            paneW[paneN] = paneRows;
        return paneX + paneW[paneN++] + P(2);
    }

    /// <summary>The right end of the pane's title strip (drawn), for its figure or chips.</summary>
    float TitleRight => paneX + paneW[paneN] - P(6);

    void Row(int i) => (penX, penY) = (paneX + P(6), paneTop + TitleH + P(5) + i * P(25));

    void Advance(float w) => paneRows = Math.Max(paneRows, (penX += w) - paneX + P(6));

    /// <summary>An order's button in the row: its label (or the given one), its key; greyed with its reason, lit; joined
    /// to the one before it (sharing an edge) in a row of exclusive ones. True when it gave the order.</summary>
    bool Btn(Act a, in Orders c, string? label = null, bool joined = false, bool menu = false)
    {
        var d = Def(a);
        label ??= d.Label;
        if (joined)
            penX -= P(3) + 1;
        float w = ButtonWidth(label, d.Chip, menu: menu);
        bool done = false;
        if (!dry)
        {
            string? why = WhyNot(a, c);
            var lit = why == null ? LitOf(a, c) : Lit.None;
            var at = new Vector2(penX, penY);
            ImGui.PushID((int)a);
            bool hit = sdl.Button("##b", at, label, d.Chip, enabled: why == null, w: w, lit: lit, menu: menu);
            if (ImGui.IsItemHovered(ImGuiHoveredFlags.AllowWhenDisabled))
                Tip(why != null ? why.Length > 0 ? $"{d.Tip} ({why})" : d.Tip : d.Tip);
            ImGui.PopID();
            if (lit != Lit.None)
                (litAt, litW, litKind) = (at, w, lit);
            if (a == Act.Shells)
                shellsAt = at;
            else if (a == Act.Turn)
                turnAt = at;
            else if (a == Act.Formation)
                formationAt = at;
            if (hit)
            {
                Do(a, c);
                done = true;
            }
        }
        Advance(w + P(3));
        return done;
    }

    /// <summary>A joined row's end: its lit button's edge over its neighbours'.</summary>
    void EndJoined()
    {
        if (!dry)
            sdl.LitEdge(litAt, litW, litKind);
        litKind = Lit.None;
    }

    /// <summary>Muted words after the row's buttons.</summary>
    void Say(string s, Vector4? colour = null)
    {
        if (!dry)
            sdl.Text(UiFonts.SansSmall, new Vector2(penX + P(3), Mid(UiFonts.SansSmall, penY, P(20))), colour ?? T.Muted, s);
        Advance(P(3) + Width(UiFonts.SansSmall, s));
    }

    /// <summary>Key chips on their own (the telegraph's ↓ ↑).</summary>
    void Keys(string a, string b)
    {
        float w = KbdWidth(a) + P(2) + KbdWidth(b);
        if (!dry)
        {
            float x = penX + P(3);
            x += sdl.Kbd(new Vector2(x, penY + P(3.5f)), a) + P(2);
            sdl.Kbd(new Vector2(x, penY + P(3.5f)), b);
        }
        Advance(w + P(3));
    }

    /// <summary>A figure at the title strip's right end; its width.</summary>
    float TitleFigure(string s, Vector4 colour)
    {
        if (!dry)
            sdl.TextRight(UiFonts.Mono, TitleRight, Mid(UiFonts.Mono, paneTop, TitleH), colour, s);
        return Width(UiFonts.Mono, s);
    }

    // ------------------------------------------------------------------ the panes

    /// <summary>Orders to: the chain as rows with a radio mark, the top unit first and the ship last, a click picks the
    /// level (Q, Shift+Q step it); whose the top unit is as the rail's chip; under them what's become of her.</summary>
    float ToPane(in Orders c, float x)
    {
        var s = c.S;
        var gv = s.Groups;
        var v = s.Ships;
        int more = OrderUnits(s).Count - 1;
        if (more != toMore)
            (toMore, toNote) = (more, more > 0 ? $"+{more} more, each" : null);
        float chips = KbdWidth(Def(Act.LevelUp).Chip!) + P(2) + KbdWidth(Def(Act.LevelDown).Chip!);
        BeginPane(x, "ORDERS TO", toNote, chips, P(230));
        if (!dry)
        {
            float r = TitleRight, y = paneTop + P(3.5f);
            r -= sdl.Kbd(new Vector2(r - KbdWidth(Def(Act.LevelDown).Chip!), y), Def(Act.LevelDown).Chip!) + P(2);
            sdl.Kbd(new Vector2(r - KbdWidth(Def(Act.LevelUp).Chip!), y), Def(Act.LevelUp).Chip!);
        }
        // what's become of her, under the chain
        string? state = null;
        var stateColour = T.Amber;
        bool back = false;
        if (v.Reason[c.K] != 0)
            (state, back) = (ReasonText(v.Reason[c.K]), CanComeBack(s, c.K));
        else if (gv.IndexOfShip(selected) is int g0 and >= 0 && DetachmentOf(gv, g0, selected) is var d && d != Detachment.InLine)
            state = HeldStateText(d, v.Asked[c.K]);
        else if (v.LandSide[c.K] != 0)
            (state, stateColour) = (v.LandSide[c.K] > 0 ? "following the coast, land to port" : "following the coast, land to starboard", MapPalette.DevSlide);
        int rows = chain.Count + 1 + (state != null ? 1 : 0);
        float rowH = MathF.Min(RowH, (StripH - TitleH - P(2)) / rows), w = paneW[paneN] - 2;
        var a = new Vector2(x + 1, paneTop + TitleH + 1);
        for (int i = chain.Count; i >= 0; i--, a.Y += rowH)
        {
            int depth = chain.Count - i;
            string name = i == 0 ? nameText : gv.Name[chain[i - 1]];
            string kind = i == 0 ? "ship" : gv.Force[chain[i - 1]] ? "force" : "squadron";
            bool top = i == chain.Count;
            byte commanded = i == 0 ? v.Commanded[c.Kc] : gv.Commanded[chain[i - 1]];
            bool tagged = top && Tagged(s, commanded);
            float kindW = Width(UiFonts.SansSmall, kind) + (tagged ? P(28) : 0);
            float nameX = P(6 + 12 * depth) + P(14);
            paneRows = Math.Max(paneRows, nameX + Width(UiFonts.SansSmall, name) + (i == 0 ? P(5) + Width(UiFonts.SansSmall, classText) : 0) + P(12) + kindW + P(6));
            if (dry)
                continue;
            ImGui.PushID(i);
            var (click, hover) = HitRow("##lvl", a, new Vector2(w, rowH));
            bool on = c.Level == i;
            sdl.RowBg(a, w, rowH, chain.Count - i, on, hover);
            sdl.Radio(new Vector2(a.X + P(6 + 12 * depth) + P(5), a.Y + rowH / 2), on);
            float ty = Mid(UiFonts.SansSmall, a.Y, rowH);
            sdl.Text(UiFonts.SansSmall, new Vector2(a.X + nameX, ty), on ? T.Brass : T.Text, name);
            if (i == 0)
                sdl.Text(UiFonts.SansSmall, new Vector2(a.X + nameX + Width(UiFonts.SansSmall, name) + P(5), ty), T.Muted, classText);
            float right = a.X + w - P(6);
            sdl.TextRight(UiFonts.SansSmall, right, ty, T.Muted, kind);
            if (tagged && WhoseChip(sdl, a with { Y = a.Y + (rowH - P(20)) / 2 }, right - Width(UiFonts.SansSmall, kind) - P(5), OwnConn(s, i == 0 ? Unit.Ship(selected) : Unit.Group(gv.Id[chain[i - 1]])), true, commanded))
                world.Send(world.Orders.Automate(i == 0 ? Unit.Ship(selected) : Unit.Group(gv.Id[chain[i - 1]]), commanded != 1));
            else if (click)
                PickLevel(s, i);
            ImGui.PopID();
        }
        if (state != null)
        {
            float sw = Width(UiFonts.SansSmall, state);
            paneRows = Math.Max(paneRows, P(12) + sw + (back ? P(8) + ButtonWidth(Def(Act.BackIntoLine).Label, Def(Act.BackIntoLine).Chip) : 0));
            if (!dry)
            {
                sdl.Text(UiFonts.SansSmall, new Vector2(a.X + P(6), Mid(UiFonts.SansSmall, a.Y, rowH)), stateColour, state);
                if (back)
                {
                    (penX, penY) = (a.X + P(14) + sw, a.Y + (rowH - P(20)) / 2);
                    Btn(Act.BackIntoLine, c);
                }
            }
        }
        return EndPane();
    }

    int toMore = -1;
    string? toNote;
    (Detachment, bool) heldStateKey = ((Detachment)255, false);
    string heldStateText = "";

    string HeldStateText(Detachment d, bool asked)
    {
        if ((d, asked) != heldStateKey)
            (heldStateKey, heldStateText) = ((d, asked), $"{HeldText(d)}{(asked ? "; her orders changed" : "")}");
        return heldStateText;
    }

    /// <summary>The helm: the course (±10°, ±90°, typed in the well), the telegraph's bells, hard over, the conn, the
    /// route cleared, smoke. On the conn (the unit's own, or her line's) the figure says so in its colour.</summary>
    float HelmPane(in Orders c, float x)
    {
        var s = c.S;
        var v = s.Ships;
        bool own = OwnConn(s, c.Unit), line = !own && c.Unit.IsShip && v.Conn[c.Kc] == 2;
        if (!ReferenceEquals(helmText, connSource) || (own, line) != connKey)
        {
            (connSource, connKey) = (helmText, (own, line));
            connText = own ? "conn · " + helmText : line ? "line on conn · " + helmText : helmText;
        }
        // a speed asked over her line's limit: the bell lit is the one asked, the note says what she's held to
        double asked = v.SpeedAskedKn[c.Kc], limit = v.SpeedLimitKn[c.Kc];
        int held = limit > 0 && asked > limit + 0.05 ? (int)Math.Round(limit * 10) : -1;
        if (!ReferenceEquals(courseText, orderedSource) || held != orderedHeld)
            (orderedSource, orderedHeld, orderedText) = (courseText, held, held < 0 ? "ordered " + courseText
                : string.Format(CultureInfo.InvariantCulture, "ordered {0} · held to {1:0.0} kn by the line", courseText, held / 10.0));
        BeginPane(x, "HELM", orderedText, Width(UiFonts.Mono, connText));
        TitleFigure(connText, own || line ? MapPalette.Conn : T.Bright);
        // the course
        Row(0);
        Btn(Act.Port90, c);
        Btn(Act.Port10, c);
        CourseWell(c);
        Btn(Act.Stbd10, c);
        Btn(Act.Stbd90, c);
        // the telegraph
        Row(1);
        int bell = BellNow(s, c.Kc);
        bool flank = HasFlank(s, c.Kc);
        for (int i = 0; i < Bells.Length; i++)
        {
            if (i > 0)
                penX -= 1;
            float w = ButtonWidth(Bells[i].Label, null);
            if (!dry)
            {
                var at = new Vector2(penX, penY);
                ImGui.PushID(100 + i);
                var lit = i == bell ? Lit.On : Lit.None;
                bool on = flank || !Bells[i].Flank;
                if (sdl.Button("##bell", at, Bells[i].Label, enabled: on, w: w, lit: lit))
                    world.Send(world.Orders.Speed(c.Unit, Math.Round(BellKn(s, c.Kc, i), 2)));
                if (ImGui.IsItemHovered(ImGuiHoveredFlags.AllowWhenDisabled))
                    Tip(on ? Def(Act.Faster).Tip + " (↑), slower (↓)" : "Her plant has no overload: flank is full");
                ImGui.PopID();
                if (lit != Lit.None)
                    (litAt, litW, litKind) = (at, w, lit);
            }
            Advance(w);
        }
        EndJoined();
        Keys(Def(Act.Slower).Chip!, Def(Act.Faster).Chip!);
        // the helm's toggles
        Row(2);
        Btn(Act.HardOver, c);
        Btn(Act.Conn, c);
        Btn(Act.ClearRoute, c);
        Btn(Act.Smoke, c);
        return EndPane();
    }

    string connText = "", connSource = "", orderedText = "", orderedSource = "";
    int orderedHeld = -1;
    (bool, bool) connKey;

    /// <summary>The course ordered in an inset well: typed there (Enter puts the cursor in it, Enter sends it).</summary>
    void CourseWell(in Orders c)
    {
        string chip = Def(Act.Course).Chip!;
        float w = P(80), chipW = KbdWidth(chip);
        if (!dry)
        {
            var a = new Vector2(penX, penY);
            sdl.AddRectFilled(a, a + new Vector2(w, P(20)), U(T.Inset));
            sdl.AddRect(a, a + new Vector2(w, P(20)), U(courseTyping ? T.Brass : T.InsetEdge));
            sdl.Kbd(new Vector2(a.X + w - chipW - P(4), a.Y + P(3.5f)), chip);
            ImGui.SetCursorScreenPos(a + new Vector2(P(3), (P(20) - UiFonts.Mono.FontSize - P(4)) / 2));
            ImGui.SetNextItemWidth(w - chipW - P(10));
            ImGui.PushFont(UiFonts.Mono);
            ImGui.PushStyleVar(ImGuiStyleVar.FramePadding, new Vector2(P(4), P(2)));
            ImGui.PushStyleVar(ImGuiStyleVar.FrameBorderSize, 0);
            ImGui.PushStyleColor(ImGuiCol.FrameBg, Vector4.Zero);
            ImGui.PushStyleColor(ImGuiCol.FrameBgHovered, T.Hover);
            ImGui.PushStyleColor(ImGuiCol.FrameBgActive, Vector4.Zero);
            ImGui.PushStyleColor(ImGuiCol.Text, T.Bright);
            double ordered = c.S.Ships.CourseOrderDeg[c.Kc];
            if (!courseTyping)
                courseEdit = (int)Math.Round(ordered) % 360;
            if (courseFocus)
            {
                ImGui.SetKeyboardFocusHere();
                courseFocus = false;
            }
            ImGui.InputInt("##course", ref courseEdit, 0);
            courseTyping = ImGui.IsItemActive();
            if (ImGui.IsItemDeactivatedAfterEdit())
            {
                courseEdit = ((courseEdit % 360) + 360) % 360;
                world.Send(world.Orders.Course(c.Unit, courseEdit, hardOver ? 90 : 0));
            }
            if (ImGui.IsItemHovered())
                Tip(Def(Act.Course).Tip);
            ImGui.PopStyleColor(4);
            ImGui.PopStyleVar(2);
            ImGui.PopFont();
        }
        Advance(w + P(3));
    }

    /// <summary>The guns: fire at will, engage (armed: click an enemy), hold fire; the main guns' shell; torpedoes. The
    /// main battery's line as the note, each battery in its tooltip.</summary>
    float GunsPane(in Orders c, float x)
    {
        var s = c.S;
        var fire = (FireOrder)s.Ships.Fire[c.K];
        int main = -1;
        for (int i = 0; i < s.Batteries.Count; i++)
            if (s.Batteries.Shooter[i] == selected && s.Batteries.Battery[i] == 0)
                main = i;
        bool guns = c.Cls != null && c.Cls.Batteries.Length > 0;
        string? note = !guns ? null : main < 0 ? "main: no target" : BatteryLine(s.Batteries.State[main], s.Batteries.Phase[main], s.Batteries.Salvos[main], s.Batteries.Hits[main]);
        BeginPane(x, "GUNS", note, Width(UiFonts.Mono, gunsText));
        TitleFigure(gunsText, fire == FireOrder.Hold ? T.Amber : T.Bright);
        if (!dry && guns && ImGui.IsMouseHoveringRect(new Vector2(paneX, paneTop), new Vector2(paneX + paneW[paneN], paneTop + TitleH)) && ImGui.IsWindowHovered())
            BatteriesTip(s, c.Cls!);
        Row(0);
        Btn(Act.AtWill, c);
        Btn(Act.Engage, c, joined: true);
        Btn(Act.HoldFire, c, joined: true);
        EndJoined();
        Row(1);
        Btn(Act.Shells, c, ShellsLabel(s, c), menu: true);
        Row(2);
        Btn(Act.Torpedoes, c);
        Say(torpText);
        return EndPane();
    }

    /// <summary>Each battery: a line in the guns' tooltip.</summary>
    void BatteriesTip(Snapshot s, ShipClass cls)
    {
        ImGui.BeginTooltip();
        var v = s.Batteries;
        for (int b = 0; b < cls.Batteries.Length; b++)
        {
            var row = cls.Batteries[b];
            int i = 0;
            while (i < v.Count && (v.Shooter[i] != selected || v.Battery[i] != b))
                i++;
            string kind = row.Kind == Battery.Main ? "main" : "secondary";
            string label = BatteryLabel(cls, b);
            U8.Text(i == v.Count ? U8.W($"{label} {kind}: no target")
                : U8.W($"{label} {kind} on {LabelOf(s, v.Target[i])}: {StateText(v.State[i], v.Phase[i])}, {(v.Local[i] ? "local control" : FireControlPresets.Name(v.Level[i]))}; {v.Salvos[i]} salvos, {v.Hits[i]} hits of {v.Shells[i]}"));
        }
        ImGui.EndTooltip();
    }

    /// <summary>The task: the captain's or the unit's (fight, follow, hold, every other in the task menu: Task, T),
    /// withdraw, and handing the unit to the side's commander. The unit's role as the note, the task it's on at the right.</summary>
    float TaskPane(in Orders c, float x)
    {
        var s = c.S;
        var order = (CaptainOrder)s.Ships.Captain[c.Kc];
        var (role, set) = RoleOf(s, c.Unit);
        if ((role, set) != roleKey)
            (roleKey, roleText) = ((role, set), role == UnitRole.None ? null : $"{RoleName(role)} · {(set ? "set by you" : "worked out")}");
        string head = TaskHead(s, order, c.G);
        BeginPane(x, "TASK", roleText, Width(UiFonts.Mono, head));
        TitleFigure(head, T.Bright);
        Row(0);
        Btn(Act.Fight, c);
        Btn(Act.Follow, c, joined: true);
        Btn(Act.Hold, c, joined: true);
        EndJoined();
        penX += P(6);
        Btn(Act.Tasks, c, menu: true);
        Row(1);
        Btn(Act.Withdraw, c);
        Btn(Act.Auto, c);
        return EndPane();
    }

    (UnitRole, bool) roleKey = ((UnitRole)255, false);
    string? roleText;

    /// <summary>A unit's formation: its whole line's shape (Line ahead, Columns for a force, Abreast; battle-hud.md P1),
    /// its manoeuvres (turn together, deploy), this level's own formation and spacing.</summary>
    float FormationPane(in Orders c, float x)
    {
        var gv = c.S.Groups;
        int g = c.G;
        int guide = gv.GuideShip[g];
        string guideName = guide != 0 ? ShipName(c.S, guide) : "-";
        if ((gv.Id[g], gv.MemberCount[g], (int)gv.Formation[g], gv.SpacingM[g]) != formKey)
        {
            formKey = (gv.Id[g], gv.MemberCount[g], (int)gv.Formation[g], gv.SpacingM[g]);
            formNote = $"{gv.MemberCount[g]} {(gv.Force[g] ? "units" : "ships")}";
            formLabel = string.Format(CultureInfo.InvariantCulture, "{0}{1}, {2:N0} m", gv.Force[g] ? "This level: " : "",
                GroupFormationNames[(int)gv.Formation[g]].ToLowerInvariant(), gv.SpacingM[g]);
        }
        BeginPane(x, "FORMATION", formNote, Width(UiFonts.SansSmall, "guide ") + Width(UiFonts.SansSmall, guideName));
        if (!dry)
        {
            float y = Mid(UiFonts.SansSmall, paneTop, TitleH), r = TitleRight;
            sdl.TextRight(UiFonts.SansSmall, r, y, T.Bright, guideName);
            sdl.TextRight(UiFonts.SansSmall, r - Width(UiFonts.SansSmall, guideName), y, T.Muted, "guide ");
        }
        Row(0);
        Btn(Act.LineAhead, c);
        Btn(Act.Columns, c, joined: true);
        Btn(Act.Abreast, c, joined: true);
        EndJoined();
        Row(1);
        Btn(Act.Turn, c, menu: true);
        Btn(Act.Deploy, c);
        Btn(Act.DeployPort, c);
        Btn(Act.DeployStbd, c);
        Row(2);
        Btn(Act.Formation, c, formLabel, menu: true);
        return EndPane();
    }

    (int, int, int, double) formKey = (-1, -1, -1, -1);
    string formNote = "", formLabel = "";

    /// <summary>An enemy as held: her identity with the rail's diamond, range and bearing from the ship you had, what's
    /// seen, how many of yours fire at her.</summary>
    float EnemyPane(in Orders c, float x)
    {
        var s = c.S;
        var cv = s.Contacts;
        int i = c.Contact;
        string id = Identity(cv, i);
        float labelW = P(70);
        BeginPane(x, "ENEMY", "as held", 0, MathF.Max(P(262), labelW + P(26) + Width(UiFonts.SansSmall, id) + P(12)));
        if (!dry)
        {
            float w = paneW[paneN] - 2, vx = paneX + 1 + labelW;
            var a = new Vector2(paneX + 1, paneTop + TitleH + 1);
            bool seen = cv.InSight[i];
            for (int r = 0; r < 4; r++, a.Y += RowH)
            {
                sdl.RowBg(a, w, RowH, r);
                float ty = Mid(UiFonts.SansSmall, a.Y, RowH), my = Mid(UiFonts.Mono, a.Y, RowH);
                var label = r switch { 0 => "CONTACT", 1 => "FROM", 2 => "SEEN", _ => "FIRING" };
                sdl.Text(UiFonts.SansSmall, new Vector2(a.X + P(6), ty), T.Muted, label);
                switch (r)
                {
                    case 0:
                        Diamond(sdl, new Vector2(vx + P(5), a.Y + RowH / 2), cv.Level[i] >= ContactLevel.Class, seen);
                        sdl.Text(UiFonts.SansSmall, new Vector2(vx + P(14), ty), seen ? T.Bright : T.Faint, id);
                        break;
                    case 1:
                        if (c.From >= 0)
                        {
                            var from = s.Ships.PoseAt(c.From, world.DrawMs).Position;
                            double brg = (Earth.Bearing(from, cv.Position[i]) % 360 + 360) % 360;
                            sdl.Text(UiFonts.Mono, new Vector2(vx, my), T.Bright, U8.W($"{Earth.Distance(from, cv.Position[i]) / 1000:0.0} km · {brg:000}°"));
                            sdl.TextRight(UiFonts.SansSmall, a.X + w - P(6), ty, T.Muted, ShipName(s, lastOwn));
                        }
                        else
                            sdl.Text(UiFonts.SansSmall, new Vector2(vx, ty), T.Muted, "none of yours chosen");
                        break;
                    case 2:
                        sdl.Text(UiFonts.SansSmall, new Vector2(vx, ty), cv.Seen[i] != SeenFlags.None ? T.Text : T.Muted,
                            cv.Seen[i] != SeenFlags.None ? U8.W($"{SeenWords(cv.Seen[i])}") : seen ? "no damage seen"u8
                                : U8.W($"last seen {new WorldTime(cv.SeenMs[i]).ToUtc().Hour:00}:{new WorldTime(cv.SeenMs[i]).ToUtc().Minute:00}"));
                        break;
                    default:
                        int firing = railOn.GetValueOrDefault(selected);
                        sdl.Text(UiFonts.SansSmall, new Vector2(vx, ty), firing > 0 ? T.Text : T.Muted, firing > 0 ? U8.W($"{firing} of yours") : "none of yours"u8);
                        break;
                }
            }
        }
        return EndPane();
    }

    /// <summary>The orders against her, from the ship you had: engage her with that ship's squadron (E) or the ship alone
    /// (Alt+E), torpedoes (Shift+E).</summary>
    float AgainstPane(in Orders c, float x)
    {
        var s = c.S;
        string? from = c.From >= 0 ? ShipName(s, lastOwn) : null;
        BeginPane(x, "ORDERS AGAINST HER", from != null ? U8Label("from ", from) : null, 0);
        if (c.FromGroup >= 0)
        {
            Row(0);
            Btn(Act.EngageUnit, c, U8Label("Engage her with ", s.Groups.Name[c.FromGroup]));
        }
        Row(c.FromGroup >= 0 ? 1 : 0);
        Btn(Act.EngageShip, c, AloneLabel(from ?? "your ship", c.FromGroup >= 0));
        Row(c.FromGroup >= 0 ? 2 : 1);
        Btn(Act.TorpedoesAt, c);
        if (!dry && WhyNot(Act.TorpedoesAt, c) is { Length: > 0 } why)
            Say(why);
        else if (dry)
            Say("out of their reach");
        return EndPane();
    }

    // "Engage her with Lion alone", made once a name
    readonly Dictionary<(string, bool), string> aloneLabels = [];

    string AloneLabel(string ship, bool alone) =>
        aloneLabels.TryGetValue((ship, alone), out var l) ? l : aloneLabels[(ship, alone)] = $"Engage her with {ship}{(alone ? " alone" : "")}";

    // ------------------------------------------------------------------ the popups

    /// <summary>The popups an order opens (asked by its key or its button), above the button.</summary>
    void Popups(in Orders c)
    {
        if (popupAsked != null)
        {
            ImGui.OpenPopup(popupAsked);
            popupAsked = null;
        }
        if (c.Enemy)
            return;
        var s = c.S;
        ImGui.SetNextWindowPos(shellsAt - new Vector2(0, P(2)), ImGuiCond.Appearing, new Vector2(0, 1));
        if (ImGui.BeginPopup("##shells"))
        {
            ShellsMenu(s, c.K, c.Cls!);
            ImGui.EndPopup();
        }
        ImGui.SetNextWindowPos(turnAt - new Vector2(0, P(2)), ImGuiCond.Appearing, new Vector2(0, 1));
        if (ImGui.BeginPopup("##turn"))
        {
            TurnMenu(c.Unit);
            ImGui.EndPopup();
        }
        ImGui.SetNextWindowPos(formationAt - new Vector2(0, P(2)), ImGuiCond.Appearing, new Vector2(0, 1));
        if (ImGui.BeginPopup("##formation"))
        {
            if (c.G >= 0)
            {
                ImGui.TextDisabled(s.Groups.Name[c.G]);
                FormationEdit(s.Groups, c.G, ref (c.G == chain[0] ? ref spacingEdit : ref forceSpacingEdit), "##fm");
            }
            ImGui.EndPopup();
        }
    }

    /// <summary>The Shells button's labels (docs/battle/shell-choice.md A5): by the kind held (+ 1, 0 by target) and,
    /// by target, the kind her main guns hold (+ 1, 0 none).</summary>
    static readonly string[][] ShellsLabels = [.. Enumerable.Range(0, 4).Select(h => Enumerable.Range(0, 4).Select(g =>
        h > 0 ? $"Shells: {KindName((ShellKind)(h - 1))}"
        : g > 0 ? $"Shells: by target, {KindName((ShellKind)(g - 1))}" : "Shells: by target").ToArray())];
    static readonly string[] ShellsItems = [.. Enum.GetValues<ShellKind>().Select(x => $"{KindName(x)}##shells{x}")];

    string ShellsLabel(Snapshot s, in Orders c)
    {
        var v = s.Ships;
        int chief = c.Cls != null ? ChiefBattery(c.Cls) : -1;
        int guns = chief >= 0 && v.BatteryStart[c.K] >= 0 ? v.BatteryInGuns[v.BatteryStart[c.K] + chief] : 0;
        return ShellsLabels[Math.Min((int)v.ShellHeld[c.K], 3)][Math.Min(guns, 3)];
    }

    /// <summary>The main guns' shell for the selection (decision 3): by target, her captain's choice, or a kind held until
    /// they're out of it; the kinds her main guns carry.</summary>
    void ShellsMenu(Snapshot s, int k, ShipClass cls)
    {
        var v = s.Ships;
        var lo = v.Loadout[k];
        if (lo == null)
            return;
        var o = world.Orders;
        if (ImGui.Selectable("By target##shells", v.ShellHeld[k] == 0))
            foreach (var u in OrderUnits(s))
                world.Send(o.Shells(u, null));
        foreach (var kind in Enum.GetValues<ShellKind>())
        {
            if (kind == ShellKind.Star)
                continue;   // fired by an illuminating mount, not loaded by the battery (docs/battle/night.md Q5)
            bool carried = false;
            for (int b = 0; b < cls.Batteries.Length; b++)
                if (cls.Batteries[b].Chief)
                    foreach (var l in lo.Loads(b))
                        carried |= lo.ShellTypes[l.ShellType].Kind == kind;
            if (carried && ImGui.Selectable(ShellsItems[(int)kind], v.ShellHeld[k] == (byte)(kind + 1)))
                foreach (var u in OrderUnits(s))
                    world.Send(o.Shells(u, kind));
        }
    }

    // ------------------------------------------------------------------ the strip's texts

    // the strip's strings, made when what they say changes
    (int Ship, int Course, int Kn) helmKey = (-1, -1, -1);
    string helmText = "", courseText = "";
    (int Ship, int Target, byte Fire) gunsKey = (-1, -1, 255);
    string gunsText = "";
    (int Ship, int Loaded, int Left) torpKey = (-1, -1, -1);
    string torpText = "";
    int nameShip = -1, nameGeneration = -1;
    string nameText = "", classText = "";

    void StripTexts(Snapshot s, int k, int kc, ShipClass? cls)
    {
        var v = s.Ships;
        if (selected != nameShip || world.Generation != nameGeneration)
        {
            (nameShip, nameGeneration) = (selected, world.Generation);
            nameText = world.NameOf(selected) ?? $"#{selected}";
            classText = cls?.Name ?? "?";
        }
        var (p, f) = v.PoseAt(kc, world.DrawMs);
        int course = (int)Math.Round(Earth.Heading(p, f)) % 360, kn = (int)Math.Round(v.Speed[kc] / Handling.MsPerKnot * 10);
        if ((kc, course, kn) != helmKey)
        {
            helmKey = (kc, course, kn);
            helmText = string.Format(CultureInfo.InvariantCulture, "{0:000}° · {1:0.0} kn", course, kn / 10.0);
        }
        courseText = CourseText(v.CourseOrderDeg[kc]);
        // the guns' target: the engaged one, else the main battery's
        int target = v.FireTarget[k];
        if (target == 0)
            for (int i = 0; i < s.Batteries.Count; i++)
                if (s.Batteries.Shooter[i] == selected && s.Batteries.Battery[i] == 0)
                    target = s.Batteries.Target[i];
        if ((selected, target, v.Fire[k]) != gunsKey)
        {
            gunsKey = (selected, target, v.Fire[k]);
            gunsText = (FireOrder)v.Fire[k] == FireOrder.Hold ? "holding fire" : target > 0 ? "on " + TargetLabel(s, target) : "no target";
        }
        if ((selected, v.TorpedoesLoaded[k], v.TorpedoesLeft[k]) != torpKey)
        {
            torpKey = (selected, v.TorpedoesLoaded[k], v.TorpedoesLeft[k]);
            torpText = cls == null || cls.Launchers.Length == 0 ? "no tubes" : $"{v.TorpedoesLoaded[k]} loaded · {v.TorpedoesLeft[k]} left";
        }
    }

    // the course ordered, as text: kept per whole degree
    int courseKey = -1;
    string courseKeyText = "";

    string CourseText(double deg)
    {
        int d = ((int)Math.Round(deg) % 360 + 360) % 360;
        if (d != courseKey)
            (courseKey, courseKeyText) = (d, $"{d:000}°");
        return courseKeyText;
    }

    // the main battery's line, kept while it says the same
    (BatteryState, FirePhase, int, int) batteryKey = ((BatteryState)255, default, -1, -1);
    string batteryText = "";

    string BatteryLine(BatteryState state, FirePhase phase, int salvos, int hits)
    {
        var key = (state, phase, salvos, hits);
        if (key != batteryKey)
            (batteryKey, batteryText) = (key, $"main: {StateText(state, phase)} · {salvos} salvos, {hits} hits");
        return batteryText;
    }

    /// <summary>The status bar's left end: what the mouse does with the selection, or what the armed pick waits for.</summary>
    string StripHint(Snapshot s)
    {
        if (armedPick != ClickPick.None)
        {
            if (armedPick != hintPick)
                (hintPick, hintText) = (armedPick, $"{PickVerb(armedPick)}: {PickClick(armedPick)} · Esc cancels");
            return hintText;
        }
        if (s.Ships.IndexOf(selected) >= 0)
            return SmartScreens(s) ? "Right-click: steer · on an enemy: engage · on your ship: screen · Shift: waypoint · Alt with any order: this ship only"
                : "Right-click: steer · on an enemy: engage · on your ship: follow · Shift: waypoint · Alt with any order: this ship only";
        if (selected >= 0)
            return "Select one of your ships and right-click her to engage · Esc: deselect";
        return "Click a ship to select her · drag: a box · Tab: your ships · wheel: zoom · WASD or middle drag: pan";
    }

    ClickPick hintPick;
    string hintText = "";

    /// <summary>Whether the right click on one of ours screens her (the orders' unit is a flotilla or a screen, decision 4
    /// of docs/battle/admirals-orders.md), else follows her.</summary>
    bool SmartScreens(Snapshot s)
    {
        var (unit, _) = Commanded(s);
        return RoleOf(s, unit).Role is UnitRole.Flotilla or UnitRole.Screen;
    }

    readonly List<int> smokers = [];

    /// <summary>The ships the strip's smoke goes to, into a list: every ship of her squadron when the orders go to a unit
    /// (a division of a divided squadron is a squadron like any), never a force's (whatever level the helm's go to), or her
    /// alone when they go to the ship. Her squadron's snapshot index, or -1 for her alone.</summary>
    internal static int Smokers(GroupsView gv, int ship, bool toUnit, List<int> into)
    {
        into.Clear();
        int g = toUnit ? gv.IndexOfShip(ship) : -1;
        if (g < 0)
            into.Add(ship);
        else
            for (int i = 0; i < gv.MemberCount[g]; i++)
                into.Add(gv.Members[gv.MemberStart[g] + i]);
        return g;
    }

    /// <summary>Each unit the orders go to (a group) into a shape, its whole line (docs/map-ui/battle-hud.md P1).</summary>
    void SendShape(Snapshot s, LineShape shape)
    {
        foreach (var u in OrderUnits(s))
            if (u.IsGroup)
                world.Send(world.Orders.Shape(u, shape));
    }

    /// <summary>Each unit the orders go to (a group) deployed 8 points to port (-1) or starboard (+1) of its guide's
    /// heading: its wing column on that side leads round, as Jellicoe's port wing did.</summary>
    void DeployAbeam(Snapshot s, int side)
    {
        foreach (var u in OrderUnits(s))
            if (u.IsGroup && s.Ships.IndexOf(GuideOf(s, u)) is int k and >= 0)
            {
                var (p, f) = s.Ships.PoseAt(k, world.DrawMs);
                world.Send(world.Orders.Deploy(u, ((Earth.Heading(p, f) + 90 * side) % 360 + 360) % 360));
            }
    }

    /// <summary>The Turn popup: together by 4 or 8 points either way, or about with the rear ship first (the battle
    /// turn-away), each to the unit's admiral.</summary>
    void TurnMenu(Unit unit)
    {
        var o = world.Orders;
        ImGui.TextDisabled("turn together");
        foreach (var (label, deg) in TurnButtons)
            if (ImGui.Selectable(label))
                world.Send(o.TurnTogether(unit, deg));
        ImGui.Separator();
        ImGui.TextDisabled("the battle turn: about, the rear ship first");
        if (ImGui.Selectable("about, to port##bt"))
            world.Send(o.BattleTurn(unit, toStarboard: false));
        if (ImGui.Selectable("about, to starboard##bt"))
            world.Send(o.BattleTurn(unit, toStarboard: true));
    }

    // the task's head, kept while it says the same
    (CaptainOrder, int, byte, byte, byte, int) taskKey = ((CaptainOrder)255, -2, 0, 0, 0, -1);
    string taskText = "";

    string TaskHead(Snapshot s, CaptainOrder order, int g)
    {
        var gv = s.Groups;
        var key = (order, g, g >= 0 ? gv.Task[g] : (byte)0, g >= 0 ? gv.TaskStatus[g] : (byte)0, g >= 0 ? gv.Commanded[g] : (byte)0, world.Generation);
        if (key == taskKey)
            return taskText;
        taskKey = key;
        string what = order switch
        {
            CaptainOrder.Fight => "fight",
            CaptainOrder.Follow => "follow",
            CaptainOrder.Run => "run",
            CaptainOrder.Station => "on station",
            CaptainOrder.Route => "route",
            CaptainOrder.Shadow => "shadow",
            CaptainOrder.Home => "home",
            CaptainOrder.Screen => "screen",
            _ => "hold",
        };
        if (g >= 0 && gv.Task[g] != 0)
            what = TaskText(gv.Task[g], gv.TaskStatus[g]);
        if (g >= 0 && gv.Commanded[g] != 0)
            what += gv.Commanded[g] == 1 ? " · AI" : " · yours";
        return taskText = what;
    }

    void Arm(ClickPick pick)
    {
        armedPick = armedPick == pick ? ClickPick.None : pick;
        fireArmed = false;
    }

    // ------------------------------------------------------------------ clicks and the cursor's verb

    /// <summary>What's under the cursor for an order: an enemy (in sight, a contact, or with truth one of another side's
    /// ships), one of hers, or the sea (with where on it).</summary>
    (int Enemy, int Own, bool Sea, Vec3d At) Under(Snapshot s, double x, double y)
    {
        var view = View;
        int enemy = PickShip(view, x, y, s.Sighted);
        if (enemy < 0)
            enemy = PickContact(view, s, x, y);
        int own = -1;
        if (enemy < 0 && PickShip(view, x, y) is int hit and >= 0)
        {
            int kh = s.Ships.IndexOf(hit), ks = s.Ships.IndexOf(selected);
            if (ks >= 0 && kh >= 0 && s.Ships.Side[kh] != s.Ships.Side[ks])
                enemy = hit;
            else
                own = hit;
        }
        bool sea = view.Pick(x, y, out var at);
        return (enemy, own, sea, at);
    }

    /// <summary>A right click with a pick armed: done with what's under it (true), else false and the smart order goes. A
    /// miss on what the pick wants keeps it armed.</summary>
    bool ArmedClick(double x, double y)
    {
        if (armedPick == ClickPick.None || selected < 0)
            return false;
        var s = world.Current;
        var (enemy, own, sea, at) = Under(s, x, y);
        var (unit, _) = Commanded(s);
        int k = s.Ships.IndexOf(selected);
        bool done = true;
        switch (armedPick)
        {
            case ClickPick.Engage or ClickPick.Torpedoes:
                if (enemy <= 0)
                    return true;
                world.Send(armedPick == ClickPick.Engage ? world.Orders.Engage(unit, enemy)
                    : TorpedoSystem.Launch(selected, enemy, Math.Max(k >= 0 ? s.Ships.TorpedoesLoaded[k] : 1, 1)));
                break;
            case ClickPick.Follow or ClickPick.Run:
                int target = enemy > 0 ? enemy : own;
                if (target <= 0 || target == selected)
                    return true;
                if (armedPick == ClickPick.Run)
                    foreach (var u in OrderUnits(s))
                        Behaviour(2, u, target);
                else
                    foreach (var u in OrderUnits(s))
                        if (!ShipIn(s, target, u))
                            Behaviour(1, u, target);
                break;
            default:
                done = GiveTask(s, armedPick, own, enemy, sea, at);
                break;
        }
        if (done)
            armedPick = ClickPick.None;
        return true;
    }

    /// <summary>The right-click's order: an enemy engaged, one of hers screened (the unit a flotilla or a screen) or
    /// followed (station astern), the sea steered for (with shift, a waypoint added). True when it gave one. An order
    /// on one of hers goes to each unit selected.</summary>
    bool SmartOrder(double x, double y, bool waypoint)
    {
        var s = world.Current;
        if (s.Ships.IndexOf(selected) < 0)
            return false;
        var (enemy, own, _, _) = Under(s, x, y);
        var (unit, ship) = Commanded(s);
        if (enemy > 0)
        {
            foreach (var u in OrderUnits(s))
                world.Send(world.Orders.Engage(u, enemy));
            return true;
        }
        if (own > 0 && own != selected && own != ship && !waypoint)
        {
            if (SmartScreens(s))
                GiveTask(s, ClickPick.Screen, own, 0, false, default);
            else
                foreach (var u in OrderUnits(s))
                    if (!ShipIn(s, own, u))
                        Behaviour(1, u, own);
            return true;
        }
        return false;   // the sea: OrderAt steers
    }

    /// <summary>A left click on a ship twice within a moment: her squadron takes the orders.</summary>
    void DoubleClick(int ship)
    {
        long now = Environment.TickCount64;
        if (ship >= 0 && ship == lastClickShip && now - lastClickMs < 400)
        {
            Chain(world.Current);
            if (chain.Count > 0)
                PickLevel(world.Current, 1);
        }
        (lastClickShip, lastClickMs) = (ship, now);
    }

    /// <summary>Tab: the player's next ship (shift: the one before), by entity.</summary>
    void NextShip(bool back)
    {
        var s = world.Current;
        var v = s.Ships;
        int side = s.PlayerSide, best = -1, wrap = -1;
        for (int k = 0; k < v.Count; k++)
        {
            if (side >= 0 && v.Side[k] != side || v.Loss[k] != LossKind.None)
                continue;
            int e = v.Entity[k];
            if (back ? e < selected && (best < 0 || e > best) : e > selected && (best < 0 || e < best))
                best = e;
            if (back ? wrap < 0 || e > wrap : wrap < 0 || e < wrap)
                wrap = e;
        }
        int next = best >= 0 ? best : wrap;
        if (next >= 0)
        {
            selected = next;
            ClearPicks();
            armedPick = ClickPick.None;
        }
    }

    // the verb's text, kept while it says the same; whether it's an armed pick's miss
    (int Kind, int Target, int Course, int Tenths, int Who) verbKey = (-1, 0, 0, 0, 0);
    string verbText = "";
    bool verbMiss;

    /// <summary>A target's distance from a point, tenths of a kilometre (0 if she's gone).</summary>
    int Dist(Snapshot s, Vec3d from, int ship)
    {
        int k = s.Ships.IndexOf(ship);
        if (k < 0 && s.Sighted.IndexOf(ship) is int j and >= 0)
            return (int)Math.Round(Earth.Distance(from, s.Sighted.PoseAt(j, world.DrawMs).Position) / 100);
        return k < 0 ? 0 : (int)Math.Round(Earth.Distance(from, s.Ships.PoseAt(k, world.DrawMs).Position) / 100);
    }

    /// <summary>An armed pick's colour at the cursor: an order about the enemy red, one of ours friend blue, the sea's and
    /// a manoeuvre's brass.</summary>
    static Vector4 PickColour(ClickPick pick) => pick switch
    {
        ClickPick.Engage or ClickPick.Torpedoes or ClickPick.Fight or ClickPick.Intercept or ClickPick.Shadow or ClickPick.Attack or ClickPick.Run => Bad,
        ClickPick.Follow or ClickPick.Screen or ClickPick.Scout or ClickPick.Cover or ClickPick.Join or ClickPick.Station or ClickPick.DivideFrom => Friend,
        _ => Theme.Brass,
    };

    /// <summary>The cursor's verb, beside it on the map: what a right-click (or the armed pick's click) would do.</summary>
    void CursorVerb(Snapshot s)
    {
        if (!mouseIn || selected < 0 || ImGui.GetIO().WantCaptureMouse || dragging || boxing || placing)
            return;
        int k = s.Ships.IndexOf(selected);
        if (k < 0)
            return;
        var vp = ImGui.GetMainViewport();
        double x = mouse.X * width / Math.Max(vp.Size.X, 1), y = mouse.Y * height / Math.Max(vp.Size.Y, 1);
        var (enemy, own, sea, at) = Under(s, x, y);
        var (unit, ship) = Commanded(s);
        int ks = s.Ships.IndexOf(ship);
        var (p, _) = s.Ships.PoseAt(ks >= 0 ? ks : k, world.DrawMs);
        int course = sea ? (int)Math.Round((Earth.Bearing(p, at) % 360 + 360) % 360) % 360 : 0;
        int tenths = sea ? (int)Math.Round(Earth.Distance(p, at) / 100) : 0;
        bool shift = ImGui.GetIO().KeyShift;
        int many = OrderUnits(s).Count;
        (int Kind, int Target, int Course, int Tenths, int Who) key = armedPick == ClickPick.None && RightDragged(x, y) && Shape(s, x, y) is var (press, heading, deploy)
            ? (deploy ? 8 : 7, 0, (int)Math.Round(heading) % 360, (int)Math.Round(Earth.Distance(p, press) / 100), unit.Id * 64 + many)
            : armedPick != ClickPick.None
            ? (10 + (int)armedPick, enemy > 0 ? enemy : own > 0 ? own : 0, enemy > 0 || own > 0 ? 0 : course, enemy > 0 || own > 0 ? Dist(s, p, enemy > 0 ? enemy : own) : tenths, unit.Id * 64 + many)
            : enemy > 0 ? (1, enemy, 0, tenths, unit.Id * 64 + many)
            : own > 0 && own != selected && own != ship && !shift ? (SmartScreens(s) ? 6 : 2, own, 0, 0, unit.Id * 64 + many)
            : sea && world.Shore.IsLand(at) ? (3, 0, 0, 0, unit.Id)
            : sea ? (shift ? 4 : 5, 0, course, tenths, unit.Id) : (0, 0, 0, 0, 0);
        if (key.Kind == 0 || own == selected && armedPick == ClickPick.None)
            return;
        if (key != verbKey)
        {
            verbKey = key;
            string who = unit.IsGroup && s.Groups.IndexOfGroup(unit.GroupId) is int g and >= 0 ? s.Groups.Name[g] : nameText;
            if (many > 1)
                who = $"{who} + {many - 1}";
            string? words = key.Kind >= 10 ? TaskVerb(s, armedPick, own, enemy, sea, at, key.Tenths, key.Course) : null;
            verbMiss = key.Kind >= 10 && words == null;
            verbText = key.Kind switch
            {
                >= 10 => words != null ? $"{who}: {words}" : $"{PickVerb(armedPick)}: {PickClick(armedPick)}",
                1 => string.Format(CultureInfo.InvariantCulture, "{0}: ENGAGE {1} · {2:0.0} km", who, TargetLabel(s, key.Target), key.Tenths / 10.0),
                2 => $"{who}: FOLLOW {TargetLabel(s, key.Target)}",
                6 => TaskVerb(s, ClickPick.Screen, own, 0, false, default, 0, 0) is { } screen ? $"{who}: {screen}" : $"{who}: FOLLOW {TargetLabel(s, key.Target)}",
                3 => "land",
                4 => string.Format(CultureInfo.InvariantCulture, "{0}: WAYPOINT {1:000}° · {2:0.0} km", who, key.Course, key.Tenths / 10.0),
                7 => string.Format(CultureInfo.InvariantCulture, "{0}: THERE ({1:0.0} km), THEN {2:000}°", who, key.Tenths / 10.0, key.Course),
                8 => string.Format(CultureInfo.InvariantCulture, "{0}: DEPLOY {1:000}°", who, key.Course),
                _ => string.Format(CultureInfo.InvariantCulture, "{0}: STEER {1:000}° · {2:0.0} km", who, key.Course, key.Tenths / 10.0),
            };
        }
        bool miss = verbMiss;
        var dl = ImGui.GetForegroundDrawList();
        var size = ImGui.CalcTextSize(verbText);
        var pos = vp.Pos + mouse + new Vector2(16, -22) * UiFonts.Scale;
        dl.AddRectFilled(pos - new Vector2(5, 2) * UiFonts.Scale, pos + size + new Vector2(5, 2) * UiFonts.Scale, 0xD8110E0A, 2 * UiFonts.Scale);
        var colour = key.Kind switch { >= 10 => miss ? Theme.Amber : PickColour(armedPick), 1 => Bad, 2 or 6 => Friend, 3 => Theme.Muted, _ => Theme.Brass };
        dl.AddText(pos, ImGui.GetColorU32(colour), verbText);
    }
}
