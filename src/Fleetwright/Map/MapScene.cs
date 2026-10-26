using System.Numerics;
using Fleetwright.Designer;
using Fleetwright.Gpu;
using Fleetwright.Sim;
using Fleetwright.Sim.Core;
using ImGuiNET;
using SDL;
using static SDL.SDL3;
using Fleetwright.Ui;

namespace Fleetwright.Map;

/// <summary>The world tab (docs/battle/plan.md step 1.2): the globe of water at every zoom from orbit down to 10 px/m,
/// the lat/lon grid, day and night from the snapshot's sun, and the clock's controls (pause, 1x to 1,000,000x, asked
/// against achieved). It reads the world only through <see cref="WorldSession.Current"/> and changes it only by
/// commands. Mouse: wheel zooms at the cursor, the left button selects (a click a ship, a drag a box), the right gives the order
/// the cursor names on its release (an armed verb's too; armed to place ships, its drag aims them), the middle drag pans
/// and its click centres or follows (docs/map-ui/input.md). Keys: WASD pans, space pauses, + and - change the speed. The ships
/// (step 2.3) are in MapScene.Ships.cs.</summary>
public sealed unsafe partial class MapScene : IScene
{
    readonly WorldSession world;
    readonly GlobeRenderer renderer;
    readonly MapLines lines = new(), overlay = new();   // overlay: rebuilt every frame (routes, tracks, the selection)
    readonly MapSymbols symbols = new();   // the overlay's symbols, over its lines (docs/map-ui/symbols.md Y3)
    readonly MapLabels mapLabels = new();   // every label on the map, placed together (Y5)
    readonly LandView land;
    readonly SunLapse sunLapse = new();
    readonly SeaView sea = new();
    MapCamera camera, built;   // built: the view the lines and the land's fill were last built for
    (double, double) builtSize;
    double builtBoundary, builtInset;   // WorldView.BoundaryM and the top bar's height (target px) the lines were built for
    readonly SquareGrid squares = new();   // the battle zooms' grid, and its anchor
    int builtGrid;   // GridLook.Version the lines were built for
    readonly List<Graticule.BorderTick> ticks = [];   // the grid at the screen's edge, from the blend band in
    readonly Dictionary<(double, double, bool), string> tickLabels = [];   // their labels, kept: a pan makes no garbage
    double width = 1, height = 1;   // the target's size at the last draw, pixels
    bool dragging, clamped, dragPending, middle;   // middle: the middle button is down (its drag pans)
    double middleX, middleY;   // where it went down
    double panX, panY;   // the cursor the camera was last panned to
    double dragX, dragY;   // the newest cursor of a drag: panned to once a frame, however many motion events came
    Vector2 mouse;
    bool mouseIn;
    double cursorX = -1, cursorY = -1;   // the cursor in target pixels

    // the map's parts, timed into the world's instruments (WorldPerf.Ui, docs/battle/plan.md step 2.7b)
    readonly int probeLines, probeUpload, probeGroups, probeDrawList, probeSplashes, probeOverlay, probeRender, probeUiShips, probeUiLabels, probeUiRest;

