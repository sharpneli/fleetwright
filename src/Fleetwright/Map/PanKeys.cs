using SDL;

namespace Fleetwright.Map;

/// <summary>The camera keys' pan (docs/map-ui/input.md): W A S D held, by scancode so they stay under the hand on any
/// layout, at a share of the view's height a second whatever the zoom (shift faster), eased so a tap nudges. One
/// implementation for every view that pans (the battle map, the launch screen's opening): the keys are the same
/// everywhere (docs/map-ui/input.md, controls unified).</summary>
public sealed class PanKeys
{
    static readonly SDL_Scancode[] Keys = [SDL_Scancode.SDL_SCANCODE_W, SDL_Scancode.SDL_SCANCODE_A, SDL_Scancode.SDL_SCANCODE_S, SDL_Scancode.SDL_SCANCODE_D];
    static readonly (int X, int Y)[] Ways = [(0, -1), (-1, 0), (0, 1), (1, 0)];

    const double Screens = 0.8;   // view heights a second
    const double Fast = 3;        // with shift
    const double EaseS = 0.08;    // the time constant, seconds: a tap nudges, a hold glides

    readonly bool[] held = new bool[4];   // W A S D, held from their key's down to its up
    double vx, vy;                        // the pan now, pixels a second

    /// <summary>A key down or up: one of the pan keys holds its way (true) from its down to its up. SDL sends the ups
    /// of held keys when the window loses the keyboard.</summary>
    public bool Key(SDL_Scancode code, bool down)
    {
        int i = Array.IndexOf(Keys, code);
        if (i < 0)
            return false;
        held[i] = down;
        return true;
    }

    /// <summary>Every key let go (a text field took the keyboard: the ups go to it).</summary>
    public void Release() => Array.Clear(held);

    /// <summary>This frame's pan, pixels the view moves (x right, y down), for a view <paramref name="heightPx"/> tall;
    /// (0, 0) once it has eased to a stop.</summary>
    public (double X, double Y) Step(double dt, double heightPx, bool fast)
    {
        dt = Math.Min(dt, 0.1);   // a stalled frame doesn't throw the view
        int wx = 0, wy = 0;
        for (int i = 0; i < Keys.Length; i++)
            if (held[i])
                (wx, wy) = (wx + Ways[i].X, wy + Ways[i].Y);
        double speed = Screens * heightPx * (fast ? Fast : 1);
        double k = 1 - Math.Exp(-dt / EaseS);
        vx += (wx * speed - vx) * k;
        vy += (wy * speed - vy) * k;
        if (wx == 0 && wy == 0 && Math.Abs(vx) + Math.Abs(vy) < 1)
            (vx, vy) = (0, 0);
        return (vx * dt, vy * dt);
    }
}
