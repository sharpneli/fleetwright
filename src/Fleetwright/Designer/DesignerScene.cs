using System.Globalization;
using System.Numerics;
using Fleetwright.Gpu;
using Fleetwright.HitView;
using Fleetwright.Shipgen;
using ImGuiNET;
using SDL;
using static SDL.SDL3;
using T = Fleetwright.Designer.DesignerTheme;

namespace Fleetwright.Designer;

/// <summary>
/// The ship designer: a scene of its own that takes a design in and hands one back. The game opens it from its UI
/// with a design (or <see cref="DesignDoc.Empty"/>) and a callback; Accept calls back with the edited design, Cancel
/// with null. Without a callback (the dev launch) it only saves to the player's folder.
///
/// Layout after docs/designer-ui/follow.html: the sections on the left, the ship pinned on top of the centre (the top
/// view and the side profile, each from its renderer into an offscreen target), the open section under it, the Legend
/// on the right and the history along the bottom. Every edit goes into the <see cref="DesignDoc"/>, the new design
/// to the <see cref="DesignWorker"/>, and the result lands a few frames later: nothing an edit does waits for a build.
/// While a build runs the last result stays up, dimmed.
/// </summary>
public sealed unsafe class DesignerScene : IScene
{
    const double SpriteScale = 8;   // px per metre at mip 0
    const int MipLevels = 4;
    static float S => UiFonts.Scale;
    static float P(float px) => px * S;
    static Vector2 V(float x, float y) => new(x * S, y * S);
    static float RailW => P(230);
    static float LegendW => P(320);
    static float TopBarH => P(54);
    static float HistoryH => P(40);

    static readonly string[] Sections =
        ["Overview", "Main battery", "Secondary · AA · TT", "Protection", "Speed & machinery", "Hull & upperworks", "Fire control", "Crew"];

    readonly SDL_GPUDevice* device;
    readonly Action<Design?>? onClose;
    readonly DesignWorker worker;
    readonly ShipSpriteRenderer sprite;
    readonly HitboxRenderer profile;
    RenderTarget? topTarget, profileTarget;
    Vector2 topSize = new(800, 220), profileSize = new(800, 120);   // laid out last frame, used for the targets
    ShipSpriteView topView = ShipSpriteView.Default;
    HitboxCamera profileCam = new() { Bearing = 90, Elevation = 0 };
    HitboxViewState profileState = HitboxViewState.All;

    DesignDoc doc;
    string submitted = "";   // the JSON of the design last sent to the worker
    DesignResult? shown;   // the newest result with a ship: what's on screen
    DesignResult? latest;   // the newest result, ship or not (its errors are remarks)
    Ship? baselineShip;   // the design as opened, built (for "since opened")
    int section;
    UnitSystem units;
    Snapshot? snap;
    bool stale = true;   // the snapshot is rebuilt at the next BuildUi (never mid-frame: the panels drawn after an edit still read it)

    // a value being typed into
    string? editing;
    string editText = "";
    bool editFocus;

    readonly HashSet<(int Section, string Header)> openDetails = [];   // the Details headers left open

    // dialogs
    string saveName = "", pennant = "";
    string[] shippedDesigns = [], userDesigns = [];

    // linked to a session (the dev app): edits go to it, and a design changed elsewhere (the viewer's pick) comes back
    readonly DesignSession? session;
    readonly bool ownsWorker;
    int seenDesign;   // the session's DesignVersion last taken or caused here
    LookInput? ownLook;   // the look override when there's no session (with one, the session's is shared)

    /// <summary>A designer of its own: <paramref name="start"/> in, the edited design out through
    /// <paramref name="onClose"/> (the game's use).</summary>
    /// <param name="steps">Knob steps to apply at once, as the + and - buttons do ("speed:-2", "belt:+3"): a dev hook for
    /// screenshots of edited states.</param>
    public DesignerScene(SDL_GPUDevice* device, Design start, string? sourcePath = null, Action<Design?>? onClose = null,
        UnitSystem units = UnitSystem.Metric, int section = 0, IEnumerable<string>? steps = null)
        : this(device, null, new DesignWorker(SpriteScale, MipLevels, limits: true, (nint)device), new DesignDoc(start, sourcePath),
            onClose, units, section, steps)
    {
    }

    /// <summary>A designer on the session's design (the dev app): every edit shows in the other scenes at once, and a
    /// design picked there opens here.</summary>
    public DesignerScene(SDL_GPUDevice* device, DesignSession session, UnitSystem units = UnitSystem.Metric, int section = 0,
        IEnumerable<string>? steps = null)
        : this(device, session, session.Worker, new DesignDoc(session.Design ?? DesignDoc.Empty(), session.DesignPath), null, units,
            section, steps)
    {
    }

    DesignerScene(SDL_GPUDevice* device, DesignSession? session, DesignWorker worker, DesignDoc doc, Action<Design?>? onClose,
        UnitSystem units, int section, IEnumerable<string>? steps)
    {
        this.device = device;
        this.session = session;
        this.worker = worker;
        ownsWorker = session == null;
        this.doc = doc;
        this.onClose = onClose;
        this.units = units;
        this.section = Math.Clamp(section, 0, Sections.Length - 1);
        if (session != null)   // the session already builds this design
        {
            submitted = doc.History[0].Json;
            seenDesign = session.DesignVersion;
        }
        sprite = new ShipSpriteRenderer(device);
        profile = new HitboxRenderer(device);
        ulong mask = 0;
        for (int k = 0; k < HitKinds.All.Length; k++)
            if (HitKinds.All[k].Name is "belt" or "strake" or "armour_deck" or "armoured_bulkhead" or "barbette" or "conning_tower"
                or "main" or "secondary" or "waterline" or "hull_lines")
                mask |= 1UL << k;
        profileState.KindMask = mask;
        Submit();
        worker.WaitIdle(TimeSpan.FromMinutes(1));   // open with the ship on screen
        if (steps != null)
        {
            Take();
            foreach (var st in steps)
            {
                var parts = st.Split(':');
                var knob = parts.Length == 2 ? Knobs.All(doc.Current).FirstOrDefault(k => k.Id == parts[0]) : null;
                switch (knob)
                {
                    case NumberKnob k when int.TryParse(parts[1], out int n):
                        Step(Row(k, doc.Current, shown?.Ship), n);
                        break;
                    case ChoiceKnob c when c.Values.Contains(parts[1]):
                        Edit(c.Set(doc.Current, parts[1]), $"{c.Label}: {parts[1]}");
                        break;
                    case ToggleKnob t when parts[1] is "on" or "off":
                        Edit(t.Set(doc.Current, parts[1] == "on"), $"{t.Label} {parts[1]}");
                        break;
                    default:
                        Console.Error.WriteLine($"designer: no step '{st}' (knob:steps, knob:value or knob:on/off, e.g. speed:-2)");
                        continue;
                }
                doc.Seal();
            }
            Submit();
            worker.WaitIdle(TimeSpan.FromMinutes(1));
        }
    }

    /// <summary>The design as edited so far.</summary>
    public Design Current => doc.Current;

    /// <summary>Starts over on another design (the old history goes); with a session, the other scenes switch too.</summary>
    public void Load(DesignDoc next)
    {
        Switch(next);
        if (session != null)
        {
            session.Load(next.Current, next.SourcePath);
            submitted = next.History[0].Json;
            seenDesign = session.DesignVersion;
        }
    }

    void Switch(DesignDoc next)
    {
        doc = next;
        baselineShip = null;
        editing = null;
        stale = true;
    }

    /// <summary>A design changed in another scene (the viewer's pick, a reload) opens here.</summary>
    void Sync()
    {
        if (session == null || session.DesignVersion == seenDesign)
            return;
        seenDesign = session.DesignVersion;
        if (session.Design is { } d && d.ToJson() != doc.History[doc.Index].Json)
        {
            Switch(new DesignDoc(d, session.DesignPath));
            submitted = doc.History[0].Json;
        }
    }

    /// <summary>Sends the current design to be built if it changed: through the session (every scene shows it), or
    /// to this designer's own worker.</summary>
    void Submit()
    {
        var json = doc.History[doc.Index].Json;
        if (ReferenceEquals(json, submitted))
            return;
        submitted = json;
        if (session != null)
        {
            session.Edit(doc.Current);
            seenDesign = session.DesignVersion;
        }
        else
            worker.Submit(doc.Current, ownLook);
    }

    /// <summary>The look the sprite is drawn in, other than the design's own (null: its own).</summary>
    LookInput? Look => session != null ? session.Look : ownLook;

