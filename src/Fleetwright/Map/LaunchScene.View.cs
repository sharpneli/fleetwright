using System.Numerics;
using Fleetwright.Sim.Core;
using ImGuiNET;
using static Fleetwright.Ui.Pieces;

namespace Fleetwright.Map;

/// <summary>The opening's camera and mouse, in the battle map's grammar (docs/map-ui/input.md: controls unified):
/// the wheel zooms at the cursor, WASD pans (<see cref="PanKeys"/>, the map's own), the middle button drags and its
/// click centres there; the left button selects (a unit's mark: its ships in the order of battle); the right acts.
/// Placing a unit is an armed action, so either button places her (its press her head, its drag her course). The player's view is kept as a centre and a scale over
/// rebuilds; a new pick, or placing (framed with room round the units), frames it again.</summary>
public sealed unsafe partial class LaunchScene
{
    readonly PanKeys panKeys = new();
    (Vec3d Centre, double Mpp)? openView;   // the player's view of the opening, or null: framed to the units

    // the opening's press this frame: which button (0 left, 1 right, 2 middle; -1 none), where, whether it moved a
    // click's worth; and the placing button's (left or right while placing)
    int openPress = -1;
    Vector2 openPressAt;
    bool openMoved, openHovered, placePressed, placeHeld, placeReleased;

    const double OpeningMaxMpp = 5000;   // the farthest out: the gnomonic plane stays a fair chart

    /// <summary>The opening's input, after it's drawn: one invisible button over it (under the placing bar) for all
    /// three buttons, the wheel, the keys' pan. A view change frames it again next frame.</summary>
    void OpeningInput(OpeningView v)
    {
        var io = ImGui.GetIO();
        float top = placing != null ? RowH + P(4) : 0;   // the placing bar's
        ImGui.SetCursorScreenPos(v.At + new Vector2(0, top));
        ImGui.InvisibleButton("##opening", v.Size - new Vector2(0, top),
            ImGuiButtonFlags.MouseButtonLeft | ImGuiButtonFlags.MouseButtonRight | ImGuiButtonFlags.MouseButtonMiddle);
        openHovered = ImGui.IsItemHovered();
        bool active = ImGui.IsItemActive(), released = ImGui.IsItemDeactivated();
        var mouse = io.MousePos;
        if (ImGui.IsItemActivated())
        {
            openPress = ImGui.IsMouseClicked(ImGuiMouseButton.Middle) ? 2 : ImGui.IsMouseClicked(ImGuiMouseButton.Right) ? 1 : 0;
            (openPressAt, openMoved) = (mouse, false);
        }
        if (active && Vector2.Distance(mouse, openPressAt) > P(6))
            openMoved = true;
        bool places = placing != null && openPress is 0 or 1;   // an armed action takes the left too (docs/map-ui/input.md I4)
        (placePressed, placeHeld, placeReleased) = (places && ImGui.IsItemActivated(), places && active, places && released);

        if (openHovered && io.MouseWheel != 0)
            Zoom(v, mouse, Math.Pow(1.2, -io.MouseWheel));
        if (active && openPress == 2 && io.MouseDelta != Vector2.Zero)
            Pan(v, -io.MouseDelta.X, -io.MouseDelta.Y);
        if (io.WantTextInput)
            panKeys.Release();
        var (dx, dy) = panKeys.Step(io.DeltaTime, v.Size.Y, io.KeyShift);
        if (dx != 0 || dy != 0)
            Pan(v, dx, dy);
        if (released && !openMoved)
        {
            if (openPress == 2)
                Centre(v, mouse);
            else if (openPress == 0 && placing == null)
                SelectAt(v, mouse);
        }
        if (released)
            openPress = -1;
    }

    /// <summary>The view moved by (dx, dy) pixels (x right, y down).</summary>
    void Pan(OpeningView v, double dx, double dy)
    {
        var c = v.At + v.Size / 2;
        openView = (v.From(c + new Vector2((float)dx, (float)dy)), v.Mpp);
        opening = null;
    }

    /// <summary>The scale times <paramref name="factor"/> (above 1 out), the point under the cursor kept there.</summary>
    void Zoom(OpeningView v, Vector2 mouse, double factor)
    {
        double mpp = Math.Clamp(v.Mpp * factor, 15 / UiFonts.Scale, OpeningMaxMpp);
        var c = v.At + v.Size / 2;
        double ox = mouse.X - c.X, oy = mouse.Y - c.Y;
        double px = v.Mx + ox * v.Mpp, py = v.My - oy * v.Mpp;   // the plane's point under the cursor
        openView = (v.OnPlane(px - ox * mpp, py + oy * mpp), mpp);
        opening = null;
    }

    void Centre(OpeningView v, Vector2 mouse)
    {
        openView = (v.From(mouse), v.Mpp);
        opening = null;
    }

    /// <summary>A left click on a unit's mark: the order of battle shows her side and selects her first design.</summary>
    void SelectAt(OpeningView v, Vector2 mouse)
    {
        int best = -1;
        float bestD = P(12);
        for (int i = 0; i < v.Units.Count; i++)
            if (Vector2.Distance(v.Units[i].Head, mouse) is var d && d < bestD)
                (best, bestD) = (i, d);
        if (best < 0)
            return;
        var unit = v.Placed[best].Unit;
        int tabAt = shipTabs.FindIndex(t => t.Side == unit.Side);
        if (tabAt < 0 || unit.Side >= linesBySide.Length)
            return;
        shipTab = tabAt;
        bool inside = false;
        foreach (var l in linesBySide[unit.Side])
        {
            if (l.IsUnit && l.Top)
            {
                if (inside)
                    break;
                inside = l.Unit.Name == unit.Name;
                continue;
            }
            if (inside && !l.IsUnit)
            {
                selKey = (l.Side, l.Unit.Name, l.Design);
                return;
            }
        }
    }
}
