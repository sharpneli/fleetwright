using System.Numerics;
using Fleetwright.Ui;
using ImGuiNET;

namespace Fleetwright.Map;

/// <summary>The controls (the app's menu, Controls…): every way the map is worked, on one page for players who haven't read
/// docs/map-ui/input.md. The camera, selecting, the right button, the clock and the windows are written out here; the
/// strip's orders are read from its key table (<see cref="Def"/>), so a key can't drift from its page either.</summary>
public sealed partial class MapScene
{
    bool controlsOpen, controlsShut;

    /// <summary>The map's own rows: the keys (each a chip; "Shift+" drawn as the shift mark) and what they do.</summary>
    static readonly (string Title, (string[] Keys, string What)[] Rows)[] MapControls =
    [
        ("THE CAMERA",
        [
            (["W", "A", "S", "D"], "pan (Shift: faster)"),
            (["Middle drag"], "pan, the point under the cursor held"),
            (["Middle click"], "on the sea: centre there; on a ship: follow her until you pan"),
            (["Wheel"], "zoom at the cursor"),
        ]),
        ("SELECTING",
        [
            (["Left click"], "a ship; twice: her squadron (the orders go to it); the empty sea: nothing"),
            (["Ctrl+click"], "add a ship to the selection, or take her out (in the rail too)"),
            (["Left drag"], "a box: the ships inside (a squadron wholly inside goes as a unit)"),
            (["Tab"], "your next ship; held: the fire lines"),
            (["Esc"], "shut the topmost thing; with nothing open, the menu"),
        ]),
        ("THE RIGHT BUTTON ACTS",
        [
            (["Right click"], "the order the cursor names: on an enemy engage, on one of ours follow, on the sea steer there"),
            (["Right drag"], "the same order, shaped: on the sea, arrive on this heading"),
            (["Shift+Right"], "add a waypoint"),
            (["Alt"], "with any order: to her alone, not her squadron"),
            (["Left click"], "with an order armed (Engage…, Follow…, a task's pick): aim it; a right click or Esc drops it"),
        ]),
        ("THE CLOCK",
        [
            (["Space"], "pause, run"),
            (["1", "2", "3", "4", "5"], "1×, 10×, 60×, 240×, 3,000× (a paused clock runs); the top bar has every speed"),
            (["+", "−"], "a speed up, down (and the mouse's side buttons)"),
            (["§"], "held: fast forward (the key left of 1), back on release"),
            (["Shift+Space"], "run until something matters: a contact, a hit, a torpedo track, a task done"),
            (["Ctrl+Space"], "a minute's step, then paused"),
        ]),
        ("WINDOWS",
        [
            (["V"], "her card; P pins it as a tab"),
            (["J"], "the whole log"),
            (["T"], "the task menu: every order by what it's about; its letters pick while it's open"),
            (["F3"], "the ship designer and the viewers"),
            (["F2"], "frame and sim figures"),
            (["Alt+Enter"], "fullscreen"),
        ]),
    ];

    /// <summary>The strip's orders, by its panes, as the table gives them.</summary>
    static readonly (string Title, Act[] Acts)[] StripControls =
    [
        ("ORDERS TO", [Act.LevelUp, Act.LevelDown, Act.BackIntoLine]),
        ("HELM", [Act.Port10, Act.Stbd10, Act.Port90, Act.Stbd90, Act.Course, Act.Faster, Act.Slower, Act.HardOver, Act.Conn, Act.ClearRoute, Act.Smoke]),
        ("GUNS", [Act.AtWill, Act.Engage, Act.HoldFire, Act.Shells, Act.Torpedoes, Act.StarShell, Act.Dark, Act.Beams]),
        ("TASK", [Act.Fight, Act.Follow, Act.Hold, Act.Tasks, Act.Withdraw, Act.Auto]),
        ("FORMATION", [Act.LineAhead, Act.Columns, Act.Abreast, Act.Turn, Act.Deploy, Act.DeployPort, Act.DeployStbd, Act.Formation]),
        ("AN ENEMY SELECTED", [Act.EngageUnit, Act.EngageShip, Act.TorpedoesAt, Act.StarShellOn]),
    ];

    void OpenControls() => controlsOpen = true;