    void SetLook(LookInput? look)
    {
        if (session != null)
            session.SetLook(look);
        else
        {
            ownLook = look;
            worker.Submit(doc.Current, look);
        }
        stale = true;
    }

    // ------------------------------------------------------------------ frame

    public void Draw(SDL_GPUCommandBuffer* cmd, RenderTarget target, float dt)
    {
        Sync();
        Submit();
        Take();
        if (topTarget == null || profileTarget == null)   // sized in BuildUi, which runs first; this is the first frame
        {
            Offscreen(ref topTarget, topSize);
            Offscreen(ref profileTarget, profileSize);
        }
        if (sprite.Meta != null)
        {
            topView.Zoom = sprite.FitZoom(topSize.X, topSize.Y, 0.96f, 0.92f);
            topView.Centre = Vector2.Zero;
        }
        sprite.Render(cmd, topTarget!, topView, ReadOnlySpan<double>.Empty, Clear(T.Inset));
        if (profile.Mesh is { Prisms.Length: > 0 } m)
            profileCam.Fit(m.Vertices, profileSize.X / Math.Max(1, profileSize.Y), profileState.KindMask);
        profile.Render(cmd, profileTarget!, profileCam, profileState);
        var pass = target.BeginPass(cmd, Clear(T.Ground), depth: false);
        SDL_EndGPURenderPass(pass);
    }

    /// <summary>Takes the worker's newest result: a ship goes on screen (sprite and profile uploaded), and a failure
    /// stays as remarks over the last ship.</summary>
    void Take()
    {
        if (worker.Latest is not { } r || r.Id == (latest?.Id ?? 0))
            return;
        latest = r;
        if (r.Ship != null)
        {
            shown = r;
            sprite.Upload(r);
            if (r.Mesh is { } mesh)
                profile.Upload(mesh);
            if (baselineShip == null && r.Design.ToJson() == doc.History[0].Json)
                baselineShip = r.Ship;
        }
        stale = true;
    }

    static SDL_FColor Clear(Vector4 c) => new() { r = c.X, g = c.Y, b = c.Z, a = 1 };

    /// <summary>Makes or resizes an offscreen target. Called from BuildUi before the ImGui.Image that shows it: the
    /// engine builds the UI before it calls Draw, so a resize in Draw would free a texture the UI already holds.</summary>
    void Offscreen(ref RenderTarget? t, Vector2 size)
    {
        uint w = (uint)Math.Max(16, size.X), h = (uint)Math.Max(16, size.Y);
        if (t == null)
            t = new RenderTarget(device, w, h, SDL_GPUTextureFormat.SDL_GPU_TEXTUREFORMAT_R8G8B8A8_UNORM, SDL_GPUSampleCount.SDL_GPU_SAMPLECOUNT_4);
        else if (t.Width != w || t.Height != h)
            t.Resize(w, h);
    }

    public bool ProcessEvent(SDL_Event* e, uint width, uint height) => false;   // the UI covers the window

    // ------------------------------------------------------------------ the snapshot: every string the UI shows

    /// <summary>What the UI shows, formatted once per change (design, result, units or section), so a frame that
    /// changes nothing allocates nothing.</summary>
    sealed class Snapshot
    {
        public required string Title, TypeLabel, File, Status, Std, StdDelta, Settling, Style;
        public required string[] Summaries;
        public required (string Label, string Value)[] Particulars;
        public required (string Group, float Frac, string Text)[] Weights;
        public required (bool Error, string Text)[] Remarks;
        public required (string Label, string Value)[] Consequences;
        public required List<Item> Panel;
        public required string[] HistoryLabels;
    }

    /// <summary>A line of the section panel.</summary>
    abstract record Item;
    /// <summary>A section heading; a <see cref="Details"/> one folds away the rows under it (the second layer).</summary>
    sealed record Header(string Text, string? Button = null, Action? OnButton = null, bool Details = false) : Item
    {
        public string Upper { get; } = Text.ToUpperInvariant();
    }
    sealed record Note(string Text) : Item;
    sealed record Act(string Label, Action OnClick) : Item;
    sealed record NumRow(NumberKnob Knob, string Label, string Value, string Hint, string Delta, bool Applies, bool IsAuto, double? Num) : Item;
    sealed record ChoiceRow(ChoiceKnob Knob, string[] Labels, int Index, bool Applies) : Item;
    /// <summary>A template picker: <see cref="Count"/> catalogue entries, then maybe the design's own block ("custom").</summary>
    sealed record PickRow(string Label, string[] Names, int Index, int Count, Action<int> Apply) : Item;
    sealed record ToggleRow(ToggleKnob Knob, bool Value, bool Applies) : Item;

    string Fmt(double metric, Quantity q, double? stepMetric = null, bool unit = true)
    {
        double step = stepMetric is { } s ? s / Units.Factor(q, units) : q is Quantity.Armour or Quantity.Calibre && units == UnitSystem.Imperial ? 0.1 : 1;
        return Units.Format(metric, q, units, step, unit);
    }

    string Fmt(NumberKnob k, double v, bool unit = true) => Units.Format(v, k.Quantity, units, k.Step(units) / Units.Factor(k.Quantity, units), unit);

    static string N0(double v) => v.ToString("N0", CultureInfo.InvariantCulture);

    static string Signed(double v, string fmt) => (v > 0 ? "+" : v < 0 ? "−" : "±") + Math.Abs(v).ToString(fmt, CultureInfo.InvariantCulture);

    Snapshot Build()
    {
        var d = doc.Current;
        var ship = shown?.Ship;
        var res = ship?.Report.Results;
        var bres = baselineShip?.Report.Results;

        string std = res != null ? N0(res.StandardDisplacementT) + " t" : "–";
        string stdDelta = res != null && bres != null
            ? $"since opened: {Signed(res.StandardDisplacementT - bres.StandardDisplacementT, "N0")} t · {Signed(res.LengthM - bres.LengthM, "F1")} m"
            : "";

        var mains = d.Main ?? [];
        var secs = d.Secondary ?? [];
        string Guns(IEnumerable<BatteryInput> bs, bool main) => string.Join(" · ", bs.Where(b => (main ? b.Turrets : b.MountCount) > 0).Select(b =>
            $"{(main ? b.Turrets : b.MountCount) * (b.Barrels ?? 1)} × {Fmt(b.CalibreMm ?? 0, Quantity.Calibre)}"));
        string mainGuns = Guns(mains, true), secGuns = Guns(secs, false);
        double belt = d.Armour?.BeltMm ?? 0, deck = Knobs.ArmourDeck.Get(d) ?? 0;

        string[] summaries =
        [
            "trim sheet",
            mainGuns.Length > 0 ? mainGuns : "none",
            secGuns.Length > 0 ? secGuns : (d.Aa?.Heavy ?? 0) + (d.Aa?.Light ?? 0) > 0 ? "AA only" : "none",
            belt > 0 || deck > 0 ? $"belt {Fmt(belt, Quantity.Armour, unit: false)} · deck {Fmt(deck, Quantity.Armour)}" : "none",
            $"{d.SpeedKn:0.#} kn · {N0(d.RangeNm ?? 6000)} nm",
            res != null ? $"Cb {res.BlockCoefficient:0.00} · tower {ship!.Report.Bridge?.TowerLevels}" : "",
            (ship?.Report.FireControl.Count ?? 0) switch { 0 => "no directors", 1 => "1 director", var n => $"{n} directors" },
            res != null ? $"{N0(res.Crew)} men" : "",
        ];

        var part = new List<(string, string)>();
        if (res != null)
        {
            part.Add(("Dimensions", $"{Fmt(res.LengthM, Quantity.Length, unit: false)} × {Fmt(res.BeamM, Quantity.Length, unit: false)} × " +
                                    $"{Fmt(res.DraughtM, Quantity.Length, 0.1)}"));
            part.Add(("Displacement", $"{N0(res.StandardDisplacementT)} t std · {N0(res.FullDisplacementT)} t full"));
            part.Add(("Machinery", $"{N0(res.PowerShp)} shp · {d.SpeedKn:0.#} kn"));
            part.Add(("Range", $"{N0(d.RangeNm ?? 6000)} nm"));
            part.Add(("Armament", mainGuns.Length + secGuns.Length > 0 ? string.Join(" · ", new[] { mainGuns, secGuns }.Where(s => s.Length > 0)) : "none"));
            part.Add(("Protection", belt > 0 || deck > 0 ? $"belt {Fmt(belt, Quantity.Armour, unit: false)} · deck {Fmt(deck, Quantity.Armour)}" : "none"));
            part.Add(("Complement", N0(res.Crew)));
            part.Add(("Stability", $"GM {res.GmFullM:0.00} m · roll {res.RollPeriodS:0.0} s"));
        }

        var weights = new List<(string, float, string)>();
        if (ship != null)
        {
            double total = ship.Report.WeightGroupsT.Values.Sum();
            foreach (var (g, t) in ship.Report.WeightGroupsT)
                if (t > 0)
                    weights.Add((g, (float)(t / Math.Max(1, total)), $"{g.Replace('_', ' ')} {N0(t)} t"));
        }

        var remarks = new List<(bool, string)>();
        if (latest is { Ship: null } bad)
            foreach (var e in bad.Errors.Where(e => e != "invalid design:"))
                remarks.Add((true, e));
        if (ship != null)
            foreach (var w in ship.Report.Warnings)
                remarks.Add((false, w));

        var cons = new List<(string, string)>();
        if (res != null)
        {
            cons.Add(("Full load", N0(res.FullDisplacementT) + " t"));
            cons.Add(("Length", Fmt(res.LengthM, Quantity.Length, 0.1)));
            cons.Add(("Beam", Fmt(res.BeamM, Quantity.Length, 0.1)));
            cons.Add(("GM", $"{res.GmFullM:0.00} m"));
            cons.Add(("Power", N0(res.PowerShp) + " shp"));
            cons.Add(("Complement", N0(res.Crew)));
        }

        string file = doc.SourcePath != null ? Path.GetFileName(doc.SourcePath) + (doc.Dirty ? " *" : "") : doc.Dirty ? "unsaved" : "";
        string status = latest != null ? latest.Ship != null ? $"built in {latest.BuildS * 1000 + latest.MeshS * 1000 + latest.DrawS * 1000 + latest.BakeS * 1000:F0} ms"
            : "doesn't build: see the remarks" : "";

        return new Snapshot
        {
            Title = d.Name ?? d.Id ?? "Design",
            TypeLabel = d.Type is { Length: > 0 } ty ? ty.ToUpperInvariant() : "NO TYPE LABEL",
            Style = $"Style: {Look?.Navy ?? d.Look?.Navy ?? "generic"} · {Look?.Era ?? d.Look?.Era ?? "wwii"}" + (Look != null ? " (trying)" : ""),
            File = file,
            Status = status,
            Settling = "settling…",
            Std = std,
            StdDelta = stdDelta,
            Summaries = summaries,
            Particulars = part.ToArray(),
            Weights = weights.ToArray(),
            Remarks = remarks.ToArray(),
            Consequences = cons.ToArray(),
            Panel = PanelItems(d, ship),
            HistoryLabels = doc.History.Select(h => h.Label).ToArray(),
        };
    }

