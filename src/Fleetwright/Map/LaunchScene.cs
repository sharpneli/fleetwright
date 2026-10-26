using System.Globalization;
using System.Numerics;
using System.Text.Json;
using Fleetwright.Gpu;
using Fleetwright.Sim;
using Fleetwright.Sim.Core;
using Fleetwright.Sim.Missions;
using ImGuiNET;
using SDL;
using static SDL.SDL3;
using static Fleetwright.Ui.Pieces;
using T = Fleetwright.Ui.Theme;

namespace Fleetwright.Map;

/// <summary>The scenario launch (docs/map-ui/launch.md L1, in the UI language of docs/ui-language.md and the mockup
/// launch-bureau.html): the app's first screen, open before any ship is built, laid out as a workstation like the
/// designer. A menu bar and a resource bar of the picked scenario's figures; at the left the scenarios as an outliner by
/// era over Your setups; in the centre the opening drawn as a plan (<c>LaunchScene.Opening.cs</c>) over three numbered
/// tabs: the order of battle (L3, <c>LaunchScene.Ships.cs</c>, a design changed by the picker in
/// <c>LaunchScene.Picker.cs</c>), the standing orders (L4, <c>LaunchScene.Orders.cs</c>), the conditions, the day and the
/// changes (L5, <c>LaunchScene.Conditions.cs</c>); at the right Play as, the strength ledger, the brief, the remarks and
/// the Begin box; a status bar of keys. Picking a scenario or a side asks for its classes and places its world at once
/// (<see cref="WorldSession.Select(Scenario, string?)"/>), so reading the brief covers the build; Begin swaps the world in
/// (<see cref="WorldSession.Begin"/>) and <see cref="Begun"/> shows the map. What the player changes is a setup
/// (<see cref="ScenarioSetup"/>, L2): the scenario as written plus its changes, listed with an undo each, saved as a
/// scenario file in My Games/Fleetwright/Setups and listed under Your setups. Its strings are made when the pick changes,
/// so a frame allocates nothing but the load's line while it builds.</summary>
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
        public string Name = "", When = "", Era = "", Ships = "", Year = "—", Of = "";   // Of: a setup's scenario
        public string[] Ensigns = [];
        public int StartYear, ShipCount;
    }

    readonly List<Entry> entries = [];
    int pick = -1;
    int side;   // the player's side, -1 to watch

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

    /// <summary>The shipped first by their dates, the scale test's rungs and the benches (a file's "test") under Tests, the player's setups
    /// newest first, the files opened last.</summary>
    void Sort()
    {
        entries.Sort((a, b) => Group(a) != Group(b) ? Group(a).CompareTo(Group(b))
            : a.Saved ? b.Modified.CompareTo(a.Modified)
            : (a.Scenario?.Start.Ms ?? long.MaxValue, a.Name).CompareTo((b.Scenario?.Start.Ms ?? long.MaxValue, b.Name)));
        groupCounts.Clear();
    }

    static int Group(Entry e) => e.Opened ? 3 : e.Saved ? 2 : e.Test ? 1 : 0;

    /// <summary>How the last one played is kept: a shipped scenario's file name, a setup's as "setups/name".</summary>
    static string KeyOf(Entry e) => (e.Saved ? "setups/" : "") + Path.GetFileNameWithoutExtension(e.Path);

    static Entry Read(string path, bool opened)
    {
        var e = new Entry { Path = path, Opened = opened, Name = Path.GetFileNameWithoutExtension(path) };
        e.Saved = !opened && Path.GetFullPath(Path.GetDirectoryName(path)!).Equals(Path.GetFullPath(SetupsFolder), StringComparison.OrdinalIgnoreCase);
        e.Modified = File.GetLastWriteTime(path);
        e.When = e.Modified.Date == DateTime.Today ? "today" : e.Modified.ToString("MMM d", CultureInfo.InvariantCulture);
        try
        {
            var s = Scenario.Load(path, [ShippedFolder]);
            e.Scenario = s;
            e.Name = s.Name;
            e.Test = s.Test;
            e.StartYear = s.Start.ToUtc().Year;
            e.Year = e.StartYear.ToString(CultureInfo.InvariantCulture);
            e.ShipCount = s.ShipCount;
            var counts = Enumerable.Range(0, s.Sides.Length).Select(i => s.Units.Where(u => u.Side == i).Sum(u => u.ShipCount)).ToArray();
            var sides = counts.Where(n => n > 0).ToArray();
            e.Ships = sides.Length <= 2 ? string.Join(":", sides) : sides.Sum().ToString(CultureInfo.InvariantCulture);   // more sides: the whole
            e.Ensigns = [.. Enumerable.Range(0, s.Sides.Length).Where(i => counts[i] > 0).Select(i => s.AboutOf(i)?.Ensign ?? "")];
            e.Of = s.Written?.Name ?? "";
        }
        catch (Exception x) when (x is ScenarioException or IOException or JsonException)
        {
            e.Error = x.Message;
        }
        e.Era = opened ? "Opened" : e.Test ? "Tests" : e.StartYear <= 1905 ? "1890–1905" : e.StartYear <= 1925 ? "1906–1925" : "1926–";
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
        shutGroups.Remove(entries[at].Era);
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
        else if (world.Selected == null && pick >= 0 && written != null)
            Apply();   // a seed new each time is drawn again
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
        selKey = null;
        (written, current, setupError) = (null, null, null);
        if (i < 0 || entries[i].Scenario is not { } s)
            return;
        written = s.Written ?? s;
        described.Clear();
        drawnSeed = 0;
        try
        {
            presets = ScenarioLoader.PresetsFor(written);
        }
        catch (ScenarioException)
        {
            presets = Sim.Ai.DoctrinePresets.Builtin;   // its world says why when it's placed
        }
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

    int? pendingSide;   // a Play as row clicked this frame

    void SetSide(int sd)
    {
        if (written == null)
            return;
        string? name = sd >= 0 ? written.Sides[sd] : null;
        Edit(name == written.PlayerName ? setup.Without("player") : setup.With(new PlayAs(name)));
    }

    /// <summary>The newest change undone (Ctrl+Z, Edit's Undo).</summary>
    ScenarioSetup? UndoLast() => setup.Changes.Count == 0 ? null
        : setup.Changes[^1] is AddUnit a ? setup.WithoutAdded(a.Unit.Name) : setup.Without(setup.Changes[^1].Key);

    void Apply()
    {
        try
        {
            current = setup.Apply(written!);
            if (setup.Changes.Any(c => c is SetSeed { Seed: null }))
            {
                // a new seed each time it's begun: drawn once for the pick, again after each Begin
                if (drawnSeed == 0)
                    drawnSeed = (ulong)Random.Shared.NextInt64(1, 1_000_000);
                current = current with { Seed = drawnSeed };
            }
            setupError = null;
            side = current.Player;
        }
        catch (ScenarioException x)
        {
            (current, setupError) = (null, x.Message);
        }
        Select();
        if (current == null && written != null)
            MadeError();
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
        drawnSeed = 0;
        world.Begin();
        if (!entries[pick].Opened)
            WriteLast(KeyOf(entries[pick]));
    }

    public void Draw(SDL_GPUCommandBuffer* cmd, RenderTarget target, float dt)
    {
        art.UploadPending();   // the rows' and the picker's ships
        var g = T.Ground;
        var pass = target.BeginPass(cmd, new SDL_FColor { r = g.X, g = g.Y, b = g.Z, a = 1 }, depth: false);
        SDL_EndGPURenderPass(pass);
    }

    /// <summary>Esc shuts the picker, or stops placing a unit; with nothing open, the menu. WASD pans the opening
    /// (<see cref="PanKeys"/>, as on the map).</summary>
    public bool ProcessEvent(SDL_Event* e, uint width, uint height)
    {
        var type = (SDL_EventType)e->type;
        if (type is SDL_EventType.SDL_EVENT_KEY_DOWN or SDL_EventType.SDL_EVENT_KEY_UP && !saveOpen
            && panKeys.Key(e->key.scancode, type == SDL_EventType.SDL_EVENT_KEY_DOWN))
            return true;
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

    static float LeftW => P(300);
    static float RightW => P(340);
    static float ResBarH => P(34);
    static float StatusH => P(20);
    static float Gap => P(2);

    ImDrawListPtr dl;
    int tab = 1;   // 1 the order of battle, 2 the standing orders, 3 the conditions
    static readonly string[] TabNames = ["Order of battle", "Standing orders", "Conditions"];
    static readonly string[] Digits = ["1", "2", "3"];
    Vector2 centreAt, centreSize;   // the centre column: the picker opens over it

    public void BuildUi()
    {
        if (begin && world.Generation != beginGeneration)
        {
            begin = false;
            Begun?.Invoke();
        }
        else if (begin && world.LoadError != null)
            begin = false;
        Figure();
        Load();
        T.Push();
        Keys();
        MenuBar();
        var vp = ImGui.GetMainViewport();
        ImGui.SetNextWindowPos(vp.WorkPos);
        ImGui.SetNextWindowSize(vp.WorkSize);
        ImGui.PushStyleVar(ImGuiStyleVar.WindowPadding, Vector2.Zero);
        bool open = ImGui.Begin("##launch", ImGuiWindowFlags.NoDecoration | ImGuiWindowFlags.NoSavedSettings | ImGuiWindowFlags.NoMove |
                                            ImGuiWindowFlags.NoBringToFrontOnFocus | ImGuiWindowFlags.NoNav | ImGuiWindowFlags.NoScrollWithMouse);
        ImGui.PopStyleVar();
        if (open)
        {
            dl = ImGui.GetWindowDrawList();
            ImGui.PushFont(UiFonts.Sans);
            var o = vp.WorkPos;
            float w = vp.WorkSize.X, h = vp.WorkSize.Y;
            ResourceBar(o, w);
            float top = o.Y + ResBarH, bodyH = h - ResBarH - StatusH;

            // left: the scenarios over your setups
            float setupsH = MathF.Min(TitleH + RowH * Math.Max(1, SetupCount) + P(30), bodyH * 0.4f);
            ScenariosPane(new Vector2(o.X, top), new Vector2(LeftW, bodyH - setupsH - Gap));
            SetupsPane(new Vector2(o.X, top + bodyH - setupsH), new Vector2(LeftW, setupsH));

            // the centre: the opening, the tabs, the tab's pages
            float cx = o.X + LeftW + Gap, cw = w - LeftW - RightW - 2 * Gap;
            (centreAt, centreSize) = (new Vector2(cx, top), new Vector2(cw, bodyH));
            float openH = MathF.Round(Math.Clamp(bodyH * 0.4f, P(260), P(440)));
            OpeningPane(new Vector2(cx, top), new Vector2(cw, openH));
            float tabsY = top + openH + Gap;
            Tabs(new Vector2(cx, tabsY), cw);
            var pageAt = new Vector2(cx, tabsY + P(22));
            Page(pageAt, new Vector2(cw, top + bodyH - pageAt.Y));

            // right: play as, the strength ledger, the brief, the remarks, begin
            RightColumn(new Vector2(o.X + w - RightW, top), new Vector2(RightW, bodyH));

            StatusBar(new Vector2(o.X, o.Y + h - StatusH), w);
            ImGui.PopFont();
        }
        ImGui.End();
        PickerUi(centreAt, centreSize);
        SaveDialog();
        T.Pop();
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

    /// <summary>The keys (the status bar lists them): 1–3 the tabs, Tab the side played, V the side shown, C R L G the
    /// opening's overlays, N add a unit, P and E the selected class's design, Enter begin (Shift paused), Ctrl+S save,
    /// Ctrl+Z undo the newest change; WASD pans the opening (ProcessEvent), so no key here is W, A, S or D
    /// (docs/map-ui/input.md). None while text is typed, the save box is up or a unit is being placed.</summary>
    void Keys()
    {
        var io = ImGui.GetIO();
        if (io.WantTextInput || saveOpen || current == null || ImGui.IsPopupOpen("", ImGuiPopupFlags.AnyPopup))
            return;
        bool picker = pickerRow != null;
        if (io.KeyCtrl)
        {
            if (ImGui.IsKeyPressed(ImGuiKey.S) && CanSave)
                OpenSave();
            else if (ImGui.IsKeyPressed(ImGuiKey.Z) && !begin && UndoLast() is { } u)
                pendingEdit = u;
            return;
        }
        if (picker)
        {
            if (ImGui.IsKeyPressed(ImGuiKey.Enter) || ImGui.IsKeyPressed(ImGuiKey.KeypadEnter))
                pickerEnter = true;
            return;
        }
        if (placing != null)
            return;
        for (int i = 0; i < 3; i++)
            if (ImGui.IsKeyPressed(ImGuiKey._1 + i))
                tab = i + 1;
        if (ImGui.IsKeyPressed(ImGuiKey.Tab) && !begin && playRows.Count > 0)
        {
            int at = playRows.FindIndex(r => r.Side == side);
            pendingSide = playRows[(at + 1) % playRows.Count].Side;
        }
        if (ImGui.IsKeyPressed(ImGuiKey.V) && shipTabs.Count > 1)
            shipTab = (shipTab + 1) % shipTabs.Count;
        if (ImGui.IsKeyPressed(ImGuiKey.C))
            showCourses = !showCourses;
        if (ImGui.IsKeyPressed(ImGuiKey.R))
            showRange = !showRange;
        if (ImGui.IsKeyPressed(ImGuiKey.L))
            showLabels = !showLabels;
        if (ImGui.IsKeyPressed(ImGuiKey.G))
            showGrid = !showGrid;
        if (ImGui.IsKeyPressed(ImGuiKey.N) && !begin && OrdersSide >= 0)
            pendingAdd = (OrdersSide, null);
        if (ImGui.IsKeyPressed(ImGuiKey.P) && !begin && Sel is { } d)
            OpenPicker(d);
        if (ImGui.IsKeyPressed(ImGuiKey.E) && !begin && Sel is { } e)
            Copy(InUseCandidate(e));
        if ((ImGui.IsKeyPressed(ImGuiKey.Enter) || ImGui.IsKeyPressed(ImGuiKey.KeypadEnter)) && !begin)
            BeginWorld(paused: io.KeyShift);
    }

    bool CanSave => current != null && !setup.IsEmpty && setup.From.Length > 0 && !begin;

    // ------------------------------------------------------------------ the menu bar

    void MenuBar()
    {
        if (!ImGui.BeginMainMenuBar())
            return;
        if (ImGui.BeginMenu("File"))
        {
            if (ImGui.MenuItem("Save setup…", "Ctrl+S", false, CanSave))
                OpenSave();
            if (ImGui.MenuItem("Open the setups folder"))
                ShowFolder(SetupsFolder);
            ImGui.TextDisabled("A scenario file dropped on the window opens");
            ImGui.Separator();
            if (ImGui.MenuItem("Ship designer", "F3"))
                OpenDesigner?.Invoke();
            if (ImGui.MenuItem("Quit to desktop"))
                Quit?.Invoke();
            ImGui.EndMenu();
        }
        if (ImGui.BeginMenu("Edit"))
        {
            if (ImGui.MenuItem("Undo the newest change", "Ctrl+Z", false, !begin && !setup.IsEmpty) && UndoLast() is { } u)
                pendingEdit = u;
            if (ImGui.MenuItem("Reset to the scenario as written", null, false, !begin && !setup.IsEmpty))
                pendingEdit = setup with { Changes = [] };
            ImGui.EndMenu();
        }
        if (ImGui.BeginMenu("View"))
        {
            if (ImGui.MenuItem("Courses", "C", showCourses))
                showCourses = !showCourses;
            if (ImGui.MenuItem("Range", "R", showRange))
                showRange = !showRange;
            if (ImGui.MenuItem("Labels", "L", showLabels))
                showLabels = !showLabels;
            if (ImGui.MenuItem("Grid", "G", showGrid))
                showGrid = !showGrid;
            ImGui.EndMenu();
        }
        if (ImGui.BeginMenu("Scenario"))
        {
            if (ImGui.MenuItem(side < 0 ? "Watch" : "Begin", "Enter", false, current != null && !begin))
                BeginWorld(paused: false);
            if (ImGui.MenuItem("Begin paused", "Shift+Enter", false, current != null && !begin))
                BeginWorld(paused: true);
            ImGui.Separator();
            foreach (var r in playRows)
                if (ImGui.MenuItem(r.Side < 0 ? "Watch" : "Play as " + r.Name, null, r.Side == side, !begin) && r.Side != side)
                    pendingSide = r.Side;
            ImGui.EndMenu();
        }
        // the screens at the right end, as the designer's tabs
        const string Here = "Scenarios", There = "Designer", Key = "F3";
        var st = ImGui.GetStyle();
        float tw = ImGui.CalcTextSize(Here).X + ImGui.CalcTextSize(There).X + ImGui.CalcTextSize(Key).X + st.ItemSpacing.X * 6 + st.WindowPadding.X;
        ImGui.SetCursorPosX(Math.Max(ImGui.GetCursorPosX(), ImGui.GetWindowWidth() - tw));
        ImGui.TextDisabled(Key);
        Ui.Pieces.ScreenTab(Here, true);
        if (ImGui.MenuItem(There))
            OpenDesigner?.Invoke();
        ImGui.EndMainMenuBar();
    }

    static void ShowFolder(string folder)
    {
        try
        {
            Directory.CreateDirectory(folder);
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(folder) { UseShellExecute = true });
        }
        catch (Exception e)
        {
            Console.Error.WriteLine($"launch: {folder}: {e.Message}");
        }
    }

    // ------------------------------------------------------------------ the resource bar

    // the pick's strings, made when it changes (Made)
    string kind = "", title = "", file = "", brief = "";
    (string Label, string Value)[] cells = [];
    string shipsA = "", shipsB = "", changesCell = "0", readyLine = "";

    void ResourceBar(Vector2 o, float w)
    {
        dl.ResourceBarBg(o, w, ResBarH);
        float x = o.X + P(10);
        dl.Text(UiFonts.SansSmall, new Vector2(x, o.Y + P(1)), T.Muted, kind);
        dl.Text(UiFonts.SansBold, new Vector2(x, o.Y + P(15)), current == null && pick >= 0 ? T.Amber : T.Bright, title);
        dl.Text(UiFonts.SansSmall, new Vector2(x + Width(UiFonts.SansBold, title) + P(8), o.Y + P(17)), T.Muted, file);
        x = o.X + LeftW;
        foreach (var (label, value) in cells)
            dl.ResourceCell(ref x, o.Y, ResBarH, label, value);
        if (current == null)
        {
            dl.ResourceEnd(x, o.Y, ResBarH);
            return;
        }
        // the sides' ships and tonnage, friend blue v enemy red, and the changes
        var at = dl.ResourceCell(ref x, o.Y, ResBarH, "SHIPS", "", drawnW: VersusWidth(shipsA, shipsB));
        Versus(at.X, at.Y, shipsA, shipsB);
        var lv = Ledger();
        if (lv.TonsA.Length > 0)
        {
            at = dl.ResourceCell(ref x, o.Y, ResBarH, "TONNAGE", "", drawnW: VersusWidth(lv.TonsA, lv.TonsB));
            Versus(at.X, at.Y, lv.TonsA, lv.TonsB);
        }
        else
            dl.ResourceCell(ref x, o.Y, ResBarH, "TONNAGE", "—", T.Muted);
        dl.ResourceCell(ref x, o.Y, ResBarH, "CHANGES", changesCell);
        dl.ResourceEnd(x, o.Y, ResBarH);
    }

    static float VersusWidth(string a, string b) => Width(UiFonts.Mono, a) + (b.Length > 0 ? Width(UiFonts.Mono, " v ") + Width(UiFonts.Mono, b) : 0);

    /// <summary>"13 v 14": the friend's figure cream, the enemy's red; returns where it ends.</summary>
    float Versus(float x, float y, string a, string b)
    {
        dl.Text(UiFonts.Mono, new Vector2(x, y), T.Friend, a);
        x += Width(UiFonts.Mono, a);
        if (b.Length == 0)
            return x;
        dl.Text(UiFonts.Mono, new Vector2(x, y), T.Muted, " v ");
        x += Width(UiFonts.Mono, " v ");
        dl.Text(UiFonts.Mono, new Vector2(x, y), T.Hostile, b);
        return x + Width(UiFonts.Mono, b);
    }

    // ------------------------------------------------------------------ the scenarios and your setups

    readonly HashSet<string> shutGroups = [];

    int SetupCount
    {
        get
        {
            int n = 0;
            foreach (var e in entries)
                n += e.Saved ? 1 : 0;
            return n;
        }
    }

    void ScenariosPane(Vector2 a, Vector2 size)
    {
        int shipped = 0;
        foreach (var e in entries)
            shipped += e.Test || e.Opened || e.Saved ? 0 : 1;
        dl.Pane(a, size, "SCENARIOS", shipped == 1 ? "1 shipped" : ShippedNote(shipped));
        var keep = dl;
        if (!BeginBody("##scenarios", a, size, out dl))
        {
            EndBody(0);
            dl = keep;
            return;
        }
        var o = ImGui.GetCursorScreenPos();
        float w = ImGui.GetContentRegionAvail().X;
        float xSides = o.X + P(156), xYear = o.X + P(206), xShips = o.X + w - P(6);
        dl.Head(o, w);
        dl.HeadCell(o, o.X + P(6), "NAME");
        dl.HeadCell(o, xSides, "SIDES");
        dl.HeadCell(o, xYear, "YEAR");
        dl.HeadCell(o, xShips, "SHIPS", right: true);
        float y = o.Y + HeadH;
        string? group = null;
        int n = 0;
        for (int i = 0; i < entries.Count; i++)
        {
            var e = entries[i];
            if (e.Saved)
                continue;   // under Your setups
            if (e.Era != group)
            {
                group = e.Era;
                var ga = new Vector2(o.X, y);
                ImGui.PushID(group);
                var (gc, gh) = HitRow("##grp", ga, new Vector2(w, RowH));
                ImGui.PopID();
                if (gc && !shutGroups.Remove(group))
                    shutGroups.Add(group);
                dl.GroupBg(ga, w, RowH, gh);
                bool shut = shutGroups.Contains(group);
                dl.Twisty(new Vector2(ga.X + P(9), ga.Y + RowH / 2), !shut);
                dl.Text(UiFonts.SansBold, new Vector2(ga.X + P(18), Mid(UiFonts.SansBold, ga.Y, RowH)), T.Bright, group);
                dl.TextRight(UiFonts.Mono, xShips, Mid(UiFonts.Mono, ga.Y, RowH), T.Muted, GroupCount(group));
                y += RowH;
                n = 0;
            }
            if (shutGroups.Contains(group))
                continue;
            var ra = new Vector2(o.X, y);
            ImGui.PushID(i);
            var (click, hover) = HitRow("##sc", ra, new Vector2(w, RowH));
            ImGui.PopID();
            if (click && i != pick)
                Pick(i);
            bool on = i == pick;
            dl.RowBg(ra, w, RowH, n++, on, hover);
            float ty = Mid(UiFonts.Sans, ra.Y, RowH);
            dl.PushClipRect(ra, new Vector2(xSides - P(6), ra.Y + RowH), true);
            dl.Text(UiFonts.Sans, new Vector2(ra.X + P(24), ty), e.Error != null ? T.Amber : on ? T.Brass : T.Text, e.Name);
            dl.PopClipRect();
            for (int k = 0; k < Math.Min(e.Ensigns.Length, 2); k++)
                Ensign(e.Ensigns[k], new Vector2(xSides + k * P(18), ra.Y + P(6)), new Vector2(P(14), P(9)));
            dl.Text(UiFonts.Mono, new Vector2(xYear, Mid(UiFonts.Mono, ra.Y, RowH)), T.Muted, e.Year);
            dl.TextRight(UiFonts.Mono, xShips, Mid(UiFonts.Mono, ra.Y, RowH), T.Bright, e.Ships);
            if (hover && e.Error != null)
                ImGui.SetTooltip(e.Error);
            y += RowH;
        }
        EndBody(y - o.Y + P(4));
        dl = keep;
    }

    readonly Dictionary<int, string> shippedNotes = [];
    string ShippedNote(int n) => shippedNotes.TryGetValue(n, out var s) ? s : shippedNotes[n] = $"{n} shipped";

    readonly Dictionary<string, string> groupCounts = [];

    /// <summary>A group's count, made once (the entries change only when a file is added or a setup saved: <see cref="Sort"/>'s callers clear it).</summary>
    string GroupCount(string g)
    {
        if (!groupCounts.TryGetValue(g, out var s))
            groupCounts[g] = s = entries.Count(e => !e.Saved && e.Era == g).ToString(CultureInfo.InvariantCulture);
        return s;
    }

    void SetupsPane(Vector2 a, Vector2 size)
    {
        dl.Pane(a, size, "YOUR SETUPS", "a scenario file each");
        var keep = dl;
        if (!BeginBody("##setups", a, size, out dl))
        {
            EndBody(0);
            dl = keep;
            return;
        }
        var o = ImGui.GetCursorScreenPos();
        float w = ImGui.GetContentRegionAvail().X, y = o.Y;
        int n = 0;
        for (int i = 0; i < entries.Count; i++)
        {
            var e = entries[i];
            if (!e.Saved)
                continue;
            var ra = new Vector2(o.X, y);
            ImGui.PushID(i);
            var (click, hover) = HitRow("##setup", ra, new Vector2(w, RowH));
            ImGui.PopID();
            if (click && i != pick)
                Pick(i);
            bool on = i == pick;
            dl.RowBg(ra, w, RowH, n++, on, hover);
            float ty = Mid(UiFonts.SansSmall, ra.Y, RowH);
            dl.PushClipRect(ra, new Vector2(ra.X + P(84), ra.Y + RowH), true);
            dl.Text(UiFonts.SansSmall, new Vector2(ra.X + P(8), ty), T.Muted, e.Of);
            dl.PopClipRect();
            float ww = Width(UiFonts.SansSmall, e.When);
            dl.PushClipRect(new Vector2(ra.X + P(88), ra.Y), new Vector2(ra.X + w - ww - P(12), ra.Y + RowH), true);
            dl.Text(UiFonts.Sans, new Vector2(ra.X + P(88), Mid(UiFonts.Sans, ra.Y, RowH)), e.Error != null ? T.Amber : on ? T.Brass : T.Bright, e.Name);
            dl.PopClipRect();
            dl.TextRight(UiFonts.SansSmall, ra.X + w - P(6), ty, T.Muted, e.When);
            if (hover && e.Error != null)
                ImGui.SetTooltip(e.Error);
            y += RowH;
        }
        if (n == 0)
        {
            dl.Text(UiFonts.SansSmall, new Vector2(o.X + P(8), Mid(UiFonts.SansSmall, y, RowH)), T.Muted, "None yet: change a scenario, then Save setup…");
            y += RowH;
        }
        EndBody(y - o.Y + P(4));
        dl = keep;
    }

    // ------------------------------------------------------------------ the pick: its strings

    /// <summary>A Play as row: a side, or Watch (-1).</summary>
    sealed record PlayRow(int Side, string Name, string Ensign, string Commander, string Ships, string Character);

    readonly List<PlayRow> playRows = [];
    readonly List<(bool Amber, string Text)> remarks = [];
    string remarkCount = "";

    /// <summary>The pick's resource bar, Play as, brief and remarks, its body's lines and the opening, made once per pick
    /// and change.</summary>
    void Made(Scenario s)
    {
        var start = s.Start.ToUtc();
        var (lat, lon) = Centre(s);
        var local = start.AddHours(lon / 15);   // local mean time, by the sun
        bool isSetup = s.Setup is { IsEmpty: false } || entries[pick].Saved;
        string sides = string.Join(" v ", s.Sides.Where((_, i) => s.Units.Any(u => u.Side == i)).Select(n => n.ToUpperInvariant()));
        kind = $"{(isSetup ? $"SETUP OF {s.Written?.Name.ToUpperInvariant() ?? ""} · " : "")}{start.Year} · {sides}";
        title = isSetup && s.Setup is { Name.Length: > 0 } st ? st.Name : s.Written?.Name ?? s.Name;
        file = Path.GetFileName(entries[pick].Path);
        var c = new List<(string, string)>
        {
            ("DATE", local.ToString("ddd d MMM yyyy", CultureInfo.InvariantCulture)),
            ("LOCAL", local.ToString("HH:mm", CultureInfo.InvariantCulture)),
            ("POSITION", $"{Dms(lat, 'N', 'S')} {Dms(lon, 'E', 'W')}"),
        };
        var wx = WeatherOf(s);
        c.Add(("VISIBILITY", $"{wx.VisibilityM / 1000:0} km {SightWord(wx.VisibilityM)}"));
        c.Add(("WIND", $"{Compass(wx.WindFromDeg)} {wx.WindMs / 0.514444:0} kn"));
        if (SunTurn(s.Start, Earth.Position(lat, lon)) is { } turn)
            c.Add((turn.Word.ToUpperInvariant(), turn.At.ToUtc().AddHours(lon / 15).ToString("HH:mm", CultureInfo.InvariantCulture)));
        c.Add(("ENDS", EndOf(s).ToUtc().AddHours(lon / 15).ToString("d MMM HH:mm", CultureInfo.InvariantCulture)));
        int me = side >= 0 ? side : 0;
        c.Add(("DOCTRINE", s.Doctrine.FirstOrDefault(d => d.Side == me).Preset ?? Sim.Ai.DoctrinePresets.DefaultName));
        cells = [.. c];
        brief = s.Brief;
        changesCell = setup.Changes.Count.ToString(CultureInfo.InvariantCulture);
        changesLine = $"{setup.Changes.Count} change{(setup.Changes.Count == 1 ? "" : "s")} from the scenario (Ctrl+Z undoes the newest)";

        playRows.Clear();
        int them = -1;
        for (int i = 0; i < s.Sides.Length; i++)
        {
            int ships = s.Units.Where(u => u.Side == i).Sum(u => u.ShipCount);
            if (ships == 0)
                continue;
            var about = s.AboutOf(i);
            playRows.Add(new PlayRow(i, s.Sides[i], about?.Ensign ?? "", about?.Commander ?? "", $"{ships} ship{(ships == 1 ? "" : "s")}", about?.Character ?? ""));
            if (i != me && them < 0)
                them = i;
        }
        playRows.Add(new PlayRow(-1, "Watch", "", "both sides the AI's", "all shown", "Both sides the AI's; you see everything. For checking a setup before playing it."));
        int Ships(int sd) => s.Units.Where(u => u.Side == sd).Sum(u => u.ShipCount);
        shipsA = Ships(me).ToString(CultureInfo.InvariantCulture);
        shipsB = them >= 0 ? Ships(them).ToString(CultureInfo.InvariantCulture) : "";
        readyLine = (side < 0 ? "both sides the AI's, all shown" : string.Join(" · ", playRows.Where(r => r.Side >= 0).Select(r => r.Side == side ? r.Name + ", yours" : r.Name + ", the AI's")))
                    + (setup.IsEmpty ? " · as written" : $" · {setup.Changes.Count} change{(setup.Changes.Count == 1 ? "" : "s")}");

        remarks.Clear();
        void Walk(ScenarioUnit u)
        {
            if (!u.Removed && u.OwnTemper && u.TemperWhy.Length > 0 && u.Temper is double t)
                remarks.Add((false, $"{(u.Admiral.Length > 0 ? Sim.Missions.SetTemper.AdmiralName(u.Admiral) : u.Name)}'s temper is {DoctrineRules.Signed(t)}: {u.TemperWhy}."));
            foreach (var x in u.Units)
                Walk(x);
        }
        foreach (var u in s.Units)
            Walk(u);
        remarkKey = (null, -1);
        MadeBody(s);
        ledger = null;
        (opening, openView) = (null, null);   // drawn again for this pick and side, framed to it
    }

    /// <summary>A pick whose changes no longer apply (or that couldn't be read): its name, and why in the remarks.</summary>
    void MadeError()
    {
        var e = entries[pick];
        kind = "COULDN'T BE SET UP";
        title = e.Name;
        file = Path.GetFileName(e.Path);
        cells = [];
        brief = "";
        playRows.Clear();
        remarks.Clear();
        remarks.Add((true, e.Error ?? setupError ?? ""));
        remarkKey = (null, -1);
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

    static string SightWord(double m) => m < 1000 ? "fog" : m < 5000 ? "mist" : m < 18000 ? "haze" : "clear";

    static string Compass(double deg) => SetWeather.Compass(deg);

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

    // ------------------------------------------------------------------ the tabs and their pages

    /// <summary>The numbered tabs (as the designer's 0–7) and, at their right, the side the first two show.</summary>
    void Tabs(Vector2 a, float w)
    {
        int t = dl.NumberedTabs(new Vector2(a.X + P(4), a.Y), w - P(8), TabNames, Digits, tab - 1);
        if (t >= 0)
            tab = t + 1;
        // the side shown on the order of battle and the standing orders (V)
        if (tab < 3 && shipTabs.Count > 1)
        {
            float sx = a.X + w - P(6) - SegmentedWidth(sideLabels);
            int sd = dl.Segmented("##side", new Vector2(sx, a.Y + P(3)), sideLabels, shipTab, out _);
            if (sd >= 0)
                shipTab = sd;
            dl.RightNote(sx - P(8), a.Y + P(1), P(20), "side  V");
        }
    }

    void Page(Vector2 a, Vector2 size)
    {
        var b = a + size;
        dl.AddRectFilled(a, b, U(T.Panel));
        dl.AddRect(a, b, U(T.Edge));
        dl.HLine(a.X + 1, b.X - 1, a.Y, T.TitleTop);
        if (current == null)
            return;
        var inner = new Vector2(a.X + 1, a.Y + 1);
        var isize = size - new Vector2(2, 2);
        if (tab == 1)
            OrderOfBattle(inner, isize);
        else if (tab == 2)
            OrdersPage(inner, isize);
        else
            ConditionsPage(inner, isize);
    }

    // ------------------------------------------------------------------ the status bar

    string status = "";
    const string StatusKeys = "1–3 tabs · Tab side played · V side shown · WASD or middle drag pan · wheel zoom · C R L G overlays · N add a unit · P design · Enter begin · Shift+Enter paused · Ctrl+S save · Ctrl+Z undo · F3 designer";

    void StatusBar(Vector2 a, float w)
    {
        dl.StatusBarBg(a, w, StatusH);
        var (compiled, _, of) = Counts();
        float ty = Mid(UiFonts.SansSmall, a.Y, StatusH);
        var dot = a + new Vector2(P(10), StatusH / 2);
        bool failed = world.LoadError != null || current == null;
        if (of > 0 && compiled < of && !failed)
            dl.Busy(dot, P(4));
        else
            dl.AddCircleFilled(dot, P(3.5f), U(failed ? T.Amber : T.Ok));
        dl.Text(UiFonts.SansSmall, new Vector2(a.X + P(20), ty), T.Muted, status);
        float x = a.X + P(38) + Width(UiFonts.SansSmall, status);
        dl.Text(UiFonts.SansSmall, new Vector2(x, ty), T.Muted, setup.IsEmpty ? "history: as written" : "history:");
        if (!setup.IsEmpty)
            dl.Text(UiFonts.SansSmall, new Vector2(x + Width(UiFonts.SansSmall, "history: "), ty), T.Brass, changesLine);
        dl.TextRight(UiFonts.SansSmall, a.X + w - P(10), ty, T.Muted, StatusKeys);
    }

    string changesLine = "";
}
