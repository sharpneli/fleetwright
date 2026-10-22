using System.Globalization;
using System.Numerics;
using Fleetwright.Designer;
using Fleetwright.Sim;
using Fleetwright.Sim.Ai.Commanders;
using Fleetwright.Sim.Core;
using Fleetwright.Sim.Missions;
using ImGuiNET;

namespace Fleetwright.Map;

/// <summary>The map's HUD (docs/map-ui/battle-hud.md U1): the top bar (the side and scenario, the clock and its speeds,
/// in action, the aim and the end, the way to the designer and the menu), the briefing a scenario opens with, the scale
/// at the bottom right, the map's items in the app's menu, and Esc. Its strings are made when what they say changes,
/// so a frame that changes nothing allocates nothing.</summary>
public sealed partial class MapScene : ISceneMenu
{
    /// <summary>Opens the app's menu (the top bar's menu button; Esc opens it through the switcher).</summary>
    public Action? OpenMenu { get; init; }

    /// <summary>Opens the designer (the top bar's menu).</summary>
    public Action? OpenDesigner { get; init; }

    /// <summary>The next scenario's briefing is shown (-brief); a load from the menu always shows it.</summary>
    public bool BriefOnce { get; set; }

    float topBarH;   // the top bar's height, UI pixels: the HUD's windows start under it
    bool pausedForMenu, pausedForBrief, briefOpen, briefShut;

    /// <summary>Where the HUD's windows start: under the top bar.</summary>
    float HudTop => ImGui.GetMainViewport().Pos.Y + topBarH + 8 * UiFonts.Scale;

    static readonly Vector4 BarBg = new(0.043f, 0.059f, 0.071f, 0.94f), Friend = new(0.357f, 0.608f, 0.878f, 1),
        Bad = new(0.878f, 0.416f, 0.353f, 1);

    // ------------------------------------------------------------------ the top bar

    // the bar's strings, made when what they say changes
    long clockSecond = long.MinValue;
    DateTime clockDay;
    string clockText = "", dayText = "";
    (double Asked, double Held, bool Paused) heldKey = (-1, -1, false);
    string heldText = "";
    int actionMinutes = -1;
    string actionText = "";
    int sideGeneration = -1, sidePlayer = int.MinValue;
    string sideText = "", scenarioText = "", aimText = "";
    int newsShown = -1;
    string newsText = "";