    NumRow Row(NumberKnob k, Design d, Ship? ship, string? label = null)
    {
        double? v = k.Value(d, ship);
        bool isAuto = k.Get(d) == null;
        var (lo, hi) = k.Range(d);
        string hint = isAuto && k.CanAuto ? "Auto" : "";
        string delta = "";
        if (k.Value(doc.Baseline, baselineShip) is { } b0 && v is { } v1 && Math.Abs(v1 - b0) > 1e-9 && k.Applies?.Invoke(doc.Baseline) != false)
            delta = (v1 > b0 ? "+" : "−") + Fmt(k, Math.Abs(v1 - b0));
        return new NumRow(k, label ?? k.Label, v is { } x ? Fmt(k, x) : "–", hint, delta, k.AppliesTo(d), isAuto, v);
    }

    List<Item> PanelItems(Design d, Ship? ship)
    {
        var items = new List<Item>();
        void Num(NumberKnob k, string? label = null) => items.Add(Row(k, d, ship, label));
        void Choice(ChoiceKnob k)
        {
            string? v = k.Get(d) ?? k.Default;
            int i = Array.IndexOf(k.Values, v);
            var labels = i < 0 ? [.. k.Labels, v ?? "custom"] : k.Labels;
            items.Add(new ChoiceRow(k, labels, i < 0 ? labels.Length - 1 : i, k.AppliesTo(d)));
        }
        void Toggle(ToggleKnob k) => items.Add(new ToggleRow(k, k.Value(d), k.AppliesTo(d)));
        var style = Styles.Get(d);

        // a catalogue as a picker: the entry the design's block matches, else the block itself as "custom" (or what
        // the engine chose, when the design leaves it out)
        void Pick<TV>(string label, IReadOnlyList<Template<TV>> list, Func<Template<TV>, string> name, Func<Template<TV>, bool> matches,
            string? own, Func<Design, TV, Design> apply)
        {
            var names = list.Select(name).ToList();
            int i = -1;
            for (int n = 0; n < list.Count && i < 0; n++)
                if (matches(list[n]))
                    i = n;
            if (i < 0)
            {
                names.Add(own ?? "custom");
                i = names.Count - 1;
            }
            items.Add(new PickRow(label, names.ToArray(), i, list.Count, k => Edit(apply(doc.Current, list[k].Value), $"{label}: {names[k]}")));
        }

        switch (section)
        {
            case 0:
                items.Add(new Header("Trim sheet · main parameters"));
                foreach (var k in Knobs.TrimSheet)
                    Num(k, k.Id switch
                    {
                        "main0_armour" => "Main turret faces",
                        "main0_rounds" => "Main rounds per gun",
                        "sec0_per_side" => "Secondaries per side",
                        _ => null,
                    });
                break;
            case 1:
                var mains = d.Main ?? [];
                for (int i = 0; i < mains.Count; i++)
                {
                    int j = i;
                    items.Add(new Header($"Main battery {(char)('A' + i)}", "Remove", () => Edit(RemoveAt(doc.Current, j, main: true), $"Main battery {(char)('A' + j)} removed")));
                    Num(Knobs.MainCalibre(i));
                    Num(Knobs.MainLength(i));
                    Num(Knobs.MainBarrels(i));
                    Num(Knobs.MainGroup(i, "fore"));
                    Num(Knobs.MainGroup(i, "aft"));
                    if (style.HasMidshipsTurrets)
                        Num(Knobs.MainGroup(i, "mid"));
                    if (style.HasWingTurrets)
                    {
                        Num(Knobs.MainGroup(i, "wing"));
                        Toggle(Knobs.MainEchelon(i));
                    }
                    Num(Knobs.MainStepped(i, "fore"));
                    Num(Knobs.MainStepped(i, "aft"));
                    Num(Knobs.MainArmour(i));
                    Num(Knobs.MainRounds(i));
                    items.Add(new Header($"Battery {(char)('A' + i)}, details", Details: true));
                    Toggle(Knobs.MainCrossDeck(i));
                    if (style.HasRaisedMounts)
                        Choice(Knobs.MainStandsOn(i));
                    Choice(Knobs.MainMaterial(i));
                }
                if (mains.Count == 0 || style.TakesMainList)
                    items.Add(new Act(mains.Count == 0 ? "Add a main battery" : "Add another main battery", () => Edit(AddMain(doc.Current), "Main battery added")));
                if (mains.Count == 0)
                    items.Add(new Note("No main battery: an unarmed hull."));
                break;
            case 2:
                var secs = d.Secondary ?? [];
                for (int i = 0; i < secs.Count; i++)
                {
                    int j = i;
                    items.Add(new Header($"Secondary battery {i + 1}", "Remove", () => Edit(RemoveAt(doc.Current, j, main: false), $"Secondary battery {j + 1} removed")));
                    Num(Knobs.SecCalibre(i));
                    Num(Knobs.SecLength(i));
                    Num(Knobs.SecBarrels(i));
                    if (Knobs.SecPerSide(i).AppliesTo(d))
                        Num(Knobs.SecPerSide(i));
                    else
                        Num(Knobs.SecCount(i));
                    if (d.StyleName == "warship")
                        Choice(Knobs.SecMount(i));
                    Num(Knobs.SecArmour(i));
                    items.Add(new Header($"Battery {i + 1}, details", Details: true));
                    Num(Knobs.SecRounds(i));
                    if (style.HasCasemates)
                        Choice(Knobs.SecTier(i));
                    if (style.HasRaisedMounts)
                        Choice(Knobs.SecStandsOn(i));
                    Choice(Knobs.SecMaterial(i));
                }
                items.Add(new Act("Add a secondary battery", () => Edit(AddSecondary(doc.Current), "Secondary battery added")));
                items.Add(new Header("Anti-aircraft"));
                Num(Knobs.AaHeavy);
                Num(Knobs.AaLight);
                items.Add(new Header("Torpedoes"));
                Num(Knobs.TorpedoMounts);
                Num(Knobs.TorpedoTubes);
                break;
            case 3:
                items.Add(new Header("Materials"));
                Pick("Armour", Templates.Armour, t => t.Name, t => Templates.SameMaterials(d.Armour?.Materials, t.Value),
                    d.Armour?.Materials is { } am ? $"custom: {am.GetValueOrDefault("belt", "?")}" : "default",
                    (x, v) => x with { Armour = (x.Armour ?? new ArmourInput()) with { Materials = Templates.MaterialsFor(x, v) } });
                items.Add(new Header("Side"));
                Num(Knobs.Belt);
                Num(Knobs.BeltBottom);
                Num(Knobs.UpperBelt);
                Num(Knobs.EndBeltMm("fore"));
                Num(Knobs.EndBeltReach("fore"));
                Num(Knobs.EndBeltMm("aft"));
                Num(Knobs.EndBeltReach("aft"));
                Num(Knobs.Bulkheads);
                Num(Knobs.Tds);
                if (d.StyleName == "carrier")
                    Num(Knobs.FlightDeckArmour);
                items.Add(new Header("Side, details", Details: true));
                Num(Knobs.BeltDepth);
                Num(Knobs.BeltHeight);
                Num(Knobs.UpperBeltToDeck);
                Choice(Knobs.UpperBeltExtent);
                foreach (var end in new[] { "fore", "aft" })
                {
                    Num(Knobs.EndBeltTip(end));
                    Num(Knobs.EndBeltBulkhead(end));
                }
                items.Add(new Header("Steering gear box", Details: true));
                Num(Knobs.SteeringSides);
                Num(Knobs.SteeringRoof);
                Num(Knobs.SteeringEnds);
                items.Add(new Header("Materials, part by part", Details: true));
                Choice(Knobs.UpperBeltMaterial);
                Choice(Knobs.EndBeltMaterial("fore"));
                Choice(Knobs.EndBeltMaterial("aft"));
                Choice(Knobs.SteeringMaterial);
                Choice(Knobs.SteeringRoofMaterial);
                var decks = d.Armour?.Decks ?? [];
                for (int i = 0; i < decks.Count; i++)
                {
                    int j = i;
                    items.Add(new Header($"Armour deck {i + 1}", "Remove", () => Edit(RemoveDeck(doc.Current, j), $"Armour deck {j + 1} removed")));
                    Num(Knobs.DeckMm(i));
                    Num(Knobs.DeckLevel(i));
                    Choice(Knobs.DeckExtent(i));
                    Choice(Knobs.DeckMaterial(i));
                }
                items.Add(new Act("Add an armour deck", () => Edit(AddDeck(doc.Current), "Armour deck added")));
                if ((d.Main?.Count ?? 0) > 0)
                {
                    items.Add(new Header("Turrets"));
                    for (int i = 0; i < d.Main!.Count; i++)
                        Num(Knobs.MainArmour(i), $"Battery {(char)('A' + i)} faces");
                }
                break;
            case 4:
                items.Add(new Header("Speed and range"));
                Num(Knobs.Speed);
                Num(Knobs.Range);
                items.Add(new Header("Plant"));
                Pick("Technology", Templates.Plants, t => t.Name, t => t.Name == d.Machinery?.Tech?.Name,
                    d.Machinery?.Tech is { } tech ? $"custom: {tech.Name}" : $"default: {ship?.Report.Plant.Name}",
                    (x, v) => x with { Machinery = (x.Machinery ?? new MachineryInput()) with { Tech = v } });
                Num(Knobs.Stress);
                Num(Knobs.Shafts);
                Choice(Knobs.Arrangement);
                if (ship != null)
                    items.Add(new Note($"{ship.Report.Plant.Name}, {ship.Report.Plant.Fuel}: {N0(ship.Report.Plant.RatedShp)} shp, " +
                                       $"{ship.Report.Plant.Shafts} shafts, {N0(ship.Report.Plant.WeightT)} t"));
                items.Add(new Header("Machinery, details", Details: true));
                Num(Knobs.Funnels);
                Choice(Knobs.Transmission);
                Num(Knobs.UnitsPerShaft);
                Toggle(Knobs.CentrelineBulkhead);
                Choice(Knobs.Bunkers);
                Num(Knobs.WingBunker);
                Num(Knobs.Rudders);
                break;
            case 5:
                items.Add(new Header("Hull"));
                Pick("Construction", Templates.Hulls, t => t.Note, t => t.Name == d.Hull?.Construction?.Name,
                    d.Hull?.Construction is { } hc ? $"custom: {hc.Name}" : $"default: {ship?.Report.Hull.Construction}",
                    (x, v) => x with { Hull = (x.Hull ?? new HullInput()) with { Construction = v } });
                Num(Knobs.BlockCoefficient);
                Num(Knobs.Freeboard);
                if (Knobs.Raised.AppliesTo(d))
                    Choice(Knobs.Raised);
                items.Add(new Header("Hull form"));
                Choice(Knobs.Topside);
                Num(Knobs.TumblehomeStrength);
                Num(Knobs.TumblehomeKnuckle);
                Choice(Knobs.TumblehomeExtent);
                items.Add(new Header("Upperworks"));
                Num(Knobs.TowerLevels);
                Num(Knobs.DeckhouseLevels);
                Toggle(Knobs.AftControl);
                if (ship?.Report.Bridge is { } br)
                    items.Add(new Note($"Bridge eye {br.EyeHeightM:0.0} m, horizon {br.HorizonKm:0.0} km" +
                                       (br.SeesOverTurrets ? ", sees over the turrets" : ", blocked by the turrets")));
                items.Add(new Header("Hull and upperworks, details", Details: true));
                Choice(Knobs.DeckFinish);
                Num(Knobs.ShellMm);
                Num(Knobs.DeckWoodMm);
                if (style.HasControlTowers)
                    Num(Knobs.LevelsOverBridge);
                Num(Knobs.SuperstructurePlating);
                Num(Knobs.ControlPlating);
                Choice(Knobs.HullMaterial);
                Choice(Knobs.SuperstructureMaterial);
                break;
            case 6:
                foreach (var b in new[] { "main", "secondary", "aa" })
                {
                    Num(Knobs.Directors(b));
                    Num(Knobs.Rangefinder(b));
                }
                items.Add(new Header("Fire control, details", Details: true));
                foreach (var (b, name) in new[] { ("main", "Main"), ("secondary", "Secondary"), ("aa", "AA") })
                {
                    Num(Knobs.DirectorArmour(b), $"{name} director armour");
                    Num(Knobs.DirectorRadar(b), $"{name} fire-control radar, t");
                    Num(Knobs.DirectorComputer(b), $"{name} computer, t");
                }
                Num(Knobs.SearchRadar);
                break;
            case 7:
                Pick("Crew standard", Templates.Crew, t => $"{t.Group}: {t.Name}", t => t.Name == d.Crew?.Standard?.Name,
                    d.Crew?.Standard is { } cs ? $"custom: {cs.Name}" : $"default: {ship?.Report.Crew?.Standard}",
                    (x, v) => x with { Crew = (x.Crew ?? new CrewInput()) with { Standard = v } });
                Num(Knobs.Endurance);
                Toggle(Knobs.Distiller);
                items.Add(new Header("Crew, details", Details: true));
                Num(Knobs.BufferDays);
                Num(Knobs.WaterRation);
                Num(Knobs.BerthRatio);
                Num(Knobs.OfficerFraction);
                if (ship?.Report.Crew is { } c)
                    items.Add(new Note($"{N0(c.Complement)} men ({c.Officers} officers), {c.Standard}; {c.SleepM2PerMan:0.00} m² to sleep per man " +
                                       $"against {c.SleepStandardM2:0.00}"));
                break;
        }
        return items;
    }

