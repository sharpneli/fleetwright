using System.Diagnostics;
using System.Numerics;
using Fleetwright.Gpu;
using ImGuiNET;
using SDL;

namespace Fleetwright.HitView;

/// <summary>
/// The hitbox viewer: the session's ship as its hitbox model in 3D (hitview.py, live). Left drag orbits, right drag
/// pans, the wheel zooms. The panel picks the design, a view (hitview's bow, quarter, side and internal, and plan),
/// the projection, and which kinds are drawn, alone or by preset. Draws through a <see cref="HitboxRenderer"/>,
/// which could as well draw into a small target inside another scene.
/// </summary>
public sealed unsafe class HitboxScene : IScene
{
    readonly DesignSession session;
    readonly HitboxRenderer renderer;
    HitboxCamera cam;
    HitboxViewState state = HitboxViewState.All;
    int builtVersion = -1;
    bool frame = true;   // fit the camera to the ship at the next draw
    float aspect = 16f / 9;
    string status = "";
    bool orbiting, panning;

    static readonly string[] Rooms =
    [
        "boiler_room", "engine_room", "bunker", "magazine", "accommodation", "steering", "hold", "cargo_tank", "fuel_tank",
        "stores", "double_bottom", "tds",
    ];
    static readonly string[] Armour = ["belt", "strake", "armour_deck", "armoured_bulkhead"];
    static readonly string[] Guides = ["waterline", "hull_lines"];

    /// <summary>The presets: hitview's two modes, and narrower ones.</summary>
    static readonly (string Name, Func<string, bool> Shows)[] Presets =
    [
        ("all", _ => true),
        ("outside", k => k is not ("armour_deck" or "hull_lines")),   // hitview's outside views
        ("internal", k => Rooms.Contains(k) || Armour.Contains(k) || Guides.Contains(k)
                          || k is "barbette" or "uptake" or "casing" || HitKinds.Propulsion.Contains(k)),
        ("rooms", k => Rooms.Contains(k) || Guides.Contains(k)),
        ("armour", k => Armour.Contains(k) || Guides.Contains(k) || k is "barbette" or "conning_tower" or "main" or "secondary"),
    ];

    /// <summary>hitview's views (bearing, elevation), and a plan from above.</summary>
    static readonly (string Name, float Bearing, float Elevation)[] Views =
    [
        ("bow", 35, 28), ("quarter", 215, 24), ("side", 90, 0), ("internal", 55, 35), ("plan", 90, 89.9f),
    ];

    /// <summary><paramref name="view"/> and <paramref name="show"/> name a view and a preset to start with (the
    /// buttons' names: bow, quarter, side, internal, plan; all, outside, internal, rooms, armour).</summary>
    public HitboxScene(SDL_GPUDevice* device, DesignSession session, string? view = null, string? show = null)
    {
        this.session = session;
        renderer = new HitboxRenderer(device);
        ApplyPreset(Presets.FirstOrDefault(p => p.Name == show, Presets[1]).Shows);
        var v = Views.FirstOrDefault(v => v.Name == view, Views[0]);
        (cam.Bearing, cam.Elevation) = (v.Bearing, v.Elevation);
    }

    void ApplyPreset(Func<string, bool> shows)
    {
        state.KindMask = 0;
        for (int k = 0; k < HitKinds.All.Length; k++)
            if (shows(HitKinds.All[k].Name))
                state.KindMask |= 1UL << k;
    }

    void Rebuild()
    {
        builtVersion = session.Version;
        state.Hover = state.Selected = HitboxMesh.NoPrism;
        if (session.Ship is not { } ship)
        {
            renderer.Clear();
            status = session.Status;
            return;
        }
        var sw = Stopwatch.StartNew();
        var mesh = HitboxMesh.Build(ship.Hitboxes);
        double tMesh = sw.Elapsed.TotalSeconds;
        renderer.Upload(mesh);
        status = $"{ship.Design.Name}\n{mesh.Prisms.Length} prisms, {mesh.Vertices.Length} vertices, {mesh.Indices.Length / 3} triangles\n" +
                 $"{session.Status}, mesh {tMesh * 1000:F0} ms, upload {(sw.Elapsed.TotalSeconds - tMesh) * 1000:F0} ms";
        frame = true;
    }

