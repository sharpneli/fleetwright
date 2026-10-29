using System.Numerics;
using Fleetwright.Gpu;
using Fleetwright.HitView;
using Fleetwright.Shipgen;
using Fleetwright.Shipgen.Render;
using ImGuiNET;
using SDL;

namespace Fleetwright;

/// <summary>The ship viewer (PORTING.md Step 6), a test tool: shows the session's design from its baked textures as
/// the game will (<see cref="ShipSpriteRenderer"/>). Mips, turrets turning through their arcs, turret shadows and the
/// sun's shadow from the height map (a first version of the game's shadow shader). The ImGui panel picks the design,
/// the look, the mip level, the turrets, the sun and the moon, and edits the light table live (the map's lights too:
/// the bench for docs/visuals/ship-lighting.md). Mouse: wheel zooms, left drag pans.</summary>
public sealed unsafe class ShipViewer : IScene
{
    /// <summary>Sprite pixels per metre at mip level 0, and the mip levels below it.</summary>
    public const double Scale = 10.0;
    public const int MipLevels = 5;
    static readonly Vector4 Sea = new(0x2d / 255f, 0x5a / 255f, 0x73 / 255f, 1f);

    readonly SDL_GPUDevice* device;
    readonly ShipSpriteRenderer renderer;

    // what is shown: the session's ship in a look
    readonly DesignSession session;
    int shownVersion = -1;   // the session version the textures are from
    int navyIndex, eraIndex;   // 0 = the design's own
    readonly string[] navies, eras;
    string status = "";

    // view and controls
    ShipSpriteView view = ShipSpriteView.Default;
    bool fit = true;
    int turretMode = 1;   // rest, sweep, starboard
    double clock;
    double[] bearings = [];   // each mount's bearing now, slewing towards its target
    bool bearingsSet;
    (double Lo, double Hi)[][] mountArcs = [];   // each mount's arcs, made once per ship so a frame allocates nothing
    const double TraverseDegPerS = 45;   // faster than the sweep, so a turret catches up after a blind arc
    bool dragging;   // the middle button's
    readonly Map.PanKeys panKeys = new();   // WASD, as every view's

    // the hitbox inset: the hitbox renderer drawing into a small target of its own, shown in the panel, turning
    HitboxRenderer? insetRenderer;
    RenderTarget? insetTarget;
    HitboxCamera insetCam = new() { Elevation = 25 };
    readonly HitboxViewState insetState = HitboxViewState.All;
    int insetVersion = -1;
    bool inset;
    const uint InsetW = 320, InsetH = 180;

    /// <summary>The view's settings for captures (the -viewer= flag): sun:AZ:EL, moon:AZ:EL:PHASE, shade:NAME (lit,
    /// albedo, normal, ao, roughness, metallic), at:X:Y:ZOOM (ship metres at the centre, px/m), mip:N.</summary>
    public static string? Dev;

    void ApplyDev()
    {
        foreach (var item in (Dev ?? "").Split(',', StringSplitOptions.RemoveEmptyEntries))
        {
            var p = item.Split(':');
            float F(int i) => float.Parse(p[i], System.Globalization.CultureInfo.InvariantCulture);
            switch (p[0])
            {
                case "sun": (view.SunAzimuthDeg, view.SunElevationDeg) = (F(1), F(2)); break;
                case "moon": (view.MoonAzimuthDeg, view.MoonElevationDeg, view.MoonPhaseDeg) = (F(1), F(2), F(3)); break;
                case "shade": view.Show = ShowViews[Math.Max(0, Array.FindIndex(ShowNames, n => n.Equals(p[1], StringComparison.OrdinalIgnoreCase)))]; break;
                case "at": (view.Centre, view.Zoom, fit) = (new Vector2(F(1), F(2)), F(3), false); break;
                case "mip": view.Mip = (int)F(1); break;
                default: Console.Error.WriteLine($"Bad -viewer item: {item}"); break;
            }
        }
    }

    public ShipViewer(SDL_GPUDevice* device, DesignSession session, string? navy = null, string? era = null)
    {
        ApplyDev();
        this.device = device;
        this.session = session;
        navies = ["(design)", .. Shipgen.Render.Looks.Navies];
        eras = ["(design)", .. Shipgen.Render.Looks.Eras];
        navyIndex = navy != null ? Math.Max(0, Array.IndexOf(navies, navy)) : 0;
        eraIndex = era != null ? Math.Max(0, Array.IndexOf(eras, era)) : 0;
        renderer = new ShipSpriteRenderer(device);
        if (navyIndex > 0 || eraIndex > 0)
        {
            session.SetLook(Look());
            session.WaitIdle();
        }
    }

