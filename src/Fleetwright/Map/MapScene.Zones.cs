using System.Numerics;
using Fleetwright.Sim;
using Fleetwright.Sim.Ai.Commanders;
using Fleetwright.Sim.Core;
using ImGuiNET;

namespace Fleetwright.Map;

/// <summary>The aims' zones on the map (docs/battle/aims.md A5): a Reach's zone a ring, a Sink's hunting ground a dashed
/// one, in its side's colour (yours, the enemy's; the brass watching), light as the map's marks are, named above its top.
/// Every side's are drawn: a side knows what the enemy's after (the scenario's intelligence).</summary>
public sealed partial class MapScene
{
    // the zones' names, made once a world; this frame's labels, target px
    readonly List<string> zoneNames = [];
    int zoneGeneration = -1;
    readonly List<(double X, double Y, int Name, Vector4 Colour)> zoneLabels = [];

    /// <summary>The zones' rings, inside the overlay's build.</summary>
    void DrawZones(in MapView view, Snapshot s)
    {
        zoneLabels.Clear();
        if (world.Scenario is not { } sc)
            return;
        if (zoneGeneration != world.Generation)
        {
            zoneGeneration = world.Generation;
            zoneNames.Clear();
            foreach (var a in sc.AimList)
                zoneNames.Add(AimWords.Where(a));
        }
        for (int i = 0; i < sc.AimList.Count && i < zoneNames.Count; i++)
        {
            var a = sc.AimList[i];
            if (a.Aim.Kind is not (AimKind.Reach or AimKind.Sink) || !a.Aim.Zone.Has)
                continue;
            var colour = (s.PlayerSide < 0 ? MapPalette.Brass : a.Side == s.PlayerSide ? MapPalette.Friend : MapPalette.Hostile) with { W = 0.55f };
            var centre = a.Aim.Zone.Centre;
            RingM(view, centre, a.Aim.Zone.RadiusM, colour, 1.4f, a.Aim.Kind == AimKind.Sink ? Dash.Dashed : Dash.Solid);
            var top = centre;
            var north = Earth.Forward(top, 0);
            Earth.Advance(ref top, ref north, a.Aim.Zone.RadiusM);
            if (view.Project(top, out double x, out double y))
                zoneLabels.Add((x, y, i, colour));
        }
    }

    /// <summary>Each zone's name, centred above its ring.</summary>
    void ZoneLabels()
    {
        if (zoneLabels.Count == 0)
            return;
        var vp = ImGui.GetMainViewport();
        float sc = UiFonts.Scale, sx = vp.Size.X / (float)Math.Max(width, 1), sy = vp.Size.Y / (float)Math.Max(height, 1);
        var dl = ImGui.GetBackgroundDrawList();
        ImGui.PushFont(UiFonts.SansSmall);
        foreach (var (x, y, name, colour) in zoneLabels)
        {
            string text = zoneNames[name];
            var size = ImGui.CalcTextSize(text);
            var at = vp.Pos + new Vector2((float)x * sx - size.X / 2, (float)y * sy - size.Y - 4 * sc);
            MapLabels.Text(dl, at, colour with { W = 0.9f }, text, 1);
        }
        ImGui.PopFont();
    }
}
