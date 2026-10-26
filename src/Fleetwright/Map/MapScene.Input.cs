using System.Numerics;
using Fleetwright.Sim;
using Fleetwright.Sim.Ai;
using Fleetwright.Sim.Combat;
using Fleetwright.Ui;
using ImGuiNET;
using SDL;
using static SDL.SDL3;

namespace Fleetwright.Map;

/// <summary>The camera's input (docs/map-ui/input.md I0): WASD pans at a share of the screen a second whatever the
/// zoom (shift faster), eased so a tap nudges; the middle button drags the map, and its click centres the map on the
/// sea or follows a ship until a pan. The keys are scancodes, so they stay under the hand on any layout. The left
/// button only selects (I1): a click a ship, a drag a box.</summary>
public sealed unsafe partial class MapScene
{
    static readonly SDL_Scancode[] PanKeys = [SDL_Scancode.SDL_SCANCODE_W, SDL_Scancode.SDL_SCANCODE_A, SDL_Scancode.SDL_SCANCODE_S, SDL_Scancode.SDL_SCANCODE_D];
    static readonly (int X, int Y)[] PanWays = [(0, -1), (-1, 0), (0, 1), (1, 0)];

    const double PanScreens = 0.8;   // the keys' pan, screen heights a second
    const double PanFast = 3;        // with shift
    const double PanEaseS = 0.08;    // the pan's time constant, seconds: a tap nudges, a hold glides

    readonly bool[] panHeld = new bool[4];   // W A S D, held from their key's down to its up
    double panVX, panVY;                      // the keys' pan now, target pixels a second
    int follow;                               // the camera kept on this ship (an entity; 0 none)

    /// <summary>A key down or up: one of the pan keys holds its way (true) from its down to its up. SDL sends the ups
    /// of held keys when the window loses the keyboard; a text field taking it lets go of them all (<see cref="KeyPan"/>).</summary>
    bool PanKey(SDL_Scancode code, bool down)
    {
        int i = Array.IndexOf(PanKeys, code);
        if (i < 0)
            return false;
        panHeld[i] = down;
        return true;
    }

    /// <summary>The keys' pan for this frame: their ways summed, eased toward, and the map dragged by it. A pan lets go
    /// of a followed ship.</summary>
    void KeyPan(double dt)
    {
        dt = Math.Min(dt, 0.1);   // a stalled frame doesn't throw the map
        if (ImGui.GetIO().WantTextInput)
            Array.Clear(panHeld);   // typing: the ups go to the field
        int wx = 0, wy = 0;
        for (int i = 0; i < PanKeys.Length; i++)
            if (panHeld[i])
                (wx, wy) = (wx + PanWays[i].X, wy + PanWays[i].Y);
        double speed = PanScreens * height * ((SDL_GetModState() & SDL_Keymod.SDL_KMOD_SHIFT) != 0 ? PanFast : 1);
        double k = 1 - Math.Exp(-dt / PanEaseS);
        panVX += (wx * speed - panVX) * k;
        panVY += (wy * speed - panVY) * k;
        if (wx == 0 && wy == 0 && Math.Abs(panVX) + Math.Abs(panVY) < 1)
            (panVX, panVY) = (0, 0);
        if (panVX == 0 && panVY == 0)
            return;
        double cx = width / 2, cy = height / 2;
        camera.Pan(cx, cy, cx - panVX * dt, cy - panVY * dt, width, height);
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