    LookInput? Look() => navyIndex > 0 || eraIndex > 0
        ? new LookInput { Navy = navyIndex > 0 ? navies[navyIndex] : null, Era = eraIndex > 0 ? eras[eraIndex] : null }
        : null;

    /// <summary>Takes the session's newest result: its textures, and the mounts' arcs.</summary>
    void Show()
    {
        shownVersion = session.Version;
        if (session.Result is not { } r)
            return;
        renderer.Upload(r);
        if (renderer.Meta is not { } meta)
        {
            status = session.Status;
            return;
        }
        var mounts = meta.Mounts;
        mountArcs = mounts.Select(m => m.ArcsDeg.Select(a => (Lo: a[0], Hi: a[1])).ToArray()).ToArray();
        if (bearings.Length != mounts.Count)
            bearings = new double[mounts.Count];
        bearingsSet = false;
        var size = meta.SizePx;
        status = $"{meta.Name}\n{size[0]} x {size[1]} px, {r.Sprites!.Turrets.Count} turret types, {r.Sprites.Clutter.Count} clutter items\n" +
                 r.Status;
        Console.WriteLine($"viewer: {r.Design.Id}: {status.Replace('\n', ';')}");
    }

    /// <summary>Turns each turret towards its target at the traverse rate, the way round that stays inside its arcs
    /// (a turret never swings through the superstructure). A new ship starts with every turret on its target.</summary>
    void Traverse(float dt)
    {
        var mounts = renderer.Meta!.Mounts;
        for (int i = 0; i < mounts.Count; i++)
        {
            var m = mounts[i];
            var arcs = mountArcs[i];
            double target = Target(m, arcs);
            if (!bearingsSet)
            {
                bearings[i] = target;
                continue;
            }
            double now = bearings[i];
            double ccw = Geometry.Normalize360(target - now), cw = ccw - 360;
            if (ccw == 0)
                continue;
            // the shorter way, unless it crosses a blind arc (only the path's inside is tested: both ends are allowed)
            var (a, b) = ccw <= -cw ? (ccw, cw) : (cw, ccw);
            double delta = Clear(arcs, now, a) || !Clear(arcs, now, b) ? a : b;
            double step = TraverseDegPerS * dt;
            bearings[i] = Math.Abs(delta) <= step ? target : Geometry.Normalize360(now + Math.Sign(delta) * step);
        }
        bearingsSet = true;
    }

    /// <summary>Where the turret is heading: its rest angle, a bearing circling the ship (stopping at the arcs' ends),
    /// or the nearest allowed to starboard.</summary>
    double Target(SpriteMount m, (double Lo, double Hi)[] arcs)
    {
        double target = turretMode switch
        {
            0 => m.RestDeg,
            1 => Geometry.Wrap180(clock * 24.0),
            _ => 90.0,
        };
        if (turretMode == 0 || arcs.Length == 0 || Geometry.AngleAllowed(arcs, target))
            return target;
        double best = target, bd = 1e9;   // geometry.nearest_allowed
        foreach (var (lo, hi) in arcs)
        {
            double dl = Math.Abs(Geometry.Wrap180(lo - target)), dh = Math.Abs(Geometry.Wrap180(hi - target));
            if (dl < bd)
                (best, bd) = (lo, dl);
            if (dh < bd)
                (best, bd) = (hi, dh);
        }
        return best;
    }

    /// <summary>Does turning d degrees from now stay inside the arcs? Only the path's inside is tested.</summary>
    static bool Clear((double Lo, double Hi)[] arcs, double now, double d)
    {
        if (arcs.Length == 0)
            return true;
        int n = (int)Math.Ceiling(Math.Abs(d));
        for (int s = 1; s < n; s++)
            if (!Geometry.AngleAllowed(arcs, now + d * s / n))
                return false;
        return true;
    }

    public void Draw(SDL_GPUCommandBuffer* cmd, RenderTarget target, float dt)
    {
        if (shownVersion != session.Version)
            Show();
        if (ImGui.GetIO().WantTextInput)
            panKeys.Release();
        var (kx, ky) = panKeys.Step(dt, target.Height, (SDL3.SDL_GetModState() & SDL_Keymod.SDL_KMOD_SHIFT) != 0);
        view.Centre += new Vector2((float)kx, (float)ky) / view.Zoom;
        clock += dt;
        if (renderer.Meta != null)
        {
            Traverse(dt);
            if (fit)
            {
                view.Zoom = renderer.FitZoom(target.Width, target.Height);
                view.Centre = Vector2.Zero;
                fit = false;
            }
        }
        if (inset)
            DrawInset(cmd, dt);
        float level = view.Lit ? view.Lights().Uniforms.Sky.W : 1;   // the sea darkens with the light
        renderer.Render(cmd, target, view, bearings, new SDL_FColor { r = Sea.X * level, g = Sea.Y * level, b = Sea.Z * level, a = 1 });
    }

