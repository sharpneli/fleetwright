using System.Diagnostics;
using System.Numerics;
using Fleetwright.Gpu;
using ImGuiNET;
using SDL;

namespace Fleetwright.HitView;

/// <summary>
/// The hitbox viewer: the session's ship as its hitbox model in 3D (hitview.py, live). Left drag orbits, right drag
/// pans, the wheel zooms; hovering names a prism, a click selects it and lists its fields. The panel picks the design,
/// a view (hitview's bow, quarter, side and internal, and plan), the projection, which kinds are drawn (alone or by
/// preset) and a clip box (a tier at a click). Draws through a <see cref="HitboxRenderer"/>, which could as well draw
/// into a small target inside another scene.
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
    float dragPx;   // how far the mouse moved with the left button down: a click selects, a drag orbits
    uint hoverFor = HitboxMesh.NoPrism, selectedFor = HitboxMesh.NoPrism;
    string hoverText = "", selectedText = "";
    List<Shipgen.Tier> tiers = [];

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
        tiers = ship.Hitboxes.Tiers;
        (state.ClipMin, state.ClipMax) = (mesh.Min - Vector3.One, mesh.Max + Vector3.One);
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

        ClipUi();

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
        if (renderer.Mesh is { } m && state.Selected < m.Prisms.Length)
        {
            ImGui.Separator();
            if (selectedFor != state.Selected)
                (selectedFor, selectedText) = (state.Selected, HitInfo.Full(m.Prisms[state.Selected]));
            if (ImGui.SmallButton("clear selection"))
                state.Selected = HitboxMesh.NoPrism;
            ImGui.TextWrapped(selectedText);
        }
        ImGui.End();

        // the hovered prism's name by the cursor, unless the cursor is over a window
        if (ImGui.GetIO().WantCaptureMouse || orbiting || panning)
            state.Hover = HitboxMesh.NoPrism;
        else if (renderer.Mesh is { } mesh && state.Hover < mesh.Prisms.Length)
        {
            if (hoverFor != state.Hover)
                (hoverFor, hoverText) = (state.Hover, HitInfo.Short(mesh.Prisms[state.Hover]));
            ImGui.SetTooltip(hoverText);
        }
    }

    /// <summary>The clip box: ranges along the ship, across and up, and a button per tier of the subdivision (cut to
    /// its decks, the plan view of hitbox_cells.png).</summary>
    void ClipUi()
    {
        if (renderer.Mesh is not { } m)
            return;
        ImGui.Checkbox("clip", ref state.Clip);
        if (!state.Clip)
            return;
        ImGui.SameLine();
        if (ImGui.RadioButton("whole prisms", !state.ClipCut))
            state.ClipCut = false;
        ImGui.SameLine();
        if (ImGui.RadioButton("cut", state.ClipCut))
            state.ClipCut = true;
        Vector3 lo = m.Min - Vector3.One, hi = m.Max + Vector3.One;
        ImGui.SliderFloat("aft", ref state.ClipMin.X, lo.X, hi.X, "%.1f m");
        ImGui.SliderFloat("fore", ref state.ClipMax.X, lo.X, hi.X, "%.1f m");
        ImGui.SliderFloat("port", ref state.ClipMin.Y, lo.Y, hi.Y, "%.1f m");
        ImGui.SliderFloat("starboard", ref state.ClipMax.Y, lo.Y, hi.Y, "%.1f m");
        ImGui.SliderFloat("bottom", ref state.ClipMin.Z, lo.Z, hi.Z, "%.1f m");
        ImGui.SliderFloat("top", ref state.ClipMax.Z, lo.Z, hi.Z, "%.1f m");
        if (ImGui.SmallButton("whole ship"))
            (state.ClipMin, state.ClipMax) = (lo, hi);
        ImGui.SameLine();
        if (ImGui.SmallButton("port half"))
            (state.ClipMin, state.ClipMax) = (lo, hi with { Y = 0 });
        ImGui.Text("tier");
        foreach (var t in tiers)
        {
            ImGui.SameLine();
            if (ImGui.SmallButton(t.Id))   // the tier's decks, a little inside, so the decks themselves don't show
                (state.ClipMin, state.ClipMax) = (lo with { Z = (float)t.Base + 0.05f }, hi with { Z = (float)t.Top - 0.05f });
        }
    }

    void Pick(float x, float y, uint width, uint height)
    {
        if (renderer.Mesh is not { } m)
            return;
        var ndc = new Vector2(2 * x / Math.Max(1, width) - 1, 1 - 2 * y / Math.Max(1, height));
        var (o, d) = cam.Ray(ndc, (float)width / Math.Max(1, height), (m.Max - m.Min).Length() + 10);
        state.Hover = m.Pick(o, d, state);
    }

    public bool ProcessEvent(SDL_Event* e, uint width, uint height)
    {
        switch ((SDL_EventType)e->type)
        {
            case SDL_EventType.SDL_EVENT_MOUSE_WHEEL:
                cam.HalfHeight *= MathF.Pow(1.15f, -e->wheel.y);
                return true;
            case SDL_EventType.SDL_EVENT_MOUSE_BUTTON_DOWN:
                if (e->button.button == 1)
                    dragPx = 0;
                orbiting |= e->button.button == 1;
                panning |= e->button.button is 2 or 3;
                return orbiting || panning;
            case SDL_EventType.SDL_EVENT_MOUSE_BUTTON_UP:
                if (e->button.button == 1)
                {
                    orbiting = false;
                    if (dragPx < 4)   // a click: select what is under the cursor, or nothing
                    {
                        Pick(e->button.x, e->button.y, width, height);
                        state.Selected = state.Hover == state.Selected ? HitboxMesh.NoPrism : state.Hover;
                    }
                }
                if (e->button.button is 2 or 3)
                    panning = false;
                return true;
            case SDL_EventType.SDL_EVENT_MOUSE_MOTION:
                if (orbiting)
                {
                    dragPx += MathF.Abs(e->motion.xrel) + MathF.Abs(e->motion.yrel);
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
                Pick(e->motion.x, e->motion.y, width, height);
                return true;
        }
        return false;
    }

    public void Dispose() => renderer.Dispose();
}
