using System.Globalization;
using System.Numerics;
using Fleetwright.Sim.Missions;
using ImGuiNET;
using T = Fleetwright.Designer.DesignerTheme;

namespace Fleetwright.Map;

/// <summary>The launch screen's right column (docs/map-ui/launch.md, the mockup launch-bureau.html): Play as (a row a side
/// and Watch, Tab to switch), the strength ledger (the sides by group, yours against the other, share bars), the brief,
/// the remarks (what the player should know and might miss: the tempers the scenario gave and why, a class that didn't
/// build, the world's error) and the Begin box: the load in a table and a segment a class, the margin box's verdict, and
/// Save setup…, Begin paused and Begin.</summary>
public sealed unsafe partial class LaunchScene
{
    // ------------------------------------------------------------------ the right column

    void RightColumn(Vector2 a, Vector2 size)
    {
        float w = size.X, y = a.Y, bottom = a.Y + size.Y;
        // play as
        float playH = TitleH + RowH * Math.Max(playRows.Count, 1) + P(2);
        PlayAs(new Vector2(a.X, y), new Vector2(w, playH));
        y += playH + Gap;
        // the strength ledger
        var lv = Ledger();
        float ledgerH = TitleH + HeadH + RowH * Math.Max(lv.Rows.Count, 1) + P(2);
        StrengthLedger(new Vector2(a.X, y), new Vector2(w, ledgerH), lv);
        y += ledgerH + Gap;
        // begin at the foot, the remarks over it, the brief in what's left
        float beginH = P(190);
        float textW = w - P(30);
        Remarks();
        float remarksBody = 0;
        foreach (var (_, text) in remarks)
            remarksBody += WrappedHeight(UiFonts.SansSmall, text, textW) + P(8);
        float remarksH = Math.Clamp(TitleH + Math.Max(remarksBody, RowH) + P(6), P(64), P(240));
        float beginY = bottom - beginH;
        BeginBox(new Vector2(a.X, beginY), new Vector2(w, beginH));
        float remarksY = beginY - Gap - remarksH;
        RemarksPane(new Vector2(a.X, remarksY), new Vector2(w, remarksH));
        BriefPane(new Vector2(a.X, y), new Vector2(w, remarksY - Gap - y));
    }

    void PlayAs(Vector2 a, Vector2 size)
    {
        float right = Pane(a, size, "PLAY AS");
        RightNote(right, a.Y, TitleH, "Tab to switch");
        float w = size.X - 2, y = a.Y + TitleH + 1;
        for (int i = 0; i < playRows.Count; i++)
        {
            var r = playRows[i];
            var ra = new Vector2(a.X + 1, y);
            ImGui.PushID(i);
            var (click, hover) = HitRow("##play", ra, new Vector2(w, RowH));
            ImGui.PopID();
            bool on = r.Side == side;
            if (click && !on && !begin)
                pendingSide = r.Side;
            RowBg(ra, w, RowH, i, on, hover);
            Radio(new Vector2(ra.X + P(12), ra.Y + RowH / 2), on);
            float x = ra.X + P(24);
            if (r.Side >= 0)
                Ensign(r.Ensign, new Vector2(x, ra.Y + P(4)), new Vector2(P(18), P(12)));
            else
            {
                var wa = new Vector2(x, ra.Y + P(4));
                dl.AddRectFilled(wa, wa + new Vector2(P(18), P(12)), U(T.Button));
                dl.AddCircle(wa + new Vector2(P(9), P(6)), P(3.5f), U(T.KeyText), 12);
                dl.AddCircleFilled(wa + new Vector2(P(9), P(6)), P(1.2f), U(T.KeyText), 8);
            }
            x += P(24);
            Txt(UiFonts.SansBold, new Vector2(x, Mid(UiFonts.SansBold, ra.Y, RowH)), on ? T.Brass : T.Bright, r.Name);
            x += Math.Max(Width(UiFonts.SansBold, r.Name), P(52)) + P(8);
            string chip = r.Side < 0 ? "" : on ? "you" : "AI";
            float chipX = ra.X + w - P(6) - (chip.Length > 0 ? ChipWidth(chip) : 0);
            float shipsX = chipX - P(8);
            TxtR(UiFonts.SansSmall, shipsX, Mid(UiFonts.SansSmall, ra.Y, RowH), T.Muted, r.Ships);
            dl.PushClipRect(new Vector2(x, ra.Y), new Vector2(shipsX - Width(UiFonts.SansSmall, r.Ships) - P(6), ra.Y + RowH), true);
            Txt(UiFonts.SansSmall, new Vector2(x, Mid(UiFonts.SansSmall, ra.Y, RowH)), T.Muted, r.Commander);
            dl.PopClipRect();
            if (chip.Length > 0)
                Chip(new Vector2(chipX, ra.Y + P(2.5f)), chip, on ? ChipKind.Amber : ChipKind.Blue);
            if (hover && r.Character.Length > 0)
                ImGui.SetTooltip(r.Character);
            y += RowH;
        }
    }

