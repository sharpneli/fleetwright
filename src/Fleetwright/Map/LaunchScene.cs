using System.Globalization;
using System.Numerics;
using System.Text.Json;
using Fleetwright.Designer;
using Fleetwright.Gpu;
using Fleetwright.Sim;
using Fleetwright.Sim.Core;
using Fleetwright.Sim.Missions;
using ImGuiNET;
using SDL;
using static SDL.SDL3;

namespace Fleetwright.Map;

/// <summary>The scenario launch (docs/map-ui/launch.md L1, the mockup launch.html): the app's first screen, open before
/// any ship is built. The scenarios at the left, the picked one's header (its date, place and day, the brief), a card a
/// side and Watch, the opening on the map, what each side has and wants, and at the foot the load and Begin. Picking a
/// scenario or a side asks for its classes and places its world at once (<see cref="WorldSession.Select(Scenario, string?)"/>),
/// so reading the brief covers the build; Begin swaps the world in (<see cref="WorldSession.Begin"/>) and
/// <see cref="Begun"/> shows the map. What the player changes is a setup (<see cref="ScenarioSetup"/>, L2): the
/// scenario as written plus its changes, listed with an undo each, saved as a scenario file in My Games/Fleetwright/
/// Setups and listed under Your setups. The body's first column is each side's ships (L3, <c>LaunchScene.Ships.cs</c>),
/// a design changed by the picker (<c>LaunchScene.Picker.cs</c>); the standing orders and the conditions (L4-L5) take the
/// rest later, until then the aims and the orders before action. Its strings are made when the pick changes, so a frame
/// allocates nothing but the load's line while it builds.</summary>
public sealed unsafe partial class LaunchScene : IScene, ISceneMenu
{
    readonly WorldSession world;

    /// <summary>The picked scenario's world is in: the map should show.</summary>
    public Action? Begun { get; init; }

    public Action? OpenDesigner { get; init; }
    public Action? Quit { get; init; }

    sealed class Entry
    {
        public required string Path;
        public Scenario? Scenario;
        public string? Error;
        public bool Test, Opened, Saved;   // Saved: a setup of the player's, in the setups folder
        public DateTime Modified;
        public string Name = "", Date = "", Classes = "", When = "";
        public (string Name, string Count)[] Sides = [];
        public int Year, Ships;
    }

    readonly List<Entry> entries = [];
    int pick = -1;
    int side;   // the player's side, -1 to watch
    int filter;   // 0 all, 1 1890-1905, 2 1906-1918, 3 small, 4 fleet, 5 tests
    static readonly string[] Filters = ["ALL", "1890–1905", "1906–1918", "SMALL", "FLEET", "TESTS"];
    const int SmallShips = 30;   // a fleet from here

    readonly int initial;   // the pick when it first opens: the scenario asked for, else the last played, else the first
    bool opened;
    bool begin;   // Begin pressed, its world not in yet
    int beginGeneration;
    bool pausedHere;   // a battle was running when the screen opened, and it paused it