    void TopBar(Snapshot s)
    {
        var vp = ImGui.GetMainViewport();
        float sc = UiFonts.Scale;
        topBarH = 30 * sc;
        ImGui.SetNextWindowPos(vp.Pos);
        ImGui.SetNextWindowSize(new Vector2(vp.Size.X, topBarH));
        ImGui.PushStyleColor(ImGuiCol.WindowBg, BarBg);
        ImGui.PushStyleVar(ImGuiStyleVar.WindowPadding, new Vector2(10 * sc, 0));
        ImGui.PushStyleVar(ImGuiStyleVar.WindowRounding, 0);
        bool open = ImGui.Begin("##topbar", ImGuiWindowFlags.NoDecoration | ImGuiWindowFlags.NoSavedSettings | ImGuiWindowFlags.NoMove |
                                            ImGuiWindowFlags.NoNav | ImGuiWindowFlags.NoFocusOnAppearing | ImGuiWindowFlags.NoBringToFrontOnFocus);
        ImGui.PopStyleVar(2);
        ImGui.PopStyleColor();
        if (!open)
        {
            ImGui.End();
            return;
        }
        BarTexts(s);
        float mid = topBarH / 2, gap = 16 * sc;

        // the side and the scenario
        ImGui.PushFont(UiFonts.SansBold);
        BarText(sideText, DesignerTheme.Bright, mid);
        ImGui.PopFont();
        ImGui.SameLine();
        BarText(scenarioText, DesignerTheme.Muted, mid);

        // the date and the clock
        ImGui.SameLine(0, gap);
        BarText(dayText, DesignerTheme.Muted, mid);
        ImGui.SameLine();
        ImGui.PushFont(UiFonts.Mono);
        BarText(clockText, DesignerTheme.Bright, mid);
        ImGui.PopFont();

        // pause, the speeds, what's held
        ImGui.SameLine(0, gap);
        PauseButton(s, mid);
        SpeedButtons(s, mid);
        ImGui.SameLine(0, 10 * sc);
        if (s.Paused)
            BarText("PAUSED", DesignerTheme.Amber, mid);
        else
            BarText(heldText, s.AchievedSpeed < s.Speed * 0.9 ? DesignerTheme.Amber : DesignerTheme.Muted, mid);
        if (GunsSilent(s))
        {
            ImGui.SameLine(0, gap);
            BarText("guns silent at this speed", DesignerTheme.Amber, mid);
            if (ImGui.IsItemHovered())
                ImGui.SetTooltip($"no fire in the coarse tier ({world.Settings.CoarseMinSpeed:0}x and up) until coarse fire (4.9)");
        }

        // in action, the news, a stopped sim
        if (actionText.Length > 0)
        {
            ImGui.SameLine(0, gap * 2);
            ImGui.SetCursorPosY(mid);
            var p = ImGui.GetCursorScreenPos();
            float r = 3.5f * sc, cy = p.Y;
            float pulse = 0.55f + 0.45f * MathF.Cos((float)ImGui.GetTime() * 4);
            ImGui.GetWindowDrawList().AddQuadFilled(new Vector2(p.X + r, cy - r), new Vector2(p.X + 2 * r, cy), new Vector2(p.X + r, cy + r),
                new Vector2(p.X, cy), ImGui.GetColorU32(DesignerTheme.Amber with { W = pulse }));
            ImGui.Dummy(new Vector2(2 * r + 4 * sc, 1));
            ImGui.SameLine(0, 0);
            BarText(actionText, DesignerTheme.Amber, mid);
        }
        if (newsText.Length > 0)
        {
            ImGui.SameLine(0, gap);
            BarText(newsText, DesignerTheme.Amber, mid);
        }
        if (s.Error != null)
        {
            ImGui.SameLine(0, gap);
            BarText("the sim stopped", Bad, mid);
            if (ImGui.IsItemHovered())
                ImGui.SetTooltip(s.Error);
        }

        // at the right: the aim and the end, the menu
        float menuW = 26 * sc, aimW = aimText.Length > 0 ? ImGui.CalcTextSize(aimText).X + gap : 0;
        float right = ImGui.GetWindowWidth() - 10 * sc - menuW - aimW;
        ImGui.SameLine(0, gap);
        if (ImGui.GetCursorPosX() < right)
            ImGui.SetCursorPosX(right);
        if (aimText.Length > 0)
        {
            BarText(aimText, DesignerTheme.Brass, mid);
            if (ImGui.IsItemHovered())
                ImGui.SetTooltip("the aim, and when the scenario ends; the briefing is in the menu");
            ImGui.SameLine(0, gap);
        }
        MenuButton(mid, menuW);
        ImGui.End();
    }

    /// <summary>A piece of the bar's text, centred on the bar whatever the line's baseline (the speed buttons' padding
    /// would push ImGui's text down): drawn, then a dummy of its size for the layout and its tooltip.</summary>
    static void BarText(string text, Vector4 colour, float mid)
    {
        var size = ImGui.CalcTextSize(text);
        ImGui.SetCursorPosY(mid - size.Y / 2);
        ImGui.GetWindowDrawList().AddText(ImGui.GetCursorScreenPos(), ImGui.GetColorU32(colour), text);
        ImGui.Dummy(size);
    }