    void BriefPane(Vector2 a, Vector2 size)
    {
        Pane(a, size, "BRIEF");
        if (!BeginBody("##brief", a, size, out var keep))
        {
            EndBody(keep, 0);
            return;
        }
        var o = ImGui.GetCursorScreenPos();
        float w = ImGui.GetContentRegionAvail().X;
        float h = Wrapped(UiFonts.Sans, o + new Vector2(P(8), P(6)), T.Text, brief, w - P(16));
        EndBody(keep, h + P(12));
    }

    (string?, int) remarkKey = (null, -1);
    int remarksStatic;

    /// <summary>The remarks: the static ones made with the pick, then the load's failures (a class that didn't build, the
    /// world's error), kept until either changes.</summary>
    void Remarks()
    {
        int failed = 0;
        if (world.LoadClasses is { } ids)
            foreach (int id in ids)
                failed += world.Classes.IsReady(id) && world.Classes.ErrorOf(id) != null ? 1 : 0;
        var key = (world.LoadError, failed);
        if (key == remarkKey)
            return;
        if (remarkKey.Item2 < 0)
            remarksStatic = remarks.Count;   // the first look since Made: what's there is the pick's own
        remarkKey = key;
        remarks.RemoveRange(remarksStatic, remarks.Count - remarksStatic);
        if (world.LoadError is { } err)
            remarks.Insert(0, (true, err));
        if (failed > 0 && world.LoadIds is { } byName)
            foreach (var (design, id) in byName)
                if (world.Classes.IsReady(id) && world.Classes.ErrorOf(id) is { } why)
                    remarks.Add((true, $"{TitleOf(design)} didn't build: {why}"));
        remarkCount = remarks.Count.ToString(CultureInfo.InvariantCulture);
    }

    void RemarksPane(Vector2 a, Vector2 size)
    {
        float right = Pane(a, size, "REMARKS");
        RightNote(right, a.Y, TitleH, remarkCount);
        if (!BeginBody("##remarks", a, size, out var keep))
        {
            EndBody(keep, 0);
            return;
        }
        var o = ImGui.GetCursorScreenPos();
        float w = ImGui.GetContentRegionAvail().X, y = o.Y + P(4);
        if (remarks.Count == 0)
        {
            Txt(UiFonts.SansSmall, new Vector2(o.X + P(8), y), T.Muted, "None.");
            y += RowH;
        }
        foreach (var (amber, text) in remarks)
        {
            Txt(UiFonts.SansBold, new Vector2(o.X + P(7), y - P(1)), amber ? T.Amber : T.Muted, amber ? "!" : "•");
            y += Wrapped(UiFonts.SansSmall, new Vector2(o.X + P(20), y), amber ? T.Amber : T.Text, text, w - P(28)) + P(8);
        }
        EndBody(keep, y - o.Y);
    }

    // ------------------------------------------------------------------ the strength ledger

    sealed class LedgerView
    {
        public Scenario? Of;
        public int Known;   // the classes figured when it was made
        public string HeadA = "", HeadB = "", TonsA = "", TonsB = "";
        public readonly List<(string Group, string A, string B, float Share)> Rows = [];   // Share NaN: no bar
    }

    LedgerView? ledger;

    static readonly (string Type, string Group)[] TypeGroups =
        [("BB", "Battleships"), ("BC", "Battlecruisers"), ("CA", "Armoured cruisers"), ("CL", "Cruisers"), ("DD", "Destroyers"), ("TB", "Torpedo boats"),
         ("AV", "Seaplane carriers"), ("CV", "Carriers")];

