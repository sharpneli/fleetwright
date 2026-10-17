using System.Globalization;
using System.Diagnostics;
using System.Numerics;
using Fleetwright.Gpu;
using Fleetwright.Sim.Ships;
using ShipP3 = Fleetwright.Sim.Ships.P3;
using ImGuiNET;
using SDL;

namespace Fleetwright.HitView;

/// <summary>
/// The hitbox viewer: the session's ship as its hitbox model in 3D (hitview.py, live). Left drag orbits, right drag
/// pans, the wheel zooms; hovering names a prism, a click selects it and lists its fields. The panel picks the design,
/// a view (hitview's bow, quarter, side and internal, and plan), the projection, which kinds are drawn (alone or by
/// preset) and a clip box (a tier at a click). With "click fires a ray" (or shift+click), a click fires a ray from the
/// eye at the ship through the hit query's tracer (<see cref="ShipTracer"/>, docs/battle/fighting.md 4.1): the ray and
/// what it crossed are drawn over the view, and the crossings listed in the panel. With "shell", a click fires a shell
/// at what's under the cursor through the damage system (<see cref="ShotRange"/>): its path, the terminal walk plate by
/// plate and what it did, played back over the view (HitboxScene.Shot.cs). Draws through a
/// <see cref="HitboxRenderer"/>, which could as well draw into a small target inside another scene.
/// </summary>
public sealed unsafe partial class HitboxScene : IScene
{
    /// <summary>A dev hook (-clip=AFT,FORE[,port][,cut]): the clip box at start, from AFT to FORE along the ship (the prisms
    /// it touches whole, or cut with "cut"), only the port half with "port" (a section seen from ahead).</summary>
    public static string? DevClip { get; set; }

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
    ClickMode click;
    bool shiftDown;
    ShipClass? cls;   // compiled when a ray is first fired at this build
    readonly HitPath path = new();
    bool hasRay;
    string pathText = "";

    static readonly string[] Rooms =
    [
        "boiler_room", "engine_room", "bunker", "magazine", "handling", "accommodation", "steering", "hold", "cargo_tank", "fuel_tank",
        "stores", "double_bottom", "tds", "torpedo_room",
    ];
    static readonly string[] Armour = ["belt", "strake", "armour_deck", "armoured_bulkhead", "inner_bulkhead"];
    static readonly string[] Guides = ["waterline", "hull_lines"];

    /// <summary>The presets: hitview's two modes, and narrower ones.</summary>
    static readonly (string Name, Func<string, bool> Shows)[] Presets =
    [
        ("all", _ => true),
        ("outside", k => k is not ("armour_deck" or "hull_lines")),   // hitview's outside views
        ("internal", k => Rooms.Contains(k) || Armour.Contains(k) || Guides.Contains(k)
                          || k is "barbette" or "uptake" or "casing" or "torpedo_tube" || HitKinds.Propulsion.Contains(k)),
        ("rooms", k => Rooms.Contains(k) || Guides.Contains(k)),
        ("armour", k => Armour.Contains(k) || Guides.Contains(k) || k is "barbette" or "conning_tower" or "main" or "secondary"),
    ];

    /// <summary>hitview's views (bearing, elevation), and a plan from above.</summary>
    static readonly (string Name, float Bearing, float Elevation)[] Views =
    [
        ("bow", 35, 28), ("quarter", 215, 24), ("side", 90, 0), ("internal", 55, 35), ("plan", 90, 89.9f), ("ahead", 0, 4), ("astern", 180, 4),
    ];