    /// <summary>Remakes the bar's strings whose subject changed.</summary>
    void BarTexts(Snapshot s)
    {
        long second = s.Time.Ms / 1000;
        if (second != clockSecond)
        {
            clockSecond = second;
            var utc = s.Time.ToUtc();
            clockText = utc.ToString("HH:mm:ss", CultureInfo.InvariantCulture);
            if (utc.Date != clockDay)
            {
                clockDay = utc.Date;
                dayText = utc.ToString("ddd d MMM yyyy", CultureInfo.InvariantCulture);
            }
        }
        // what's held: the speed asked while the sim keeps up, else what it makes (made when the words change)
        bool lagging = s.AchievedSpeed < s.Speed * 0.9;
        var held = (s.Speed, lagging ? Math.Round(s.AchievedSpeed) : -1, s.Paused);
        if (held != heldKey)
        {
            heldKey = held;
            heldText = lagging ? $"held {SpeedLabel(held.Item2)} of {SpeedLabel(s.Speed)}" : $"held {SpeedLabel(s.Speed)}";
        }
        // in action: since the oldest open encounter of ours
        long opened = long.MaxValue;
        var e = s.Encounters;
        for (int i = 0; i < e.Count; i++)
            if (e.Own[i])
                opened = Math.Min(opened, e.OpenedMs[i]);
        int minutes = opened == long.MaxValue ? -1 : (int)((s.Time.Ms - opened) / 60_000);
        if (minutes != actionMinutes)
        {
            actionMinutes = minutes;
            actionText = minutes < 0 ? "" : $"In action {minutes} min";
        }
        if (world.Generation != sideGeneration || s.PlayerSide != sidePlayer)
        {
            (sideGeneration, sidePlayer) = (world.Generation, s.PlayerSide);
            var sc = world.Scenario;
            sideText = s.PlayerSide < 0 ? "WATCHING" : (sc != null && s.PlayerSide < sc.Sides.Length ? sc.Sides[s.PlayerSide] : $"SIDE {s.PlayerSide}").ToUpperInvariant();
            scenarioText = sc != null ? $"· {sc.Name}" : "· the dev world (F1)";
            aimText = sc == null || s.PlayerSide < 0 ? "" : $"{AimsText(sc, s.PlayerSide)}{(sc.Until is { } u ? $" · ends {u.ToUtc():HH:mm}" : "")}";
        }
        // the newest encounter's news, while fresh or the clock is paused at it
        var v = s.Encounters;
        if (v.NewsId != newsSeen)
            (newsSeen, newsSeenAt) = (v.NewsId, System.Diagnostics.Stopwatch.GetTimestamp());
        bool pausedAtIt = s.Paused && s.Time.Ms - v.NewsMs <= 1;
        int shown = v.NewsId != 0 && (pausedAtIt || System.Diagnostics.Stopwatch.GetElapsedTime(newsSeenAt).TotalSeconds < NewsShownSeconds)
            ? v.NewsId * 2 + (pausedAtIt ? 1 : 0) : 0;
        if (shown != newsShown)
        {
            newsShown = shown;
            newsText = shown == 0 ? "" : (v.NewsKind == InterestKind.Own ? "Contact: our ships meet the enemy" : "Others in contact") +
                                         (pausedAtIt ? " (paused)" : "");
        }
    }

    static string AimsText(Scenario sc, int side) => string.Join(", ", sc.AimsOf(side).Select(a => a.Kind switch
    {
        AimKind.DestroyEveryone => "Destroy everyone",
        _ => a.Kind.ToString(),
    }));

    /// <summary>Pause and run: two bars or a triangle, drawn.</summary>
    void PauseButton(Snapshot s, float mid)
    {
        float sc = UiFonts.Scale, w = 26 * sc, h = 20 * sc;
        ImGui.SetCursorPosY(mid - h / 2);
        var p = ImGui.GetCursorScreenPos();
        if (ImGui.InvisibleButton("##pause", new Vector2(w, h)))
            world.Send(s.Paused ? Command.Resume() : Command.Pause());
        if (ImGui.IsItemHovered())
            ImGui.SetTooltip(s.Paused ? "run (space)" : "pause (space)");
        var dl = ImGui.GetWindowDrawList();
        dl.AddRectFilled(p, p + new Vector2(w, h), ImGui.GetColorU32(s.Paused ? new Vector4(0.35f, 0.17f, 0.08f, 1) :
            ImGui.IsItemHovered() ? new Vector4(1, 1, 1, 0.1f) : new Vector4(1, 1, 1, 0.05f)), 2 * sc);
        uint ink = ImGui.GetColorU32(s.Paused ? DesignerTheme.Amber : DesignerTheme.Text);
        var c = p + new Vector2(w, h) / 2;
        if (s.Paused)
            dl.AddTriangleFilled(c + new Vector2(-4, -5) * sc, c + new Vector2(5, 0) * sc, c + new Vector2(-4, 5) * sc, ink);
        else
        {
            dl.AddRectFilled(c + new Vector2(-4, -5) * sc, c + new Vector2(-1.5f, 5) * sc, ink);
            dl.AddRectFilled(c + new Vector2(1.5f, -5) * sc, c + new Vector2(4, 5) * sc, ink);
        }
    }

