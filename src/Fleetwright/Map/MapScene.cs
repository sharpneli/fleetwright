using System.Numerics;
using Fleetwright.Gpu;
using Fleetwright.Sim;
using Fleetwright.Sim.Core;
using ImGuiNET;
using SDL;
using static SDL.SDL3;

namespace Fleetwright.Map;

/// <summary>The world tab (docs/battle/plan.md step 1.2): the globe of water at every zoom from orbit down to 10 px/m,
/// the lat/lon grid, day and night from the snapshot's sun, and the clock's controls (pause, 1x to 1,000,000x, asked
/// against achieved). It reads the world only through <see cref="WorldSession.Current"/> and changes it only by
/// commands. Mouse: wheel zooms at the cursor, left drag pans. Keys: space pauses, + and - change the speed. The ships
/// (step 2.3) are in MapScene.Ships.cs.</summary>
public sealed unsafe partial class MapScene : IScene
{
    static readonly double[] Speeds = [1, 10, 100, 1000, 10_000, 30_000, 100_000, 300_000, 1_000_000];

    readonly WorldSession world;
    readonly GlobeRenderer renderer;
    readonly MapLines lines = new(), overlay = new();   // overlay: rebuilt every frame (routes, tracks, the selection)
    readonly LandView land;
    readonly SunLapse sunLapse = new();
    MapCamera camera, built;   // built: the view the lines and the land's fill were last built for
    (double, double) builtSize;
    double width = 1, height = 1;   // the target's size at the last draw, pixels
    bool dragging, clamped, dragPending;
    Vec3d grab;
    double dragX, dragY;   // the newest cursor of a drag: solved once a frame, however many motion events came
    Vector2 mouse;
    bool mouseIn;
    double toldScale;   // the scale last told the world, metres per pixel (in steps)

    public MapScene(SDL_GPUDevice* device, WorldSession world, MapCamera camera, ShipArtLibrary art, int selectEntity = -1)
    {
        this.world = world;
        this.camera = camera;
        this.art = art;
        selected = selectEntity;
        renderer = new GlobeRenderer(device);
        shipRenderer = new ShipBatchRenderer(device);
        artOf = art.Get;
        lengthOf = id => world.Classes.TryGet(id)?.Length ?? 150;
        designNames = [.. Directory.GetFiles(Path.Combine(AppContext.BaseDirectory, "Content", "Designs"), "*.json")
            .Select(Path.GetFileNameWithoutExtension).Order(StringComparer.Ordinal)!];
        static Land Load(string res) => Land.Load(Path.Combine(AppContext.BaseDirectory, "Content", "World", $"land-{res}.bin"));
        land = new LandView(Load("110m"), Load("50m"), world.Shore.Land);   // the finest is the world's own
        // the coarse levels baked once into static meshes; the finest is built per view (docs/battle/plan.md 1.4)
        renderer.UploadLand(LandMesh.Bake(land.Coarse), LandMesh.Bake(land.Medium));
    }

    /// <summary>Over the North Sea, the whole globe in view.</summary>
    public static MapCamera DefaultCamera => MapCamera.At(56.7, 5.8, 12_000);

    MapView View => new(camera, width, height);

    public void Draw(SDL_GPUCommandBuffer* cmd, RenderTarget target, float dt)
    {
        (width, height) = (target.Width, target.Height);
        NewWorld();
        if (!clamped)
        {
            camera.Clamp(height);
            clamped = true;
        }
        if (dragPending)
        {
            camera.Drag(grab, dragX, dragY, width, height);
            dragPending = false;
        }
        var view = View;
        var level = land.LevelFor(view);
        int baked = level == land.Coarse ? 0 : level == land.Medium ? 1 : -1;
        if (camera.Target != built.Target || camera.MetresPerPixel != built.MetresPerPixel || (width, height) != builtSize)
        {
            // the grid and the finest level's land change only with the view: a still map rebuilds nothing, and the
            // baked levels never do
            lines.Begin(width, height);
            Graticule.Build(view, lines);
            if (baked < 0)
                land.Build(view, lines);
            else
                land.Clear();
            (built, builtSize) = (camera, (width, height));
        }
        TellScale();
        var s = world.Current;
        sunLapse.Update(s.Paused ? 0 : s.AchievedSpeed, dt);
        art.UploadPending();
        PlaceGroups(view, s);
        shipRenderer.List.Build(view, s.Ships, s.Time.Ms, s.Sun, artOf, lengthOf, shipRenderer.Icon, selected, hidden, sunLapse.Motion);
        BuildOverlay(view, s);
        renderer.Render(cmd, target, view, s.Sun, land.Fill, baked, lines, overlay, shipRenderer, sunLapse.Motion);
    }