    /// <summary><paramref name="view"/> and <paramref name="show"/> name a view and a preset to start with (the
    /// buttons' names: bow, quarter, side, internal, plan, ahead and astern (the sections head on); all, outside, internal, rooms, armour).</summary>
    public HitboxScene(SDL_GPUDevice* device, DesignSession session, string? view = null, string? show = null, bool fire = false)
    {
        this.session = session;
        click = fire ? ClickMode.Ray : DevShot != null ? ClickMode.Shell : ClickMode.Select;
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
        (cls, hasRay, pathText) = (null, false, "");
        ResetShot();
        if (session.Ship is not { } ship || session.Result?.Mesh is not { } mesh)
        {
            renderer.Clear();
            status = session.Status;
            return;
        }
        var sw = Stopwatch.StartNew();
        renderer.Upload(mesh);
        tiers = ship.Hitboxes.Tiers;
        (state.ClipMin, state.ClipMax) = (mesh.Min - Vector3.One, mesh.Max + Vector3.One);
        if (DevClip?.Split(',') is { Length: >= 2 } dc && float.TryParse(dc[0], NumberStyles.Float, CultureInfo.InvariantCulture, out float c0)
            && float.TryParse(dc[1], NumberStyles.Float, CultureInfo.InvariantCulture, out float c1))
        {
            (state.Clip, state.ClipCut, state.ClipMin.X, state.ClipMax.X) = (true, dc.Contains("cut"), Math.Min(c0, c1), Math.Max(c0, c1));
            if (dc.Contains("port"))
                state.ClipMax.Y = 0;
        }
        status = $"{ship.Design.Name}\n{mesh.Prisms.Length} prisms, {mesh.Vertices.Length} vertices, {mesh.Indices.Length / 3} triangles\n" +
                 $"{session.Status}, upload {sw.Elapsed.TotalSeconds * 1000:F0} ms";
        frame = true;
    }

    public void Draw(SDL_GPUCommandBuffer* cmd, RenderTarget target, float dt)
    {
        aspect = (float)target.Width / Math.Max(1, target.Height);
        if (builtVersion != session.Version)
            Rebuild();
        if (frame && renderer.Mesh is { Prisms.Length: > 0 } m)   // the ship fills the view; the direction stays
        {
            if (state.Clip)
                cam.Fit(m.Vertices, aspect, clipMin: state.ClipMin, clipMax: state.ClipMax);   // what the clip box keeps
            else
                cam.Fit(m.Vertices, aspect);
            frame = false;
        }
        renderer.Render(cmd, target, cam, state);
        AdvanceShot(dt);
    }