    /// <summary>The speeds as buttons: the one asked in brass, the coarse tier's dimmer (no fire there yet); a click
    /// runs a paused clock at it.</summary>
    void SpeedButtons(Snapshot s, float mid)
    {
        float sc = UiFonts.Scale, h = 20 * sc;
        ImGui.PushFont(UiFonts.Mono);
        ImGui.PushStyleVar(ImGuiStyleVar.FramePadding, new Vector2(4 * sc, (h - ImGui.GetTextLineHeight()) / 2));
        ImGui.PushStyleVar(ImGuiStyleVar.ItemSpacing, new Vector2(1 * sc, 0));
        ImGui.PushStyleVar(ImGuiStyleVar.FrameRounding, 2 * sc);
        for (int i = 0; i < Speeds.Length; i++)
        {
            ImGui.SameLine(0, i == 0 ? 2 * sc : Speeds[i] >= world.Settings.CoarseMinSpeed && Speeds[i - 1] < world.Settings.CoarseMinSpeed ? 7 * sc : 1 * sc);
            ImGui.SetCursorPosY(mid - h / 2);
            bool asked = Math.Abs(Speeds[i] - s.Speed) < 1e-6, coarse = Speeds[i] >= world.Settings.CoarseMinSpeed;
            ImGui.PushStyleColor(ImGuiCol.Button, asked ? DesignerTheme.BrassDarker : new Vector4(1, 1, 1, 0.04f));
            ImGui.PushStyleColor(ImGuiCol.ButtonHovered, asked ? DesignerTheme.BrassDark : new Vector4(1, 1, 1, 0.1f));
            ImGui.PushStyleColor(ImGuiCol.Text, asked ? DesignerTheme.Brass : coarse ? DesignerTheme.Muted with { W = 0.6f } : DesignerTheme.Muted);
            if (ImGui.Button(SpeedLabels[i]))
            {
                world.Send(Command.SetSpeed(Speeds[i]));
                if (s.Paused)
                    world.Send(Command.Resume());
            }
            ImGui.PopStyleColor(3);
            if (ImGui.IsItemHovered())
            {
                ImGui.BeginTooltip();
                ImGui.TextUnformatted(SpeedSpans[i]);
                if (coarse)
                    ImGui.TextDisabled("coarse tier: ships off rails in 1-minute steps, guns silent");
                ImGui.EndTooltip();
            }
        }
        if (ImGui.IsWindowHovered() && ImGui.GetIO().MouseWheel != 0 && ImGui.GetMousePos().X < ImGui.GetItemRectMax().X)
            world.Send(Command.SetSpeed(StepSpeed(s.Speed, Math.Sign(ImGui.GetIO().MouseWheel))));
        ImGui.PopStyleVar(3);
        ImGui.PopFont();
    }

    /// <summary>The menu button at the right end: the designer, and the app's menu.</summary>
    void MenuButton(float mid, float w)
    {
        float sc = UiFonts.Scale, h = 22 * sc;
        ImGui.SetCursorPosY(mid - h / 2);
        var p = ImGui.GetCursorScreenPos();
        if (ImGui.InvisibleButton("##menu", new Vector2(w, h)))
            ImGui.OpenPopup("##mapmenu");
        var dl = ImGui.GetWindowDrawList();
        dl.AddRect(p, p + new Vector2(w, h), ImGui.GetColorU32(ImGui.IsItemHovered() ? DesignerTheme.Muted : DesignerTheme.Bevel), 2 * sc);
        uint ink = ImGui.GetColorU32(DesignerTheme.Text);
        for (int i = -1; i <= 1; i++)
            dl.AddLine(p + new Vector2(w * 0.28f, h / 2 + i * 4 * sc), p + new Vector2(w * 0.72f, h / 2 + i * 4 * sc), ink, 1.2f * sc);
        if (ImGui.BeginPopup("##mapmenu"))
        {
            if (ImGui.MenuItem("Ship designer", "F3"))
                OpenDesigner?.Invoke();
            if (ImGui.MenuItem("Menu", "Esc"))
                OpenMenu?.Invoke();
            ImGui.Separator();
            if (ImGui.MenuItem("Dev panel", "F1", devOpen))
                devOpen = !devOpen;
            ImGui.EndPopup();
        }
    }