    public MapScene(SDL_GPUDevice* device, WorldSession world, MapCamera camera, ShipArtLibrary art, DesignSession designer, int selectEntity = -1)
    {
        this.world = world;
        this.camera = camera;
        this.art = art;
        this.designer = designer;
        selected = selectEntity;
        renderer = new GlobeRenderer(device);
        shipRenderer = new ShipBatchRenderer(device);
        artOf = art.Get;
        lengthOf = id => world.Classes.TryGet(id)?.Length ?? 150;
        classOf = world.Classes.TryGet;
        designNames = [.. Directory.GetFiles(Path.Combine(AppContext.BaseDirectory, "Content", "Designs"), "*.json")
            .Select(Path.GetFileNameWithoutExtension).Order(StringComparer.Ordinal)!];
        static Land Load(string res) => Land.Load(Path.Combine(AppContext.BaseDirectory, "Content", "World", $"land-{res}.bin"));
        land = new LandView(Load("110m"), Load("50m"), world.Shore.Land);   // the finest is the world's own
        // the coarse levels baked once into static meshes; the finest is built per view (docs/battle/plan.md 1.4)
        renderer.UploadLand(LandMesh.Bake(land.Coarse), LandMesh.Bake(land.Medium));
        var ui = world.Perf.Ui;
        probeLines = ui.Register("Grid and land lines");
        probeUpload = ui.Register("Art upload");
        probeGroups = ui.Register("Groups placed");
        probeDrawList = ui.Register("Ship draw list");
        probeSplashes = ui.Register("Splashes");
        probeOverlay = ui.Register("Overlay (tracks, routes)");
        probeRender = ui.Register("Globe render (CPU)");
        probeUiShips = ui.Register("UI: panels");
        probeUiLabels = ui.Register("UI: group labels");
        probeUiRest = ui.Register("UI: top bar, where, briefing");
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
        KeyPan(dt);
        if (dragPending)
        {
            camera.Pan(panX, panY, dragX, dragY, width, height);
            (panX, panY, dragPending, follow) = (dragX, dragY, false, 0);
        }
        KeepFollow(world.Current);   // a middle click's, or the dev's (-follow=ENTITY: benches and captures stay on a ship)
        var probes = world.Perf.Ui;
        long t = Probes.Now;
        var view = View;
        var level = land.LevelFor(view);
        int baked = level == land.Coarse ? 0 : level == land.Medium ? 1 : -1;
        var vp = ImGui.GetMainViewport();
        double inset = topBarH * height / Math.Max(vp.Size.Y, 1);   // the top edge's marks go under the top bar
        squares.Update(view, dt);
        if (camera.Target != built.Target || camera.MetresPerPixel != built.MetresPerPixel || (width, height) != builtSize
            || WorldView.BoundaryM != builtBoundary || inset != builtInset || squares.Fading || GridLook.Version != builtGrid)
        {
            // the grid and the finest level's land change only with the view: a still map rebuilds nothing, and the
            // baked levels never do
            lines.Begin(width, height);
            Graticule.Build(view, lines);
            squares.Build(view, lines);
            if (baked < 0)
                land.Build(view, lines);
            else
                land.Clear();
            Graticule.BorderTicks(view, ticks, lines, inset);
            (built, builtSize, builtBoundary, builtInset, builtGrid) = (camera, (width, height), WorldView.BoundaryM, inset, GridLook.Version);
        }
        probes.Add(probeLines, t);
        var s = world.Current;
        sunLapse.Update(s.Paused ? 0 : s.AchievedSpeed, dt);
        t = Probes.Now;
        art.UploadPending();
        probes.Add(probeUpload, t);
        t = Probes.Now;
        PlaceGroups(view, s);
        probes.Add(probeGroups, t);
        t = Probes.Now;
        (shipRenderer.List.IconCasingPx, shipRenderer.List.IconGlowPx) = (SymbolLook.Casing * 1.5 * Rp, 4 * Rp);
        shipRenderer.List.Build(view,s.Ships, world.DrawMs, s.Sun, artOf, classOf, shipRenderer.Icon, selected, hidden, sunLapse.Motion, s.Sighted,
            shipRenderer.Wakes);
        var now = new WorldTime(world.DrawMs);
        double moonHere = Moon.Light((-view.Forward).Normalized() * Earth.Radius, now);
        shipRenderer.List.SetHaze(HazeLook.Uniforms(view, s.Weather, world.Settings, shipRenderer.List.Lights, moonHere, WorldView.Factor(view)));
        probes.Add(probeDrawList, t);
        t = Probes.Now;
        BuildOverlay(view, s);
        probes.Add(probeOverlay, t);
        t = Probes.Now;
        // the flashes first: their lights go into the frame's, which the splashes, the ships and the sea read
        shipRenderer.Flashes.Build(view, s, world.DrawMs, s.Paused ? 0 : s.AchievedSpeed, dt, WorldView.Factor(view), shipRenderer.List.Lights, classOf, selected);
        shipRenderer.Blasts.Build(view, shipRenderer.Flashes.Book, s.Weather.WindMs, WorldView.Factor(view));
        // the magazines' fire adds to the flashes' lights and their blasts to the water's marks
        shipRenderer.Magazines.Build(view, s, world.DrawMs, s.Paused ? 0 : s.AchievedSpeed, dt, WorldView.Factor(view), shipRenderer.List.Lights, classOf,
            shipRenderer.Flashes.Lights, shipRenderer.Blasts, shipRenderer.Smoke.Book, shipRenderer.Flashes.Book.SeaClock);
        BuildHits(view, s, dt);
        sinking.Build(view, s, world.DrawMs, s.Paused ? 0 : s.AchievedSpeed, WorldView.Factor(view), classOf, shipRenderer.Splashes.Book,
            shipRenderer.Blasts, shipRenderer.Flashes.Book.SeaClock);
        smokeTrails.Begin();
        Map.Fires.FireEffects.Build(view, s, world.DrawMs, WorldView.Factor(view), shipRenderer.Flashes.Book.SeaClock, classOf, shipRenderer.Magazines,
            shipRenderer.Flashes.Lights, shipRenderer.Smoke.Book, smokeTrails);
        Map.Fires.FunnelSmoke.Build(view, s, world.DrawMs, WorldView.Factor(view), world.DrawMs / 1000.0, classOf, shipRenderer.Smoke.Book,
            smokeTrails, world.Settings);
        world.Perf.Effects(shipRenderer.Smoke.Book.Count, shipRenderer.Flashes.Lights.Count, shipRenderer.Flashes.Lights.Fires,
            shipRenderer.Splashes.Book.Count, smokeTrails.Count);
        shipRenderer.List.SetFlashLights(shipRenderer.Flashes.Lights.Block);
        shipRenderer.Smoke.Build(view, shipRenderer.Flashes.Book, world.DrawMs / 1000.0, s.Weather, WorldView.Factor(view), shipRenderer.List.Lights);
        shipRenderer.Field.Build(view, s, world.DrawMs, WorldView.Factor(view), shipRenderer.List.Lights);
        shipRenderer.Splashes.Build(view, s, world.DrawMs, s.Paused ? 0 : s.AchievedSpeed, dt, selected, WorldView.Factor(view), shipRenderer.List.Lights);
        shipRenderer.Torpedoes.Build(view, torpedoBook, Torpedoes.TorpedoLook.DrawMs(torpedoBook, world.DrawMs), WorldView.Factor(view),
            shipRenderer.List.Lights, classOf);
        probes.Add(probeSplashes, t);
        t = Probes.Now;
        renderer.Sea.SetState(SeaState.Of(s.Weather));   // rebuilt only when the weather changes
        var lights = shipRenderer.List.Lights;
        sea.Update(view, renderer.Sea, dt, s.Paused ? 0 : s.AchievedSpeed, lights, sunLapse.Motion, moonHere);
        shipRenderer.FoamClockS = sea.ClockS;
        shipRenderer.List.PlayerSide = world.Current.PlayerSide;
        renderer.Render(cmd, target, view, s.Sun, land.Fill, baked, lines, overlay, shipRenderer, sunLapse.Motion, sea, lights.Uniforms,
            GlobeRenderer.MoonUniform(now), symbols);
        probes.Add(probeRender, t);
    }