    // ------------------------------------------------------------------ edits

    void Edit(Design next, string label, string? key = null)
    {
        doc.Apply(next, label, key);
        editing = null;
        stale = true;
    }

    /// <summary>Steps a number knob <paramref name="steps"/> steps along the displayed units' grid.</summary>
    void Step(NumRow r, int steps)
    {
        var k = r.Knob;
        double step = k.Step(units);
        double v = r.Num ?? k.Range(doc.Current).Lo;
        double next = Math.Round((v + steps * step) / step) * step;
        SetValue(k, next);
    }

    void SetValue(NumberKnob k, double? value)
    {
        var d = doc.Current;
        // the label runs from the value before this knob's run of edits
        var top = doc.History[doc.Index];
        var before = top.Key == k.Id && doc.Index > 0 ? doc.History[doc.Index - 1].Design : d;
        var next = k.Apply(d, value);
        string from = k.Value(before, shown?.Ship) is { } f ? Fmt(k, f) : "Auto";
        string to = k.Value(next, null) is { } t ? Fmt(k, t) : "Auto";
        Edit(next, $"{k.Label} {from} to {to}", k.Id);
    }

    static Design AddMain(Design d) => d with
    {
        Main = [.. d.Main ?? [], new BatteryInput { CalibreMm = 152, CalibreLength = 50, Barrels = 2, Fore = 1, Aft = 0, ArmourMm = 25 }],
    };