    /// <summary>The page: a modal over the map (the clock stays as the menu left it), the map's controls at the left, the
    /// strip's orders at the right, scrolling if the window's short.</summary>
    void Controls()
    {
        const string Id = "##controls";
        if (!controlsOpen)
            return;
        if (!ImGui.IsPopupOpen(Id))
            ImGui.OpenPopup(Id);
        float sc = UiFonts.Scale, w = 1180 * sc;
        var vp = ImGui.GetMainViewport();
        ImGui.SetNextWindowPos(vp.GetCenter(), ImGuiCond.Always, new Vector2(0.5f, 0.5f));
        ImGui.SetNextWindowSize(new Vector2(w, Math.Min(vp.Size.Y - 60 * sc, 860 * sc)));
        ImGui.PushStyleVar(ImGuiStyleVar.WindowPadding, new Vector2(22, 16) * sc);
        bool open = ImGui.BeginPopupModal(Id, ImGuiWindowFlags.NoDecoration | ImGuiWindowFlags.NoSavedSettings);
        ImGui.PopStyleVar();
        if (!open)
            return;
        ImGui.TextColored(Theme.Brass, "CONTROLS");
        ImGui.SameLine();
        ImGui.TextDisabled("the left button selects, the right acts; every order is a button on the strip with its key");
        ImGui.Separator();
        ImGui.Spacing();
        float bodyH = ImGui.GetContentRegionAvail().Y - 40 * sc;
        if (ImGui.BeginChild("##controlsbody", new Vector2(0, bodyH)))
        {
            float colW = (ImGui.GetContentRegionAvail().X - 20 * sc) / 2;   // the two halves
            if (ImGui.BeginTable("##controlcols", 2, ImGuiTableFlags.BordersInnerV | ImGuiTableFlags.SizingFixedFit))
            {
                ImGui.TableSetupColumn("##l", ImGuiTableColumnFlags.WidthFixed, colW);
                ImGui.TableSetupColumn("##r", ImGuiTableColumnFlags.WidthFixed, colW);
                ImGui.TableNextColumn();
                foreach (var (title, rows) in MapControls)
                    if (Section(title))
                    {
                        foreach (var (keys, what) in rows)
                            ControlRow(keys, null, what);
                        ImGui.EndTable();
                    }
                ImGui.TableNextColumn();
                ImGui.Indent(10 * sc);
                foreach (var (title, acts) in StripControls)
                    if (Section(title))
                    {
                        foreach (var a in acts)
                        {
                            var d = Def(a);
                            ControlRow(d.Chip != null ? [d.Chip] : [], d.Label.Length > 0 ? d.Label : null, d.Tip);
                        }
                        ImGui.EndTable();
                    }
                ImGui.Unindent(10 * sc);
                ImGui.EndTable();
            }
        }
        ImGui.EndChild();
        ImGui.Separator();
        ImGui.Spacing();
        float bw = 130 * sc;
        ImGui.SetCursorPosX(ImGui.GetWindowWidth() - 22 * sc - bw);
        if (ImGui.Button("Close  (Esc)", new Vector2(bw, 26 * sc)) || controlsShut)
        {
            controlsShut = controlsOpen = false;
            ImGui.CloseCurrentPopup();
        }
        ImGui.EndPopup();

        // a section's heading, then its table: the keys, then what they do (wrapped in its cell)
        static bool Section(string text)
        {
            ImGui.Spacing();
            ImGui.PushFont(UiFonts.SansSmall);
            ImGui.TextColored(Theme.Brass, text);
            ImGui.PopFont();
            if (!ImGui.BeginTable("##" + text, 2, ImGuiTableFlags.SizingFixedFit))
                return false;
            ImGui.TableSetupColumn("##keys", ImGuiTableColumnFlags.WidthFixed, 150 * UiFonts.Scale);
            ImGui.TableSetupColumn("##what", ImGuiTableColumnFlags.WidthStretch);
            return true;
        }
    }

    /// <summary>A row of a section's table: its keys as chips (none: a button alone), then the order's name bright and what
    /// it does.</summary>
    static void ControlRow(string[] keys, string? label, string what)
    {
        float sc = UiFonts.Scale;
        ImGui.TableNextRow();
        ImGui.TableNextColumn();
        var dl = ImGui.GetWindowDrawList();
        var at = ImGui.GetCursorScreenPos();
        float x = at.X;
        if (keys.Length == 0)
            dl.Text(UiFonts.SansSmall, new Vector2(x, at.Y + 2 * sc), Theme.Muted, "button");
        foreach (string k in keys)
        {
            // a mouse's words are no key: written plain
            if (k.Contains(' ') || k.EndsWith("click", StringComparison.Ordinal) || k.Contains("Right", StringComparison.Ordinal) || k == "Wheel")
            {
                dl.Text(UiFonts.Sans, new Vector2(x, at.Y), Theme.Text, k);
                x += Pieces.Width(UiFonts.Sans, k) + 6 * sc;
            }
            else
                x += dl.Kbd(new Vector2(x, at.Y + 2 * sc), k) + 4 * sc;
        }
        ImGui.Dummy(new Vector2(1, ImGui.GetTextLineHeight()));
        ImGui.TableNextColumn();
        if (label != null)
        {
            ImGui.TextColored(Theme.Bright, label);
            ImGui.SameLine(0, 8 * sc);
        }
        ImGui.TextWrapped(what);
    }
}