    /// <summary>The sides by group, yours (or the first) against the other: ships by type, guns by calibre, the line's
    /// speed, the gun crews, the tonnage; made again as the classes' figures come in.</summary>
    LedgerView Ledger()
    {
        if (current is not { } s)
            return ledger ??= new LedgerView();
        int known = figuredCount;
        if (ledger is { } v && ReferenceEquals(v.Of, s) && v.Known == known)
            return v;
        v = ledger = new LedgerView { Of = s, Known = known };
        int a = side >= 0 ? side : playRows.Count > 0 ? playRows[0].Side : 0;
        int b = playRows.Select(r => r.Side).FirstOrDefault(sd => sd >= 0 && sd != a, -1);
        v.HeadA = s.AboutOf(a)?.Ensign ?? "";
        v.HeadB = b >= 0 ? s.AboutOf(b)?.Ensign ?? "" : "";
        var types = new Dictionary<string, int[]>();
        int[] heavy = new int[2], medium = new int[2];
        double[] tons = new double[2], slowHeavy = [double.MaxValue, double.MaxValue], slowAny = [double.MaxValue, double.MaxValue];
        bool all = true;
        void Walk(ScenarioUnit u)
        {
            if (u.Removed)
                return;
            int k = u.Side == a ? 0 : u.Side == b ? 1 : -1;
            foreach (var x in u.Units)
                Walk(x);
            if (k < 0)
                return;
            foreach (var sh in u.Ships)
            {
                string type = InfoOf(sh.Design)?.Type ?? "";
                if (!types.TryGetValue(type, out var n))
                    types[type] = n = new int[2];
                n[k] += sh.Count;
                int id = ClassOf(sh.Design);
                if (FiguresOf(id) is not { } f || world.Classes.TryGet(id) is not { } c)
                {
                    all = false;
                    continue;
                }
                tons[k] += f.Profile.DisplacementT * sh.Count;
                slowAny[k] = Math.Min(slowAny[k], f.Profile.SpeedKn);
                if (type is "BB" or "BC" or "CA")
                    slowHeavy[k] = Math.Min(slowHeavy[k], f.Profile.SpeedKn);
                foreach (ref readonly var bat in c.Batteries)
                    if (bat.Kind is Sim.Ships.Battery.Main or Sim.Ships.Battery.Secondary)
                    {
                        if (bat.CalibreMm >= 254)
                            heavy[k] += bat.Guns * sh.Count;
                        else if (bat.CalibreMm >= 120)
                            medium[k] += bat.Guns * sh.Count;
                    }
            }
        }
        foreach (var u in s.Units)
            Walk(u);
        static float Share(double x, double y) => x + y > 0 ? (float)(x / (x + y)) : float.NaN;
        static string N(int n) => n.ToString(CultureInfo.InvariantCulture);
        foreach (var (type, group) in TypeGroups)
            if (types.TryGetValue(type, out var n))
                v.Rows.Add((group, N(n[0]), b >= 0 ? N(n[1]) : "", Share(n[0], n[1])));
        foreach (var (type, n) in types)
            if (!TypeGroups.Any(t => t.Type == type))
                v.Rows.Add((type.Length > 0 ? type : "Other", N(n[0]), b >= 0 ? N(n[1]) : "", Share(n[0], n[1])));
        if (all)
        {
            v.Rows.Add(("Guns 254 mm and up", N(heavy[0]), b >= 0 ? N(heavy[1]) : "", Share(heavy[0], heavy[1])));
            v.Rows.Add(("Guns 120–253 mm", N(medium[0]), b >= 0 ? N(medium[1]) : "", Share(medium[0], medium[1])));
            // the line's speed: its slowest heavy ship's (a side of light craft only: its slowest)
            string Kn(int k) => (slowHeavy[k] < double.MaxValue ? slowHeavy[k] : slowAny[k]) is var x && x < double.MaxValue ? x.ToString("0", CultureInfo.InvariantCulture) : "—";
            v.Rows.Add(("Line speed, kn", Kn(0), b >= 0 ? Kn(1) : "", float.NaN));
            (v.TonsA, v.TonsB) = (tons[0].ToString("N0", Inv), b >= 0 ? tons[1].ToString("N0", Inv) : "");
        }
        v.Rows.Add(("Gun crews", SettingOf(s, SideKind.Crews, a), b >= 0 ? SettingOf(s, SideKind.Crews, b) : "", float.NaN));
        if (all)
            v.Rows.Add(("Tonnage", v.TonsA, v.TonsB, Share(tons[0], tons[1])));
        return v;
    }