    static string UserFolder => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "My Games", "Fleetwright");
    static string LastPath => Path.Combine(UserFolder, "launch.json");

    /// <summary>The player's setups, beside their designs.</summary>
    public static string SetupsFolder => Path.Combine(UserFolder, "Setups");

    static readonly string ShippedFolder = Path.Combine(AppContext.BaseDirectory, "Content", "Scenarios");

    // the pick as the player has set it: the scenario as written, the setup's changes, and the two applied (null if a
    // change no longer applies: its error)
    Scenario? written, current;
    ScenarioSetup setup = new();
    string? setupError;

    // the theme of the mockup (launch.html's :root)
    static readonly Vector4 Bg = Hex(0x0C1217), BgLight = Hex(0x14222D), Bar = Hex(0x0B0F12), ListBg = new(10 / 255f, 14 / 255f, 17 / 255f, 0.92f),
        LineC = new(150 / 255f, 172 / 255f, 188 / 255f, 0.11f), EdgeC = new(150 / 255f, 172 / 255f, 188 / 255f, 0.17f),
        Text = Hex(0xC9D3DB), BriefC = Hex(0xB8C3CB), Soft = Hex(0xAAB6BF), Bright = Hex(0xEEF3F6), Muted = Hex(0x7F8B95),
        Brass = Hex(0xD9B868), BrassDark = Hex(0x5A4A24), BrassBg = Hex(0x3A3014), Ok = Hex(0x86C77E), Amber = Hex(0xE3A452),
        Friend = Hex(0x5B9BE0), Hostile = Hex(0xE2685C), Sea = Hex(0x12293A), LandC = Hex(0x2C3128), AiBg = Hex(0x16283A);

    static Vector4 Hex(uint rgb) => new(((rgb >> 16) & 0xFF) / 255f, ((rgb >> 8) & 0xFF) / 255f, (rgb & 0xFF) / 255f, 1);
    static uint U(Vector4 c) => ImGui.ColorConvertFloat4ToU32(c);

    public LaunchScene(WorldSession world, ShipArtLibrary art, string? pickName = null)
    {
        this.world = world;
        this.art = art;
        land = Land.Load(Path.Combine(AppContext.BaseDirectory, "Content", "World", "land-50m.bin"));
        foreach (var path in WorldSession.ShippedScenarios())
            entries.Add(Read(path, opened: false));
        if (Directory.Exists(SetupsFolder))
            foreach (var path in Directory.GetFiles(SetupsFolder, "*.json"))
                entries.Add(Read(path, opened: false));
        Sort();
        ScanShipped();
        pickName ??= ReadLast();
        int at = pickName == null ? -1 : entries.FindIndex(e => KeyOf(e).Equals(pickName, StringComparison.OrdinalIgnoreCase));
        initial = at >= 0 ? at : entries.FindIndex(e => !e.Test && e.Scenario != null);
    }

    /// <summary>The shipped first by their dates, the scale test's rungs (ladder-*) under Tests, the player's setups
    /// newest first, the files opened last.</summary>
    void Sort()
    {
        entries.Sort((a, b) => Group(a) != Group(b) ? Group(a).CompareTo(Group(b))
            : a.Saved ? b.Modified.CompareTo(a.Modified)
            : (a.Scenario?.Start.Ms ?? long.MaxValue, a.Name).CompareTo((b.Scenario?.Start.Ms ?? long.MaxValue, b.Name)));
        setupsOf.Clear();
        foreach (var e in entries)
            if (e.Saved && e.Scenario?.Setup is { } st)
                setupsOf.Add(st.From);
    }

    readonly HashSet<string> setupsOf = new(StringComparer.OrdinalIgnoreCase);   // the scenarios the player has setups of

    static int Group(Entry e) => e.Opened ? 3 : e.Saved ? 2 : e.Test ? 1 : 0;

    /// <summary>How the last one played is kept: a shipped scenario's file name, a setup's as "setups/name".</summary>
    static string KeyOf(Entry e) => (e.Saved ? "setups/" : "") + Path.GetFileNameWithoutExtension(e.Path);

    static Entry Read(string path, bool opened)
    {
        var e = new Entry { Path = path, Opened = opened, Name = Path.GetFileNameWithoutExtension(path) };
        e.Test = e.Name.StartsWith("ladder-", StringComparison.Ordinal);
        e.Saved = !opened && Path.GetFullPath(Path.GetDirectoryName(path)!).Equals(Path.GetFullPath(SetupsFolder), StringComparison.OrdinalIgnoreCase);
        e.Modified = File.GetLastWriteTime(path);
        e.When = e.Modified.Date == DateTime.Today ? "today" : e.Modified.ToString("MMM d", CultureInfo.InvariantCulture);
        try
        {
            var s = Scenario.Load(path, [ShippedFolder]);
            e.Scenario = s;
            e.Name = s.Name;
            var start = s.Start.ToUtc();
            e.Year = start.Year;
            e.Date = start.ToString("d MMMM yyyy", CultureInfo.InvariantCulture) + (s.Place.Length > 0 ? " · " + s.Place : "");
            e.Ships = s.ShipCount;
            e.Sides = [.. s.Sides.Select((n, i) => (n, s.Units.Where(u => u.Side == i).Sum(u => u.ShipCount).ToString(CultureInfo.InvariantCulture)))];
            int classes = s.DesignNames().Count;
            e.Classes = $"{classes} class{(classes == 1 ? "" : "es")}";
        }
        catch (Exception x) when (x is ScenarioException or IOException or JsonException)
        {
            e.Error = x.Message;
        }
        return e;
    }

    static string? ReadLast()
    {
        try
        {
            if (File.Exists(LastPath))
                using (var doc = JsonDocument.Parse(File.ReadAllText(LastPath)))
                    return doc.RootElement.TryGetProperty("last", out var l) ? l.GetString() : null;
        }
        catch (Exception x) when (x is IOException or JsonException or UnauthorizedAccessException)
        {
        }
        return null;
    }

    static void WriteLast(string name)
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(LastPath)!);
            File.WriteAllText(LastPath, JsonSerializer.Serialize(new Dictionary<string, string> { ["last"] = name }));
        }
        catch (Exception x) when (x is IOException or UnauthorizedAccessException)
        {
        }
    }

    /// <summary>A scenario file dropped on the window: added under Opened (or found among the shipped) and picked.</summary>
    public void Add(string path)
    {
        string full = Path.GetFullPath(path);
        int at = entries.FindIndex(e => Path.GetFullPath(e.Path).Equals(full, StringComparison.OrdinalIgnoreCase));
        if (at < 0)
        {
            var e = Read(path, opened: true);
            entries.Add(e);
            Sort();
            at = entries.IndexOf(e);
        }
        filter = 0;
        Pick(at);
    }

    /// <summary>The screen opens (the app's start, or "Scenarios" from the map): a battle running behind it pauses, and
    /// the pick is asked for again if Begin used it up.</summary>
    public void Open()
    {
        begin = false;
        if (world.Scenario != null && !world.Current.Paused)
        {
            world.Send(Command.Pause());
            pausedHere = true;
        }
        if (!opened)
        {
            opened = true;   // its first pick only now: a run straight into a scenario asks for nothing here
            Pick(initial);
        }
        else if (world.Selected == null && pick >= 0)
            Select();
    }

    /// <summary>The screen gives way without a Begin (back to the battle, or the designer): a battle it paused runs on.</summary>
    public void Left()
    {
        if (pausedHere && !begin)
            world.Send(Command.Resume());
        pausedHere = false;
    }

    void Pick(int i)
    {
        ClosePicker();
        placing = null;
        pick = i;
        (written, current, setupError) = (null, null, null);
        if (i < 0 || entries[i].Scenario is not { } s)
            return;
        written = s.Written ?? s;
        described.Clear();
        // a setup picked is its changes; a scenario as written, none yet (of its file, if a setup can be "from" it)
        setup = s.Setup ?? new ScenarioSetup { From = entries[i].Opened ? "" : Path.GetFileNameWithoutExtension(entries[i].Path) };
        Apply();
    }

    /// <summary>A change of the player's: the setup with it (or without it, when it puts back what's written).</summary>
    void Edit(ScenarioSetup next)
    {
        ClosePicker();
        setup = next;
        Apply();
    }

    int? pendingSide;   // a side card clicked this frame

    void SetSide(int sd)
    {
        if (written == null)
            return;
        string? name = sd >= 0 ? written.Sides[sd] : null;
        Edit(name == written.PlayerName ? setup.Without("player") : setup.With(new PlayAs(name)));
    }

    void Apply()
    {
        try
        {
            current = setup.Apply(written!);
            setupError = null;
            side = current.Player;
        }
        catch (ScenarioException x)
        {
            (current, setupError) = (null, x.Message);
        }
        Select();
    }

    /// <summary>The pick as set, to the world: its classes asked for and its world placed now.</summary>
    void Select()
    {
        if (current == null)
            return;
        world.Select(current, entries[pick].Path);
        Made(current);
    }

    void BeginWorld(bool paused)
    {
        if (current == null || begin)
            return;
        if (world.Selected == null)
            Select();
        world.Truth = side < 0;   // Watch: everything shown
        world.Send(paused ? Command.Pause() : Command.Resume());
        pausedHere = false;
        beginGeneration = world.Generation;
        begin = true;
        world.Begin();
        if (!entries[pick].Opened)
            WriteLast(KeyOf(entries[pick]));
    }

    public void Draw(SDL_GPUCommandBuffer* cmd, RenderTarget target, float dt)
    {
        art.UploadPending();   // the rows' and the picker's ships
        var pass = target.BeginPass(cmd, new SDL_FColor { r = Bg.X, g = Bg.Y, b = Bg.Z, a = 1 }, depth: false);
        SDL_EndGPURenderPass(pass);
    }

    /// <summary>Esc shuts the picker, or stops placing a unit; with nothing open, the menu.</summary>
    public bool ProcessEvent(SDL_Event* e, uint width, uint height)
    {
        if ((SDL_EventType)e->type == SDL_EventType.SDL_EVENT_KEY_DOWN && (uint)e->key.key == SDLK_ESCAPE && (pickerRow != null || placing != null) && !saveOpen)
        {
            if (pickerRow != null)
                ClosePicker();
            else
                placing = null;
            return true;
        }
        return false;
    }

    public bool MenuItems() => false;

    public void MenuShown(bool shown)
    {
    }

    public void Dispose()
    {
    }

    // ------------------------------------------------------------------ the frame

    float sc;
    ImDrawListPtr dl;

    public void BuildUi()
    {
        if (begin && world.Generation != beginGeneration)
        {
            begin = false;
            Begun?.Invoke();
        }
        else if (begin && world.LoadError != null)
            begin = false;
        sc = UiFonts.Scale;
        Figure();
        var vp = ImGui.GetMainViewport();
        ImGui.SetNextWindowPos(vp.Pos);
        ImGui.SetNextWindowSize(vp.Size);
        ImGui.PushStyleVar(ImGuiStyleVar.WindowPadding, Vector2.Zero);
        ImGui.PushStyleVar(ImGuiStyleVar.WindowBorderSize, 0);
        ImGui.PushStyleColor(ImGuiCol.WindowBg, Bg);
        bool open = ImGui.Begin("##launch", ImGuiWindowFlags.NoDecoration | ImGuiWindowFlags.NoSavedSettings | ImGuiWindowFlags.NoMove |
                                            ImGuiWindowFlags.NoBringToFrontOnFocus | ImGuiWindowFlags.NoNav | ImGuiWindowFlags.NoScrollWithMouse);
        ImGui.PopStyleColor();
        ImGui.PopStyleVar(2);
        if (open)
        {
            dl = ImGui.GetWindowDrawList();
            var o = vp.Pos;
            float w = vp.Size.X, h = vp.Size.Y;
            float top = 34 * sc, listW = 372 * sc, foot = 64 * sc, head = 332 * sc;
            // the ground: a light at the upper right fading out
            dl.AddRectFilledMultiColor(o + new Vector2(listW, top), o + new Vector2(w, h), U(BgLight), U(BgLight), U(Bg), U(Bg));
            var main = new Vector2(o.X + listW, o.Y + top);
            Header(main, w - listW, head);
            Body(main + new Vector2(0, head), w - listW, h - top - head - foot);
            Footer(new Vector2(o.X + listW, o.Y + h - foot), w - listW, foot);
            List(new Vector2(o.X, o.Y + top), listW, h - top);
            TopBar(o, w, top);
        }
        ImGui.End();
        {
            float top = 34 * sc, x = vp.Pos.X + 372 * sc + ShipsW * sc;
            PickerUi(new Vector2(x, vp.Pos.Y + top), new Vector2(vp.Pos.X + vp.Size.X - x, vp.Size.Y - top - 64 * sc));
        }
        if (pendingSide is int ps)
        {
            pendingSide = null;
            SetSide(ps);
        }
        if (pendingAdd is { } pa)
        {
            pendingAdd = null;
            if (!begin)
                OpenAdd(pa.Side, pa.Joins);
        }
        if (pendingEdit is { } pe)
        {
            pendingEdit = null;
            if (!begin)
                Edit(pe);
        }
    }

    // ------------------------------------------------------------------ text and boxes

    void Txt(ImFontPtr font, float px, Vector2 at, Vector4 col, string s) => dl.AddText(font, px * sc, at, U(col), s);

    static float Width(ImFontPtr font, float px, string s) => font.CalcTextSizeA(px * UiFonts.Scale, float.MaxValue, 0, s).X;

    /// <summary>Text wrapped at <paramref name="wrap"/> wide; returns its height.</summary>
    float Wrapped(ImFontPtr font, float px, Vector2 at, Vector4 col, string s, float wrap)
    {
        dl.AddText(font, px * sc, at, U(col), s, wrap);
        return font.CalcTextSizeA(px * sc, float.MaxValue, wrap, s).Y;
    }

    /// <summary>Spaced capitals (the mockup's headings, letter-spacing): returns the width.</summary>
    float Tracked(ImFontPtr font, float px, Vector2 at, Vector4 col, string s, float track)
    {
        float x = at.X;
        Span<char> one = stackalloc char[1];
        foreach (char c in s)
        {
            one[0] = c;
            string g = c < Glyphs.Length ? Glyphs[c] : c.ToString();
            dl.AddText(font, px * sc, new Vector2(x, at.Y), U(col), g);
            x += font.CalcTextSizeA(px * sc, float.MaxValue, 0, g).X + track * sc;
        }
        return x - at.X;
    }

    static readonly string[] Glyphs = [.. Enumerable.Range(0, 128).Select(i => ((char)i).ToString())];

    float TrackedWidth(ImFontPtr font, float px, string s, float track)
    {
        float x = 0;
        foreach (char c in s)
            x += font.CalcTextSizeA(px * sc, float.MaxValue, 0, c < Glyphs.Length ? Glyphs[c] : c.ToString()).X + track * sc;
        return x;
    }

    void Box(Vector2 a, Vector2 b, Vector4 fill, Vector4 edge, float rounding = 3)
    {
        dl.AddRectFilled(a, b, U(fill), rounding * sc);
        dl.AddRect(a, b, U(edge), rounding * sc);
    }

    /// <summary>A button of the mockup's: framed, its label centred; brass when primary.</summary>
    bool Button(string id, string label, Vector2 at, Vector2 size, bool primary = false, bool enabled = true, float px = 12)
    {
        ImGui.SetCursorScreenPos(at);
        bool hit = ImGui.InvisibleButton(id, size) && enabled;
        bool hover = enabled && ImGui.IsItemHovered();
        var fill = primary ? (hover ? Hex(0xE6C77A) : Brass) : new Vector4(1, 1, 1, hover ? 0.09f : 0.04f);
        var edge = primary ? Brass : EdgeC;
        if (!enabled)
            (fill, edge) = (fill with { W = fill.W * 0.45f }, edge with { W = edge.W * 0.45f });
        Box(at, at + size, fill, edge, 2);
        var font = primary ? UiFonts.SansBold : UiFonts.Sans;
        float tw = Width(font, px, label);
        var col = primary ? Hex(0x111111) : enabled ? Text : Muted;
        Txt(font, px, at + new Vector2((size.X - tw) / 2, (size.Y - px * sc * 1.3f) / 2), col, label);
        return hit;
    }

    void Chip(Vector2 at, string label, Vector4 fill, Vector4 col)
    {
        float tw = TrackedWidth(UiFonts.SansBold, 10, label, 0.5f);
        dl.AddRectFilled(at, at + new Vector2(tw + 10 * sc, 16 * sc), U(fill), 2 * sc);
        Tracked(UiFonts.SansBold, 10, at + new Vector2(5 * sc, 1.5f * sc), col, label, 0.5f);
    }

    void HLine(float x0, float x1, float y) => dl.AddLine(new Vector2(x0, y), new Vector2(x1, y), U(LineC));

    void VLine(float x, float y0, float y1) => dl.AddLine(new Vector2(x, y0), new Vector2(x, y1), U(LineC));

    // ------------------------------------------------------------------ the top bar

    string topLoad = "";
    int topLoadKey = -1;

    void TopBar(Vector2 o, float w, float h)
    {
        dl.AddRectFilled(o, o + new Vector2(w, h), U(Bar));
        HLine(o.X, o.X + w, o.Y + h);
        float mid = o.Y + h / 2;
        float x = o.X + 14 * sc;
        x += Tracked(UiFonts.SansBold, 13, new Vector2(x, mid - 9 * sc), Brass, "FLEETWRIGHT", 2.6f) + 12 * sc;
        Txt(UiFonts.Sans, 13, new Vector2(x, mid - 9 * sc), Muted, "Scenarios ›");
        x += Width(UiFonts.Sans, 13, "Scenarios ›") + 5 * sc;
        if (pick >= 0)
            Txt(UiFonts.SansBold, 13, new Vector2(x, mid - 9 * sc), Bright, current?.Name ?? entries[pick].Name);
        // at the right: the build under way, the designer, quit
        float bx = o.X + w - 14 * sc;
        float qw = 44 * sc, dw = 72 * sc, bh = 24 * sc;
        bx -= qw;
        if (Button("##quit", "Quit", new Vector2(bx, mid - bh / 2), new Vector2(qw, bh)))
            Quit?.Invoke();
        bx -= dw + 6 * sc;
        if (Button("##designer", "Designer", new Vector2(bx, mid - bh / 2), new Vector2(dw, bh)))
            OpenDesigner?.Invoke();
        var (compiled, _, of) = Counts();
        if (compiled < of)
        {
            if (topLoadKey != of - compiled)
                (topLoadKey, topLoad) = (of - compiled, $"building {of - compiled} class{(of - compiled == 1 ? "" : "es")}…");
            float tw = Width(UiFonts.SansSmall, 12, topLoad);
            Txt(UiFonts.SansSmall, 12, new Vector2(bx - 12 * sc - tw, mid - 8 * sc), Muted, topLoad);
        }
    }

    /// <summary>The picked (or begun) scenario's classes: compiled, drawn, of all.</summary>
    (int Compiled, int Drawn, int Of) Counts()
    {
        if (world.LoadClasses is not { } ids)
            return (0, 0, 0);
        int c = 0, d = 0;
        foreach (int id in ids)
        {
            c += world.Classes.IsReady(id) ? 1 : 0;
            d += world.Classes.IsDrawn(id) ? 1 : 0;
        }
        return (c, d, ids.Count);
    }

    // ------------------------------------------------------------------ the scenarios

    void List(Vector2 o, float w, float h)
    {
        dl.AddRectFilled(o, o + new Vector2(w, h), U(ListBg));
        VLine(o.X + w, o.Y, o.Y + h);
        float x = o.X + 16 * sc, y = o.Y + 14 * sc;
        Tracked(UiFonts.Sans, 10.5f, new Vector2(x, y), Muted, "SCENARIOS", 0.9f);
        int shipped = 0;
        foreach (var e in entries)
            shipped += e.Test || e.Opened || e.Saved ? 0 : 1;
        string n = shipped == 1 ? "1 shipped" : $"{shipped} shipped";
        Txt(UiFonts.SansSmall, 11, new Vector2(o.X + w - 16 * sc - Width(UiFonts.SansSmall, 11, n), y), Muted, n);
        // the filters
        float fx = x, fy = y + 18 * sc;
        for (int i = 0; i < Filters.Length; i++)
        {
            float fw = TrackedWidth(UiFonts.Sans, 10.5f, Filters[i], 0.4f) + 14 * sc;
            ImGui.SetCursorScreenPos(new Vector2(fx, fy));
            if (ImGui.InvisibleButton($"##flt{i}", new Vector2(fw, 20 * sc)))
                filter = i;
            bool on = filter == i;
            Box(new Vector2(fx, fy), new Vector2(fx + fw, fy + 20 * sc), on ? Hex(0x2A2412) : new Vector4(0, 0, 0, 0), on ? BrassDark : Hex(0x3B4752), 2);
            Tracked(UiFonts.Sans, 10.5f, new Vector2(fx + 7 * sc, fy + 2.5f * sc), on ? Brass : Muted, Filters[i], 0.4f);
            fx += fw + 4 * sc;
        }
        // the cards, scrolling; your setups and the drop at the foot
        float footH = 150 * sc, cardsTop = fy + 30 * sc;
        ImGui.SetCursorScreenPos(new Vector2(o.X, cardsTop));
        ImGui.PushStyleColor(ImGuiCol.ChildBg, Vector4.Zero);
        if (ImGui.BeginChild("##cards", new Vector2(w - 1, o.Y + h - footH - cardsTop), ImGuiChildFlags.None, ImGuiWindowFlags.NoScrollbar))
        {
            var keep = dl;
            dl = ImGui.GetWindowDrawList();
            var p = ImGui.GetCursorScreenPos();
            float cy = p.Y, cx = o.X + 10 * sc, cw = w - 20 * sc;
            string? group = null;
            for (int i = 0; i < entries.Count; i++)
            {
                var e = entries[i];
                if (!Shown(e) || e.Saved)
                    continue;   // the setups are at the foot
                string g = e.Opened ? "OPENED" : e.Test ? "TESTS" : "SHIPPED";
                if (g != group)
                {
                    group = g;
                    Tracked(UiFonts.Sans, 10, new Vector2(cx + 2 * sc, cy + 6 * sc), Muted, g, 0.9f);
                    cy += 26 * sc;
                }
                cy += Card(i, e, new Vector2(cx, cy), cw) + 7 * sc;
            }
            ImGui.SetCursorScreenPos(new Vector2(cx, cy));
            ImGui.Dummy(new Vector2(1, 4 * sc));
            dl = keep;
        }
        ImGui.EndChild();
        ImGui.PopStyleColor();
        float y0 = o.Y + h - footH;
        HLine(o.X, o.X + w, y0);
        Tracked(UiFonts.Sans, 10, new Vector2(o.X + 14 * sc, y0 + 12 * sc), Muted, "YOUR SETUPS", 0.9f);
        float rowsTop = y0 + 30 * sc, rowsH = footH - 60 * sc;
        ImGui.SetCursorScreenPos(new Vector2(o.X, rowsTop));
        ImGui.PushStyleColor(ImGuiCol.ChildBg, Vector4.Zero);
        if (ImGui.BeginChild("##setups", new Vector2(w - 1, rowsH), ImGuiChildFlags.None, ImGuiWindowFlags.None))
        {
            var keep = dl;
            dl = ImGui.GetWindowDrawList();
            var p = ImGui.GetCursorScreenPos();
            float ry = p.Y, rw = w - 24 * sc;
            bool any = false;
            for (int i = 0; i < entries.Count; i++)
            {
                var e = entries[i];
                if (!e.Saved)
                    continue;
                any = true;
                var a = new Vector2(o.X + 8 * sc, ry);
                ImGui.SetCursorScreenPos(a);
                if (ImGui.InvisibleButton($"##setup{i}", new Vector2(rw, 26 * sc)) && i != pick)
                    Pick(i);
                bool on = i == pick, hover = ImGui.IsItemHovered();
                if (on || hover)
                    dl.AddRectFilled(a, a + new Vector2(rw, 26 * sc), U(on ? Brass with { W = 0.08f } : new Vector4(1, 1, 1, 0.04f)), 2 * sc);
                var t = a + new Vector2(8 * sc, 13 * sc);
                dl.AddTriangleFilled(t - new Vector2(0, 4 * sc), t + new Vector2(5 * sc, 0), t + new Vector2(0, 4 * sc), U(Brass));
                Txt(UiFonts.SansSmall, 12, a + new Vector2(20 * sc, 5 * sc), e.Error != null ? Hostile : on ? Bright : Text, e.Name);
                float ww = Width(UiFonts.SansSmall, 12, e.When);
                Txt(UiFonts.SansSmall, 12, a + new Vector2(rw - 8 * sc - ww, 5 * sc), Muted, e.When);
                if (hover && e.Error != null)
                    ImGui.SetTooltip(e.Error);
                ry += 28 * sc;
            }
            if (!any)
                Txt(UiFonts.SansSmall, 12, new Vector2(o.X + 14 * sc, ry + 4 * sc), Muted, "None yet: change a scenario, then Save setup…");
            ImGui.SetCursorScreenPos(new Vector2(o.X, ry));
            ImGui.Dummy(new Vector2(1, 2 * sc));
            dl = keep;
        }
        ImGui.EndChild();
        ImGui.PopStyleColor();
        Txt(UiFonts.SansSmall, 12, new Vector2(o.X + 14 * sc, o.Y + h - 26 * sc), Brass, "+");
        Txt(UiFonts.SansSmall, 12, new Vector2(o.X + 30 * sc, o.Y + h - 26 * sc), Muted, "Open a scenario file: drop it on the window");
    }

    bool Shown(Entry e) => filter switch
    {
        1 => !e.Test && e.Year <= 1905,
        2 => !e.Test && e.Year is >= 1906 and <= 1918,
        3 => !e.Test && e.Ships < SmallShips,
        4 => !e.Test && e.Ships >= SmallShips,
        5 => e.Test,
        _ => true,
    };

    /// <summary>A scenario's card: its name, date and place, each side's ships, its classes (and the load's bar when it's
    /// the pick); returns its height.</summary>
    float Card(int i, Entry e, Vector2 at, float w)
    {
        bool on = i == pick, small = e.Test;
        bool hasSetups = !e.Test && !e.Opened && setupsOf.Contains(Path.GetFileNameWithoutExtension(e.Path));
        float h = (small ? 60 : hasSetups ? 94 : 78) * sc;
        ImGui.SetCursorScreenPos(at);
        if (ImGui.InvisibleButton($"##sc{i}", new Vector2(w, h)) && i != pick)
            Pick(i);
        bool hover = ImGui.IsItemHovered();
        Box(at, at + new Vector2(w, h), on ? Brass with { W = 0.06f } : new Vector4(1, 1, 1, hover ? 0.04f : 0.015f), on ? BrassDark : LineC);
        if (on)
            dl.AddRectFilled(at, at + new Vector2(3 * sc, h), U(Brass));
        float x = at.X + 12 * sc, y = at.Y + 9 * sc;
        if (small)
        {
            Txt(UiFonts.Title, 14, new Vector2(x, y), Bright, e.Name);
            Txt(UiFonts.SansSmall, 11.5f, new Vector2(x, y + 22 * sc), Muted, e.Error ?? e.Date);
        }
        else
        {
            Txt(UiFonts.Title, 16, new Vector2(x, y), Bright, e.Name);
            Txt(UiFonts.SansSmall, 11.5f, new Vector2(x, y + 22 * sc), e.Error != null ? Hostile : Muted, e.Error ?? e.Date);
            float sx = x, sy = y + 42 * sc;
            for (int k = 0; k < e.Sides.Length; k++)
            {
                if (k > 0)
                {
                    Txt(UiFonts.SansSmall, 11.5f, new Vector2(sx, sy), Muted, "v");
                    sx += 14 * sc;
                }
                var (name, count) = e.Sides[k];
                Txt(UiFonts.SansSmall, 11.5f, new Vector2(sx, sy), Text, name);
                sx += Width(UiFonts.SansSmall, 11.5f, name) + 4 * sc;
                Txt(UiFonts.Mono, 11.5f, new Vector2(sx, sy + 1 * sc), Bright, count);
                sx += Width(UiFonts.Mono, 11.5f, count) + 10 * sc;
            }
        }
        if (hasSetups)
        {
            var d = new Vector2(at.X + 15 * sc, at.Y + 80 * sc);
            dl.AddQuadFilled(d - new Vector2(0, 3.5f * sc), d + new Vector2(3.5f * sc, 0), d + new Vector2(0, 3.5f * sc), d - new Vector2(3.5f * sc, 0), U(Brass));
            Txt(UiFonts.SansSmall, 11, new Vector2(at.X + 23 * sc, at.Y + 72 * sc), Brass, "a saved setup of yours");
        }
        if (e.Classes.Length > 0)
        {
            float tw = Width(UiFonts.SansSmall, 10.5f, e.Classes);
            Txt(UiFonts.SansSmall, 10.5f, new Vector2(at.X + w - 10 * sc - tw, at.Y + 9 * sc), Muted, e.Classes);
            if (on)
            {
                var (c, d, of) = Counts();
                if (of > 0)
                {
                    var b0 = new Vector2(at.X + w - 10 * sc - 64 * sc, at.Y + 26 * sc);
                    dl.AddRectFilled(b0, b0 + new Vector2(64 * sc, 3 * sc), U(Hex(0x222A31)));
                    dl.AddRectFilled(b0, b0 + new Vector2(64 * sc * c / of, 3 * sc), U(Hex(0x6B5A2E)));
                    dl.AddRectFilled(b0, b0 + new Vector2(64 * sc * d / of, 3 * sc), U(Brass));
                }
            }
        }
        return h;
    }

    // ------------------------------------------------------------------ the header: what it is, the sides, the opening

    // the pick's strings, made when it changes (Made)
    string kicker = "", meta = "", brief = "";
    readonly List<SideCard> cards = [];

    sealed class SideCard
    {
        public int Side;
        public string Name = "", Ships = "", Character = "", Commander = "", Ensign = "";
        public int Capital = -1, Cruiser, Light;   // the strength pips, once the classes are in (-1 until)
    }

    /// <summary>The pick's header and side cards, and the opening, made once per pick and side.</summary>
    void Made(Scenario s)
    {
        var start = s.Start.ToUtc();
        kicker = $"{(s.Setup is { IsEmpty: false } ? $"YOUR SETUP OF {s.Written!.Name.ToUpperInvariant()}" : "SCENARIO")}{(s.Place.Length > 0 ? " · " + s.Place.ToUpperInvariant() : "")} · {start.Year}";
        var (lat, lon) = Centre(s);
        var local = start.AddHours(lon / 15);   // local mean time, by the sun
        var parts = new List<string>
        {
            $"{local:dddd d MMMM yyyy, HH:mm} local ({start:HH:mm} GMT)".Replace("  ", " "),
            $"{Dms(lat, 'N', 'S')} {Dms(lon, 'E', 'W')}",
        };
        if (s.Weather is { } wx)
            parts.Add($"{Sight(wx.VisibilityM)}, wind {Compass(wx.WindFromDeg)} {wx.WindMs / 0.514444:0} kn");
        if (SunTurn(s.Start, Earth.Position(lat, lon)) is { } turn)
            parts.Add($"{turn.Word} {turn.At.ToUtc().AddHours(lon / 15):HH:mm}");
        if (s.Until is { } u)
            parts.Add($"ends {u.ToUtc().AddHours(lon / 15).ToString("d MMMM HH:mm", CultureInfo.InvariantCulture)}");
        meta = string.Join(" · ", parts);
        brief = s.Brief;
        cards.Clear();
        for (int i = 0; i < s.Sides.Length; i++)
        {
            int ships = s.Units.Where(u => u.Side == i).Sum(u => u.ShipCount);
            if (ships == 0)
                continue;
            var about = s.AboutOf(i);
            cards.Add(new SideCard
            {
                Side = i, Name = s.Sides[i].ToUpperInvariant(), Ships = $"{ships} ship{(ships == 1 ? "" : "s")}",
                Character = about?.Character ?? "", Commander = about is { Commander.Length: > 0 } a ? "Commander: " + a.Commander : "",
                Ensign = about?.Ensign ?? "",
            });
        }
        MadeBody(s);
        opening = null;   // drawn again for this pick and side
    }

    /// <summary>Where the scenario is: its camera's centre, else its top units' mean.</summary>
    static (double Lat, double Lon) Centre(Scenario s)
    {
        if (s.Camera is { } c)
            return (c.Lat, c.Lon);
        var heads = s.Units.Where(u => u.At != null).Select(u => u.At!.Value).ToList();
        return heads.Count > 0 ? (heads.Average(h => h.Lat), heads.Average(h => h.Lon)) : (0, 0);
    }

    static string Dms(double v, char pos, char neg)
    {
        double a = Math.Abs(v);
        int d = (int)a, m = (int)Math.Round((a - d) * 60);
        if (m == 60)
            (d, m) = (d + 1, 0);
        return $"{d}°{m:00}′{(v >= 0 ? pos : neg)}";
    }

    static string Sight(double m) => (m < 1000 ? "fog" : m < 5000 ? "mist" : m < 18000 ? "haze" : "clear") + $", {m / 1000:0} km";

    static string Compass(double deg) =>
        new[] { "N", "NNE", "NE", "ENE", "E", "ESE", "SE", "SSE", "S", "SSW", "SW", "WSW", "W", "WNW", "NW", "NNW" }[(int)Math.Round(((deg % 360) + 360) % 360 / 22.5) % 16];

    /// <summary>The next sunset after the start (or sunrise, if it starts at night) within a day, by the minute: the sun's
    /// upper limb at the horizon, refraction in (-0.833°).</summary>
    static (string Word, WorldTime At)? SunTurn(WorldTime start, Vec3d at)
    {
        const double Horizon = -0.833;
        bool up = Light.SunElevationDeg(at, start) > Horizon;
        for (int m = 1; m <= 24 * 60; m++)
        {
            var t = start + m * 60_000L;
            if (Light.SunElevationDeg(at, t) > Horizon != up)
                return (up ? "sunset" : "sunrise", t);
        }
        return null;
    }

    void Header(Vector2 o, float w, float h)
    {
        HLine(o.X, o.X + w, o.Y + h);
        if (pick < 0)
            return;
        var e = entries[pick];
        float x = o.X + 26 * sc, y = o.Y + 18 * sc;
        if (current == null)
        {
            Txt(UiFonts.Title, 16, new Vector2(x, y), Hostile, e.Scenario == null ? $"{e.Name} couldn't be read" : $"{e.Name}: a change no longer applies");
            Wrapped(UiFonts.Sans, 13, new Vector2(x, y + 26 * sc), Muted, e.Error ?? setupError ?? "", 800 * sc);
            if (e.Scenario != null && Button("##resetbad", "Reset to the scenario as written", new Vector2(x, y + 70 * sc), new Vector2(230 * sc, 24 * sc)))
                Edit(setup with { Changes = [] });
            return;
        }
        if (placing != null)
        {
            // the placing view: the opening widened over the header
            Opening(new Vector2(x, y), new Vector2(w - 48 * sc, h - 34 * sc));
            return;
        }
        Tracked(UiFonts.Sans, 10.5f, new Vector2(x, y), Brass, kicker, 1.2f);
        Txt(UiFonts.PosterTitle, 34, new Vector2(x, y + 14 * sc), Bright, current.Name);
        Txt(UiFonts.SansSmall, 12, new Vector2(x, y + 58 * sc), Muted, meta);
        float wrap = Math.Min(800 * sc, w - 700 * sc);
        Wrapped(UiFonts.Sans, 13.5f, new Vector2(x, y + 82 * sc), BriefC, brief, wrap);
        // the sides, and Watch
        float cx = x, cy = o.Y + 196 * sc;
        foreach (var c in cards)
        {
            SideCardDraw(c, new Vector2(cx, cy), new Vector2(272 * sc, 128 * sc));
            cx += 282 * sc;
        }
        WatchCard(new Vector2(cx, cy), new Vector2(180 * sc, 128 * sc));
        // the opening, at the right
        var pa = new Vector2(o.X + w - 22 * sc - 640 * sc, o.Y + 18 * sc);
        Opening(pa, new Vector2(640 * sc, 298 * sc));
    }

    void SideCardDraw(SideCard c, Vector2 at, Vector2 size)
    {
        bool on = side == c.Side;
        ImGui.SetCursorScreenPos(at);
        if (ImGui.InvisibleButton($"##side{c.Side}", size) && !on && !begin)
            pendingSide = c.Side;   // at the frame's end: the cards are being drawn
        bool hover = ImGui.IsItemHovered();
        Box(at, at + size, on ? Brass with { W = 0.07f } : new Vector4(1, 1, 1, hover ? 0.05f : 0.02f), on ? Brass : EdgeC);
        float x = at.X + 12 * sc, y = at.Y + 10 * sc;
        Ensign(c.Ensign, new Vector2(x, y + 2 * sc), new Vector2(26 * sc, 16 * sc));
        Tracked(UiFonts.Title, 16, new Vector2(x + 34 * sc, y), Bright, c.Name, 1);
        if (on)
            Chip(new Vector2(at.X + size.X - 40 * sc, y + 2 * sc), "YOU", BrassBg, Brass);
        else
            Chip(new Vector2(at.X + size.X - 34 * sc, y + 2 * sc), "AI", AiBg, Friend);
        float py = y + 27 * sc, px = x;
        if (Pips(c) && c.Capital + c.Cruiser + c.Light is > 0 and <= 24)
        {
            var col = U(on || side < 0 && c.Side == 0 ? Friend : Hostile);
            for (int k = 0; k < c.Capital; k++, px += 9 * sc)
                dl.AddRectFilled(new Vector2(px, py + 2 * sc), new Vector2(px + 7 * sc, py + 11 * sc), col);
            for (int k = 0; k < c.Cruiser; k++, px += 7 * sc)
                dl.AddRectFilled(new Vector2(px, py + 4 * sc), new Vector2(px + 5 * sc, py + 11 * sc), col);
            for (int k = 0; k < Math.Min(c.Light, 6); k++, px += 5 * sc)
                dl.AddRectFilled(new Vector2(px, py + 6 * sc), new Vector2(px + 3 * sc, py + 11 * sc), col);
            px += 8 * sc;
        }
        Txt(UiFonts.SansSmall, 11.5f, new Vector2(px, py), Muted, c.Ships);
        float ch = Wrapped(UiFonts.SansSmall, 12, new Vector2(x, py + 20 * sc), Soft, c.Character, size.X - 24 * sc);
        if (c.Commander.Length > 0)
            Txt(UiFonts.SansSmall, 11, new Vector2(x, Math.Min(py + 24 * sc + ch, at.Y + size.Y - 22 * sc)), Muted, c.Commander);
    }

    /// <summary>A side's strength, its ships by type, once every class of the pick is compiled.</summary>
    bool Pips(SideCard c)
    {
        if (c.Capital >= 0)
            return true;
        if (current is not { } s || world.LoadIds is not { } ids)
            return false;
        int cap = 0, cru = 0, light = 0;
        foreach (var u in s.Units)
            if (u.Side == c.Side && !Count(u, ref cap, ref cru, ref light))
                return false;
        (c.Capital, c.Cruiser, c.Light) = (cap, cru, light);
        return true;

        bool Count(ScenarioUnit u, ref int cap, ref int cru, ref int light)
        {
            foreach (var sub in u.Units)
                if (!Count(sub, ref cap, ref cru, ref light))
                    return false;
            foreach (var ships in u.Ships)
            {
                if (!ids.TryGetValue(ships.Design, out int id) || !world.Classes.IsReady(id))
                    return false;
                var type = world.Classes.TryGet(id) is { } cls ? Sightings.TypeOf(cls) : ShipType.Unknown;
                if (type == ShipType.Capital)
                    cap += ships.Count;
                else if (type == ShipType.Cruiser)
                    cru += ships.Count;
                else
                    light += ships.Count;
            }
            return true;
        }
    }

    void WatchCard(Vector2 at, Vector2 size)
    {
        bool on = side < 0;
        ImGui.SetCursorScreenPos(at);
        if (ImGui.InvisibleButton("##watch", size) && !on && !begin)
            pendingSide = -1;
        bool hover = ImGui.IsItemHovered();
        Box(at, at + size, on ? Brass with { W = 0.07f } : new Vector4(1, 1, 1, hover ? 0.05f : 0.02f), on ? Brass : EdgeC);
        float x = at.X + 12 * sc, y = at.Y + 10 * sc;
        Ensign("", new Vector2(x, y + 2 * sc), new Vector2(26 * sc, 16 * sc));
        dl.AddCircle(new Vector2(x + 13 * sc, y + 10 * sc), 4 * sc, U(Muted), 12, 1);
        dl.AddCircleFilled(new Vector2(x + 13 * sc, y + 10 * sc), 1.5f * sc, U(Muted));
        Tracked(UiFonts.Title, 16, new Vector2(x + 34 * sc, y), Bright, "WATCH", 1);
        Wrapped(UiFonts.SansSmall, 12, new Vector2(x, y + 34 * sc), Soft, "Both sides the AI's; you see everything. For checking a setup before playing it.", size.X - 24 * sc);
    }

    // ------------------------------------------------------------------ the body: the ships; until L4-L5, the aims and orders

    const float ShipsW = 600;   // the ships column, reference px (the picker opens right of it)

    readonly List<(string Label, string Value)> aims = [], orders = [];
    string ordersTitle = "";

    void MadeBody(Scenario s)
    {
        MadeShips(s);
        aims.Clear();
        orders.Clear();
        var (_, lon) = Centre(s);
        string end = s.Until is { } u2 ? $"at {u2.ToUtc().AddHours(lon / 15):HH:mm} local" : "";
        for (int sd = 0; sd < s.Sides.Length; sd++)
            if (side < 0 || sd == side)
                aims.Add((side < 0 ? s.Sides[sd] : "The aim", MapScene.AimsText(s, sd)));
        aims.Add(("The end", $"when one side is gone or out of the fight{(end.Length > 0 ? ", or " + end : "")}"));
        ordersTitle = side < 0 ? "ORDERS BEFORE ACTION" : $"ORDERS BEFORE ACTION · {s.Sides[side].ToUpperInvariant()}";
        void Orders(string label, IEnumerable<(int Side, string Name)> list)
        {
            foreach (var (sd, name) in list)
                if (side < 0 || sd == side)
                    orders.Add((side < 0 ? $"{label}, {s.Sides[sd]}" : label, MapScene.Pretty(name)));
        }
        Orders("Gun crews", s.Crews.Select(c => (c.Side, c.Quality.ToString().ToLowerInvariant())));
        Orders("Readiness", s.Readiness);
        Orders("Damage control", s.DamageControl);
        Orders("Fire discipline", s.FireDisciplines);
    }

    void Body(Vector2 o, float w, float h)
    {
        if (current == null)
            return;
        ShipsColumn(new Vector2(o.X + 18 * sc, o.Y + 14 * sc), ShipsW * sc - 36 * sc, o.Y + h);
        float x = o.X + ShipsW * sc;
        VLine(x, o.Y, o.Y + h);
        // the aims and the orders before action (the standing orders' place, L4)
        float cx = x + 18 * sc, cy = o.Y + 14 * sc, cw = 520 * sc - 36 * sc;
        Tracked(UiFonts.Sans, 10.5f, new Vector2(cx, cy), Muted, "THE AIM", 0.9f);
        cy += 24 * sc;
        foreach (var (label, value) in aims)
        {
            Txt(UiFonts.SansSmall, 12, new Vector2(cx, cy), Soft, label);
            cy += Wrapped(UiFonts.SansSmall, 12, new Vector2(cx + 118 * sc, cy), Bright, value, cw - 118 * sc) + 6 * sc;
        }
        if (orders.Count > 0)
        {
            cy += 14 * sc;
            Tracked(UiFonts.Sans, 10.5f, new Vector2(cx, cy), Muted, ordersTitle, 0.9f);
            cy += 24 * sc;
            foreach (var (label, value) in orders)
            {
                Txt(UiFonts.SansSmall, 12, new Vector2(cx, cy), Soft, label);
                Txt(UiFonts.SansSmall, 12, new Vector2(cx + (side < 0 ? 170 : 118) * sc, cy), Bright, value);
                cy += 20 * sc;
            }
        }
        x += 520 * sc;
        VLine(x, o.Y, o.Y + h);
        // the changes (under the conditions, L5)
        Changes(new Vector2(x + 18 * sc, o.Y + 14 * sc), o.X + w - x - 36 * sc);
    }

    /// <summary>Every change from the scenario as written, a line each with its undo, and Reset all (the mockup's
    /// "Changes from the scenario").</summary>
    void Changes(Vector2 at, float w)
    {
        float y = at.Y;
        Tracked(UiFonts.Sans, 10.5f, at, Muted, "CHANGES FROM THE SCENARIO", 0.9f);
        if (setup.IsEmpty)
        {
            const string None = "none: as written";
            Txt(UiFonts.SansSmall, 11, new Vector2(at.X + w - Width(UiFonts.SansSmall, 11, None), y), Muted, None);
            return;
        }
        const string Reset = "Reset all";
        float rw = Width(UiFonts.SansSmall, 11.5f, Reset);
        ImGui.SetCursorScreenPos(new Vector2(at.X + w - rw, y));
        bool reset = ImGui.InvisibleButton("##resetall", new Vector2(rw, 16 * sc));
        Txt(UiFonts.SansSmall, 11.5f, new Vector2(at.X + w - rw, y), ImGui.IsItemHovered() ? Hex(0xE6C77A) : Brass, Reset);
        y += 24 * sc;
        SetupChange? undo = null;
        foreach (var c in setup.Changes)
        {
            HLine(at.X, at.X + w, y + 21 * sc);
            Txt(UiFonts.SansSmall, 12, new Vector2(at.X, y + 2 * sc), Brass, Described(c));
            ImGui.SetCursorScreenPos(new Vector2(at.X + w - 16 * sc, y));
            if (ImGui.InvisibleButton($"##undo{c.Key}", new Vector2(16 * sc, 20 * sc)))
                undo = c;
            bool hover = ImGui.IsItemHovered();
            var xc = new Vector2(at.X + w - 7 * sc, y + 10 * sc);
            uint xcol = U(hover ? Hostile : Muted);
            dl.AddLine(xc - new Vector2(3.5f, 3.5f) * sc, xc + new Vector2(3.5f, 3.5f) * sc, xcol, 1.2f * sc);
            dl.AddLine(xc + new Vector2(-3.5f, 3.5f) * sc, xc + new Vector2(3.5f, -3.5f) * sc, xcol, 1.2f * sc);
            if (hover)
                ImGui.SetTooltip("undo: as written");
            y += 24 * sc;
        }
        if (reset)
            Edit(setup with { Changes = [] });
        else if (undo != null)
            Edit(undo is AddUnit a ? setup.WithoutAdded(a.Unit.Name) : setup.Without(undo.Key));
    }

    // each change's line, made once (a frame allocates nothing for a list that stands); by the change itself, as a
    // change is a new one when it's changed
    readonly Dictionary<SetupChange, string> described = new(ReferenceEqualityComparer.Instance);

    string Described(SetupChange c)
    {
        if (!described.TryGetValue(c, out var s))
            described[c] = s = c switch
            {
                SwapDesign sw => sw.Describe(written!, TitleOf),
                AddUnit a => a.Describe(written!, TitleOf),
                ChangeCount n => n.Describe(written!, TitleOf),
                _ => c.Describe(written!),
            };
        return s;
    }

    // ------------------------------------------------------------------ the foot: the load and Begin

    string loadHead = "", loadLine = "";
    (int, int, int, int, bool) loadKey = (-1, 0, 0, 0, false);

    void Footer(Vector2 o, float w, float h)
    {
        dl.AddRectFilled(o, o + new Vector2(w, h), U(Bar));
        HLine(o.X, o.X + w, o.Y);
        var (compiled, drawn, of) = Counts();
        int tenths = compiled < of ? (int)(world.LoadClock * 10) : -1;   // the clock only while it builds
        var key = (compiled, drawn, of, tenths, world.Beginning);
        if (key != loadKey)
        {
            loadKey = key;
            string threads = $"{world.Classes.Threads} builder thread{(world.Classes.Threads == 1 ? "" : "s")}";
            if (of == 0)
                (loadHead, loadLine) = ("", "");
            else if (compiled < of)
                (loadHead, loadLine) = ("Building the ships", $"classes {compiled} of {of} · art {drawn} of {of} · {world.LoadClock:0.0} s   ·   {threads}");
            else
                (loadHead, loadLine) = (world.Beginning ? "Beginning" : "Ready",
                    $"{of} class{(of == 1 ? "" : "es")} built · {(drawn < of ? $"art {drawn} of {of}" : "art in")}   ·   {threads} · built classes are kept when you switch scenarios");
        }
        float x = o.X + 22 * sc, y = o.Y + 14 * sc;
        if (loadHead.Length > 0)
        {
            Txt(UiFonts.SansBold, 12, new Vector2(x, y), Bright, loadHead);
            Txt(UiFonts.SansSmall, 12, new Vector2(x + Width(UiFonts.SansBold, 12, loadHead) + 14 * sc, y), Muted, loadLine);
        }
        if (world.LoadError is { } err)
            Txt(UiFonts.SansSmall, 12, new Vector2(x, y), Hostile, err);
        // a segment a class: dark while it builds, darker brass compiled, brass with its art
        if (of > 0 && world.LoadClasses is { } ids)
        {
            float sw = Math.Min(820 * sc, w - 560 * sc), gap = 2 * sc, seg = Math.Max((sw - gap * (of - 1)) / of, 3 * sc);
            float sx = x, sy = o.Y + 36 * sc;
            foreach (int id in ids)
            {
                var fill = world.Classes.IsDrawn(id) ? Brass : world.Classes.IsReady(id) ? Hex(0x6B5A2E) : Hex(0x1F262C);
                dl.AddRectFilled(new Vector2(sx, sy), new Vector2(sx + seg, sy + 8 * sc), U(fill));
                sx += seg + gap;
                if (sx > x + sw)
                    break;
            }
        }
        // the buttons
        bool ready = current != null;
        float bh = 34 * sc, by = o.Y + (h - bh) / 2, bx = o.X + w - 22 * sc;
        float pw = 170 * sc;
        bx -= pw;
        string go = begin ? "Begins when ready…" : side < 0 ? "Watch" : "Begin";
        if (Button("##begin", go, new Vector2(bx, by), new Vector2(pw, bh), primary: true, enabled: ready && !begin, px: 13.5f))
            BeginWorld(paused: false);
        float qw = 108 * sc;
        bx -= qw + 8 * sc;
        if (Button("##paused", "Begin paused", new Vector2(bx, by), new Vector2(qw, bh), enabled: ready && !begin, px: 13.5f))
            BeginWorld(paused: true);
        bx -= qw + 8 * sc;
        bool canSave = current != null && !setup.IsEmpty && setup.From.Length > 0 && !begin;
        if (Button("##save", "Save setup…", new Vector2(bx, by), new Vector2(qw, bh), enabled: canSave, px: 13.5f))
            OpenSave();
        if (ImGui.IsItemHovered() && !canSave)
            ImGui.SetTooltip(setup.IsEmpty ? "nothing changed from the scenario as written" : "a setup is of a shipped scenario");
        SaveDialog();
    }
}