    /// <summary>Tells the world the map's scale when it changes, in quarter-octave steps (a zoom is a few commands, not
    /// one a frame): the rails place what the map shows as one marker more readily (ShipSystem.Rails.cs).
    /// <para>EXCEPTION: the sim is otherwise independent of what the map shows; this is the one display state sent to
    /// it, kept only until the rails' snap can be hidden better. Don't send any other.</para></summary>
    void TellScale()
    {
        double q = Math.Pow(2, Math.Round(Math.Log2(camera.MetresPerPixel) * 4) / 4);
        if (q == toldScale)
            return;
        toldScale = q;
        world.Send(Command.SetViewScale(q));
    }

    public void BuildUi()
    {
        NewWorld();
        ShipsUi();
        var s = world.Current;
        GroupLabels(s);
        var vp = ImGui.GetMainViewport();
        float pad = 8 * UiFonts.Scale;
        const ImGuiWindowFlags Hud = ImGuiWindowFlags.NoDecoration | ImGuiWindowFlags.AlwaysAutoResize |
                                     ImGuiWindowFlags.NoSavedSettings | ImGuiWindowFlags.NoFocusOnAppearing |
                                     ImGuiWindowFlags.NoNav | ImGuiWindowFlags.NoMove;

        // the clock: top centre
        ImGui.SetNextWindowPos(new Vector2(vp.WorkPos.X + vp.WorkSize.X / 2, vp.WorkPos.Y + pad), ImGuiCond.Always, new Vector2(0.5f, 0));
        ImGui.SetNextWindowBgAlpha(0.75f);
        if (ImGui.Begin("##clock", Hud))
        {
            ImGui.Text($"{s.Time} UTC");
            ImGui.SameLine();
            if (ImGui.Button(s.Paused ? "Run" : "Pause"))
                world.Send(s.Paused ? Command.Resume() : Command.Pause());
            foreach (double sp in Speeds)
            {
                ImGui.SameLine();
                bool on = !s.Paused && s.Speed == sp;
                if (on)
                    ImGui.PushStyleColor(ImGuiCol.Button, ImGui.GetColorU32(ImGuiCol.ButtonActive));
                if (ImGui.Button(SpeedLabel(sp)))
                {
                    world.Send(Command.SetSpeed(sp));
                    if (s.Paused)
                        world.Send(Command.Resume());
                }
                if (on)
                    ImGui.PopStyleColor();
            }
            ImGui.TextDisabled(s.Paused
                ? "paused"
                : $"asked {SpeedLabel(s.Speed)}, achieved {SpeedLabel(Math.Round(s.AchievedSpeed))}");
            if (s.Error != null)
                ImGui.TextColored(new Vector4(1, 0.4f, 0.3f, 1), "the sim stopped: " + s.Error.Split('\n')[0]);
        }
        ImGui.End();

        // where the map looks: bottom left
        var view = View;
        ImGui.SetNextWindowPos(new Vector2(vp.WorkPos.X + pad, vp.WorkPos.Y + vp.WorkSize.Y - pad), ImGuiCond.Always, new Vector2(0, 1));
        ImGui.SetNextWindowBgAlpha(0.6f);
        if (ImGui.Begin("##where", Hud))
        {
            double across = camera.MetresPerPixel * width;
            ImGui.Text($"{Band(across)} · {Distance(across)} across · {Scale(camera.MetresPerPixel)} · grid {Graticule.Label(Graticule.Spacing(camera.MetresPerPixel))}");
            var (lat, lon) = Earth.LatLon(camera.Target);
            if (sunLapse.Motion < 0.5)   // the hours would only churn
                ImGui.Text($"centre {LatLon(lat, lon)}, time-lapse sun, {s.AchievedSpeed / 86_400:0.#} days/s");
            else
            {
                var (_, sunLon) = Earth.LatLon(s.Sun);
                double solar = ((12 + (lon - sunLon) / 15) % 24 + 24) % 24;
                ImGui.Text($"centre {LatLon(lat, lon)}, local sun time {(int)solar:00}:{(int)(solar % 1 * 60):00}");
            }
            if (mouseIn && view.Pick(mouse.X * width / Math.Max(vp.Size.X, 1), mouse.Y * height / Math.Max(vp.Size.Y, 1), out var p))
            {
                var (la, lo) = Earth.LatLon(p);
                bool onLand = world.Shore.IsLand(p);
                ImGui.Text($"cursor {LatLon(la, lo)}{(onLand ? ", land" : "")}");
                if (onLand && ImGui.GetIO().KeyShift && selected >= 0 && !ImGui.GetIO().WantCaptureMouse)
                    ImGui.SetTooltip("land: no waypoint here");
            }
            else
                ImGui.TextDisabled("cursor off the globe");
        }
        ImGui.End();
    }