    // ------------------------------------------------------------------ the briefing

    // what the briefing says, made when it opens
    string briefTitle = "", briefMeta = "", briefText = "", briefAim = "";
    readonly List<(string Label, string Value)> briefOrders = [];
    readonly List<(string Unit, string Ships)> briefOwn = [], briefEnemy = [];

    /// <summary>Opens the briefing of the scenario now loaded, pausing the clock under it.</summary>
    void OpenBrief()
    {
        var sc = world.Scenario;
        if (sc == null)
            return;
        var s = world.Current;
        int side = s.PlayerSide >= 0 ? s.PlayerSide : Math.Max(sc.Player, 0);
        var start = sc.Start.ToUtc();
        briefTitle = sc.Name;
        briefMeta = start.ToString("dddd d MMMM yyyy, HH:mm 'GMT'", CultureInfo.InvariantCulture);
        if (sc.Camera is { } c)
            briefMeta += $" · {Math.Abs(c.Lat):0.0}°{(c.Lat >= 0 ? 'N' : 'S')} {Math.Abs(c.Lon):0.0}°{(c.Lon >= 0 ? 'E' : 'W')}";
        if (sc.Weather is { } w)
            briefMeta += $" · visibility {w.VisibilityM / 1000:0} km, wind from {w.WindFromDeg:000}° at {w.WindMs / 0.514444:0} kn";
        briefText = sc.Brief;
        briefAim = $"{AimsText(sc, side)}. {(sc.Until is { } u ? $"The scenario ends when one side is gone or out of the fight, or at {u.ToUtc():HH:mm}." : "The scenario ends when one side is gone or out of the fight.")}";
        briefOrders.Clear();
        void Order(string label, List<(int Side, string Name)> list)
        {
            foreach (var (sd, name) in list)
                if (sd == side)
                    briefOrders.Add((label, Pretty(name)));
        }
        Order("Readiness", sc.Readiness);
        Order("Damage control", sc.DamageControl);
        Order("Fire discipline", sc.FireDisciplines);
        briefOwn.Clear();
        briefEnemy.Clear();
        foreach (var unit in sc.Units)
            if (unit.Side == side)
                briefOwn.Add((unit.Name, string.Join(", ", ShipsOf(unit))));
            else
                briefEnemy.Add((unit.Name, $"{unit.ShipCount} ship{(unit.ShipCount == 1 ? "" : "s")}"));
        if (!s.Paused)
        {
            world.Send(Command.Pause());
            pausedForBrief = true;
        }
        briefOpen = true;
    }

    /// <summary>A preset's id as a reader would write it: "rn-1914" RN 1914, "closed up" Closed up.</summary>
    static string Pretty(string id) => string.Join(' ', id.Split('-', ' ').Select((t, i) => i > 0 || t.Length == 0 ? t
        : t.Length <= 3 && t.All(char.IsLetter) ? t.ToUpperInvariant() : char.ToUpperInvariant(t[0]) + t[1..]));

    /// <summary>A unit's ships by name, else their designs with a count.</summary>
    static IEnumerable<string> ShipsOf(ScenarioUnit unit)
    {
        foreach (var sub in unit.Units)
            foreach (var n in ShipsOf(sub))
                yield return n;
        foreach (var ships in unit.Ships)
            if (ships.Names.Length > 0)
                foreach (var n in ships.Names)
                    yield return n;
            else
                yield return ships.Count == 1 ? ships.Design : $"{ships.Count} × {ships.Design}";
    }