    static Design AddSecondary(Design d) => d with
    {
        Secondary = [.. d.Secondary ?? [], d.StyleName == "warship"
            ? new BatteryInput { CalibreMm = 102, CalibreLength = 45, Barrels = 1, PerSide = 2, ArmourMm = 25, Mount = "deck" }
            : new BatteryInput { CalibreMm = 102, CalibreLength = 45, Barrels = 1, Count = 2, ArmourMm = 25 }],
    };

    static Design RemoveAt(Design d, int i, bool main)
    {
        var l = ((main ? d.Main : d.Secondary) ?? []).ToList();
        l.RemoveAt(i);
        return main ? d with { Main = l } : d with { Secondary = l };
    }

    static Design AddDeck(Design d)
    {
        var a = d.Armour ?? new ArmourInput();
        var decks = (a.Decks ?? []).ToList();
        int deck = decks.Count == 0 ? 1 : decks.Max(x => x.Deck ?? 1) + 1;
        decks.Add(new ArmourDeckInput { Deck = deck, Mm = 25, Extent = "citadel" });
        return d with { Armour = a with { Decks = decks } };
    }

    static Design RemoveDeck(Design d, int i)
    {
        var decks = d.Armour!.Decks!.ToList();
        decks.RemoveAt(i);
        return d with { Armour = d.Armour with { Decks = decks } };
    }

    // ------------------------------------------------------------------ UI

    public void BuildUi()
    {
        Sync();
        if (snap == null || stale)
        {
            snap = Build();
            stale = false;
        }
        Keys();
        var vp = ImGui.GetMainViewport();
        T.Push();
        ImGui.SetNextWindowPos(vp.WorkPos);
        ImGui.SetNextWindowSize(vp.WorkSize);
        ImGui.PushStyleVar(ImGuiStyleVar.WindowPadding, Vector2.Zero);
        ImGui.Begin("##designer", ImGuiWindowFlags.NoDecoration | ImGuiWindowFlags.NoMove | ImGuiWindowFlags.NoBringToFrontOnFocus |
                                  ImGuiWindowFlags.NoSavedSettings | ImGuiWindowFlags.NoScrollWithMouse);
        ImGui.PopStyleVar();
        ImGui.PushFont(UiFonts.Sans);
        var size = vp.WorkSize;
        float midW = size.X - RailW - LegendW, midH = size.Y - TopBarH - HistoryH;

        TopBar(new Vector2(size.X, TopBarH));
        ImGui.SetCursorPos(new Vector2(0, TopBarH));
        Rail(new Vector2(RailW, midH));
        ImGui.SetCursorPos(new Vector2(RailW, TopBarH));
        Centre(new Vector2(midW, midH));
        ImGui.SetCursorPos(new Vector2(RailW + midW, TopBarH));
        Legend(new Vector2(LegendW, midH));
        ImGui.SetCursorPos(new Vector2(0, TopBarH + midH));
        History(new Vector2(size.X, HistoryH));

        ImGui.PopFont();
        ImGui.End();
        T.Pop();
    }

    void Keys()
    {
        var io = ImGui.GetIO();
        if (io.WantTextInput)
            return;
        if (io.KeyCtrl && ImGui.IsKeyPressed(ImGuiKey.Z))
            Undo();
        else if (io.KeyCtrl && ImGui.IsKeyPressed(ImGuiKey.Y))
            Redo();
        else if (!io.KeyCtrl)
            for (int i = 0; i < Sections.Length; i++)
                if (ImGui.IsKeyPressed(ImGuiKey._0 + i))
                    Open(i);
    }

    void Undo()
    {
        doc.Undo();
        stale = true;
    }

    void Redo()
    {
        doc.Redo();
        stale = true;
    }

    void Open(int s)
    {
        if (s == section)
            return;
        section = s;
        editing = null;
        doc.Seal();
        stale = true;
    }

    void TopBar(Vector2 size)
    {
        var s = snap!;
        var dl = ImGui.GetWindowDrawList();
        var p = ImGui.GetCursorScreenPos();
        dl.AddRectFilled(p, p + size, T.U32(T.Panel));
        dl.AddLine(p + new Vector2(0, size.Y - 1), p + size - V(0, 1), T.U32(T.Line));

        // the type label and the name: click to edit, Enter keeps it, Escape or clicking away doesn't
        ImGui.SetCursorPos(V(16, 4));
        TextField("##type", s.TypeLabel, UiFonts.SansSmall, T.Muted, P(200), doc.Current.Type ?? "",
            v => doc.Current with { Type = v.Length > 0 ? v : null }, "Type label");
        ImGui.SetCursorPos(V(16, 22));
        TextField("##name", s.Title, UiFonts.Title, T.Text, P(260), doc.Current.Name ?? "",
            v => doc.Current with { Name = v.Length > 0 ? v : null }, "Name");
        ImGui.SameLine();
        ImGui.SetCursorPosY(P(26));
        ImGui.TextColored(T.Muted, s.File);

        float x = Math.Max(P(360), ImGui.GetItemRectMax().X - ImGui.GetWindowPos().X + P(24));   // after a long name
        ImGui.SetCursorPos(new Vector2(x, P(14)));
        if (ImGui.Button("New"))
        {
            Load(DesignDoc.New());
            stale = true;
        }
        ImGui.SameLine();
        if (ImGui.Button("Open…"))
        {
            shippedDesigns = List(Path.Combine(AppContext.BaseDirectory, "Content", "Designs"));
            userDesigns = List(DesignDoc.UserFolder);
            ImGui.OpenPopup("open");
        }
        OpenPopup();
        ImGui.SameLine();
        if (ImGui.Button("Save as…"))
        {
            saveName = Path.GetFileNameWithoutExtension(DesignDoc.FileName(doc.Current));
            ImGui.OpenPopup("save");
        }
        SavePopup();
        ImGui.SameLine(0, P(24));
        ImGui.BeginDisabled(!doc.CanUndo);
        if (ImGui.Button("Undo"))
            Undo();
        ImGui.EndDisabled();
        ImGui.SameLine();
        ImGui.BeginDisabled(!doc.CanRedo);
        if (ImGui.Button("Redo"))
            Redo();
        ImGui.EndDisabled();
        ImGui.SameLine();
        ImGui.BeginDisabled(doc.Index == 0 && !doc.Dirty);
        if (ImGui.Button("Reset"))
        {
            doc.Reset();
            stale = true;
        }
        ImGui.EndDisabled();

        ImGui.SameLine(0, P(24));
        int u = (int)units;
        bool unitsChanged = ImGui.RadioButton("metric", ref u, 0);
        ImGui.SameLine();
        unitsChanged |= ImGui.RadioButton("imperial", ref u, 1);
        if (unitsChanged)
        {
            units = (UnitSystem)u;
            stale = true;
        }
        ImGui.SameLine(0, P(24));
        if (ImGui.Button(s.Style))
        {
            pennant = doc.Current.Look?.Number ?? "";
            ImGui.OpenPopup("style");
        }
        StylePopup();

        // the right end: build status, then Accept / Cancel for a caller
        float right = size.X - 16;
        if (onClose != null)
        {
            right -= P(180);
            ImGui.SetCursorPos(new Vector2(right, 14));
            ImGui.PushStyleColor(ImGuiCol.Button, T.AmberBg);
            ImGui.PushStyleColor(ImGuiCol.Text, T.Brass);
            if (ImGui.Button("Accept design"))
                onClose(doc.Current);
            ImGui.PopStyleColor(2);
            ImGui.SameLine();
            if (ImGui.Button("Cancel"))
                onClose(null);
        }
        bool busy = worker.Busy;
        string st = busy ? s.Settling : s.Status;
        float w = ImGui.CalcTextSize(st).X;
        ImGui.SetCursorPos(new Vector2(right - w - P(30), 18));
        if (busy)
            Spinner(dl, ImGui.GetCursorScreenPos() + V(-14, 9), P(6));
        ImGui.TextColored(busy ? T.Brass : T.Muted, st);
    }

