using System.Globalization;
using System.Numerics;
using Fleetwright.Gpu;
using Fleetwright.HitView;
using Fleetwright.Shipgen;
using ImGuiNET;
using SDL;
using static SDL.SDL3;
using T = Fleetwright.Ui.Theme;
using static Fleetwright.Ui.Pieces;

namespace Fleetwright.Designer;

/// <summary>
/// The ship designer: a scene of its own that takes a design in and hands one back. The game opens it from its UI
/// with a design (or <see cref="DesignDoc.Empty"/>) and a callback; Accept calls back with the edited design, Cancel
/// with null. Without a callback (the dev launch) it only saves to the player's folder.
///
/// Layout after docs/designer-ui/bureau.html (DesignerScene.Layout.cs): the outliner and templates on the left, the plan
/// and profile on top of the centre (each from its renderer into an offscreen target) with the sections as tabs under
/// it, the weight ledger, particulars and remarks on the right, and the history in the status bar. Every edit goes into the <see cref="DesignDoc"/>, the new design
/// to the <see cref="DesignWorker"/>, and the result lands a few frames later: nothing an edit does waits for a build.
/// While a build runs the last result stays up, dimmed.
/// </summary>
public sealed unsafe partial class DesignerScene : IScene
{
    const double SpriteScale = 8;   // px per metre at mip 0
    const int MipLevels = 4;
    static Vector2 V(float x, float y) => new(P(x), P(y));

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
    ulong profileMask;   // the kinds the profile always draws; the magazines overlay adds its own
    static readonly ulong MagazineBit = 1UL << HitKinds.IndexOf("magazine");

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
        if (session != null)
            session.Units = units;
        this.section = Math.Clamp(section, 0, Sections.Length - 1);
        if (session != null)   // the session already builds this design
        {
            submitted = doc.History[0].Json;
            seenDesign = session.DesignVersion;
        }
        sprite = new ShipSpriteRenderer(device);
        profile = new HitboxRenderer(device) { Background = T.Plan };
        ulong mask = 0;
        for (int k = 0; k < HitKinds.All.Length; k++)
            if (HitKinds.All[k].Name is "belt" or "strake" or "armour_deck" or "armoured_bulkhead" or "barbette" or "conning_tower"
                or "main" or "secondary" or "waterline" or "hull_lines")
                mask |= 1UL << k;
        profileMask = profileState.KindMask = mask;
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
        if (DevUi != null)
            ApplyDevUi(DevUi);
    }

    /// <summary>A dev hook for screenshots: UI state to set when a designer opens (-designer-ui=, see ApplyDevUi).</summary>
    public static string[]? DevUi { get; set; }

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

    /// <summary>A design changed in another scene opens here: the viewer's pick or a reload starts over, an edit from
    /// outside (a dropped file) is one more step in the history.</summary>
    void Sync()
    {
        if (session == null || session.DesignVersion == seenDesign)
            return;
        seenDesign = session.DesignVersion;
        if (session.Design is { } d && d.ToJson() != doc.History[doc.Index].Json)
        {
            if (session.Edited)
            {
                doc.Apply(d, session.EditLabel ?? "Changed elsewhere");
                doc.Seal();
                editing = null;
                stale = true;
            }
            else
                Switch(new DesignDoc(d, session.DesignPath));
            submitted = doc.History[doc.Index].Json;
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
        // the plan and the profile at one scale, the ship's middle at the middle of both: a general arrangement, the
        // overlays line up
        profileState.KindMask = profileMask | (showMags ? MagazineBit : 0);
        float zoom = sprite.Meta != null ? sprite.FitZoom(topSize.X, topSize.Y, 0.96f, 0.86f) : 0;
        if (profile.Mesh is { Prisms.Length: > 0 } m)
        {
            profileCam.Fit(m.Vertices, profileSize.X / Math.Max(1, profileSize.Y), profileMask);
            float fit = profileSize.Y / (2 * profileCam.HalfHeight);
            zoom = zoom > 0 ? Math.Min(zoom, fit) : fit;
            profileCam.HalfHeight = profileSize.Y / (2 * zoom);
            profileCam.Target = profileCam.Target with { X = 0 };
        }
        if (sprite.Meta != null)
        {
            topView.Zoom = zoom;
            topView.Centre = Vector2.Zero;
        }
        sprite.Render(cmd, topTarget!, topView, ReadOnlySpan<double>.Empty, Clear(T.Plan));
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
        public required string Title, TypeLabel, File, Status, Std, Settling, Style;
        public required string[] Summaries;
        public required (string Label, string Value)[] Particulars;
        /// <summary>The resource bar's figures.</summary>
        public required (string Label, string Value)[] Stats;
        public required LedgerRow[] Ledger;
        public required string FullT, RemarkCount;
        public required OutlineRow[] Outline;
        public required (bool Error, string Text)[] Remarks;
        public required List<Item> Panel;
        /// <summary>The templates the design is built from (the left column's lower panel).</summary>
        public required PickRow[] Templates;
        public required string[] HistoryLabels;
        /// <summary>What the plan and profile's overlays draw, in ship metres; null without a ship.</summary>
        public required Overlay? Over;
        /// <summary>The Main battery tab (built only while it's open).</summary>
        public required MainPane? Main;
        /// <summary>The Secondary · AA · TT tab (built only while it's open).</summary>
        public required SecPane? Sec;
        /// <summary>The Overview tab (built only while it's open).</summary>
        public required OvPane? Ov;
        /// <summary>The Protection tab (built only while it's open).</summary>
        public required ProtPane? Prot;
    }

    /// <summary>A gun mount on the plan: where it is, its tag, its arcs of fire (degrees clockwise from dead ahead, the
    /// end past the start; past 360 it keeps going).</summary>
    sealed record MountMark(string Tag, float X, float Y, bool Main, (float From, float To)[] Arcs, string Id, int Guns);

    /// <summary>An armour plate on the profile: x along the ship, z above the main deck, its thickness and label.</summary>
    sealed record Plate(float X0, float X1, float Z0, float Z1, double Mm, string Label);

    sealed record Overlay(MountMark[] Mounts, Plate[] Plates, float Length, float Beam, string Dimensions);

    /// <summary>A weight group in the ledger; <see cref="Tip"/> is its heaviest items.</summary>
    sealed record LedgerRow(string Group, string Name, string T, string Pct, float Frac, (string Name, string T)[] Tip);

    /// <summary>An outliner line: a section (opens its tab) or, under it, the weight items that belong to it.</summary>
    sealed record OutlineRow(int Section, bool Child, string Label, string Detail, string T, float Frac, string Group);

    /// <summary>A line of the section panel.</summary>
    abstract record Item;
    /// <summary>A section heading; a <see cref="Details"/> one folds away the rows under it (the second layer).</summary>
    sealed record Header(string Text, string? Button = null, Action? OnButton = null, bool Details = false) : Item
    {
        public string Upper { get; } = Text.ToUpperInvariant();
    }
    sealed record Note(string Text) : Item;
    sealed record Act(string Label, Action OnClick) : Item;
    sealed record NumRow(NumberKnob Knob, string Label, string Value, string Hint, string Delta, bool Applies, bool IsAuto, double? Num) : Item
    {
        /// <summary>The value without its unit, as typed (for a field that edits in place).</summary>
        public string Plain { get; init; } = "";
    }
    sealed record ChoiceRow(ChoiceKnob Knob, string[] Labels, int Index, bool Applies) : Item;
    /// <summary>A template picker: <see cref="Count"/> catalogue entries, then maybe the design's own block ("custom").</summary>
    sealed record PickRow(string Label, string[] Names, int Index, int Count, Action<int> Apply) : Item
    {
        /// <summary>A picker of its own in place of the list (the shells' family tree).</summary>
        public Action? Popup { get; init; }
        /// <summary>Each catalogue entry's group, a heading where it changes (null: no groups).</summary>
        public string[]? Groups { get; init; }
        /// <summary>Each catalogue entry's tooltip (null: none).</summary>
        public string[]? Tips { get; init; }
        public bool Applies { get; init; } = true;
    }
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

        string std = res != null ? N0(res.StandardDisplacementT) + " t" : "–";
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
            $"{d.SpeedKn:0.#} kn · {N0(d.RangeNm ?? Navarch.DefaultRangeNm)} nm",
            res != null ? $"Cb {res.BlockCoefficient:0.00} · tower {ship!.Report.Bridge?.TowerLevels}" : "",
            (ship?.Report.FireControl.Count ?? 0) switch { 0 => "no directors", 1 => "1 director", var n => $"{n} directors" },
            res != null ? $"{N0(res.Crew)} men" : "",
        ];

        var stats = new List<(string, string)>();
        if (res != null)
        {
            stats.Add(("STANDARD", std));
            stats.Add(("FULL LOAD", N0(res.FullDisplacementT) + " t"));
            stats.Add(("LENGTH", Fmt(res.LengthM, Quantity.Length, 0.1)));
            stats.Add(("BEAM", Fmt(res.BeamM, Quantity.Length, 0.1)));
            stats.Add(("DRAUGHT", Fmt(res.DraughtM, Quantity.Length, 0.1)));
            stats.Add(("SPEED", $"{d.SpeedKn:0.0} kn"));
            stats.Add(("RANGE", N0(d.RangeNm ?? Navarch.DefaultRangeNm) + " nm"));
            stats.Add(("POWER", N0(res.PowerShp) + " shp"));
            stats.Add(("GM", $"{res.GmFullM:0.00} m"));
            stats.Add(("CREW", N0(res.Crew)));
        }

        var part = new List<(string, string)>();
        if (res != null)
        {
            part.Add(("Dimensions", $"{Fmt(res.LengthM, Quantity.Length, unit: false)} × {Fmt(res.BeamM, Quantity.Length, unit: false)} × " +
                                    $"{Fmt(res.DraughtM, Quantity.Length, 0.1)}"));
            part.Add(("Displacement", $"{N0(res.StandardDisplacementT)} t std · {N0(res.FullDisplacementT)} t full"));
            part.Add(("Machinery", $"{N0(res.PowerShp)} shp · {d.SpeedKn:0.#} kn"));
            part.Add(("Range", $"{N0(d.RangeNm ?? Navarch.DefaultRangeNm)} nm"));
            part.Add(("Armament", mainGuns.Length + secGuns.Length > 0 ? string.Join(" · ", new[] { mainGuns, secGuns }.Where(s => s.Length > 0)) : "none"));
            part.Add(("Protection", belt > 0 || deck > 0 ? $"belt {Fmt(belt, Quantity.Armour, unit: false)} · deck {Fmt(deck, Quantity.Armour)}" : "none"));
            part.Add(("Complement", N0(res.Crew)));
            part.Add(("Stability", $"GM {res.GmFullM:0.00} m · roll {res.RollPeriodS:0.0} s"));
            part.Add(("Block coeff.", $"{res.BlockCoefficient:0.000}"));
            if (ship!.Report.Bridge is { } br)
                part.Add(("Bridge", $"eye {Fmt(br.EyeHeightM, Quantity.Length, 0.1)} · horizon {br.HorizonKm:0.0} km"));
            if (ship.Report.Crew is { } cr)
                part.Add(("Berths", $"{cr.SleepM2PerMan:0.00} m² / man (std {cr.SleepStandardM2:0.00})"));
            part.Add(("Plant", $"{ship.Report.Plant.Name}, {ship.Report.Plant.Fuel}"));
        }

        var ledger = new List<LedgerRow>();
        if (ship != null)
        {
            double total = Math.Max(1, ship.Report.WeightGroupsT.Values.Sum());
            foreach (var (g, t) in ship.Report.WeightGroupsT.OrderByDescending(kv => kv.Value))
                if (t > 0)
                    ledger.Add(new LedgerRow(g, GroupName(g), N0(t), $"{100 * t / total:0.0}", (float)(t / total),
                        Aggregate(ship.Report.Weights.Where(w => w.Group == g)).Take(8).Select(a => (a.Label, N0(a.T) + " t")).ToArray()));
        }

        var remarks = new List<(bool, string)>();
        if (latest is { Ship: null } bad)
            foreach (var e in bad.Errors.Where(e => e != "invalid design:"))
                remarks.Add((true, e));
        if (ship != null)
            foreach (var w in ship.Report.Warnings)
                remarks.Add((false, w));
        if (ship != null)
            foreach (var w in LimitRemarks(d, ship))
                remarks.Add((false, w));

        string file = doc.SourcePath != null ? Path.GetFileName(doc.SourcePath) + (doc.Dirty ? " *" : "") : doc.Dirty ? "unsaved" : "";
        string status = latest != null ? latest.Ship != null ? $"built in {latest.BuildS * 1000 + latest.MeshS * 1000 + latest.DrawS * 1000 + latest.BakeS * 1000:F0} ms"
            : "doesn't build: see the remarks" : "";

        var over = ship != null ? MakeOverlay(ship) : null;
        return new Snapshot
        {
            Title = d.Name ?? d.Id ?? "Design",
            TypeLabel = d.Type is { Length: > 0 } ty ? ty.ToUpperInvariant() : "NO TYPE LABEL",
            Style = $"Style: {Look?.Navy ?? d.Look?.Navy ?? "generic"} · {Look?.Era ?? d.Look?.Era ?? "wwii"}" + (Look != null ? " (trying)" : ""),
            File = file,
            Status = status,
            Settling = "settling…",
            Std = std,
            Summaries = summaries,
            Particulars = part.ToArray(),
            Stats = stats.ToArray(),
            Ledger = ledger.ToArray(),
            FullT = res != null ? N0(res.FullDisplacementT) : "–",
            RemarkCount = remarks.Count == 0 ? "none" : remarks.Count.ToString(CultureInfo.InvariantCulture),
            Outline = Outline(ship, summaries),
            Templates = [PlantPick(d, ship), HullPick(d, ship), CrewPick(d, ship), ArmourPick(d), ProtectionPick(d), ShellsPick(d),
                GunsPick(d), TorpedoPick(d), ShockPick(d), OutfitPick(d)],
            Remarks = remarks.ToArray(),
            Panel = PanelItems(d, ship),
            HistoryLabels = doc.History.Select(h => h.Label).ToArray(),
            Over = over,
            Main = section == 1 ? MainPaneOf(d, ship, over) : null,
            Sec = section == 2 ? SecPaneOf(d, ship, over) : null,
            Ov = section == 0 ? OvPaneOf(d, ship) : null,
            Prot = section == 3 ? ProtPaneOf(d, ship) : null,
        };
    }

    /// <summary>The overlays' geometry from the ship's hitboxes: the gun mounts and their arcs for the plan, the armour
    /// as plates for the profile (belt and strakes, decks, bulkheads, barbettes, turret faces, conning tower).</summary>
    Overlay MakeOverlay(Ship ship)
    {
        var hb = ship.Hitboxes;
        string Mm(double mm) => Fmt(mm, Quantity.Armour, unit: false);
        var mounts = new List<MountMark>();
        var plates = new List<Plate>();
        var barbetteR = new Dictionary<string, double>();
        foreach (var c in hb.Components)
            if (c.Kind == ComponentKind.Barbette && c.R is { } r)
                barbetteR[c.Id] = r;
        if (hb.Armour.Belt is { } b)
            plates.Add(new Plate((float)b.X0, (float)b.X1, (float)b.Bottom, (float)b.Top, b.ThicknessMm,
                b.BottomMm is { } bm && bm != b.ThicknessMm ? $"{Mm(b.ThicknessMm)}–{Mm(bm)}" : Mm(b.ThicknessMm)));
        foreach (var st in hb.Armour.Strakes ?? [])
            plates.Add(new Plate((float)st.X0, (float)st.X1, (float)st.Bottom, (float)st.Top, st.ThicknessMm, Mm(st.ThicknessMm)));
        foreach (var dk in hb.Armour.Decks ?? [])   // drawn half a metre thick so they show; labelled at their ends
            plates.Add(new Plate((float)dk.X0, (float)dk.X1, (float)dk.Z - 0.5f, (float)dk.Z, dk.ThicknessMm, Mm(dk.ThicknessMm)));
        foreach (var bh in hb.Armour.Bulkheads ?? [])
            plates.Add(new Plate((float)bh.X - 0.6f, (float)bh.X + 0.6f, (float)bh.Bottom, (float)bh.Top, bh.ThicknessMm, ""));
        foreach (var c in hb.Components)
        {
            if (c.X is not { } x)
                continue;
            switch (c.Kind)
            {
                case ComponentKind.Barbette when c.ArmourMm is { } mm && c.R is { } r:
                    plates.Add(new Plate((float)(x - r), (float)(x + r), (float)c.Base, (float)c.Top, mm, ""));
                    break;
                case ComponentKind.ConningTower when c.ArmourMm is { } mm && c.R is { } r:
                    plates.Add(new Plate((float)(x - r), (float)(x + r), (float)c.Base, (float)c.Top, mm, Mm(mm)));
                    break;
                case ComponentKind.Main or ComponentKind.Secondary:
                    double rr = barbetteR.GetValueOrDefault(c.Id, 2) * 1.15;
                    if (c.Kind == ComponentKind.Main && c.ArmourMm is { } face)
                        plates.Add(new Plate((float)(x - rr), (float)(x + rr), (float)c.Base, (float)c.Top, face, Mm(face)));
                    int barrels = c.Type != null && hb.TurretTypes.TryGetValue(c.Type, out var tt) ? (int)tt.Barrels : 1;
                    string tag = c.Kind == ComponentKind.Main ? $"{c.Id} {barrels}×{Fmt(c.CalibreMm ?? 0, Quantity.Calibre)}" : c.Id;
                    var arcs = (c.ArcsDeg ?? []).Where(a => a.Length == 2).Select(a => ((float)a[0], (float)(a[1] < a[0] ? a[1] + 360 : a[1]))).ToArray();
                    mounts.Add(new MountMark(tag, (float)x, (float)(c.Y ?? 0), c.Kind == ComponentKind.Main, arcs, c.Id, barrels));
                    break;
            }
        }
        var res = ship.Report.Results;
        string dims = $"{Fmt(res.LengthM, Quantity.Length, 0.1)} oa · {Fmt(res.BeamM, Quantity.Length, 0.1)} · T {Fmt(res.DraughtM, Quantity.Length, 0.1)}";
        return new Overlay(mounts.ToArray(), plates.ToArray(), (float)hb.Length, (float)hb.Beam, dims);
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
        return new NumRow(k, label ?? k.Label, v is { } x ? Fmt(k, x) : "–", hint, delta, k.AppliesTo(d), isAuto, v)
        {
            Plain = v is { } p ? Fmt(k, p, unit: false).Replace(",", "") : "",
        };
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


        switch (section)
        {
            case 0:
                break;   // its own pane (DesignerScene.Overview.cs)
            case 1:   // its own pane (DesignerScene.MainBattery.cs)
                break;
            case 2:   // its own pane (DesignerScene.Secondary.cs)
                break;
            case 3:   // its own pane (DesignerScene.Protection.cs)
                break;
            case 4:
                items.Add(new Header("Speed and range"));
                Num(Knobs.Speed);
                Num(Knobs.Range);
                items.Add(new Header("Plant"));
                items.Add(PlantPick(d, ship));
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
                items.Add(HullPick(d, ship));
                Num(Knobs.Scantlings);
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
                items.Add(new Header("Masts"));
                for (int i = 0; i < 2; i++)
                {
                    Choice(Knobs.MastKind(i));
                    Choice(Knobs.MastTop(i));
                    Choice(Knobs.MastDirector(i));
                    Num(Knobs.MastHeight(i));
                    Num(Knobs.MastTiers(i));
                }
                items.Add(new Header("Hull and upperworks, details", Details: true));
                Choice(Knobs.ShellJoint);
                Choice(Knobs.InternalsJoint);
                Toggle(Knobs.DoorsBelow);
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
            case 6:   // plain rows until the fire-control plan's tab (docs/designer-ui/tech-blocks.md)
                foreach (var (b, name) in new[] { ("main", "Main battery"), ("secondary", "Secondary battery"), ("aa", "Anti-aircraft") })
                {
                    items.Add(new Header(name));
                    Num(Knobs.Directors(b));
                    Num(Knobs.Rangefinder(b));
                    items.Add(PresetPick(d, b));
                    for (int slot = 0; slot < 4; slot++)
                        items.Add(BlockPick(d, b, slot));
                    if (b == "main")
                    {
                        for (int i = 0; i < (d.Main?.Count ?? 0); i++)
                            Num(Knobs.MainRangefinder(i), d.Main!.Count > 1 ? $"Turret rangefinders ({i + 1})" : null);
                        Num(Knobs.AftRangefinder);
                        Num(Knobs.TowerRangefinder);
                    }
                }
                items.Add(new Header("Fire control, details", Details: true));
                foreach (var (b, name) in new[] { ("main", "Main"), ("secondary", "Secondary"), ("aa", "AA") })
                {
                    Num(Knobs.DirectorArmour(b), $"{name} director armour");
                    Num(Knobs.DirectorRadar(b), $"{name} fire-control radar, t");
                }
                Num(Knobs.SearchRadar);
                break;
            case 7:
                items.Add(CrewPick(d, ship));
                items.Add(OutfitPick(d));
                items.Add(new Note(OutfitLine(Outfits.Of(d))));
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

    // a catalogue as a picker: the entry the design's block matches, else the block itself as "custom" (or what the
    // engine chose, when the design leaves it out)
    PickRow Pick<TV>(string label, IReadOnlyList<Template<TV>> list, Func<Template<TV>, string> name, Func<Template<TV>, bool> matches,
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
        return new PickRow(label, names.ToArray(), i, list.Count, k => Edit(apply(doc.Current, list[k].Value), $"{label}: {names[k]}"));
    }

    PickRow PlantPick(Design d, Ship? ship) => Pick("Technology", Templates.Plants, t => t.Name, t => t.Name == d.Machinery?.Tech?.Name,
        d.Machinery?.Tech is { } tech ? $"custom: {tech.Name}" : $"default: {ship?.Report.Plant.Name}",
        (x, v) => x with { Machinery = (x.Machinery ?? new MachineryInput()) with { Tech = v } });

    PickRow HullPick(Design d, Ship? ship) => Pick("Construction", Templates.Hulls, t => t.Note, t => t.Name == d.Hull?.Construction?.Name,
        d.Hull?.Construction is { } hc ? $"custom: {hc.Name}" : $"default: {ship?.Report.Hull.Construction}",
        (x, v) => x with { Hull = (x.Hull ?? new HullInput()) with { Construction = v with { Standard = x.Hull?.Construction?.Standard ?? v.Standard } } });   // the scantlings chosen stay

    PickRow CrewPick(Design d, Ship? ship) => Pick("Crew standard", Templates.Crew, t => $"{t.Group}: {t.Name}", t => t.Name == d.Crew?.Standard?.Name,
        d.Crew?.Standard is { } cs ? $"custom: {cs.Name}" : $"default: {ship?.Report.Crew?.Standard}",
        (x, v) => x with { Crew = (x.Crew ?? new CrewInput()) with { Standard = v } });

    PickRow ArmourPick(Design d) => Pick("Armour", Templates.Armour, t => t.Name, t => Templates.SameMaterials(d.Armour?.Materials, t.Value),
        d.Armour?.Materials is { } am ? $"custom: {am.GetValueOrDefault("belt", "?")}" : "default",
        (x, v) => x with { Armour = (x.Armour ?? new ArmourInput()) with { Materials = Templates.MaterialsFor(x, v) } });

    /// <summary>The protection layout (docs/designer-ui/protection-layers.md): the template the design is on, or Plain or
    /// Custom; a pick puts the template's layout on (ProtectionEdits.ApplyLayout). The same list as the Protection tab's
    /// Layout.</summary>
    PickRow ProtectionPick(Design d)
    {
        var list = Templates.Protection;
        var (i, exact) = ProtectionEdits.LayoutOf(d, list.Select(t => t.Value).ToList());
        var names = list.Select(t => t.Name).ToList();
        if (!exact)
            names.Add(i >= 0 ? $"{list[i].Name} · own values" : ProtectionEdits.HasLayout(d) ? "Custom" : "Plain");
        return new PickRow("Protection layout", names.ToArray(), exact ? i : names.Count - 1, list.Count, k =>
        {
            var next = ProtectionEdits.ApplyLayout(doc.Current, list[k].Value);
            Edit(next, $"Layout: {list[k].Name}");
            protLayoutApplied = (k, ProtectionEdits.LayoutKey(next));
        });
    }

    /// <summary>Every gun battery's shell family at once (docs/designer-ui/shells.md): the family they share, "mixed", or
    /// "default"; picked from the family tree.</summary>
    PickRow ShellsPick(Design d)
    {
        bool any = (d.Main ?? []).Concat(d.Secondary ?? []).Any(b => b.CalibreMm != null);
        bool dflt = !(d.Main ?? []).Concat(d.Secondary ?? []).Any(b => b.Shells is { Count: > 0 });
        string fam = !any ? "no guns" : dflt ? $"default: {ShellTemplates.DefaultFamily}" : ShellEdits.FamilyOfAll(d);
        return new PickRow("Shells: every battery's family", [fam], 0, 0, _ => { })
        {
            Popup = any ? () => FamilyTree(d, ShellEdits.FamilyOfAll(d), f => Edit(ShellEdits.SetFamilyAll(doc.Current, f), $"Shells: {f}")) : null,
        };
    }

    // ------------------------------------------------------------------ the outliner and the ledger

    static string GroupName(string g) => g switch
    {
        "fire_control" => "Fire control",
        "misc" => "Equipment & crew",
        _ => char.ToUpperInvariant(g[0]) + g[1..].Replace('_', ' '),
    };

    static readonly System.Text.RegularExpressions.Regex IdToken = new(@"^([A-Z]{1,2}\d*[A-Z]?|\d+[A-Z]?)$");

    /// <summary>A weight item's name without its mount or position id and any "(of what)": "Turret A", "Mount S1S",
    /// "Plotting room (Main director 2)" are "Turret", "Mount", "Plotting room".</summary>
    static string Kind(string name)
    {
        int paren = name.IndexOf(" (", StringComparison.Ordinal);
        if (paren > 0)
            name = name[..paren];
        int sp = name.LastIndexOf(' ');
        if (sp > 0 && IdToken.IsMatch(name[(sp + 1)..]))
            return name[..sp];
        if (sp < 0)
            return name.TrimEnd('0', '1', '2', '3', '4', '5', '6', '7', '8', '9');   // "AA17"
        return name;
    }

    /// <summary>Weight items of a kind together ("Turret ×4"), heaviest first; empty ones left out.</summary>
    static List<(string Label, double T)> Aggregate(IEnumerable<WeightReport> items)
    {
        var sums = new Dictionary<string, (double T, int N)>();
        var order = new List<string>();
        foreach (var w in items)
        {
            if (w.T < 0.5)
                continue;
            var k = Kind(w.Name);
            if (!sums.TryGetValue(k, out var a))
                order.Add(k);
            sums[k] = (a.T + w.T, a.N + 1);
        }
        return order.Select(k => (Label: sums[k].N > 1 ? $"{k} ×{sums[k].N}" : k, sums[k].T)).OrderByDescending(x => x.T).ToList();
    }

    /// <summary>The section a weight item is edited in: the main battery's turrets and magazines, the other guns,
    /// armour, machinery and fuel, hull and upperworks, fire control, crew and stores.</summary>
    static int SectionOf(WeightReport w, HashSet<string> mainMounts) => w.Group switch
    {
        "armament" => (w.Name.StartsWith("Turret ") || w.Name.StartsWith("Magazine ")) && mainMounts.Contains(w.Name[(w.Name.LastIndexOf(' ') + 1)..]) ? 1 : 2,
        "armour" => 3,
        "machinery" or "fuel" => 4,
        "hull" or "superstructure" => 5,
        "fire_control" => 6,
        _ => 7,
    };

    /// <summary>The weight group whose colour marks a section in the outliner.</summary>
    static readonly string[] SectionGroup = ["", "armament", "armament", "armour", "machinery", "hull", "fire_control", "misc"];

    OutlineRow[] Outline(Ship? ship, string[] summaries)
    {
        var rows = new List<OutlineRow>();
        double std = ship?.Report.Results.StandardDisplacementT ?? 0;
        var bySection = new List<WeightReport>[Sections.Length];
        for (int i = 0; i < bySection.Length; i++)
            bySection[i] = [];
        if (ship != null)
        {
            var mains = ship.Report.Weights.Where(w => w.Name.StartsWith("Turret ")).Select(w => w.Name[7..]).ToHashSet();
            foreach (var w in ship.Report.Weights)
                bySection[SectionOf(w, mains)].Add(w);
        }
        float Frac(double t) => std > 0 ? (float)(t / std) : 0;
        for (int i = 0; i < Sections.Length; i++)
        {
            double t = i == 0 ? std : bySection[i].Sum(w => w.T);
            rows.Add(new OutlineRow(i, false, Sections[i], summaries[i], ship != null ? N0(t) : "", i == 0 ? 0 : Frac(t), SectionGroup[i]));
            foreach (var (label, ct) in Aggregate(bySection[i]))
                rows.Add(new OutlineRow(i, true, label, "", N0(ct), Frac(ct), SectionGroup[i]));
        }
        return rows.ToArray();
    }

    // ------------------------------------------------------------------ edits

    void Edit(Design next, string label, string? key = null)
    {
        doc.Apply(next, label, key);
        editing = null;
        stale = true;
    }

    /// <summary>Steps a number knob <paramref name="steps"/> steps along the displayed units' grid.</summary>
    void Step(NumRow r, int steps) => SetValue(r.Knob, StepTarget(r.Knob, r.Num, steps));

    /// <summary>Where <paramref name="steps"/> steps from <paramref name="value"/> land on the displayed units' grid (the
    /// overview's probes ask the same, so a probe is exactly the step its click takes).</summary>
    double StepTarget(NumberKnob k, double? value, int steps) => k.Lever.Target(doc.Current, value, steps, k.Step(units));

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

    public void Dispose()
    {
        probes.Dispose();
        if (ownsWorker)
            worker.Dispose();
        SDL_WaitForGPUIdle(device);
        sprite.Dispose();
        profile.Dispose();
        topTarget?.Dispose();
        profileTarget?.Dispose();
    }
}