    /// <summary>The hitbox inset: the session's ship as hitboxes, drawn into its own target before the main pass.</summary>
    void DrawInset(SDL_GPUCommandBuffer* cmd, float dt)
    {
        insetRenderer ??= new HitboxRenderer(device);
        insetTarget ??= new RenderTarget(device, InsetW, InsetH, SDL_GPUTextureFormat.SDL_GPU_TEXTUREFORMAT_R8G8B8A8_UNORM,
            SDL_GPUSampleCount.SDL_GPU_SAMPLECOUNT_4);
        if (insetVersion != session.Version)
        {
            insetVersion = session.Version;
            if (session.Result?.Mesh is { } mesh)
            {
                insetRenderer.Upload(mesh);
                insetCam.Bearing = 90;   // fitted broadside, its widest, so it stays in view as it turns
                insetCam.Fit(mesh.Vertices, (float)InsetW / InsetH);
            }
            else
                insetRenderer.Clear();
        }
        insetCam.Bearing = (insetCam.Bearing + 20 * dt) % 360;
        insetRenderer.Render(cmd, insetTarget, insetCam, insetState);
    }

    // ------------------------------------------------------------------ UI and input

    public void BuildUi()
    {
        SidePanel.Begin("Ship");
        // the look may have been changed in another scene (the designer's style button)
        navyIndex = Math.Max(0, Array.IndexOf(navies, session.Look?.Navy));
        eraIndex = Math.Max(0, Array.IndexOf(eras, session.Look?.Era));
        int designIndex = session.Index;
        if (ImGui.Combo("design", ref designIndex, session.Names, session.Names.Length))
        {
            session.Select(designIndex);
            fit = true;
        }
        if (session.Edited)
            ImGui.TextColored(new Vector4(0.88f, 0.68f, 0.31f, 1), "edited in the designer (rebuild reloads the file)");
        if (ImGui.Combo("navy", ref navyIndex, navies, navies.Length) | ImGui.Combo("era", ref eraIndex, eras, eras.Length))
            session.SetLook(Look());
        ImGui.Separator();
        ImGui.SliderInt("mip", ref view.Mip, -1, MipLevels, view.Mip < 0 ? "auto" : "%d");
        ImGui.Text("turrets");
        ImGui.SameLine();
        ImGui.RadioButton("rest", ref turretMode, 0);
        ImGui.SameLine();
        ImGui.RadioButton("sweep", ref turretMode, 1);
        ImGui.SameLine();
        ImGui.RadioButton("starboard", ref turretMode, 2);
        ImGui.Checkbox("shadows", ref view.Shadows);
        ImGui.Checkbox("lit", ref view.Lit);
        if (view.Lit)
        {
            ImGui.SameLine();
            int show = Array.IndexOf(ShowViews, view.Show);
            ImGui.SetNextItemWidth(120);
            if (ImGui.Combo("show", ref show, ShowNames, ShowNames.Length))
                view.Show = ShowViews[show];
        }
        ImGui.SliderFloat("sun bearing", ref view.SunAzimuthDeg, 0, 360, "%.0f");
        ImGui.SliderFloat("sun elevation", ref view.SunElevationDeg, -20, 90, "%.0f");
        ImGui.SliderFloat("moon bearing", ref view.MoonAzimuthDeg, 0, 360, "%.0f");
        ImGui.SliderFloat("moon elevation", ref view.MoonElevationDeg, -10, 90, "%.0f");
        ImGui.SliderFloat("moon phase", ref view.MoonPhaseDeg, 0, 180, view.MoonPhaseDeg < 1 ? "full" : view.MoonPhaseDeg > 179 ? "new" : "%.0f");
        LightTableUi(LightTable.Current);
        if (ImGui.Button("fit"))
            fit = true;
        ImGui.SameLine();
        if (ImGui.Button("rebuild"))
            session.Rebuild();
        ImGui.Checkbox("hitbox inset", ref inset);
        if (inset && insetTarget != null)
            ImGui.Image((nint)insetTarget.Resolve, new Vector2(InsetW, InsetH));
        ImGui.Separator();
        ImGui.TextWrapped(session.Busy ? "building...\n" + status : status);
        ImGui.End();
    }