    void Briefing()
    {
        const string Id = "##briefing";
        if (!briefOpen)
            return;
        if (!ImGui.IsPopupOpen(Id))
            ImGui.OpenPopup(Id);
        float sc = UiFonts.Scale, w = 900 * sc;
        var vp = ImGui.GetMainViewport();
        ImGui.SetNextWindowPos(vp.GetCenter(), ImGuiCond.Always, new Vector2(0.5f, 0.5f));
        ImGui.SetNextWindowSize(new Vector2(w, 0));
        ImGui.PushStyleVar(ImGuiStyleVar.WindowPadding, new Vector2(22, 16) * sc);
        bool open = ImGui.BeginPopupModal(Id, ImGuiWindowFlags.NoDecoration | ImGuiWindowFlags.AlwaysAutoResize | ImGuiWindowFlags.NoSavedSettings);
        ImGui.PopStyleVar();
        if (!open)
            return;
        bool begin = false, paused = false;
        ImGui.TextColored(DesignerTheme.Brass, "SCENARIO");
        ImGui.PushFont(UiFonts.PosterFigure);
        ImGui.TextColored(DesignerTheme.Bright, briefTitle);
        ImGui.PopFont();
        ImGui.TextDisabled(briefMeta);
        ImGui.Spacing();
        ImGui.Separator();
        ImGui.Spacing();
        float colW = (w - 44 * sc) / 2;
        if (ImGui.BeginTable("##briefcols", 2, ImGuiTableFlags.BordersInnerV | ImGuiTableFlags.SizingFixedFit, new Vector2(w - 44 * sc, 0)))
        {
            ImGui.TableSetupColumn("##l", ImGuiTableColumnFlags.WidthFixed, colW - 14 * sc);
            ImGui.TableSetupColumn("##r", ImGuiTableColumnFlags.WidthFixed, colW - 14 * sc);
            ImGui.TableNextColumn();
            float x0 = ImGui.GetCursorPosX();
            ImGui.PushTextWrapPos(x0 + colW - 30 * sc);
            Heading("THE SITUATION");
            ImGui.TextWrapped(briefText.Length > 0 ? briefText : "No brief.");
            ImGui.Spacing();
            Heading("YOUR AIM");
            ImGui.TextWrapped(briefAim);
            if (briefOrders.Count > 0)
            {
                ImGui.Spacing();
                Heading("YOUR SIDE'S ORDERS BEFORE ACTION");
                foreach (var (label, value) in briefOrders)
                {
                    ImGui.TextDisabled(label);
                    ImGui.SameLine(x0 + 130 * sc);
                    ImGui.TextColored(DesignerTheme.Bright, value);
                }
            }
            ImGui.PopTextWrapPos();
            ImGui.TableNextColumn();
            ImGui.Indent(10 * sc);
            ImGui.PushTextWrapPos(ImGui.GetCursorPosX() + colW - 30 * sc);
            Heading("YOURS");
            foreach (var (unit, ships) in briefOwn)
            {
                ImGui.TextColored(Friend, unit);
                ImGui.TextWrapped(ships);
            }
            ImGui.Spacing();
            Heading("THE ENEMY, AS REPORTED");
            foreach (var (unit, ships) in briefEnemy)
            {
                ImGui.TextColored(Bad, unit);
                ImGui.SameLine();
                ImGui.TextDisabled(ships);
            }
            ImGui.PopTextWrapPos();
            ImGui.Unindent(10 * sc);
            ImGui.EndTable();
        }
        ImGui.Spacing();
        ImGui.Separator();
        ImGui.Spacing();
        float bw = 130 * sc;
        ImGui.SetCursorPosX(ImGui.GetWindowWidth() - 22 * sc - 2 * bw - ImGui.GetStyle().ItemSpacing.X);
        if (ImGui.Button("Begin paused", new Vector2(bw, 26 * sc)))
            (begin, paused) = (true, true);
        ImGui.SameLine();
        ImGui.PushStyleColor(ImGuiCol.Button, DesignerTheme.Brass);
        ImGui.PushStyleColor(ImGuiCol.ButtonHovered, DesignerTheme.Brass with { W = 0.85f });
        ImGui.PushStyleColor(ImGuiCol.Text, DesignerTheme.Ground);
        if (ImGui.Button("Begin", new Vector2(bw, 26 * sc)))
            begin = true;
        ImGui.PopStyleColor(3);
        if (begin || briefShut)
        {
            briefShut = false;
            briefOpen = false;
            if (pausedForBrief && !paused)
                world.Send(Command.Resume());
            pausedForBrief = false;
            ImGui.CloseCurrentPopup();
        }
        ImGui.EndPopup();

        static void Heading(string text)
        {
            ImGui.PushFont(UiFonts.SansSmall);
            ImGui.TextDisabled(text);
            ImGui.PopFont();
        }
    }

