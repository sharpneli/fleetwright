using System.Numerics;
using Fleetwright.Sim;
using Fleetwright.Sim.Ai;
using Fleetwright.Sim.Combat;
using Fleetwright.Sim.Core;
using Fleetwright.Ui;
using ImGuiNET;
using SDL;
using static SDL.SDL3;

namespace Fleetwright.Map;

/// <summary>The camera's input (docs/map-ui/input.md I0): WASD pans (<see cref="PanKeys"/>); the middle button drags
/// the map, and its click centres the map on the sea or follows a ship until a pan. The left button only selects (I1): a click a ship, a drag a box. The right acts (I2): its order goes on the release, drawn
/// from the ship that steams it to what it's about while the button is held (Esc drops it); an armed verb is aimed by
/// it too, and a drag shapes it (I3: the heading on arrival, a force's deploy).</summary>
public sealed unsafe partial class MapScene
{
    readonly PanKeys panKeys = new();   // WASD, as every view's (PanKeys.cs)
    int follow;                         // the camera kept on this ship (an entity; 0 none)

    /// <summary>A key down or up: a pan key's (true), or not one.</summary>
    bool PanKey(SDL_Scancode code, bool down) => panKeys.Key(code, down);

    /// <summary>The keys' pan for this frame, the map dragged by it. A pan lets go of a followed ship.</summary>
    void KeyPan(double dt)
    {
        if (ImGui.GetIO().WantTextInput)
            panKeys.Release();   // typing: the ups go to the field
        var (dx, dy) = panKeys.Step(dt, height, (SDL_GetModState() & SDL_Keymod.SDL_KMOD_SHIFT) != 0);
        if (dx == 0 && dy == 0)
            return;
        double cx = width / 2, cy = height / 2;
        camera.Pan(cx, cy, cx - dx, cy - dy, width, height);
        follow = 0;
    }

    /// <summary>A middle click: on a ship (hers, or one in sight) the camera follows her; on the sea the map centres
    /// there.</summary>
    void MiddleClick(double x, double y)
    {
        var view = View;
        var s = world.Current;
        int hit = PickShip(view, x, y);
        if (hit < 0)
            hit = PickShip(view, x, y, s.Sighted);
        if (hit >= 0)
            follow = hit;
        else if (view.Pick(x, y, out var at))
        {
            camera.Drag(at, width / 2, height / 2, width, height);
            follow = 0;
        }
    }

    // ------------------------------------------------------------------ the right button (I2)

    bool rightHeld;   // the right button is down on the map: its release gives the order the cursor names
    double rightX, rightY;   // where it went down, target pixels

    /// <summary>Metres on from the press point along a right drag's heading: a short last leg, so the route ends on it.</summary>
    const double ShapeLegM = 2000;

    /// <summary>Whether the right button held has moved a click's worth from its press: a drag, shaping the order (I3).</summary>
    bool RightDragged(double x, double y) => rightHeld && Math.Abs(x - rightX) + Math.Abs(y - rightY) >= ClickPx;

    /// <summary>A right drag let go at (x, y), shaped from its press on the sea (docs/map-ui/input.md I3): a force in
    /// columns deploys on the drag's bearing; else the unit steers for the press point and holds the drag's heading
    /// there (a waypoint, and one a short leg on: past the last a ship holds the course she arrived on); with shift both
    /// added to the route. False when it isn't a shape (pressed on a ship, an armed verb, land): the press's click then.</summary>
    bool ShapedOrder(double x, double y, bool waypoint)
    {
        var s = world.Current;
        if (armedPick != ClickPick.None || fireArmed || s.Ships.IndexOf(selected) < 0 || Shape(s, x, y) is not var (from, heading, deploy))
            return false;
        var (unit, _) = Commanded(s);
        var o = world.Orders;
        if (deploy)
        {
            world.Send(o.Deploy(unit, heading));
            return true;
        }
        var to = from;
        var f = Earth.Forward(from, heading);
        Earth.Advance(ref to, ref f, ShapeLegM);
        if (world.Shore.IsLand(to))
            to = from;   // the leg on would be ashore: the press point alone
        if (!waypoint)
            world.Send(o.ClearRoute(unit));
        var (la, lo) = Earth.LatLon(from);
        world.Send(o.AddWaypoint(unit, la, lo));
        if (to != from)
        {
            (la, lo) = Earth.LatLon(to);
            world.Send(o.AddWaypoint(unit, la, lo));
        }
        return true;
    }