    /// <summary>Text that turns into an input when clicked; Enter applies <paramref name="apply"/> as a history entry.</summary>
    void TextField(string id, string shown, ImFontPtr font, Vector4 colour, float width, string current, Func<string, Design> apply, string what)
    {
        if (editing == id)
        {
            ImGui.SetNextItemWidth(width);
            if (editFocus)
            {
                ImGui.SetKeyboardFocusHere();
                editFocus = false;
            }
            if (ImGui.InputText(id, ref editText, 80, ImGuiInputTextFlags.EnterReturnsTrue | ImGuiInputTextFlags.AutoSelectAll))
            {
                var v = editText.Trim();
                editing = null;
                Edit(apply(v), $"{what}: {(v.Length > 0 ? v : "none")}");
            }
            else if (ImGui.IsItemDeactivated())
                editing = null;
            return;
        }
        ImGui.PushFont(font);
        ImGui.TextColored(colour, shown);
        ImGui.PopFont();
        if (ImGui.IsItemHovered())
        {
            ImGui.SetMouseCursor(ImGuiMouseCursor.TextInput);
            ImGui.SetTooltip("click to edit");
        }
        if (ImGui.IsItemClicked())
        {
            editing = id;
            editText = current;
            editFocus = true;
        }
    }

    static void Spinner(ImDrawListPtr dl, Vector2 c, float r)
    {
        float a = (float)(ImGui.GetTime() * 6);
        dl.PathArcTo(c, r, a, a + 4.2f, 16);
        dl.PathStroke(T.U32(T.Brass), ImDrawFlags.None, P(2));
    }

    static readonly string[] Navies = ["(the design's own)", .. Shipgen.Render.Looks.Navies];
    static readonly string[] Eras = ["(the design's own)", .. Shipgen.Render.Looks.Eras];

    /// <summary>The navy and era the sprite is drawn in: tried on without touching the design (and shown in the other
    /// scenes too), or kept in it.</summary>
    void StylePopup()
    {
        if (!ImGui.BeginPopup("style"))
            return;
        ImGui.TextColored(T.Muted, "How she looks: a navy's paint and fittings in an era");
        var look = Look;
        int ni = Math.Max(0, Array.IndexOf(Navies, look?.Navy)), ei = Math.Max(0, Array.IndexOf(Eras, look?.Era));
        ImGui.SetNextItemWidth(P(260));
        bool changed = ImGui.Combo("navy", ref ni, Navies, Navies.Length);
        ImGui.SetNextItemWidth(P(260));
        changed |= ImGui.Combo("era", ref ei, Eras, Eras.Length);
        if (changed)
            SetLook(ni == 0 && ei == 0 ? null : new LookInput { Navy = ni > 0 ? Navies[ni] : null, Era = ei > 0 ? Eras[ei] : null });
        ImGui.Separator();
        ImGui.SetNextItemWidth(P(120));
        if (ImGui.InputText("pennant number", ref pennant, 12, ImGuiInputTextFlags.EnterReturnsTrue) || ImGui.IsItemDeactivatedAfterEdit())
        {
            var v = pennant.Trim();
            var d = doc.Current;
            Edit(d with { Look = (d.Look ?? new LookInput()) with { Number = v.Length > 0 ? v : null } }, $"Pennant number: {(v.Length > 0 ? v : "none")}");
        }
        ImGui.TextColored(T.Muted, "painted on the hull by the looks that carry numbers (cold war)");
        if (look != null)
        {
            if (ImGui.Button("Keep in the design"))
            {
                var d = doc.Current;
                var kept = (d.Look ?? new LookInput()) with { Navy = look.Navy ?? d.Look?.Navy, Era = look.Era ?? d.Look?.Era };
                Edit(d with { Look = kept }, $"Look: {kept.Navy ?? "generic"}, {kept.Era ?? "wwii"}");
                SetLook(null);
            }
            ImGui.SameLine();
            if (ImGui.Button("Back to the design's own"))
                SetLook(null);
        }
        ImGui.EndPopup();
    }

    static string[] List(string dir) => Directory.Exists(dir)
        ? Directory.GetFiles(dir, "*.json").Order(StringComparer.Ordinal).ToArray()
        : [];

    void OpenPopup()
    {
        if (!ImGui.BeginPopup("open"))
            return;
        ImGui.TextColored(T.Muted, "New design: an 8 kn hull and nothing else");
        if (ImGui.Selectable("  empty hull"))
            Load(DesignDoc.New());
        ImGui.Separator();
        void Group(string title, string[] files)
        {
            if (files.Length == 0)
                return;
            ImGui.TextColored(T.Muted, title);
            ImGui.BeginChild("##" + title, V(320, Math.Min(files.Length * 26 + 4, 300)));
            foreach (var f in files)
                if (ImGui.Selectable("  " + Path.GetFileNameWithoutExtension(f)))
                {
                    try
                    {
                        Load(DesignDoc.Open(f));
                    }
                    catch (Exception e)
                    {
                        Console.Error.WriteLine($"designer: {f}: {e.Message}");
                    }
                    ImGui.CloseCurrentPopup();
                }
            ImGui.EndChild();
        }
        Group("My designs", userDesigns);
        Group("Shipped designs", shippedDesigns);
        ImGui.EndPopup();
    }

    void SavePopup()
    {
        if (!ImGui.BeginPopup("save"))
            return;
        ImGui.TextColored(T.Muted, DesignDoc.UserFolder);
        ImGui.SetNextItemWidth(P(260));
        bool enter = ImGui.InputText("##name", ref saveName, 64, ImGuiInputTextFlags.EnterReturnsTrue);
        ImGui.SameLine();
        if ((ImGui.Button("Save") || enter) && saveName.Trim().Length > 0)
        {
            var file = DesignDoc.FileName(doc.Current with { Name = saveName.Trim() });
            doc.Save(Path.Combine(DesignDoc.UserFolder, file));
            stale = true;
            ImGui.CloseCurrentPopup();
        }
        ImGui.EndPopup();
    }

    void Rail(Vector2 size)
    {
        var s = snap!;
        ImGui.PushStyleColor(ImGuiCol.ChildBg, T.Panel);
        ImGui.BeginChild("##rail", size);
        var dl = ImGui.GetWindowDrawList();
        for (int i = 0; i < Sections.Length; i++)
        {
            var p = ImGui.GetCursorScreenPos();
            var rowSize = new Vector2(size.X, P(42));
            ImGui.PushID(i);
            if (ImGui.InvisibleButton("##sec", rowSize))
                Open(i);
            bool hover = ImGui.IsItemHovered();
            ImGui.PopID();
            if (i == section)
                dl.AddRectFilled(p, p + rowSize, T.U32(T.Panel2));
            else if (hover)
                dl.AddRectFilled(p, p + rowSize, T.U32(T.Inset));
            if (i == section)
                dl.AddRectFilled(p, p + new Vector2(P(3), rowSize.Y), T.U32(T.Brass));
            var num = p + V(14, 11);
            dl.AddRect(num, num + V(20, 20), T.U32(i == section ? T.Brass : T.Line), P(3));
            dl.AddText(UiFonts.Mono, UiFonts.Mono.FontSize, num + V(6, 1), T.U32(i == section ? T.Brass : T.Muted), DigitText[i]);
            dl.AddText(UiFonts.Sans, UiFonts.Sans.FontSize, p + V(46, 4), T.U32(i == section ? T.Brass : T.Text), Sections[i]);
            dl.AddText(UiFonts.SansSmall, UiFonts.SansSmall.FontSize, p + V(46, 23), T.U32(T.Muted), s.Summaries[i]);
            dl.AddLine(p + new Vector2(0, rowSize.Y), p + rowSize, T.U32(T.Line));
        }
        ImGui.SetCursorPosY(size.Y - P(48));
        ImGui.PushFont(UiFonts.SansSmall);
        ImGui.PushTextWrapPos(size.X - P(12));
        ImGui.SetCursorPosX(P(14));
        ImGui.TextColored(T.Muted, "0–7 open a section · Ctrl+Z undo · wheel over a value steps it (Shift: ×4)");
        ImGui.PopTextWrapPos();
        ImGui.PopFont();
        ImGui.EndChild();
        ImGui.PopStyleColor();
    }

    static readonly string[] DigitText = ["0", "1", "2", "3", "4", "5", "6", "7"];