    static readonly ShadeView[] ShowViews = [ShadeView.Lit, ShadeView.Albedo, ShadeView.Normal, ShadeView.Ao, ShadeView.Roughness, ShadeView.Metallic];
    static readonly string[] ShowNames = ["lit", "albedo", "normal", "AO", "roughness", "metallic"];

    /// <summary>The light table's knobs (the map reads the same table, so a change shows there too).</summary>
    static void LightTableUi(LightTable t)
    {
        if (!ImGui.CollapsingHeader("light table"))
            return;
        ImGui.ColorEdit3("sun overhead", ref t.SunZenith, ImGuiColorEditFlags.Float | ImGuiColorEditFlags.HDR);
        ImGui.SliderFloat3("extinction", ref t.Extinction, 0, 1, "%.2f");
        ImGui.ColorEdit3("sky, day", ref t.SkyDay, ImGuiColorEditFlags.Float);
        ImGui.ColorEdit3("sea, day", ref t.SeaDay, ImGuiColorEditFlags.Float);
        ImGui.ColorEdit3("sky, dusk", ref t.SkyDusk, ImGuiColorEditFlags.Float);
        ImGui.ColorEdit3("sea, dusk", ref t.SeaDusk, ImGuiColorEditFlags.Float);
        ImGui.ColorEdit3("sky, night", ref t.SkyNight, ImGuiColorEditFlags.Float);
        ImGui.ColorEdit3("sea, night", ref t.SeaNight, ImGuiColorEditFlags.Float);
        ImGui.SliderFloat("night eye x", ref t.EyeGain, 0.1f, 5, "%.2f");
        ImGui.SliderFloat("purkinje", ref t.Purkinje, 0, 1, "%.2f");
        ImGui.ColorEdit3("moon tint", ref t.MoonTint, ImGuiColorEditFlags.Float);
        ImGui.SliderFloat("moonlit sky", ref t.MoonSky, 0, 1, "%.2f");
        ImGui.SliderFloat("roughness x", ref t.Roughness, 0.1f, 2, "%.2f");
        ImGui.SliderFloat("horizon", ref t.Horizon, 0.5f, 3, "%.2f");
        ImGui.SliderFloat("specular", ref t.Specular, 0, 2, "%.2f");
        ImGui.SliderFloat("shoulder knee", ref t.Knee, 0.5f, 0.99f, "%.2f");
        ImGui.SliderFloat("shadow", ref t.Shadow, 0, 1.5f, "%.2f");
        var s = t.SunZenith + t.SkyDay;
        ImGui.TextDisabled($"sun + day sky: {s.X:0.00} {s.Y:0.00} {s.Z:0.00} (1: a flat matte deck at noon as drawn)");
    }

    /// <summary>The map's grammar (docs/map-ui/input.md, the controls unified): the wheel zooms about the cursor, the
    /// middle drag and WASD pan. False when the event isn't the viewer's.</summary>
    public bool ProcessEvent(SDL_Event* e, uint w, uint h)
    {
        switch ((SDL_EventType)e->type)
        {
            case SDL_EventType.SDL_EVENT_MOUSE_WHEEL:
                {
                    var at = new Vector2(e->wheel.mouse_x - w / 2f, e->wheel.mouse_y - h / 2f);
                    var before = view.Centre + at / view.Zoom;
                    view.Zoom *= MathF.Pow(1.15f, e->wheel.y);
                    view.Centre = before - at / view.Zoom;
                    return true;
                }
            case SDL_EventType.SDL_EVENT_KEY_DOWN or SDL_EventType.SDL_EVENT_KEY_UP:
                return panKeys.Key(e->key.scancode, (SDL_EventType)e->type == SDL_EventType.SDL_EVENT_KEY_DOWN);
            case SDL_EventType.SDL_EVENT_MOUSE_BUTTON_DOWN:
                dragging = e->button.button == 2;
                return dragging;
            case SDL_EventType.SDL_EVENT_MOUSE_BUTTON_UP:
                dragging = false;
                return true;
            case SDL_EventType.SDL_EVENT_MOUSE_MOTION:
                if (!dragging)
                    return false;
                view.Centre -= new Vector2(e->motion.xrel, e->motion.yrel) / view.Zoom;
                return true;
        }
        return false;
    }

    public void Dispose()
    {
        renderer.Dispose();
        insetRenderer?.Dispose();
        insetTarget?.Dispose();
    }
}