    public void BuildUi()
    {
        long allocated = GC.GetAllocatedBytesForCurrentThread();
        NewWorld();
        Theme.Push();
        U8.Frame();
        var probes = world.Perf.Ui;
        long t = Probes.Now;
        var s = world.Current;
        feed.Read(s, world.Generation);
        CollectEvents(s);
        WatchEnd(s);
        CardRequests(s);
        TopBar(s);
        Rail(s);
        ShipsUi();
        Card(s);
        Strip(s);
        LogDrawer(s);
        probes.Add(probeUiShips, t);
        t = Probes.Now;
        var lvp = ImGui.GetMainViewport();
        mapLabels.Begin(lvp.Pos, lvp.Size, 6 * UiFonts.Scale);
        SelectionMarks(s);
        BoxMark();
        ShipLabels(s);
        GroupLabels(s);
        TaskMarkLabels(s);
        ContactLabels(s);
        EncounterLabels(s);
        WreckLabels();
        mapLabels.Place(ImGui.GetIO().DeltaTime);
        DrawLabels(s);
        TickLabels();
        probes.Add(probeUiLabels, t);
        t = Probes.Now;
        if (Sdl3GpuEngine.StatsShown)
            world.Perf.Draw(s, art, renderer.OverlayVertices);
        StatusBar(s);
        CursorVerb(s);
        HoverTip(s);
        Briefing();
        Result(s);
        probes.Add(probeUiRest, t);
        Theme.Pop();
        world.Perf.UiBytes += GC.GetAllocatedBytesForCurrentThread() - allocated;
        world.Perf.UiFrames++;
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
                if (e->button.button == 2)
                {
                    // the middle button pans, off the globe too; its click centres or follows (MapScene.Input.cs)
                    (middleX, middleY, middle) = (e->button.x * sx, e->button.y * sy, true);
                    (panX, panY, dragging) = (e->button.x * sx, e->button.y * sy, true);
                    return true;
                }
                if (e->button.button is 4 or 5)
                {
                    // the side buttons: back a step slower, forward faster (docs/map-ui/input.md T0)
                    world.Send(Command.SetSpeed(StepSpeed(world.Current.Speed, e->button.button == 5 ? 1 : -1)));
                    return true;
                }
                if (e->button.button == 3)
                {
                    if (!BeginPlace(e->button.x * sx, e->button.y * sy))   // armed to place: the drag aims the ships
                        (rightHeld, rightX, rightY) = (true, e->button.x * sx, e->button.y * sy);   // the order goes on the release, drawn while held (MapScene.Input.cs)
                    return true;
                }
                if (e->button.button != 1)
                    return false;
                (pressX, pressY, pressed, boxing) = (e->button.x * sx, e->button.y * sy, true, false);   // a click, or a box (MapScene.Input.cs)
                return true;
            case SDL_EventType.SDL_EVENT_MOUSE_BUTTON_UP:
                if (e->button.button == 3 && placeDrag)
                {
                    EndPlace(e->button.x * sx, e->button.y * sy);
                    return true;
                }
                if (e->button.button == 3)
                {
                    if (rightHeld)
                    {
                        double ux = e->button.x * sx, uy = e->button.y * sy;
                        bool shift = (SDL_GetModState() & SDL_Keymod.SDL_KMOD_SHIFT) != 0;
                        if (!RightDragged(ux, uy) || !ShapedOrder(ux, uy, shift))
                            OrderAt(RightDragged(ux, uy) ? rightX : ux, RightDragged(ux, uy) ? rightY : uy, shift);
                    }
                    rightHeld = false;
                    return true;
                }
                if (e->button.button == 2 && middle)
                {
                    double mx = e->button.x * sx, my = e->button.y * sy;
                    if (Math.Abs(mx - middleX) + Math.Abs(my - middleY) < ClickPx)
                        MiddleClick(mx, my);
                    (middle, dragging) = (false, false);
                    return true;
                }
                if (e->button.button == 1 && pressed)
                {
                    double ux = e->button.x * sx, uy = e->button.y * sy;
                    if (boxing)
                        BoxSelect(pressX, pressY, ux, uy, (SDL_GetModState() & SDL_Keymod.SDL_KMOD_CTRL) != 0);
                    else
                        Click(ux, uy);
                    (pressed, boxing) = (false, false);
                }
                return true;
            case SDL_EventType.SDL_EVENT_MOUSE_MOTION:
                (mouse, mouseIn) = (new Vector2(e->motion.x, e->motion.y), true);
                (cursorX, cursorY) = (e->motion.x * sx, e->motion.y * sy);
                if (placeDrag)
                {
                    AimPlace(cursorX, cursorY);
                    return true;
                }
                BoxMove();
                if (!dragging)
                    return false;
                (dragX, dragY, dragPending) = (e->motion.x * sx, e->motion.y * sy, true);
                return true;
            case SDL_EventType.SDL_EVENT_WINDOW_MOUSE_LEAVE:
                mouseIn = false;
                return false;
            case SDL_EventType.SDL_EVENT_KEY_DOWN:
                if (e->key.scancode == FastKey)
                {
                    if (!e->key.repeat)
                        FastForward(true);
                    return true;
                }
                return Key((uint)e->key.key, e->key.scancode, e->key.mod);
            case SDL_EventType.SDL_EVENT_KEY_UP:
                if ((uint)e->key.key == SDLK_TAB)
                {
                    TabKey(false, (e->key.mod & SDL_Keymod.SDL_KMOD_SHIFT) != 0);
                    return true;
                }
                if (e->key.scancode == FastKey)
                {
                    FastForward(false);
                    return true;
                }
                return PanKey(e->key.scancode, false);
        }
        return false;
    }

    bool Key(uint key, SDL_Scancode code, SDL_Keymod mod)
    {
        var s = world.Current;
        if (key == SDLK_ESCAPE)
            return Escape();
        if (TaskKey(key))
            return true;   // the task menu's letters while it's open (docs/battle/admirals-orders.md O9)
        if (PanKey(code, true))
            return true;   // WASD pans, before any order's letter (docs/map-ui/input.md)
        if (StripKey(key))
            return true;   // an order's key while the strip is up (docs/map-ui/strip.md)
        if (key == SDLK_F1)
            devOpen = !devOpen;
        else if (key == SDLK_J)
            logOpen = !logOpen;
        else if (key == SDLK_V)
            cardOpen = !cardOpen;
        else if (key == SDLK_P)
            TogglePin(selected);
        else if (key == SDLK_TAB)
            TabKey(true, (mod & SDL_Keymod.SDL_KMOD_SHIFT) != 0);   // held: the fire lines; a tap: the next ship (MapScene.Targets.cs)
        else if (key == SDLK_SPACE && (mod & SDL_Keymod.SDL_KMOD_SHIFT) != 0)
            Run();   // run until it matters (MapScene.Clock.cs)
        else if (key == SDLK_SPACE)
            world.Send(s.Paused ? Command.Resume() : Command.Pause());
        else if (key is SDLK_EQUALS or SDLK_KP_PLUS or SDLK_MINUS or SDLK_KP_MINUS)
            world.Send(Command.SetSpeed(StepSpeed(s.Speed, key is SDLK_MINUS or SDLK_KP_MINUS ? -1 : 1)));
        else if (SpeedKey(key) is var i and >= 0)
        {
            // as the bar's button: the gear's speed, and a paused clock runs at it
            world.Send(Command.SetSpeed(Gears[i]));
            if (s.Paused)
                world.Send(Command.Resume());
        }
        else
            return false;
        return true;
    }

    /// <summary>The gear a number key picks: 1 to 5 (the keypad's too); -1 for any other key (6 to 0 are free,
    /// docs/map-ui/input.md).</summary>
    static int SpeedKey(uint key) => key switch
    {
        >= SDLK_1 and <= SDLK_5 => (int)(key - SDLK_1),
        >= SDLK_KP_1 and <= SDLK_KP_5 => (int)(key - SDLK_KP_1),
        _ => -1,
    };

    /// <summary>A scale bar at the length of a round number (in the map's unit, <see cref="MapUnits"/>) near 120 UI pixels,
    /// with its length after it, ending at <paramref name="right"/>: where it starts.</summary>
    static float ScaleBar(ImDrawListPtr dl, float right, float midY, double metresPerUiPx)
    {
        double want = 120 * UiFonts.Scale * metresPerUiPx, unit = MapUnits.Base(want);
        double pow = Math.Pow(10, Math.Floor(Math.Log10(want / unit))) * unit, nice = pow;
        foreach (double k in NiceSteps)
            if (k * pow <= want)
                nice = k * pow;
        if (nice != scaleNice)
            (scaleNice, scaleText) = (nice, MapUnits.Distance(nice));
        var font = UiFonts.MonoSmall;
        dl.TextRight(font, right, Pieces.Mid(font, midY - Pieces.P(10), Pieces.P(20)), Theme.Text, scaleText);
        float w = (float)(nice / metresPerUiPx), hgt = Pieces.P(5), x = right - Pieces.Width(font, scaleText) - Pieces.P(6) - w;
        var p = new Vector2(x, midY - hgt / 2);
        uint c = Pieces.U(Theme.Text);
        dl.AddLine(p, p + new Vector2(0, hgt), c, 1.5f);
        dl.AddLine(p + new Vector2(0, hgt), p + new Vector2(w, hgt), c, 1.5f);
        dl.AddLine(p + new Vector2(w, 0), p + new Vector2(w, hgt), c, 1.5f);
        return x;
    }

    static readonly double[] NiceSteps = [1, 2, 5];
    static double scaleNice = -1;
    static string scaleText = "";

    /// <summary>The grid's values at the screen's edge (<see cref="Graticule.Margin"/>), beside <see cref="Graticule.BorderTicks"/>'
    /// marks: those far enough apart (<see cref="GridLook.Label"/>), faded in with them and with the margin, latitudes at the sides and longitudes at the top
    /// and bottom; a label that would overlap the last one on its
    /// edge is left out.</summary>
    void TickLabels()
    {
        if (ticks.Count == 0)
            return;
        var vp = ImGui.GetMainViewport();
        float sx = vp.Size.X / (float)Math.Max(width, 1), sy = vp.Size.Y / (float)Math.Max(height, 1);
        var dl = ImGui.GetBackgroundDrawList();
        float wf = (float)Graticule.Margin(View);
        if (tickLabels.Count > 1024)
            tickLabels.Clear();
        float pad = 13 * UiFonts.Scale, lh = ImGui.GetTextLineHeight();
        Span<float> last = stackalloc float[4];
        last.Fill(float.MinValue);
        foreach (var t in ticks)
        {
            float a = wf * t.Fade * t.Label;
            if (a < 0.02f || t.Parallel != (t.Edge is 1 or 3))
                continue;   // as on a chart: latitudes at the sides, longitudes at the top and bottom
            var key = (t.Value, t.Step, t.Parallel);
            if (!tickLabels.TryGetValue(key, out var text))
                tickLabels[key] = text = Graticule.Format(t.Value, t.Step, t.Parallel);
            var size = ImGui.CalcTextSize(text);
            float x = vp.Pos.X + (float)(t.X * sx), y = vp.Pos.Y + (float)(t.Y * sy);
            var at = t.Edge switch
            {
                0 => new Vector2(x - size.X / 2, y + pad),
                1 => new Vector2(x - pad - size.X, y - lh / 2),
                2 => new Vector2(x - size.X / 2, y - pad - lh),
                _ => new Vector2(x + pad, y - lh / 2),
            };
            bool alongX = t.Edge is 0 or 2;
            float start = alongX ? at.X : at.Y, end = alongX ? at.X + size.X : at.Y + lh;
            if (start < last[t.Edge] + 8)
                continue;
            last[t.Edge] = end;
            dl.AddText(at + new Vector2(1, 1), ImGui.GetColorU32(new Vector4(0, 0, 0, 0.75f * a)), text);
            dl.AddText(at, ImGui.GetColorU32(MapPalette.Label with { W = 0.85f * a }), text);
        }
    }

    /// <summary>The map's zoom bands by the distance across the view (docs/map-ui/README.md).</summary>
    static string Band(double acrossM) => acrossM switch
    {
        >= 6_000_000 => "Globe",
        >= 1_000_000 => "Theatre",
        >= 50_000 => "Operations",
        >= 4_000 => "Tactical",
        _ => "Ship",
    };

    static string Scale(double mpp) => mpp >= 1 ? $"{mpp:#,0.#} m/px" : $"{1 / mpp:0.#} px/m";

    static string LatLon(double lat, double lon) =>
        $"{Math.Abs(lat):0.0000}°{(lat >= 0 ? 'N' : 'S')} {Math.Abs(lon):0.0000}°{(lon >= 0 ? 'E' : 'W')}";

    public void Dispose()
    {
        shipRenderer.Dispose();
        renderer.Dispose();
    }
}