    void StrengthLedger(Vector2 a, Vector2 size, LedgerView v)
    {
        float right = Pane(a, size, "STRENGTH LEDGER");
        RightNote(right, a.Y, TitleH, setup.IsEmpty ? "the sides as written" : "the sides as set");
        float w = size.X - 2;
        var o = new Vector2(a.X + 1, a.Y + TitleH + 1);
        float xA = o.X + P(176), xB = o.X + P(224), barX = o.X + P(234), barW = w - P(240);
        Head(o, w);
        HeadCell(o, o.X + P(6), "GROUP");
        Ensign(v.HeadA, new Vector2(xA - P(16), o.Y + P(4)), new Vector2(P(16), P(10)));
        if (v.HeadB.Length > 0)
            Ensign(v.HeadB, new Vector2(xB - P(16), o.Y + P(4)), new Vector2(P(16), P(10)));
        HeadCell(o, barX, "SHARE");
        float y = o.Y + HeadH;
        for (int i = 0; i < v.Rows.Count; i++)
        {
            var (group, va, vb, share) = v.Rows[i];
            var ra = new Vector2(o.X, y);
            bool total = group == "Tonnage";
            RowBg(ra, w, RowH, i);
            Txt(total ? UiFonts.SansBold : UiFonts.Sans, new Vector2(ra.X + P(6), Mid(UiFonts.Sans, ra.Y, RowH)), total ? T.Bright : T.Text, group);
            var font = char.IsDigit(va.Length > 0 ? va[0] : ' ') || va == "—" ? UiFonts.Mono : UiFonts.SansSmall;
            TxtR(font, xA, Mid(font, ra.Y, RowH), T.Bright, va);
            TxtR(font, xB, Mid(font, ra.Y, RowH), T.Bright, vb);
            if (!float.IsNaN(share) && vb.Length > 0)
                ShareBar(new Vector2(barX, ra.Y + RowH / 2 - P(3.5f)), barW, P(7), share);
            y += RowH;
        }
    }

    // ------------------------------------------------------------------ begin: the load, the verdict, the buttons

    object? clockOf;
    double compiledAt = -1, drawnAt = -1;
    string compiledN = "", drawnN = "", compiledS = "", drawnS = "", threadsN = "";
    (int, int, int, int) loadKey = (-1, 0, 0, 0);

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

    /// <summary>Each frame: when the load's classes were all compiled and all drawn, and its lines (made when a count or
    /// the tenth of a second changes).</summary>
    void Load()
    {
        var (compiled, drawn, of) = Counts();
        if (!ReferenceEquals(clockOf, world.LoadIds))
            (clockOf, compiledAt, drawnAt) = (world.LoadIds, -1, -1);
        if (of > 0 && compiled == of && compiledAt < 0)
            compiledAt = world.LoadClock;
        if (of > 0 && drawn == of && drawnAt < 0)
            drawnAt = world.LoadClock;
        int tenths = compiled < of || drawn < of ? (int)(world.LoadClock * 10) : -1;
        var key = (compiled, drawn, of, tenths);
        if (key == loadKey)
            return;
        loadKey = key;
        compiledN = $"{compiled} / {of}";
        drawnN = $"{drawn} / {of}";
        compiledS = compiledAt >= 0 ? $"{compiledAt:0.00} s" : of > 0 ? $"{world.LoadClock:0.0} s" : "";
        drawnS = drawnAt >= 0 ? $"{drawnAt:0.00} s" : of > 0 && compiled == of ? $"{world.LoadClock:0.0} s" : "";
        threadsN = world.Classes.Threads.ToString(CultureInfo.InvariantCulture);
        status = of == 0 ? "" : compiled < of
            ? $"building: classes {compiled} of {of} · art {drawn} of {of} · {world.LoadClock:0.0} s · {threadsN} builder thread{(world.Classes.Threads == 1 ? "" : "s")}"
            : $"{of} class{(of == 1 ? "" : "es")} built in {compiledAt:0.00} s · {(drawn < of ? $"art {drawn} of {of}" : "art in")} · {threadsN} builder thread{(world.Classes.Threads == 1 ? "" : "s")}";
    }