    void Centre(Vector2 size)
    {
        ImGui.BeginChild("##centre", size);
        var s = snap!;
        float pad = P(12);
        float w = size.X - 2 * pad;
        float topH = MathF.Round(Math.Clamp(size.Y * 0.28f, P(140), P(300)));
        float profH = MathF.Round(Math.Clamp(size.Y * 0.16f, P(80), P(180)));
        topSize = new Vector2(w, topH);
        profileSize = new Vector2(w, profH);
        Offscreen(ref topTarget, topSize);
        Offscreen(ref profileTarget, profileSize);
        bool dim = worker.Busy;
        var tint = dim ? new Vector4(1, 1, 1, 0.75f) : Vector4.One;

        ImGui.SetCursorPos(new Vector2(pad, pad));
        if (topTarget != null)
        {
            var p = ImGui.GetCursorScreenPos();
            ImGui.Image((nint)topTarget.Resolve, topSize, Vector2.Zero, Vector2.One, tint);
            ScaleBar(ImGui.GetWindowDrawList(), p + new Vector2(P(10), topH - P(12)), topView.Zoom);
        }
        ImGui.SetCursorPos(new Vector2(pad, pad + topH + 6));
        if (profileTarget != null)
            ImGui.Image((nint)profileTarget.Resolve, profileSize, Vector2.Zero, Vector2.One, tint);

        float y = pad + topH + 6 + profH + 12;
        ImGui.SetCursorPos(new Vector2(pad, y));
        float panelH = size.Y - y - pad;
        if (section == 0)
        {
            float sheetW = MathF.Round(w - Math.Min(P(280), w * 0.4f) - P(12));
            Panel("##panel", new Vector2(sheetW, panelH), s.Panel);
            ImGui.SetCursorPos(new Vector2(pad + sheetW + 12, y));
            Standard(new Vector2(w - sheetW - 12, panelH));
        }
        else
            Panel("##panel", new Vector2(w, panelH), s.Panel);
        ImGui.EndChild();
    }

    /// <summary>A scale bar of a round length that fits about 120 px.</summary>
    void ScaleBar(ImDrawListPtr dl, Vector2 at, float pxPerM)
    {
        if (pxPerM <= 0)
            return;
        double unitM = units == UnitSystem.Metric ? 1 : 0.3048;
        double want = P(120) / pxPerM / unitM;
        double len = new[] { 5, 10, 20, 25, 50, 100, 200, 250, 500, 1000 }.FirstOrDefault(x => x >= want * 0.6, 1000);
        float px = (float)(len * unitM * pxPerM);
        uint c = T.U32(T.Text);
        dl.AddLine(at, at + new Vector2(px, 0), c, P(2));
        dl.AddLine(at + V(0, -4), at + V(0, 4), c, 2);
        dl.AddLine(at + new Vector2(px, P(-4)), at + new Vector2(px, P(4)), c, 2);
        dl.AddText(UiFonts.SansSmall, UiFonts.SansSmall.FontSize, at + new Vector2(px + P(6), P(-8)), c, units == UnitSystem.Metric ? ScaleText(len, "m") : ScaleText(len, "ft"));
    }

    readonly Dictionary<(double, string), string> scaleTexts = [];
    string ScaleText(double len, string unit)
    {
        if (!scaleTexts.TryGetValue((len, unit), out var t))
            scaleTexts[(len, unit)] = t = $"{len:0} {unit}";
        return t;
    }

    void Standard(Vector2 size)
    {
        var s = snap!;
        ImGui.PushStyleColor(ImGuiCol.ChildBg, T.Panel);
        ImGui.BeginChild("##standard", size, ImGuiChildFlags.Border);
        ImGui.SetCursorPos(V(14, 10));
        ImGui.PushFont(UiFonts.SansBold);
        ImGui.TextColored(T.Muted, "STANDARD DISPLACEMENT");
        ImGui.PopFont();
        ImGui.SetCursorPosX(P(14));
        ImGui.PushFont(UiFonts.MonoBig);
        ImGui.TextColored(worker.Busy ? T.Muted : T.Text, s.Std);
        ImGui.PopFont();
        ImGui.SetCursorPosX(P(14));
        ImGui.TextColored(T.Muted, s.StdDelta);
        ImGui.Spacing();
        foreach (var (label, value) in s.Consequences)
        {
            ImGui.SetCursorPosX(P(14));
            ImGui.TextColored(T.Muted, label);
            ImGui.SameLine(size.X * 0.45f);
            ImGui.PushFont(UiFonts.Mono);
            ImGui.TextUnformatted(value);
            ImGui.PopFont();
        }
        ImGui.EndChild();
        ImGui.PopStyleColor();
    }

    void Panel(string id, Vector2 size, List<Item> items)
    {
        ImGui.PushStyleColor(ImGuiCol.ChildBg, T.Panel);
        ImGui.BeginChild(id, size, ImGuiChildFlags.Border);
        ImGui.Indent(P(6));
        ImGui.Dummy(V(0, 2));
        float labelW = P(170), valueW = P(110);
        bool folded = false;   // under a closed Details header
        for (int n = 0; n < items.Count; n++)
        {
            if (folded && items[n] is NumRow or ChoiceRow or ToggleRow or PickRow)
                continue;
            ImGui.PushID(n);
            switch (items[n])
            {
                case Header { Details: true } h:
                    ImGui.Dummy(V(0, 4));
                    bool open = openDetails.Contains((section, h.Text));
                    ImGui.SetNextItemOpen(open);
                    ImGui.PushStyleColor(ImGuiCol.Text, T.Muted);
                    bool nowOpen = ImGui.CollapsingHeader(h.Text);
                    ImGui.PopStyleColor();
                    if (nowOpen != open && !openDetails.Remove((section, h.Text)))
                        openDetails.Add((section, h.Text));
                    folded = !nowOpen;
                    break;
                case Header h:
                    folded = false;
                    ImGui.Dummy(V(0, 4));
                    ImGui.PushFont(UiFonts.SansBold);
                    ImGui.TextColored(T.Brass, h.Upper);
                    ImGui.PopFont();
                    if (h.Button != null)
                    {
                        ImGui.SameLine(size.X - P(90));
                        ImGui.PushFont(UiFonts.SansSmall);
                        if (ImGui.SmallButton(h.Button))
                            h.OnButton!();
                        ImGui.PopFont();
                    }
                    ImGui.Separator();
                    break;
                case Note t:
                    ImGui.PushTextWrapPos(size.X - P(16));
                    ImGui.TextColored(T.Muted, t.Text);
                    ImGui.PopTextWrapPos();
                    break;
                case Act a:
                    if (ImGui.Button(a.Label))
                        a.OnClick();
                    break;
                case NumRow r:
                    NumberRow(r, labelW, valueW);
                    break;
                case ChoiceRow c:
                    ImGui.BeginDisabled(!c.Applies);
                    ImGui.AlignTextToFramePadding();
                    ImGui.TextUnformatted(c.Knob.Label);
                    ImGui.SameLine(labelW);
                    ImGui.SetNextItemWidth(valueW + P(150));
                    int idx = c.Index;
                    if (ImGui.Combo("##c", ref idx, c.Labels, c.Labels.Length) && idx < c.Knob.Values.Length)
                        Edit(c.Knob.Set(doc.Current, c.Knob.Values[idx]), $"{c.Knob.Label}: {c.Labels[idx]}");
                    ImGui.EndDisabled();
                    break;
                case PickRow pr:
                    ImGui.AlignTextToFramePadding();
                    ImGui.TextUnformatted(pr.Label);
                    ImGui.SameLine(labelW);
                    ImGui.SetNextItemWidth(Math.Min(P(460), size.X - labelW - P(24)));
                    int pi = pr.Index;
                    if (ImGui.Combo("##p", ref pi, pr.Names, pr.Names.Length) && pi < pr.Count && pi != pr.Index)
                        pr.Apply(pi);
                    break;
                case ToggleRow tr:
                    ImGui.BeginDisabled(!tr.Applies);
                    ImGui.AlignTextToFramePadding();
                    ImGui.TextUnformatted(tr.Knob.Label);
                    ImGui.SameLine(labelW);
                    bool v = tr.Value;
                    if (ImGui.Checkbox("##t", ref v))
                        Edit(tr.Knob.Set(doc.Current, v), $"{tr.Knob.Label} {(v ? "on" : "off")}");
                    ImGui.EndDisabled();
                    break;
            }
            ImGui.PopID();
        }
        ImGui.Unindent(P(6));
        ImGui.EndChild();
        ImGui.PopStyleColor();
    }

