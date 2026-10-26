using Fleetwright.Sim;
using ImGuiNET;
using SDL;
using static SDL.SDL3;

namespace Fleetwright.Map;

/// <summary>The camera's input (docs/map-ui/input.md I0): WASD pans at a share of the screen a second whatever the
/// zoom (shift faster), eased so a tap nudges; the middle button drags the map, and its click centres the map on the
/// sea or follows a ship until a pan. The keys are scancodes, so they stay under the hand on any layout.</summary>
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