    // ------------------------------------------------------------------ the scale, bottom right

    long whereKey = long.MinValue;
    string whereText = "";

    /// <summary>The scale bar and the centre's position, small, at the bottom right; with shift held and a ship selected,
    /// a waypoint on land is refused here. The band, the view and the sun's time are the dev panel's.</summary>
    void Where()
    {
        var vp = ImGui.GetMainViewport();
        float pad = 8 * UiFonts.Scale;
        ImGui.SetNextWindowPos(new Vector2(vp.Pos.X + vp.Size.X - pad, vp.Pos.Y + vp.Size.Y - pad), ImGuiCond.Always, new Vector2(1, 1));
        ImGui.SetNextWindowBgAlpha(0.45f);
        if (ImGui.Begin("##where", ImGuiWindowFlags.NoDecoration | ImGuiWindowFlags.AlwaysAutoResize | ImGuiWindowFlags.NoSavedSettings |
                                   ImGuiWindowFlags.NoFocusOnAppearing | ImGuiWindowFlags.NoNav | ImGuiWindowFlags.NoMove | ImGuiWindowFlags.NoInputs))
        {
            ImGui.PushFont(UiFonts.Mono);
            ScaleBar(camera.MetresPerPixel * width / Math.Max(vp.Size.X, 1));
            ImGui.SameLine(0, 12 * UiFonts.Scale);
            var (lat, lon) = Earth.LatLon(camera.Target);
            long key = (long)Math.Round(lat * 600) * 1_000_000 + (long)Math.Round(lon * 600);
            if (key != whereKey)
            {
                whereKey = key;
                whereText = $"{Math.Abs(lat):0}°{Math.Abs(lat) % 1 * 60:00}'{(lat >= 0 ? 'N' : 'S')} {Math.Abs(lon):0}°{Math.Abs(lon) % 1 * 60:00}'{(lon >= 0 ? 'E' : 'W')}";
            }
            ImGui.TextDisabled(whereText);
            ImGui.PopFont();
        }
        ImGui.End();
        if (mouseIn && ImGui.GetIO().KeyShift && selected >= 0 && !ImGui.GetIO().WantCaptureMouse
            && View.Pick(mouse.X * width / Math.Max(vp.Size.X, 1), mouse.Y * height / Math.Max(vp.Size.Y, 1), out var p) && world.Shore.IsLand(p))
            ImGui.SetTooltip("land: no waypoint here");
    }