    void BeginBox(Vector2 a, Vector2 size)
    {
        var (compiled, drawn, of) = Counts();
        bool ready = current != null && of > 0 && compiled == of && world.LoadError == null;
        float right = Pane(a, size, "BEGIN");
        RightNote(right, a.Y, TitleH, begin ? "beginning" : ready ? "the world is placed" : current == null ? "" : "building");
        float w = size.X - 2, y = a.Y + TitleH + 1, x0 = a.X + 1;
        float xN = x0 + w - P(80), xS = x0 + w - P(8);
        void Row(int i, string label, string n, string s, bool done)
        {
            var ra = new Vector2(x0, y);
            RowBg(ra, w, RowH, i);
            Txt(UiFonts.Sans, new Vector2(ra.X + P(6), Mid(UiFonts.Sans, ra.Y, RowH)), T.Text, label);
            TxtR(UiFonts.Mono, xN, Mid(UiFonts.Mono, ra.Y, RowH), T.Bright, n);
            TxtR(UiFonts.Mono, xS, Mid(UiFonts.Mono, ra.Y, RowH), done ? T.Ok : T.Muted, s);
            y += RowH;
        }
        Row(0, "Classes compiled", compiledN, compiledS, of > 0 && compiled == of);
        Row(1, "Art drawn", drawnN, drawnS, of > 0 && drawn == of);
        Row(2, "Builder threads", threadsN, "", true);
        // a segment a class: dark while it builds, dark brass compiled, brass with its art
        y += P(6);
        var bar0 = new Vector2(x0 + P(6), y);
        float bw = w - P(12), bh = P(8);
        dl.AddRectFilled(bar0, bar0 + new Vector2(bw, bh), U(T.Inset));
        if (of > 0 && world.LoadClasses is { } ids)
        {
            float seg = bw / of, sx = bar0.X;
            foreach (int id in ids)
            {
                var fill = world.Classes.IsDrawn(id) ? T.Brass : world.Classes.IsReady(id) ? T.BrassDark : T.Inset;
                dl.AddRectFilled(new Vector2(sx, y), new Vector2(sx + seg, y + bh), U(fill));
                if (of <= 80)
                    dl.AddLine(new Vector2(sx + seg, y), new Vector2(sx + seg, y + bh), U(T.Edge));
                sx += seg;
            }
        }
        dl.AddRect(bar0, bar0 + new Vector2(bw, bh), U(T.Edge));
        y += bh + P(8);
        // the margin box: the verdict
        var m0 = new Vector2(x0 + P(6), y);
        var m1 = new Vector2(x0 + w - P(6), y + P(46));
        var (bg, edge, ink, word) = current == null || world.LoadError != null ? (T.AmberTint, T.AmberEdge, T.Amber, "Not ready")
            : begin ? (T.Panel2, T.Bevel, T.Text, "Beginning")
            : ready ? (T.OkTint, T.OkEdge, T.Ok, "Ready")
            : (T.Panel2, T.Bevel, T.Text, "Building");
        dl.AddRectFilled(m0, m1, U(bg));
        dl.AddRect(m0, m1, U(edge));
        Txt(UiFonts.MonoBig, new Vector2(m0.X + P(8), m0.Y + P(3)), ink, word);
        dl.PushClipRect(m0, m1, true);
        Txt(UiFonts.SansSmall, new Vector2(m0.X + P(8), m0.Y + P(28)), T.Muted, current == null ? "see the remarks" : readyLine);
        dl.PopClipRect();
        y = m1.Y + P(8);
        // the buttons: the primary takes the room left, as the mockup's
        float bx = x0 + P(6);
        const string SaveL = "Save setup…", SaveK = "Ctrl+S", PausedL = "Begin paused";
        if (Button("##save", new Vector2(bx, y), SaveL, SaveK, enabled: CanSave))
            OpenSave();
        if (ImGui.IsItemHovered() && !CanSave)
            ImGui.SetTooltip(setup.IsEmpty ? "nothing changed from the scenario as written" : "a setup is of a shipped scenario");
        bx += ButtonWidth(SaveL, SaveK) + P(4);
        if (Button("##paused", new Vector2(bx, y), PausedL, enabled: current != null && !begin))
            BeginWorld(paused: true);
        if (ImGui.IsItemHovered())
            ImGui.SetTooltip("Shift+Enter");
        bx += ButtonWidth(PausedL, null) + P(4);
        string go = side < 0 ? "Watch" : "Begin";
        if (Button("##begin", new Vector2(bx, y), go, "Enter", primary: true, enabled: current != null && !begin, w: x0 + w - P(6) - bx))
            BeginWorld(paused: false);
    }
}