    public void BuildUi()
    {
        SidePanel.Begin("Hitboxes");
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
        ImGui.Text("click");
        foreach (var (mode, label) in (ReadOnlySpan<(ClickMode, string)>)[(ClickMode.Select, "selects"), (ClickMode.Ray, "fires a ray"), (ClickMode.Shell, "fires a shell")])
        {
            ImGui.SameLine();
            if (ImGui.RadioButton(label, click == mode))
                click = mode;
        }
        if (hasRay)
        {
            ImGui.SameLine();
            if (ImGui.SmallButton("clear ray"))
                hasRay = false;
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
        if (renderer.Mesh is { } m && state.Selected < m.Prisms.Length)
        {
            ImGui.Separator();
            if (selectedFor != state.Selected)
                (selectedFor, selectedText) = (state.Selected, HitInfo.Full(m.Prisms[state.Selected]));
            if (ImGui.SmallButton("clear selection"))
                state.Selected = HitboxMesh.NoPrism;
            ImGui.TextWrapped(selectedText);
        }
        if (hasRay)
        {
            ImGui.Separator();
            ImGui.TextUnformatted("the ray, as the hit query traces it:");
            ImGui.BeginChild("ray", new Vector2(0, 0), ImGuiChildFlags.None, ImGuiWindowFlags.HorizontalScrollbar);
            ImGui.TextUnformatted(pathText);
            ImGui.EndChild();
        }
        if (click == ClickMode.Shell)
            ShotPanel();
        ImGui.End();
        if (click == ClickMode.Shell)
            ShotWindow();
        if (hasRay)
            DrawRay();
        DrawShot();

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

    /// <summary>A ray from the eye through the view at (x, y), traced through the ship (turrets at rest, as drawn).</summary>
    void Fire(float x, float y, uint width, uint height)
    {
        if (TraceEye(x, y, width, height, path))
            (hasRay, pathText) = (true, path.Text(cls!));
    }

    /// <summary>The ray and its crossings over the view: armour red, the hull's skin orange, parts blue, the sea cyan,
    /// cell walls grey (not hidden by what's in front: it's a look inside).</summary>
    void DrawRay()
    {
        if (renderer.Mesh is not { } m)
            return;
        var size = ImGui.GetIO().DisplaySize;
        var vp = cam.ViewProj(size.X / Math.Max(1, size.Y), (m.Min + m.Max) / 2, Math.Max(1, (m.Max - m.Min).Length() / 2 + 5));
        bool Screen(ShipP3 p, out Vector2 q)
        {
            var c = Vector4.Transform(new Vector4((float)p.X, (float)p.Y, (float)p.Z, 1), vp);
            q = c.W > 1e-4f ? new Vector2((c.X / c.W + 1) / 2 * size.X, (1 - c.Y / c.W) / 2 * size.Y) : default;
            return c.W > 1e-4f;
        }
        var dl = ImGui.GetBackgroundDrawList();
        uint line = ImGui.GetColorU32(new Vector4(1, 1, 0.4f, 0.9f));
        // from the first crossing to the last, a little either side
        var xs = path.Crossings;
        double t0 = xs.Length > 0 ? Math.Max(0, xs[0].T - 15) : 0, t1 = xs.Length > 0 ? Math.Min(path.Length, xs[^1].T + 5) : path.Length;
        if (Screen(path.At(t0), out var a) && Screen(path.At(t1), out var b))
            dl.AddLine(a, b, line, 2);
        foreach (ref readonly var x in xs)
        {
            if (!Screen(x.Point, out var q))
                continue;
            var (colour, r) = x.Kind switch
            {
                CrossingKind.Plate when x.Plate is PlateKind.Deck or PlateKind.Bulkhead => (new Vector4(0.7f, 0.7f, 0.7f, 0.9f), 2.5f),
                CrossingKind.Plate or CrossingKind.PartIn or CrossingKind.PartOut when x.Plate is PlateKind.SideArmour or PlateKind.ArmourDeck
                    or PlateKind.ArmourBulkhead or PlateKind.Inclined or PlateKind.Grating or PlateKind.Barbette or PlateKind.TurretFace or PlateKind.TurretSide
                    or PlateKind.TurretRear or PlateKind.TurretRoof or PlateKind.PartArmour => (new Vector4(1, 0.2f, 0.2f, 1), 6f),
                CrossingKind.HullIn or CrossingKind.HullOut => (new Vector4(1, 0.6f, 0.1f, 1), 5f),
                CrossingKind.PartIn or CrossingKind.PartOut => (new Vector4(0.4f, 0.6f, 1, 1), 4f),
                CrossingKind.WaterIn or CrossingKind.WaterOut => (new Vector4(0.2f, 0.9f, 1, 1), 5f),
                _ => (new Vector4(0.7f, 0.7f, 0.7f, 0.6f), 2f),
            };
            dl.AddCircleFilled(q, r, ImGui.GetColorU32(colour));
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
                shiftDown = (SDL3.SDL_GetModState() & SDL_Keymod.SDL_KMOD_SHIFT) != 0;
                if (e->button.button == 1)
                    dragPx = 0;
                orbiting |= e->button.button == 1;
                panning |= e->button.button is 2 or 3;
                return orbiting || panning;
            case SDL_EventType.SDL_EVENT_MOUSE_BUTTON_UP:
                if (e->button.button == 1)
                {
                    orbiting = false;
                    if (dragPx < 4 && click == ClickMode.Shell && !shiftDown)   // a click fires a shell
                        Shoot(e->button.x, e->button.y, width, height);
                    else if (dragPx < 4 && (click == ClickMode.Ray || shiftDown))   // a click fires a ray
                        Fire(e->button.x, e->button.y, width, height);
                    else if (dragPx < 4)   // a click: select what is under the cursor, or nothing
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