    /// <summary>The dev panel's line on where the map looks: the band, the world view, the scale, the grid, the centre and
    /// the sun's local time, the cursor; with the stats (F2) the world view's boundary.</summary>
    void DevWhere(Snapshot s)
    {
        var vp = ImGui.GetMainViewport();
        var view = View;
        double across = camera.MetresPerPixel * width, wf = WorldView.Factor(view);
        ImGui.TextDisabled($"{Band(across)} · {WorldView.Label(wf)} · {Distance(across)} across · {Scale(camera.MetresPerPixel)} · grid {Graticule.Label(Graticule.Spacing(camera.MetresPerPixel))}{(wf >= 1 ? " at the edges" : "")}");
        if (Sdl3GpuEngine.StatsShown)
        {
            float b = (float)WorldView.BoundaryM;
            ImGui.SetNextItemWidth(160 * UiFonts.Scale);
            if (ImGui.SliderFloat("world below, m/px at 1080p", ref b, 0.5f, 25f, "%.2f", ImGuiSliderFlags.Logarithmic))
                WorldView.BoundaryM = b;
        }
        var (lat, lon) = Earth.LatLon(camera.Target);
        if (sunLapse.Motion < 0.5)   // the hours would only churn
            ImGui.TextDisabled($"centre {LatLon(lat, lon)}, time-lapse sun, {s.AchievedSpeed / 86_400:0.#} days/s");
        else
        {
            var (_, sunLon) = Earth.LatLon(s.Sun);
            double solar = ((12 + (lon - sunLon) / 15) % 24 + 24) % 24;
            ImGui.TextDisabled($"centre {LatLon(lat, lon)}, local sun time {(int)solar:00}:{(int)(solar % 1 * 60):00}");
        }
        if (mouseIn && view.Pick(mouse.X * width / Math.Max(vp.Size.X, 1), mouse.Y * height / Math.Max(vp.Size.Y, 1), out var p))
        {
            var (la, lo) = Earth.LatLon(p);
            ImGui.TextDisabled($"cursor {LatLon(la, lo)}{(world.Shore.IsLand(p) ? ", land" : "")}");
        }
    }

    // ------------------------------------------------------------------ the app's menu, and Esc

    /// <summary>The map's items in the app's menu: the briefing, restart, the scenarios, the clock's rules on contact.</summary>
    public bool MenuItems()
    {
        var full = new Vector2(-1, 0);
        bool shut = false;
        var sc = world.Scenario;
        if (sc != null)
        {
            if (ImGui.Button("Briefing", full))
            {
                shut = true;
                OpenBrief();
            }
            if (ImGui.Button($"Restart {sc.Name}", full))
            {
                shut = true;
                BriefOnce = true;
                world.Load(sc);
            }
        }
        ImGui.Spacing();
        ImGui.TextDisabled("Scenarios");
        scenarioPaths ??= WorldSession.ShippedScenarios();
        if (world.Loading is { } l)
            ImGui.TextDisabled($"loading {l.Name}: classes {l.Ready} of {l.Of}...");
        else
            foreach (var path in scenarioPaths)
                if (ImGui.Selectable(Path.GetFileNameWithoutExtension(path)))
                {
                    shut = true;
                    BriefOnce = true;
                    Load(path);
                }
        if (world.LoadError is { } e)
            ImGui.TextColored(Bad, e);
        var s = world.Current;
        if (s.PlayerSide >= 0)
        {
            ImGui.Spacing();
            ImGui.TextDisabled("The clock");
            ImGui.TextUnformatted("When our ships meet the enemy");
            ImGui.SameLine(200 * UiFonts.Scale);
            ImGui.Dummy(Vector2.Zero);   // the rule's combo goes on the same line after it
            Rule(s, InterestKind.Own, "##own");
            ImGui.TextUnformatted("When others meet");
            ImGui.SameLine(200 * UiFonts.Scale);
            ImGui.Dummy(Vector2.Zero);
            Rule(s, InterestKind.Others, "##others");
        }
        return shut;
    }

    /// <summary>The menu over the map stops the clock while it's up, and runs it again if it was running.</summary>
    public void MenuShown(bool shown)
    {
        var s = world.Current;
        if (shown && !s.Paused)
        {
            world.Send(Command.Pause());
            pausedForMenu = true;
        }
        else if (!shown && pausedForMenu)
        {
            world.Send(Command.Resume());
            pausedForMenu = false;
        }
    }

    /// <summary>Esc on the map: the topmost thing shuts (the briefing, an armed order, a placement, the selection, the
    /// dev panel); false with nothing open, and the switcher opens the menu.</summary>
    bool Escape()
    {
        if (briefOpen)
            briefShut = true;
        else if (armedPick != ClickPick.None || fireArmed)
            (armedPick, fireArmed) = (ClickPick.None, false);
        else if (placing || placeDrag)
            (placing, placeDrag) = (false, false);
        else if (selected >= 0)
        {
            selected = -1;
            picked.Clear();
        }
        else if (devOpen)
            devOpen = false;
        else
            return false;
        return true;
    }
}