    public bool ProcessEvent(SDL_Event* e, uint w, uint h)
    {
        double sx = width / Math.Max(w, 1u), sy = height / Math.Max(h, 1u);   // window to target pixels
        switch ((SDL_EventType)e->type)
        {
            case SDL_EventType.SDL_EVENT_MOUSE_WHEEL:
                camera.ZoomAt(Math.Pow(1.2, -e->wheel.y), e->wheel.mouse_x * sx, e->wheel.mouse_y * sy, width, height);
                return true;
            case SDL_EventType.SDL_EVENT_MOUSE_BUTTON_DOWN:
                if (e->button.button == 3)
                {
                    OrderAt(e->button.x * sx, e->button.y * sy, (SDL_GetModState() & SDL_Keymod.SDL_KMOD_SHIFT) != 0);
                    return true;
                }
                if (e->button.button != 1)
                    return false;
                (pressX, pressY, pressed) = (e->button.x * sx, e->button.y * sy, true);
                dragging = View.Pick(e->button.x * sx, e->button.y * sy, out grab);
                return true;
            case SDL_EventType.SDL_EVENT_MOUSE_BUTTON_UP:
                if (e->button.button == 1 && pressed)
                {
                    double ux = e->button.x * sx, uy = e->button.y * sy;
                    if (Math.Abs(ux - pressX) + Math.Abs(uy - pressY) < 5)
                        Click(ux, uy);   // a click, not a drag
                    pressed = false;
                }
                dragging = false;
                return true;
            case SDL_EventType.SDL_EVENT_MOUSE_MOTION:
                (mouse, mouseIn) = (new Vector2(e->motion.x, e->motion.y), true);
                if (!dragging)
                    return false;
                (dragX, dragY, dragPending) = (e->motion.x * sx, e->motion.y * sy, true);
                return true;
            case SDL_EventType.SDL_EVENT_WINDOW_MOUSE_LEAVE:
                mouseIn = false;
                return false;
            case SDL_EventType.SDL_EVENT_KEY_DOWN:
                return Key((uint)e->key.key);
        }
        return false;
    }

    bool Key(uint key)
    {
        var s = world.Current;
        if (key == SDLK_SPACE)
            world.Send(s.Paused ? Command.Resume() : Command.Pause());
        else if (key is SDLK_EQUALS or SDLK_KP_PLUS or SDLK_MINUS or SDLK_KP_MINUS)
        {
            int i = Array.IndexOf(Speeds, s.Speed);
            i = i < 0 ? 0 : Math.Clamp(i + (key is SDLK_MINUS or SDLK_KP_MINUS ? -1 : 1), 0, Speeds.Length - 1);
            world.Send(Command.SetSpeed(Speeds[i]));
        }
        else
            return false;
        return true;
    }

    static string SpeedLabel(double s) => s >= 1_000_000 && s % 1_000_000 == 0 ? $"{s / 1_000_000:0}M×"
        : s >= 1000 && s % 1000 == 0 ? $"{s / 1000:0}k×" : $"{s:0.#}×";

    /// <summary>The map's zoom bands by the distance across the view (docs/map-ui/README.md).</summary>
    static string Band(double acrossM) => acrossM switch
    {
        >= 6_000_000 => "Globe",
        >= 1_000_000 => "Theatre",
        >= 50_000 => "Operations",
        >= 4_000 => "Tactical",
        _ => "Ship",
    };

    static string Distance(double m) => m >= 10_000 ? $"{m / 1000:#,0} km" : m >= 1000 ? $"{m / 1000:0.0} km" : $"{m:0} m";

    static string Scale(double mpp) => mpp >= 1 ? $"{mpp:#,0.#} m/px" : $"{1 / mpp:0.#} px/m";

    static string LatLon(double lat, double lon) =>
        $"{Math.Abs(lat):0.0000}°{(lat >= 0 ? 'N' : 'S')} {Math.Abs(lon):0.0000}°{(lon >= 0 ? 'E' : 'W')}";

    public void Dispose()
    {
        shipRenderer.Dispose();
        renderer.Dispose();
    }
}