    public void Draw(SDL_GPUCommandBuffer* cmd, RenderTarget target, float dt)
    {
        aspect = (float)target.Width / Math.Max(1, target.Height);
        if (builtVersion != session.Version)
            Rebuild();
        if (frame && renderer.Mesh is { Prisms.Length: > 0 } m)   // the ship fills the view; the direction stays
        {
            cam.Fit(m.Vertices, aspect);
            frame = false;
        }
        renderer.Render(cmd, target, cam, state);
    }

    public void BuildUi()
    {
        ImGui.SetNextWindowPos(new Vector2(10, 170), ImGuiCond.FirstUseEver);
        ImGui.SetNextWindowSize(new Vector2(330, 560), ImGuiCond.FirstUseEver);
        ImGui.Begin("Hitboxes");
        int designIndex = session.Index;
        if (ImGui.Combo("design", ref designIndex, session.Names, session.Names.Length))
            session.Select(designIndex);
        if (ImGui.Button("rebuild"))
            session.Rebuild();
        ImGui.SameLine();
        if (ImGui.Button("fit"))
            frame = true;
        ImGui.SameLine();
        ImGui.Checkbox("perspective", ref cam.Perspective);

        ImGui.Text("view");
        foreach (var (name, bearing, elevation) in Views)
        {
            ImGui.SameLine();
            if (ImGui.SmallButton(name))
                (cam.Bearing, cam.Elevation, frame) = (bearing, elevation, true);
        }
        ImGui.Text("show");
        foreach (var (name, shows) in Presets)
        {
            ImGui.SameLine();
            if (ImGui.SmallButton(name))
                ApplyPreset(shows);
        }

        ImGui.Separator();
        var present = renderer.Mesh?.KindsPresent;
        for (int k = 0; k < HitKinds.All.Length; k++)
        {
            if (present == null || !present[k])
                continue;
            var hk = HitKinds.All[k];
            bool on = state.Shows(k);
            ImGui.ColorButton($"##c{k}", new Vector4(hk.Colour, 1), ImGuiColorEditFlags.NoTooltip, new Vector2(14, 14));
            ImGui.SameLine();
            if (ImGui.Checkbox(hk.Name, ref on))
                state.KindMask = on ? state.KindMask | (1UL << k) : state.KindMask & ~(1UL << k);
        }
        ImGui.Separator();
        ImGui.TextWrapped(status);
        ImGui.End();
    }

    public bool ProcessEvent(SDL_Event* e, uint width, uint height)
    {
        switch ((SDL_EventType)e->type)
        {
            case SDL_EventType.SDL_EVENT_MOUSE_WHEEL:
                cam.HalfHeight *= MathF.Pow(1.15f, -e->wheel.y);
                return true;
            case SDL_EventType.SDL_EVENT_MOUSE_BUTTON_DOWN:
                orbiting |= e->button.button == 1;
                panning |= e->button.button is 2 or 3;
                return orbiting || panning;
            case SDL_EventType.SDL_EVENT_MOUSE_BUTTON_UP:
                if (e->button.button == 1)
                    orbiting = false;
                if (e->button.button is 2 or 3)
                    panning = false;
                return true;
            case SDL_EventType.SDL_EVENT_MOUSE_MOTION:
                if (orbiting)
                {
                    cam.Bearing = (cam.Bearing - e->motion.xrel * 0.4f + 360) % 360;
                    cam.Elevation = Math.Clamp(cam.Elevation + e->motion.yrel * 0.4f, -89.9f, 89.9f);
                    return true;
                }
                if (panning)
                {
                    var (r, s, _) = cam.Basis();
                    float mPerPx = 2 * cam.HalfHeight / Math.Max(1, height);
                    cam.Target += (-r * e->motion.xrel + s * e->motion.yrel) * mPerPx;
                    return true;
                }
                return false;
        }
        return false;
    }

    public void Dispose() => renderer.Dispose();
}