    void NumberRow(NumRow r, float labelW, float valueW)
    {
        var k = r.Knob;
        ImGui.BeginDisabled(!r.Applies);
        ImGui.AlignTextToFramePadding();
        ImGui.TextUnformatted(r.Label);
        ImGui.SameLine(labelW);
        float bw = ImGui.GetFrameHeight();
        if (ImGui.Button("−", new Vector2(bw, 0)))
            Step(r, ImGui.GetIO().KeyShift ? -4 : -1);
        ImGui.SameLine(0, P(4));
        if (editing == k.Id)
        {
            ImGui.SetNextItemWidth(valueW);
            if (editFocus)
            {
                ImGui.SetKeyboardFocusHere();
                editFocus = false;
            }
            if (ImGui.InputText("##v", ref editText, 32, ImGuiInputTextFlags.EnterReturnsTrue | ImGuiInputTextFlags.AutoSelectAll))
            {
                if (Units.TryParse(editText, k.Quantity, units, out double metric))
                {
                    SetValue(k, metric);
                    doc.Seal();
                }
                editing = null;
            }
            else if (ImGui.IsItemDeactivated())
                editing = null;
        }
        else
        {
            ImGui.PushStyleColor(ImGuiCol.Button, T.Inset);
            ImGui.PushFont(UiFonts.Mono);
            if (ImGui.Button(r.Value, new Vector2(valueW, 0)))
            {
                editing = k.Id;
                editText = r.Num is { } x ? Fmt(k, x, unit: false).Replace(",", "") : "";
                editFocus = true;
            }
            ImGui.PopFont();
            ImGui.PopStyleColor();
            if (ImGui.IsItemHovered())
            {
                ImGui.SetItemKeyOwner(ImGuiKey.MouseWheelY);   // the value takes the wheel, the panel doesn't scroll
                if (ImGui.GetIO().MouseWheel is var wheel && wheel != 0)
                    Step(r, Math.Sign(wheel) * (ImGui.GetIO().KeyShift ? 4 : 1));
            }
        }
        ImGui.SameLine(0, P(4));
        if (ImGui.Button("+", new Vector2(bw, 0)))
            Step(r, ImGui.GetIO().KeyShift ? 4 : 1);
        ImGui.SameLine();
        if (r.IsAuto && k.CanAuto)
            ImGui.TextColored(T.Muted, "Auto");
        else if (k.CanAuto)
        {
            ImGui.PushFont(UiFonts.SansSmall);
            if (ImGui.SmallButton("auto"))
            {
                SetValue(k, null);
                doc.Seal();
            }
            ImGui.PopFont();
        }
        else
            ImGui.Dummy(Vector2.Zero);   // ends the line SameLine opened
        if (r.Delta.Length > 0)
        {
            ImGui.SameLine(labelW + valueW + 2 * bw + P(90));
            ImGui.TextColored(T.Brass, r.Delta);
        }
        ImGui.EndDisabled();
    }

    void Legend(Vector2 size)
    {
        var s = snap!;
        ImGui.PushStyleColor(ImGuiCol.ChildBg, T.Paper);
        ImGui.PushStyleColor(ImGuiCol.Text, T.Ink);
        ImGui.BeginChild("##legend", size);
        var dl = ImGui.GetWindowDrawList();
        float pad = P(16), w = size.X - 2 * pad;
        var ink = worker.Busy ? T.InkMuted : T.Ink;
        ImGui.SetCursorPos(new Vector2(pad, 12));
        ImGui.PushFont(UiFonts.CaslonBig);
        ImGui.TextUnformatted("Legend");
        ImGui.PopFont();
        foreach (var (label, value) in s.Particulars)
        {
            ImGui.SetCursorPosX(pad);
            ImGui.PushFont(UiFonts.Caslon);
            ImGui.TextColored(T.InkMuted, label);
            ImGui.PopFont();
            ImGui.SameLine(pad + P(96));
            ImGui.PushFont(UiFonts.Mono);
            ImGui.PushTextWrapPos(size.X - pad);
            ImGui.TextColored(ink, value);
            ImGui.PopTextWrapPos();
            ImGui.PopFont();
            var y = ImGui.GetCursorScreenPos().Y - 3;
            dl.AddLine(new Vector2(ImGui.GetWindowPos().X + pad, y), new Vector2(ImGui.GetWindowPos().X + size.X - pad, y), T.U32(T.PaperLine));
        }

        // the weight bar, its groups below it
        ImGui.Dummy(V(0, 6));
        ImGui.SetCursorPosX(pad);
        var p = ImGui.GetCursorScreenPos();
        float x = p.X;
        foreach (var (g, frac, _) in s.Weights)
        {
            float wpx = frac * w;
            dl.AddRectFilled(new Vector2(x, p.Y), new Vector2(x + wpx, p.Y + 16), T.U32(T.Group(g)));
            x += wpx;
        }
        dl.AddRect(p, p + new Vector2(w, P(16)), T.U32(T.InkMuted));
        ImGui.Dummy(new Vector2(w, P(20)));
        ImGui.PushFont(UiFonts.SansSmall);
        float colX = 0;
        for (int i = 0; i < s.Weights.Length; i++)
        {
            ImGui.SetCursorPosX(pad + colX);
            var q = ImGui.GetCursorScreenPos();
            dl.AddRectFilled(q + V(0, 4), q + V(9, 13), T.U32(T.Group(s.Weights[i].Group)));
            ImGui.SetCursorPosX(pad + colX + P(14));
            ImGui.TextColored(T.InkMuted, s.Weights[i].Text);
            if (i % 2 == 0)
            {
                ImGui.SameLine();
                colX = w / 2;
            }
            else
                colX = 0;
        }
        if (s.Weights.Length % 2 == 1)
            ImGui.NewLine();
        ImGui.PopFont();

        ImGui.Dummy(V(0, 8));
        ImGui.SetCursorPosX(pad);
        ImGui.PushFont(UiFonts.CaslonBig);
        ImGui.TextUnformatted(s.Remarks.Length > 0 ? $"Remarks ({s.Remarks.Length})" : "Remarks");
        ImGui.PopFont();
        ImGui.PushTextWrapPos(size.X - pad);
        if (s.Remarks.Length == 0)
        {
            ImGui.SetCursorPosX(pad);
            ImGui.TextColored(T.InkMuted, "None.");
        }
        foreach (var (error, text) in s.Remarks)
        {
            ImGui.SetCursorPosX(pad);
            ImGui.PushFont(UiFonts.SansBold);
            ImGui.TextColored(T.AmberInk, error ? "Won't build" : "Note");
            ImGui.PopFont();
            ImGui.SameLine();
            ImGui.PushFont(UiFonts.SansSmall);
            ImGui.TextUnformatted(text);
            ImGui.PopFont();
        }
        ImGui.PopTextWrapPos();
        ImGui.EndChild();
        ImGui.PopStyleColor(2);
    }

    void History(Vector2 size)
    {
        var s = snap!;
        var dl = ImGui.GetWindowDrawList();
        var p = ImGui.GetCursorScreenPos();
        dl.AddRectFilled(p, p + size, T.U32(T.Panel));
        dl.AddLine(p, p + new Vector2(size.X, 0), T.U32(T.Line));
        ImGui.SetCursorPos(ImGui.GetCursorPos() + V(16, 9));
        ImGui.PushFont(UiFonts.SansBold);
        ImGui.TextColored(T.Muted, "HISTORY");
        ImGui.PopFont();
        ImGui.SameLine(0, P(16));
        // the newest entries that fit, oldest first
        int first = Math.Max(0, s.HistoryLabels.Length - 8);
        for (int i = first; i < s.HistoryLabels.Length; i++)
        {
            ImGui.PushID(i);
            bool current = i == doc.Index;
            ImGui.PushStyleColor(ImGuiCol.Text, current ? T.Brass : i > doc.Index ? T.Line : T.Muted);
            if (ImGui.Selectable(s.HistoryLabels[i], current, ImGuiSelectableFlags.None, ImGui.CalcTextSize(s.HistoryLabels[i])))
            {
                doc.Jump(i);
                stale = true;
            }
            ImGui.PopStyleColor();
            ImGui.PopID();
            ImGui.SameLine(0, P(18));
        }
        ImGui.NewLine();
    }

    public void Dispose()
    {
        if (ownsWorker)
            worker.Dispose();
        SDL_WaitForGPUIdle(device);
        sprite.Dispose();
        profile.Dispose();
        topTarget?.Dispose();
        profileTarget?.Dispose();
    }
}