    /// <summary>The right drag's shape to (x, y): the press point on the sea, the heading from it to the cursor, and
    /// whether it deploys (the unit commanded a force in columns); null when the press is on a ship, off the globe or
    /// ashore.</summary>
    (Vec3d From, double Heading, bool Deploy)? Shape(Snapshot s, double x, double y)
    {
        var (enemy, own, sea, from) = Under(s, rightX, rightY);
        if (enemy > 0 || own > 0 || !sea || world.Shore.IsLand(from) || !View.Pick(x, y, out var to))
            return null;
        var (unit, _) = Commanded(s);
        var gv = s.Groups;
        bool deploy = unit.IsGroup && gv.IndexOfGroup(unit.GroupId) is int g and >= 0 && gv.Force[g] && ShapeOf(gv, g) == (int)LineShape.Columns;
        return (from, (Earth.Bearing(from, to) % 360 + 360) % 360, deploy);
    }

    /// <summary>While the right button is held: a dashed line from the ship that steams the orders to what the release
    /// would be about (the enemy, one of ours, the sea), in the verb's colour; the cursor's verb names it.</summary>
    void OrderPreview(in MapView view, Snapshot s)
    {
        if (!rightHeld || !mouseIn || ImGui.GetIO().WantCaptureMouse || s.Ships.IndexOf(selected) < 0)
            return;
        var (enemy, own, sea, at) = Under(s, cursorX, cursorY);
        var (_, ship) = Commanded(s);
        int k = s.Ships.IndexOf(ship);
        if (k < 0)
            k = s.Ships.IndexOf(selected);
        var from = s.Ships.PoseAt(k, world.DrawMs).Position;
        int target = enemy > 0 ? enemy : own > 0 && own != ship ? own : 0;
        Vec3d to;
        if (target > 0)
        {
            if (!TargetPos(s, target, out to))
                return;
        }
        else if (sea && !world.Shore.IsLand(at))
            to = at;
        else
            return;
        var colour = armedPick != ClickPick.None ? PickColour(armedPick) : enemy > 0 ? Bad : own > 0 ? Friend : MapPalette.RouteSelected;
        if (armedPick == ClickPick.None && RightDragged(cursorX, cursorY) && Shape(s, cursorX, cursorY) is var (press, _, deploy))
        {
            // a shape (I3): to the press point, then along the drag (a deploy: the drag alone, the line's course)
            if (!deploy)
            {
                overlay.BeginStrip(colour, 1.5f, Dash.Dashed, true);
                Point(view, from);
                Point(view, press);
                overlay.End();
            }
            overlay.BeginStrip(colour, 2f, Dash.Solid, true);
            Point(view, press);
            Point(view, at);
            overlay.End();
            if (view.Project(press, out double px, out double py) && view.Project(at, out double ex, out double ey))
                symbols.Add(SymbolShape.Arrow, ex, ey, 6 * Rp, colour, colour, 1f, Math.Atan2(ey - py, ex - px));
            return;
        }
        overlay.BeginStrip(colour, 1.5f, Dash.Dashed, true);
        Point(view, from);
        Point(view, to);
        overlay.End();
    }

    /// <summary>Where a ship the player can name is now: hers, one in sight, or a contact's marker.</summary>
    bool TargetPos(Snapshot s, int ship, out Vec3d p)
    {
        if (s.Ships.IndexOf(ship) is int k and >= 0)
            p = s.Ships.PoseAt(k, world.DrawMs).Position;
        else if (s.Sighted.IndexOf(ship) is int j and >= 0)
            p = s.Sighted.PoseAt(j, world.DrawMs).Position;
        else if (Array.IndexOf(s.Contacts.Entity, ship, 0, s.Contacts.Count) is int c and >= 0)
            p = s.Contacts.Position[c];
        else
        {
            p = default;
            return false;
        }
        return true;
    }

    // ------------------------------------------------------------------ the box (I1)

    bool boxing;   // the left press has moved a click's worth: its release selects what the box holds
    readonly List<int> boxShips = [];   // the ships in the box at its release (entities)
    sbyte[] boxWhole = new sbyte[16];   // per group (snapshot index): 0 not asked yet, 1 wholly in the box, -1 not

    /// <summary>A left press moved: past a click's worth, it's a box.</summary>
    void BoxMove()
    {
        if (pressed && !boxing && Math.Abs(cursorX - pressX) + Math.Abs(cursorY - pressY) >= ClickPx)
            boxing = true;
    }

    /// <summary>The box being drawn, from the press to the cursor: a hairline in the accent over a faint fill.</summary>
    void BoxMark()
    {
        if (!boxing)
            return;
        var vp = ImGui.GetMainViewport();
        float sx = vp.Size.X / (float)Math.Max(width, 1), sy = vp.Size.Y / (float)Math.Max(height, 1);
        var a = vp.Pos + new Vector2((float)(Math.Min(pressX, cursorX) * sx), (float)(Math.Min(pressY, cursorY) * sy));
        var b = vp.Pos + new Vector2((float)(Math.Max(pressX, cursorX) * sx), (float)(Math.Max(pressY, cursorY) * sy));
        var dl = ImGui.GetBackgroundDrawList();
        dl.AddRectFilled(a, b, Pieces.U(Theme.Brass with { W = 0.08f }));
        dl.AddRect(a, b, Pieces.U(Theme.Brass with { W = 0.8f }));
    }

    /// <summary>The box let go, from (x0, y0) to (x1, y1): the player's ships inside it selected. The selection is the
    /// guide of the largest unit wholly inside, its orders at that unit (a squadron or force inside entire goes as
    /// itself), the rest picked beside it; a squadron cut by the box goes as its ships alone. With ctrl the ships are
    /// added to the selection; an empty box deselects, as a click on the empty sea.</summary>
    void BoxSelect(double x0, double y0, double x1, double y1, bool ctrl)
    {
        var s = world.Current;
        var v = s.Ships;
        var gv = s.Groups;
        var view = View;
        (double l, double r, double t, double b) = (Math.Min(x0, x1), Math.Max(x0, x1), Math.Min(y0, y1), Math.Max(y0, y1));
        boxShips.Clear();
        for (int k = 0; k < v.Count; k++)
            if (Boxable(s, k) && view.Project(v.PoseAt(k, world.DrawMs).Position, out double px, out double py) && px >= l && px <= r && py >= t && py <= b)
                boxShips.Add(v.Entity[k]);
        if (ctrl && selected >= 0)
        {
            foreach (int e in boxShips)
                if (e != selected && !picked.Contains(e))
                    picked.Add(e);
            return;
        }
        if (boxWhole.Length < gv.Count)
            boxWhole = new sbyte[Math.Max(gv.Count, boxWhole.Length * 2)];
        Array.Clear(boxWhole, 0, gv.Count);
        // the senior: the largest unit wholly inside, and the ship that guides it
        int lead = -1, leadGroup = 0, leadShips = 0;
        foreach (int e in boxShips)
        {
            int g = gv.IndexOfShip(e), top = -1, ships = 1;
            while (g >= 0 && Whole(s, g, out int n))
            {
                (top, ships) = (g, n);
                g = gv.Parent[g] != 0 ? gv.IndexOfGroup(gv.Parent[g]) : -1;
            }
            if (ships <= leadShips)
                continue;
            int guide = top >= 0 ? gv.GuideShip[top] : e;
            (lead, leadGroup, leadShips) = (boxShips.Contains(guide) ? guide : e, top >= 0 ? gv.Id[top] : 0, ships);
        }
        selected = lead;
        ClearPicks();
        squadronCardOf = -1;
        if (lead < 0)
            return;
        foreach (int e in boxShips)
            if (e != lead)
                picked.Add(e);
        (levelFor, levelPick) = (lead, leadGroup);   // 0: the ship alone (Level); else the unit by its id
    }

    /// <summary>Whether the player's ship at snapshot row <paramref name="k"/> is one the box takes: hers, afloat.</summary>
    static bool Boxable(Snapshot s, int k) =>
        (s.PlayerSide < 0 || s.Ships.Side[k] == s.PlayerSide) && s.Ships.Loss[k] == LossKind.None;

    /// <summary>Whether every ship of group <paramref name="g"/> afloat is in the box (kept for the release), and how many.</summary>
    bool Whole(Snapshot s, int g, out int ships)
    {
        ships = 0;
        var v = s.Ships;
        for (int k = 0; k < v.Count; k++)
            if (Boxable(s, k) && Holds(s.Groups, g, Unit.Ship(v.Entity[k])))
            {
                ships++;
                if (boxWhole[g] == 0 && !boxShips.Contains(v.Entity[k]))
                    boxWhole[g] = -1;
            }
        if (boxWhole[g] == 0)
            boxWhole[g] = 1;
        return boxWhole[g] > 0;
    }

    /// <summary>The followed ship's place, the camera put over it: hers, or one in sight (nothing while she's neither).</summary>
    void KeepFollow(Snapshot s)
    {
        if (follow <= 0)
            return;
        if (s.Ships.IndexOf(follow) is int k and >= 0)
            camera.Target = s.Ships.PoseAt(k, world.DrawMs).Position.Normalized();
        else if (s.Sighted.IndexOf(follow) is int j and >= 0)
            camera.Target = s.Sighted.PoseAt(j, world.DrawMs).Position.Normalized();
    }
}
